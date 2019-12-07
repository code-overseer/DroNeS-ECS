using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DroNeS.Mapbox.Custom;
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
    [DebuggerDisplay("Vertices Count = {VertexCount}, Triangles Count = {TriangleCount}," +
                     "UV Count = {UVCount}, Normals Count = {NormalCount}")]
    [DebuggerTypeProxy(typeof(NativeMeshDebugView))]
    public unsafe struct NativeMesh : IDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_vertexSafety;
        internal AtomicSafetyHandle m_normalSafety;
        internal AtomicSafetyHandle m_triangleSafety;
        internal AtomicSafetyHandle m_uvSafety;
        internal AtomicSafetyHandle m_edgeSafety;

        [NativeSetClassTypeToNullOnSchedule] 
        private DisposeSentinel m_DisposeSentinel;
#endif
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_vertices;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_normals;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_triangles;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_uv;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_edges;
        
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

        public NativeMesh(int vertices, int normals, int triangles, int uvs, int edges, Allocator allocator)
            : this(vertices, normals, triangles, uvs, edges, allocator, 2)
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

            DisposeSentinel.Create(out m_vertexSafety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
            if (allocator != Allocator.Temp)
            {
                m_normalSafety = AtomicSafetyHandle.Create();
                m_triangleSafety = AtomicSafetyHandle.Create();
                m_uvSafety = AtomicSafetyHandle.Create();
                m_edgeSafety = AtomicSafetyHandle.Create();
            }
            else
            {
                m_normalSafety = AtomicSafetyHandle.GetTempMemoryHandle();
                m_triangleSafety = AtomicSafetyHandle.GetTempMemoryHandle();
                m_uvSafety = AtomicSafetyHandle.GetTempMemoryHandle();
                m_edgeSafety = AtomicSafetyHandle.GetTempMemoryHandle();
            }
            
#endif
            m_vertices = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), initialCapacity, allocator);
            m_normals = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), initialCapacity, allocator);
            m_triangles = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(),3 * initialCapacity, allocator);
            m_uv = UnsafeList.Create(UnsafeUtility.SizeOf<Vector2>(), UnsafeUtility.AlignOf<Vector2>(), initialCapacity, allocator);
            m_edges = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), 2 * initialCapacity, allocator);
            m_allocator = allocator;

#if UNITY_2019_3_OR_NEWER && ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        private NativeMesh(int vertices, int normals, int triangles, int uvs, int edges, Allocator allocator,
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
            if (edges < 0)
                throw new ArgumentOutOfRangeException(nameof(edges), "Capacity must be >= 0");
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException($"Capacity has exceeded {int.MaxValue} bytes");

            DisposeSentinel.Create(out m_vertexSafety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
            if (allocator != Allocator.Temp)
            {
                m_normalSafety = AtomicSafetyHandle.Create();
                m_triangleSafety = AtomicSafetyHandle.Create();
                m_uvSafety = AtomicSafetyHandle.Create();
                m_edgeSafety = AtomicSafetyHandle.Create();
            }
            else
            {
                m_normalSafety = AtomicSafetyHandle.GetTempMemoryHandle();
                m_triangleSafety = AtomicSafetyHandle.GetTempMemoryHandle();
                m_uvSafety = AtomicSafetyHandle.GetTempMemoryHandle();
                m_edgeSafety = AtomicSafetyHandle.GetTempMemoryHandle();
            }
#endif
            m_vertices = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), vertices, allocator);
            m_normals = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), normals, allocator);
            m_triangles = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(),triangles, allocator);
            m_uv = UnsafeList.Create(UnsafeUtility.SizeOf<Vector2>(), UnsafeUtility.AlignOf<Vector2>(), uvs, allocator);
            m_edges = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), edges, allocator);
            m_allocator = allocator;

