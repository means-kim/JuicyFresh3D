namespace JuicyFresh3D.Parkour
{
    public enum ParkourRootStateType_y
    {
        None = 0,
        Grounded = 1,
        Airborne = 2
    }

    public enum ParkourSubStateType_y
    {
        None = 0,
        Idle = 1,
        SprintStart = 2,
        Sprint = 3,
        SprintStop = 4,
        Jump = 5,
        Fall = 6,
        Slide = 7,
        ObstacleVault = 8,
        WallRun = 9,
        Land = 10,
        Roll = 11,
        SprintWallClimb = 12,
        CrouchedToSprint = 13,
        JumpingToHanging = 14,
        HangingIdle = 15,
        BracedHangHopLeft = 16,
        BracedHangHopRight = 17,
        BracedHangToCrouch = 18,
        JumpFromWall = 19
    }
}
