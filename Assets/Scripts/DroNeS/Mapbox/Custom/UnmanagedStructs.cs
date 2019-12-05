using System;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Utils;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{

	public struct MeshDataStruct : IDisposable
	{
		// ReSharper disable UnassignedField.Global
		public RectD TileRect;
		public NativeList<int> Edges;
		public NativeList<Vector3> Vertices;
		public NativeList<Vector3> Normals;
		public NativeList<int> Triangles;
		// ReSharper disable once InconsistentNaming
		public NativeList<Vector2> UV;

		private readonly Allocator _allocator;
		// ReSharper restore UnassignedField.Global

		public MeshDataStruct(RectD tileRect, Allocator allocator)
		{
			TileRect = tileRect;
			_allocator = allocator;
			Edges = new NativeList<int>(allocator);
			Vertices = new NativeList<Vector3>(allocator);
			Normals = new NativeList<Vector3>(allocator);
			Triangles = new NativeList<int>(allocator);
			UV = new NativeList<Vector2>(allocator);
		}
		
		public MeshDataStruct(int index, in MeshDataStruct other, Allocator allocator)
		{
			_allocator = allocator;
			TileRect = other.TileRect;
			if (other._allocator == Allocator.None || other._allocator == Allocator.Invalid)
			{
				Edges = default;
				Vertices = default;
				Normals = default;
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
				Triangles = new NativeList<int>(other.Triangles.Capacity, allocator);
				Triangles.AddRange(other.Triangles);
				UV = new NativeList<Vector2>(other.UV.Capacity, allocator);
				UV.AddRange(other.UV);
			}
		}

		private void Clear()
		{
			Edges.Clear();
			Vertices.Clear();
			Normals.Clear();
			Triangles.Clear();
			UV.Clear();
		}

		public void CopyFrom(in MeshDataStruct other)
		{
			Clear();
			Edges.AddRange(other.Edges.AsArray());
			Vertices.AddRange(other.Vertices.AsArray());
			Normals.AddRange(other.Normals.AsArray());
			Triangles.AddRange(other.Triangles.AsArray());
			UV.AddRange(other.UV.AsArray());
		}

		public void Dispose()
		{
			if (_allocator == Allocator.Invalid || 
			    _allocator == Allocator.None ||
			    _allocator == Allocator.Temp) return;
			
			if (Edges.IsCreated) Edges.Dispose();
			if (Vertices.IsCreated) Vertices.Dispose();
			if (Normals.IsCreated) Normals.Dispose();
			if (Triangles.IsCreated) Triangles.Dispose();
			if (UV.IsCreated) UV.Dispose();
		}

		public JobHandle Dispose(JobHandle inputDeps)
		{
			JobHandle handle = default;
			if (_allocator == Allocator.Invalid || 
			    _allocator == Allocator.None ||
			    _allocator == Allocator.Temp) return handle;

			if (Edges.IsCreated)
			{
				handle = JobHandle.CombineDependencies(Edges.Dispose(inputDeps), handle);
			}

			if (Vertices.IsCreated)
			{
				handle = JobHandle.CombineDependencies(Vertices.Dispose(inputDeps), handle);
			}
			if (Normals.IsCreated)
			{
				handle = JobHandle.CombineDependencies(Normals.Dispose(inputDeps), handle);
			}

			if (Triangles.IsCreated)
			{
				handle = JobHandle.CombineDependencies(Triangles.Dispose(inputDeps), handle);
			}
			if (UV.IsCreated)
			{
				handle = JobHandle.CombineDependencies(UV.Dispose(inputDeps), handle);
			}

			return handle;
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
