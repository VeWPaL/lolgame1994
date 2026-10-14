# Weapons.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Weapons.cs`. The code keeps a one-line gist tagged `[h:Weapons-N]`.

## [h:Weapons-1]
near: `public static class Weapons`

/// <summary>
/// The four guns and the act of firing one, src/40-combat.js fireWeapon. Values read from the
/// running game 2026-10-06, held in seconds and px/s; at 210 Hz they convert back to its numbers.
/// </summary>

## [h:Weapons-2]
near: `public static void Fire(RunState run, double aimX, double aimY, double strength = 3, double precision = 0)`

/// <summary>
/// Fires the held weapon at a world point. Strength is added once per SHOT and split across the
/// pellets; precision narrows a weapon whose cone is a stat. Both are parameters until the stats
/// system is ported (base strength is the character's 3). Spread and buckshot draw from the
/// JITTER stream, never the run stream, so firing cannot move the dungeon.
/// </summary>

## [h:Weapons-3]
near: `public static void FireAlt(RunState run, double aimX, double aimY)`

/// <summary>
/// fireAlt for the BLAST: a slow bolt to the cursor (clamped to the room), on its own cooldown.
/// The hook (a secret-room pickup) is not ported and throws.
/// </summary>
