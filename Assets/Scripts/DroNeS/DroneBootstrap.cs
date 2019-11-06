using DroNeS.Components;
using DroNeS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;
using DroneStatus = DroNeS.Components.DroneStatus;

namespace DroNeS
{
    public static class DroneBootstrap
    {
        private static EntityManager _manager;
        private static DroneBuilderSystem _builder;
//        private static WaypointUpdateSystem _updater;
//        private static DroneMovementSystem _movement;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
//            _manager = World.Active.EntityManager;

        }


    }
}