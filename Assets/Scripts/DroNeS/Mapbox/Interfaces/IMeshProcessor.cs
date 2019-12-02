using System.Collections.Generic;
using DroNeS.Mapbox.Custom;
using Unity.Rendering;

namespace DroNeS.Mapbox.Interfaces
{
    public interface IMeshProcessor
    {
        Dictionary<CustomTile, IEnumerable<RenderMesh>> RenderMeshes { get; }
    }
}
