using DroNeS.Components;
using DroNeS.SharedComponents;
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
    public class DroneBuilderSystem : ComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem _barrier;
        private EntityArchetype _drone;
        private RenderMesh _droneMesh;
        private EntityManager Manager => World.Active.EntityManager;
        private int _droneUid;

        protected override void OnCreate()
        {
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _droneUid = 0;
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

        public void AddDrone()
        {
            var buildCommands = _barrier.CreateCommandBuffer();
            for (var i = 0; i < 5; ++i)
            {
                var drone = buildCommands.CreateEntity(_drone);
                buildCommands.SetComponent(drone, new Translation {Value = Random.insideUnitSphere * 5});
                buildCommands.SetComponent(drone, new DroneUID {Value = ++_droneUid} );
                buildCommands.SetComponent(drone, new DroneStatus {Value = Status.New} );
                buildCommands.SetComponent(drone, new Waypoint(float3.zero, -1,0));
                buildCommands.AddSharedComponent(drone, _droneMesh);
                buildCommands.AddSharedComponent(drone, new Clickable());
            }
        }

        protected override void OnUpdate()
        {
        }
    }
}
