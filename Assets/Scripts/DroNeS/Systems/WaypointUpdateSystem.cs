using System.Collections.Generic;
using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace DroNeS.Systems
{
    public class WaypointUpdateSystem : JobComponentSystem
    {
       // private readonly Dictionary<int, Queue<float3>> _wp = new Dictionary<int, Queue<float3>>();
        private static EntityCommandBuffer _commandBuffer;
        private static NativeMultiHashMap<int, Waypoint> _queues;
        private static NativeQueue<int> _queueBuffer;

        /*
         * TODO
         * Change to NativeMultiHashMap
         * Change waypoint to contain queue index and queue length
         * Scan through using IJobChunk for 'Requesting' drones to generate new 'queue'
        */
        private EntityQuery _droneQuery;

        protected override void OnCreate()
        {
            _droneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    typeof(Waypoint),
                    typeof(DroneStatus),
                    ComponentType.ReadOnly<DroneTag>()
                }
            });
            _droneQuery.SetFilterChanged(typeof(DroneStatus));
            _queues = new NativeMultiHashMap<int, Waypoint>(400, Allocator.Persistent);
            _queueBuffer = new NativeQueue<int>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _queues.Dispose();
            _queueBuffer.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job0 = new QueueCompletionCheck
            {
                Completed = _queueBuffer.AsParallelWriter()
            };
            var job1 = new QueueRemovalJob
            {
                AllQueues = _queues,
                Completed = _queueBuffer
            };
            var job2 = new QueueGenerationJob
            {
                AllQueues = _queues.AsParallelWriter(),
            };
            var job3 = new WaypointUpdateJob
            {
                AllQueues = _queues
            };
            var handle = job0.Schedule(this, inputDeps);
            handle = job1.Schedule(handle);
            handle = job2.Schedule(this, handle);

            return job3.Schedule(this, handle);
        }
        
        private struct QueueCompletionCheck : IJobForEach<DroneTag, DroneUID, DroneStatus, Waypoint>
        {
            public NativeQueue<int>.ParallelWriter Completed;
            public void Execute([ReadOnly] ref DroneTag tag, [ReadOnly] ref DroneUID id, [ReadOnly] ref DroneStatus stats, ref Waypoint point)
            {
                if (stats.Value != Status.RequestingWaypoints) return;
                Completed.Enqueue(id.uid);
                point.index = -1;
                point.length = 0;
            }
        }
        
        private struct QueueRemovalJob : IJob
        {
            public NativeMultiHashMap<int, Waypoint> AllQueues;
            public NativeQueue<int> Completed;

            public void Execute()
            {
                while (Completed.Count > 0)
                {
                    var idx = Completed.Dequeue();
                    if (AllQueues.TryGetFirstValue(idx, out _, out _)) AllQueues.Remove(idx);
                }
            }
        }
        
        private struct QueueGenerationJob : IJobForEach<DroneTag, DroneUID, DroneStatus, Waypoint>
        {
            public NativeMultiHashMap<int, Waypoint>.ParallelWriter AllQueues;
            public void Execute([ReadOnly] ref DroneTag tag, [ReadOnly] ref DroneUID id, ref DroneStatus stats, ref Waypoint point)
            {
                if (stats.Value != Status.RequestingWaypoints) return;
                var rand = new Random((uint)id.uid | 1);
                for (var i = 0; i < 15 && point.length == 0; ++i)
                {
                    var p = new float3(rand.NextFloat(), rand.NextFloat(), rand.NextFloat()) * 25 - 12.5f;
                    AllQueues.Add(id.uid, new Waypoint(p, i, 15));
                }
                stats.Value = Status.Waiting;
            }
        }

        private struct WaypointUpdateJob : IJobForEach<DroneTag, DroneUID, DroneStatus, Waypoint>
        {
            [ReadOnly] public NativeMultiHashMap<int, Waypoint> AllQueues;
            public void Execute([ReadOnly] ref DroneTag tag, ref DroneUID id, ref DroneStatus stats, ref Waypoint point)
            {
                if (stats.Value != Status.Waiting) return;
                AllQueues.TryGetFirstValue(id.uid, out var p, out var it);
                do
                {
                    if (p.index != point.index + 1) continue;
                    point = p;
                    return;
                }
                while (AllQueues.TryGetNextValue(out p, ref it));
            }
        }
    }
}
