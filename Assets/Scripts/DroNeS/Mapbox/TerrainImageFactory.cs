using DroNeS.Systems;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Mapbox
{
    public class TerrainImageFactory : CustomTileFactory
    {
	    private readonly TerrainImageFetcher _dataFetcher;
	    private ImageryLayerProperties Properties { get; }

	    private string TilesetId => Properties.sourceOptions.Id;

		public TerrainImageFactory()
		{
			_dataFetcher = ScriptableObject.CreateInstance<TerrainImageFetcher>();
			_dataFetcher.dataReceived += OnImageReceived;
			Properties = new ImageryLayerProperties
			{
				sourceType = ImagerySourceType.Custom,
				sourceOptions = new LayerSourceOptions
				{
					isActive = true,
					layerSource = MapboxDefaultImagery.GetParameters(ImagerySourceType.MapboxStreets),
					Id = "mapbox://styles/jw5514/cjr7loico0my12rnrzcm9qk2p"
				},
				rasterOptions = new ImageryRasterOptions
				{
					useCompression = true
				}
			};
		}
		
		#region DataFetcherEvents
		private void OnImageReceived(CustomTile tile, RasterTile rasterTile)
		{
			if (tile == null) return;
			TilesWaitingResponse.Remove(tile);
			var pos = tile.Position;
			var rm = tile.SetRasterData(rasterTile.Data);
			CityBuilderSystem.MakeTerrain(in pos, in rm);
		}
		#endregion

		protected override void OnRegistered(CustomTile tile)
		{
			if (Properties.sourceType != ImagerySourceType.Custom)
			{
				Properties.sourceOptions.layerSource = MapboxDefaultImagery.GetParameters(Properties.sourceType);
			}
			var parameters = new TerrainImageFetcherParameters
			{
				canonicalTileId = tile.CanonicalTileId,
				tilesetId = TilesetId,
				cTile = tile,
				useRetina = Properties.rasterOptions.useRetina
			};
			_dataFetcher.FetchData(parameters);
		}
    }
}
