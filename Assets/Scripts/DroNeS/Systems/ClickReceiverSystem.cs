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
    public struct ClickEvent
    {
        public Entity Entity;
        public RaycastHit Hit;
    }
    [UpdateAfter(typeof(BuildPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
    public class ClickReceiverSystem : JobComponentSystem
    {
        private BuildPhysicsWorld _buildPhysicsWorldSystem;
        private EndFramePhysicsSystem _endFramePhysicsSystem;
        private EventSystem _eventSystem;
        private Camera _camera;

        protected override void OnCreate()
        {
            base.OnCreate();
            _eventSystem = World.Active.GetOrCreateSystem<EventSystem>();
            _eventSystem.NewEvent<ClickEvent>(1);
        }

        protected override void OnStartRunning()
        {
            _buildPhysicsWorldSystem = World.Active.GetExistingSystem<BuildPhysicsWorld>();
            _endFramePhysicsSystem = World.Active.GetOrCreateSystem<EndFramePhysicsSystem>();
            _camera = Camera.main;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!Input.GetMouseButtonDown(0)) return inputDeps;
            
            inputDeps = JobHandle.CombineDependencies(inputDeps, _buildPhysicsWorldSystem.FinalJobHandle);
            var screenRay = _camera.ScreenPointToRay(Input.mousePosition);
            var handle = new RayCastJob
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
                EventStream = _eventSystem.GetWriter<ClickEvent>(),
                World = _buildPhysicsWorldSystem.PhysicsWorld

            }.Schedule(inputDeps);
            
            _eventSystem.AddProducerJobHandle<ClickEvent>(handle);
            _endFramePhysicsSystem.HandlesToWaitFor.Add(handle);

            return handle;

        }

        [BurstCompile]
        private struct RayCastJob : IJob
        {
            [ReadOnly] public PhysicsWorld World;
            [ReadOnly] public RaycastInput Input;
            public NativeStream.Writer EventStream;

            public void Execute()
            {
                if (!World.CollisionWorld.CastRay(Input, out var hit)) return;
                var entity = World.Bodies[hit.RigidBodyIndex].Entity;
                EventStream.BeginForEachIndex(0);
                EventStream.Write(new ClickEvent
                {
                    Entity = entity,
                    Hit = hit,
                });
                EventStream.EndForEachIndex();
            }
        }
    }
}
