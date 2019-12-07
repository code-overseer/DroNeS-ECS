using System.Collections;
using System.Collections.Generic;
using Mapbox.Unity.Utilities;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{
    public class CoroutineManager : MonoBehaviour
    {
        private static CoroutineManager Instance => Singleton<CoroutineManager>.Instance;
        private readonly Dictionary<int, Routine> _routines = new Dictionary<int, Routine>();
        private readonly Queue<Routine> _routineQueue = new Queue<Routine>();
        public static int ActiveCount => Instance._routines.Count;
        public static int Count => Instance._routineQueue.Count + Instance._routines.Count;
        private const int MaxRoutines = 16;
        private int _nextId = 1;
        
        public static void Run(IEnumerator routine, string name = "")
        {
            var r = new Routine(routine, name);
        }

        public static bool IsRunning(int id) => Instance._routines.ContainsKey(id);

        private class Routine : IEnumerator
        {
            private int Id { get; }

            private readonly IEnumerator _enumerator;

            private string _name;

            public Routine(IEnumerator enumerator, string name)
            {
                _enumerator = enumerator;
                _name = name;
                Id = Instance._nextId++;

                if (Instance._routines.Count < MaxRoutines)
                {
                    Instance._routines.Add(Id, this);
                    Instance.StartCoroutine(this);
                }
                else
                {
                    Instance._routineQueue.Enqueue(this);
                }
                
            }

            public object Current => _enumerator.Current;

            public bool MoveNext()
            {
                if (_enumerator.MoveNext()) return true;
                if (!string.IsNullOrEmpty(_name)) Debug.Log($"{_name} ended");
                
                Instance._routines.Remove(Id);
                if (Instance._routineQueue.Count <= 0) return false;
                
                var next = Instance._routineQueue.Dequeue();
                Instance._routines.Add(next.Id, next);
                Instance.StartCoroutine(next);
                return false;
            }

            public void Reset()
            {
                _enumerator.Reset();
            }
        }
    }
}
