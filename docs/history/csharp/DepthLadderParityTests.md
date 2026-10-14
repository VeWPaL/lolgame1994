# DepthLadderParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/DepthLadderParityTests.cs`. The code keeps a one-line gist tagged `[h:DepthLadderParityTests-N]`.

## [h:DepthLadderParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The depth ladder, checked against the values measured in the JavaScript.
///
/// <para>
/// These are not written from the formula. Every expected number is a figure that came out of
/// the running game - the floor-1 through floor-1000 columns, the ceiling, and the three shell
/// rates - so a change to the formula that nobody re-measures fails here rather than quietly
/// shipping a different ladder to the Unity build. A port that agrees with its own source is
/// not parity; a port that agrees with a measurement is.
/// </para>
///
/// <para>
/// The one that matters most is <see cref="AGunnerNeverOutrunsThePlayer"/>. Rate scales approach
/// speed as well as cadence, so a ladder that looks reasonable as a percentage can quietly make
/// a body faster than the player, which converts a reactive game into a pre-emptive one. That
/// is the failure the bounded ladder exists to prevent, and it is worth a test of its own rather
/// than a line inside a loop.
/// </para>
/// </summary>

## [h:DepthLadderParityTests-2]
near: `[TestCase(1, 1.0000, 1.0000, 0.4500, 0.0000, Area.Area1)]`

// READ OUT OF THE RUNNING JAVASCRIPT at these floors, with rolled=0. The density column was
// hand-computed the first time and three of the nine were wrong - 9.5 where the game says
// 9.0129, 19.3 where it says 19.0467, 25.2 where it says 25.7890. Every figure below was
// then read out of the running game rather than recomputed, which is the only way a
// parity table is worth anything. They were re-read when the ladder changed from a
// saturating curve to an exponential, because a parity table of STALE values is worse than
// none: it agrees with a game that no longer exists.
//
// The density column saturates at 28 from floor 30 on. That is a cap, not a coincidence, and
// TheLadderIsPlayableAtEveryFloor is the test that says why.

## [h:DepthLadderParityTests-3]
near: `Assert.That(Balance.DepthPack(floor, Bodies.MixFor(area).Brunch),`

/* The pack chance is not a pure function of the floor on the JS side: `depthPack()` takes
   no argument and reads `areaMix().brunch`, so the AREA decides the base and the floor
   only decides the step. The port says the same thing from the other end -
   `DepthPack(int floor, double brunchChance)` - and this assertion passes the area's own
   brunch figure in, which is what makes the two descriptions comparable. */

## [h:DepthLadderParityTests-4]
near: `Assert.That(double.IsInfinity(Balance.DepthTough(100000)) ||`

// BothDialsStopAtTheirCeiling is GONE, and its removal is the change rather than an
// omission. It asserted the health dial stayed under 1 + DepthHpGrowth and got CLOSE to
// it - "or it is a ramp with a lid" - and that reasoning was sound: a ceiling does stop a
// deep run becoming arithmetic. But the shape it produced was the wrong one. An
// asymptote is a promise the content eventually outgrows, and the deep floors were
// shallower than the table made them look.
//
// So the health dial is now unbounded, and HealthKeepsClimbingPastAnyCeiling is the test
// that says so. If the deep floors do become arithmetic, that is a per-level coefficient
// to retune - which is a decision to make with the numbers in front of you, rather than a
// lid that quietly flattens the curve from floor 15 onward and nobody notices for a year.

## [h:DepthLadderParityTests-5]
near: `double t14 = Balance.DepthTough(14), t50 = Balance.DepthTough(50), t200 = Balance.DepthTough(200);`

// The ladder is EXPONENTIAL and health is UNBOUNDED, which inverts this file's previous
// contract. It used to assert the health dial stayed under 1 + DepthHpGrowth and got
// CLOSE to it, on the reasoning that a ceiling stops a deep run becoming arithmetic.
//
// That reasoning was sound and the shape it produced was the wrong one: the asymptote was
// 3.6x, floor 14 already reached 2.5x, and everything after that was spent approaching a
// number it had nearly hit - so the deep floors were shallower than they looked on paper.
// The brief now asks for a climb that is unbroken, and an asymptote is a promise the
// content will eventually outgrow. If the deep floors become arithmetic, that is a
// per-level coefficient to retune, not a reason to put the lid back on.

## [h:DepthLadderParityTests-6]
near: `double early = Balance.DepthTough(3) - Balance.DepthTough(2);`

// The design claim, and the assertion a monotonic-only test cannot make: a saturating
// ladder is perfectly monotone while doing the exact opposite of this. An exponential's
// increments increase, so every floor costs more than the one before it and the run never
// flattens out.

## [h:DepthLadderParityTests-7]
near: `for (int f = 1; f <= 1000; f += 1)`

// THE fairness property, and the reason the RATE dial is capped even though health is
// not. A ranged body whose approach speed exceeds the player's cannot be responded to,
// only anticipated, and anticipating is a different game.
//
// The cap is DERIVED rather than chosen: a ranged body starts at 0.648 px/tick against a
// player who moves at 1.20, and the standing guarantee is that it never closes faster
// than 0.87. 0.87/0.648 is 1.343, so the ceiling is 1.34. The old ceiling was 1.95, which
// would allow 1.264 - FASTER THAN THE PLAYER - and the old ladder only stayed honest
// because its own saturation never reached it before the content ran out. A safety limit
// that safety never needed is indistinguishable from no limit until the day it is needed.

## [h:DepthLadderParityTests-8]
near: `for (int f = 1; f <= 1000; f += 1)`

// Density is capped too, and for a DIFFERENT reason than the rate: not fairness but
// playability. Uncapped it is 246 bodies by floor 40 and 1869 by floor 50, which is not a
// hard fight, it is a hang, and the old ladder put floor 50 at 23. Past the point where
// the room stops being playable the ladder leans on health, which costs the player
// attention rather than the machine its frame budget.

## [h:DepthLadderParityTests-9]
near: `var tough = typeof(Balance).GetMethod("DepthTough");`

// There is nothing to assert here that is not in the signature: none of these take a
// player, a stat, a health number or an item count. That IS the assertion, and it is
// enforced by the shape rather than by a check - which is the right way to enforce it,
// because a check can be deleted and a signature cannot be deleted by accident.
// The discipline is restated here so the reason is on the page next to the thing.
