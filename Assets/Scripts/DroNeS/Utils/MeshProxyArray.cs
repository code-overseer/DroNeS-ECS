using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace DroNeS.Utils
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    [DebuggerDisplay("Length = {" + nameof(Length) + "}")]
    [DebuggerTypeProxy(typeof(MeshProxyArrayDebugView))]
    public unsafe struct MeshProxyArray : IDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule] 
        private DisposeSentinel m_DisposeSentinel;
        private int m_MinIndex;
        private int m_MaxIndex;
#endif
        [NativeDisableUnsafePtrRestriction]
        internal void* m_Meshes;
        internal int m_Length;
        internal Allocator m_allocator;

        [StructLayout(LayoutKind.Sequential)]
        public struct MeshProxyElement : IDisposable
        {
            private GCHandle _meshHandle;
            private GCHandle _verticesHandle;
            private GCHandle _normalsHandle;
            private GCHandle _trianglesHandle;
            private GCHandle _uvHandle;
            
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

            internal MeshProxyElement(Mesh mesh)
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
            }

            public void Dispose()
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
            }
        }
        
        public MeshProxyArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Allocate(length, allocator, out this);
            if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory) return;
            
            UnsafeUtility.MemClear(this.m_Meshes, (long) this.Length * (long) UnsafeUtility.SizeOf<MeshProxyElement>());
        }

        private static void Allocate(int length, Allocator allocator, out MeshProxyArray array)
        {
            var size = (long) UnsafeUtility.SizeOf<MeshProxyElement>() * length;
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof (allocator));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof (length), "Length must be >= 0");
            if (size > (long) int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof (length),
                    $"Length * sizeof(T) cannot exceed {(object) int.MaxValue} bytes");

            array = new MeshProxyArray
            {
                m_Meshes = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<MeshProxyElement>(), allocator),
                m_Length = length,
                m_allocator = allocator,
                m_MinIndex = 0,
                m_MaxIndex = length - 1
            };
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator);
#endif
        }
        
        public int Length => m_Length;

        public MeshProxy this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                if ((uint)index >= (uint)m_MaxIndex || index < m_MinIndex)
                    FailOutOfRangeError(index);
#endif
                var element = UnsafeUtility.ReadArrayElement<MeshProxyElement>(m_Meshes, index);
                return AsMeshProxy(element, m_Safety);
            }
        }
        
        public void SetMesh(Mesh mesh, int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            if ((uint)index >= (uint)m_MaxIndex || index < m_MinIndex)
                FailOutOfRangeError(index);
#endif
            UnsafeUtility.WriteArrayElement(m_Meshes, index, new MeshProxyElement(mesh));
        }
        
        public void SetMesh(RenderMesh mesh, int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            if ((uint)index >= (uint)m_MaxIndex || index < m_MinIndex)
                FailOutOfRangeError(index);
#endif
            UnsafeUtility.WriteArrayElement(m_Meshes, index, new MeshProxyElement(mesh.mesh));
        }

        public bool IsCreated => (IntPtr) this.m_Meshes != IntPtr.Zero;

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
            UnsafeUtility.Free(m_Meshes, m_allocator);
            m_Meshes = null;
            m_Length = 0;
        }

        private void Release(int index)
        {
            var element = UnsafeUtility.ReadArrayElement<MeshProxyElement>(m_Meshes, index);
            element.Dispose();
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

    internal struct MeshProxyArrayDebugView
    {
        private MeshProxyArray m_Array;

        public MeshProxyArrayDebugView(MeshProxyArray array)
        {
            m_Array = array;
        }

        public Mesh[] Items 
        {
            get
            {
                var output = new Mesh[m_Array.Length];
                for (var i = 0; i < output.Length; ++i)
                {
                    output[i] = (Mesh) m_Array[i]._meshHandle.Target;
                }

                return output;
            }
            
        }
    }
    
    public static class MeshProxyArrayUtilities 
    {
        public static unsafe void* GetUnsafePtr(this MeshProxyArray list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_Safety);
#endif
            return list.m_Meshes;
        }
        
        public static unsafe void* GetUnsafeReadOnlyPtr(this MeshProxyArray list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_Safety);
#endif
            return list.m_Meshes;
        }
        
        public static unsafe MeshProxyArray.MeshProxyElement* GetUnsafePtr(this MeshProxyArray list, int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_Safety);
