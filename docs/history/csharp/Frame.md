# Frame.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Frame.cs`. The code keeps a one-line gist tagged `[h:Frame-N]`.

## [h:Frame-1]
near: `public readonly struct RoomBounds`

/// <summary>
/// One room's rectangle, in world space. A value type with no reference to a room, so a caller can
/// be handed the bounds of a room it is not standing in - drawing a neighbour through an open door,
/// or asking about the room the player just left, which is the case the camera clamp depends on.
/// </summary>

## [h:Frame-2]
near: `public struct RoomFrame`

/// <summary>
/// The wall shorthand, as a mutable holder rather than four loose fields.
///
/// The JavaScript keeps ROOM_LEFT and friends as module-scope `let`s written by exactly one
/// function, `syncRoomBounds`. Porting them as four static fields would keep the discipline and
/// lose the type: a caller could write ROOM_LEFT directly and the "one writer" would be a comment
/// rather than a fact. A struct with read-only properties and one Sync method makes it structural.
/// </summary>

## [h:Frame-3]
near: `public static RoomFrame Sync(RoomBounds bounds)`

/// <summary>
/// The ONE writer, and the function to look at when a wall is in the wrong place.
///
/// <para>
/// It comes before anything that reads the frame, in the JavaScript as well, because the spawn
/// margins, the reward pickup at the centre and the wave are all placed from these numbers. A
/// spawn placed by the previous room's dimensions is the kind of bug that only shows up in the
/// first oversized room and then never again.
/// </para>
/// </summary>

## [h:Frame-4]
near: `public static class Frame`

/// <summary>
/// The camera arithmetic, with no game state in it: bounds in, target in, transform out.
///
/// <para>
/// The JavaScript keeps this in <c>00-balance.js</c> as one `cameraTarget` reading a module-scope
/// player, a module-scope bounds accessor and a module-scope viewport. Every one of those is a
/// hidden input, and the clamp in particular is arithmetic that was wrong once and passed every
/// check that existed - because every room in the game fit on screen, so the branch that was wrong
/// had never run. Taking them as parameters means the branch can be tested here directly.
/// </para>
///
/// <para>
/// The order of the clamp is the whole content: <c>max(lower, min(upper, desired))</c>. Written
/// min-then-max with the bounds transposed it reads plausibly, and it is exactly what the
/// JavaScript was - it pinned the camera to the room's left edge whenever the player was right of
/// centre, so the view never moved at all.
/// </para>
/// </summary>

## [h:Frame-5]
near: `public static RoomBounds RoomBoundsAt(double w, double h)`

/// <summary>
/// Where a room's rectangle starts. Every room anchors on the same origin, so world
/// coordinates are PER-ROOM: (400,355) is the middle of the room you are in and the middle of
/// the room you just left. That is not a simplification, it is the property the whole layout
/// rests on, and it is also why an effect that outlives its room lands in the next one.
/// </summary>
