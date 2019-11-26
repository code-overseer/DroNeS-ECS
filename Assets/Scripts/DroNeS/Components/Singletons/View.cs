using Unity.Entities;
using UnityEngine;

namespace DroNeS.Components.Singletons
{
    public struct View : IComponentData
    {
        public CameraTypeValue CameraType;
    }
}
