# BossParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/BossParityTests.cs`. The code keeps a one-line gist tagged `[h:BossParityTests-N]`.

## [h:BossParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The Warden against the running game: rows recorded by tools/boss-parity.js on 2026-10-06 from
/// the game's own tickBodies() - a phase-one fight (volleys and sweeps) and one started below a third
/// of its health (phase three: the wall of statues and the heavier bag). Every 25 ticks: position,
/// move, moveT, castT, volleyLeft, volleyT, bossCd, phase, the body count, the shells fired and the
/// player's hp; both streams' draws exact (each statue is a real spawn, two jitter draws apiece).
/// </summary>
