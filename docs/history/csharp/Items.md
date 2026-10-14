# Items.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Items.cs`. The code keeps a one-line gist tagged `[h:Items-N]`.

## [h:Items-1]
near: `public sealed class Stats`

/// <summary>
/// The stats a build is made of (src/06-stats.js): value = min(cap, base + flat + earned). The base
/// is the character (Wyrd); flat comes only from items, by rebuilding, never by subtracting.
/// </summary>

## [h:Items-2]
near: `public static class Items`

/// <summary>
/// The item system (src/25-items.js): the roster in definition order, the rarity roll (luck
/// weights the rare end), the pool (one run draw per pick), give/remove by rebuilding, the one
/// active slot and Q. The hooks are the game's: heal, pull the loot, reveal the room; a companion
/// that is written down but not built refuses, as it does there.
/// </summary>

## [h:Items-3]
near: `public static void CompassOpens(RunState run)`

/// <summary>
/// Brass Compass (placeholder, C# only): carried into the room that hides the floor's fake
/// wall, the wall opens by itself. Called on entering a room and on every rebuild.
/// </summary>
