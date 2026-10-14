# LedgerType.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Hud/LedgerType.cs`. The code keeps a one-line gist tagged `[h:LedgerType-N]`.

## [h:LedgerType-1]
near: `public static class LedgerType`

/// <summary>
/// The HUD's one hand: a 5x7 pixel face, so every word on the lecterns is drawn in the same pixels
/// as the room beside it (the 3x5 PixelFont stays for item marks). Lower case is drawn as upper case.
/// Text is laid out without building strings: a word wrap walks the source string in place.
/// </summary>

## [h:LedgerType-2]
near: `public static (int end, int next) Wrap(string s, int from, int maxW, int scale)`

/// <summary>
/// Greedy word wrap of s[from..] into maxW pixels: the end (exclusive) of the first line and the
/// start of the next. No strings are made, so the ink layer can wrap every frame for free.
/// </summary>
