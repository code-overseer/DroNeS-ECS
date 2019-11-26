using System.Collections.Generic;
using System.Diagnostics;
using DroNeS.Components.Singletons;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace DroNeS.Systems.FixedUpdates
{
    [UpdateInGroup(typeof(FixedUpdateGroup))]
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

        protected override void OnCreate()
        {
            base.OnCreate();
            SpeedFactor = 1;
            EntityManager.CreateEntity(typeof(Clock));
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _watch.Start();
            SetSingleton(new Clock{ Value = 0});
        }

        protected override JobHandle OnUpdate(JobHandle input)
        {
            var job = new SunMovementJob
            {
                Delta = _watch.ElapsedMilliseconds * 0.001f * SpeedFactor,
            };
            _watch.Restart();
            return new UpdateClockJob{ Delta = job.Delta }.Schedule(this, job.Schedule(this, input));
        }

        [BurstCompile]
        private struct SunMovementJob : IJobForEach<Translation, Rotation, LightComponent>
        {
            public float Delta;
            private const float RadPerSec = 2 * math.PI * 0.00001157407407f; 
            public void Execute(ref Translation pos, ref Rotation rotation, [ReadOnly] ref LightComponent light)
            {
                var dTheta = Delta * RadPerSec;
                var q = quaternion.AxisAngle(new float3(0, 0, 1), dTheta);
                pos = new Translation {Value = math.rotate(q, pos.Value)};
                rotation = new Rotation { Value = math.mul(q, rotation.Value)};
            }
        }
        
        [BurstCompile]
        private struct UpdateClockJob : IJobForEach<Clock>
        {
            public float Delta;
            public void Execute(ref Clock clock)
            {
                clock.Value += Delta;
            }
        }

        public void ChangeTimeSpeed(in Speed speed)
        {
            SpeedFactor = _timeSpeed[speed];
        }

    }
    
}
