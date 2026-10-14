# Compressor.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Audio/Compressor.cs`. The code keeps a one-line gist tagged `[h:Compressor-N]`.

## [h:Compressor-1]
near: `public sealed class Compressor`

/// <summary>
/// The JS game's master compressor (src/45-sound.js: threshold -14 dB, knee 22, ratio 5, attack 4 ms,
/// release 180 ms) as plain DSP: a soft-knee gain computer in dB, a peak detector smoothed by attack
/// and release, channels linked, and Chrome's makeup gain so the set sits at the loudness it had in
/// the browser. Pure, so it is tested without an audio device; MasterBus runs it on the final mix.
/// </summary>
