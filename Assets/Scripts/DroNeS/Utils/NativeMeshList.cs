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
    [DebuggerDisplay("Length = {" + nameof(Length) + "}")]
    [DebuggerTypeProxy(typeof(NativeMeshListDebugView))]
    public unsafe struct NativeMeshList : IDisposable
    {
        private static readonly int MaxMeshSize = 130000 * sizeof(Vector3) + 195000 * sizeof(int) + 65000 * sizeof(Vector2);
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule] 
        private DisposeSentinel m_DisposeSentinel;
#endif
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_Buffer;

        internal Allocator m_allocator;

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMeshElement
        {
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

            internal NativeMeshElement(NativeMesh mesh, Allocator allocator)
            {
                var length = mesh.m_vertices->Length;
                m_vertices = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), length, allocator);
                UnsafeUtility.MemCpy(m_vertices, mesh.m_vertices, (long)sizeof(Vector3) * length);

                length = mesh.m_normals->Length;
                m_normals = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), length, allocator);
                UnsafeUtility.MemCpy(m_normals, mesh.m_normals, (long)sizeof(Vector3) * length);
                
                length = mesh.m_triangles->Length;
                m_triangles = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), length, allocator);
                UnsafeUtility.MemCpy(m_triangles, mesh.m_triangles, (long)sizeof(int) * length);
                
                length = mesh.m_uv->Length;
                m_uv = UnsafeList.Create(UnsafeUtility.SizeOf<Vector2>(), UnsafeUtility.AlignOf<Vector2>(), length, allocator);
                UnsafeUtility.MemCpy(m_uv, mesh.m_uv, (long)sizeof(Vector2) * length);
                
                length = mesh.m_edges->Length;
                m_edges = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), length, allocator);
                UnsafeUtility.MemCpy(m_edges, mesh.m_edges, (long)sizeof(int) * length);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [NativeContainer]
        [NativeContainerSupportsMinMaxWriteRestriction]
        public struct Parallel
        {

            [NativeDisableUnsafePtrRestriction]
            internal void* m_Buffer;
            internal int m_Length;
            internal int m_MinIndex;
            internal int m_MaxIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;

            internal Parallel(in UnsafeList* list, in AtomicSafetyHandle safety, in Allocator allocator)
            {
                m_Buffer = list->Ptr;
                m_Length = list->Length;
                m_Allocator = allocator;
                m_Safety = safety;
                m_MinIndex = 0;
                m_MaxIndex = m_Length - 1;
            }
#else
            internal Parallel(in UnsafeList* list, in Allocator allocator)
            {
                m_meshes = list->Ptr;
                Length = list->Length;
                m_MinIndex = 0;
				m_MaxIndex = m_Length - 1;
                m_allocator = allocator;
            }
#endif
            private Allocator m_Allocator;
            public int Length => m_Length;
            public NativeMesh this[int index]
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                    if (index < m_MinIndex || index > m_MaxIndex)
                        FailOutOfRangeError(index);
#endif
                    var element = UnsafeUtility.ReadArrayElement<NativeMeshElement>(m_Buffer, index);
                    return AsNativeMesh(element, m_Safety);
                }
                
                [WriteAccessRequired]
                set
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                    if (index < m_MinIndex || index > m_MaxIndex)
                        FailOutOfRangeError(index);
#endif
                    UnsafeUtility.WriteArrayElement(m_Buffer, index, new NativeMeshElement(value, m_Allocator));
                }
            }
            
            [WriteAccessRequired]
            public void Deallocate(int index)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                if (index < m_MinIndex || index > m_MaxIndex)
                    FailOutOfRangeError(index);
                
