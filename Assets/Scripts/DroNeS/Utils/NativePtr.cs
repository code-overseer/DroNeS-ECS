using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming

namespace DroNeS.Utils
{
    [NativeContainer]
	[NativeContainerSupportsDeallocateOnJobCompletion]
	[DebuggerTypeProxy(typeof(NativePtrDebugView<>))]
	[DebuggerDisplay("Value = {" + nameof(Value) + "}")]
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct NativePtr<T> : IDisposable where T : unmanaged
	{
		[NativeContainer]
		[NativeContainerIsReadOnly]
		public struct Parallel
		{
			[NativeDisableUnsafePtrRestriction]
			internal readonly void* m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			
			internal AtomicSafetyHandle m_Safety;
			
			internal Parallel(void* value, AtomicSafetyHandle safety)
			{
				m_Buffer = value;
				m_Safety = safety;
			}
#else
			internal Parallel(void* value)
			{
				m_Buffer = value;
			}
#endif
			public T Value => *(T*)m_Buffer;
		}
		
		[NativeDisableUnsafePtrRestriction]
		internal void* m_Buffer;
		internal readonly Allocator m_AllocatorLabel;

		
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		private AtomicSafetyHandle m_Safety;
		
		[NativeSetClassTypeToNullOnSchedule]
		private DisposeSentinel m_DisposeSentinel;
#endif
		
		public NativePtr(T value, Allocator allocator)
		{
			if (allocator <= Allocator.None) throw new ArgumentException("Allocator must be Temp, TempJob or Persistent allocator");
			
			m_Buffer = UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);

			// Store the allocator to use when deallocating
			m_AllocatorLabel = allocator;

			// Create the dispose sentinel
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#else
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif
			UnsafeUtility.WriteArrayElement(m_Buffer, 0, value);
		}
		
		public T Value
		{
			get
			{
				RequireReadAccess();
				return *(T*)m_Buffer;
			}

			[WriteAccessRequired]
			set
			{
				RequireWriteAccess();
				*(T*)m_Buffer = value;
			}
		}
		
		public Parallel AsParallel()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var parallel = new Parallel(m_Buffer, m_Safety);
			AtomicSafetyHandle.UseSecondaryVersion(ref parallel.m_Safety);
#else
			Parallel parallel = new Parallel(m_Buffer);
#endif
			return parallel;
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
			UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
			m_Buffer = null;
		}

		[WriteAccessRequired]
		public JobHandle Dispose(JobHandle inputDeps)
		{
			if (m_Buffer == null) return inputDeps;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
			var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.Release(m_Safety);
#endif
			m_Buffer = null;

			return jobHandle;
		}
		
		[BurstCompile]
		private struct DisposeJob : IJob
		{
			public NativePtr<T> Container;
			public void Execute()
			{
				UnsafeUtility.Free(Container.m_Buffer, Container.m_AllocatorLabel);
				Container.m_Buffer = null;
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
	}
	
	internal sealed class NativePtrDebugView<T> where T : unmanaged
	{
		private readonly NativePtr<T> m_Ptr;
		
		public NativePtrDebugView(NativePtr<T> ptr)
		{
			m_Ptr = ptr;
		}
		
		public T Value => m_Ptr.Value;
	}
}