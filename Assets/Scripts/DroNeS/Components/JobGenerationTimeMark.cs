using Unity.Entities;
using Unity.Mathematics;

namespace DroNeS.Components
{
    public struct JobGenerationTimeMark : IComponentData
    {
        public double2 Value;
    }
}