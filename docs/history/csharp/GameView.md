# GameView.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/GameView.cs`. The code keeps a one-line gist tagged `[h:GameView-N]`.

## [h:GameView-1]
near: `[RequireComponent(typeof(UIDocument))]`

/// <summary>
/// The game, on Depths.Core at its tick rate (Balance.TickHz, 60 Hz): a seeded run of rooms, doors, keys, waves,
/// the Warden and the way down (TickOrder.Update), all ported with parity. F1 swaps to the sandbox
/// (one room of every enemy), B to a Warden arena. This class only reads input and draws.
/// Esc: menu. 1-4: guns. R: a new run (or room).
/// </summary>

## [h:GameView-2]
near: `double _lastFrameAt = -1;`

// This frame's seconds, from the wall clock rather than Time.unscaledDeltaTime, so the deltas sum to
// real time by construction. Once, with three other Unity editors running, the player measured
// 61.65 Hz; the cause is not identified (CPU load alone matches the wall clock), and the check now
// logs Unity's own delta sum next to the wall clock so a repeat says which side drifted.
