using BovineLabs.Entities.Systems;
using Unity.Entities;
using UnityEngine;

namespace DroNeS.Systems
{
    [UpdateAfter(typeof(EntityEventSystem))]
    public class ClickProcessingSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref ClickEvent clickEvent) =>
            {
                Debug.Log($"Entity {clickEvent.Entity.Index} clicked");
            });
        }
    }
}
