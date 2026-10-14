# Policy.cs - moved comments

Long comments moved out of `csharp/Depths.Playtest/Policy.cs`. The code keeps a one-line gist tagged `[h:Policy-N]`.

## [h:Policy-1]
near: `internal Dir? Route(RunState run)`

/// <summary>
/// BFS over the floor through doors that exist and are open or that the bot holds the key for.
/// Goals in a careful first-timer's order: unvisited rooms, the item room with a key, the boss.
/// </summary>
