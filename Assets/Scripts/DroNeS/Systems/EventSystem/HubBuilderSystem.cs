using DroNeS.Components;
using DroNeS.Systems.FixedUpdates;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using BoxCollider = Unity.Physics.BoxCollider;
using Clock = DroNeS.Components.Singletons.Clock;
using Random = Unity.Mathematics.Random;
using Collider = Unity.Physics.Collider;

namespace DroNeS.Systems.EventSystem
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class HubBuilderSystem : ComponentSystem
    {
        private EntityArchetype _hub;
        private RenderMesh _hubMesh;
        private BlobAssetReference<Collider> _hubCollider;
        private int _hubUid;
        private Random _rand = new Random(1u);
        private const float Altitude = 800;
        private NativeQueue<float4> _buildQueue;
        private static EntityManager Manager => World.Active.EntityManager;
        protected override void OnCreate()
        {
            base.OnCreate();
            _buildQueue = new NativeQueue<float4>(Allocator.Persistent);
            _hub = Manager.CreateArchetype(
                ComponentType.ReadOnly<HubUID>(),
                typeof(Translation),
                typeof(Rotation),
                typeof(LocalToWorld),
                typeof(JobGenerationCounter),
                typeof(JobGenerationRate),
                typeof(JobGenerationTimeMark),
                typeof(PhysicsCollider)
            );
            _hubUid = 0;
            _hubMesh = EntityData.Hub.ToRenderMesh();
            _hubCollider = BoxCollider.Create(EntityData.Hub.BoxGeometry,
                new CollisionFilter
                {
                    BelongsTo = CollisionGroups.Hub,
                    CollidesWith = CollisionGroups.Cast,
                    GroupIndex = 0
                });
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _buildQueue.Dispose();
        }

        public void BuildHub(float rate, float3 position)
        {
            position.y = Altitude;
            _buildQueue.Enqueue(new float4(rate, position));
        }

        private float2 CurrentMark(in float rate)
        {
            var now = (float)GetSingleton<Clock>().Value;
            var next = -math.log(1 - _rand.NextFloat(0, 1)) / rate;
            return new float2(now, next);
        }
        
        protected override void OnUpdate()
        {
            if (_buildQueue.Count < 1) return;
            
            var hubs = new NativeArray<Entity>(_buildQueue.Count, Allocator.TempJob);
            EntityManager.CreateEntity(_hub, hubs);
            var idx = 0;
            while (_buildQueue.TryDequeue(out var info))
            {
                EntityManager.SetComponentData(hubs[idx], new HubUID {Value = ++_hubUid} );
                EntityManager.SetComponentData(hubs[idx], new Translation {Value = info.yzw});
                EntityManager.SetComponentData(hubs[idx], new Rotation{ Value = quaternion.identity });
                EntityManager.SetComponentData(hubs[idx], new JobGenerationCounter {Value = 0} );
                EntityManager.SetComponentData(hubs[idx], new JobGenerationRate {Value = info.x} );
                EntityManager.SetComponentData(hubs[idx], new JobGenerationTimeMark { Value = CurrentMark(in info.x) });
                EntityManager.SetComponentData(hubs[idx], new PhysicsCollider{ Value = _hubCollider });
                EntityManager.AddSharedComponentData(hubs[idx], _hubMesh);
                ++idx;
            }
            hubs.Dispose();

        }
    }
}
