# BlastParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/BlastParityTests.cs`. The code keeps a one-line gist tagged `[h:BlastParityTests-N]`.

## [h:BlastParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The right-click blast against the running game: rows recorded by tools/blast-parity.js on
/// 2026-10-06. Seed 4242, the player at (300, 400), the RNG reseeded just before fireAlt(), then
/// the projectile phase alone until the blast is gone. Compared: where it was aimed (clamped to
/// the room), the tick it went off, each body's hp, knock, stun, slow and flash (or death), the
/// kills, and both streams' draw counts (a kill rolls loot on the run stream).
/// </summary>
