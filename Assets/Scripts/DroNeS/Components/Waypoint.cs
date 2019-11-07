using Unity.Entities;
using Unity.Mathematics;
// ReSharper disable InconsistentNaming

namespace DroNeS.Components
{
    public struct Waypoint : IComponentData
    {
        public float3 waypoint;
        public int index;
        public int length;
        
        public Waypoint(float3 point, int  index, int length)
        {
            waypoint = point;
            this.index = index;
            this.length = length;
        }
    }
}