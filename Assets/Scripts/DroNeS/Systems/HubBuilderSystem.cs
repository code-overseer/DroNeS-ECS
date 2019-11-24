using DroNeS.Components;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;
using Random = Unity.Mathematics.Random;
using Collider = Unity.Physics.Collider;

namespace DroNeS.Systems
{
    public class HubBuilderSystem : ComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem _barrier;
        private EntityArchetype _hub;
        private RenderMesh _hubMesh;
        private BlobAssetReference<Collider> _hubCollider;
        private int _hubUid;
        private Random _rand = new Random(1u);
        private const float Altitude = 800; 
        private static EntityManager Manager => World.Active.EntityManager;
        protected override void OnCreate()
        {
            base.OnCreate();
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _hub = Manager.CreateArchetype(
                ComponentType.ReadOnly<HubUID>(),
                typeof(Translation),
                typeof(Rotation),
                typeof(LocalToWorld),
                typeof(JobGenerationCounter),
                typeof(JobGenerationRate),
                typeof(JobGenerationTimeMark)
            );
            _hubUid = 0;
            _hubMesh = new RenderMesh
            {
                mesh = Resources.Load("Meshes/Airship") as Mesh,
                material = Resources.Load("Materials/Airship") as UnityEngine.Material
            };
            var geometry = new BoxGeometry
            {
                Center = new float3(0.01313019f, -0.6270895f, -16.10762f),
                Orientation = quaternion.identity,
                Size = new float3(43.56474f, 44.71868f, 170.6734f),
                BevelRadius = 0.05f * 45,
            };
            _hubCollider = BoxCollider.Create(geometry,
                new CollisionFilter
                {
                    BelongsTo = CollisionGroups.Hub,
                    CollidesWith = CollisionGroups.Cast,
                    GroupIndex = 0
                });
        }

        public void BuildHub(float rate, float3 position)
        {
            var buildCommands = _barrier.CreateCommandBuffer();
            position.y = Altitude;
            var hub = buildCommands.CreateEntity(_hub);
            buildCommands.SetComponent(hub, new HubUID {Value = ++_hubUid} );
            buildCommands.SetComponent(hub, new Translation {Value = position});
            buildCommands.SetComponent(hub, new Rotation{ Value = quaternion.identity });
            buildCommands.SetComponent(hub, new JobGenerationCounter {Value = 0} );
            buildCommands.SetComponent(hub, new JobGenerationRate {Value = rate} );
            buildCommands.SetComponent(hub, new JobGenerationTimeMark { Value = CurrentMark(in rate) });
            buildCommands.AddSharedComponent(hub, _hubMesh);
        }

        private float2 CurrentMark(in float rate)
        {
            var now = (float) World.Active.GetOrCreateSystem<SunOrbitSystem>().Clock;
            var next = -math.log(1 - _rand.NextFloat(0, 1)) / rate;
            return new float2(now, next);
        }
        
        protected override void OnUpdate()
        {
        }
    }
}
