using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using IMap = DroNeS.Mapbox.Interfaces.IMap;

namespace DroNeS.Mapbox.Custom
{
	public class CustomTile
	{
		public TilePropertyState VectorDataState;
		private TileTerrainType ElevationType { get; }
		private Texture2D RasterData { get; set; }
		public VectorTile VectorData { get; private set; }
		private readonly Material _material;
		private float RelativeScale { get; set; }
		public RectD Rect { get; private set; }
		public float3 Position { get; }
		private Mesh  QuadMesh { get; set; }
		public float TileScale { get; private set; }

		public readonly int CurrentZoom;
		public UnwrappedTileId UnwrappedTileId { get; }
		public CanonicalTileId CanonicalTileId => UnwrappedTileId.Canonical;
		//keeping track of tile objects to be able to cancel them safely if tile is destroyed before data fetching finishes
		private readonly List<Tile> _tiles = new List<Tile>();

		public CustomTile(in IMap map, in UnwrappedTileId tileId)
		{
			CurrentZoom = map.AbsoluteZoom;
			ElevationType = TileTerrainType.None;
			TileScale = map.WorldRelativeScale;
			RelativeScale = math.rcp(math.cos(math.radians((float)map.CenterLatitudeLongitude.x)));
			Rect = Conversions.TileBounds(tileId);
			UnwrappedTileId = tileId;
			_material = new Material(Shader.Find("Standard"));
			var scaleFactor = math.pow(2, map.InitialZoom - map.AbsoluteZoom);
			Position = new float3(
				(float)(Rect.Center.x - map.CenterMercator.x) * TileScale * scaleFactor,
				0,
				(float)(Rect.Center.y - map.CenterMercator.y) * TileScale * scaleFactor);
			MakeFlatTerrain();
		}

		private void MakeFlatTerrain()
		{
			var verts = new Vector3[4];
			verts[0] = TileScale * (Rect.Min - Rect.Center).ToVector3xz();
			verts[1] = TileScale * new Vector3((float)(Rect.Max.x - Rect.Center.x), 0, (float)(Rect.Min.y - Rect.Center.y));
			verts[2] = TileScale * (Rect.Max - Rect.Center).ToVector3xz();
			verts[3] = TileScale * new Vector3((float)(Rect.Min.x - Rect.Center.x), 0, (float)(Rect.Max.y - Rect.Center.y));
			var norms = new [] {Vector3.up, Vector3.up, Vector3.up, Vector3.up};

			QuadMesh = new Mesh
			{
				vertices = verts,
				normals = norms,
				triangles = new[] {0, 1, 2, 0, 2, 3},
				uv = new[]
				{
					new Vector2(0, 1),
					new Vector2(1, 1),
					new Vector2(1, 0),
					new Vector2(0, 0)
				}
			};
		}

		public RenderMesh SetRasterData(byte[] data, bool useMipMap = true, bool useCompression = false)
		{
			if (RasterData == null)
			{
				RasterData = new Texture2D(0, 0, TextureFormat.RGB24, useMipMap) {wrapMode = TextureWrapMode.Clamp};
			}
			RasterData.LoadImage(data);
			RasterData.Compress(useCompression);
			_material.mainTexture = RasterData;

			return new RenderMesh
			{
				mesh = QuadMesh,
				material = _material
			};
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
