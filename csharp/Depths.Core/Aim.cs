namespace Depths
{
    /// <summary>
    /// The gunner's counterstrafing allowance: how far a reversing player must be before swerving buys
    /// anything, and the spread that buys. Read by <c>TickOrder.StepRanged</c>; the values are pinned
    /// against the running JS in <c>PortedBehaviourTests</c>. (Replaces the unused Intercept.cs solver.)
    /// </summary>
    public static class Aim
    {
        /// <summary>0 inside the room's deadzone, rising to 1 at <see cref="Balance.SwerveFull()"/>.</summary>
        public static double SwerveReach(double dist) => SwerveReach(dist, Balance.Room.Standard);

        /// <summary>The same ramp in a named room: the deadzone is a fraction of the room, not a constant.</summary>
        public static double SwerveReach(double dist, Balance.Room room)
        {
            double dz = Balance.SwerveDeadzone(room), top = Balance.SwerveFull(room);
            return System.Math.Max(0, System.Math.Min(1, (dist - dz) / (top - dz)));
        }

        /// <summary>Total spread in radians: a floor so no two shots match, plus the swerve allowance.</summary>
        public static double GunSpread(double swerve, double dist) =>
            Balance.GunSpreadFloor + Balance.SwerveAim * swerve * SwerveReach(dist);
    }
}
