# Sprites.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Art/Sprites.cs`. The code keeps a one-line gist tagged `[h:Sprites-N]`.

## [h:Sprites-1]
near: `public static class Sprites`

/// <summary>
/// The JS pixel art (src/10-art.js at js-final), rows and palettes verbatim, and its bakers. Every
/// sprite is generated: there are no art assets, in either game. Bakes return <see cref="Img"/>;
/// <see cref="Atlas"/> turns them into one texture.
/// </summary>

## [h:Sprites-2]
near: `public static readonly string[] CrackRows =`

// ---- the fake wall's tell: jagged fissures, dark with a lit lip, across a horizontal wall (rotated for
// a vertical one). The JS drew three 1px lines at 24%, readable only scaled up in a browser: at 1:1 it
// vanished and the secret room read as missing (playtest 2026-10-13) ----
// The fake wall's tell: one jagged split down the whole band, with branches and a dark scorch around it,
// so it reads at a glance against the brick (the JS hairlines were 3 px at 24% and vanished at 1:1).
// '#' the split, '+' its lit lip, ':' the scorch.
