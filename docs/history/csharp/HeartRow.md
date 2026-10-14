# HeartRow.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/HeartRow.cs`. The code keeps a one-line gist tagged `[h:HeartRow-N]`.

## [h:HeartRow-1]
near: `public static class HeartRow`

/// <summary>
/// The health row as data: 1 HP is half a heart. Red hearts (dark when empty), then the
/// regenerating heart, then armour (no empty slots). Where each heart goes is the HUD's business
/// (Hud.HudLayout.HeartTopLeft); the colours are the atlas hearts' (Sprites.HeartPal), so the
/// screenshot check can tell a lit lobe from an unlit one by the pixels the player sees.
/// </summary>
