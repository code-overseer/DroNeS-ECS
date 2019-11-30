using System;
using System.Collections;
using DroNeS.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public class TestScript : MonoBehaviour
    {
        private unsafe void Start()
        {
            var integer = new NativePtr<int>(Allocator.TempJob);
            var handle = new TestJob2 {Ptr = integer, Structs = null}.Schedule();
            handle.Complete();
            Debug.Log(integer.Value);
            integer.Dispose();

        }
    }

    public unsafe struct TestJob2 : IJob
    {
        [NativeDisableUnsafePtrRestriction] 
        public int* Structs;

        public NativePtr<int> Ptr;
        public void Execute()
        {
            Structs = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), Allocator.Temp);
            *Structs = 5;
            Ptr.Value = *Structs * 8;
            UnsafeUtility.Free(Structs, Allocator.Temp);
        }
    }
    
    [BurstCompile]
    public unsafe struct TestJob : IJob{
        public void Execute()
        {
            var points = new NativeList<float3>(12, Allocator.Temp)
            {
                float3.zero, 
                new float3(1, 1, 1),
                new float3(1, 2, 3),
            };
            
            points.Add(new float3(12,12,12));
            var zeroth = (float3*) points.GetUnsafePtr();
            var fourth = (float3*) ((IntPtr) points.GetUnsafePtr() + (int) 3 * sizeof(float3));

            var tmp = *zeroth;
            *zeroth = *fourth;
            *fourth = tmp;

        }

        private static void Add(float3 point, ref NativeList<float3> points)
        {
            points.Add(point);
        }
    }

}
