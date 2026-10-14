# TickClock.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/TickClock.cs`. The code keeps a one-line gist tagged `[h:TickClock-N]`.

## [h:TickClock-1]
near: `public sealed class TickClock`

/// <summary>
/// Turns frame time into simulation ticks at <c>Balance.TickHz</c>: a fixed-step accumulator, so the
/// game runs at the same speed at any frame rate. Kept out of GameView so it can be tested.
/// </summary>
