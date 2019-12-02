using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Unity.Mathematics;

namespace DroNeS.Mapbox.Interfaces
{
    public interface IMap
    {
        double2 CenterMercator { get; }
        float WorldRelativeScale { get; }
        double2 CenterLatitudeLongitude { get; }
        int InitialZoom { get; }
        int AbsoluteZoom { get; }
        IEnumerable<UnwrappedTileId> Tiles { get; }
        VectorSubLayerProperties BuildingProperties { get; }
        
    }
}
