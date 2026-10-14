# Materials.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Art/Materials.cs`. The code keeps a one-line gist tagged `[h:Materials-N]`.

## [h:Materials-1]
near: `public static class Materials`

/// <summary>
/// The carved-wood and paper materials and the room's stone (src/10-art.js, src/70-view.js), as
/// bakes. Noise comes from an art stream the view owns, never the run's: a draw must not move the
/// simulation (CONVENTIONS, one-way data flow).
/// </summary>

## [h:Materials-2]
near: `public sealed class FloorLayers`

/// <summary>
/// The floor's type-independent part, for <see cref="Floor(FloorLayers, RoomKind, AreaPalette)"/>. Every layer
/// of <see cref="Floor(int, int, RoomKind, AreaPalette, Img)"/> composites over an opaque base, so a floor is
/// A(x) + B(x) * C(type): A and B are made once per floor (one vignette pass), and each room type is then one
/// multiply-add a pixel instead of four blend passes (the five types cost 0.6 s at a run's start, measured).
/// </summary>
