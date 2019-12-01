using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace DroNeS.Utils
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerDisplay("Vertices Count = {VertexCount}, Triangles Count = {TriangleCount}," +
                     "UV Count = {UVCount}, Normals Count = {NormalCount}")]
    [DebuggerTypeProxy(typeof(MeshProxyDebugView))]
    public unsafe struct MeshProxy : IDisposable
    {
        internal GCHandle _meshHandle;
        internal GCHandle _verticesHandle;
        internal GCHandle _normalsHandle;
        internal GCHandle _trianglesHandle;
        internal GCHandle _uvHandle;
        public int VertexCount { get; private set; }
        public int NormalCount { get; private set; }
        public int TriangleCount { get; private set; }
        public int UVCount { get; private set; }

        internal void* Vertices;
        internal void* Normals;
        internal void* Triangles;
        internal void* UV;

        private Allocator m_allocator;
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_vertexSafety;
        internal AtomicSafetyHandle m_normalSafety;
        internal AtomicSafetyHandle m_triangleSafety;
        internal AtomicSafetyHandle m_uvSafety;

        [NativeSetClassTypeToNullOnSchedule] 
        private DisposeSentinel m_DisposeSentinel;
#endif
        public MeshProxy(Mesh mesh)
        {
            VertexCount = mesh.vertices.Length;
            NormalCount = mesh.normals.Length;
            TriangleCount = mesh.triangles.Length;
            UVCount = mesh.uv.Length;

            _meshHandle = GCHandle.Alloc(mesh, GCHandleType.Pinned);
            _verticesHandle = GCHandle.Alloc(mesh.vertices, GCHandleType.Pinned);
            _normalsHandle = GCHandle.Alloc(mesh.normals, GCHandleType.Pinned);
            _trianglesHandle = GCHandle.Alloc(mesh.triangles, GCHandleType.Pinned);
            _uvHandle = GCHandle.Alloc(mesh.uv, GCHandleType.Pinned);

            Vertices = (void*)_verticesHandle.AddrOfPinnedObject();
            Normals = (void*)_normalsHandle.AddrOfPinnedObject();
            Triangles = (void*)_trianglesHandle.AddrOfPinnedObject();
            UV = (void*)_uvHandle.AddrOfPinnedObject();
            
            m_allocator = Allocator.Persistent;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_vertexSafety, out m_DisposeSentinel, 1, Allocator.Persistent);
            m_normalSafety = AtomicSafetyHandle.Create();
            m_triangleSafety = AtomicSafetyHandle.Create();
            m_uvSafety = AtomicSafetyHandle.Create();
#if UNITY_2019_3_OR_NEWER
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_vertexSafety, true);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_normalSafety, true);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_triangleSafety, true);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_uvSafety, true);
#endif
#endif
        }

        public Vector3 GetVertex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_vertexSafety);
            if ((uint)index >= (uint)VertexCount)
                throw new IndexOutOfRangeException($"Index {index} is out of range in VertexArray of '{VertexCount}' Length.");
#endif
            return UnsafeUtility.ReadArrayElement<Vector3>(Vertices, index);
        }

        public Vector3 GetNormal(int index) 
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_normalSafety);
            if ((uint)index >= (uint)NormalCount)
                throw new IndexOutOfRangeException($"Index {index} is out of range in VertexArray of '{NormalCount}' Length.");
#endif
            return UnsafeUtility.ReadArrayElement<Vector3>(Normals, index);
        }

        public int GetTriangle(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_triangleSafety);
            if ((uint)index >= (uint)TriangleCount)
                throw new IndexOutOfRangeException($"Index {index} is out of range in VertexArray of '{TriangleCount}' Length.");
#endif
            return UnsafeUtility.ReadArrayElement<int>(Triangles, index);
        }
        
        public Vector2 GetUV(int index) 
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_uvSafety);
            if ((uint)index >= (uint)UVCount)
                throw new IndexOutOfRangeException($"Index {index} is out of range in VertexArray of '{UVCount}' Length.");
#endif
            return UnsafeUtility.ReadArrayElement<Vector2>(UV, index);
        }
        
        public void SetVertex(int index, in Vector3 value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_vertexSafety);
            if ((uint)index >= (uint)VertexCount)
                throw new IndexOutOfRangeException($"Index {index} is out of range in VertexArray of '{VertexCount}' Length.");
