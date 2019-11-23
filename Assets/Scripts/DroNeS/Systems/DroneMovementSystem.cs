using DroNeS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Systems
{
    [UpdateAfter(typeof(SunOrbitSystem))]
    public class DroneMovementSystem : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return inputDeps;
            var job = new DroneMovementJob
            {
                Delta = Time.deltaTime * World.Active.GetOrCreateSystem<SunOrbitSystem>().SpeedFactor
            };
            return job.Schedule(this, inputDeps);
        }
        
        [BurstCompile]
        private struct DroneMovementJob : IJobForEach<DroneTag, DroneStatus, Translation, Waypoint>
        {
            public float Delta;
            private const float Speed = 2;
            public void Execute([ReadOnly] ref DroneTag tag, ref DroneStatus status, ref Translation pos, ref Waypoint point)
            {
                switch (status.Value)
                {
                    case Status.New:
                        status = Status.RequestingWaypoints;
                        return;
                    case Status.Ready:
                        status = Status.Waiting;
                        return;
                    case Status.RequestingWaypoints:
                        return;
                }

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
