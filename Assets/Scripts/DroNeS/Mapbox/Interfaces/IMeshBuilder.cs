using DroNeS.Mapbox.Custom;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Filters;
using Mapbox.VectorTile;

namespace DroNeS.Mapbox.Interfaces
{
    public interface IMeshBuilder
    {
        VectorSubLayerProperties SubLayerProperties { get; }
        IMeshProcessor Processor { get; }
        void Create(VectorTileLayer layer, CustomTile tile);
    }
    
    public class BuildingMeshBuilderProperties
    {
        public bool BuildingsWithUniqueIds;
        public VectorTileLayer VectorTileLayer;
        public ILayerFeatureFilterComparer[] LayerFeatureFilters;
        public ILayerFeatureFilterComparer LayerFeatureFilterCombiner;
        public int FeatureCount;
    }
}
