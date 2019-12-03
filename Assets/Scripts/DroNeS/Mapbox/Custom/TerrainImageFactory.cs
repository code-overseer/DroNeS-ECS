using Boo.Lang;
using Mapbox.Map;
using Mapbox.Unity.Map;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{
	public delegate void CompletionDelegate(Texture argument);
    public class TerrainImageFactory : CustomTileFactory
    {
	    private readonly TerrainImageFetcher _dataFetcher;
	    private ImageryLayerProperties Properties { get; }
	    private string TilesetId => Properties.sourceOptions.Id;

	    private int _counter = 0;
	    private Texture2D[] Textures { get; set; }
	    private Texture2DArray TextureArray { get; set; }

	    private event CompletionDelegate AllImagesLoaded;
	    public TerrainImageFactory(CompletionDelegate completionCallback = null)
	    {
		    AllImagesLoaded += completionCallback;
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
			Textures = new Texture2D[ManhattanTileProvider.Tiles.Count];
		}
		
		private void OnImageReceived(CustomTile tile, RasterTile rasterTile)
		{
			if (tile == null) return;
			TilesWaitingResponse.Remove(tile);
			var raster = new Texture2D(512, 512, TextureFormat.RGB24, false) {wrapMode = TextureWrapMode.Clamp};
			raster.LoadImage(rasterTile.Data);
			raster.Compress(true);
			Textures[tile.TextureIndex] = raster;
			if (++_counter == ManhattanTileProvider.Tiles.Count)
			{
				OnComplete();
			}
		}

		private void OnComplete()
		{
			TextureArray = new Texture2DArray(512,512, Textures.Length, TextureFormat.RGB24, false);
			for (var i = 0; i < Textures.Length; ++i)
			{
				TextureArray.SetPixels(Textures[i].GetPixels(), i);
			}
			Textures = null;
			TextureArray.Apply();
			AllImagesLoaded?.Invoke(TextureArray);
		}

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
