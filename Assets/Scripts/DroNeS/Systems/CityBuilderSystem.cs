using DroNeS.Components.Tags;
using Mapbox.Unity;
//using DroNeS.Mapbox.ECS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace DroNeS.Systems
{
    [DisableAutoCreation]
    public class CityBuilderSystem : ComponentSystem
    {
        private static EntityArchetype _terrain;
        private static EntityArchetype _building;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
        }

        public static void Initialize()
        {
            var i = MapboxAccess.Instance;
            _building = World.Active.EntityManager.CreateArchetype(
                ComponentType.ReadOnly<BuildingTag>(), 
                ComponentType.ReadOnly<Translation>(),
                typeof(Static),
                typeof(LocalToWorld));
            _terrain = World.Active.EntityManager.CreateArchetype(
                ComponentType.ReadOnly<TerrainTag>(),
                ComponentType.ReadOnly<Translation>(),
                typeof(Static),
                typeof(LocalToWorld));
        }

        public static void MakeBuilding(in float3 position, in RenderMesh renderMesh)
        {
            var entity = World.Active.EntityManager.CreateEntity(_building);
            World.Active.EntityManager.SetComponentData(entity, new Translation { Value = position });
            World.Active.EntityManager.AddSharedComponentData(entity, renderMesh);
        }
        public static void MakeTerrain(in float3 position, in RenderMesh renderMesh)
        {
            var entity = World.Active.EntityManager.CreateEntity(_terrain);
            World.Active.EntityManager.SetComponentData(entity, new Translation { Value = position });
            World.Active.EntityManager.AddSharedComponentData(entity, renderMesh);
        }
    }
}