#endif
                var element = (NativeMeshElement*) ( (IntPtr) m_Buffer + index * sizeof(NativeMeshElement));
                UnsafeList.Destroy(element->m_vertices);
                UnsafeList.Destroy(element->m_normals);
                UnsafeList.Destroy(element->m_triangles);
                UnsafeList.Destroy(element->m_uv);
                UnsafeList.Destroy(element->m_edges);
                
                element->m_vertices = null;
                element->m_normals = null;
                element->m_triangles = null;
                element->m_uv = null;
                element->m_edges = null;
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

        public NativeMeshList(Allocator allocator)
            : this(1, allocator, 2)
        {
        }

        public NativeMeshList(int initialCapacity, Allocator allocator)
            : this(initialCapacity, allocator, 2)
        {
        }

        private NativeMeshList(int meshes, Allocator allocator,
            int disposeSentinelStackDepth)
        {
            var totalSize = (long)MaxMeshSize * meshes;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent.
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException($"Capacity has exceeded {int.MaxValue} bytes");

            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
#endif
            m_Buffer = UnsafeList.Create(UnsafeUtility.SizeOf<NativeMeshElement>(), UnsafeUtility.AlignOf<NativeMeshElement>(), meshes, allocator);
            
            m_allocator = allocator;

#if UNITY_2019_3_OR_NEWER && ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }
        
        public NativeMesh this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                if ((uint)index >= (uint)m_Buffer->Length || index < 0)
                    throw new IndexOutOfRangeException($"Index {index} is out of range in NativeList of '{m_Buffer->Length}' Length.");
#endif
                var element = UnsafeUtility.ReadArrayElement<NativeMeshElement>(m_Buffer->Ptr, index);
                return AsNativeMesh(element, m_Safety);
            }
            [WriteAccessRequired]
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                if ((uint)index >= (uint)m_Buffer->Length || index < 0)
                    throw new IndexOutOfRangeException($"Index {index} is out of range in NativeList of '{m_Buffer->Length}' Length.");
#endif
                UnsafeUtility.WriteArrayElement(m_Buffer->Ptr, index, new NativeMeshElement(value, m_allocator));
            }
        }
        
        public int Length
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_Buffer->Length;
            }
        }
        
        public int Capacity
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_Buffer->Capacity;
            }

            [WriteAccessRequired]
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
                if (value < m_Buffer->Length)
                    throw new ArgumentException("Capacity must be larger than the length of the NativeList.");
#endif
                m_Buffer->SetCapacity<NativeMeshElement>(value);
            }
        }
        
        public void Add(NativeMesh element)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_Buffer->Add(new NativeMeshElement(element, m_allocator));
        }

        public void RemoveAtSwapBack(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);

            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException(index.ToString());
#endif
            m_Buffer->RemoveAtSwapBack<NativeMeshElement>(index);
        }

        public Parallel AsParallel()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var parallel = new Parallel(m_Buffer, m_Safety, m_allocator);
            AtomicSafetyHandle.UseSecondaryVersion(ref parallel.m_Safety);
#else
			Parallel parallel = new Parallel(m_Buffer, m_allocator);
#endif
            return parallel;     
        }

        private void Deallocate()
        {
            var l = Length;

            for (var i = 0; i < l; ++i)
            {
                Deallocate(i);
            }
            UnsafeList.Destroy(m_Buffer);
            m_Buffer = null;
        }
        
        private void Deallocate(int index)
        {
            var element = (NativeMeshElement*) ( (IntPtr) m_Buffer->Ptr + index * sizeof(NativeMeshElement));
            UnsafeList.Destroy(element->m_normals);
            UnsafeList.Destroy(element->m_triangles);
            UnsafeList.Destroy(element->m_vertices);
            UnsafeList.Destroy(element->m_uv);
            UnsafeList.Destroy(element->m_edges);
                
            element->m_vertices = null;
            element->m_normals = null;
            element->m_triangles = null;
            element->m_uv = null;
            element->m_edges = null;
        }
        
        public bool IsCreated => m_Buffer != null;

        [WriteAccessRequired]
        public void Dispose()
        {
            if (!IsCreated) return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            Deallocate();
            m_Buffer = null;
        }
        
        [WriteAccessRequired]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (!IsCreated) return inputDeps;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
            var jobHandle = new DisposeJob {Container = this}.Schedule(inputDeps);
            m_Buffer = null;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            return jobHandle;
        }
        
        [BurstCompile]
        private struct DisposeJob : IJob
        {
            public NativeMeshList Container;
            public void Execute()
            {
                Container.Deallocate();
            }
        }
        
        [WriteAccessRequired]
        public void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            var l = Length;

            for (var i = 0; i < l; ++i)
            {
                Clear(i);
            }
            m_Buffer->Clear();
        }

        private void Clear(int index)
        {
            var element = (NativeMeshElement*) ( (IntPtr) m_Buffer->Ptr + index * sizeof(NativeMeshElement));
            element->m_triangles->Clear();
            element->m_vertices->Clear();
            element->m_normals->Clear();
            element->m_uv->Clear();
            element->m_edges->Clear();
        }

        private static NativeMesh AsNativeMesh(NativeMeshElement element, in AtomicSafetyHandle instanceSafety)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(instanceSafety);
