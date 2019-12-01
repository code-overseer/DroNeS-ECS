using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace DroNeS.Utils
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativeMeshListDebugView))]
    public unsafe struct MeshProxyList : IDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule] 
        private DisposeSentinel m_DisposeSentinel;
#endif
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_meshes;

        internal Allocator m_allocator;

        [StructLayout(LayoutKind.Sequential)]
        public struct MeshProxyElement : IDisposable
        {
            private readonly ulong _verticesHandle;
            private readonly ulong _normalsHandle;
            private readonly ulong _trianglesHandle;
            private readonly ulong _uvHandle;
            
            [NativeDisableUnsafePtrRestriction]
            internal void* Vertices;
            [NativeDisableUnsafePtrRestriction]
            internal void* Normals;
            [NativeDisableUnsafePtrRestriction]
            internal void* Triangles;
            [NativeDisableUnsafePtrRestriction]
            internal void* UV;
            public int VertexCount { get; private set; }
            public int NormalCount { get; private set; }
            public int TriangleCount { get; private set; }
            public int UVCount { get; private set; }

            internal MeshProxyElement(Mesh mesh, Allocator allocator)
            {
                VertexCount = mesh.vertices.Length;
                NormalCount = mesh.normals.Length;
                TriangleCount = mesh.triangles.Length;
                UVCount = mesh.uv.Length;
            
                Vertices = UnsafeUtility.PinGCArrayAndGetDataAddress(mesh.vertices, out _verticesHandle);
                Normals = UnsafeUtility.PinGCArrayAndGetDataAddress(mesh.normals, out _normalsHandle);
                Triangles = UnsafeUtility.PinGCArrayAndGetDataAddress(mesh.triangles, out _trianglesHandle);
                UV = UnsafeUtility.PinGCArrayAndGetDataAddress(mesh.uv, out _uvHandle);
            }

            public void Dispose()
            {
                UnsafeUtility.ReleaseGCObject(_verticesHandle);
                UnsafeUtility.ReleaseGCObject(_normalsHandle);
                UnsafeUtility.ReleaseGCObject(_trianglesHandle);
                UnsafeUtility.ReleaseGCObject(_uvHandle);
                Vertices = null;
                Normals = null;
                Triangles = null;
                UV = null;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [NativeContainer]
        [NativeContainerSupportsMinMaxWriteRestriction]
        public struct Parallel
        {
            internal void* m_meshes;
            public int Length { get; }
            private Allocator m_allocator;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
            private int m_MinIndex;
            private int m_MaxIndex;

            internal Parallel(in UnsafeList* list, in AtomicSafetyHandle safety, in Allocator allocator)
            {
                m_meshes = list->Ptr;
                Length = list->Length;
                m_allocator = allocator;
                m_Safety = safety;
                m_MinIndex = 0;
                m_MaxIndex = Length - 1;
            }
#else
            internal Parallel(in UnsafeList* list, in Allocator allocator)
            {
                m_meshes = list->Ptr;
                Length = list->Length;
                m_allocator = allocator;
            }
#endif
            public MeshProxy this[int index]
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                    if (index < m_MinIndex || index > m_MaxIndex)
                        FailOutOfRangeError(index);
#endif
                    var element = UnsafeUtility.ReadArrayElement<MeshProxyElement>(m_meshes, index);
                    return AsMeshProxy(element, m_Safety);
                }
            }

            [WriteAccessRequired]
            public void SetMesh(Mesh mesh, int index)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                if (index < m_MinIndex || index > m_MaxIndex)
                    FailOutOfRangeError(index);
#endif
                UnsafeUtility.WriteArrayElement(m_meshes, index, new MeshProxyElement(mesh, m_allocator));
            }
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private void FailOutOfRangeError(int index)
            {
                if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
                    throw new IndexOutOfRangeException(
                        $"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in ReadWriteBuffer.\n" +
                        "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
                        "You can use double buffering strategies to avoid race conditions due to " +
                        "reading & writing in parallel to the same elements from a job.");

                throw new IndexOutOfRangeException($"Index {index} is out of range of '{Length}' Length.");
            }
#endif
        }


        public int Length => m_meshes->Length;
        
        private static MeshProxy AsMeshProxy(MeshProxyElement element, in AtomicSafetyHandle instanceSafety)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(instanceSafety);
#endif
            return element;
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            Release();
        }
        
        private void Release()
        {
            var l = Length;

            for (var i = 0; i < l; ++i)
            {
                Release(i);
            }
            UnsafeList.Destroy(m_meshes);
            m_meshes = null;
        }

        private void Release(int index)
        {
            var element = UnsafeUtility.ReadArrayElement<MeshProxyElement>(m_meshes, index);
            element.Dispose();
        }
    }
}
