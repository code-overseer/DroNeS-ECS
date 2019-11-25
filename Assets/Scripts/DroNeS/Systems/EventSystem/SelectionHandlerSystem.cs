using DroNeS.Components.Tags;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

// ReSharper disable AccessToDisposedClosure

namespace DroNeS.Systems.EventSystem
{
    public class SelectionHandlerSystem : ComponentSystem
    {
        private EntityQuery _clicked;
        private EntityQuery _selected;
        private RenderMesh _droneHighlight;
        private RenderMesh _droneMesh;
        private RenderMesh _hubHighlight;
        private RenderMesh _hubMesh;

        protected override void OnCreate()
        {
            base.OnCreate();
            _droneMesh = EntityData.Drone.ToRenderMesh();
            _droneHighlight = EntityData.Drone.ToHighlightMesh();
            _hubHighlight = EntityData.Hub.ToHighlightMesh();
            _hubMesh = EntityData.Hub.ToRenderMesh();
            _clicked = GetEntityQuery(typeof(SelectionTag));
            _selected = GetEntityQuery(typeof(RenderMesh));
            _selected.SetFilter(_droneHighlight);
        }

        protected override void OnUpdate()
        {
            SelectAction(out var clicked, out var selected);
            DeselectAction(ref selected);
            clicked.Dispose();
            selected.Dispose();
        }

        private void SelectAction(out NativeArray<Entity> clicked, out NativeArray<Entity> selected)
        {
            
            clicked = _clicked.ToEntityArray(Allocator.TempJob);
            selected = _selected.ToEntityArray(Allocator.TempJob);
            
            if (!clicked.IsCreated || clicked.Length < 1) return;
            
            EntityManager.RemoveComponent<SelectionTag>(clicked[0]);
            var isDrone = EntityManager.HasComponent<DroneTag>(clicked[0]);
            EntityManager.SetSharedComponentData(clicked[0], isDrone ? _droneHighlight : _hubHighlight);

            if (!selected.IsCreated || selected.Length < 1 || clicked[0] == selected[0]) return;
            
            isDrone = EntityManager.HasComponent<DroneTag>(selected[0]);
            EntityManager.SetSharedComponentData(selected[0], isDrone ? _droneMesh : _hubMesh);
        }

        private void DeselectAction(ref NativeArray<Entity> selected)
        {
            if (!Input.GetMouseButtonDown(1) || !selected.IsCreated || selected.Length < 1) return;
            var isDrone = EntityManager.HasComponent<DroneTag>(selected[0]);
            EntityManager.SetSharedComponentData(selected[0], isDrone ? _droneMesh : _hubMesh);
        }

    }
}
