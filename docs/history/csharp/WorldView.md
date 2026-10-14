# WorldView.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/WorldView.cs`. The code keeps a one-line gist tagged `[h:WorldView-N]`.

## [h:WorldView-1]
near: `public sealed class WorldView : VisualElement`

/// <summary>
/// The room and everything in it, drawn the way src/70-view.js drawRoom draws it: the baked floor
/// and masonry, doors and their locks, the bodies' pixel sprites and walk cycles, shells, rings and
/// the player with the wand. Everything, rings included, is quads from one atlas, batched: a frame is
/// a mesh or two and builds no strings. Reads the run, never writes it.
/// </summary>
