using Unity.Entities;
using UnityEngine;

namespace DroNeS.Components
{
    public struct DroneStatus : IComponentData
    {
        public Status Value;

        public DroneStatus(Status stat)
        {
            Value = stat;
        }

        public static implicit operator DroneStatus(Status stat)
        {
            return new DroneStatus(stat);
        }
        
    }
}
