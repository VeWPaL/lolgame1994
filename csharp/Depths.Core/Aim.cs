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

        /// <summary>Ticks a shell takes to fly <paramref name="d"/> px: <paramref name="speed"/>, or past <paramref name="splitAt"/> its shards' speed.</summary>
        public static double Flight(double d, double speed, double splitAt = 0, double shardSpeed = 0) =>
            splitAt > 0 && d > splitAt ? splitAt / speed + (d - splitAt) / shardSpeed : d / speed;

        /// <summary>
        /// The angle from (mx, my) that meets a target at (px, py) moving (vx, vy) a tick, for a shell leaving after
        /// <paramref name="delay"/> ticks: the first time the shell's flight covers the gap, found by stepping then
        /// bisecting; a target faster than the shell is aimed where the shell comes closest to it within
        /// <see cref="Balance.AimHorizon"/>. The JS's fixed-point loop diverges there: a Splitter shot 2.4 rad wide.
        /// </summary>
        public static double Intercept(double mx, double my, double px, double py, double vx, double vy, double delay,
                                       double speed, double splitAt = 0, double shardSpeed = 0)
        {
            double horizon = Balance.AimHorizon, step = System.Math.Max(1, horizon / 48), lo = 0, hi = -1;
            double Gap(double t)
            {
                double ax = px + vx * (delay + t) - mx, ay = py + vy * (delay + t) - my;
                return Flight(System.Math.Sqrt(ax * ax + ay * ay), speed, splitAt, shardSpeed) - t;
            }
            double at = 0, least = Gap(0);
            if (least <= 0) hi = 0;
            for (double t = step; hi < 0 && t <= horizon; t += step)
            {
                double g = Gap(t);
                if (g <= 0) { lo = t - step; hi = t; }
                else if (g < least) { least = g; at = t; }   // no catch yet: remember where it comes closest
            }
            if (hi >= 0)
            {
                for (int k = 0; k < 10 && hi > 0; k++) { double mid = (lo + hi) / 2; if (Gap(mid) <= 0) hi = mid; else lo = mid; }
                at = hi;
            }
            return System.Math.Atan2(py + vy * (delay + at) - my, px + vx * (delay + at) - mx);
        }
    }
}
