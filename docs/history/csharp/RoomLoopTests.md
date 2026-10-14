# RoomLoopTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RoomLoopTests.cs`. The code keeps a one-line gist tagged `[h:RoomLoopTests-N]`.

## [h:RoomLoopTests-1]
near: `[TestFixture]`

/// <summary>
/// The room loop as a player meets it, through Update alone: walk out of the start room, arrive in
/// a live room whose doors the fight has shut, clear it, and find the doors open again. Not a
/// parity table (RoomEntryParityTests pins the numbers); this pins that the pieces are connected.
/// </summary>
