using DroNeS.Components;
using DroNeS.Components.Tags;
using DroNeS.Mapbox.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace DroNeS.Systems
{
    [DisableAutoCreation]
    public class CityBuilderSystem : ComponentSystem
    {
        private static EntityManager Manager => World.Active.EntityManager;
        private static EntityArchetype _terrain;
        private static EntityArchetype _building;
        
        protected override void OnCreate()
        {
            _terrain = Manager.CreateArchetype(
                ComponentType.ReadOnly<TerrainTag>(),
                ComponentType.ReadOnly<Translation>(),
                typeof(Static),
                typeof(LocalToWorld));
            
            _building = Manager.CreateArchetype(
                ComponentType.ReadOnly<BuildingTag>(), 
                ComponentType.ReadOnly<Translation>(),
                typeof(Static),
                typeof(LocalToWorld));
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            DronesMap.Build();
        }

        protected override void OnUpdate()
        {
        }

        public static void MakeBuilding(in float3 position, in RenderMesh renderMesh)
        {
            var entity = Manager.CreateEntity(_building);
            Manager.SetComponentData(entity, new Translation { Value = position });
            Manager.AddSharedComponentData(entity, renderMesh);
        }
        public static void MakeTerrain(in float3 position, in RenderMesh renderMesh)
        {
            var entity = Manager.CreateEntity(_terrain);
            Manager.SetComponentData(entity, new Translation { Value = position });
            Manager.AddSharedComponentData(entity, renderMesh);
        }
    }
}
