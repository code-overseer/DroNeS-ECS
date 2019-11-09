using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Mapbox
{
	public class CustomTile
	{
		private readonly int _initialZoom;
		public TilePropertyState VectorDataState;
		public TileTerrainType ElevationType { get; private set; }
		public Texture2D RasterData { get; private set; }
		public VectorTile VectorData { get; private set; }
		private Material _material;
		public float RelativeScale { get; private set; }
		public RectD Rect { get; private set; }
		public float TileScale { get; private set; }
		public UnwrappedTileId UnwrappedTileId { get; }
		public CanonicalTileId CanonicalTileId => UnwrappedTileId.Canonical;
		private static Texture2D _loadingTexture;
		//keeping track of tile objects to be able to cancel them safely if tile is destroyed before data fetching finishes
		private List<Tile> _tiles = new List<Tile>();
    

		public CustomTile(in IMapReadable map, in UnwrappedTileId tileId, int initialZoom)
		{
			_initialZoom = initialZoom;
			if (_loadingTexture == null) _loadingTexture = map.LoadingTexture;
			ElevationType = TileTerrainType.None;
			TileScale = map.WorldRelativeScale;
			RelativeScale = math.rcp(math.cos(math.radians((float)map.CenterLatitudeLongitude.x)));
			Rect = Conversions.TileBounds(tileId);
			UnwrappedTileId = tileId;
			_material = new Material(map.TileMaterial);
			var scaleFactor = math.pow(2, map.InitialZoom - map.AbsoluteZoom);
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
		
		public void SetVectorData(VectorTile vectorTile)
		{
			VectorData = vectorTile;
		}

		internal void AddTile(Tile tile)
		{
			_tiles.Add(tile);
		}

		public void Cancel()
		{
			for (int i = 0, tilesCount = _tiles.Count; i < tilesCount; i++)
			{
				_tiles[i].Cancel();
			}
		}

	}
}
