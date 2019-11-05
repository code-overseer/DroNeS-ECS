using Unity.Entities;
using Unity.Mathematics;
// ReSharper disable InconsistentNaming

namespace DroNeS.Components
{
    public struct Waypoint : IComponentData
    {
        public float3 waypoint;

        public Waypoint(float3 point)
        {
            waypoint = point;
        }

        public static implicit operator Waypoint(float3 val)
        {
            return new Waypoint(val);
        }
        
        public static implicit operator float3(Waypoint val)
        {
            return new float3(val.waypoint);
        }
    }
}