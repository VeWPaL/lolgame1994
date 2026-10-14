# HeartTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/HeartTests.cs`. The code keeps a one-line gist tagged `[h:HeartTests-N]`.

## [h:HeartTests-1]
near: `[TestFixture, Category("csharp-only")]`

/// <summary>
/// Whole-number HP (C# only, 2026-10-06): 1 HP is half a heart; every enemy hit
/// is a whole number; armour takes 0.6x a normal enemy's hit, rounded down but at least 1, and
/// the Warden's at full weight; pickups come whole (2) and half (1). The JS has none of this, so
/// these are assertions, not parity rows. The player here is a bare one (8 red, no regenerating
/// heart), so each rule is seen alone; RegenHeartTests covers the real starting body.
/// </summary>
