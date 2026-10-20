namespace JuicyFresh3D.Parkour
{
    public enum ParkourRootStateType_y
    {
        None = 0,
        Grounded = 1,
        Airborne = 2,
        WallRun = 3,
        Roll = 4
    }

    public enum ParkourSubStateType_y
    {
        None = 0,
        Idle = 1,
        SprintStart = 2,
        Sprint = 3,
        SprintStop = 4,
        Jump = 5,
        Fall = 6
    }
}
