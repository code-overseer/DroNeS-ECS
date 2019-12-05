using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DroNeS.Mapbox.Custom;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.UIElements;

namespace DroNeS.Utils
{
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct UnsafeListContainer : IDisposable
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

        private void AddRange<T>(void* elements, int count) where T : unmanaged
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

        public void Dispose()
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
