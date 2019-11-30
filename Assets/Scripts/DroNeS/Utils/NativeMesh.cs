using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace DroNeS.Utils
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerDisplay("Vertices Count = {VerticesCount}, Triangles Count = {TrianglesCount}," +
                     "UV Count = {UVCount}, Normals Count = {NormalsCount}")]
    [DebuggerTypeProxy(typeof(NativeMeshDebugView))]
    public unsafe struct NativeMesh : IDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel m_DisposeSentinel;
#endif
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_vertices;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_normals;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_triangles;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_uv;
        
        internal Allocator m_allocator;

        private static long Size => UnsafeUtility.SizeOf<Vector3>() * 2 
                                    + UnsafeUtility.SizeOf<Vector2>() +
                                    3 * UnsafeUtility.SizeOf<int>();

        public NativeMesh(Allocator allocator)
            : this(1, allocator, 2)
        {
        }
        
        public NativeMesh(int initialCapacity, Allocator allocator)
            : this(initialCapacity, allocator, 2)
        {
        }

        public NativeMesh(int vertices, int normals, int triangles, int uvs, Allocator allocator)
            : this(vertices, normals, triangles, uvs, allocator, 2)
        {
        }
        private NativeMesh(int initialCapacity, Allocator allocator, int disposeSentinelStackDepth)
        {
            var totalSize = Size * initialCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent.
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"Capacity * sizeof(T) cannot exceed {int.MaxValue} bytes");

            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
#endif
            m_vertices = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), initialCapacity, allocator);
            m_normals = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), initialCapacity, allocator);
            m_triangles = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(),3 * initialCapacity, allocator);
            m_uv = UnsafeList.Create(UnsafeUtility.SizeOf<Vector2>(), UnsafeUtility.AlignOf<Vector2>(), initialCapacity, allocator);
            m_allocator = allocator;

#if UNITY_2019_3_OR_NEWER && ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        private NativeMesh(int vertices, int normals, int triangles, int uvs, Allocator allocator,
            int disposeSentinelStackDepth)
        {
            var totalSize = (long)(vertices + normals) * UnsafeUtility.SizeOf<Vector3>() + (long)triangles * UnsafeUtility.SizeOf<int>() + (long)uvs * UnsafeUtility.SizeOf<Vector2>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent.
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (vertices < 0)
                throw new ArgumentOutOfRangeException(nameof(vertices), "Capacity must be >= 0");
            if (normals < 0)
                throw new ArgumentOutOfRangeException(nameof(normals), "Capacity must be >= 0");
            if (triangles < 0)
                throw new ArgumentOutOfRangeException(nameof(triangles), "Capacity must be >= 0");
            if (uvs < 0)
                throw new ArgumentOutOfRangeException(nameof(uvs), "Capacity must be >= 0");
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException($"Capacity has exceeded {int.MaxValue} bytes");

            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
#endif
            m_vertices = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), vertices, allocator);
            m_normals = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), normals, allocator);
            m_triangles = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(),triangles, allocator);
            m_uv = UnsafeList.Create(UnsafeUtility.SizeOf<Vector2>(), UnsafeUtility.AlignOf<Vector2>(), uvs, allocator);
            m_allocator = allocator;

