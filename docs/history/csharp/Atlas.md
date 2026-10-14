# Atlas.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Art/Atlas.cs`. The code keeps a one-line gist tagged `[h:Atlas-N]`.

## [h:Atlas-1]
near: `public sealed class Atlas`

/// <summary>
/// Packs baked images into one point-filtered texture (shelf packing, tallest first, 2px gutters),
/// so the whole room draws from a single texture and batches. Built once; nothing is added later.
/// </summary>
