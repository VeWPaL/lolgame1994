# RangedParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RangedParityTests.cs`. The code keeps a one-line gist tagged `[h:RangedParityTests-N]`.

## [h:RangedParityTests-1]
near: `[TestFixture]`

/// <summary>
/// Shooters and gunners against the running game. Every row was recorded by tools/ranged-parity.js
/// on 2026-10-06 from the game's own tickBodies(): seed 4242, the player held still with a fixed
/// velocity and swerve, Rnd reseeded before the first tick, every 10th tick compared to 1e-6
/// (position, castT, castReady, shootCd, castAim, dodgeCd, knock velocity; then the shells fired
/// and the player's hp), jitter draws exact. Covers the standoff walk, the cast, the intercept lead
/// against a moving, swerving player, the aim sweep around a lunger in the line, and the gunner dodge.
/// </summary>
