using System.Collections;
using System.Collections.Generic;
using DroNeS.Components;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class WaypointManager : MonoBehaviour
{
    private HashSet<Queue<Position>> _waypoints;
    private EntityManager _manager;

    private EntityManager Manager => _manager ?? (_manager = World.Active.GetOrCreateManager<EntityManager>());

    private void Start()
    {
        
    }

}
