using DroNeS.Components;
using DroNeS.SharedComponents;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace DroNeS.Systems
{
    [UpdateAfter(typeof(SunOrbitSystem))]
    public class JobGeneratorSystem : JobComponentSystem
    {
        private static EndSimulationEntityCommandBufferSystem _barrier;

        protected override void OnCreate()
        {
            base.OnCreate();
            JobGenerator.Job = World.Active.EntityManager.CreateArchetype(ComponentType.ReadOnly<JobTag>(),
                ComponentType.ReadOnly<JobUID>(),
                ComponentType.ReadOnly<JobOrigin>(),
                ComponentType.ReadOnly<JobDestination>(),
                ComponentType.ReadOnly<JobCreationTime>());
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new JobGenerator
            {
                JobCreation = _barrier.CreateCommandBuffer().ToConcurrent(),
                CurrentTime = SunOrbitSystem.Clock
            };
            inputDeps = job.Schedule(this, inputDeps);
            _barrier.AddJobHandleForProducer(inputDeps);
            return inputDeps;
        }
        
        [BurstCompile]
        private struct JobGenerator : IJobForEach<HubUID, JobGenerationCounter, JobGenerationRate, JobGenerationTimeMark>
        {
            [WriteOnly] public EntityCommandBuffer.Concurrent JobCreation;
            public double CurrentTime;
            public static EntityArchetype Job;
            public void Execute(ref HubUID hub, ref JobGenerationCounter counter, ref JobGenerationRate rate, ref JobGenerationTimeMark mark)
            {
                if (CurrentTime - mark.Value.x < mark.Value.y) return;
                var f = new Random();
                var job = JobCreation.CreateEntity(hub.Value, Job);
                counter.Value += 1;
                JobCreation.SetComponent(hub.Value, job, new JobUID {Value = counter.Value});
                var pos = new float3(f.NextFloat(-200, 200), f.NextFloat(-200, 200), f.NextFloat(-200, 200));
                JobCreation.SetComponent(hub.Value, job, new JobOrigin{Value = pos});
                pos = new float3(f.NextFloat(-200, 200), f.NextFloat(-200, 200), f.NextFloat(-200, 200));
                JobCreation.SetComponent(hub.Value, job, new JobDestination{Value = pos});
                JobCreation.SetComponent(hub.Value, job, new JobCreationTime{Value = CurrentTime});
                JobCreation.AddSharedComponent(hub.Value, job, new ParentHub{ Value = hub.Value });
                mark.Value.x = CurrentTime;
                mark.Value.y = -math.log(1 - f.NextFloat(0, 1)) / rate.Value;
            }
        }
    }
}
