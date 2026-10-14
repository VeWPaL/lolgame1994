# BlinkParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/BlinkParityTests.cs`. The code keeps a one-line gist tagged `[h:BlinkParityTests-N]`.

## [h:BlinkParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The blink against the running game: rows recorded by tools/blink-parity.js on 2026-10-06. Seed
/// 4242, one inert body in the room (so the charge refills at the in-fight rate), the player at the
/// centre holding a direction: 20 ticks of the player phase, the blink, then 800 more. Each row:
/// tick, x, y, vx, vy, lagX, lagY, iframes, blinkGrace, boost, blinkCharges, blinkRegen.
/// </summary>
