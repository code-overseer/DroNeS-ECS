using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DroNeS.Utils.Interfaces;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace DroNeS.Utils
{
    [NativeContainer]
	[NativeContainerSupportsDeallocateOnJobCompletion]
	[DebuggerTypeProxy(typeof(NativeRoutineArrayDebugView<>))]
	[DebuggerDisplay("Length = {" + nameof(Length) + "}")]
	[StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeRoutines<T> : IDisposable where T : unmanaged, IRoutine
	{
		[NativeDisableUnsafePtrRestriction]
		internal UnsafeList* m_ListData;

		internal Allocator m_AllocatorLabel;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		internal AtomicSafetyHandle m_Safety;

		[NativeSetClassTypeToNullOnSchedule] 
		private DisposeSentinel m_DisposeSentinel;
#endif
		
		public NativeRoutines(Allocator allocator)
			: this(1, allocator, 2)
		{
		}

		public NativeRoutines(int initialCapacity, Allocator allocator)
			: this(initialCapacity, allocator, 2)
		{
		}

		private NativeRoutines(int initialCapacity, Allocator allocator, int disposeSentinelStackDepth)
		{
			var totalSize = (long)sizeof(T) * initialCapacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			// Native allocation is only valid for Temp, Job and Persistent.
			if (allocator <= Allocator.None)
				throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
			if (totalSize > int.MaxValue)
				throw new ArgumentOutOfRangeException($"Capacity has exceeded {int.MaxValue.ToString()} bytes");

			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
#endif
			m_ListData = UnsafeList.Create(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), initialCapacity, allocator);
            
			m_AllocatorLabel = allocator;

#if UNITY_2019_3_OR_NEWER && ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
		}
		
		[StructLayout(LayoutKind.Sequential)]
		[NativeContainer]
		[NativeContainerSupportsMinMaxWriteRestriction]
		private struct Parallel
		{
			[NativeDisableUnsafePtrRestriction]
			internal void* m_Buffer;
			
			internal int m_Length;
			internal int m_MinIndex;
			internal int m_MaxIndex;
			internal Allocator m_AllocatorLabel;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			internal AtomicSafetyHandle m_Safety;

			internal Parallel(in UnsafeList* list, in AtomicSafetyHandle safety, in Allocator allocator)
			{
				m_Buffer = list->Ptr;
				m_Length = list->Length;
				m_AllocatorLabel = allocator;
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
                m_AllocatorLabel = allocator;
            }
#endif
			public int Length => m_Length;
			public T this[int index]
			{
				get
				{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
					if (index < m_MinIndex || index > m_MaxIndex)
						FailOutOfRangeError(index);
#endif
					var element = UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
					return element;
				}
                
				[WriteAccessRequired]
				set
				{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
					if (index < m_MinIndex || index > m_MaxIndex)
						FailOutOfRangeError(index);
#endif
					UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
				}
			}
            
			[WriteAccessRequired]
			public void Deallocate(int idx)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				if (idx < m_MinIndex || idx > m_MaxIndex)
					FailOutOfRangeError(idx);
                
#endif
				((T*) ((IntPtr) m_Buffer + idx * sizeof(T)))->Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.Release(m_Safety);
#endif
				m_Buffer = null;
			}

			[WriteAccessRequired]
			public bool MoveNext(int idx)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				if (idx < m_MinIndex || idx > m_MaxIndex)
					FailOutOfRangeError(idx);
#endif
				return ((T*) ((IntPtr) m_Buffer + idx * sizeof(T)))->MoveNext();
			}
			
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			private void FailOutOfRangeError(int index)
			{
				if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
					throw new IndexOutOfRangeException(
						$"Index {index.ToString()} is out of restricted IJobParallelFor range " +
						$"[{m_MinIndex.ToString()}...{m_MaxIndex.ToString()}] in ReadWriteBuffer.\n" +
						"ReadWriteBuffers are restricted to only read & write the element at the job index. " +
						"You can use double buffering strategies to avoid race conditions due to " +
						"reading & writing in parallel to the same elements from a job.");

				throw new IndexOutOfRangeException($"Index {index.ToString()} is out of range of '{Length.ToString()}' Length.");
			}
#endif
		}

		public JobHandle MoveNext(JobHandle inputDeps)
		{
			var boolean = new NativeArray<Bool>(Length, Allocator.TempJob);
			var n = Length > 512 ? 16 : 32;
			var handle = new MoveNextJob(this, boolean).Schedule(Length, n, inputDeps);

			return new RemovalJob(this, boolean).Schedule(handle);
		}

		public void MoveNext()
		{
			var indices = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), Length, Allocator.Temp);
			for (var index = 0; index < Length; ++index)
			{
				if (((T*) ((IntPtr) m_ListData->Ptr + index * sizeof(T)))->MoveNext()) continue;
				indices->Add(index);
			}

			for (var j = indices->Length - 1; j >= 0; ++j)
			{
				m_ListData->RemoveAtSwapBack<T>(UnsafeUtility.ReadArrayElement<int>(indices->Ptr, j));
			}
			UnsafeList.Destroy(indices);
		}
		
		private struct MoveNextJob : IJobParallelFor
		{
			[WriteOnly]
			private Parallel _routines;
			[WriteOnly]
			private NativeArray<Bool> _toRemove; 
			public MoveNextJob(NativeRoutines<T> routines, NativeArray<Bool> remove)
			{
				_routines = routines.AsParallel();
				_toRemove = remove;
			}
			public void Execute(int index)
			{
				if (_routines.MoveNext(index)) return;
				_toRemove[index] = true;
			}
		}

		[BurstCompile]
		private struct RemovalJob : IJob
		{
			private NativeRoutines<T> _list;
			[DeallocateOnJobCompletion]
			private NativeArray<Bool> _toRemove;

			public RemovalJob(NativeRoutines<T> list, NativeArray<Bool>  bitmap)
			{
				_list = list;
				_toRemove = bitmap;
			}
			public void Execute()
			{
				for (var j = _list.Length - 1; j >= 0; ++j)
				{
					if (_toRemove[j]) _list.RemoveAtSwapBack(j);
				}
			}
		}

		private Parallel AsParallel()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var parallel = new Parallel(m_ListData, m_Safety, m_AllocatorLabel);
			AtomicSafetyHandle.UseSecondaryVersion(ref parallel.m_Safety);
