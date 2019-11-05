using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Systems
{
    public class DroneBuilderBarrierSystem : BarrierSystem
    {
 
    }
    public class DroneBuilderSystem : ComponentSystem
    {
        [Inject] private DroneBuilderBarrierSystem _barrier;
        private static DroneBuilderSystem _instance;   
        private static EntityArchetype _drone;
        private static RenderMesh _droneMesh;
        private static EntityManager _manager;
        private static EntityCommandBuffer _buildCommands;
        private static int _droneUid;
        private ComponentGroup _destructionQuery;
        
        protected override void OnCreateManager()
        {
            _manager = World.Active.GetOrCreateManager<EntityManager>();
            _instance = this;
            _drone = _manager.CreateArchetype(
                ComponentType.Create<Position>(),
                ComponentType.ReadOnly<DroneUID>(),
                ComponentType.Create<DroneStatus>(),
                ComponentType.Create<Waypoint>(),
                ComponentType.ReadOnly<DroneTag>());
            _droneMesh = new RenderMesh()
            {
                mesh = Resources.Load("Meshes/Drone") as Mesh,
                material = Resources.Load("Materials/Drone") as Material
            };
            for (var i = 0; i < 5; ++i)
            {
                var drone = _manager.CreateEntity(_drone);
                _manager.SetComponentData(drone, new Position {Value = Random.insideUnitSphere * 5} );
                _manager.SetComponentData(drone, new DroneUID {uid = _droneUid++} );
                _manager.SetComponentData(drone, new DroneStatus {Value = Status.New} );
                _manager.AddSharedComponentData(drone, _droneMesh);
            }

            _destructionQuery = GetComponentGroup(new EntityArchetypeQuery
            {
                All = new []{ComponentType.Create<DestructionTag>()}
            });

        }

        public static void AddDrone()
        {
            _buildCommands = _instance._barrier.CreateCommandBuffer();
            for (var i = 0; i < 5; ++i)
            {
                var drone = _buildCommands.CreateEntity(_drone);
                _buildCommands.SetComponent(drone, new Position {Value = Random.insideUnitSphere * 5});
                _buildCommands.SetComponent(drone, new DroneUID {uid = _droneUid++} );
                _buildCommands.SetComponent(drone, new DroneStatus {Value = Status.New} );
                _buildCommands.AddSharedComponent(drone, _droneMesh);
            }
        }

        protected override void OnUpdate()
        {
            
        }
    }
}
