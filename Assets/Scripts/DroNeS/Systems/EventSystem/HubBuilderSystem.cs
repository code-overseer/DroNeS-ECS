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
    [DisableAutoCreation]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class HubBuilderSystem : ComponentSystem
    {
        private int _hubUid;
        private const float Altitude = 800;
        private EntityArchetype _hub;
        private RenderMesh _hubMesh;
        private BlobAssetReference<Collider> _hubCollider;
        private Random _rand = new Random(1u);
        private NativeQueue<float4> _buildQueue;
        protected override void OnCreate()
        {
            base.OnCreate();
            _buildQueue = new NativeQueue<float4>(Allocator.Persistent);
            _hubUid = 0;
            _hub = Archetypes.Hub;
            _hubMesh = AssetData.Hub.ToRenderMesh();
            _hubCollider = AssetData.Hub.BoxCollider;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _buildQueue.Dispose();
            _hubCollider.Dispose();
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
                EntityManager.SetSharedComponentData(hubs[idx], _hubMesh);
                ++idx;
            }
            hubs.Dispose();

        }
    }
}
