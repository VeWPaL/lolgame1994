# Rooms.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Rooms.cs`. The code keeps a one-line gist tagged `[h:Rooms-N]`.

## [h:Rooms-1]
near: `public static class Rooms`

/// <summary>
/// Moving through the floor: doors (open only on a cleared room, sealed boss/item doors that a key
/// unlocks by standing at them), the door-aware wall, the transition, and entering a room - which
/// spawns its wave the first time and hands the fight its opening state. src/20-world.js,
/// 40-combat.js clampPlayer/checkDoorTransition, 30-enemies.js enterRoom.
///
/// A room keeps its own bodies and pickups (Room.Enemies/Pickups); the run's lists are the current
/// room's, swapped on entry, so every phase reads run.enemies exactly as before.
/// </summary>

## [h:Rooms-2]
near: `public static void EnterRoom(RunState run, int nx, int ny, Dir from)`

/// <summary>
/// enterRoom: the room becomes current and visited; its wave spawns the first time (a normal
/// room's plan, the Warden at a random point, the secret's hook, the item room's weapons); the
/// player is placed inside the door they came through; a live room hands back the gun and alt
/// cooldowns and tops up the blink; the fade and the ready freeze start.
/// </summary>
