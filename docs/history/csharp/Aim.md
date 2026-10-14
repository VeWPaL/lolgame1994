# Aim.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Aim.cs`. The code keeps a one-line gist tagged `[h:Aim-N]`.

## [h:Aim-1]
near: `public static class Aim`

/// <summary>
/// The gunner's counterstrafing allowance: how far a reversing player must be before swerving buys
/// anything, and the spread that buys. Read by <c>TickOrder.StepRanged</c>; the values are pinned
/// against the running JS in <c>PortedBehaviourTests</c>. (Replaces the unused Intercept.cs solver.)
/// </summary>

## [h:Aim-2]
near: `public static double Intercept(double mx, double my, double px, double py, double vx, double vy, double delay,`

/// <summary>
/// The angle from (mx, my) that meets a target at (px, py) moving (vx, vy) a tick, for a shell leaving after
/// <paramref name="delay"/> ticks: the first time the shell's flight covers the gap, found by stepping then
/// bisecting; a target faster than the shell is aimed where the shell comes closest to it within
/// <see cref="Balance.AimHorizon"/>. The JS's fixed-point loop diverges there: a Splitter shot 2.4 rad wide.
/// </summary>
