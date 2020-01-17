using System.Collections.Generic;
using DroNeS.Components;
using DroNeS.Components.Tags;
using Unity.Entities;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

namespace DroNeS
{
    public class Archetypes
    {
        private static EntityManager Manager => World.Active.EntityManager;
        
        private static readonly Archetypes Instance = new Archetypes();

        private readonly Dictionary<ArchetypeKey, EntityArchetype> _archetypes = new Dictionary<ArchetypeKey, EntityArchetype>();

        public static EntityArchetype Drone
        {
            get
            {
                var dict = Instance._archetypes;
                if (!dict.TryGetValue(ArchetypeKey.Drone, out var output))
                {
                    dict[ArchetypeKey.Drone] = output = Manager.CreateArchetype(
                        ComponentType.ReadOnly<DroneTag>(),
                        ComponentType.ReadOnly<DroneUID>(),
                        typeof(Translation),
                        typeof(DroneStatus),
                        typeof(Waypoint),
                        typeof(Rotation),
                        typeof(LocalToWorld),
                        typeof(PhysicsCollider),
                        typeof(RenderMesh)
                    ); 
                }
                return output;
            }
        }

        public static EntityArchetype Propeller
        {
            get
            {
                var dict = Instance._archetypes;
                if (!dict.TryGetValue(ArchetypeKey.Propeller, out var output))
                {
                    dict[ArchetypeKey.Propeller] = output = Manager.CreateArchetype(
                        ComponentType.ReadOnly<Parent>(),
                        ComponentType.ReadOnly<PropellerTag>(),
                        typeof(Translation),
                        typeof(Rotation),
                        typeof(LocalToParent),
                        typeof(LocalToWorld),
                        typeof(RenderMesh)
                    );
                }
                return output;
            }
        }
        
        public static EntityArchetype Hub
        {
            get
            {
                var dict = Instance._archetypes;
                if (!dict.TryGetValue(ArchetypeKey.Hub, out var output))
                {
                    dict[ArchetypeKey.Hub] = output = Manager.CreateArchetype(
                        ComponentType.ReadOnly<HubUID>(),
                        typeof(Translation),
                        typeof(Rotation),
                        typeof(LocalToWorld),
                        typeof(JobGenerationCounter),
                        typeof(JobGenerationRate),
                        typeof(JobGenerationTimeMark),
                        typeof(PhysicsCollider),
                        typeof(RenderMesh)
                    );
                }
                return output;
            }
        }
    }
}
