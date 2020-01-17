namespace DroNeS
{
    public enum Status
    {
        New, // Newly created drone entity
        Waiting, // After reaching a particular waypoint
        Ready, // Ready to move, before moving to first waypoint in  queue"
        RequestingWaypoints, // Finished waypoints queue
        Delivering,
        Returning,
        EnRoute,
        Dead,
    }

    public enum Speed
    {
        Pause,
        Half,
        Normal,
        Fast,
        Faster,
        Ultra,
        Wtf
    }

    public enum CameraTypeValue
    {
        Satellite,
        Main,
        MainLong
    }
    
    public enum SimulationTypeValue
    {
        Emergency,
        Delivery
    }

    public enum CoroutineType
    {
        Fixed,
        Normal,
        Late,
        Timed
    }

    public enum ArchetypeKey
    {
        Drone,
        Propeller,
        Hub
    }
}