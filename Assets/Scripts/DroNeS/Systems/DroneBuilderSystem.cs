using DroNeS.Components;
using Unity.Collections;
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
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class DroneBuilderSystem : ComponentSystem
    {
        private EntityArchetype _drone;
        private BlobAssetReference<Collider> _droneCollider;
        private EntityArchetype _propeller;
        private RenderMesh _propellerMesh;
        private RenderMesh _droneMesh;
        private static EntityManager Manager => World.Active.EntityManager;
        private int _droneUid;
        private int _buildQueue;

        private float3[] _propellerPositions;

        protected override void OnCreate()
        {
            base.OnCreate();
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
            Manager.CreateEntity(_drone, drones);
            Manager.CreateEntity(_propeller, propellers);
            for (var i = 0; i < drones.Length; ++i)
            {
                Manager.SetComponentData(drones[i], new Translation {Value = Random.insideUnitSphere * 5});
                Manager.SetComponentData(drones[i], new Rotation{ Value = quaternion.identity });
                Manager.SetComponentData(drones[i], new DroneUID {Value = ++_droneUid} );
                Manager.SetComponentData(drones[i], new DroneStatus {Value = Status.New} );
                Manager.SetComponentData(drones[i], new Waypoint(float3.zero, -1,0));
                Manager.SetComponentData(drones[i], new PhysicsCollider { Value = _droneCollider });
                Manager.AddSharedComponentData(drones[i], _droneMesh);
                for (var j = 0; j < 4; ++j)
                {
                    var k = j + 4 * i;
                    Manager.SetComponentData(propellers[k], new Parent {Value = drones[i]});
                    Manager.SetComponentData(propellers[k], new Translation { Value = _propellerPositions[j]});
                    Manager.SetComponentData(propellers[k], new Rotation{ Value = quaternion.identity });
                    Manager.AddSharedComponentData(propellers[k], _propellerMesh);
                }
            }
            drones.Dispose();
            propellers.Dispose();

        }
    }
}
