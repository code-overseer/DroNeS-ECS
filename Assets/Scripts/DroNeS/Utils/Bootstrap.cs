using System;
using System.Reflection;
using DroNeS.Systems.FixedUpdates;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Experimental.LowLevel;
using UnityEngine.Experimental.PlayerLoop;

namespace DroNeS.Utils
{
    
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateWorld()
        {
            Initialize("Default World", false);
        }
        
        private static readonly MethodInfo InsertManagerIntoSubsystemListBase =
            typeof(ScriptBehaviourUpdateOrder).GetMethod("InsertManagerIntoSubsystemList",
                BindingFlags.NonPublic | BindingFlags.Static);
        
        private static void DomainUnloadShutdown()
        {
            World.DisposeAllWorlds();

            WordStorage.Instance.Dispose();
            WordStorage.Instance = null;
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(null);
        }

        private static MethodInfo InsertManagerIntoSubsystemList<T>() where T : ComponentSystemBase
        {
            return InsertManagerIntoSubsystemListBase.MakeGenericMethod(typeof(T));
        }

        private static ComponentSystemBase GetOrCreateManagerAndLogException(World world, Type type)
        {
            try
            {
                return world.GetOrCreateSystem(type);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        private static void Initialize(string worldName, bool editorWorld)
        {
            PlayerLoopManager.RegisterDomainUnload(DomainUnloadShutdown, 10000);

            var world = new World(worldName);
            World.Active = world;
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            if (systems == null)
            {
                world.Dispose();
                if (World.Active == world)
                {
                    World.Active = null;
                }
                return;
            }

            // create presentation system and simulation system
            var initializationSystemGroup = world.GetOrCreateSystem<InitializationSystemGroup>();
            var simulationSystemGroup = world.GetOrCreateSystem<SimulationSystemGroup>();
            var presentationSystemGroup = world.GetOrCreateSystem<PresentationSystemGroup>();
            var fixedUpdateSystemGroup = world.GetOrCreateSystem<FixedUpdateGroup>();
            // Add systems to their groups, based on the [UpdateInGroup] attribute.
            foreach (var type in systems)
            {
                // Skip the built-in root-level systems
                if (type == typeof(InitializationSystemGroup) ||
                    type == typeof(SimulationSystemGroup) ||
                    type == typeof(PresentationSystemGroup) ||
                    type == typeof(FixedUpdateGroup))
                {
                    continue;
                }
                if (editorWorld)
                {
                    if (Attribute.IsDefined(type, typeof(ExecuteInEditMode)))
                        Debug.LogError(
                            $"{type} is decorated with {typeof(ExecuteInEditMode)}. Support for this attribute will be deprecated. Please use {typeof(ExecuteAlways)} instead.");
                    if (!Attribute.IsDefined(type, typeof(ExecuteAlways)))
                        continue;
                }

                var groups = type.GetCustomAttributes(typeof(UpdateInGroupAttribute), true);
                if (groups.Length == 0)
                {
                    simulationSystemGroup.AddSystemToUpdateList(GetOrCreateManagerAndLogException(world, type));
                }

                foreach (var g in groups)
                {
                    var group = g as UpdateInGroupAttribute;
                    if (group == null)
                        continue;

                    if (!(typeof(ComponentSystemGroup)).IsAssignableFrom(group.GroupType))
                    {
                        Debug.LogError($"Invalid [UpdateInGroup] attribute for {type}: {group.GroupType} must be derived from ComponentSystemGroup.");
                        continue;
                    }

                    var groupMgr = GetOrCreateManagerAndLogException(world, group.GroupType);
                    if (groupMgr == null)
                    {
                        Debug.LogWarning(
                            $"Skipping creation of {type} due to errors creating the group {group.GroupType}. Fix these errors before continuing.");
                        continue;
                    }
                    var groupSys = groupMgr as ComponentSystemGroup;
                    if (groupSys != null)
                    {
                        groupSys.AddSystemToUpdateList(GetOrCreateManagerAndLogException(world, type) as ComponentSystemBase);
                    }
                }
            }

            // Update player loop
            initializationSystemGroup.SortSystemUpdateList();
            simulationSystemGroup.SortSystemUpdateList();
            presentationSystemGroup.SortSystemUpdateList();
            fixedUpdateSystemGroup.SortSystemUpdateList();
            UpdatePlayerLoop(world);
        }

        private static void UpdatePlayerLoop(World world)
        {
            var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            if (world != null)
            {
                // Insert the root-level systems into the appropriate PlayerLoopSystem subsystems:
                for (var i = 0; i < playerLoop.subSystemList.Length; ++i)
                {
                    var subsystemListLength = playerLoop.subSystemList[i].subSystemList.Length;
                    if (playerLoop.subSystemList[i].type == typeof(Update))
                    {
                        var newSubsystemList = new PlayerLoopSystem[subsystemListLength + 1];
                        for (var j = 0; j < subsystemListLength; ++j)
                            newSubsystemList[j] = playerLoop.subSystemList[i].subSystemList[j];
                        InsertManagerIntoSubsystemList<SimulationSystemGroup>().Invoke(null, new object[] { newSubsystemList,
                        subsystemListLength + 0, world.GetOrCreateSystem<SimulationSystemGroup>()});
                        playerLoop.subSystemList[i].subSystemList = newSubsystemList;
                    }
                    else if (playerLoop.subSystemList[i].type == typeof(PreLateUpdate))
                    {
                        var newSubsystemList = new PlayerLoopSystem[subsystemListLength + 1];
                        for (var j = 0; j < subsystemListLength; ++j)
                            newSubsystemList[j] = playerLoop.subSystemList[i].subSystemList[j];
                        InsertManagerIntoSubsystemList<PresentationSystemGroup>().Invoke(null, new object[] { newSubsystemList,
                            subsystemListLength + 0, world.GetOrCreateSystem<PresentationSystemGroup>()});
                        playerLoop.subSystemList[i].subSystemList = newSubsystemList;
                    }
                    else if (playerLoop.subSystemList[i].type == typeof(Initialization))
                    {
                        var newSubsystemList = new PlayerLoopSystem[subsystemListLength + 1];
                        for (var j = 0; j < subsystemListLength; ++j)
                            newSubsystemList[j] = playerLoop.subSystemList[i].subSystemList[j];
                        InsertManagerIntoSubsystemList<InitializationSystemGroup>().Invoke(null, new object[] { newSubsystemList,
                            subsystemListLength + 0, world.GetOrCreateSystem<InitializationSystemGroup>()});
                        playerLoop.subSystemList[i].subSystemList = newSubsystemList;
                    } 
                    else if (playerLoop.subSystemList[i].type == typeof(FixedUpdate))
                    {
                        var newSubsystemList = new PlayerLoopSystem[subsystemListLength + 1];
                        for (var j = 0; j < subsystemListLength; ++j)
                            newSubsystemList[j] = playerLoop.subSystemList[i].subSystemList[j];
                        InsertManagerIntoSubsystemList<FixedUpdateGroup>().Invoke(null, new object[] { newSubsystemList,
                            subsystemListLength + 0, world.GetOrCreateSystem<FixedUpdateGroup>()});
                        playerLoop.subSystemList[i].subSystemList = newSubsystemList;
                    }
                }
            }

            ScriptBehaviourUpdateOrder.SetPlayerLoop(playerLoop);
        }
    }
}