#if UNITY_2019_3_OR_NEWER && ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            
        }

        public Vector3 GetVertex(int index) => ReadValue<Vector3>(m_vertices, index);

        public Vector3 GetNormal(int index) => ReadValue<Vector3>(m_normals, index);
        
        public Vector2 GetUV(int index) => ReadValue<Vector2>(m_uv, index);

        public int GetTriangle(int index) => ReadValue<int>(m_triangles, index);

        public void SetVertex(int index, in Vector3 value)=> SetValue(m_vertices, index, in value);

        public void SetNormal(int index, in Vector3 value) => SetValue(m_normals, index, in value);

        public void SetUV(int index, in Vector2 value) => SetValue(m_uv, index, in value);

        public void SetTriangle(int index, in int value) => SetValue(m_triangles, index, in value);

        public int VerticesCount => GetCount(m_vertices);
        public int NormalsCount => GetCount(m_normals);
        public int UVCount => GetCount(m_uv);
        public int TrianglesCount => GetCount(m_triangles);
        
        public int VerticesCapacity
        {
            get => GetCapacity(m_vertices);
            set => SetCapacity<Vector3>(m_vertices, value);
        }
        public int NormalsCapacity
        {
            get => GetCapacity(m_normals);
            set => SetCapacity<Vector3>(m_normals, value);
        }
        public int TrianglesCapacity
        {
            get => GetCapacity(m_triangles);
            set => SetCapacity<int>(m_triangles, value);
        }
        public int UVCapacity
        {
            get => GetCapacity(m_uv);
            set => SetCapacity<Vector2>(m_uv, value);
        }

        public void AddVertex(in Vector3 element) => Add(m_vertices, element);
        public void AddNormal(in Vector3 element) => Add(m_normals, element);
        public void AddTriangleValue(in int element) => Add(m_triangles, element);
        public void AddUV(in Vector2 element) => Add(m_uv, element);

        public void AddRangeVertices(in NativeArray<Vector3> elements) => AddRange(m_vertices, elements);
        public void AddRangeNormals(in NativeArray<Vector3> elements) => AddRange(m_normals, elements);
        public void AddRangeTriangles(in NativeArray<int> elements) => AddRange(m_triangles, elements);
        public void AddRangeUV(in NativeArray<Vector2> elements) => AddRange(m_uv, elements);

        public void RemoveAtSwapBackVertex(int index) => RemoveAtSwapBack<Vector3>(m_vertices, index);
        public void RemoveAtSwapBackNormal(int index) => RemoveAtSwapBack<Vector3>(m_normals, index);
        public void RemoveAtSwapBackTriangle(int index) => RemoveAtSwapBack<int>(m_triangles, index);
        public void RemoveAtSwapBackUV(int index) => RemoveAtSwapBack<Vector2>(m_uv, index);
        
        public bool IsCreated => m_vertices != null && m_normals != null && m_triangles != null && m_uv != null;
        
        private void AddRange<T>(in UnsafeList* list, NativeArray<T> elements) where T : unmanaged
        {
            AddRange<T>(list, elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }
        
        private void AddRange<T>(in UnsafeList* list, void* elements, int count) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            list->AddRange<T>(elements, count);
        }
        
        private void RemoveAtSwapBack<T>(in UnsafeList* list, int index) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);

            if (index < 0 || index >= list->Length)
                throw new ArgumentOutOfRangeException(index.ToString());
#endif
            list->RemoveAtSwapBack<T>(index);
        }

        private void Deallocate()
        {
            UnsafeList.Destroy(m_vertices);
            UnsafeList.Destroy(m_normals);
            UnsafeList.Destroy(m_triangles);
            UnsafeList.Destroy(m_uv);
            m_triangles = null;
            m_vertices = null;
            m_normals = null;
            m_uv = null;
        }
        
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            Deallocate();
        }
        
        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
            var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            m_triangles = null;
            m_vertices = null;
            m_normals = null;
            m_uv = null;

            return jobHandle;
        }

        [BurstCompile]
        private struct DisposeJob : IJob
        {
            public NativeMesh Container;

            public void Execute()
            {
                Container.Deallocate();
            }
        }
        
        public void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_triangles->Clear();
            m_vertices->Clear();
            m_normals->Clear();
            m_uv->Clear();
        }
        
        
        private int GetCount(in UnsafeList* list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return list->Length;
        }

        private int GetCapacity(in UnsafeList* list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return list->Capacity;
        }
        
        private void SetCapacity<T>(in UnsafeList* list, in int value) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
            if (value < list->Length)
                throw new ArgumentException("Capacity must be larger than the length of the NativeList.");
#endif
            list->SetCapacity<T>(value);
        }
        
        private void SetValue<T>(in UnsafeList* list, int index, in T value) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            if ((uint)index >= (uint)list->Length)
                throw new IndexOutOfRangeException($"Index {index} is out of range in NativeList of '{list->Length}' Length.");
#endif
            UnsafeUtility.WriteArrayElement(list->Ptr, index, value);
        }

        private T ReadValue<T>(in UnsafeList* list, int index) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            if ((uint)index >= (uint)list->Length)
                throw new IndexOutOfRangeException($"Index {index} is out of range in NativeList of '{list->Length}' Length.");
#endif
            return UnsafeUtility.ReadArrayElement<T>(list->Ptr, index);
        }
        
        public void Add<T>(in UnsafeList* list, T element) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            list->Add(element);
        }
        
    }

    internal struct NativeMeshDebugView
    {
        
    }
}
