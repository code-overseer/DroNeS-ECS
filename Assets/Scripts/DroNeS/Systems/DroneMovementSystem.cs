using System.Collections.Generic;
using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Systems
{
    public class DroneMovementSystem : JobComponentSystem
    {

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new DroneMovementJob
            {
                Delta = Time.deltaTime
            };
            return job.Schedule(this, inputDeps);
        }
        
        private struct DroneMovementJob : IJobForEach<DroneStatus, Translation, Waypoint>
        {
            public float Delta;
            private const float Speed = 10;
            public void Execute(ref DroneStatus status, ref Translation pos, ref Waypoint point)
            {
                if (status.Value == Status.RequestingWaypoints) return;
                if (math.lengthsq(pos.Value - point.waypoint) < 1e-3f)
                {
                    status = (point.index >= point.length - 1) ? Status.RequestingWaypoints : Status.Waiting;
                    return;
                }
                status = Status.EnRoute;
                pos.Value = MoveTowards(pos.Value, point.waypoint, Speed * Delta);

            }

            private static float3 MoveTowards(float3 current, float3 target, float maxDelta)
            {
                var delta = target - current;
                var sq = math.lengthsq(delta);
                if (sq <= maxDelta * maxDelta) return target;
                var dist = math.sqrt(sq);
                return new float3(current + delta * maxDelta * math.rcp(dist));
            }
        }
    }
}
