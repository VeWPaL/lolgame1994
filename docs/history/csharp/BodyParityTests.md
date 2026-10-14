# BodyParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/BodyParityTests.cs`. The code keeps a one-line gist tagged `[h:BodyParityTests-N]`.

## [h:BodyParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The body table, the spawner, and the trait - against values read out of the running JavaScript.
///
/// <para>
/// The one this file exists for is <see cref="TheSpawnerNeverGivesARangedBodyAWalkSpeed"/>. That
/// is the bug that nearly shipped, it has already happened once in the JavaScript, and the shape
/// of it is worth being blunt about: a trait meant to make a shooter close faster silently turned
/// it into a lunger, and the whole ranged kit - shell, cadence, muzzle prediction, standoff rule -
/// stopped running with nothing anywhere reporting why.
/// </para>
/// </summary>

## [h:BodyParityTests-2]
near: `[Test]`

/// <summary>
/// One armour rule for every body, asserted on the TABLE rather than on the values the
/// per-body test above already reads.
///
/// <para>
/// The per-body assertions would all still pass if a new row were added tomorrow without an
/// armour field, because nothing in them walks the table. That is precisely how the Warden
/// ended up on a different rule from everything else in the JavaScript while this port sat
/// here asserting it was the correct one: <c>BodyRow.Armour</c> is a plain double, so the
/// omission read 0.0 here and 1.0 there, and neither side had a check that would have said so.
/// </para>
///
/// <para>
/// The rule is about SIZE and not about any one body: every row at least as wide as a lunger
/// is armoured, and the Brunch - half a lunger's radius, an eighth of its health - is the one
/// body that is not. The line is read off the table rather than written as a list of names,
/// so a new body lands on the correct side of it automatically and a renamed one cannot
/// quietly escape.
/// </para>
/// <para>
/// The expectation is the constant, not a literal. A second place holding a copy of 0.66 is
/// what let the two ports drift in the first place, and a copy is exactly what this test
/// would be protecting if it read 0.66 directly.
/// </para>
/// </summary>

## [h:BodyParityTests-3]
near: `var rng = new Rng(1u);`

// THE asymmetry, and it is the whole reason the rate dial is bounded. A ranged body's
// approach speed and cadence scale with depth, so the bound on that dial is what keeps a
// gunner slower than the player. A WALKER'S TOP SPEED DOES NOT SCALE AT ALL - and if that
// ever changes, a lunger at depth outruns the player and the fairness property is gone.

## [h:BodyParityTests-4]
near: `var rng = new Rng(11u);`

// far under sense, or a body notices you and then holds somewhere it can never shoot
// from - which reads as broken rather than as an answer, and the player cannot act on it
// because nothing is happening.

## [h:BodyParityTests-5]
near: `var rng = new Rng(1u);`

// A cached standoff is a second number that has to be kept in step with the two it comes
// from. Measured against the JavaScript: at full room pressure a plain shooter holds at
// 165px and at zero pressure at 250px.

## [h:BodyParityTests-6]
near: `var rng = new Rng(1u);`

// A body's trait is what the run IS - same bodies, same answers, every replay - so it has
// to come from the run stream. Its spawn JITTER (notice delay, first shot) must not, or a
// cosmetic roll would shift the dungeon.
