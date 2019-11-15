using System;
using DroNeS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace DroNeS.Systems
{
    public class JobGeneratorBarrierSystem : EntityCommandBufferSystem
    {
    }
    public class JobGeneratorSystem : JobComponentSystem
    {
        private static EntityArchetype _job;
        private static JobGeneratorBarrierSystem _barrier;
        private static int _jobUid;

        protected override void OnCreate()
        {
            base.OnCreate();
            _job = World.Active.EntityManager.CreateArchetype(ComponentType.ReadOnly<JobTag>(),
                ComponentType.ReadOnly<JobOrigin>(),
                    ComponentType.ReadOnly<JobDestination>(),
                    ComponentType.ReadOnly<JobCreationTime>());
            _barrier = World.Active.GetOrCreateSystem<JobGeneratorBarrierSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
//            var buffer = _barrier.CreateCommandBuffer();
//            var job = new JobGenerator
//            {
//                JobCreation = buffer.ToConcurrent(),
//                CurrentTime = 1.0f // TODO
//            };
//            return job.Schedule(this, inputDeps);
            return inputDeps;
        }
        
        [BurstCompile]
        private struct JobGenerator : IJobForEach<HubUID, JobGenerationRate, JobGenerationTimeMark>
        {
            [WriteOnly] public EntityCommandBuffer.Concurrent JobCreation;
            public float CurrentTime;
            public void Execute(ref HubUID hub, ref JobGenerationRate rate, ref JobGenerationTimeMark mark)
            {
                if (CurrentTime - mark.Value.x < mark.Value.y) return;
                var f = new Random();
                var job = JobCreation.CreateEntity(hub.Value, _job);
                var pos = new float3(f.NextFloat(-200, 200), f.NextFloat(-200, 200), f.NextFloat(-200, 200));
                JobCreation.SetComponent(hub.Value, job, new JobOrigin{Value = pos});
                pos = new float3(f.NextFloat(-200, 200), f.NextFloat(-200, 200), f.NextFloat(-200, 200));
                JobCreation.SetComponent(hub.Value, job, new JobDestination{Value = pos});
                JobCreation.SetComponent(hub.Value, job, new JobCreationTime{Value = CurrentTime});
                mark.Value.x = CurrentTime;
                mark.Value.y = -math.log(1 - f.NextFloat(0, 1)) / rate.Value;
            }
        }
    }
}
