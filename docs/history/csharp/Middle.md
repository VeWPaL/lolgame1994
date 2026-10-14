# Middle.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Middle.cs`. The code keeps a one-line gist tagged `[h:Middle-N]`.

## [h:Middle-1]
near: `public static class Middle`

/// <summary>
/// The middle enemies (C# only): Splitter, Mortar and Sprayer walk and aim like a shooter (TickOrder.StepRanged)
/// and differ in what leaves when the cast ends (<see cref="Release"/>); the Burrower and the egg (Eggs.cs) have
/// their own steps. <see cref="Sharp"/> is the 2026-10-13/14 retune; off, the lab-era bodies. Design notes and
/// the numbers' history: docs/DESIGN.md (written 2026-10-14; there was never an enemies.md).
/// </summary>

## [h:Middle-2]
near: `public static void StepBurrower(RunState run, Enemy e, double edx, double edy, double dist, double sm)`

/// <summary>
/// The Burrower's cycle on <c>move</c>/<c>moveT</c>: walk (hittable), dig (a rooted tell), under (buried: no
/// contact, no hits), rise (a tell where it stopped, still buried), then up, striking that spot. Sharp, it
/// steers rather than slides: a weaving walk, a dive that gathers speed and curves onto its mark.
/// </summary>
