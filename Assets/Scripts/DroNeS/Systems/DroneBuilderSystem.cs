using DroNeS.Components;
using DroNeS.SharedComponents;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;
using Collider = Unity.Physics.Collider;
using Material = UnityEngine.Material;
using Random = UnityEngine.Random;

namespace DroNeS.Systems
{
    public class DroneBuilderSystem : ComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem _barrier;
        private EntityArchetype _drone;
        private BlobAssetReference<Collider> _droneCollider;
        private EntityArchetype _propeller;
        private RenderMesh _droneMesh;
        private RenderMesh _propellerMesh;
        private static EntityManager Manager => World.Active.EntityManager;
        private int _droneUid;

        private float3[] _propellerPositions;

        protected override void OnCreate()
        {
            base.OnCreate();
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _droneUid = 0;
            _drone = Manager.CreateArchetype(
                ComponentType.ReadOnly<DroneTag>(),
                ComponentType.ReadOnly<DroneUID>(),
                typeof(Translation),
                typeof(DroneStatus),
                typeof(Waypoint),
                typeof(Rotation),
                typeof(LocalToWorld),
                typeof(PhysicsCollider)
            );
            _propeller = Manager.CreateArchetype(
                ComponentType.ReadOnly<Parent>(),
                ComponentType.ReadOnly<PropellerTag>(),
                typeof(Translation),
                typeof(Rotation),
                typeof(LocalToParent),
                typeof(LocalToWorld)
            );
            _droneMesh = new RenderMesh
            {
                mesh = Resources.Load("Meshes/Drone") as Mesh,
                material = Resources.Load("Materials/RedDrone") as Material
            };
            _propellerMesh = new RenderMesh
            {
                mesh = Resources.Load("Meshes/Propeller") as Mesh,
                material = Resources.Load("Materials/Propeller") as Material
            };
            
            _propellerPositions = new[] 
            {
                new float3(0.59f, 0.082f, 0.756f),
                new float3(-0.59f, 0.082f, 0.756f),
                new float3(0.6f, 0.082f, -0.7f),
                new float3(-0.59f, 0.082f, -0.7f)
            };
            var geometry = new BoxGeometry
            {
                Center = new float3(-0.001946479f, -0.0465135f, 0.03175002f),
                BevelRadius = 0.015f,
                Orientation = quaternion.identity,
                Size = new float3(1.421171f, 0.288217f, 1.492288f)
            };
            _droneCollider = BoxCollider.Create(geometry,
                new CollisionFilter
                {
                    BelongsTo = CollisionGroups.Drone,
                    CollidesWith = CollisionGroups.Buildings | CollisionGroups.Cast,
                    GroupIndex = 0
                });

        }

        public void AddDrone()
        {
            var buildCommands = _barrier.CreateCommandBuffer();
            for (var i = 0; i < 5; ++i)
            {
                var drone = buildCommands.CreateEntity(_drone);
                float3 pos = Random.insideUnitSphere * 5;
                buildCommands.SetComponent(drone, new Translation {Value = pos});
                buildCommands.SetComponent(drone, new Rotation{ Value = quaternion.identity });
                buildCommands.SetComponent(drone, new DroneUID {Value = ++_droneUid} );
                buildCommands.SetComponent(drone, new DroneStatus {Value = Status.New} );
                buildCommands.SetComponent(drone, new Waypoint(float3.zero, -1,0));
                buildCommands.SetComponent(drone, new PhysicsCollider { Value = _droneCollider });
                buildCommands.AddSharedComponent(drone, _droneMesh);
                for (var j = 0; j < 4; ++j)
                {
                    var prop = buildCommands.CreateEntity(_propeller);
                    buildCommands.SetComponent(prop, new Parent {Value = drone});
                    buildCommands.SetComponent(prop, new Translation { Value = _propellerPositions[j]});
                    buildCommands.SetComponent(prop, new Rotation{ Value = quaternion.identity });
                    buildCommands.AddSharedComponent(prop, _propellerMesh);
                }
            }
        }

        protected override void OnUpdate()
        {
        }
    }
}
