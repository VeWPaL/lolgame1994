# Bindings.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Bindings.cs`. The code keeps a one-line gist tagged `[h:Bindings-N]`.

## [h:Bindings-1]
near: `public sealed class Bindings`

/// <summary>
/// The player's key binds: a layout preset plus per-slot overrides, saved as the Input System's
/// own override JSON so a rebind survives a restart and a new build reads it unchanged.
/// </summary>

## [h:Bindings-2]
near: `public static int FindIndex(InputAction action, string slot)`

/// <summary>
/// The binding a slot names: "Move/up" is the "up" part of the FIRST composite (WASD, not the
/// arrows, which stay fixed); "Blink" is the first keyboard binding of the action.
/// </summary>
