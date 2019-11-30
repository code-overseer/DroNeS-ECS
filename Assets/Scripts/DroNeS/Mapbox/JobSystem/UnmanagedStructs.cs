using System;
using DroNeS.Mapbox.ECS;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Utils;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Mapbox.JobSystem
{
	public struct VectorFeatureStruct
	{
		public NativeMultiHashMap<int, float3> Points;
		public NativeList<int> PointCount;

		public static implicit operator VectorFeatureStruct(CustomFeatureUnity feature)
		{
			var output = new VectorFeatureStruct();
			var points = feature.Points;
			output.PointCount = new NativeList<int>(points.Count + 1, Allocator.TempJob);
			var size = 0;
			foreach (var list in points)
			{
				output.PointCount.Add(list.Count);
				size += list.Count;
			}
			
			output.Points = new NativeMultiHashMap<int, float3>(size + 1, Allocator.TempJob);
			for (var i = 0; i < points.Count; ++i)
			{
				for (var j = points[i].Count - 1; j >= 0; --j)
				{
					output.Points.Add(i, points[i][j]);
				}
			}

			return output;
		}
		
	}
	
	public struct MathRect
	{
		public double2 Min;
		public double2 Max;
		public double2 Size;
		public double2 Center;

		public static implicit operator MathRect(in RectD rectD)
		{
			return new MathRect
			{
				Min = new double2(rectD.Min.x, rectD.Min.y),
				Max = new double2(rectD.Max.x, rectD.Max.y),
				Size = new double2(rectD.Size.x, rectD.Size.y),
				Center = new double2(rectD.Center.x, rectD.Center.y)
			};
		}
	}

	public struct MeshDataStruct : IDisposable
	{
		// ReSharper disable UnassignedField.Global
		public MathRect TileRect;
		public NativeList<int> Edges;
		public NativeList<Vector3> Vertices;
		public NativeList<Vector3> Normals;
		public NativeList<Vector4> Tangents;
		public NativeList<int> Triangles;
		public NativeList<Vector2> UV;

		private readonly Allocator _allocator;
		// ReSharper restore UnassignedField.Global

		public MeshDataStruct(in RectD tileRect, Allocator allocator)
		{
			TileRect = tileRect;
			_allocator = allocator;
			Edges = new NativeList<int>(allocator);
			Vertices = new NativeList<Vector3>(allocator);
			Normals = new NativeList<Vector3>(allocator);
			Tangents = new NativeList<Vector4>(allocator);
			Triangles = new NativeList<int>(allocator);
			UV = new NativeList<Vector2>(allocator);
		}
		
		public MeshDataStruct(in MeshDataStruct other, Allocator allocator)
		{
			_allocator = allocator;
			TileRect = other.TileRect;
			if (other._allocator == Allocator.None || other._allocator == Allocator.Invalid)
			{
				Edges = default;
				Vertices = default;
				Normals = default;
				Tangents = default;
				Triangles = default;
				UV = default;
			}
			else
			{
				Edges = new NativeList<int>(other.Edges.Capacity, allocator);
				Edges.AddRange(other.Edges);
				Vertices = new NativeList<Vector3>(other.Vertices.Capacity, allocator);
				Vertices.AddRange(other.Vertices);
				Normals = new NativeList<Vector3>(other.Normals.Capacity, allocator);
				Normals.AddRange(other.Normals);
				Tangents = new NativeList<Vector4>(other.Tangents.Capacity, allocator);
				Tangents.AddRange(other.Tangents);
				Triangles = new NativeList<int>(other.Triangles.Capacity, allocator);
				Triangles.AddRange(other.Triangles);
				UV = new NativeList<Vector2>(other.UV.Capacity, allocator);
				UV.AddRange(other.UV);
			}
		}

		public void CopyFrom(in MeshDataStruct other)
		{
			Clear();
			Edges.AddRange(other.Edges);
			Vertices.AddRange(other.Vertices);
			Normals.AddRange(other.Normals);
			Tangents.AddRange(other.Tangents);
			Triangles.AddRange(other.Edges);
			UV.AddRange(other.UV);
		}

		public void Dispose()
		{
			if (_allocator == Allocator.Invalid || 
			    _allocator == Allocator.None ||
			    _allocator == Allocator.Temp) return;
			
			if (Edges.IsCreated) Edges.Dispose();
			if (Vertices.IsCreated) Vertices.Dispose();
			if (Normals.IsCreated) Normals.Dispose();
			if (Tangents.IsCreated) Tangents.Dispose();
			if (Triangles.IsCreated) Triangles.Dispose();
			if (UV.IsCreated) UV.Dispose();
		}

		private void Clear()
		{
			Edges.Clear();
			Vertices.Clear();
			Normals.Clear();
			Tangents.Clear();
			Triangles.Clear();
			UV.Clear();
		}
	}

	public struct AtlasEntityStruct
	{
		public Rect TextureRect;
		public int MidFloorCount;
		public float ColumnCount;

		public float TopSectionRatio;
		public float BottomSectionRatio;

		public int PreferredEdgeSectionLength;
		public float FloorHeight;
		public float FirstFloorHeight;
		public float TopFloorHeight;
	
		public float BottomOfTopUv;
		public float TopOfMidUv;
		public float TopOfBottomUv;
		public float MidUvHeight;
		public float WallToFloorRatio;
		
		public static implicit operator AtlasEntityStruct(in AtlasEntity entity)
		{
			return new AtlasEntityStruct
			{
				TextureRect = entity.TextureRect,
				MidFloorCount = entity.MidFloorCount,
				ColumnCount = entity.ColumnCount,
				TopSectionRatio = entity.TopSectionRatio,
				BottomSectionRatio = entity.BottomSectionRatio,
				PreferredEdgeSectionLength = entity.PreferredEdgeSectionLength,
				FloorHeight = entity.FloorHeight,
				FirstFloorHeight = entity.FirstFloorHeight,
				TopFloorHeight = entity.TopFloorHeight,
				BottomOfTopUv = entity.bottomOfTopUv,
				TopOfMidUv = entity.topOfMidUv,
				TopOfBottomUv = entity.topOfBottomUv,
				MidUvHeight = entity.midUvHeight,
				WallToFloorRatio = entity.WallToFloorRatio
			};
		}
		
		public void CalculateParameters()
		{
			BottomOfTopUv = TextureRect.yMax - (TextureRect.size.y * TopSectionRatio);
			TopOfMidUv = TextureRect.yMax - (TextureRect.height * TopSectionRatio);
			TopOfBottomUv = TextureRect.yMin + (TextureRect.size.y * BottomSectionRatio);
			MidUvHeight = TextureRect.height * (1 - TopSectionRatio - BottomSectionRatio);
			WallToFloorRatio = (1 - TopSectionRatio - BottomSectionRatio) * (TextureRect.height / TextureRect.width);
		}
		
	}
}
