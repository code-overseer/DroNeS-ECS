using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
// ReSharper disable AccessToDisposedClosure

namespace DroNeS.Systems
{
    public class SelectionHandlerSystem : ComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem _barrier;
        private DroneBuilderSystem _droneBuilder;
        private EntityQuery _clicked;
        private EntityQuery _selected;
        private RenderMesh Selection => _droneBuilder.DroneSelection;

        protected override void OnCreate()
        {
            base.OnCreate();
            _barrier = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _droneBuilder = World.Active.GetOrCreateSystem<DroneBuilderSystem>();
            _clicked = GetEntityQuery(typeof(SelectionTag));
            _selected = GetEntityQuery(typeof(RenderMesh));
            _selected.SetFilter(_droneBuilder.DroneSelection);
        }

        protected override void OnUpdate()
        {
            var clicked = _clicked.ToEntityArray(Allocator.TempJob);
            var selected = _selected.ToEntityArray(Allocator.TempJob);
            SelectAction(ref clicked, ref selected);
            clicked.Dispose();
            selected.Dispose();
        }

        private void SelectAction(ref NativeArray<Entity> clicked, ref NativeArray<Entity> selected)
        {
            if (clicked.IsCreated && clicked.Length < 1) return;
            
            var buffer = _barrier.CreateCommandBuffer();
            buffer.RemoveComponent<SelectionTag>(clicked[0]);
            
            if (selected.IsCreated && selected.Length > 0 && clicked[0] != selected[0])
            {
                buffer.SetSharedComponent(selected[0], _droneBuilder.DroneMesh);
            }
            buffer.SetSharedComponent(clicked[0], Selection);
        }
        
    }
}
