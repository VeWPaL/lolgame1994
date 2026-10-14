# InterpTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/InterpTests.cs`. The code keeps a one-line gist tagged `[h:InterpTests-N]`.

## [h:InterpTests-1]
near: `double lunge = Balance.LungeSpeed * Math.Max(1 / Movement.DashArea(Balance.LungeRampIn, Balance.LungeRampOut), Balance.D`

// the cutoff must sit above every real per-tick step at the game's 60 Hz (this suite ticks faster), with
// room for a doubled shot speed from items, or fast things stop blending and judder again
// the eased dashes peak above lunge speed, and a deep floor speeds every body up
