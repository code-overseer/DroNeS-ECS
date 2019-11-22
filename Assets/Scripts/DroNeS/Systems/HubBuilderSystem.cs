using DroNeS.Components;
using DroNeS.SharedComponents;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DroNeS.Systems
{
    public class HubBuilderSystem : ComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem _barrier;
        private EntityArchetype _hub;
        private RenderMesh _hubMesh;
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
                material = Resources.Load("Materials/Airship") as Material
            };
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
            buildCommands.AddSharedComponent(hub, new Clickable());
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
