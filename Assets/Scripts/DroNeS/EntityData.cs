using DroNeS.ScriptableObjects;
using UnityEngine;

namespace DroNeS
{
    public static class EntityData
    {
        private static HubEntity _hub;
        private static DroneEntity _drone;
        private static BuildingColliderEntity _building;

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
        
        public static BuildingColliderEntity BuildingCollider
        {
            get
            {
                if (_building != null) return _building;
                _building = Resources.Load("EntityData/BuildingColliderData") as BuildingColliderEntity;
                return _building;
            }
        }
        
        
    }
}