#endif
            return (MeshProxyArray.MeshProxyElement*)((IntPtr)list.m_Meshes + index * sizeof(MeshProxyArray.MeshProxyElement));
        }
        
        public static unsafe MeshProxyArray.MeshProxyElement* GetUnsafeReadOnlyPtr(this MeshProxyArray list, int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_Safety);
#endif
            return (MeshProxyArray.MeshProxyElement*)((IntPtr)list.m_Meshes + index * sizeof(MeshProxyArray.MeshProxyElement));
        }
        private unsafe class ProxyAssignmentTask : ITask
        {
            private GCHandle _handle;
            private readonly Mesh _mesh;
            private readonly MeshProxyArray.MeshProxyElement* _proxy;
            public GCHandle Handle 
            {
                get
                {
                    if (_handle == default) _handle = GCHandle.Alloc(this, GCHandleType.Pinned);
                    return _handle;
                }
            }

            public ProxyAssignmentTask(in RenderMesh mesh, in MeshProxyArray array, int index)
            {
                _proxy = array.GetUnsafePtr(index);
                _mesh = mesh.mesh;
            }
            
            public ProxyAssignmentTask(in Mesh mesh, in MeshProxyArray array, int index)
            {
                _proxy = array.GetUnsafePtr(index);
                _mesh = mesh;
            }

            public void Execute()
            {
                *_proxy = new MeshProxyArray.MeshProxyElement(_mesh);
            }
        }

        private struct MeshProxyAssignmentJob : IJobParallelFor
        {
            [DeallocateOnJobCompletion]
            public NativeArray<GCHandle> Tasks;
            public void Execute(int index)
            {
                var task = (ITask)Tasks[index].Target;
                task.Execute();
                task.Handle.Free();
            }
        }

        public static MeshProxyArray GenerateArray(RenderMesh[] mesh, Allocator allocator)
        {
            var array = new MeshProxyArray(mesh.Length, allocator);
            for (var i = 0; i < mesh.Length; ++i)
            {
                array.SetMesh(mesh[i], i);
            }

            return array;
        }
        
        public static MeshProxyArray GenerateArray(Mesh[] mesh, Allocator allocator)
        {
            var array = new MeshProxyArray(mesh.Length, allocator);
            for (var i = 0; i < mesh.Length; ++i)
            {
                array.SetMesh(mesh[i], i);
            }

            return array;
        }

        public static JobHandle GenerateArray(RenderMesh[] mesh, Allocator allocator, JobHandle inputDeps, out MeshProxyArray array)
        {
            array = new MeshProxyArray(mesh.Length, allocator);
            var tasks = new NativeArray<GCHandle>(mesh.Length, Allocator.TempJob);
            for (var i = 0; i < mesh.Length; ++i)
            {
                tasks[i] = new ProxyAssignmentTask(mesh[i], array, i).Handle;
            }
            
            return new MeshProxyAssignmentJob{ Tasks = tasks }.Schedule(mesh.Length, 32, inputDeps);
        }
        
        public static JobHandle GenerateArray(Mesh[] mesh, Allocator allocator, JobHandle inputDeps, out MeshProxyArray array)
        {
            array = new MeshProxyArray(mesh.Length, allocator);
            var tasks = new NativeArray<GCHandle>(mesh.Length, Allocator.TempJob);
            for (var i = 0; i < mesh.Length; ++i)
            {
                tasks[i] = new ProxyAssignmentTask(mesh[i], array, i).Handle;
            }
            
            return new MeshProxyAssignmentJob{ Tasks = tasks }.Schedule(mesh.Length, 32, inputDeps);
        }

    }
}
