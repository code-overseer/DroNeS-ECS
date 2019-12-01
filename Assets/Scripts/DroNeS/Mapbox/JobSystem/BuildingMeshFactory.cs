using DroNeS.Mapbox.Custom;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Modifiers;
using UnityEngine;

namespace DroNeS.Mapbox.JobSystem
{
    public class BuildingMeshFactory : CustomTileFactory
    {
        private readonly BuildingMeshFetcher _dataFetcher;
        private readonly LayerSourceOptions _sourceOptions;
        private readonly BuildingMeshBuilder _builder;
        private string TilesetId => _sourceOptions.Id;
        private const string LayerName = "building";

        public MeshProcessor Processor => _builder.Processor;

        public BuildingMeshFactory()
        {
            _sourceOptions = new LayerSourceOptions
            {
                isActive = true,
                layerSource = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8WithBuildingIds)
            };
            _dataFetcher = ScriptableObject.CreateInstance<BuildingMeshFetcher>();
            _dataFetcher.dataReceived += OnVectorDataReceived;
            
            var properties = new VectorSubLayerProperties
            {
                colliderOptions = {colliderType = ColliderType.None},
                coreOptions =
                {
                    geometryType = VectorPrimitiveType.Polygon,
                    layerName = LayerName,
                    snapToTerrain = true,
                    combineMeshes = true
                },
                extrusionOptions =
                {
                    extrusionType = ExtrusionType.PropertyHeight,
                    extrusionScaleFactor = 1.3203f,
                    propertyName = "height",
                    extrusionGeometryType = ExtrusionGeometryType.RoofAndSide
                },
                moveFeaturePositionTo = PositionTargetType.CenterOfVertices
            };
            properties.coreOptions.sublayerName = "Buildings";
            properties.buildingsWithUniqueIds = true;
            properties.coreOptions.geometryType = VectorPrimitiveType.Polygon;
            properties.honorBuildingIdSetting = true;
            
            _builder = new BuildingMeshBuilder(properties);
        }
        
        private void OnVectorDataReceived(CustomTile tile, VectorTile vectorTile)
        {
            if (tile == null) return;
            TilesWaitingResponse.Remove(tile);
            tile.SetVectorData(vectorTile);
			
            _builder.Create(tile.VectorData.Data.GetLayer(LayerName), tile);
        }
        
        protected override void OnRegistered(CustomTile tile)
        {
            tile.VectorDataState = TilePropertyState.Loading;
            TilesWaitingResponse.Add(tile);
            _dataFetcher.FetchData(new BuildingMeshFetcherParameters
            {
                canonicalTileId = tile.CanonicalTileId,
                tilesetId = TilesetId,
                cTile = tile,
                useOptimizedStyle = false,
                style = _sourceOptions.layerSource
            });
        }

    }
}
