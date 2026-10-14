# FireParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/FireParityTests.cs`. The code keeps a one-line gist tagged `[h:FireParityTests-N]`.

## [h:FireParityTests-1]
near: `[TestFixture]`

/// <summary>
/// fireWeapon against the running game: every row was produced by tools/fire-parity.js on
/// 2026-10-06. The RNG is reseeded immediately before the shot on both sides, the player stands at
/// (400, 330) with shootSlow 0.5, and the aim is a world point 200px away at three angles. The
/// signature is every shell's position, velocity, damage, radius, muzzle and direction, then its
/// pierce and fNear - compared numerically to 2e-6, so the two languages' rounding cannot differ.
/// </summary>
