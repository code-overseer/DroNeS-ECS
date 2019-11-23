using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace DroNeS.Systems
{
    [UpdateAfter(typeof(ClickReceiverSystem)), UpdateBefore(typeof(EventSystem))]
    public class ClickProcessingSystem : JobComponentSystem
    {
        private EventSystem _eventSystem;
        protected override void OnCreate()
        {
            base.OnCreate();
            _eventSystem = World.Active.GetOrCreateSystem<EventSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle input)
        {
            var output = _eventSystem.GetReader<ClickEvent>(input, out var reader);
            
            output = new ProcessJob
             {
                 Reader = reader
             }.Schedule(output);
             _eventSystem.AddConsumerJobHandle<ClickEvent>(output);

             return output;
        }
        
//        [BurstCompile]
        private struct ProcessJob : IJob
        {
            public NativeStream.Reader Reader;
            public void Execute()
            {
                if (Reader.ComputeItemCount() < 1) return;
                Reader.BeginForEachIndex(0);
                var i = Reader.Read<ClickEvent>().Entity.Index;
                Debug.Log($"Entity {i} Clicked!");
                Reader.EndForEachIndex();
            }
        }
    }
}
