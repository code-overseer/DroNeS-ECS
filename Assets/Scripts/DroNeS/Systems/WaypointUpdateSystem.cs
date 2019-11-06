using System.Collections.Generic;
using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Systems
{
    public class WaypointUpdateSystem : ComponentSystem
    {
        private readonly Dictionary<int, Queue<float3>> _wp = new Dictionary<int, Queue<float3>>();
        /* TODO
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
        }

        protected override void OnUpdate()
        {
            Entities.With(_droneQuery).ForEach((ref DroneUID id, ref DroneStatus status, ref Waypoint point) =>
            {
                if (status.Value != Status.New && status.Value != Status.Waiting) return;
                GenerateQueue(id.uid);
                point = _wp[id.uid].Dequeue();
                status.Value = Status.Waiting;
            });

        }
        
        private void GenerateQueue(int id)
        {

            if (!_wp.TryGetValue(id, out var queue))
            {
                queue = new Queue<float3>();
                _wp.Add(id, queue);
            }
            if (queue.Count > 0) return;
            for (var i = 0; i < 15; ++i)
            {
                queue.Enqueue(UnityEngine.Random.insideUnitSphere * 25);
            }
            
        }

    }
}
