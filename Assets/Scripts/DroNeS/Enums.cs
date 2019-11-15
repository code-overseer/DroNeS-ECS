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
        Destroy, // Flag for destruction TODO
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
}