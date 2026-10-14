# ItemRoomParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/ItemRoomParityTests.cs`. The code keeps a one-line gist tagged `[h:ItemRoomParityTests-N]`.

## [h:ItemRoomParityTests-1]
near: `[TestFixture]`

/// <summary>
/// An item room against the running game: recorded by tools/item-room-parity.js on 2026-10-06.
/// startGame, optionally give items (luck, and what is excluded from the offer), enter the floor's
/// item room from the west. Compared: both streams' totals, the stats read, and every pedestal -
/// the two weapons from the shuffle and the two items from the luck-weighted pool.
/// </summary>
