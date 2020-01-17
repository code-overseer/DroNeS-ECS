using DroNeS.Components;
using DroNeS.Components.Tags;
using DroNeS.Systems.EventSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace DroNeS.Systems
{
    [DisableAutoCreation]
    [UpdateAfter(typeof(JobDataGeneratorSystem))]
    public class JobEntityGeneratorSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem _barrier;
        private MessagePassingSystem _eventSystem;
        protected override void OnCreate()
        {
            base.OnCreate();
            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _eventSystem = World.GetOrCreateSystem<MessagePassingSystem>();
            JobSpawner.Job = World.EntityManager.CreateArchetype(
                ComponentType.ReadOnly<JobTag>(),
                ComponentType.ReadOnly<JobUID>(),
                ComponentType.ReadOnly<JobOrigin>(),
                ComponentType.ReadOnly<JobDestination>(),
                ComponentType.ReadOnly<JobCreationTime>(),
                ComponentType.ReadOnly<CostFunction>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var (reader, count) = _eventSystem.GetReader<JobEntityProxy>(ref inputDeps);
            var job = new JobSpawner
            {
                Buffer = _barrier.CreateCommandBuffer().ToConcurrent(),
                ToSpawn = reader
            };
            
            var handle = job.Schedule(count, 16, inputDeps);
            _eventSystem.AddConsumerJobHandle<JobEntityProxy>(handle);
            _barrier.AddJobHandleForProducer(handle);

            return handle;
        }

        private struct JobSpawner : IJobParallelFor
        {
            [WriteOnly] public EntityCommandBuffer.Concurrent Buffer;
            [ReadOnly] public NativeStream.Reader ToSpawn;
            public static EntityArchetype Job;

            public void Execute(int index)
            {
                ToSpawn.BeginForEachIndex(index);
                var n = ToSpawn.RemainingItemCount;
                for (var i = 0; i < n; ++i)
                {
                    var data = ToSpawn.Read<JobEntityProxy>();
                    var entity = Buffer.CreateEntity(index, Job);
                    Buffer.SetComponent(index, entity, data.id);
                    Buffer.SetComponent(index, entity, data.created);
                    Buffer.SetComponent(index, entity, data.origin);
                    Buffer.SetComponent(index, entity, data.destination);
                    Buffer.SetComponent(index, entity, data.costFunction);
                    Buffer.SetSharedComponent(index, entity, data.hub);
                }
                ToSpawn.EndForEachIndex();
            }
        }
    }
}
