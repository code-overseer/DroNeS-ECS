using Mapbox.Map;
using Mapbox.Unity.Map;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
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
			var rm = tile.SetRasterData(rasterTile.Data);
			var tileObj = new GameObject(tile.ToString()) {layer = LayerMask.NameToLayer("Terrain")};
			tileObj.transform.position = tile.Position;
			var filter = tileObj.AddComponent<MeshFilter>();
			filter.sharedMesh = rm.mesh;
			var renderer = tileObj.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = rm.material;
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