#if UNITY_2019_3_OR_NEWER && ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            
        }
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS

        public Vector3 GetVertex(int index) => ReadValue<Vector3>(m_vertices, m_vertexSafety, index);

        public Vector3 GetNormal(int index) => ReadValue<Vector3>(m_normals, m_normalSafety, index);
        
        public Vector2 GetUV(int index) => ReadValue<Vector2>(m_uv, m_uvSafety,index);

        public int GetTriangle(int index) => ReadValue<int>(m_triangles, m_triangleSafety, index);
        
        public int GetEdge(int index) => ReadValue<int>(m_edges, m_edgeSafety, index);

        public void SetVertex(int index, in Vector3 value)=> SetValue(m_vertices, m_vertexSafety, index, in value);

        public void SetNormal(int index, in Vector3 value) => SetValue(m_normals, m_normalSafety, index, in value);

        public void SetTriangle(int index, in int value) => SetValue(m_triangles, m_triangleSafety, index, in value);
        public void SetUV(int index, in Vector2 value) => SetValue(m_uv, m_uvSafety, index, in value);
        
        public void SetUV(int index, in int value) => SetValue(m_edges, m_edgeSafety, index, in value);


        public int VertexCount => GetCount(m_vertices, m_vertexSafety);
        public int NormalCount => GetCount(m_normals, m_normalSafety);
        public int TriangleCount => GetCount(m_triangles, m_triangleSafety);
        public int UVCount => GetCount(m_uv, m_uvSafety);
        public int EdgeCount => GetCount(m_edges, m_edgeSafety);
        
        public int VerticesCapacity
        {
            get => GetCapacity(m_vertices, m_vertexSafety);
            set => SetCapacity<Vector3>(m_vertices, m_vertexSafety, value);
        }
        public int NormalsCapacity
        {
            get => GetCapacity(m_normals, m_normalSafety);
            set => SetCapacity<Vector3>(m_normals, m_normalSafety, value);
        }
        public int TrianglesCapacity
        {
            get => GetCapacity(m_triangles, m_triangleSafety);
            set => SetCapacity<int>(m_triangles, m_triangleSafety, value);
        }
        public int UVCapacity
        {
            get => GetCapacity(m_uv, m_uvSafety);
            set => SetCapacity<Vector2>(m_uv, m_uvSafety, value);
        }

        public int EdgeCapacity
        {
            get => GetCapacity(m_edges, m_edgeSafety);
            set => SetCapacity<int>(m_edges, m_edgeSafety, value);
        }

        public void AddVertex(in Vector3 element) => Add(m_vertices, m_vertexSafety, element);
        public void AddNormal(in Vector3 element) => Add(m_normals, m_normalSafety, element);
        public void AddTriangleValue(in int element) => Add(m_triangles, m_triangleSafety, element);
        public void AddUV(in Vector2 element) => Add(m_uv, m_uvSafety, element);
        public void AddEdge(in int element) => Add(m_edges, m_edgeSafety, element);

        public void AddRangeVertices(in NativeArray<Vector3> elements) => AddRange(m_vertices, m_vertexSafety, elements);
        public void AddRangeNormals(in NativeArray<Vector3> elements) => AddRange(m_normals, m_normalSafety, elements);
        public void AddRangeTriangles(in NativeArray<int> elements) => AddRange(m_triangles, m_triangleSafety, elements);
        public void AddRangeUV(in NativeArray<Vector2> elements) => AddRange(m_uv, m_uvSafety, elements);
        public void AddRangeEdges(in NativeArray<int> elements) => AddRange(m_edges, m_edgeSafety, elements);

        public void RemoveVertexAt(int index) => RemoveAt<Vector3>(m_vertices, m_vertexSafety, index, 1);
        public void RemoveNormalAt(int index) => RemoveAt<Vector3>(m_normals, m_normalSafety, index, 1);
        public void RemoveTriangleAt(int index) => RemoveAt<int>(m_triangles, m_triangleSafety, index, 1);
        public void RemoveUVAt(int index) => RemoveAt<Vector2>(m_uv, m_uvSafety, index, 1);
        public void RemoveEdgeAt(int index) => RemoveAt<int>(m_edges, m_edgeSafety, index, 1);
        
        public void RemoveVertexRangeAt(int index, int length) => RemoveAt<Vector3>(m_vertices, m_vertexSafety, index, length);
        public void RemoveNormalRangeAt(int index, int length) => RemoveAt<Vector3>(m_normals, m_normalSafety, index, length);
        public void RemoveTriangleRangeAt(int index, int length) => RemoveAt<int>(m_triangles, m_triangleSafety, index, length);
        public void RemoveUVRangeAt(int index, int length) => RemoveAt<Vector2>(m_uv, m_uvSafety, index, length);
        public void RemoveEdgeRangeAt(int index, int length) => RemoveAt<int>(m_edges, m_edgeSafety, index, length);
        
        public bool IsCreated => m_vertices != null && m_normals != null && m_triangles != null && m_uv != null && m_edges != null;
        
        private static void AddRange<T>(in UnsafeList* list, in AtomicSafetyHandle handle, NativeArray<T> elements) where T : unmanaged
        {
            AddRange<T>(list, handle, elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }
        
        private static void AddRange<T>(in UnsafeList* list, in AtomicSafetyHandle handle, void* elements, int count) where T : unmanaged
        {
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(handle);
            list->AddRange<T>(elements, count);
        }
        
        private static void RemoveAt<T>(in UnsafeList* list,in AtomicSafetyHandle handle, int index, int length) where T : unmanaged
        {
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(handle);
            var shift = list->Length - index - length;
            if (index < 0 || index + shift >= list->Length || shift < 0)
                throw new ArgumentOutOfRangeException(index.ToString());

            var size = sizeof(T);
            UnsafeUtility.MemMove((void*)((IntPtr) list->Ptr + index * size),
                (void*)((IntPtr) list->Ptr + (index + length) * size),
                shift * size);
            list->Length -= length;
        }

        private static int GetCount(in UnsafeList* list, in AtomicSafetyHandle handle)
        {
            AtomicSafetyHandle.CheckReadAndThrow(handle);
            return list->Length;
        }
        private static int GetCapacity(in UnsafeList* list, in AtomicSafetyHandle handle)
        {
            AtomicSafetyHandle.CheckReadAndThrow(handle);
            return list->Capacity;
        }
        
        private static void SetCapacity<T>(in UnsafeList* list, in AtomicSafetyHandle handle, in int value) where T : unmanaged
        {
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(handle);
            if (value < list->Length)
                throw new ArgumentException("Capacity must be larger than the length of the NativeList.");
            list->SetCapacity<T>(value);
        }

        private static void SetValue<T>(in UnsafeList* list, in AtomicSafetyHandle handle, int index, in T value) where T : unmanaged
        {
            AtomicSafetyHandle.CheckWriteAndThrow(handle);
            if ((uint)index >= (uint)list->Length || index < 0)
                throw new IndexOutOfRangeException($"Index {index} is out of range in NativeList of '{list->Length}' Length.");
            UnsafeUtility.WriteArrayElement(list->Ptr, index, value);
        }

        private T ReadValue<T>(in UnsafeList* list, in AtomicSafetyHandle handle, int index) where T : unmanaged
        {
            AtomicSafetyHandle.CheckReadAndThrow(handle);
            if ((uint)index >= (uint)list->Length || index < 0)
                throw new IndexOutOfRangeException($"Index {index} is out of range in NativeList of '{list->Length}' Length.");
            return UnsafeUtility.ReadArrayElement<T>(list->Ptr, index);
        }
        
        public void Add<T>(in UnsafeList* list, in AtomicSafetyHandle handle, T element) where T : unmanaged
        {
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(handle);
            list->Add(element);
        }

        public Vector3[] VerticesArray() => AsArray<Vector3>(m_vertices, m_vertexSafety).ToArray();
        public Vector3[] NormalsArray() => AsArray<Vector3>(m_normals, m_normalSafety).ToArray();
        public int[] TriangleArray() => AsArray<int>(m_triangles, m_triangleSafety).ToArray();
        public Vector2[] UVArray() => AsArray<Vector2>(m_uv, m_uvSafety).ToArray();
        public int[] EdgeArray() => AsArray<int>(m_edges, m_edgeSafety).ToArray();

        private static NativeArray<T> AsArray<T>(in UnsafeList* list, in AtomicSafetyHandle handle) where T : unmanaged
        {
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(handle);
            var arraySafety = handle;
            AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list->Ptr, list->Length, Allocator.Invalid);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);

            return array;
        }
