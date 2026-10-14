# SoundTests.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Tests/EditMode/SoundTests.cs`. The code keeps a one-line gist tagged `[h:SoundTests-N]`.

## [h:SoundTests-1]
near: `static readonly Dictionary<string, (int peakAtMs, int zc)> Js = new Dictionary<string, (int, int)>`

// Measured from the running JS game, 2026-10-06: Sound.render(name) -> peakAtMs, zeroCrossHz.
// PITCH is compared with the game. PEAK TIMING is not: the JS render goes through the master
// compressor (threshold -14 dB, ratio 5), which clamps each attack and moves the measured peak
// later (hurt 45ms against an envelope peak at 5ms). Here the peak is asserted where the
// envelope puts it. A compressor on the mixer Master is the follow-up if the set sounds spiky.
