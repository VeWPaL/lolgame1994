# Volume.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Volume.cs`. The code keeps a one-line gist tagged `[h:Volume-N]`.

## [h:Volume-1]
near: `public sealed class VolumeSettings`

/// <summary>
/// The three volume sliders. A slider is linear 0..1 because that is what a player expects to
/// drag; the mixer works in decibels, so the conversion lives here and is the only one.
/// </summary>
