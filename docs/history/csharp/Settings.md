# Settings.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Settings.cs`. The code keeps a one-line gist tagged `[h:Settings-N]`.

## [h:Settings-1]
near: `public sealed class GameplaySettings`

/// <summary>
/// Gameplay options, saved like the volume. The difficulty curve (Depths.Core <c>Curve.On</c>) is read
/// per floor, so it is applied when a run starts, never mid-run.
/// </summary>

## [h:Settings-2]
near: `public sealed class DisplaySettings`

/// <summary>
/// The frame rate, apart from the game's speed: the simulation always steps at <c>Balance.TickHz</c> and the
/// screen draws between ticks (Depths.Core <c>Interp</c>), so any cap plays the same game, only smoother or
/// cheaper. VSync follows the monitor; a number caps it without VSync; Uncapped draws as fast as it can.
/// </summary>
