using DroNeS.ScriptableObjects;
using UnityEngine;

namespace DroNeS
{
    public static class EntityData
    {
        private static HubEntity _hub;
        private static DroneEntity _drone;

        public static HubEntity Hub
        {
            get
            {
                if (_hub != null) return _hub;
                _hub = Resources.Load("EntityData/HubData") as HubEntity;
                return _hub;
            }
        }
        
        public static DroneEntity Drone
        {
            get
            {
                if (_drone != null) return _drone;
                _drone = Resources.Load("EntityData/DroneData") as DroneEntity;
                return _drone;
            }
        }
        
        
    }
}
