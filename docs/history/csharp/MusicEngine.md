# MusicEngine.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Audio/MusicEngine.cs`. The code keeps a one-line gist tagged `[h:MusicEngine-N]`.

## [h:MusicEngine-1]
near: `public sealed class MusicEngine : MonoBehaviour`

/// <summary>
/// Plays the Score: two looping sources started on the same DSP tick so the layers stay locked.
/// The game calls <c>MusicEngine.SetArea(area)</c> and <c>MusicEngine.SetFight(live)</c> every frame;
/// a new area renders on a worker thread and crossfades in, the fight layer fades with the room.
/// </summary>
