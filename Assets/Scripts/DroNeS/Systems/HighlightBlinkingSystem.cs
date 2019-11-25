using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class HighlightBlinkingSystem : ComponentSystem
    {
        private Color _highlight = Color.white;
        protected override void OnUpdate()
        {
            _highlight.a = math.sin(8 * Time.unscaledTime);
            EntityData.Drone.HighlightMaterial.color = _highlight;
            EntityData.Hub.HighlightMaterial.color = _highlight;
        }
    }
}
