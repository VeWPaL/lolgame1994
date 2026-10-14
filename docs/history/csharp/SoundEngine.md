# SoundEngine.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Audio/SoundEngine.cs`. The code keeps a one-line gist tagged `[h:SoundEngine-N]`.

## [h:SoundEngine-1]
near: `public sealed class VoicePool`

/// <summary>
/// Which source a new sound gets: a free one, else the one that will finish soonest (the JS pool
/// trims oldest-first for the same reason - a cap that refuses new sounds drops the important ones).
/// Pure, so the bound is tested without an audio device.
/// </summary>

## [h:SoundEngine-2]
near: `public sealed class SoundEngine : MonoBehaviour`

/// <summary>
/// The one call the game makes: <c>SoundEngine.Play("hit", pan)</c>. Voices are rendered once at
/// start by <see cref="Synth"/> (or loaded from <c>Resources/Sounds/&lt;name&gt;</c> when a sample
/// exists) and played through a fixed pool of AudioSources on the mixer's Sfx group. Pitch jitter is
/// per play, from a local stream, so sound never touches the game's RNG. It also owns the game's one
/// AudioListener (with the master compressor) and the music, and lives across scenes.
/// </summary>
