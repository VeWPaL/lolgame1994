# PortedBehaviourTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/PortedBehaviourTests.cs`. The code keeps a one-line gist tagged `[h:PortedBehaviourTests-N]`.

## [h:PortedBehaviourTests-1]
near: `[TestFixture]`

/// <summary>
/// Checks ported from the JavaScript suite, plus the arithmetic the ports depend on.
///
/// <para>
/// Most of the 94 original checks are integration tests against the whole running game and cannot
/// be ported until <c>update()</c> exists - which is the next step, not this one. What belongs here
/// now is everything that is genuinely self-contained, so that when the simulation arrives there is
/// already a floor under it.
/// </para>
/// </summary>

## [h:PortedBehaviourTests-2]
near: `Assert.That(Balance.RoomRight - Balance.RoomLeft, Is.EqualTo(700));`

// "How far away a gunner has to be before counterstrafing buys anything. Half the width
// of the room, less a margin, is the line."
//
// This test USED to be called TheDeadzoneIsHalfTheRoomWidth and asserted 350 and 570.
// The JavaScript reads 300 and 430. So the drift was not merely unnoticed - the test was
// DEFENDING it, because it had been written from the same prose description the constant
// was written from, and a test that re-derives the same rule from the same sentence will
// agree with the bug for ever.
//
// The margin is the whole difference and it was invisible in the prose. The point of
// stating the number as well as the rule is that a parity port cannot be written from a
// description: half of every number in this file has to come from the other file.

## [h:PortedBehaviourTests-3]
near: `Assert.That(Balance.SwerveDeadzone(Balance.Room.Standard), Is.EqualTo(175),`

// QUARTER AND FOUR-FIFTHS OF THE WIDTH. These were 300 and 430, and they were the port
// being 130px of ramp behind a JavaScript change made on 2026-10-03 - in the same commit
// that made it, which is the one rule PORTED.md states twice. What made it possible is the
// subject of this very test: 300 was described in prose as "half the width less a margin"
// and this file asserted 300 by re-deriving that same sentence, so it agreed with itself.

## [h:PortedBehaviourTests-4]
near: `Assert.That(typeof(Balance).GetField("SwerveMargin"), Is.Null,`

// the margin constant is GONE rather than renamed. A constant named SwerveMargin sitting
// at 50 next to a formula that no longer uses it is a number that looks load-bearing and
// is not, so its absence is the assertion.

## [h:PortedBehaviourTests-5]
near: `var big = new Balance.Room(50, 130, 50 + 1680, 130 + 760);`

// The two numbers that were frozen at one room's size, now asked of a room four times as
// wide. A value derived from the world is only correct until the world changes shape, and
// the JavaScript had both of these wrong for exactly that reason: aggro read 707 where a
// 1680-wide room wants 1567, so a body 900px away stood still.

## [h:PortedBehaviourTests-6]
near: `var small = new Balance.Room(50, 130, 50 + 350, 130 + 225);`

// and a SMALLER room, because "follows the room" has to mean both directions. A constant
// captured at load reads correctly in the room that existed at load and nowhere else, so
// only the big room can catch it - a check in the standard room alone cannot.

## [h:PortedBehaviourTests-7]
near: `var std = Balance.Room.Standard;`

// The consequence, not the constant: a number can follow the room and still not be read
// anywhere, and only a reach asked at a distance can tell the difference.
//
// THESE EXPECTATIONS CHANGED on 2026-10-03, and the old ones were not a retune - they
// were describing a deadzone of 790 in a 1680-wide room, so 700px read as ZERO. That was
// the frozen-constant bug this test was written to catch, and it was catching it in the
// only direction it could: a big room gave a reversing player nothing at 700px because
// 700 happened to be under its deadzone, and the fix moved the deadzone to 420 so the
// same 700px is now 30% of a 924px ramp.
//
// The property worth keeping is the one both rooms share: the ramp is a FRACTION of the
// room, so a distance that is "far" in one room is "mid-ramp" in a larger one, and no
// distance inside either room can be past the top.

## [h:PortedBehaviourTests-8]
near: `Assert.That(Aim.SwerveReach(std.W * 0.5, std), Is.LessThan(1.0),`

// THE PROPERTY: the ramp can never saturate inside the room it is scaled to. Under the
// old fixed 130px ramp, every room more than 430px wide had a stretch beyond which
// 'further is worse' stopped being expressible - which is the bug this whole area is
// about. Both rooms must still be opening at four fifths of their own width.

## [h:PortedBehaviourTests-9]
near: `Assert.That(Hit.PlayerHit(390, 365, 7, 400, 355), Is.True,`

// The lag: a shell landing where the body was, while the body has already moved on. This
// is the whole point of the trailing hitbox - a blink buys a reaction window because of it.
//
// Ten pixels, not twenty. The reach is seventeen, so a twenty pixel gap would miss and
// the assertion would be measuring nothing. The first version of this line used twenty
// and failed, with the port behaving exactly as specified.

## [h:PortedBehaviourTests-10]
near: `const double reach = Balance.PlayerHitR + 7;                          // 17`

// The offset, stated correctly. An earlier version of this test asserted that a shell on
// the drawn ORIGIN misses, reasoning that ten pixels of offset against a seventeen pixel
// reach would put it outside. It does not - ten is well inside seventeen - and the test
// failed with the port behaving exactly as specified.
//
// The offset is not a source of misses at that magnitude. It is a systematic BIAS: the
// acceptance window sits ten pixels lower than the body is drawn, so a shooter aiming at
// the origin is aiming ten pixels high, every single time. That is what the gunner's
// intercept corrects for, and why the correction is worth ten pixels rather than being
// rounded away as negligible.

## [h:PortedBehaviourTests-11]
near: `Assert.That(Balance.PlayerHitR, Is.LessThan(12));`

// "R is deliberately SMALLER than the model: 10 gives a 20px hitbox against a 24px
// character, so it is 83% of the width and misses the edges of the hood and the hem."
// A 24px character is r=12; the hitbox is r=10.

## [h:PortedBehaviourTests-12]
near: `var std = Balance.Room.Standard;`

// EVERY NUMBER IN THIS BLOCK WAS RE-READ from the running JavaScript on 2026-10-03,
// because the deadzone moved that day from 300 to 175 and the ramp from 130 to 385. These
// are literals, not formulas: the spread is an inline expression at 60-tick.js:1327
// (`0.02 + SWERVE_AIM*player.swerve*reach`) rather than a named function, so there is
// nothing to call on the JS side - the values were produced by evaluating that expression
// against the LIVE swerveDeadzone()/swerveFull() in a browser.
//
// Hand-computing them is how this test came to assert 350 against a deadzone of 300.
// Read the deadzone, read the ramp, divide, and every one of those is a chance to be
// wrong in a way that still looks plausible. Live constants and an arithmetic expression
// between them is the only legitimate way to acquire a parity value.

## [h:PortedBehaviourTests-13]
near: `var expected = new[]`

// The JavaScript suite seeds Math.random with mulberry32 at 12345 (src/99-tests.js). If
// this stream does not match, a C# run and a JavaScript run walk different dungeons, and
// every "the port agrees with the original" claim in this project quietly stops being
// true. Five draws, not one: a single draw can agree by luck far more easily than five.
//
// These are read off the original rather than computed by hand. The first version of
// this test failed, because the expected value had been invented instead of measured -
// and the port turned out to be right and the expectation wrong. Which is the correct
// way round, and the reason a parity test is worth more than a plausible assertion.
