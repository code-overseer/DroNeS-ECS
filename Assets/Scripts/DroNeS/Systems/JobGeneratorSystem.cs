using DroNeS.Components;
using DroNeS.SharedComponents;
using DroNeS.Systems.EventSystem;
using DroNeS.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Clock = DroNeS.Components.Singletons.Clock;
using Random = Unity.Mathematics.Random;
using SimulationType = DroNeS.Components.Singletons.SimulationType;

namespace DroNeS.Systems
{
    public struct JobEntityProxy
    {
        public JobUID id;
        public JobCreationTime created;
        public JobOrigin origin;
        public JobDestination destination;
        public CostFunction costFunction;
        public ParentHub hub;
    }
    [DisableAutoCreation]
    [UpdateAfter(typeof(BuildPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
    public class JobGeneratorSystem : JobComponentSystem
    {
        private InterStreamingSystem _eventSystem;
        private BuildPhysicsWorld _buildPhysicsWorld;
        private EndFramePhysicsSystem _endFramePhysicsSystem;
        private EntityQuery _query;
        private Random _rand;
        protected override void OnCreate()
        {
            base.OnCreate();
            _eventSystem = World.GetOrCreateSystem<InterStreamingSystem>();
            _query = GetEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<HubUID>(),
                        typeof(Translation),
                        typeof(JobGenerationCounter),
                        typeof(JobGenerationRate),
                        typeof(JobGenerationTimeMark),
                    }
                }
            );
            EntityManager.CreateEntity(typeof(SimulationType));
            SetSingleton(new SimulationType{Value = SimulationTypeValue.Delivery});
            _eventSystem.NewEvent<JobEntityProxy>(4);
            _rand = new Random(1u);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _endFramePhysicsSystem = World.GetOrCreateSystem<EndFramePhysicsSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            inputDeps = JobHandle.CombineDependencies(inputDeps, _buildPhysicsWorld.FinalJobHandle);
            var job = new JobGeneratorChunk
            {
                CreatedJobs = _eventSystem.GetWriter<JobEntityProxy>(ref inputDeps, _query.CalculateChunkCount()),
                World = _buildPhysicsWorld.PhysicsWorld.CollisionWorld,
                CurrentTime = GetSingleton<Clock>().Value,
                SimulationType = GetSingleton<SimulationType>().Value,
                HubUids = GetArchetypeChunkComponentType<HubUID>(true),
                Positions = GetArchetypeChunkComponentType<Translation>(true),
                Rates = GetArchetypeChunkComponentType<JobGenerationRate>(true),
                Counters = GetArchetypeChunkComponentType<JobGenerationCounter>(),
                Marks = GetArchetypeChunkComponentType<JobGenerationTimeMark>(),
                Rand = _rand
            };
            var handle = job.Schedule(_query, inputDeps);
            _eventSystem.AddProducerJobHandle<JobEntityProxy>(handle);
            _endFramePhysicsSystem.HandlesToWaitFor.Add(handle);

            return handle;
        }
        
        [BurstCompile]
        private struct JobGeneratorChunk : IJobChunk
        {
            [WriteOnly] public NativeStream.Writer CreatedJobs;
            [ReadOnly] public CollisionWorld World;
            [ReadOnly] public ArchetypeChunkComponentType<HubUID> HubUids;
            [ReadOnly] public ArchetypeChunkComponentType<JobGenerationRate> Rates;
            [ReadOnly] public ArchetypeChunkComponentType<Translation> Positions;
            public ArchetypeChunkComponentType<JobGenerationCounter> Counters;
            public ArchetypeChunkComponentType<JobGenerationTimeMark> Marks;
            public double CurrentTime;
            public SimulationTypeValue SimulationType;
            public Random Rand;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var hubUids = chunk.GetNativeArray(HubUids);
                var counters = chunk.GetNativeArray(Counters);
                var rates = chunk.GetNativeArray(Rates);
                var marks = chunk.GetNativeArray(Marks);
                var pos = chunk.GetNativeArray(Positions);

                for (var i = 0; i < chunk.Count; ++i)
                {
                    if (CurrentTime - marks[i].Value.x < marks[i].Value.y) continue;

                    var val = counters[i].Value + 1;
                    counters[i] = new JobGenerationCounter{Value = val};
                    var mark = marks[i].Value;
                    mark.x = CurrentTime;
                    mark.y = -math.log(1 - Rand.NextFloat(0, 1)) / rates[i].Value;
                    marks[i] = new JobGenerationTimeMark{Value = mark};
                    var origin = pos[i].Value;
                    var destination = GetDestination();
                    var dist = math.lengthsq(destination - new float3(origin.x, 0, origin.z));
                    var input = new RaycastInput
                    {
                        Start = new float3(destination.x, 2000, destination.z),
                        End = destination,
                        Filter = new CollisionFilter
                        {
                            BelongsTo = CollisionGroups.Cast,
                            CollidesWith = CollisionGroups.Buildings
                        }
                    };
                    while (dist < 10000 || World.CastRay(input, out _))
                    {
                        destination = GetDestination();
                        dist = math.lengthsq(destination - new float3(origin.x, 0, origin.z));
                        input.Start = new float3(destination.x, 2000, destination.z);
                        input.End = destination;
                    }
                    CreatedJobs.BeginForEachIndex(chunkIndex);
                    CreatedJobs.Write(new JobEntityProxy
                    {
                        id = new JobUID {Value = counters[i].Value},
                        created = new JobCreationTime{Value = CurrentTime},
                        origin = new JobOrigin{Value = pos[i].Value},
                        destination = new JobDestination{Value = destination},
                        costFunction = SimulationType == SimulationTypeValue.Delivery
                            ? CostFunction.GetDelivery(Reward())
                            : CostFunction.GetEmergency(Rand),
                        hub = new ParentHub{Value = hubUids[i].Value }
                    });
                    CreatedJobs.EndForEachIndex();
                }

            }

            private float3 GetDestination()
            {
                var circle = Rand.InsideUnitCircle() * (SimulationType == SimulationTypeValue.Delivery ? 7000 : 3500);
                return new float3(circle.x, 0, circle.y);
            }
            private float Reward()
            {
                var weight = Rand.NextFloat(0.1f, 2.5f);
                if (weight <= 0.25) return 2.02f;
                if (weight <= 0.5) return 2.14f;
                if (weight <= 1) return 2.30f;
                if (weight <= 1.5) return 2.45f;
                if (weight <= 2) return 2.68f;
                if (weight <= 4) return 3.83f;

                var oz = weight * 35.274f;
                if (oz <= 10) return Rand.NextFloat(0,1) < 0.5f ? 2.41f : 3.19f;
                if (oz <= 16) return Rand.NextFloat(0,1) < 0.5f ? 2.49f : 3.28f;

                var lbs = weight * 2.204625f;
                if (lbs <= 2) return 4.76f;
                if (lbs <= 3) return 5.26f;
                return 5.26f + (lbs - 3) * 0.38f;
            }
        }
    }
}
