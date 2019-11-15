using Unity.Entities;
using Unity.Mathematics;

namespace DroNeS.Components
{
    public struct JobGenerationTimeMark : IComponentData
    {
        public float2 Value;
    }
}