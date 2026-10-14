# ProjectilePhaseParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/ProjectilePhaseParityTests.cs`. The code keeps a one-line gist tagged `[h:ProjectilePhaseParityTests-N]`.

## [h:ProjectilePhaseParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The projectile phase against the running game. Every expectation below was produced by
/// <c>tools/proj-parity.js</c> on 2026-10-06 (after the loop's `break` was fixed to `continue`),
/// with the same fixture: seed 4242, a cleared standard room, the player parked at (700, 500), the
/// bodies placed by hand, one tick. The case names are the probe's.
/// </summary>
