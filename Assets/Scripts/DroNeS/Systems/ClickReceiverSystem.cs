using BovineLabs.Entities.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace DroNeS.Systems
{
    public struct ClickEvent : IComponentData
    {
        public Entity Entity;
        public RaycastHit Hit;
    }
    [UpdateAfter(typeof(BuildPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem)), UpdateBefore(typeof(EntityEventSystem))]
    public class ClickReceiverSystem : JobComponentSystem
    {
        private BuildPhysicsWorld _buildPhysicsWorldSystem;
        private EndFramePhysicsSystem _endFramePhysicsSystem;
        private EntityEventSystem _eventSystem;

    protected override void OnStartRunning()
    {
        _buildPhysicsWorldSystem = World.GetExistingSystem<BuildPhysicsWorld>();
        _endFramePhysicsSystem = World.GetOrCreateSystem<EndFramePhysicsSystem>();
        _eventSystem = World.GetExistingSystem<EntityEventSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (Camera.main == null || !Input.GetMouseButtonDown(0)) return inputDeps;
        inputDeps = JobHandle.CombineDependencies(inputDeps, _buildPhysicsWorldSystem.FinalJobHandle);

        var screenRay = Camera.main.ScreenPointToRay(Input.mousePosition);

        var handle = new FindClosest
        {
            Input = new RaycastInput
            {
                Start = screenRay.origin,
                End = screenRay.GetPoint(2000),
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                }
            },
            EventQueue = _eventSystem.CreateEventQueue<ClickEvent>().AsParallelWriter(),
            World = _buildPhysicsWorldSystem.PhysicsWorld

        }.Schedule(inputDeps);
        
        World.GetExistingSystem<EntityEventSystem>().AddJobHandleForProducer(handle);
        _endFramePhysicsSystem.HandlesToWaitFor.Add(handle);

        return handle;

    }

    [BurstCompile]
    private struct FindClosest : IJob
    {
        [ReadOnly] public PhysicsWorld World;
        [ReadOnly] public RaycastInput Input;

        public NativeQueue<ClickEvent>.ParallelWriter EventQueue;

        public void Execute()
        {
            if (!World.CollisionWorld.CastRay(Input, out var hit)) return;
            var entity = World.Bodies[hit.RigidBodyIndex].Entity;
            EventQueue.Enqueue(new ClickEvent
            {
                Entity = entity,
                Hit = hit,
            });
        }
    }
    }
}
