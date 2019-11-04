using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;

namespace DroNeS
{
    public class DroneBootstrap : MonoBehaviour
    {
        private static EntityArchetype _drone;
        private static EntityManager _manager;
        private static RenderMesh _droneMesh;
        public static Mesh mesh;
        public static Material material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            _manager = World.Active.GetOrCreateManager<EntityManager>();
            _drone = _manager.CreateArchetype(
                ComponentType.Create<Position>(),
                ComponentType.Create<NativeQueue<Position>>());
            _droneMesh = new RenderMesh()
            {
                mesh = mesh,
                material = material
            };

        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeWithScene()
        {
            for (var i = 0; i < 5; ++i)
            {
                var drone = _manager.CreateEntity(_drone);
                _manager.SetComponentData(drone, new Position {Value = Random.insideUnitSphere * 5} );
                _manager.AddSharedComponentData(drone, _droneMesh);
            }   
        }

    }
}