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
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
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
            if (_handleKeys.ContainsKey(typeof(T))) return;
            
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
            _producerHandles[key] = JobHandle.CombineDependencies(_producerHandles[key], producer);
        }
        
        public NativeStream.Reader GetReader<T>(ref JobHandle dependencies) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            dependencies = JobHandle.CombineDependencies(dependencies, _producerHandles[key]);
            return _eventCollection[key].AsReader();
        }

        public void AddConsumerJobHandle<T>(JobHandle consumer) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            _consumerHandles[key] = JobHandle.CombineDependencies(_consumerHandles[key], consumer);
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