#else
        public Vector3 GetVertex(int index) => ReadValue<Vector3>(m_vertices, index);

        public Vector3 GetNormal(int index) => ReadValue<Vector3>(m_normals, index);
        
        public Vector2 GetUV(int index) => ReadValue<Vector2>(m_uv,index);

        public int GetTriangle(int index) => ReadValue<int>(m_triangles, index);

        public void SetVertex(int index, in Vector3 value)=> SetValue(m_vertices, index, in value);

        public void SetNormal(int index, in Vector3 value) => SetValue(m_normals, index, in value);

        public void SetTriangle(int index, in int value) => SetValue(m_triangles, index, in value);
        public void SetUV(int index, in Vector2 value) => SetValue(m_uv, index, in value);
        public void SetEdge(int index, in int value) => SetValue(m_edges, index, in value);


        public int VertexCount => GetCount(m_vertices);
        public int NormalCount => GetCount(m_normals);
        public int TriangleCount => GetCount(m_triangles);
        public int UVCount => GetCount(m_uv);
        public int EdgeCount => GetCount(m_edges);
        
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
        public int EdgesCapacity
        {
            get => GetCapacity(m_edges);
            set => SetCapacity<int>(m_edges, value);
        }

        public void AddVertex(in Vector3 element) => Add(m_vertices, element);
        public void AddNormal(in Vector3 element) => Add(m_normals, element);
        public void AddTriangleValue(in int element) => Add(m_triangles, element);
        public void AddUV(in Vector2 element) => Add(m_uv, element);
        public void AddEdgeValue(in int element) => Add(m_edges, element);        

        public void AddRangeVertices(in NativeArray<Vector3> elements) => AddRange(m_vertices, elements);
        public void AddRangeNormals(in NativeArray<Vector3> elements) => AddRange(m_normals, elements);
        public void AddRangeTriangles(in NativeArray<int> elements) => AddRange(m_triangles, elements);
        public void AddRangeUV(in NativeArray<Vector2> elements) => AddRange(m_uv, elements);
        public void AddRangeEdges(in NativeArray<int> elements) => AddRange(m_edges, elements);

        public void RemoveVertexAt(int index) => RemoveAt<Vector3>(m_vertices, index, 1);
        public void RemoveNormalAt(int index) => RemoveAt<Vector3>(m_normals, index, 1);
        public void RemoveTriangleAt(int index) => RemoveAt<int>(m_triangles, index, 1);
        public void RemoveUVAt(int index) => RemoveAt<Vector2>(m_uv, index, 1);
        public void RemoveEdgeAt(int index) => RemoveAt<int>(m_edges, index, 1);
        
        public void RemoveVertexRangeAt(int index, int length) => RemoveAt<Vector3>(m_vertices, index, length);
        public void RemoveNormalRangeAt(int index, int length) => RemoveAt<Vector3>(m_normals, index, length);
        public void RemoveTriangleRangeAt(int index, int length) => RemoveAt<int>(m_triangles, index, length);
        public void RemoveUVRangeAt(int index, int length) => RemoveAt<Vector2>(m_uv, index, length);
        public void RemoveEdgeRangeAt(int index, int length) => RemoveAt<int>(m_edges, index, length);
        
        public bool IsCreated => m_vertices != null && m_normals != null && m_triangles != null && m_uv != null && m_edges != null;
        
        private static void AddRange<T>(in UnsafeList* list, in AtomicSafetyHandle handle, NativeArray<T> elements) where T : unmanaged
        {
            AddRange<T>(list, handle, elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }
        
        private static void AddRange<T>(in UnsafeList* list, in AtomicSafetyHandle handle, void* elements, int count) where T : unmanaged
        {
            list->AddRange<T>(elements, count);
        }
        
        private static void RemoveAt<T>(in UnsafeList* list,in AtomicSafetyHandle handle, int index, int length) where T : unmanaged
        {
            var size = sizeof(T);
            UnsafeUtility.MemMove((void*)((IntPtr) list->Ptr + index * size),
                (void*)((IntPtr) list->Ptr + (index + length) * size),
                (list->Length - index - length) * size);
            list->Length -= length;
        }

        private static int GetCount(in UnsafeList* list)
        {
            return list->Length;
        }
        private static int GetCapacity(in UnsafeList* list)
        {
            return list->Capacity;
        }
        private static void SetCapacity<T>(in UnsafeList* list, in int value) where T : unmanaged
        {
            list->SetCapacity<T>(value);
        }

        private static void SetValue<T>(in UnsafeList* list, int index, in T value) where T : unmanaged
        {
            UnsafeUtility.WriteArrayElement(list->Ptr, index, value);
        }

        private T ReadValue<T>(in UnsafeList* list, int index) where T : unmanaged
        {
            return UnsafeUtility.ReadArrayElement<T>(list->Ptr, index);
        }
        
        public void Add<T>(in UnsafeList* list, T element) where T : unmanaged
        {
            list->Add(element);
        }
        
        public Vector3[] VerticesArray() => AsArray<Vector3>(m_vertices).ToArray();
        public Vector3[] NormalsArray() => AsArray<Vector3>(m_normals).ToArray();
        public int[] TriangleArray() => AsArray<int>(m_triangles).ToArray();
        public Vector2[] UVArray() => AsArray<Vector2>(m_uv).ToArray();
        public int[] EdgeArray() => AsArray<int>(m_edges).ToArray();

        private static NativeArray<T> AsArray<T>(in UnsafeList* list) where T : unmanaged
        {
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list->Ptr, list->Length, Allocator.Invalid);
        }