#endif
            UnsafeUtility.WriteArrayElement(Vertices, index, value);
        }

        public void SetNormal(int index, in Vector3 value) 
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_normalSafety);
            if ((uint)index >= (uint)NormalCount)
                throw new IndexOutOfRangeException($"Index {index} is out of range in VertexArray of '{NormalCount}' Length.");
#endif
            UnsafeUtility.WriteArrayElement(Normals, index, value);
        }

        public void SetTriangle(int index, in int value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_triangleSafety);
            if ((uint)index >= (uint)TriangleCount)
                throw new IndexOutOfRangeException($"Index {index} is out of range in VertexArray of '{TriangleCount}' Length.");
#endif
            UnsafeUtility.WriteArrayElement(Triangles, index, value);
        }

        public void SetUV(int index, in Vector2 value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_uvSafety);
            if ((uint)index >= (uint)UVCount)
                throw new IndexOutOfRangeException($"Index {index} is out of range in VertexArray of '{UVCount}' Length.");
#endif
            UnsafeUtility.WriteArrayElement(UV, index, value);
        }

        public bool IsCreated => Vertices != null && Normals != null && Triangles != null && UV != null;

        private void Release()
        {
            _verticesHandle.Free();
            _normalsHandle.Free();
            _trianglesHandle.Free();
            _uvHandle.Free();
            _meshHandle.Free();
            Vertices = null;
            Normals = null;
            Triangles = null;
            UV = null;
            VertexCount = 0;
            NormalCount = 0;
            TriangleCount = 0;
            UVCount = 0;
        }

        public void Dispose()
        {
            if (m_allocator < Allocator.None) // Not our responsibility to deallocate
            {
                Vertices = null;
                Normals = null;
                Triangles = null;
                UV = null;
                return;
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_vertexSafety, ref m_DisposeSentinel);
            AtomicSafetyHandle.Release(m_normalSafety);
            AtomicSafetyHandle.Release(m_triangleSafety);
            AtomicSafetyHandle.Release(m_uvSafety);
#endif
            Release();
        }
        
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (m_allocator < Allocator.None) // Not our responsibility to deallocate
            {
                Vertices = null;
                Normals = null;
                Triangles = null;
                UV = null;
                return default;
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
            var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_vertexSafety);
            AtomicSafetyHandle.Release(m_normalSafety);
            AtomicSafetyHandle.Release(m_triangleSafety);
            AtomicSafetyHandle.Release(m_uvSafety);
#endif
            Vertices = null;
            Normals = null;
            Triangles = null;
            UV = null;

            return jobHandle;
        }
        
        [BurstCompile]
        private struct DisposeJob : IJob
        {
            public MeshProxy Container;

            public void Execute()
            {
                Container.Release();
            }
        }
        
        public bool CopyFrom(MeshProxy other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_vertexSafety);
            if ((uint)other.VertexCount != (uint)VertexCount)
                throw new ArgumentException($"Vertex array size mismatch");
            UnsafeUtility.MemCpy(Vertices, other.Vertices, (long)sizeof(Vector3) * VertexCount);
            AtomicSafetyHandle.CheckWriteAndThrow(m_normalSafety);
            if ((uint)other.NormalCount != (uint)NormalCount)
                throw new ArgumentException($"Normal array size mismatch");
            UnsafeUtility.MemCpy(Normals, other.Normals, (long)sizeof(Vector3) * NormalCount);
            AtomicSafetyHandle.CheckWriteAndThrow(m_triangleSafety);
            if ((uint)other.TriangleCount != (uint)TriangleCount)
                throw new ArgumentException($"Triangle array size mismatch");
            UnsafeUtility.MemCpy(Triangles, other.Triangles, (long)sizeof(int) * TriangleCount);
            AtomicSafetyHandle.CheckWriteAndThrow(m_uvSafety);
            if ((uint)other.UVCount != (uint)UVCount)
                throw new ArgumentException($"UV array size mismatch");
            UnsafeUtility.MemCpy(UV, other.UV, (long)sizeof(Vector2) * UVCount);
#else
            if ((uint) other.VertexCount != (uint) VertexCount) return false;
            UnsafeUtility.MemCpy(Vertices, other.Vertices, (long)sizeof(Vector3) * VertexCount);
            if ((uint) other.NormalCount != (uint) NormalCount) return false;
            UnsafeUtility.MemCpy(Normals, other.Normals, (long)sizeof(Vector3) * NormalCount);
            if ((uint) other.TriangleCount != (uint) TriangleCount) return false;
            UnsafeUtility.MemCpy(Triangles, other.Triangles, (long)sizeof(int) * TriangleCount);
            if ((uint) other.UVCount != (uint) UVCount) return false;
            UnsafeUtility.MemCpy(UV, other.UV, (long)sizeof(Vector2) * UVCount);
