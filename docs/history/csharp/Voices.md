# Voices.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Audio/Voices.cs`. The code keeps a one-line gist tagged `[h:Voices-N]`.

## [h:Voices-1]
near: `public static class Voices`

/// <summary>
/// The JavaScript game's twelve voices (src/45-sound.js VOICES), as data. Same frequencies,
/// sweeps, filters and envelopes; the tests compare each render with the JS offline render.
/// WebAudio's lowpass Q is in dB (default 1 dB = 1.122 linear); its bandpass Q is linear.
/// </summary>