#endif

        private void Deallocate()
        {
            UnsafeList.Destroy(m_vertices);
            UnsafeList.Destroy(m_normals);
            UnsafeList.Destroy(m_triangles);
            UnsafeList.Destroy(m_uv);
            UnsafeList.Destroy(m_edges);
            m_triangles = null;
            m_vertices = null;
            m_normals = null;
            m_uv = null;
            m_edges = null;
        }
        
        public void Dispose()
        {
            if (m_allocator < Allocator.None) return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_vertexSafety, ref m_DisposeSentinel);
            AtomicSafetyHandle.Release(m_normalSafety);
            AtomicSafetyHandle.Release(m_triangleSafety);
            AtomicSafetyHandle.Release(m_uvSafety);
            AtomicSafetyHandle.Release(m_edgeSafety);
#endif
            Deallocate();
        }
        
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (m_allocator < Allocator.None) return default;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
            var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_vertexSafety);
            AtomicSafetyHandle.Release(m_normalSafety);
            AtomicSafetyHandle.Release(m_triangleSafety);
            AtomicSafetyHandle.Release(m_uvSafety);
            AtomicSafetyHandle.Release(m_edgeSafety);
#endif
            m_vertices = null;
            m_normals = null;
            m_triangles = null;
            m_uv = null;
            m_edges = null;

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
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_vertexSafety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_normalSafety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_triangleSafety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_uvSafety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_edgeSafety);
#endif
            m_vertices->Clear();
            m_normals->Clear();
            m_triangles->Clear();
            m_uv->Clear();
            m_edges->Clear();
        }

        public Mesh AsMesh()
        {
            return new Mesh
            {
                normals = NormalsArray(),
                triangles = TriangleArray(),
                vertices = VerticesArray(),
                uv = UVArray()
            };
        }
        
    }

    public static unsafe class NativeMeshUnsafeUtility
    {
        public static void* GetVerticesUnsafePtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_vertexSafety);
