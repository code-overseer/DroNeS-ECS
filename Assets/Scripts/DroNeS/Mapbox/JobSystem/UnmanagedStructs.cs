using System;
using System.Runtime.InteropServices;
using DroNeS.Mapbox.Custom;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Mapbox.JobSystem
{
	public struct VectorFeatureStruct
	{
		public NativeList<UnsafeListContainer> Points;

		public static implicit operator VectorFeatureStruct(CustomFeatureUnity feature)
		{
			var output = new VectorFeatureStruct();
			var points = feature.Points;

			var listOfList = new NativeList<UnsafeListContainer>(points.Count, Allocator.TempJob);
			var idx = 0;
			foreach (var list in points)
			{
				listOfList.Add(new UnsafeListContainer(list.Count, 
					UnsafeUtility.SizeOf<Vector3>(), 
					UnsafeUtility.AlignOf<Vector3>(), 
					Allocator.TempJob));
				foreach (var value in list)
				{
					listOfList[idx].Add(value);
				}
				++idx;
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
		public NativeList<int> Triangles;
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

		public void CopyFrom(in MeshDataStruct other)
		{
			Clear();
			Edges.AddRange(other.Edges);
			Vertices.AddRange(other.Vertices);
			Normals.AddRange(other.Normals);
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

		private void Clear()
		{
			Edges.Clear();
			Vertices.Clear();
			Normals.Clear();
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

	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct UnsafeListContainer
	{
		[NativeDisableUnsafePtrRestriction]
		internal UnsafeList* m_ListData;
		internal Allocator m_Allocator;
		
		public UnsafeListContainer(int sizeOf, int alignOf, Allocator allocator)
            : this(1, sizeOf, alignOf, allocator)
        {
        }

		public UnsafeListContainer(int initialCapacity, int sizeOf, int alignOf, Allocator allocator)
        {
	        m_ListData = UnsafeList.Create(sizeOf, alignOf, initialCapacity, allocator);
            m_Allocator = allocator;
        }
        
        public T Get<T>(int index)
        {
	        return UnsafeUtility.ReadArrayElement<T>(m_ListData->Ptr, index);
        }

        public void Set<T>(int index, T value)
        {
	        UnsafeUtility.WriteArrayElement(m_ListData->Ptr, index, value);
        }
        
        public int Length => m_ListData->Length;
        
        public int Capacity => m_ListData->Capacity;

        public void SetCapacity<T>(int value) where T : unmanaged
        {
	        m_ListData->SetCapacity<T>(value);
        }
        
        public void Add<T>(T element) where T : unmanaged => m_ListData->Add(element);
        
        public void AddRange<T>(NativeArray<T> elements) where T : unmanaged
        {
	        AddRange<T>(elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }
        
        public void AddRange<T>(void* elements, int count) where T : unmanaged
        {
	        m_ListData->AddRange<T>(elements, count);
        }
        
        public void RemoveAtSwapBack<T>(int index) where T : unmanaged
        {
	        m_ListData->RemoveAtSwapBack<T>(index);
        }
        
        public void RemoveAt<T>(int index, int length = 1) where T : unmanaged
        {
	        var shift = m_ListData->Length - index - length;

	        var size = sizeof(T);
	        UnsafeUtility.MemMove((void*)((IntPtr) m_ListData->Ptr + index * size),
		        (void*)((IntPtr) m_ListData->Ptr + (index + length) * size),
		        shift * size);
	        m_ListData->Length -= length;
        }
        
        public bool IsCreated => m_ListData != null;

        private void Deallocate()
        {
	        UnsafeList.Destroy(m_ListData);
	        m_ListData = null;
        }

        
        public NativeArray<T> AsArray<T>() where T : unmanaged
        {
	        var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(m_ListData->Ptr, m_ListData->Length, Allocator.Invalid);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
	        return array;
        }
	}
}
