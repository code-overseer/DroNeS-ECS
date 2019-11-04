using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using DroneStatus = DroNeS.Components.DroneStatus;

namespace DroNeS
{
    public static class DroneBootstrap
    {
        private static EntityArchetype _drone;
        private static EntityManager _manager;
        private static RenderMesh _droneMesh;
        private static EntityCommandBuffer _droneCommands;
        private static int _droneUid = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            _manager = World.Active.GetOrCreateManager<EntityManager>();
            _drone = _manager.CreateArchetype(
                ComponentType.Create<Position>(),
                ComponentType.Create<DroneUID>(),
                ComponentType.Create<DroneStatus>());
            _droneMesh = new RenderMesh()
            {
                mesh = Resources.Load("Meshes/Drone") as Mesh,
                material = Resources.Load("Materials/Drone") as Material
            };

        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeWithScene()
        {
            for (var i = 0; i < 5; ++i)
            {
                var drone = _manager.CreateEntity(_drone);
                _manager.SetComponentData(drone, new Position {Value = Random.insideUnitSphere * 5} );
                _manager.SetComponentData(drone, new DroneUID {uid = _droneUid++} );
                _manager.SetComponentData(drone, new DroneStatus {Value = Status.Waiting} );
                _manager.AddSharedComponentData(drone, _droneMesh);
            }   
        }

        public static void AddDrone()
        {
            _droneCommands = new EntityCommandBuffer(Allocator.Temp);
            for (var i = 0; i < 5; ++i)
            {
                var drone = _droneCommands.CreateEntity(_drone);
                _droneCommands.SetComponent(drone, new Position {Value = Random.insideUnitSphere * 5});
                _droneCommands.SetComponent(drone, new DroneUID {uid = _droneUid++} );
                _droneCommands.SetComponent(drone, new DroneStatus {Value = Status.Waiting} );
                _droneCommands.AddSharedComponent(drone, _droneMesh);
            }
            _droneCommands.Playback(_manager);

        }


    }
}