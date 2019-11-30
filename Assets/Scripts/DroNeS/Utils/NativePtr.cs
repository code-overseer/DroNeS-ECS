using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

// ReSharper disable InconsistentNaming

namespace DroNeS.Utils
{
    /// <summary>
    ///   <para>A NativePtr exposes a buffer of native memory to managed code, making it possible to share data between managed and native without marshalling costs.</para>
    /// </summary>
    [DebuggerTypeProxy(typeof(NativePtrDebugView<>))]
    [NativeContainer]
    [NativeContainerSupportsDeferredConvertListToArray]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [DebuggerDisplay("Length = {Length}")]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct NativePtr<T> : IDisposable, IEquatable<NativePtr<T>> where T : unmanaged
    {
        internal AtomicSafetyHandle m_Safety;
        [NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;

        [NativeDisableUnsafePtrRestriction] internal unsafe void* m_Buffer;
        internal Allocator m_AllocatorLabel;

        public unsafe NativePtr(Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Allocate(allocator, out this);
            if ((NativeArrayOptions.ClearMemory & options) != NativeArrayOptions.ClearMemory) return;

            UnsafeUtility.MemClear(m_Buffer, (long) UnsafeUtility.SizeOf<T>());
        }

        public NativePtr(T item, Allocator allocator)
        {
            Allocate(allocator, out this);
            Copy(item, this);
        }

        public NativePtr(NativePtr<T> array, Allocator allocator)
        {
            Allocate(allocator, out this);
            Copy(array, this);
        }

        private static unsafe void Allocate(Allocator allocator, out NativePtr<T> array)
        {
            var size = (long) UnsafeUtility.SizeOf<T>();
            array = new NativePtr<T>();
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            
            DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 2, allocator);
#endif

            array.m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<T>(), allocator);
            array.m_AllocatorLabel = allocator;
#if UNITY_2019_3_OR_NEWER && ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        public unsafe bool IsCreated => (IntPtr) m_Buffer != IntPtr.Zero;

        [WriteAccessRequired]
        public unsafe void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            
            if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
                throw new InvalidOperationException(
                    "The NativePtr can not be Disposed because it was not allocated with a valid allocator.");
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif            
            UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
            m_Buffer = null;
        }

        [WriteAccessRequired]
        public void CopyFrom(T array)
        {
            Copy(array, this);
        }

        [WriteAccessRequired]
        public void CopyFrom(NativePtr<T> array)
        {
            Copy(array, this);
        }

        public void CopyTo(NativePtr<T> array)
        {
            Copy(this, array);
        }

        public unsafe T Value
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                if (!IsCreated) throw new NullReferenceException("Invalid read operation, ptr was not allocated");
#endif
                return *(T*)m_Buffer;
            }
            [WriteAccessRequired]
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                if (!IsCreated) throw new NullReferenceException("Invalid write operation, ptr was not allocated");
#endif
                *(T*)m_Buffer = value;
            }
        }

        public unsafe bool Equals(NativePtr<T> other)
        {
            return m_Buffer == other.m_Buffer;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;

            return obj is NativePtr<T> ptr && Equals(ptr);
        }

        public override unsafe int GetHashCode()
        {
            return ((int) m_Buffer * 397) ^ 1;
        }

        public static bool operator ==(NativePtr<T> left, NativePtr<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NativePtr<T> left, NativePtr<T> right)
        {
            return !left.Equals(right);
        }

        public static unsafe void Copy(T src, NativePtr<T> dst)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            if (!dst.IsCreated) throw new NullReferenceException("Invalid write operation, ptr was not allocated");
#endif

            var ptr = UnsafeUtility.AddressOf(ref src);
            UnsafeUtility.MemCpy(dst.m_Buffer, ptr, UnsafeUtility.SizeOf<T>());
        }

        public static unsafe void Copy(NativePtr<T> src, NativePtr<T> dst)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            if (!dst.IsCreated) throw new NullReferenceException("Invalid write operation, ptr was not allocated");
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            if (!src.IsCreated) throw new NullReferenceException("Invalid read operation, ptr was not allocated");
#endif
            UnsafeUtility.MemCpy(dst.m_Buffer, src.m_Buffer, UnsafeUtility.SizeOf<T>());
        }
    }

    internal sealed class NativePtrDebugView<T> where T : unmanaged
    {
        private NativePtr<T> m_Ptr;

        public NativePtrDebugView(NativePtr<T> ptr)
        {
            m_Ptr = ptr;
        }

        public T Item => m_Ptr.Value;
    }
}