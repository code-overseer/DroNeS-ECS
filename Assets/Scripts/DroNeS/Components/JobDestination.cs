using Unity.Entities;
using Unity.Mathematics;

namespace DroNeS.Components
{
    public struct JobDestination : IComponentData
    {
        public float3 Value;
    }
}