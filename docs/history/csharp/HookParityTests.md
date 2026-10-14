# HookParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/HookParityTests.cs`. The code keeps a one-line gist tagged `[h:HookParityTests-N]`.

## [h:HookParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The hook against the running game: rows recorded by tools/hook-parity.js on 2026-10-06. Three
/// inert lungers, the player at (300, 400) holding the hook, fireAlt at the aim, then each tick the
/// player phase (which runs the fields) and the projectile phase; "Early" right-clicks again at tick
/// 60 to detonate it in flight. Every 10 ticks, per body: x, y, hp, stun, kvx, kvy, hookStacks,
/// hitFlash; then the live fields and shells. The pull, the hold, the field's drain and suck, the
/// resistance charge.
/// </summary>
