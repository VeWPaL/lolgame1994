# ExitPickupTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/ExitPickupTests.cs`. The code keeps a one-line gist tagged `[h:ExitPickupTests-N]`.

## [h:ExitPickupTests-1]
near: `[TestFixture, Category("csharp-only")]`

/// <summary>
/// Regression (found by the C# playtest bot, 2026-10-06): touching the exit while other pickups
/// still lay in the room threw in TickRoom, because Descend empties run.pickups mid-loop.
/// </summary>
