using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

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
        private static int _droneUid;

        protected override void OnCreate()
        {
            _barrier = World.Active.GetOrCreateSystem<DroneBuilderBarrierSystem>();
            _drone = Manager.CreateArchetype(
                ComponentType.ReadOnly<DroneTag>(),
                ComponentType.ReadOnly<DroneUID>(),
                typeof(Translation),
                typeof(DroneStatus),
                typeof(Waypoint),
                typeof(LocalToWorld));
            
            _droneMesh = new RenderMesh
            {
                mesh = Resources.Load("Meshes/Drone") as Mesh,
                material = Resources.Load("Materials/Drone") as Material
            };
        }

        public static void AddDrone()
        {
            var buildCommands = _barrier.CreateCommandBuffer();
            for (var i = 0; i < 500; ++i)
            {
                var drone = buildCommands.CreateEntity(_drone);
                buildCommands.SetComponent(drone, new Translation {Value = Random.insideUnitSphere * 5});
                buildCommands.SetComponent(drone, new DroneUID {Value = _droneUid++} );
                buildCommands.SetComponent(drone, new DroneStatus {Value = Status.New} );
                buildCommands.SetComponent(drone, new Waypoint(float3.zero, -1,0));
                buildCommands.AddSharedComponent(drone, _droneMesh);
            }
        }

        protected override void OnUpdate()
        {
        }
    }
}
