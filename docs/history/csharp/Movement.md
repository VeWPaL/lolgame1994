# Movement.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Movement.cs`. The code keeps a one-line gist tagged `[h:Movement-N]`.

## [h:Movement-1]
near: `public static class Movement`

/// <summary>
/// How bodies move: the lunger's four-state attack (src/30-enemies.js stepLunge, solveIntercept),
/// the idle wander, the knock, the wall clamp and the separation pass (src/40-combat.js). Every
/// random choice here is on the JITTER stream, as in the game. LungerParityTests pins it against
/// trajectories recorded from the running game by tools/lunger-parity.js.
/// </summary>

## [h:Movement-2]
near: `public static (double dx, double dy, double dist) LungeIntercept(Player p, double gx, double gy)`

/// <summary>
/// Where to lunge: the player's displacement belief (falling back to the trend when the belief
/// is zero), scaled by how settled they look (1 - swerve), solved over LUNGE_ITER passes.
/// </summary>

## [h:Movement-3]
near: `public static void Separate(RunState run, List<Enemy> list)`

/// <summary>
/// Overlapping bodies are pushed apart, pairs in index order. The game uses a 96px grid from 8
/// bodies up and sorts candidates back into index order, so it tests the same overlapping pairs
/// in the same order - except a pair that only comes into contact DURING the pass and sits in
/// non-neighbouring cells, which the grid skips and this loop does not. Port the grid (without
/// its string keys and per-tick allocations) before claiming parity for large rooms.
/// </summary>

## [h:Movement-4]
near: `public static bool TryLungerDodge(RunState run, Enemy e)`

/// <summary>
/// The Lunger's dodge (C# only, Balance.Tuned; owner 2026-10-13): a short dash off a shot it can
/// see coming - sideways off a bolt's line, straight away from where a blast will burst - on the
/// lunge's own speed. Not from inside the Scatter's reach (close enough still kills), not out of
/// its own wind-up or lunge, and not sooner than it could react; a body still in its notice delay
/// is never exempt, so a Lunger spawned beside you is startled rather than a free kill.
/// </summary>

## [h:Movement-5]
near: `public static double DashShare(double u, double rampIn, double rampOut)`

/// <summary>
/// The share of a dash covered by time u (0..1): speed rises over the first <paramref name="rampIn"/> of it
/// and falls over the last <paramref name="rampOut"/> along smoothstep curves, flat between. 0 at 0, 1 at 1,
/// never decreasing, so a dash of N ticks lands exactly where the flat one did.
/// </summary>

## [h:Movement-6]
near: `public static double? ClearShot(RunState run, Enemy e, double want)`

/// <summary>
/// clearShot: the first angle, sweeping out from the wanted one, whose line to the player passes
/// no body of ours (its own guards excepted) - or null, and the shooter waits.
/// </summary>

## [h:Movement-7]
near: `public static bool LineClear(RunState run, Enemy e, double a, double reach, double r)`

/// <summary>
/// A line from <paramref name="e"/> at angle <paramref name="a"/> that passes no body of ours within
/// <paramref name="reach"/> px (a shell of radius <paramref name="r"/> would graze it). C# only: the volleys
/// that are not one aimed shell - the Sprayer's cone, the egg's ring - keep their shells off their own side.
/// </summary>
