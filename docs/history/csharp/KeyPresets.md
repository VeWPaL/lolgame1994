# KeyPresets.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/KeyPresets.cs`. The code keeps a one-line gist tagged `[h:KeyPresets-N]`.

## [h:KeyPresets-1]
near: `public static class KeyPresets`

/// <summary>
/// Layout presets for the keys whose letter differs between layouts.
///
/// The Input System binds <c>&lt;Keyboard&gt;/w</c> to a key's POSITION, so the default (Auto)
/// already puts movement under the left hand on every layout: ZQSD on AZERTY, ,AOE on Dvorak.
/// The letter presets bind by the character a key types instead (<c>&lt;Keyboard&gt;/#(Z)</c>),
/// for players who would rather press the letters they read. Dvorak binds the characters Dvorak
/// prints at those positions (, A O E): on a Dvorak system that is the same keys as Auto, and it
/// was once the positional preset itself, which made choosing it change nothing (playtest 2026-10-13).
/// </summary>
