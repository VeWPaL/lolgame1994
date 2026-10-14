# GameViewTests.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Tests/EditMode/GameViewTests.cs`. The code keeps a one-line gist tagged `[h:GameViewTests-N]`.

## [h:GameViewTests-1]
near: `[Test]`

// a 0-tick frame followed by a 2-tick one is a visible stutter. Pins Unity's float frame times
// for whole divisors of 60; a display slightly off 60 Hz still double-steps now and then.
// the frame draws between the last two ticks by this fraction (Interp): it must be what the clock has not
// yet spent, so a 144 Hz frame 2/5 of the way into a tick draws 2/5 of the way along
