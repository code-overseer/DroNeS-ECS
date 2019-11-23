using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;

namespace Utils
{
    public static unsafe class NativeStreamExtensions
    {
        private unsafe struct NativeStreamHeader
        {
            public NativeStream.BlockStreamData* Block;
            public Allocator AllocatorLabel;
        }

        public static void Clear(this NativeStream stream)
        {
            if (!stream.IsCreated)
                return;

            var streamHeader = (NativeStreamHeader*)UnsafeUtility.AddressOf(ref stream);
            if ((IntPtr)streamHeader->Block == IntPtr.Zero)
                return;

            const int blockCount = JobsUtility.MaxJobThreadCount;
            var blocksSize = sizeof(NativeStream.Block*) * blockCount;

            long forEachAllocationSize = sizeof(NativeStream.Range) * stream.ForEachCount;
            UnsafeUtility.MemClear(streamHeader->Block->Ranges, forEachAllocationSize);

            for (var index = 0; index != streamHeader->Block->BlockCount; ++index)
            {
                NativeStream.Block* next;
                for (var blockPtr = streamHeader->Block->Blocks[index]; (IntPtr)blockPtr != IntPtr.Zero; blockPtr = next)
                {
                    next = blockPtr->Next;
                    UnsafeUtility.MemClear(blockPtr, blocksSize);
                }
            }
        }
    }
}