#endif
            return true;
        }

        public bool CopyFrom(NativeMesh other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_vertexSafety);
            if ((uint)other.VertexCount != (uint)VertexCount)
                throw new ArgumentException($"Vertex array size mismatch");
            UnsafeUtility.MemCpy(Vertices, other.GetVerticesUnsafeReadOnlyPtr(), (long)sizeof(Vector3) * VertexCount);
            AtomicSafetyHandle.CheckWriteAndThrow(m_normalSafety);
            if ((uint)other.NormalCount != (uint)NormalCount)
                throw new ArgumentException($"Normal array size mismatch");
            UnsafeUtility.MemCpy(Normals, other.GetNormalsUnsafeReadOnlyPtr(), (long)sizeof(Vector3) * NormalCount);
            AtomicSafetyHandle.CheckWriteAndThrow(m_triangleSafety);
            if ((uint)other.TriangleCount != (uint)TriangleCount)
                throw new ArgumentException($"Triangle array size mismatch");
            UnsafeUtility.MemCpy(Triangles, other.GetTrianglesUnsafeReadOnlyPtr(), (long)sizeof(int) * TriangleCount);
            AtomicSafetyHandle.CheckWriteAndThrow(m_uvSafety);
            if ((uint)other.UVCount != (uint)UVCount)
                throw new ArgumentException($"UV array size mismatch");
#else
            if ((uint) other.VertexCount != (uint) VertexCount) return false;
            UnsafeUtility.MemCpy(Vertices, other.GetVerticesUnsafeReadOnlyPtr(), (long)sizeof(Vector3) * VertexCount);
            if ((uint) other.NormalCount != (uint) NormalCount) return false;
            UnsafeUtility.MemCpy(Normals, other.GetNormalsUnsafeReadOnlyPtr(), (long)sizeof(Vector3) * NormalCount);
            if ((uint) other.TriangleCount != (uint) TriangleCount) return false;
            UnsafeUtility.MemCpy(Triangles, other.GetTrianglesUnsafeReadOnlyPtr(), (long)sizeof(int) * TriangleCount);
            if ((uint) other.UVCount != (uint) UVCount) return false;
            UnsafeUtility.MemCpy(UV, other.GetUVUnsafeReadOnlyPtr(), (long)sizeof(Vector2) * UVCount);
#endif
            return true;
        }
        
        internal static NativeArray<T> AsArray<T>(in void* list, in int length) where T : unmanaged
        {
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list, length, Allocator.Invalid);
        }
        
        public static implicit operator MeshProxy(MeshProxyArray.MeshProxyElement element)
        {
            return new MeshProxy
            {
                Vertices = element.Vertices,
                Normals = element.Normals,
                Triangles = element.Triangles,
                UV = element.UV,
                VertexCount = element.VertexCount,
                NormalCount = element.NormalCount,
                TriangleCount = element.TriangleCount,
                UVCount = element.UVCount,
                m_allocator = Allocator.Invalid,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_vertexSafety = AtomicSafetyHandle.GetTempMemoryHandle(),
                m_normalSafety = AtomicSafetyHandle.GetTempMemoryHandle(),
                m_triangleSafety = AtomicSafetyHandle.GetTempMemoryHandle(),
                m_uvSafety = AtomicSafetyHandle.GetTempMemoryHandle()
#endif
            };
        }
    }

    internal struct MeshProxyDebugView
    {
        private MeshProxy _proxy;
        public MeshProxyDebugView(MeshProxy mesh)
        {
            _proxy = mesh;
        }

        public unsafe int[] Triangles => MeshProxy.AsArray<int>(_proxy.Triangles, _proxy.TriangleCount).ToArray();
        public unsafe Vector3[] Normals => MeshProxy.AsArray<Vector3>(_proxy.Normals, _proxy.NormalCount).ToArray();
        public unsafe Vector3[] Vertices => MeshProxy.AsArray<Vector3>(_proxy.Vertices, _proxy.VertexCount).ToArray();
        public unsafe Vector2[] UV => MeshProxy.AsArray<Vector2>(_proxy.UV, _proxy.UVCount).ToArray();
        
    }
}
