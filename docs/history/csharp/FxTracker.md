# FxTracker.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Art/FxTracker.cs`. The code keeps a one-line gist tagged `[h:FxTracker-N]`.

## [h:FxTracker-1]
near: `public sealed class FxTracker`

/// <summary>
/// The JS keeps its blink trail, lunge puffs and burst rings as lists the simulation pushes to.
/// Depths.Core has no view lists (it is the simulation only), so the view derives the same marks
/// from what changed between frames: a spent blink charge, a lunger's state, an alt shell that is
/// gone. Fixed pools: a frame allocates nothing once warm.
/// </summary>
