# LungerParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/LungerParityTests.cs`. The code keeps a one-line gist tagged `[h:LungerParityTests-N]`.

## [h:LungerParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The lunger against the running game. Every row was recorded by tools/lunger-parity.js on
/// 2026-10-06 from the game's own tickBodies(): seed 4242, the player held still, Rnd reseeded
/// before the first tick, every 10th tick compared (position, state, lungeT, curSpeed, knock
/// velocity, and the player's hp) to 1e-6, and the jitter draws counted exactly.
/// </summary>
