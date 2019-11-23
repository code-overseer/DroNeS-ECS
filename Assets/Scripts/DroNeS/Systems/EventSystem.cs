using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Assertions;

namespace DroNeS.Systems
{
    public class EventSystem : ComponentSystem
    {
        private readonly Dictionary<Type, NativeStream> _eventCollection = new Dictionary<Type, NativeStream>();

        public NativeStream.Writer GetWriter<T>(int count) where T : struct
        {
            Assert.AreNotEqual(0, count);
            Assert.IsFalse(_eventCollection.ContainsKey(typeof(T)));
            var stream = _eventCollection[typeof(T)] = new NativeStream(count, Allocator.TempJob);
            return stream.AsWriter();
        }
        
        public NativeStream.Reader GetReader<T>()
        {
            if (_eventCollection.TryGetValue(typeof(T), out var stream))
            {
                return stream.AsReader();
            }
            throw new NullReferenceException("No such reader");
        }
        
        protected override void OnUpdate()
        {
            throw new System.NotImplementedException();
        }
    }
}
