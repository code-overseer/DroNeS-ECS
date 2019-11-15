using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Jobs;

namespace DroNeS.Systems
{
    public class SunOrbitSystem : JobComponentSystem
    {
        private static EntityQuery _sunQuery;
        private static Stopwatch _watch;
        private static readonly Dictionary<Speed, float> TimeSpeed = new Dictionary<Speed, float>
        {
            {Speed.Pause, 0}, 
            {Speed.Half, 0.5f},
            {Speed.Normal, 1},
            {Speed.Fast, 2},
            {Speed.Faster, 4},
            {Speed.Ultra, 8},
            {Speed.Wtf, 16}
        };

        private const float MilliSecToSec = 0.001f;
        private const float DegPerSec = 360.0f / (24 * 3600);

        protected override void OnCreate()
        {
            base.OnCreate();
            _sunQuery = GetEntityQuery(typeof(Transform), typeof(LightComponent));
            _watch = new Stopwatch();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            EntityManager.AddComponent(_sunQuery, typeof(TimeSpeed));
            var e = _sunQuery.ToEntityArray(Allocator.TempJob);
            EntityManager.SetComponentData(e[0], new TimeSpeed{Value = 1});
            e.Dispose();
            _sunQuery = GetEntityQuery(typeof(Transform), typeof(TimeSpeed));
            var t = _sunQuery.GetTransformAccessArray();
            t[0].position = Vector3.up * 200;
            t[0].eulerAngles = new Vector3(90, -90, -90);
            t[0].RotateAround(Vector3.zero, new Vector3(0, 0, 1), 180);
            t[0].RotateAround(Vector3.zero, new Vector3(0, 0, 1), 135);
            _watch.Start();
        }

        protected override JobHandle OnUpdate(JobHandle input)
        {
            var job = new SunMoverJob
            {
                Delta = _watch.ElapsedMilliseconds * MilliSecToSec,
                Speeds = _sunQuery.ToComponentDataArray<TimeSpeed>(Allocator.TempJob)
            };
            _watch.Restart();
            return job.Schedule(_sunQuery.GetTransformAccessArray());
        }

        private struct SunMoverJob : IJobParallelForTransform
        {
            public float Delta;
            [DeallocateOnJobCompletion]
            [Unity.Collections.ReadOnly] 
            public NativeArray<TimeSpeed> Speeds;
            public void Execute(int index, TransformAccess transform)
            {
                var dTheta = Delta * Speeds[index].Value * DegPerSec;
                var q = Quaternion.AngleAxis(dTheta, Vector3.forward);
                transform.position = q * transform.position;
                transform.rotation = q * transform.rotation;
            }
        }

        public static void ChangeTimeSpeed(in Speed speed)
        {
            var barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var e = _sunQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in e)
            {
                barrier.PostUpdateCommands.SetComponent(entity, new TimeSpeed{Value = TimeSpeed[speed]});
            }
            e.Dispose();
        }

    }
    
}
