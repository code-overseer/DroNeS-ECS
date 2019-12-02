using DroNeS.Components;
using DroNeS.Components.Tags;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using BoxCollider = Unity.Physics.BoxCollider;
using Collider = Unity.Physics.Collider;
using Random = UnityEngine.Random;

namespace DroNeS.Systems.EventSystem
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class DroneBuilderSystem : ComponentSystem
    {
        private EntityArchetype _drone;
        private BlobAssetReference<Collider> _droneCollider;
        private EntityArchetype _propeller;
        private RenderMesh _propellerMesh;
        private RenderMesh _droneMesh;
        private int _droneUid;
        private int _buildQueue;

        private float3[] _propellerPositions;

        protected override void OnCreate()
        {
            base.OnCreate();
            _droneUid = 0;
            _drone = EntityManager.CreateArchetype(
                ComponentType.ReadOnly<DroneTag>(),
                ComponentType.ReadOnly<DroneUID>(),
                typeof(Translation),
                typeof(DroneStatus),
                typeof(Waypoint),
                typeof(Rotation),
                typeof(LocalToWorld),
                typeof(PhysicsCollider)
            );
            _propeller = EntityManager.CreateArchetype(
                ComponentType.ReadOnly<Parent>(),
                ComponentType.ReadOnly<PropellerTag>(),
                typeof(Translation),
                typeof(Rotation),
                typeof(LocalToParent),
                typeof(LocalToWorld)
            );
            
            _droneMesh = EntityData.Drone.ToRenderMesh();
            _propellerMesh = EntityData.Drone.ToPropellerMesh();
            _propellerPositions = EntityData.Drone.PropellerPositions;
            _droneCollider = BoxCollider.Create(EntityData.Drone.BoxGeometry,
                new CollisionFilter
                {
                    BelongsTo = CollisionGroups.Drone,
                    CollidesWith = CollisionGroups.Buildings | CollisionGroups.Cast,
                    GroupIndex = 0
                });

        }

        public void AddDrone()
        {
            _buildQueue += 5;
        }

        protected override void OnUpdate()
        {
            if (_buildQueue < 1) return;
            
            var drones = new NativeArray<Entity>(_buildQueue, Allocator.TempJob);
            var propellers = new NativeArray<Entity>(_buildQueue * 4, Allocator.TempJob);
            _buildQueue = 0;
            EntityManager.CreateEntity(_drone, drones);
            EntityManager.CreateEntity(_propeller, propellers);
            for (var i = 0; i < drones.Length; ++i)
            {
                EntityManager.SetComponentData(drones[i], new Translation {Value = Random.insideUnitSphere * 5});
                EntityManager.SetComponentData(drones[i], new Rotation{ Value = quaternion.identity });
                EntityManager.SetComponentData(drones[i], new DroneUID {Value = ++_droneUid} );
                EntityManager.SetComponentData(drones[i], new DroneStatus {Value = Status.New} );
                EntityManager.SetComponentData(drones[i], new Waypoint(float3.zero, -1,0));
                EntityManager.SetComponentData(drones[i], new PhysicsCollider { Value = _droneCollider });
                EntityManager.AddSharedComponentData(drones[i], _droneMesh);
                for (var j = 0; j < 4; ++j)
                {
                    var k = j + 4 * i;
                    EntityManager.SetComponentData(propellers[k], new Parent {Value = drones[i]});
                    EntityManager.SetComponentData(propellers[k], new Translation { Value = _propellerPositions[j]});
                    EntityManager.SetComponentData(propellers[k], new Rotation{ Value = quaternion.identity });
                    EntityManager.AddSharedComponentData(propellers[k], _propellerMesh);
                }
            }
            drones.Dispose();
            propellers.Dispose();

        }
    }
}
