using DroNeS.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DroNeS.Systems
{
    [UpdateAfter(typeof(UserInputBarrier))]
    public class ClickProcessingSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity entity, ref Clicked clicked) =>
            {
                Debug.Log($"Entity {entity.Index} clicked");
                PostUpdateCommands.RemoveComponent(entity, typeof(Clicked));
            });
        }
    }
}
