# AreaTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/AreaTests.cs`. The code keeps a one-line gist tagged `[h:AreaTests-N]`.

## [h:AreaTests-1]
near: `[TestFixture]`

/// <summary>
/// The themed-area split of the climb. The mapping is pinned at every boundary, the Area1 mix
/// is pinned to the pre-area measured numbers, and the planner's default area is pinned to be
/// indistinguishable from passing Area1 explicitly - that is what keeps the 127 parity rows
/// honest now that the planner takes an area argument.
/// </summary>

## [h:AreaTests-2]
near: `var changed = 0;`

// Across a deterministic set of seeds, asking for a non-Area1 area must change what
// gets planned at least once - otherwise the area dial is decorative in C# even
// though the JavaScript honours it.

## [h:AreaTests-3]
near: `int CountKind(Area area, BodyKind want)`

// The dials are decorative unless the planner consumes them. Across a fixed set of
// seeds, planning with Final (heavier, more pack-hungry) must produce a measurably
// different makeup than Area1: more gunners in particular. If the planner went back
// to a hardcoded 0.55, these two lists would come out identical.

## [h:AreaTests-4]
near: `double LungerShare(Area area)`

// Lunger SHARE among the two melee-or-shell slot kinds. A raw count cannot isolate the
// type roll: packs and gunners eat slots, so an area with more Brunch has fewer
// lunger-bearing slots even with the same roll - count delusion dressed as signal.
