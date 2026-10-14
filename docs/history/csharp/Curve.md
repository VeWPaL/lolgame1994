# Curve.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Curve.cs`. The code keeps a one-line gist tagged `[h:Curve-N]`.

## [h:Curve-1]
near: `public static class Curve`

/// <summary>
/// The difficulty curve (C# only, 2026-10-07): a per-floor shape laid over the depth ladder, plus
/// the targets the playtest bot's `curve` command checks it against. The JS ladder is untouched.
/// </summary>

## [h:Curve-2]
near: `public sealed class Stage`

/// <summary>
/// One stage of the shape, from floor First until the next stage's First. Tough, Rate and Drops
/// multiply; Bodies, Pack and Heavy add. Fields, not constants, so the bot's --dial can try a value.
/// </summary>

## [h:Curve-3]
near: ``

/// <summary>
/// The shape. START (1-2) is a bump while the player has no kit, MIDDLE (3-10) a dip, 11-12 a
/// ramp into the last area, then the climb. Each stage holds until the next one's floor.
/// </summary>

## [h:Curve-4]
near: `public static readonly Stage[] Stages =`

// Why each column (measured with the bot's curve command, seeds 101-300 and 301-500):
//   Tough   health: longer fights cost more HP; the dip's main dial.
//   Rate    cadence and ranged approach (under DepthRateCap): more shells to dodge widens the skill gap.
//   Bodies  added to the room roll; only whole bodies count, so it places the step where
//           the ladder adds a body (fewer bodies is also fewer kills, so fewer drops).
//   Pack    added to the Brunch pack chance; packs are the middle's main attrition.
//   Heavy   added to the area's gunner chance; its 4 HP shell kills, and skill barely dodges it.
//   Drops   the kill drop chance x this; 0 in START so the first floors cost hearts.
// From floor 11 on, a floor is never easier than the one before (body health and bodies).

## [h:Curve-5]
near: `public enum MiddleRule`

/// <summary>
/// Rubric 5 for MIDDLE as written ("healing below damage") is the owner's open decision; the
/// two restatements are kept here so choosing one is this one line (the bot's --mid-rule tries one).
/// </summary>

## [h:Curve-6]
near: `Recovery,`

/// <summary>Over the runs that leave floor 10: mean (HP + armour leaving 10) - (HP + armour entering 3)
/// is at most mean (max HP entering 11) - (HP entering 3): what START took plus the max HP
/// gained.</summary>
