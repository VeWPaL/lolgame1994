# LedgerArt.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Hud/LedgerArt.cs`. The code keeps a one-line gist tagged `[h:LedgerArt-N]`.

## [h:LedgerArt-1]
near: `public static class LedgerArt`

/// <summary>
/// The ledger's bakes, built from the JS materials (woodPlate, drawInset, paperTex): the two
/// lecterns with their pages ruled and headed, the overlay sheets, the menu's desk and open book,
/// key caps and knobs. Everything is an <see cref="Img"/>, baked once and turned into a texture by
/// the caller; nothing here runs per frame.
/// </summary>

## [h:LedgerArt-2]
near: `public static Img Board(bool left)`

/// <summary>
/// One board: a plank as tall as the screen allows, its inset and aged page, a clip at the top,
/// and the page's fixed parts (labels, rules, wells, the seal, the slots and their name
/// plates). Everything that changes is drawn over it by the ink layer. Origin: HudLayout.Board.
/// </summary>

## [h:LedgerArt-3]
near: `public static Img SeedTag(int w, int h, string text)`

/// <summary>
/// The seed tag (Materials.SeedTag's luggage tag: the clipped corner, the punched hole), with the seed
/// inlaid in pale wood in the HUD's hand so it reads at a glance; the tag is the label, so no word on it.
/// </summary>

## [h:LedgerArt-4]
near: `public static Img Book(int w, int h)`

/// <summary>
/// An open ledger seen from above: wooden boards with brass corners, two pages with their stacked
/// edges, the gutter's shadow and a red margin on each page. The left page is <paramref name="w"/>/2.
/// </summary>
