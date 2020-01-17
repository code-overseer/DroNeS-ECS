using DroNeS.Components.Tags;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace DroNeS.Systems.EventSystem
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(LateSimulationSystemGroup)), UpdateBefore(typeof(SelectionHighlightSystem))]
    public class OnClickEntityCommandBufferSystem : EntityCommandBufferSystem
    {
    }
    
    [DisableAutoCreation]
    [UpdateInGroup(typeof(LateSimulationSystemGroup)), UpdateBefore(typeof(MessagePassingSystem))]
    public class SelectionHighlightSystem : ComponentSystem
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
            _droneMesh = AssetData.Drone.ToRenderMesh();
            _droneHighlight = AssetData.Drone.ToHighlightMesh();
            _hubHighlight = AssetData.Hub.ToHighlightMesh();
            _hubMesh = AssetData.Hub.ToRenderMesh();
            _clicked = GetEntityQuery(typeof(PreSelectionTag));
            _selected = GetEntityQuery(typeof(SelectionTag));
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
            
            EntityManager.RemoveComponent<PreSelectionTag>(clicked[0]);
            var isDrone = EntityManager.HasComponent<DroneTag>(clicked[0]);
            EntityManager.SetSharedComponentData(clicked[0], isDrone ? _droneHighlight : _hubHighlight);
            
            if (selected.IsCreated && selected.Length > 0)
            {
                if (clicked[0] == selected[0]) return;
                RemoveSelection(ref selected);
            }
            EntityManager.AddComponent<SelectionTag>(clicked[0]);
        }

        private void DeselectAction(ref NativeArray<Entity> selected)
        {
            if (!Input.GetMouseButtonDown(1) || !selected.IsCreated || selected.Length < 1) return;
            RemoveSelection(ref selected);
        }

        private void RemoveSelection(ref NativeArray<Entity> selected)
        {
            var isDrone = EntityManager.HasComponent<DroneTag>(selected[0]);
            EntityManager.SetSharedComponentData(selected[0], isDrone ? _droneMesh : _hubMesh);
            EntityManager.RemoveComponent<SelectionTag>(selected[0]);
        }

    }
}
