# HudLayout.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Hud/HudLayout.cs`. The code keeps a one-line gist tagged `[h:HudLayout-N]`.

## [h:HudLayout-1]
near: `public static class HudLayout`

/// <summary>
/// Every place the ledger HUD draws, in panel units at the 1280x720 reference. One table, read by
/// the HUD, the overlays, the layout tests and the screenshot pixel check, so none of them can drift.
/// Two ledger boards stand the full height of the screen beside the room: the body on the left, the
/// map and the kit on the right. The band above the room is the home of the floor heading and the
/// Warden's notice; the band below holds the Warden's bar.
/// </summary>
