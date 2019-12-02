using System;
using Mapbox.Map;
using Debug = UnityEngine.Debug;

namespace DroNeS.Mapbox.Custom
{
    public class TerrainImageFetcherParameters : DataFetcherParameters
    {
        public CustomTile cTile;
        public bool useRetina;
    }
    public class TerrainImageFetcher : DataFetcher
    {
        public Action<CustomTile, RasterTile> dataReceived = (t, s) => { };

        public override void FetchData(DataFetcherParameters parameters)
        {
            if(!(parameters is TerrainImageFetcherParameters imageDataParameters)) return;
            
            var rasterTile = imageDataParameters.useRetina ? new RetinaRasterTile() : new RasterTile();

            rasterTile.Initialize(_fileSource, imageDataParameters.cTile.CanonicalTileId, imageDataParameters.tilesetId, () =>
            {
                if (imageDataParameters.cTile.CanonicalTileId != rasterTile.Id) return;

                if (rasterTile.HasError)
                {
                    Debug.LogError("Terrain Image Error!");
                }
                else
                {
                    dataReceived(imageDataParameters.cTile, rasterTile);
                }
            });
        }
    }
}
