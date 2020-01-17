using Boo.Lang;
using DroNeS.Utils;
using Mapbox.Map;
using Mapbox.Unity.Map;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{
	public delegate void TerrainCompletion(Texture argument, Mesh mesh);
    public class TerrainImageFactory : CustomTileFactory
    {
	    private readonly TerrainImageFetcher _dataFetcher;
	    private ImageryLayerProperties Properties { get; }
	    private string TilesetId => Properties.sourceOptions.Id;

	    private int _counter = 0;
	    private Texture2D[] _textures;
	    private CombineInstance[] _combineInstances;
	    private Texture2DArray _textureArray;

	    private event TerrainCompletion AllImagesLoaded;
	    public TerrainImageFactory(TerrainCompletion completionCallback = null)
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
			var tilesCount = ManhattanTileProvider.Tiles.Count;
			_textures = new Texture2D[tilesCount];
			_combineInstances = new CombineInstance[tilesCount];
			_textureArray = new Texture2DArray(512,512, _textures.Length, TextureFormat.RGB24, false);
		}
		
		private void OnImageReceived(CustomTile tile, RasterTile rasterTile)
		{
			if (tile == null) return;
			TilesWaitingResponse.Remove(tile);
			var raster = new Texture2D(512, 512, TextureFormat.RGB24, false) {wrapMode = TextureWrapMode.Clamp};
			raster.LoadImage(rasterTile.Data);
			raster.Compress(true);
			_textures[tile.TextureIndex] = raster;
			_combineInstances[tile.TextureIndex] = new CombineInstance
			{
				mesh = tile.QuadMesh,
				transform = tile.Transform.parent.localToWorldMatrix * tile.Transform.localToWorldMatrix
			};
			tile.ClearMesh();
			
			if (++_counter == ManhattanTileProvider.Tiles.Count)
			{
				OnComplete();
			}
		}

		private void OnComplete()
		{
			for (var i = 0; i < _textures.Length; ++i)
			{
				_textureArray.SetPixels(_textures[i].GetPixels(), i);
				
				Object.Destroy(_textures[i]);
			}
			_textures = null;
			_textureArray.Apply();
			
			var mesh = new Mesh();
			mesh.CombineMeshes(_combineInstances);
			_combineInstances = null;
			
			AllImagesLoaded?.Invoke(_textureArray, mesh);
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