#endif
            return list.m_vertices->Ptr;
        }
        
        public static void* GetVerticesUnsafeReadOnlyPtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_vertexSafety);
#endif
            return list.m_vertices->Ptr;
        }
        
        public static void* GetNormalsUnsafePtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_normalSafety);
#endif
            return list.m_normals->Ptr;
        }
        
        public static void* GetNormalsUnsafeReadOnlyPtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_normalSafety);
#endif
            return list.m_normals->Ptr;
        }
        
        public static void* GetTrianglesUnsafePtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_triangleSafety);
#endif
            return list.m_triangles->Ptr;
        }
        
        public static void* GetTrianglesUnsafeReadOnlyPtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_triangleSafety);
#endif
            return list.m_triangles->Ptr;
        }
        
        public static void* GetUVUnsafePtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_uvSafety);
#endif
            return list.m_uv->Ptr;
        }
        
        public static void* GetUVUnsafeReadOnlyPtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_uvSafety);
#endif
            return list.m_uv->Ptr;
        }

        
        public static void* GetEdgesUnsafePtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_edgeSafety);
#endif
            return list.m_edges->Ptr;
        }
        
        public static void* GetEdgesUnsafeReadOnlyPtr(this NativeMesh list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_edgeSafety);
#endif
            return list.m_edges->Ptr;
        }
    }

    internal struct NativeMeshDebugView
    {
        private NativeMesh m_Mesh;

        public NativeMeshDebugView(NativeMesh mesh)
        {
            m_Mesh = mesh;
        }

        public int[] Triangles => m_Mesh.TriangleArray();
        public Vector3[] Normals => m_Mesh.NormalsArray();
        public Vector3[] Vertices => m_Mesh.VerticesArray();
        public Vector2[] UV => m_Mesh.UVArray();
        public int[] Edges => m_Mesh.EdgeArray();
    }
    
    public static class NativeMeshUtilities
    {
        public static void AddRange(this NativeMesh target, in MeshDataStruct meshData)
        {
            target.AddRangeVertices(meshData.Vertices);
            target.AddRangeNormals(meshData.Normals);
            target.AddRangeUV(meshData.UV);
            target.AddRangeTriangles(meshData.Triangles);
            target.AddRangeEdges(meshData.Edges);
        }
    }
}
