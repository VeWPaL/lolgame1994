# HudOverlays.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Hud/HudOverlays.cs`. The code keeps a one-line gist tagged `[h:HudOverlays-N]`.

## [h:HudOverlays-1]
near: `public sealed class HudOverlays : VisualElement`

/// <summary>
/// The pages laid over the game in the ledger's materials: the pause note, the run's account (the
/// JS summary sheet), the floor heading and the Warden's notice. Each is built once and shown or
/// hidden; text is written when a page opens, never per frame. The heading and the notice sit in
/// the band above the room, so a descent never covers the fight.
/// </summary>
