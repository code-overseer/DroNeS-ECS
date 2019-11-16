using Unity.Entities;
using Unity.Mathematics;

// ReSharper disable InconsistentNaming

namespace DroNeS.Components
{
    public struct JobUID : IComponentData
    {
        public int Value;
    }
}
