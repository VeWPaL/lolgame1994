# LedgerHud.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Hud/LedgerHud.cs`. The code keeps a one-line gist tagged `[h:LedgerHud-N]`.

## [h:LedgerHud-1]
near: `public sealed class LedgerHud : VisualElement`

/// <summary>
/// The in-game HUD: two ledger boards standing beside the room. The left page is the body (one row
/// of hearts, keys, blink, momentum, depth, slain, the seed tag), the right the map and the kit; the
/// Warden's bar sits under the room. Everything on the pages is in one 5x7 pixel hand
/// (<see cref="LedgerType"/>), the room's own scale. Retained: the boards are baked once, the seed tag
/// when the seed changes, and the ink layer repaints only when a drawn value moves (one 64-bit
/// signature per frame, compared). <see cref="Refresh"/> allocates nothing once warm.
/// </summary>

## [h:LedgerHud-2]
near: `public static bool IsLow(Player p) => p.hp > 0 && Math.Round(p.hp) + Math.Max(0, Math.Round(p.regenHeart)) + Math.Max(0,`

/// <summary>
/// Low health: the last heart's worth of everything that stands between the player and death.
/// The regen heart and armour drain first, so they count; at 2 points or fewer the page bleeds.
/// </summary>
