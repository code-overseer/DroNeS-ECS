using DroNeS.Components;
using DroNeS.Components.Tags;
using DroNeS.SharedComponents;
using DroNeS.Systems.FixedUpdates;
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
            JobGenerator.Job = World.Active.EntityManager.CreateArchetype(ComponentType.ReadOnly<JobTag>(),
                ComponentType.ReadOnly<JobUID>(),
                ComponentType.ReadOnly<JobOrigin>(),
                ComponentType.ReadOnly<JobDestination>(),
                ComponentType.ReadOnly<JobCreationTime>());
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var output = new JobGenerator
            {
                JobCreation = _barrier.CreateCommandBuffer().ToConcurrent(),
                CurrentTime = (float)GetSingleton<Clock>().Value
            }.Schedule(this, inputDeps);
            
            _barrier.AddJobHandleForProducer(output);
            
            return output;
        }
        
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
