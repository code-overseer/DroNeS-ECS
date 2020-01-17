using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class HighlightBlinkingSystem : ComponentSystem
    {
        private Color _highlight = Color.white;
        protected override void OnUpdate()
        {
            _highlight.a = math.sin(8 * Time.unscaledTime);
            AssetData.Drone.HighlightMaterial.color = _highlight;
            AssetData.Hub.HighlightMaterial.color = _highlight;
        }
    }
}
