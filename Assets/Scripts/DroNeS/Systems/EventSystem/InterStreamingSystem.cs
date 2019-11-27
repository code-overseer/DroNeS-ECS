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

namespace DroNeS.Systems.EventSystem
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class InterStreamingSystem : ComponentSystem
    {
        private struct StreamPair
        {
            public NativeStream Stream;
            public int ForEachCount;

            public StreamPair(int count)
            {
                Stream = new NativeStream(count, Allocator.Persistent);
                ForEachCount = count;
            }
        }
        private int _eventCount;
        private readonly Dictionary<Type, int> _handleKeys = new Dictionary<Type, int>();
        private readonly Dictionary<int, StreamPair> _eventCollection = new Dictionary<int, StreamPair>();
        private readonly List<NativeStream> _toDispose = new List<NativeStream>(16);
        private NativeHashMap<int, JobHandle> _producerHandles;
        private NativeHashMap<int, JobHandle> _consumerHandles;
        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var stream in _eventCollection.Values)
            {
                stream.Stream.Dispose();
            }
            
            _producerHandles.Dispose();
            _consumerHandles.Dispose();
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _producerHandles = new NativeHashMap<int, JobHandle>(10, Allocator.Persistent);
            _consumerHandles = new NativeHashMap<int, JobHandle>(10, Allocator.Persistent);
        }

        public void NewEvent<T>(int count)
        {
            Assert.AreNotEqual(0, count);
            if (_handleKeys.ContainsKey(typeof(T))) return;
            
            var key = _handleKeys[typeof(T)] = ++_eventCount;
            _eventCollection[key] = new StreamPair(count);
            ResetHandles(key);
        }

        private void ResetHandles(int key)
        {
            _consumerHandles[key] = default;
            _producerHandles[key] = default;
        }

        public NativeStream.Writer GetWriter<T>(ref JobHandle dependencies, int count) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            dependencies = JobHandle.CombineDependencies(_consumerHandles[key], _producerHandles[key], dependencies);
            if (_eventCollection[key].ForEachCount >= count)
            {
                dependencies = new ClearStreamJob(_eventCollection[key].Stream).Schedule(dependencies);
                return _eventCollection[key].Stream.AsWriter();
            }
            dependencies.Complete();
            _toDispose.Add(_eventCollection[key].Stream);
            _eventCollection[key] = new StreamPair(2 * count);
            return _eventCollection[key].Stream.AsWriter();
        }

        public NativeStream.Writer GetWriter<T>(ref JobHandle dependencies) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            dependencies = JobHandle.CombineDependencies(
                new ClearStreamJob(_eventCollection[key].Stream).Schedule(_consumerHandles[key]), dependencies);
            
            return _eventCollection[key].Stream.AsWriter();
        }

        public void AddProducerJobHandle<T>(JobHandle producer) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            _producerHandles[key] = JobHandle.CombineDependencies(_producerHandles[key], producer);
        }
        
        public (NativeStream.Reader, int) GetReader<T>(ref JobHandle dependencies) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            dependencies = JobHandle.CombineDependencies(dependencies, _producerHandles[key]);
            var output = _eventCollection[key];
            return (output.Stream.AsReader(), output.ForEachCount);
        }

        public void AddConsumerJobHandle<T>(JobHandle consumer) where T : struct
        {
            var key = _handleKeys[typeof(T)];
            _consumerHandles[key] = JobHandle.CombineDependencies(_consumerHandles[key], consumer);
        }
        
        protected override void OnUpdate()
        {
            if (_toDispose.Count < 1) return;
            foreach (var stream in _toDispose)
            {
                stream.Dispose();
            }
            _toDispose.Clear();
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
