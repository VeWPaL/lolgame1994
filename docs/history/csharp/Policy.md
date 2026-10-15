# Policy.cs - moved comments

Long comments moved out of `csharp/Depths.Playtest/Policy.cs`. The code keeps a one-line gist tagged `[h:Policy-N]`.

## [h:Policy-1]
near: `internal Dir? Route(RunState run)`

/// <summary>
/// BFS over the floor through doors that exist and are open or that the bot holds the key for.
/// Goals in a careful first-timer's order: unvisited rooms, the item room with a key, the boss.
/// </summary>

## [h:Policy-2]
near: `internal bool Marks(RunState run, int t, ref bool blink)`

/// <summary>The floor's marks: a Mortar shell's landing and a leaping Burrower's, each a circle that hurts once, as it
/// lands. Once the bot has had its reaction time on a mark that holds its hitbox (or will, on its present course),
/// it runs straight out of it, and blinks if running would not clear it in time. True if it moved for one.</summary>
