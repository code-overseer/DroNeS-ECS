using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using Unity.Mathematics;
using UnityEngine;
using IMap = DroNeS.Mapbox.Interfaces.IMap;

namespace DroNeS.Mapbox.Custom
{
	public class CustomTile
	{
		public TilePropertyState VectorDataState;
		public VectorTile VectorData { get; private set; }
		public RectD Rect { get; }
		public float3 Position { get; }
		public float TileScale { get; }
		public int CurrentZoom { get; }
		public CanonicalTileId CanonicalTileId => _unwrappedTileId.Canonical;
		public int TextureIndex { get; private set; }
		public Mesh QuadMesh { get; private set; }
		public Transform Transform { get; }

		private static int _index;
		private readonly UnwrappedTileId _unwrappedTileId;

		public CustomTile(in IMap map, in UnwrappedTileId tileId)
		{
			CurrentZoom = map.AbsoluteZoom;
			TileScale = map.WorldRelativeScale;
			Rect = Conversions.TileBounds(tileId);
			_unwrappedTileId = tileId;
			var scaleFactor = math.pow(2, map.InitialZoom - map.AbsoluteZoom);
			Position = new float3(
				(float)(Rect.Center.x - map.CenterMercator.x) * TileScale * scaleFactor,
				0,
				(float)(Rect.Center.y - map.CenterMercator.y) * TileScale * scaleFactor);
			MakeFlatTerrain();
		}

		public void ClearMesh() => QuadMesh = null;
		
		public CustomTile(Transform root, in IMap map, in UnwrappedTileId tileId)
		{
			CurrentZoom = map.AbsoluteZoom;
			TileScale = map.WorldRelativeScale;
			Rect = Conversions.TileBounds(tileId);
			_unwrappedTileId = tileId;
			var scaleFactor = math.pow(2, map.InitialZoom - map.AbsoluteZoom);
			Position = new float3(
				(float)(Rect.Center.x - map.CenterMercator.x) * TileScale * scaleFactor,
				0,
				(float)(Rect.Center.y - map.CenterMercator.y) * TileScale * scaleFactor);
			MakeFlatTerrain();

			var obj = new GameObject(CanonicalTileId.ToString()) {layer = LayerMask.NameToLayer("Terrain")};
			Transform = obj.transform;
			Transform.position = Position;
			Transform.SetParent(root, true);
		}

		private void MakeFlatTerrain()
		{
			if (QuadMesh != null) return;
			var verts = new Vector3[4];
			verts[0] = TileScale * (Rect.Min - Rect.Center).ToVector3xz();
			verts[1] = TileScale * new Vector3((float)(Rect.Max.x - Rect.Center.x), 0, (float)(Rect.Min.y - Rect.Center.y));
			verts[2] = TileScale * (Rect.Max - Rect.Center).ToVector3xz();
			verts[3] = TileScale * new Vector3((float)(Rect.Min.x - Rect.Center.x), 0, (float)(Rect.Max.y - Rect.Center.y));
			var norms = new [] {Vector3.up, Vector3.up, Vector3.up, Vector3.up};
			TextureIndex = _index++;
			QuadMesh = new Mesh
			{
				vertices = verts,
				normals = norms,
				triangles = new[] {0, 1, 2, 0, 2, 3},
			};
			QuadMesh.SetUVs(0, new List<Vector3>
			{
				new Vector3(0, 1, TextureIndex),
				new Vector3(1, 1,TextureIndex),
				new Vector3(1, 0, TextureIndex),
				new Vector3(0, 0, TextureIndex)
			});
		}

		public void SetVectorData(VectorTile vectorTile)
		{
			VectorData = vectorTile;
		}

	}
}
