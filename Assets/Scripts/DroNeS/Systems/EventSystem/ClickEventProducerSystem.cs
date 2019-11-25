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
    public struct ClickEvent
    {
        public Entity Entity;
    }
    [UpdateAfter(typeof(BuildPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
    public class ClickEventProducerSystem : JobComponentSystem
    {
        private BuildPhysicsWorld _buildPhysicsWorldSystem;
        private EndFramePhysicsSystem _endFramePhysicsSystem;
        private EndSimulationEntityCommandBufferSystem _barrier;
        private EventSystem _eventSystem;
        private Camera _camera;

        protected override void OnCreate()
        {
            base.OnCreate();
            _eventSystem = World.Active.GetOrCreateSystem<EventSystem>();
            _eventSystem.NewEvent<ClickEvent>(1);
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _buildPhysicsWorldSystem = World.Active.GetExistingSystem<BuildPhysicsWorld>();
            _endFramePhysicsSystem = World.Active.GetOrCreateSystem<EndFramePhysicsSystem>();
            _camera = Camera.main;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!Input.GetMouseButtonDown(0)) return inputDeps;
            
            inputDeps = JobHandle.CombineDependencies(inputDeps, _buildPhysicsWorldSystem.FinalJobHandle);
            var screenRay = _camera.ScreenPointToRay(Input.mousePosition);

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
                Clicked = _eventSystem.GetWriter<ClickEvent>()
            };
            var handle = job.Schedule(inputDeps);
            _endFramePhysicsSystem.HandlesToWaitFor.Add(handle);
            _eventSystem.AddProducerJobHandle<ClickEvent>(handle);
            var reader = _eventSystem.GetReader<ClickEvent>(ref handle);
            handle = new SelectJob
            {
                Clicked = reader,
                Buffer = _barrier.CreateCommandBuffer(),
            }.Schedule(handle);
            _eventSystem.AddConsumerJobHandle<ClickEvent>(handle);
            
            return handle;

        }
        
        [BurstCompile]
        private struct RayCastJob : IJob
        {
            [ReadOnly] public PhysicsWorld World;
            [ReadOnly] public RaycastInput Input;
            [WriteOnly] public NativeStream.Writer Clicked;

            public void Execute()
            {
                if (!World.CollisionWorld.CastRay(Input, out var hit)) return;
                Clicked.BeginForEachIndex(0);
                Clicked.Write(new ClickEvent {Entity = World.Bodies[hit.RigidBodyIndex].Entity});
                Clicked.EndForEachIndex();
            }
        }

        private struct SelectJob : IJob
        {
            [ReadOnly] public NativeStream.Reader Clicked;
            [WriteOnly] public EntityCommandBuffer Buffer;
            public void Execute()
            {
                if (Clicked.ComputeItemCount() < 1) return;
                Clicked.BeginForEachIndex(0);
                Buffer.AddComponent(Clicked.Read<ClickEvent>().Entity, new SelectionTag());
                Clicked.EndForEachIndex();
            }
        }
    }
}
