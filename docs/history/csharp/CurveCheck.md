# CurveCheck.cs - moved comments

Long comments moved out of `csharp/Depths.Playtest/CurveCheck.cs`. The code keeps a one-line gist tagged `[h:CurveCheck-N]`.

## [h:CurveCheck-1]
near: `public static class CurveCheck`

/// <summary>
/// The `curve` command: band hazards, HP and orderings from a playtest file, checked against
/// <see cref="Curve.Targets"/> (the same file the game's shape lives in). Any miss: exit 2.
/// </summary>

## [h:CurveCheck-2]
near: `public static (string text, List<string> misses) Check(Batch b, Curve.MiddleRule? rule = null)`

/// <summary>
/// Measures every target profile and checks every target: the text is Markdown with one "ok" or
/// "MISS" line per target, led by its rubric number; misses are the failed lines.
/// </summary>
