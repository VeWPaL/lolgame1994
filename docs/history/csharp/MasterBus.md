# MasterBus.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Audio/MasterBus.cs`. The code keeps a one-line gist tagged `[h:MasterBus-N]`.

## [h:MasterBus-1]
near: `[RequireComponent(typeof(AudioListener))]`

/// <summary>
/// The end of the chain: sits next to the game's one AudioListener, so OnAudioFilterRead sees the
/// whole mix (after the mixer) and runs the master compressor on it, as the JS master does.
/// </summary>
