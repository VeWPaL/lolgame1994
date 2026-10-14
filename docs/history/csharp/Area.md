# Area.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Area.cs`. The code keeps a one-line gist tagged `[h:Area-N]`.

## [h:Area-1]
near: `public enum Area { Area1, Area2, Area3, Final }`

/// <summary>
/// The themed blocks of the climb. Floors 1-4 are Area1, 5-8 Area2, 9-12 Area3, and 13+ is the
/// Final boss area. Endless descent stays Final: the ladder is unbounded, and the theme does not
/// cycle back.
/// </summary>

## [h:Area-2]
near: `public static class AreaRules`

/// <summary>
/// The theme lookup. PURE: it reads the floor number and nothing else, spends no RNG, and is
/// therefore safe to call anywhere without touching the draw order. A function of the floor alone
/// is the only shape that keeps a seeded run replayable.
/// </summary>
