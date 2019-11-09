using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using UnityEngine;

namespace DroNeS.Mapbox
{
	public class TerrainBuilder
	{
		private TileTerrainType ElevationType { get; set; }
		private Texture2D RasterData { get; set; }
		private VectorTile VectorData { get; set; }
		private Material _material;
		private float RelativeScale { get; set; }
		private RectD Rect { get; set; }
		private int InitialZoom { get; set; }
		private int CurrentZoom { get; set; }
		private float TileScale { get; set; }
		public UnwrappedTileId UnwrappedTileId { get; private set; }
		public CanonicalTileId CanonicalTileId { get; private set; }
		private static Texture2D _loadingTexture;
		//keeping track of tile objects to be able to cancel them safely if tile is destroyed before data fetching finishes
//    private List<Tile> _tiles = new List<Tile>();
    

		private TerrainBuilder(in IMapReadable map, in UnwrappedTileId tileId)
		{
//        , _map.AbsoluteZoom,
			if (_loadingTexture == null) _loadingTexture = map.LoadingTexture;
			ElevationType = TileTerrainType.None;
			TileScale = map.WorldRelativeScale;
			RelativeScale = 1 / Mathf.Cos(Mathf.Deg2Rad * (float)map.CenterLatitudeLongitude.x);
			Rect = Conversions.TileBounds(tileId);
			UnwrappedTileId = tileId;
			CanonicalTileId = tileId.Canonical;
			_material = new Material(map.TileMaterial);

			var scaleFactor = Mathf.Pow(2, map.InitialZoom - map.AbsoluteZoom);
			// gameObject.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
			// maybe not required
		}
    
		public void MakeFlatTerrain()
		{
			// make quad mesh and assign
		
		}

		public void SetRasterData(byte[] data, bool useMipMap = true, bool useCompression = false)
		{
			if (RasterData == null) // make this static
			{
				RasterData = new Texture2D(0, 0, TextureFormat.RGB24, useMipMap);
				RasterData.wrapMode = TextureWrapMode.Clamp;
			}

			RasterData.LoadImage(data);
			RasterData.Compress(false);
		

			_material.mainTexture = RasterData;
		}

	}
}
