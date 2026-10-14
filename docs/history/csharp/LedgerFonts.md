# LedgerFonts.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Hud/LedgerFonts.cs`. The code keeps a one-line gist tagged `[h:LedgerFonts-N]`.

## [h:LedgerFonts-1]
near: `public static class LedgerFonts`

/// <summary>
/// The ledger's hand: a book face read from the OS at runtime (no font files in the repo). Georgia,
/// then the other Windows book faces; if none can be loaded the label keeps Unity's default, so a
/// missing font costs the look, never the text.
/// </summary>
