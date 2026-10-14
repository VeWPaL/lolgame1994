# Synth.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Audio/Synth.cs`. The code keeps a one-line gist tagged `[h:Synth-N]`.

## [h:Synth-1]
near: `public static class Synth`

/// <summary>
/// A small offline synthesizer: just enough of WebAudio to render the JavaScript game's voices
/// (src/45-sound.js) into sample buffers. Oscillators with exponential pitch sweeps, the same
/// deterministic noise buffer, RBJ biquads, and the shared envelope. Pure C#, so it is tested
/// without an audio device; SoundEngine turns the buffers into AudioClips.
/// </summary>
