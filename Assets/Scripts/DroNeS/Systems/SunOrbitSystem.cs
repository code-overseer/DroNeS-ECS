using System.Collections.Generic;
using System.Diagnostics;
using DroNeS.MonoBehaviours;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;
using Debug = UnityEngine.Debug;

namespace DroNeS.Systems
{
//    [UpdateInGroup(typeof(FixedUpdateGroup))]
    public class SunOrbitSystem : JobComponentSystem
    {
        private readonly Dictionary<Speed, float> _timeSpeed = new Dictionary<Speed, float>
        {
            {Speed.Pause, 0}, 
            {Speed.Half, 0.5f},
            {Speed.Normal, 1},
            {Speed.Fast, 2},
            {Speed.Faster, 4},
            {Speed.Ultra, 8},
            {Speed.Wtf, 16}
        };

        private readonly Stopwatch _watch = new Stopwatch();
        public float SpeedFactor { get; private set; }
        public double Clock { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            Clock = 0;
            SpeedFactor = 1;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _watch.Start();
        }

        protected override JobHandle OnUpdate(JobHandle input)
        {
            var job = new SunMovementJob
            {
                Delta = _watch.ElapsedMilliseconds * 0.001f * SpeedFactor,
            };
            _watch.Restart();
            Clock += job.Delta;
            return job.Schedule(this, input);
        }

        [BurstCompile]
        private struct SunMovementJob : IJobForEach<Translation, Rotation, LightComponent>
        {
            public float Delta;
            private const float RadPerSec = 2 * math.PI / (24 * 3600); 
            public void Execute(ref Translation pos, ref Rotation rotation, [ReadOnly] ref LightComponent light)
            {
                var dTheta = Delta * RadPerSec;
                var q = quaternion.AxisAngle(new float3(0, 0, 1), dTheta);
                pos = new Translation {Value = math.rotate(q, pos.Value)};
                rotation = new Rotation { Value = math.mul(q, rotation.Value)};
            }
        }

        public void ChangeTimeSpeed(in Speed speed)
        {
            SpeedFactor = _timeSpeed[speed];
        }

    }
    
}
