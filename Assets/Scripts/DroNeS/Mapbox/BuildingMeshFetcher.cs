using System;
using System.Diagnostics;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using UnityEngine.Serialization;

namespace DroNeS.Mapbox
{
    public class BuildingMeshFetcherParameters : DataFetcherParameters
    {
//        public CanonicalTileId canonicalTileId;
//        public string tilesetId;
//        public UnityTile tile;
        
        public bool useOptimizedStyle = false;
        public Style style = null;
    }
    
    public class BuildingMeshFetcher : DataFetcher
    {
        public Action<UnityTile, VectorTile> dataReceived = (t, s) => { };
        public Action<UnityTile, VectorTile, TileErrorEventArgs> fetchingError = (t, r, s) => { };

        //tile here should be totally optional and used only not to have keep a dictionary in terrain factory base
        public override void FetchData(DataFetcherParameters parameters)
        {
            if(!(parameters is VectorDataFetcherParameters vectorDaraParameters)) return;
            
            var vectorTile = vectorDaraParameters.useOptimizedStyle ? new VectorTile(vectorDaraParameters.style.Id, vectorDaraParameters.style.Modified) : new VectorTile();
            vectorDaraParameters.tile.AddTile(vectorTile); //This needs to be here for cancellation 
            
            vectorTile.Initialize(_fileSource, vectorDaraParameters.canonicalTileId, vectorDaraParameters.tilesetId, () =>
            {
                if (vectorDaraParameters.canonicalTileId != vectorTile.Id) return;
                
                if (vectorTile.HasError)
                {
                    UnityEngine.Debug.LogError("Vector Tile Error!");
                }
                else
                {
                    dataReceived(vectorDaraParameters.tile, vectorTile);
                }
            });
        }
    }
}