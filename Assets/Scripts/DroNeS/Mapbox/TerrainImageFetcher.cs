using System;
using System.Diagnostics;
using Mapbox.Map;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DroNeS.Mapbox
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

            imageDataParameters.tile.AddTile(rasterTile);

            rasterTile.Initialize(_fileSource, imageDataParameters.tile.CanonicalTileId, imageDataParameters.tilesetId, () =>
            {
                if (imageDataParameters.tile.CanonicalTileId != rasterTile.Id) return;

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
