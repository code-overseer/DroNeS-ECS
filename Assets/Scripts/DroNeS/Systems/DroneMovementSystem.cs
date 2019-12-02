using System.Diagnostics;
using DroNeS.Components;
using DroNeS.Components.Tags;
using DroNeS.Systems.FixedUpdates;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace DroNeS.Systems
{
    [DisableAutoCreation]
    public class DroneMovementSystem : JobComponentSystem
    {
        private SunOrbitSystem _time;
        private Stopwatch _watch;
        protected override void OnCreate()
        {
            base.OnCreate();
            _time = World.Active.GetOrCreateSystem<SunOrbitSystem>();
            _watch = new Stopwatch();
        }
        
        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _watch.Start();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new DroneMovementJob
            {
                Delta = _watch.ElapsedMilliseconds * 0.001f * _time.SpeedFactor
            };
            _watch.Restart();
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
                    status = point.index >= point.length - 1 ? Status.RequestingWaypoints : Status.Waiting;
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
