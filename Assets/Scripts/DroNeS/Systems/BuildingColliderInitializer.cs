using DroNeS.Components.Tags;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using BoxCollider = Unity.Physics.BoxCollider;
using Collider = Unity.Physics.Collider;

namespace DroNeS.Systems
{
    public class BuildingColliderInitializer : ComponentSystem
    {
        private BlobAssetReference<Collider> _collider;
        private EntityArchetype _buildingCollider;
        private RenderMesh _cubeMesh;

        protected override void OnCreate()
        {
            base.OnCreate();
            _buildingCollider = EntityManager.CreateArchetype(
                ComponentType.ReadOnly<BuildingTag>(),
                typeof(Static),
                typeof(LocalToWorld),
                typeof(PhysicsCollider)
            );
            
            _cubeMesh = AssetData.BuildingCollider.ToRenderMesh();
            _collider = BoxCollider.Create(AssetData.BuildingCollider.BoxGeometry,
                new CollisionFilter
                {
                    BelongsTo = CollisionGroups.Buildings,
                    CollidesWith = CollisionGroups.Cast,//CollisionGroups.Drone | CollisionGroups.Cast, TODO ignore drones for now
                    GroupIndex = 0
                });
            var t = AssetData.BuildingCollider.Parent;
            var entities = new NativeArray<Entity>(t.childCount, Allocator.TempJob);
            EntityManager.CreateEntity(_buildingCollider, entities);
            for (var i = 0; i < t.childCount; ++i)
            {
                var child = t.GetChild(i);
                var ltw = math.mul(new float4x4(child.rotation, child.position), float4x4.Scale(child.localScale));
                EntityManager.SetComponentData(entities[i], new LocalToWorld{Value = ltw});
                EntityManager.SetComponentData(entities[i], new PhysicsCollider{Value = _collider});
                EntityManager.AddSharedComponentData(entities[i], _cubeMesh);
            }
            entities.Dispose();
            Enabled = false;
        }

        protected override void OnUpdate()
        {
        }
    }
}
