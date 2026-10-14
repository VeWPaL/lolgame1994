# QuadBatch.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Art/QuadBatch.cs`. The code keeps a one-line gist tagged `[h:QuadBatch-N]`.

## [h:QuadBatch-1]
near: `public sealed class QuadBatch`

/// <summary>
/// Sprite quads for a UI Toolkit element, collected per texture and written as one temp mesh per
/// run of the same texture, in draw order. Buffers are reused: drawing a frame allocates nothing
/// on the managed heap. Call <see cref="Flush"/> before any Painter2D stroke so the order holds.
/// </summary>
