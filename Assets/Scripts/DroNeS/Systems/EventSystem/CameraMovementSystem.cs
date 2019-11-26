using System.Diagnostics;
using DroNeS.Components;
using DroNeS.Components.Singletons;
using DroNeS.Components.Tags;
using DroNeS.MonoBehaviours;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DroNeS.Systems.EventSystem
{
    public class CameraTypeDefinition : ComponentSystem
    {
        private EntityQuery _query;
        protected override void OnCreate()
        {
            base.OnCreate();
            _query = GetEntityQuery(typeof(Transform), typeof(Camera));
        }

        protected override void OnUpdate()
        {
            Entities.With(_query).ForEach((Entity entity, CameraTypeClass type, Camera camera) =>
            {
                
                if (type.value == CameraTypeValue.Satellite)
                {
                    EntityManager.AddComponentData(entity, new SatelliteCameraTag());
                    World.Active.GetOrCreateSystem<CameraMovementSystem>().Satellite = camera;
                }
                else
                {
                    EntityManager.AddComponentData(entity, new MainCameraTag());
                    World.Active.GetOrCreateSystem<CameraMovementSystem>().Main = camera;
                }
                
                EntityManager.RemoveComponent<CameraTypeClass>(entity);
                    
            });
            Enabled = false;
        }
    }
    
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class CameraMovementSystem : JobComponentSystem
    {
        private EntityQuery _mainCamera;
        private EntityQuery _satelliteCamera;
        private Stopwatch _watch;
        public Camera Satellite;
        public Camera Main;
        private NativeArray<float> _orthographicSize;
        protected override void OnCreate()
        {
            base.OnCreate();
            _mainCamera = GetEntityQuery(typeof(Transform),
                ComponentType.ReadOnly<MainCameraTag>());
            _satelliteCamera = GetEntityQuery(typeof(Transform),
                ComponentType.ReadOnly<SatelliteCameraTag>());
            _watch = new Stopwatch();
            _orthographicSize = new NativeArray<float>(1, Allocator.Persistent);
            EntityManager.CreateEntity(typeof(View));
            SetSingleton(new View{CameraType = CameraTypeValue.Main});
        }

        public void OnCameraSwap()
        {
            var v = GetSingleton<View>();
            SetSingleton(new View
                {CameraType = v.CameraType == CameraTypeValue.Main ? CameraTypeValue.Satellite : CameraTypeValue.Main});
            Main.enabled = !Main.enabled;
            Satellite.enabled = !Satellite.enabled;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _watch.Start();
            _orthographicSize[0] = Satellite == null ? 4000 : Satellite.orthographicSize;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _orthographicSize.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var delta = _watch.ElapsedMilliseconds * 0.001f;
            _watch.Restart();
            Satellite.orthographicSize = _orthographicSize[0];
            var job = new MainCameraMovementJob
            {
                Delta = delta,
                Input = PlayerInput.Get(),
                MainPosition = new NativeArray<float3>(1, Allocator.TempJob)
            };
            
            return new SatelliteCameraMovementJob
            {
                Input = PlayerInput.Get(),
                MainPosition = job.MainPosition,
                OrthographicSize = _orthographicSize
            }.Schedule(_satelliteCamera.GetTransformAccessArray(), 
                job.Schedule(_mainCamera.GetTransformAccessArray(), inputDeps));
        }
        
        [BurstCompile] 
        private struct MainCameraMovementJob : IJobParallelForTransform
        {
            public float Delta;
            public PlayerInput Input;
            [WriteOnly] public NativeArray<float3> MainPosition;

            public void Execute(int index, TransformAccess transform)
            {
                float3 position = transform.position;
                quaternion rotation = transform.rotation;
                var t = Input.IsMainCamera();
                var scale = 2 * (t * position.y + (1 - t) * 1500) + 5;
                var forward = math.forward(rotation);
                var right = math.normalize(math.cross(math.up(), forward));
                var positive = math.normalize(forward * new float3(1,0,1));
                
                position += Input.Vertical() * Delta * positive * scale;
                position += Input.Horizontal() * Delta * right * scale;
                position += -Input.Scroll() * Delta * 3 * forward * math.clamp(forward.y, -1, 0) * scale;
                
                rotation = math.mul(quaternion.AxisAngle(math.up(), 2 * math.radians(Input.MouseX()) * Input.MiddleMouse()), rotation);
                rotation = math.mul(quaternion.AxisAngle(right,  2 * math.radians(-Input.MouseY()) * Input.MiddleMouse()), rotation);
                rotation = math.mul(quaternion.AxisAngle(math.up(),  2 * math.radians(Input.Rotate())), rotation);
                
                transform.position = position;
                transform.rotation = rotation;
                MainPosition[0] = transform.position;
            }
        }

        [BurstCompile]
        private struct SatelliteCameraMovementJob : IJobParallelForTransform
        {
            public PlayerInput Input;
            [DeallocateOnJobCompletion, ReadOnly]
            public NativeArray<float3> MainPosition;
            public NativeArray<float> OrthographicSize;
            public void Execute(int index, TransformAccess transform)
            {
                var position = MainPosition[0];
                position.y = 1500;
                transform.position = position;

                var rotation = transform.rotation;
                rotation = math.mul(quaternion.AxisAngle(math.up(), 2 * math.radians(Input.MouseX()) * Input.MiddleMouse()), rotation);
                rotation = math.mul(quaternion.AxisAngle(math.up(),  2 * math.radians(Input.Rotate())), rotation);
                transform.rotation = rotation;
                
                OrthographicSize[0] = math.clamp(OrthographicSize[0] - Input.Scroll() * 100, 300, 4000f);
            }
        }
        
    }
}
