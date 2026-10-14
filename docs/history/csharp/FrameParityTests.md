# FrameParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/FrameParityTests.cs`. The code keeps a one-line gist tagged `[h:FrameParityTests-N]`.

## [h:FrameParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The room frame and the camera, against numbers read out of the running JavaScript rather than
/// transcribed from its source expressions.
///
/// <para>
/// The one that matters is <see cref="TheOversizedRoomClampMovesTheView"/>. The clamp in
/// <c>cameraTarget</c> was wrong in the JavaScript for as long as the camera existed - written
/// <c>Math.min(b.l, ...)</c> with the player's offset inside, which pinned the view to the room's
/// left edge whenever the player was right of centre. It passed every check that existed because
/// every room in the game fits on screen, so the branch never ran: the property that made the
/// camera safe to introduce was the same property that hid the bug in it.
///
/// <para>
/// So this file deliberately tests a room that does NOT fit. That is the whole reason the camera
/// is worth a port and the whole reason a parity table of standard-room numbers would be worth
/// very little.
/// </para>
/// </summary>

## [h:FrameParityTests-2]
near: `var big = Frame.RoomBoundsAt(1400, 900);`

// The exact shape of the bug: min-then-max with the bounds transposed reads plausibly
// and pins the camera to the room's left edge, so the view never moves at all. This is
// here so that anyone "simplifying" the clamp trips a test rather than a playtest.

## [h:FrameParityTests-3]
near: `var big = Frame.RoomBoundsAt(1400, 900);`

// the bug this whole file's origin is about: the cursor is SCREEN space and everything
// else is world, so mixing them is wrong by exactly the camera offset - measured at up to
// 21.28 degrees of aim error, changing sign across the frame.
