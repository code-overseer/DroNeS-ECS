using DroNeS.Components;
using DroNeS.Components.Singletons;
using DroNeS.Components.Tags;
using DroNeS.SharedComponents;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Clock = DroNeS.Components.Singletons.Clock;
using Random = Unity.Mathematics.Random;

namespace DroNeS.Systems
{
    public class JobGeneratorSystem : JobComponentSystem
    {
        private static EndSimulationEntityCommandBufferSystem _barrier;
        protected override void OnCreate()
        {
            base.OnCreate();
            JobGenerator.Job = World.Active.EntityManager.CreateArchetype(
                ComponentType.ReadOnly<JobTag>(),
                ComponentType.ReadOnly<JobUID>(),
                ComponentType.ReadOnly<JobOrigin>(),
                ComponentType.ReadOnly<JobDestination>(),
                ComponentType.ReadOnly<JobCreationTime>(),
                ComponentType.ReadOnly<CostFunction>());
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            EntityManager.CreateEntity(typeof(SimulationType));
            SetSingleton(new SimulationType{Value = SimulationTypeValue.Delivery});
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var output = new JobGenerator
            {
                JobCreation = _barrier.CreateCommandBuffer().ToConcurrent(),
                CurrentTime = (float)GetSingleton<Clock>().Value,
                SimulationType = GetSingleton<SimulationType>().Value
            }.Schedule(this, inputDeps);
            
            _barrier.AddJobHandleForProducer(output);
            
            return output;
        }
        
        private struct JobGenerator : IJobForEach<HubUID, JobGenerationCounter, JobGenerationRate, JobGenerationTimeMark>
        {
            [WriteOnly] public EntityCommandBuffer.Concurrent JobCreation;
            public double CurrentTime;
            public static EntityArchetype Job;
            private static Random _rand = new Random(1u);
            public SimulationTypeValue SimulationType;
            public void Execute(ref HubUID hub, ref JobGenerationCounter counter, ref JobGenerationRate rate, ref JobGenerationTimeMark mark)
            {
                if (CurrentTime - mark.Value.x < mark.Value.y) return;

                var job = JobCreation.CreateEntity(hub.Value, Job);
                counter.Value += 1;
                JobCreation.SetComponent(hub.Value, job, new JobUID {Value = counter.Value});
                JobCreation.SetComponent(hub.Value, job, new JobCreationTime{Value = CurrentTime});
                
                var pos = new float3(_rand.NextFloat(-200, 200), _rand.NextFloat(-200, 200), _rand.NextFloat(-200, 200));
                JobCreation.SetComponent(hub.Value, job, new JobOrigin{Value = pos});
                
                pos = new float3(_rand.NextFloat(-200, 200), _rand.NextFloat(-200, 200), _rand.NextFloat(-200, 200));
                JobCreation.SetComponent(hub.Value, job, new JobDestination{Value = pos});

                JobCreation.SetComponent(hub.Value, job,
                    SimulationType == SimulationTypeValue.Delivery
                        ? CostFunction.GetDelivery(Reward())
                        : CostFunction.GetEmergency());

                JobCreation.AddSharedComponent(hub.Value, job, new ParentHub{ Value = hub.Value });
                mark.Value.x = CurrentTime;
                mark.Value.y = -math.log(1 - _rand.NextFloat(0, 1)) / rate.Value;
            }
            
            private static float Reward()
            {
                var weight = _rand.NextFloat(0.1f, 2.5f);
                if (weight <= 0.25) return 2.02f;
                if (weight <= 0.5) return 2.14f;
                if (weight <= 1) return 2.30f;
                if (weight <= 1.5) return 2.45f;
                if (weight <= 2) return 2.68f;
                if (weight <= 4) return 3.83f;

                var oz = weight * 35.274f;
                if (oz <= 10) return _rand.NextFloat(0,1) < 0.5f ? 2.41f : 3.19f;
                if (oz <= 16) return _rand.NextFloat(0,1) < 0.5f ? 2.49f : 3.28f;

                var lbs = weight * 2.204625f;
                if (lbs <= 2) return 4.76f;
                if (lbs <= 3) return 5.26f;
                return 5.26f + (lbs - 3) * 0.38f;
            }
        }
    }
}
