using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
    [NativeContainerSupportsMinMaxWriteRestriction]
	public unsafe struct NativeRoutineArray<T> : IDisposable where T : unmanaged, IRoutine
	{
		[NativeDisableUnsafePtrRestriction]
		internal void* m_Buffer;
		internal int m_Length;
		internal int m_MinIndex;
		internal int m_MaxIndex;
		internal Allocator m_AllocatorLabel;
		
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		private AtomicSafetyHandle m_Safety;
		
		[NativeSetClassTypeToNullOnSchedule]
		private DisposeSentinel m_DisposeSentinel;
#endif
		
		public NativeRoutineArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
		{
			Allocate(length, allocator, out this);
			if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory) return;
            
			UnsafeUtility.MemClear(m_Buffer, (long) Length * UnsafeUtility.SizeOf<T>());
		}

		private static void Allocate(int length, Allocator allocator, out NativeRoutineArray<T> array)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var size = (long) UnsafeUtility.SizeOf<T>() * length;
			if (allocator <= Allocator.None)
				throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof (allocator));
			if (length < 0)
				throw new ArgumentOutOfRangeException(nameof (length), "Length must be >= 0");
			if (size > (long) int.MaxValue)
				throw new ArgumentOutOfRangeException(nameof (length),
					$"Length * sizeof(T) cannot exceed {(object) int.MaxValue.ToString()} bytes");
#endif
			array = new NativeRoutineArray<T>
			{
				m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<T>(), allocator),
				m_Length = length,
				m_MinIndex = 0,
				m_MaxIndex = length - 1,
				m_AllocatorLabel = allocator
			};
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator);
#endif
		}

		public JobHandle MoveNext(JobHandle inputDeps)
		{
			var n = m_Length > 512 ? 16 : 64;
			return new MoveNextJob(this).Schedule(m_Length, n, inputDeps);
		}

		public void MoveNext()
		{
			for (var index = 0; index < m_Length; ++index)
			{
				((T*) ((IntPtr) m_Buffer + index * sizeof(T)))->MoveNext();
			}
		}
		
		private struct MoveNextJob : IJobParallelFor
		{
			private NativeRoutineArray<T> _routines;
			public MoveNextJob(NativeRoutineArray<T> routines)
			{
				_routines = routines;
			}
			public void Execute(int index)
			{
				((T*) ((IntPtr) _routines.m_Buffer + index * sizeof(T)))->MoveNext();
			}
		}

		public int Length => m_Length;
		
		public T this[int idx]
		{
			get
			{
				RequireReadAccess();
				return *(T*) ((IntPtr) m_Buffer + idx * sizeof(T));
			}
			
			set
			{
				RequireWriteAccess();
				*(T*) ((IntPtr) m_Buffer + idx * sizeof(T)) = value;
			}
		}

		public bool IsCreated => m_Buffer != null;

		[WriteAccessRequired]
		public void Dispose()
		{
			if (m_Buffer == null) return;
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
			for (var i = 0; i < m_Length; ++i)
			{
				Deallocate(i);
			}
			UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
			m_Buffer = null;
		}

		private void Deallocate(int idx)
		{
			((T*) ((IntPtr) m_Buffer + idx * sizeof(T)))->Dispose();
		}

		[WriteAccessRequired]
		public JobHandle Dispose(JobHandle inputDeps)
		{
			if (m_Buffer == null) return inputDeps;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
			var jobHandle = new DisposeJob { Container = this }.Schedule(
				new FreeMembers{Container = this}.Schedule(m_Length, 64, inputDeps));

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.Release(m_Safety);
#endif
			m_Buffer = null;

			return jobHandle;
		}
		
		[BurstCompile]
		private struct DisposeJob : IJob
		{
			public NativeRoutineArray<T> Container;
			public void Execute()
			{
				UnsafeUtility.Free(Container.m_Buffer, Container.m_AllocatorLabel);
				Container.m_Buffer = null;
			}
		}
		
		[BurstCompile]
		private struct FreeMembers : IJobParallelFor
		{
			public NativeRoutineArray<T> Container;
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
			var output = new T[m_Length];
			var gcHandle = GCHandle.Alloc((object) output, GCHandleType.Pinned);
            var ptr = gcHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy((void*)ptr, m_Buffer, m_Length * UnsafeUtility.SizeOf<T>());
            gcHandle.Free();
            return output;
		}
	}
	
	internal sealed class NativeRoutineArrayDebugView<T> where T : unmanaged, IRoutine
	{
		private NativeRoutineArray<T> m_Ptr;
		
		public NativeRoutineArrayDebugView(NativeRoutineArray<T> ptr)
		{
			m_Ptr = ptr;
		}
		
		public T[] Items => m_Ptr.AsArray();
	}
}
