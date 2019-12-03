using DroNeS.Mapbox.Interfaces;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Enums;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{
    public class BuildingMeshFactory : CustomTileFactory
    {
        private readonly BuildingMeshFetcher _dataFetcher;
        private readonly LayerSourceOptions _sourceOptions;
        private readonly IMeshBuilder _builder;
        private string TilesetId => _sourceOptions.Id;

        public BuildingMeshFactory(IMeshBuilder builder)
        {
            _sourceOptions = new LayerSourceOptions
            {
                isActive = true,
                layerSource = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8WithBuildingIds)
            };
            _dataFetcher = ScriptableObject.CreateInstance<BuildingMeshFetcher>();
            _dataFetcher.dataReceived += OnVectorDataReceived;

            _builder = builder;
        }
        
        private void OnVectorDataReceived(CustomTile tile, VectorTile vectorTile)
        {
            if (tile == null) return;
            TilesWaitingResponse.Remove(tile);
//            tile.SetVectorData(vectorTile);
			
            _builder.Create(vectorTile.Data.GetLayer(DronesMap.LayerName), tile);
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
