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
        private readonly List<Routine> _awaitingActivation = new List<Routine>();
        public static int Count => Instance._awaitingActivation.Count + Instance._routines.Count;
        private const int MaxRoutines = 32;
        private int _nextId = 1;
        
        public static int Run(IEnumerator routine)
        {
            var r = new Routine(routine);
            return r.Id;
        }
        
        public static void Stop(int id)
        {
            if (Instance._routines.TryGetValue(id, out var r))
                r.Stop = true;
        }
        
        public static bool IsRunning(int id) => Instance._routines.ContainsKey(id);

#if UNITY_EDITOR
        private static bool _editorCoroutineManager;
        
        public static void EnableCoroutineManagerInEditor()
        {
            if (_editorCoroutineManager) return;
            _editorCoroutineManager = true;
            UnityEditor.EditorApplication.update += UpdateCoroutineManager;
        }

        private static void UpdateCoroutineManager()
        {
            if (!Application.isPlaying)
            {
                Instance.UpdateRoutines();
            }
        }
#endif
        
        private class Routine : IEnumerator
        {
            public int Id { get; }
            public bool Stop { private get; set; }
            private bool _canMoveNext;
            private readonly IEnumerator _enumerator;

            public Routine(IEnumerator enumerator)
            {
                _enumerator = enumerator;
                Id = Instance._nextId++;
                Stop = false;

                if (Instance._routines.Count < MaxRoutines)
                {
                    Instance.StartCoroutine(this);
                    Instance._routines[Id] = this;
                }
                else
                {
                    Instance._awaitingActivation.Add(this);
                }
                
            }

            public object Current => _enumerator.Current;

            public bool MoveNext()
            {
                _canMoveNext = _enumerator.MoveNext();
                
                if (_canMoveNext && Stop) _canMoveNext = false;
                if (_canMoveNext) return _canMoveNext;
                
                Instance._routines.Remove(Id);
                var n = Instance._awaitingActivation.Count;
                
                if (n <= 0) return _canMoveNext;
                
                var last = Instance._awaitingActivation[n - 1];
                Instance.StartCoroutine(last);
                Instance._routines[last.Id] = last;
                Instance._awaitingActivation.RemoveAt(n - 1);
                
                return _canMoveNext;
            }

            public void Reset()
            {
                _enumerator.Reset();
            }
        }

        private void UpdateRoutines()
        {
            if (_routines.Count <= 0) return;
            // we are not in play mode, so we must manually update our co-routines ourselves
            var routines = new List<Routine>();
            
            foreach (var kp in _routines) routines.Add(kp.Value);

            foreach (var r in routines) r.MoveNext();
        }
    }
}
