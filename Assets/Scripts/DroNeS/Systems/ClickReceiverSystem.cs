using DroNeS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Utils;
using Clickable = DroNeS.SharedComponents.Clickable;

namespace DroNeS.Systems
{
    public class UserInputBarrier : EntityCommandBufferSystem
    {
        
    }
    [UpdateBefore(typeof(UserInputBarrier))]
    public class ClickReceiverSystem : JobComponentSystem
    {
        private EntityQuery _clickableEntities;
        private Camera _camera;
        private UserInputBarrier _barrier;
        private NativeQueue<Entity> _intersected;
        protected override void OnCreate()
        {
            base.OnCreate();
            _clickableEntities = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<WorldRenderBounds>(),
                    ComponentType.ReadOnly<Clickable>(), 
                }
            });
            _intersected = new NativeQueue<Entity>(Allocator.Persistent);
            _barrier = World.Active.GetOrCreateSystem<UserInputBarrier>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _camera = GameObject.Find("Main Camera").GetComponent<Camera>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _intersected.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            _intersected.Clear();
            var intersectionJob = new FindClickedEntityJob
            {
                Bounds = GetArchetypeChunkComponentType<WorldRenderBounds>(),
                Entities = GetArchetypeChunkEntityType(),
                Intersected = _intersected.AsParallelWriter(),
                CursorRay = _camera.ScreenPointToRay(Input.mousePosition),
                Clicked = Input.GetMouseButtonDown(0)
            };
            var findNearestJob = new FindClosestEntityJob
            {
                Intersected = _intersected,
                Position = GetComponentDataFromEntity<Translation>(true),
                Origin = intersectionJob.CursorRay.Origin,
                Buffer = _barrier.CreateCommandBuffer().ToConcurrent(),
            };
            inputDeps = findNearestJob.Schedule(intersectionJob.Schedule(_clickableEntities, inputDeps));
            _barrier.AddJobHandleForProducer(inputDeps);
            return inputDeps;
        }
        
        [BurstCompile]
        private struct FindClickedEntityJob : IJobChunk
        {
            [ReadOnly] public ArchetypeChunkComponentType<WorldRenderBounds> Bounds;
            [ReadOnly] public ArchetypeChunkEntityType Entities;
            [WriteOnly] public NativeQueue<Entity>.ParallelWriter Intersected;
            public Utils.Ray CursorRay;
            public Bool Clicked;
            
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                if (!Clicked) return;
                var bounds = chunk.GetNativeArray(Bounds);
                var entities = chunk.GetNativeArray(Entities);
                var maxVal = 5000.0f;
                var clicked = new NativeList<Entity>(1, Allocator.Temp);
                for (var i = 0; i < chunk.Count; ++i)
                {
                    var w = bounds[i].Value.Center - CursorRay.Origin;
                    var dist = math.dot(w, CursorRay.Direction);
                    if (dist < 0 || dist > maxVal || !bounds[i].Value.Contains(CursorRay.GetPoint(dist))) continue;
                    maxVal = dist;
                    if (clicked.Length > 0) clicked[0] = entities[i];
                    else clicked.Add(entities[i]);
                }
                if (clicked.Length > 0) Intersected.Enqueue(clicked[0]);
            }
        }
        
        private struct FindClosestEntityJob : IJob
        {
            public NativeQueue<Entity> Intersected;
            [ReadOnly] 
            public ComponentDataFromEntity<Translation> Position;
            [WriteOnly]
            public EntityCommandBuffer.Concurrent Buffer;
            public float3 Origin;

            public void Execute()
            {
                if (Intersected.Count == 0) return;
                var minVal = float.MaxValue;
                var clicked = new NativeList<Entity>(1, Allocator.Temp);
                while (Intersected.TryDequeue(out var entity))
                {
                    if (!Position.Exists(entity)) continue;
                    var d = math.distancesq(Position[entity].Value, Origin);
                    if (d > minVal) continue;
                    minVal = d;
                    if (clicked.Length > 0) clicked[0] = entity;
                    else clicked.Add(entity);
                }
                if (clicked.Length > 0) Buffer.AddComponent(0, clicked[0], new Clicked());
            }
        }
    }
}
