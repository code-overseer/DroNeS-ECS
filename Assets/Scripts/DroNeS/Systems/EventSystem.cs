using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Assertions;
using Utils;
using Debug = UnityEngine.Debug;

namespace DroNeS.Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class EventBarrierSystem : EntityCommandBufferSystem
    {
        public JobHandle TerminationHandle;
        protected override void OnUpdate()
        {
            base.OnUpdate();
            TerminationHandle.Complete();
        }
    }
    
    [UpdateAfter(typeof(ClickProcessingSystem))]
    public class EventSystem : JobComponentSystem
    {
        private int _eventCount;
        private readonly Dictionary<Type, int> _handleKeys = new Dictionary<Type, int>();
        private readonly Dictionary<int, NativeStream> _eventCollection = new Dictionary<int, NativeStream>();
        private NativeHashMap<int, JobHandle> _producerHandles;
        private NativeHashMap<int, JobHandle> _consumerHandles;
        private EventBarrierSystem _barrier;
        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var stream in _eventCollection.Values)
            {
                stream.Dispose();
            }
            
            _producerHandles.Dispose();
            _consumerHandles.Dispose();
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _producerHandles = new NativeHashMap<int, JobHandle>(10, Allocator.Persistent);
            _consumerHandles = new NativeHashMap<int, JobHandle>(10, Allocator.Persistent);
            _barrier = World.Active.GetOrCreateSystem<EventBarrierSystem>();
        }

        public void NewEvent<T>(int count)
        {
            Assert.AreNotEqual(0, count);
            if (_handleKeys.ContainsKey(typeof(T)))
            {
                throw new Exception("Event already exists");
            }
            var key = _handleKeys[typeof(T)] = ++_eventCount;
            _eventCollection[key] = new NativeStream(count, Allocator.Persistent);
        }

        public NativeStream.Writer GetWriter<T>() where T : struct
        {
            return _eventCollection[_handleKeys[typeof(T)]].AsWriter();
        }

        public void AddProducerJobHandle<T>(JobHandle producer) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            if (!_producerHandles.TryGetValue(key, out var handle))
            {
                _producerHandles.TryAdd(key, producer);
                return;
            }
            _producerHandles[key] = JobHandle.CombineDependencies(handle, producer);
        }
        
        public JobHandle GetReader<T>(JobHandle dependencies, out NativeStream.Reader readers) where T : struct
        {
            readers = _eventCollection[_handleKeys[typeof(T)]].AsReader();
            return JobHandle.CombineDependencies(_producerHandles[_handleKeys[typeof(T)]], dependencies);
        }
        
        public void AddConsumerJobHandle<T>(JobHandle handlers) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            if (!_consumerHandles.TryGetValue(key, out var handle))
            {
                _consumerHandles.TryAdd(key, handlers);
                return;
            }
            _consumerHandles[key] = JobHandle.CombineDependencies(handle, handlers);
        }
        
        protected override JobHandle OnUpdate(JobHandle input)
        {
            JobHandle output = default;
            
            foreach (var key in _handleKeys.Values)
            {
                _consumerHandles[key] = JobHandle.CombineDependencies(input, _consumerHandles[key]);
                output = JobHandle.CombineDependencies(
                    new ClearStreamJob(_eventCollection[key]).Schedule(_consumerHandles[key]),
                    output);

                _consumerHandles[key] = default;
                _producerHandles[key] = default;
            }

            _barrier.TerminationHandle = output;
            return output;
        }
        
        [BurstCompile]
        private struct ClearStreamJob : IJob
        {
            private readonly NativeStream _stream;
            public ClearStreamJob(NativeStream stream)
            {
                _stream = stream;
            }
            public void Execute()
            {
                _stream.Clear();
            }
        }
    }
}
