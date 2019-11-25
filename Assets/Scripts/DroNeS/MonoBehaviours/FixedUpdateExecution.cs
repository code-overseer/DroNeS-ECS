using System.Collections.Generic;
using DroNeS.Systems.FixedUpdates;
using Unity.Entities;
using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public class FixedUpdateExecution : MonoBehaviour
    {
        // Start is called before the first frame update
        private IEnumerable<ComponentSystemBase> _fixedUpdateSystems;

        private void Start()
        {
            World.Active.GetOrCreateSystem<FixedUpdateGroup>().Enabled = false;
            _fixedUpdateSystems = World.Active.GetOrCreateSystem<FixedUpdateGroup>().Systems;
        }
        private void FixedUpdate()
        {
            foreach(var system in _fixedUpdateSystems)
            {
                system.Update();
            }
        }
        
    }
}
