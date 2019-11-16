using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Systems
{
    public class SunOrbitSystem : JobComponentSystem
    {
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

        public static float SpeedFactor { get; private set; }
        public static double Clock { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
            Clock = 0;
            SpeedFactor = 1;
        }

        protected override JobHandle OnUpdate(JobHandle input)
        {
            var job = new SunMovementJob
            {
                Delta = Time.deltaTime * SpeedFactor,
            };
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

        public static void ChangeTimeSpeed(in Speed speed)
        {
            SpeedFactor = TimeSpeed[speed];
        }

    }
    
}
