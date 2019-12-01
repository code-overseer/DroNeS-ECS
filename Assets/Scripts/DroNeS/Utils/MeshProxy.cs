using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace DroNeS.Utils
{
    public unsafe struct MeshProxy : IDisposable
    {
        private ulong _verticesHandle;
        private ulong _normalsHandle;
        private ulong _trianglesHandle;
        private ulong _uvHandle;

        public int VertexCount { get; private set; }
        public int NormalCount { get; private set; }
        public int TriangleCount { get; private set; }
        public int UVCount { get; private set; }

        private Vector3* Vertices;
        private Vector3* Normals;
        private int* Triangles;
        private Vector2* UV;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule] 
        private DisposeSentinel m_DisposeSentinel;
#endif

        public MeshProxy(Mesh mesh)
        {
            VertexCount = mesh.vertices.Length;
            NormalCount = mesh.normals.Length;
            TriangleCount = mesh.triangles.Length;
            UVCount = mesh.uv.Length;
            
            Vertices = (Vector3*)UnsafeUtility.PinGCArrayAndGetDataAddress(mesh.vertices, out _verticesHandle);
            Normals = (Vector3*)UnsafeUtility.PinGCArrayAndGetDataAddress(mesh.normals, out _normalsHandle);
            Triangles = (int*)UnsafeUtility.PinGCArrayAndGetDataAddress(mesh.triangles, out _trianglesHandle);
            UV = (Vector2*)UnsafeUtility.PinGCArrayAndGetDataAddress(mesh.uv, out _uvHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 1, Allocator.Persistent);
#endif
#if UNITY_2019_3_OR_NEWER && ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }
        
        public bool IsCreated => Vertices != null && Normals != null && Triangles != null && UV != null;

        private void Release()
        {
            UnsafeUtility.ReleaseGCObject(_verticesHandle);
            UnsafeUtility.ReleaseGCObject(_normalsHandle);
            UnsafeUtility.ReleaseGCObject(_trianglesHandle);
            UnsafeUtility.ReleaseGCObject(_uvHandle);
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            Release();
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

        public bool CopyFrom(NativeMesh mesh)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            if (mesh.VerticesCount > VertexCount) return false;
            UnsafeUtility.MemCpy(Vertices, mesh.GetVerticesUnsafeReadOnlyPtr(), sizeof(Vector3) * VertexCount);
            if (mesh.NormalsCount > NormalCount) return false;
            UnsafeUtility.MemCpy(Normals, mesh.GetNormalsUnsafeReadOnlyPtr(), sizeof(Vector3) *NormalCount);
            if (mesh.TrianglesCount > TriangleCount) return false;
            UnsafeUtility.MemCpy(Triangles, mesh.GetTrianglesUnsafeReadOnlyPtr(), sizeof(int) * TriangleCount);
            if (mesh.UVCount > UVCount) return false;
            UnsafeUtility.MemCpy(UV, mesh.GetUVUnsafeReadOnlyPtr(), sizeof(Vector2) * UVCount);
            return true;
        }


    }
}
