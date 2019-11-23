using DroNeS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace DroNeS.Systems
{
    [UpdateAfter(typeof(DroneMovementSystem))]
    public class WaypointUpdateSystem : JobComponentSystem
    {
        private EntityCommandBuffer _commandBuffer;
        private NativeQueue<int> _queuesToClear;
        private EntityQuery _droneQuery;

        protected override void OnCreate()
        {
            _droneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<DroneTag>(),
                    ComponentType.ReadOnly<DroneUID>(),
                    typeof(Waypoint),
                    typeof(DroneStatus)
                }
            });
            _droneQuery.SetFilterChanged(typeof(DroneStatus));
            Queue.Waypoints = new NativeMultiHashMap<int, Waypoint>(400, Allocator.Persistent);
            _queuesToClear = new NativeQueue<int>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Queue.Waypoints.Dispose();
            _queuesToClear.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var t = Time.deltaTime;
            uint s;
            unsafe
            {
                s = *(uint*) &t;
            }
            var job0 = new WaypointUpdateJob
            {
                AllQueues = Queue.Waypoints,
                DroneId = GetArchetypeChunkComponentType<DroneUID>(),
                Statuses = GetArchetypeChunkComponentType<DroneStatus>(),
                CurrentPoint = GetArchetypeChunkComponentType<Waypoint>()
            };
            var job1 = new QueueCompletionCheck
            {
                Completed = _queuesToClear.AsParallelWriter(),
                DroneId = GetArchetypeChunkComponentType<DroneUID>(),
                Statuses = GetArchetypeChunkComponentType<DroneStatus>(),
                CurrentPoint = GetArchetypeChunkComponentType<Waypoint>()
            };
            var job2 = new QueueRemovalJob
            {
                AllQueues = Queue.Waypoints,
                Completed = _queuesToClear
            };
            var job3 = new QueueGenerationJob
            {
                AllQueues = Queue.Waypoints.AsParallelWriter(),
                DroneId = GetArchetypeChunkComponentType<DroneUID>(),
                Statuses = GetArchetypeChunkComponentType<DroneStatus>(),
                random = new Random(s)
            };
            
            var handle = job0.Schedule(_droneQuery, inputDeps);
            handle = job1.Schedule(_droneQuery, handle);
            handle = job2.Schedule(handle);

            return job3.Schedule(_droneQuery, handle);
        }

        [BurstCompile]
        private struct QueueCompletionCheck : IJobChunk
        {
            public NativeQueue<int>.ParallelWriter Completed;
            [ReadOnly] public ArchetypeChunkComponentType<DroneUID> DroneId;
            [ReadOnly] public ArchetypeChunkComponentType<DroneStatus> Statuses;
            public ArchetypeChunkComponentType<Waypoint> CurrentPoint;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var droneIds = chunk.GetNativeArray(DroneId);
                var stats = chunk.GetNativeArray(Statuses);
                var points = chunk.GetNativeArray(CurrentPoint);
                for (var i = 0; i < chunk.Count; ++i)
                {
                    if (stats[i].Value != Status.RequestingWaypoints) continue;
                    var p = points[i];
                    p.index = -1;
                    points[i] = p;
                    Completed.Enqueue(droneIds[i].Value);
                }
                
            }
        }
        
        [BurstCompile]
        private struct QueueRemovalJob : IJob
        {
            public NativeMultiHashMap<int, Waypoint> AllQueues;
            public NativeQueue<int> Completed;

            public void Execute()
            {
                var space = Completed.Count * 20; // 20 is approximate queue length
                while (Completed.Count > 0)
                {
                    var idx = Completed.Dequeue();
                    if (AllQueues.TryGetFirstValue(idx, out _, out _)) AllQueues.Remove(idx);
                }

                while (AllQueues.Capacity - AllQueues.Length < space) AllQueues.Capacity *= 2;
            }
        }

        [BurstCompile]
        private struct QueueGenerationJob : IJobChunk
        {
            
            public NativeMultiHashMap<int, Waypoint>.ParallelWriter AllQueues;
            public ArchetypeChunkComponentType<DroneStatus> Statuses;
            [ReadOnly] public ArchetypeChunkComponentType<DroneUID> DroneId;
            public Random random;
            
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var droneIds = chunk.GetNativeArray(DroneId);
                var stats = chunk.GetNativeArray(Statuses);
                for (var i = 0; i < chunk.Count; ++i)
                {
                    if (stats[i].Value != Status.RequestingWaypoints) continue;
                    for (var j = 0; j < 15; ++j)
                    {
                        var p = new float3(random.NextFloat(), random.NextFloat(), random.NextFloat()) * 25;
                        AllQueues.Add(droneIds[i].Value, new Waypoint(p, j, 15));
                    }
                    stats[i] = new DroneStatus(Status.Ready);
                }
            }
        }


        [BurstCompile]
        private struct WaypointUpdateJob : IJobChunk
        {
            [ReadOnly] public NativeMultiHashMap<int, Waypoint> AllQueues;
            [ReadOnly] public ArchetypeChunkComponentType<DroneUID> DroneId;
            [ReadOnly] public ArchetypeChunkComponentType<DroneStatus> Statuses;
            public ArchetypeChunkComponentType<Waypoint> CurrentPoint;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var droneIds = chunk.GetNativeArray(DroneId);
                var stats = chunk.GetNativeArray(Statuses);
                var points = chunk.GetNativeArray(CurrentPoint);
                for (var i = 0; i < chunk.Count; ++i)
                {
                    if (stats[i].Value != Status.Waiting) continue;
                    AllQueues.TryGetFirstValue(droneIds[i].Value, out var p, out var it);
                    do
                    {
                        if (p.index != points[i].index + 1) continue;
                        points[i] = p;
                        break;
                    }
                    while (AllQueues.TryGetNextValue(out p, ref it));
                    
                }
            }
        }
    }
}
