# RateTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RateTests.cs`. The code keeps a one-line gist tagged `[h:RateTests-N]`.

## [h:RateTests-1]
near: `[TestFixture, Category("csharp-only")]`

/// <summary>
/// The sim at 60 Hz plays the game the 210 Hz one does: each test runs one behaviour at both rates
/// and compares it in seconds or pixels. Every tolerance is a named discrete-time error (a wait
/// rounded to whole ticks, a step of travel, the knock cut-off), never a tuned margin.
/// </summary>

## [h:RateTests-2]
near: `for (int w = 0; w < Weapons.All.Length; w++)`

// held fire: at 210 the period is the cooldown rounded up to whole ticks (the JS); off it the
// overrun is carried, so over ten seconds the mean period is the cooldown in seconds to within
// one tick spread over the shots - tight enough to fail the old rounding (Bolt +2%, Beam +7%)

## [h:RateTests-3]
near: `foreach (var kind in new[] { BodyKind.Gunner, BodyKind.Shooter })`

// the same draws per cycle at both rates. Off the JS rate the cooldown's overrun and the cast's late
// tick are carried, so the mean cycle is the JS one up to the JS side's own rounding (under a JS tick);
// without the carry each cycle at 60 Hz runs about 1.5 ticks (25 ms) long
