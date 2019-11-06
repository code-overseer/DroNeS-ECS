using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Systems
{
    public class DroneBuilderBarrierSystem : EntityCommandBufferSystem
    {
 
    }
    public class DroneBuilderSystem : ComponentSystem
    {
        private static DroneBuilderBarrierSystem _barrier;
        private static EntityArchetype _drone;
        private static RenderMesh _droneMesh;
        private static EntityManager Manager => World.Active.EntityManager;
        private static EntityCommandBuffer _buildCommands;
        private static int _droneUid;

        protected override void OnCreate()
        {
            _barrier = World.Active.GetOrCreateSystem<DroneBuilderBarrierSystem>();
            _drone = Manager.CreateArchetype(
                typeof(Translation),
                typeof(DroneUID),
                typeof(DroneStatus),
                typeof(Waypoint),
                typeof(DroneTag),
                typeof(LocalToWorld));
            
            _droneMesh = new RenderMesh
            {
                mesh = Resources.Load("Meshes/Drone") as Mesh,
                material = Resources.Load("Materials/Drone") as Material
            };
            
            var entities = new NativeArray<Entity>(5, Allocator.Temp);
            Manager.CreateEntity(_drone, entities);
            foreach (var drone in entities)
            {
                Manager.SetComponentData(drone, new Translation {Value = Random.insideUnitSphere * 5});
                Manager.SetComponentData(drone, new DroneUID {uid = _droneUid++} );
                Manager.SetComponentData(drone, new DroneStatus {Value = Status.New} );
                Manager.AddSharedComponentData(drone, _droneMesh);
            }

        }

        public static void AddDrone()
        {
            _buildCommands = _barrier.CreateCommandBuffer();
            for (var i = 0; i < 500; ++i)
            {
                var drone = _buildCommands.CreateEntity(_drone);
                _buildCommands.SetComponent(drone, new Translation {Value = Random.insideUnitSphere * 5});
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
