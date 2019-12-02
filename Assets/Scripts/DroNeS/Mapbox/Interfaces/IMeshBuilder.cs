using DroNeS.Mapbox.Custom;
using Mapbox.Unity.Map;
using Mapbox.VectorTile;

namespace DroNeS.Mapbox.Interfaces
{
    public interface IMeshBuilder
    {
        VectorSubLayerProperties SubLayerProperties { get; }
        IMeshProcessor Processor { get; }
        void Create(VectorTileLayer layer, CustomTile tile);
    }
}