#else
			Parallel parallel = new Parallel(m_Buffer, m_AllocatorLabel);
#endif
			return parallel;  
		}


		public int Length => m_ListData->Length;
		
		public T this[int index]
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				if ((uint)index >= (uint)m_ListData->Length || index < 0)
					throw new IndexOutOfRangeException($"Index {index.ToString()} is out of range in NativeList of '{m_ListData->Length.ToString()}' Length.");
#endif
				var element = UnsafeUtility.ReadArrayElement<T>(m_ListData->Ptr, index);
				return element;
			}
			[WriteAccessRequired]
			set
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				if ((uint)index >= (uint)m_ListData->Length || index < 0)
					throw new IndexOutOfRangeException($"Index {index.ToString()} is out of range in NativeList of '{m_ListData->Length.ToString()}' Length.");
#endif
				UnsafeUtility.WriteArrayElement(m_ListData->Ptr, index, value);
			}
		}

		public void Add(T item)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
			m_ListData->Add(item);
		}

		public void RemoveAtSwapBack(int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);

			if (index < 0 || index >= Length)
				throw new ArgumentOutOfRangeException(index.ToString());
#endif
			m_ListData->RemoveAtSwapBack<T>(index);
		}

		public bool IsCreated => m_ListData != null;

		[WriteAccessRequired]
		public void Dispose()
		{
			if (m_ListData == null) return;
			RequireWriteAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
			DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
			DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif
			Deallocate();
		}

		private void Deallocate()
		{
			for (var i = 0; i < Length; ++i)
			{
				Deallocate(i);
			}
			UnsafeUtility.Free(m_ListData, m_AllocatorLabel);
			m_ListData = null;
		}

		private void Deallocate(int idx)
		{
			((T*) ((IntPtr) m_ListData->Ptr + idx * sizeof(T)))->Dispose();
		}

		[WriteAccessRequired]
		public JobHandle Dispose(JobHandle inputDeps)
		{
			if (m_ListData == null) return inputDeps;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
			var jobHandle = new DisposeJob { Container = this }.Schedule(
				new FreeMembers{Container = this}.Schedule(Length, 64, inputDeps));

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.Release(m_Safety);
#endif
			m_ListData = null;

			return jobHandle;
		}
		
		[BurstCompile]
		private struct DisposeJob : IJob
		{
			public NativeRoutines<T> Container;
			public void Execute()
			{
				UnsafeUtility.Free(Container.m_ListData, Container.m_AllocatorLabel);
				Container.m_ListData = null;
			}
		}
		
		[BurstCompile]
		private struct FreeMembers : IJobParallelFor
		{
			public NativeRoutines<T> Container;
			public void Execute(int index)
			{
				Container.Deallocate(index);
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireReadAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireWriteAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		}

		public T[] AsArray()
		{
			var output = new T[Length];
			var gcHandle = GCHandle.Alloc((object) output, GCHandleType.Pinned);
            var ptr = gcHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy((void*)ptr, m_ListData->Ptr, Length * UnsafeUtility.SizeOf<T>());
            gcHandle.Free();
            return output;
		}
	}
	
	internal sealed class NativeRoutineArrayDebugView<T> where T : unmanaged, IRoutine
	{
		private NativeRoutines<T> m_Ptr;
		
		public NativeRoutineArrayDebugView(NativeRoutines<T> ptr)
		{
			m_Ptr = ptr;
		}
		
		public T[] Items => m_Ptr.AsArray();
	}
}
