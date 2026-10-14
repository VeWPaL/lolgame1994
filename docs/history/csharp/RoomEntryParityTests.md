# RoomEntryParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RoomEntryParityTests.cs`. The code keeps a one-line gist tagged `[h:RoomEntryParityTests-N]`.

## [h:RoomEntryParityTests-1]
near: `[TestFixture]`

/// <summary>
/// A fresh run walking into its first normal room, against the running game: recorded by
/// tools/room-entry-parity.js on 2026-10-06. startGame (the floor generated from the run stream),
/// passDoor through the first start-room door that leads to a normal room, update() until the
/// transition has entered it. Compared: both streams' totals since the reseed, where the player
/// stands, the ready freeze, and every body the wave spawned - kind, position, health, idle and
/// notice timers, pack id and slot, flank and first cooldown.
/// </summary>
