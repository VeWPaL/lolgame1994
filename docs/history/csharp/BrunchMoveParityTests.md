# BrunchMoveParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/BrunchMoveParityTests.cs`. The code keeps a one-line gist tagged `[h:BrunchMoveParityTests-N]`.

## [h:BrunchMoveParityTests-1]
near: `[TestFixture]`

/// <summary>
/// Brunch packs moving, against the running game: rows recorded by tools/brunch-move-parity.js on
/// 2026-10-06 from the game's own tickBodies(). A pack walling a shooter, a pack with nothing to
/// guard chasing and spending itself on the player, and the B and A+ guard variants. Every 10th
/// tick, per body in spawn order: x, y, vx, vy, curSpeed, hp, or dead; then the player's hp.
/// </summary>
