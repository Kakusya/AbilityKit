namespace AbilityKit.Combat.Projectile
{
    public enum ProjectileExitReason
    {
        Unknown = 0,
        Hit = 1,
        Lifetime = 2,
        MaxDistance = 3,
        Manual = 4,
        ReturnArrived = 5,
        ReturnTargetLost = 6,
        TrackingTargetLost = 7,
    }
}
