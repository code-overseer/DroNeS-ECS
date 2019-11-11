using System;
using Mapbox.Map;
using Mapbox.Unity.Map;

namespace DroNeS.Mapbox.ECS
{
    public class BuildingMeshFetcherParameters : DataFetcherParameters
    {
        public CustomTile cTile;
        public bool useOptimizedStyle = false;
        public Style style = null;
    }
    
    public class BuildingMeshFetcher : DataFetcher
    {
        public Action<CustomTile, VectorTile> dataReceived = (t, s) => { };
//        public Action<UnityTile, VectorTile, TileErrorEventArgs> fetchingError = (t, r, s) => { };

        //tile here should be totally optional and used only not to have keep a dictionary in terrain factory base
        public override void FetchData(DataFetcherParameters parameters)
        {
            if(!(parameters is BuildingMeshFetcherParameters fetcherParameters)) return;
            
            var vectorTile = new VectorTile();
            
            fetcherParameters.cTile.AddTile(vectorTile); //This needs to be here for cancellation 
            
            vectorTile.Initialize(_fileSource, fetcherParameters.canonicalTileId, fetcherParameters.tilesetId, () =>
            {
                if (fetcherParameters.canonicalTileId != vectorTile.Id) return;
                
                if (vectorTile.HasError)
                {
                    UnityEngine.Debug.LogError("Vector Tile Error!");
                }
                else
                {
                    dataReceived(fetcherParameters.cTile, vectorTile);
                }
            });
        }
    }
}