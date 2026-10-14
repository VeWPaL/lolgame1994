# Pools.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Pools.cs`. The code keeps a one-line gist tagged `[h:Pools-N]`.

## [h:Pools-1]
near: `public static class Pools`

/// <summary>
/// Which bodies a run meets where (C# only, Balance.TunePools; owner 2026-10-13). Each run rolls its own
/// group of <see cref="GroupSize"/> kinds for every area, so every stretch of the climb is a different fight
/// from run to run; the next area's newcomers start turning up <see cref="PreviewFloors"/> floors early, rarely,
/// so the player sees what is coming. Depth still scales every kind (Spawn.Body: health, speed, cadence),
/// so a kind met on floor 2 and again on floor 11 is a harder fight the second time.
///
/// <para>The groups come from their own stream, seeded by the run's seed, so they spend no draw of the run's
/// and the same seed always gives the same groups. Every group has a body that comes to you (Lunger or
/// Burrower; the Brunch only arrives as a pack) and one that shoots from a distance (Shooter, Splitter, Mortar, Sprayer), and shares at
/// most <see cref="MaxShared"/> kinds with the area before it.</para>
/// </summary>
