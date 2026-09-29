using System;
using System.Globalization;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
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
    [TestFixture]
    public sealed class PortedBehaviourTests
    {
        // ------------------------------------------------------------- balance

        [Test]
        public void TheTickRateAndItsConversionsAreTheOriginals()
        {
            // 60 * 3.5 = 210. Everything else in the game is expressed in these, and `sec` rounds -
            // the rounding is load-bearing, not decoration.
            Assert.That(Balance.TickHz, Is.EqualTo(210));
            Assert.That(Balance.Sec(0.5), Is.EqualTo(105), "the gunner's cast is 0.5s of warning");
            Assert.That(Balance.Sec(0.34), Is.EqualTo(71), "the lunge windup");
            Assert.That(Balance.Sec(0.42), Is.EqualTo(88), "rounds up, because 0.42*210 is 88.2");
            Assert.That(Balance.Sec(0.06), Is.EqualTo(13), "rounds up from 12.6");
        }

        [Test]
        public void TheDeadzoneIsHalfTheRoomWidth()
        {
            // "How far away a gunner has to be before counterstrafing buys anything. Half the width
            // of the room is the line."
            Assert.That(Balance.RoomRight - Balance.RoomLeft, Is.EqualTo(700));
            Assert.That(Balance.SwerveDeadzone, Is.EqualTo(350));
            Assert.That(Balance.SwerveFull, Is.EqualTo(570));
        }

        // ------------------------------------------------------------- the hitbox

        [Test]
        public void TheHitboxIsTenPixelsBelowTheDrawnPosition()
        {
            // The single most load-bearing constant in the game, and the one most likely to be
            // "tidied" away by someone who does not know why it is there.
            Assert.That(Balance.PlayerHitDy, Is.EqualTo(10));
            Assert.That(Balance.PlayerHitR, Is.EqualTo(10));
        }

        [Test]
        public void PlayerHitIsMeasuredAgainstTheLaggedPositionAndTheOffset()
        {
            // Dead centre of the hitbox: a hit.
            Assert.That(Hit.PlayerHit(400, 365, 7, 400, 355), Is.True,
                "a shell dead centre of the hitbox should connect");

            // The lag: a shell landing where the body was, while the body has already moved on. This
            // is the whole point of the trailing hitbox - a blink buys a reaction window because of it.
            //
            // Ten pixels, not twenty. The reach is seventeen, so a twenty pixel gap would miss and
            // the assertion would be measuring nothing. The first version of this line used twenty
            // and failed, with the port behaving exactly as specified.
            Assert.That(Hit.PlayerHit(390, 365, 7, 400, 355), Is.True,
                "a shell 10px behind the body, on the lagged position, should connect");
            Assert.That(Hit.PlayerHit(380, 365, 7, 400, 355), Is.False,
                "and one 20px behind should not - the hitbox is forgiving, not enormous");

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
            const double reach = Balance.PlayerHitR + 7;                          // 17
            double insideTopOfRealBox = 355 + Balance.PlayerHitDy - (reach - 1); // 16px above centre
            double insideTopIfUnbiased = 355 - (reach - 1);                      // 16px above origin

            Assert.That(Hit.PlayerHit(400, insideTopOfRealBox, 7, 400, 355), Is.True,
                "a shell inside the top of the real hitbox should connect");
            Assert.That(Hit.PlayerHit(400, insideTopIfUnbiased, 7, 400, 355), Is.False,
                "the top edge of the acceptance window is set by the OFFSET, not the drawn origin");
        }

        [Test]
        public void TheHitboxIsForgivingByDesign()
        {
            // "R is deliberately SMALLER than the model: 10 gives a 20px hitbox against a 24px
            // character, so it is 83% of the width and misses the edges of the hood and the hem."
            // A 24px character is r=12; the hitbox is r=10.
            Assert.That(Balance.PlayerHitR, Is.LessThan(12));
            // And the total reach for a gunner shell (r=7) is 17px, not 19.
            Assert.That(Balance.PlayerHitR + 7, Is.EqualTo(17));
        }

        // ------------------------------------------------------------- the intercept

        [Test]
        public void ASettledPlayerIsBelievedInFull()
        {
            // A stationary player, read with no swerve: the solution is the plain bearing.
            var r = InterceptSolver.Lunge(0, -140, 0, 0, 0, 0, 0);
            Assert.That(r.Confidence, Is.EqualTo(1.0));
            Assert.That(r.Dx, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(r.Dy, Is.EqualTo(-1.0).Within(1e-12));
            Assert.That(r.Dist, Is.EqualTo(140.0).Within(1e-9));
        }

        [Test]
        public void AThrashingPlayerIsBelievedNotAtAll()
        {
            // Max swerve drops the lunge's confidence to the floor, LUNGE_CONF_MIN.
            var settled = InterceptSolver.Lunge(0, -140, 1.122, 0, 0.0, 0, 0);
            var thrashing = InterceptSolver.Lunge(0, -140, 1.122, 0, 1.0, 0, 0);

            Assert.That(thrashing.Confidence, Is.EqualTo(Balance.LungeConfMin).Within(1e-12));
            Assert.That(thrashing.Dist, Is.LessThan(settled.Dist),
                "a player who will not hold still should get a shorter solution, not the same one");

            // The consequence, stated as the mechanic: baiting is a DOWNGRADE, not an escape. The
            // attacker still commits - it just commits to less.
            Assert.That(thrashing.Dist, Is.GreaterThan(0),
                "a thrashing player made the lunge evaporate entirely, which makes baiting free");
        }

        [Test]
        public void TheGunnerLeadsAndTheLungeLeadsTheSameWay()
        {
            // One signal, two consumers. The gunner applies no confidence floor, so at full swerve it
            // stops leading entirely - and that is the point: a player reversing every half second
            // has travelled very nearly nowhere by the time a close shell arrives, so aiming at where
            // they ARE and aiming at where they are GOING are nearly the same shot.
            var g0 = InterceptSolver.Gun(400, 355, 1.122, 0, 0.0, 400, 155, 2.2);
            var g1 = InterceptSolver.Gun(400, 355, 1.122, 0, 1.0, 400, 155, 2.2);

            Assert.That(g0.Confidence, Is.EqualTo(1.0));
            Assert.That(g1.Confidence, Is.EqualTo(0.0));
            Assert.That(g1.Dist, Is.LessThan(g0.Dist),
                "a fully thrashing player should collapse the gunner's lead to nothing");
        }

        [Test]
        public void TheGunnerAimsAtTheHitboxAndNotAtTheSprite()
        {
            // Same shot, two targets ten pixels apart. The gunner must use the hitbox, because that is
            // the circle the collision test uses - a shot aimed at the drawn origin passes over the
            // player's head with a hitbox's width to spare, every single time.
            double lagX = 400, lagY = 355, sx = 400, sy = 155;
            var g = InterceptSolver.Gun(lagX, lagY, 0, 0, 0, sx, sy, 2.2);

            // With no horizontal lead the solution is straight down, and the arrival point must be
            // 200 + PlayerHitDy below the shooter.
            Assert.That(g.Dx, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(g.Dy, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(g.Dist, Is.EqualTo(200 + Balance.PlayerHitDy).Within(1e-9),
                "the gunner's solution must land on the hitbox centre, not the sprite origin");
        }

        [Test]
        public void TheGunnerCountsItsOwnCast()
        {
            // The cast is a quarter of a second of visible muzzle light during which the player keeps
            // moving. If the solve does not count it, the prediction is made against a target that has
            // already left - which is precisely the bug that made counterstrafing free in front of a
            // gunner. The check: the arrival time must be at least the cast.
            var g = InterceptSolver.Gun(400, 355, 1.122, 0, 0, 400, 155, 2.2);
            Assert.That(g.Ticks, Is.GreaterThanOrEqualTo(Balance.CastTime),
                "the gunner's solution is shorter than its own cast, so it is aimed at the past");
        }

        [Test]
        public void FourteenIterationsIsWhatMakesTheGunnersSolutionConverge()
        {
            // The reason GunIter is 14 and not 3. Convergence is set by how much slower the target is
            // than the shell: each pass cuts the remaining error by roughly (player speed / shell
            // speed), which here is about 0.55. Three passes therefore leave about 1/16 of the error.
            //
            // Measured, not asserted from a comment: solve at both iteration counts and look at how
            // far apart they are.
            double lagX = 400, lagY = 355, tvx = 1.122, sx = 400, sy = 155, pspd = 2.2;

            var converged = InterceptSolver.Gun(lagX, lagY, tvx, 0, 0, sx, sy, pspd);

            // A deliberately under-converged solve, three passes, same constants.
            double conf = 1.0, ax = lagX - sx, ay = lagY + Balance.PlayerHitDy - sy, need = 0;
            for (int k = 0; k < 3; k++)
            {
                need = Balance.CastTime + Math.Sqrt(ax * ax + ay * ay) / pspd;
                ax = lagX + tvx * conf * need - sx;
                ay = lagY + Balance.PlayerHitDy - sy;
            }
            double three = Math.Sqrt(ax * ax + ay * ay);

            double missThree = Math.Abs(three - converged.Dist);
            double missAtArrival = tvx * Math.Abs(need - converged.Ticks);

            Assert.That(missAtArrival, Is.GreaterThan(10.0),
                "three passes and fourteen passes agree to within " + missAtArrival.ToString("F2") +
                "px of player travel, so the iteration count is not load-bearing and the comment " +
                "claiming thirty pixels is wrong");
        }

        [Test]
        public void TheSpreadIsAFloorPlusADistanceRamp()
        {
            // Inside the deadzone, counterstrafing buys nothing: only the floor.
            Assert.That(InterceptSolver.GunSpread(1.0, 200), Is.EqualTo(Balance.GunSpreadFloor).Within(1e-12));
            Assert.That(InterceptSolver.GunSpread(1.0, 350), Is.EqualTo(Balance.GunSpreadFloor).Within(1e-12));

            // Past the full-spread distance it is the whole allowance.
            Assert.That(InterceptSolver.GunSpread(1.0, 700), Is.EqualTo(0.32).Within(1e-9));

            // And the ramp is monotonic, which is the whole claim about distance.
            double prev = -1;
            for (int d = 0; d <= 800; d += 20)
            {
                double s = InterceptSolver.GunSpread(1.0, d);
                Assert.That(s, Is.GreaterThanOrEqualTo(prev), "the spread fell at " + d + "px");
                prev = s;
            }
        }

        [Test]
        public void TheStandoffIsWhatMakesTheLungeAMechanic()
        {
            // A lunge is only interesting if there is a gap to cross. LungeHold is the distance the
            // chaser parks at and LungeReach is the furthest the solution may be and still commit.
            Assert.That(Balance.LungeHold, Is.GreaterThan(Balance.LungeMin));
            Assert.That(Balance.LungeReach, Is.GreaterThan(Balance.LungeHold));
            Assert.That(Balance.LungeReach, Is.LessThanOrEqualTo(300),
                "the reach grew past the room; a lunge that can be committed to from anywhere is not a read");
        }

        // ------------------------------------------------------------- the RNG

        [Test]
        public void ThePortedRngIsDeterministic()
        {
            var a = new Mulberry32(12345);
            var b = new Mulberry32(12345);
            for (int i = 0; i < 500; i++)
                Assert.That(a.NextDouble(), Is.EqualTo(b.NextDouble()));
        }

        [Test]
        public void ThePortedRngStaysInRange()
        {
            var r = new Mulberry32(12345);
            for (int i = 0; i < 20000; i++)
            {
                double v = r.NextDouble();
                Assert.That(v, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(v, Is.LessThan(1.0));
            }
        }

        [Test]
        public void ThePortedRngMatchesTheJavaScriptStream()
        {
            // The JavaScript suite seeds Math.random with mulberry32 at 12345 (src/99-tests.js). If
            // this stream does not match, a C# run and a JavaScript run walk different dungeons, and
            // every "the port agrees with the original" claim in this project quietly stops being
            // true. Five draws, not one: a single draw can agree by luck far more easily than five.
            //
            // These are read off the original rather than computed by hand. The first version of
            // this test failed, because the expected value had been invented instead of measured -
            // and the port turned out to be right and the expectation wrong. Which is the correct
            // way round, and the reason a parity test is worth more than a plausible assertion.
            var expected = new[]
            {
                0.9797282677609473, 0.3067522644996643, 0.484205421525985,
                0.817934412509203, 0.5094283693470061,
            };

            var r = new Mulberry32(12345);
            for (int i = 0; i < expected.Length; i++)
            {
                double got = r.NextDouble();
                Assert.That(got.ToString("R", CultureInfo.InvariantCulture),
                    Is.EqualTo(expected[i].ToString("R", CultureInfo.InvariantCulture)),
                    "draw " + i + " of mulberry32(12345) does not match the JavaScript");
            }
        }
    }
}
