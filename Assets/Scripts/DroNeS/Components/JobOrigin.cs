using Unity.Entities;
using Unity.Mathematics;

namespace DroNeS.Components
{
    public struct JobOrigin : IComponentData
    {
        public float3 Value;
    }
}
