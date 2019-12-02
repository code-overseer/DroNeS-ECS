using DroNeS.Components.Tags;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

namespace DroNeS.Systems.EventSystem
{
    [DisableAutoCreation]
    [UpdateAfter(typeof(BuildPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
    public class OnClickSystem : JobComponentSystem
    {
        private BuildPhysicsWorld _buildPhysicsWorldSystem;
        private EndFramePhysicsSystem _endFramePhysicsSystem;
        private OnClickEntityCommandBufferSystem _barrier;

        protected override void OnCreate()
        {
            base.OnCreate();
            _barrier = World.Active.GetOrCreateSystem<OnClickEntityCommandBufferSystem>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _buildPhysicsWorldSystem = World.Active.GetExistingSystem<BuildPhysicsWorld>();
            _endFramePhysicsSystem = World.Active.GetOrCreateSystem<EndFramePhysicsSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!Input.GetMouseButtonDown(0) || Camera.main == null) return inputDeps;
            
            inputDeps = JobHandle.CombineDependencies(inputDeps, _buildPhysicsWorldSystem.FinalJobHandle);
            var screenRay = Camera.main.ScreenPointToRay(Input.mousePosition);

            var job = new RayCastJob
            {
                Input = new RaycastInput
                {
                    Start = screenRay.origin,
                    End = screenRay.GetPoint(2000),
                    Filter = new CollisionFilter
                    {
                        BelongsTo = CollisionGroups.Cast,
                        CollidesWith = CollisionGroups.Drone | CollisionGroups.Hub,
                        GroupIndex = 0
                    }
                },
                World = _buildPhysicsWorldSystem.PhysicsWorld,
                Clicked = new NativeArray<Entity>(1, Allocator.TempJob)
            };
            var handle = job.Schedule(inputDeps);
            _endFramePhysicsSystem.HandlesToWaitFor.Add(handle);
            
            handle = new PreSelectJob
            {
                Clicked = job.Clicked,
                Buffer = _barrier.CreateCommandBuffer(),
            }.Schedule(handle);
            
            _barrier.AddJobHandleForProducer(handle);

            return handle;

        }
        
        [BurstCompile]
        private struct RayCastJob : IJob
        {
            [ReadOnly] public PhysicsWorld World;
            [ReadOnly] public RaycastInput Input;
            [WriteOnly] public NativeArray<Entity> Clicked;

            public void Execute()
            {
                if (!World.CollisionWorld.CastRay(Input, out var hit))
                {
                    Clicked[0] = Entity.Null;
                    return;
                }
                Clicked[0] = World.Bodies[hit.RigidBodyIndex].Entity;
            }
        }

        private struct PreSelectJob : IJob
        {
            [DeallocateOnJobCompletion, ReadOnly] public NativeArray<Entity> Clicked;
            [WriteOnly] public EntityCommandBuffer Buffer;
            public void Execute()
            {
                if (Clicked[0] == Entity.Null) return;
                Buffer.AddComponent(Clicked[0], new PreSelectionTag());
            }
        }
    }
}
