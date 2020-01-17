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
//    [DisableAutoCreation]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class DroneBuilderSystem : ComponentSystem
    {
        private BlobAssetReference<Collider> _droneCollider;
        private RenderMesh _propellerMesh;
        private RenderMesh _droneMesh;
        private int _droneUid;
        private int _buildQueue;
        private float3[] _propellerPositions;

        protected override void OnCreate()
        {
            base.OnCreate();
            _droneUid = 0;
            _droneMesh = AssetData.Drone.ToRenderMesh();
            _propellerMesh = AssetData.Drone.ToPropellerMesh();
            _propellerPositions = AssetData.Drone.PropellerPositions;
            _droneCollider = AssetData.Drone.BoxCollider;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _droneCollider.Dispose();
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
            EntityManager.CreateEntity(Archetypes.Drone, drones);
            EntityManager.CreateEntity(Archetypes.Propeller, propellers);
            
            for (var i = 0; i < drones.Length; ++i)
            {
                EntityManager.SetComponentData(drones[i], new Translation {Value = Random.insideUnitSphere * 5});
                EntityManager.SetComponentData(drones[i], new Rotation{ Value = quaternion.identity });
                EntityManager.SetComponentData(drones[i], new DroneUID {Value = ++_droneUid} );
                EntityManager.SetComponentData(drones[i], new DroneStatus {Value = Status.New} );
                EntityManager.SetComponentData(drones[i], new Waypoint(float3.zero, -1,0));
                EntityManager.SetComponentData(drones[i], new PhysicsCollider { Value = _droneCollider });
                EntityManager.SetSharedComponentData(drones[i], _droneMesh);
                for (var j = 0; j < 4; ++j)
                {
                    var k = j + 4 * i;
                    EntityManager.SetComponentData(propellers[k], new Parent {Value = drones[i]});
                    EntityManager.SetComponentData(propellers[k], new Translation { Value = _propellerPositions[j]});
                    EntityManager.SetComponentData(propellers[k], new Rotation{ Value = quaternion.identity });
                    EntityManager.SetSharedComponentData(propellers[k], _propellerMesh);
                }
            }
            drones.Dispose();
            propellers.Dispose();

        }
    }
}
