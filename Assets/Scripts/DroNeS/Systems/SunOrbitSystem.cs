using System.Collections.Generic;
using System.Diagnostics;
using DroNeS.Components;
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
        private static EntityQuery _sunQuery;
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
        
        protected override void OnCreate()
        {
            base.OnCreate();
            _sunQuery = GetEntityQuery(typeof(LightComponent));
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            EntityManager.AddComponent(_sunQuery, typeof(TimeSpeed));
            var e = _sunQuery.ToEntityArray(Allocator.TempJob);
            EntityManager.SetComponentData(e[0], new TimeSpeed{Value = 1});
            e.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle input)
        {
            var job = new SunMovementJob
            {
                Delta = Time.deltaTime
            };
            return job.Schedule(this, input);
        }

        [BurstCompile]
        private struct SunMovementJob : IJobForEach<Translation, Rotation, TimeSpeed>
        {
            public float Delta;
            private const float RadPerSec = 2 * math.PI / (24 * 3600); 
            public void Execute(ref Translation pos, ref Rotation rotation, [ReadOnly] ref TimeSpeed speed)
            {
                var dTheta = Delta * speed.Value * RadPerSec;
                var q = quaternion.AxisAngle(new float3(0, 0, 1), dTheta);
                pos = new Translation {Value = math.rotate(q, pos.Value)};
                rotation = new Rotation { Value = math.mul(q, rotation.Value)};
            }
        }

        public static void ChangeTimeSpeed(in Speed speed)
        {
            var barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var commands = barrier.CreateCommandBuffer();
            var e = _sunQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in e)
            {
                commands.SetComponent(entity, new TimeSpeed{Value = TimeSpeed[speed]});
            }
            e.Dispose();
        }

    }
    
}