#endif
            return new NativeMesh
            {
                m_allocator = Allocator.Invalid,
                m_triangles = element.m_triangles,
                m_normals = element.m_normals,
                m_vertices = element.m_vertices,
                m_uv = element.m_uv,
                m_edges = element.m_edges,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_vertexSafety = default,
                m_normalSafety = default,
                m_triangleSafety = default,
                m_uvSafety = default,
                m_edgeSafety = default
#endif
            };
        }

        public Mesh AsMesh(int index) => this[index].AsMesh();
    }

    internal struct NativeMeshListDebugView
    {
        private NativeMeshList m_List;

        public NativeMeshListDebugView(NativeMeshList meshList)
        {
            m_List = meshList;
        }
        
        public Mesh[] Items 
        {
            get
            {
                var output = new Mesh[m_List.Length];
                for (var i = 0; i < output.Length; ++i)
                {
                    output[i] = new Mesh
                    {
                        vertices = m_List[i].VerticesArray(),
                        normals = m_List[i].NormalsArray(),
                        triangles = m_List[i].TriangleArray(),
                        uv = m_List[i].UVArray()
                    };
                }

                return output;
            }
            
        }
    }

    public static class NativeMeshListUtilities
    {
        private static unsafe NativeMeshList.NativeMeshElement Convert(in MeshDataStruct data, Allocator allocator)
        {
            var output = new NativeMeshList.NativeMeshElement();
            var length = data.Vertices.Length;
            output.m_vertices = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), length, allocator);
            UnsafeUtility.MemCpy(output.m_vertices, data.Vertices.GetUnsafeReadOnlyPtr(), (long)sizeof(Vector3) * length);

            length = data.Normals.Length;
            output.m_normals = UnsafeList.Create(UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), length, allocator);
            UnsafeUtility.MemCpy(output.m_normals, data.Normals.GetUnsafeReadOnlyPtr(), (long)sizeof(Vector3) * length);
                
            length = data.Triangles.Length;
            output.m_triangles = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), length, allocator);
            UnsafeUtility.MemCpy(output.m_triangles, data.Triangles.GetUnsafeReadOnlyPtr(), (long)sizeof(int) * length);
                
            length = data.UV.Length;
            output.m_uv = UnsafeList.Create(UnsafeUtility.SizeOf<Vector2>(), UnsafeUtility.AlignOf<Vector2>(), length, allocator);
            UnsafeUtility.MemCpy(output.m_uv, data.UV.GetUnsafeReadOnlyPtr(), (long)sizeof(Vector2) * length);
            
            length = data.Edges.Length;
            output.m_edges = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), length, allocator);
            UnsafeUtility.MemCpy(output.m_edges, data.Edges.GetUnsafeReadOnlyPtr(), (long)sizeof(int) * length);

            return output;

        }
        
        public static unsafe void Add(this NativeMeshList list, MeshDataStruct data)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(list.m_Safety);
#endif
            list.m_Buffer->Add(Convert(data, list.m_allocator));
        }
    }
}
