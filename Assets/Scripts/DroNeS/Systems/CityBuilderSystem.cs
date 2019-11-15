using DroNeS.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace DroNeS.Systems
{
    public class CityBuilderBarrierSystem : EntityCommandBufferSystem
    {
    }
    public class CityBuilderSystem : ComponentSystem
    {
        private static EntityManager Manager => World.Active.EntityManager;
        private static CityBuilderBarrierSystem _barrier;
        private static EntityArchetype _terrain;
        private static EntityArchetype _building;

        protected override void OnCreate()
        {
//            _barrier = World.Active.GetOrCreateSystem<CityBuilderBarrierSystem>();
//            _terrain = Manager.CreateArchetype(
//                ComponentType.ReadOnly<TerrainTag>(),
//                ComponentType.ReadOnly<Translation>(),
//                typeof(LocalToWorld));
//            
//            _building = Manager.CreateArchetype(
//                ComponentType.ReadOnly<BuildingTag>(), 
//                ComponentType.ReadOnly<Translation>(),
//                typeof(LocalToWorld));
//            DronesMap.Build();
        }

        protected override void OnUpdate()
        {
            
        }

        public static void MakeBuilding(in float3 position, in RenderMesh renderMesh)
        {
            var commands = _barrier.CreateCommandBuffer();
			var entity = commands.CreateEntity(_building);
			commands.SetComponent(entity, new Translation { Value = position });
            commands.AddSharedComponent(entity, renderMesh);
        }
        public static void MakeTerrain(in float3 position, in RenderMesh renderMesh)
        {
            var commands = _barrier.CreateCommandBuffer();
            var entity = commands.CreateEntity(_terrain);
            commands.SetComponent(entity, new Translation { Value = position });
            commands.AddSharedComponent(entity, renderMesh);
        }
    }
}
