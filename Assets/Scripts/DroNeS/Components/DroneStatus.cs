using Unity.Entities;
using UnityEngine;

namespace DroNeS.Components
{
    public struct DroneStatus : IComponentData
    {
        public Status Value;
    }
}
