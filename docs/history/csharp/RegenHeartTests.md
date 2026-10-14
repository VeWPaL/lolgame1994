# RegenHeartTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RegenHeartTests.cs`. The code keeps a one-line gist tagged `[h:RegenHeartTests-N]`.

## [h:RegenHeartTests-1]
near: `[TestFixture, Category("csharp-only")]`

/// <summary>
/// The regenerating heart (C# only, 2026-10-06): the last of the starting 8 HP is its own layer,
/// between the red hearts and the armour. It takes damage before the red, and refills in a fight
/// after RegenDelay without a hit, 1 HP then 1 HP per RegenStep, and fully when a fought room is
/// cleared. Pickups heal red only, Vigor adds red only, and Balance.JsReference turns it off.
/// </summary>
