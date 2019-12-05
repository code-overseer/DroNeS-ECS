using System.Collections.Generic;
using DroNeS.Mapbox.Custom;
using Mapbox.Unity.Map;
using Unity.Rendering;
using UnityEngine;

namespace DroNeS.Mapbox.Interfaces
{
    public interface IMeshProcessor
    {
        void SetOptions(UVModifierOptions uvOptions, GeometryExtrusionWithAtlasOptions extrusionOptions);
        Material BuildingMaterial { get; }
    }
}
