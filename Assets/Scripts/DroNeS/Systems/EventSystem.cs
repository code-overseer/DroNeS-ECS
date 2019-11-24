using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Assertions;
using Utils;

namespace DroNeS.Systems
{
    [UpdateAfter(typeof(ClickProcessingSystem))]
    public class EventSystem : ComponentSystem
    {
        private int _eventCount;
        private readonly Dictionary<Type, int> _handleKeys = new Dictionary<Type, int>();
        private readonly Dictionary<int, NativeStream> _eventCollection = new Dictionary<int, NativeStream>();
        private NativeHashMap<int, JobHandle> _producerHandles;
        private NativeHashMap<int, JobHandle> _consumerHandles;
        private EndSimulationEntityCommandBufferSystem _barrier;
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
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
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
            ResetHandles(key);
        }

        private void ResetHandles(int key)
        {
            _consumerHandles[key] = default;
            _producerHandles[key] = default;
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
            var key = _handleKeys[typeof(T)];
            readers = _eventCollection[key].AsReader();
            return JobHandle.CombineDependencies(_producerHandles[key], dependencies);
        }
        
        public void AddConsumerJobHandle<T>(JobHandle consumer) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            if (!_consumerHandles.TryGetValue(key, out var handle))
            {
                _consumerHandles.TryAdd(key, consumer);
                return;
            }
            _consumerHandles[key] = JobHandle.CombineDependencies(handle, consumer);
        }
        
        protected override void OnUpdate()
        {
            JobHandle output = default;
            foreach (var key in _handleKeys.Values)
            {
                output = JobHandle.CombineDependencies(
                    new ClearStreamJob(_eventCollection[key]).Schedule(_consumerHandles[key]), output);

                ResetHandles(key);
            }
            _barrier.AddJobHandleForProducer(output);
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
