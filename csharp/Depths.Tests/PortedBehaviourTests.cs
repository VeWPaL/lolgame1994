using System;
using System.Globalization;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>Checks ported from the JavaScript suite, plus the arithmetic the ports depend on. [h:PortedBehaviourTests-1]</summary>
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
        public void TheDeadzoneIsAQuarterOfTheRoomWidth()
        {
            // "How far away a gunner has to be before counterstrafing buys anything. [h:PortedBehaviourTests-2]
            Assert.That(Balance.RoomRight - Balance.RoomLeft, Is.EqualTo(700));
            // QUARTER AND FOUR-FIFTHS OF THE WIDTH. [h:PortedBehaviourTests-3]
            Assert.That(Balance.SwerveDeadzone(Balance.Room.Standard), Is.EqualTo(175),
                "a quarter of 700 - it was 300, which with a 130px ramp left distance doing nothing "
                + "outside a 130px notch");
            Assert.That(Balance.SwerveFull(Balance.Room.Standard), Is.EqualTo(560),
                "four fifths of 700 - it was 430, and `reach` saturated at 1.0 above that, so 'further "
                + "is worse' could not be expressed beyond it");
            // the margin constant is GONE rather than renamed. [h:PortedBehaviourTests-4]
            Assert.That(typeof(Balance).GetField("SwerveMargin"), Is.Null,
                "SwerveMargin described an offset from half the room width, and the deadzone no longer "
                + "starts there - if it is being brought back, it has to come back with a reader");
        }

        [Test]
        public void TheRoomScaledNumbersFollowTheRoom()
        {
            // The two numbers that were frozen at one room's size, now asked of a room four times as wide. [h:PortedBehaviourTests-5]
            var big = new Balance.Room(50, 130, 50 + 1680, 130 + 760);
            Assert.That(big.W, Is.EqualTo(1680));
            Assert.That(Balance.SwerveDeadzone(big), Is.EqualTo(420), "a quarter of 1680");
            Assert.That(Balance.SwerveFull(big), Is.EqualTo(1344), "420 + 924, which is 0.25 and 0.55 of 1680");
            // and a SMALLER room, because "follows the room" has to mean both directions. [h:PortedBehaviourTests-6]
            var small = new Balance.Room(50, 130, 50 + 350, 130 + 225);
            Assert.That(Balance.SwerveDeadzone(small), Is.EqualTo(88), "0.25 of 350");
            Assert.That(Balance.SwerveFull(small), Is.EqualTo(281), "88 + 193, which is 0.25 and 0.55 of 350");
            Assert.That(Balance.SwerveFullBase(small), Is.LessThan(Balance.SwerveFullBase(Balance.Room.Standard)),
                "the ramp in a room half as wide is not narrower than the standard room's");
            // and the standard room still reads what it always read, which is what makes this a fix
            // rather than a retune of a port whose balance tests were all taken in that room
            Assert.That(Balance.SwerveDeadzone(Balance.Room.Standard), Is.EqualTo(175));
            Assert.That(Balance.SwerveFull(Balance.Room.Standard), Is.EqualTo(560));            // 0.85 of the diagonal: 707 for the standard room, and it must not still be 707 for a big one
            Assert.That(Balance.AggroRange(Balance.Room.Standard), Is.EqualTo(707));
            Assert.That(Balance.AggroRange(big), Is.EqualTo(1567));
            Assert.That(Balance.AggroRange(big), Is.Not.EqualTo(Balance.AggroRange(Balance.Room.Standard)));
        }

        [Test]
        public void ASwerveRampInABigRoomOpensUpFurtherOut()
        {
            // The consequence, not the constant: a number can follow the room and still not be read anywhere, and only a... [h:PortedBehaviourTests-7]
            var std = Balance.Room.Standard;
            var big = new Balance.Room(50, 130, 50 + 1680, 130 + 760);
            Assert.That(std.W, Is.EqualTo(700));
            Assert.That(big.W, Is.EqualTo(1680));

            // 700px in the standard room: past the top (175 + 385 = 560), so fully open.
            Assert.That(Aim.SwerveReach(700, std), Is.EqualTo(1.0).Within(1e-9),
                "700px is past the top of a 700-wide room's ramp, which ends at 560");

            // the SAME 700px in a big room: 280 of the way up a 924px ramp, so 280/924.
            Assert.That(Aim.SwerveReach(700, big), Is.EqualTo(280.0 / 924.0).Within(1e-12),
                "read from the rule: (700 - 420) / (1344 - 420)");

            // and the deadzone is still real: 400px is inside a big room's 420px deadzone, so nothing.
            Assert.That(Aim.SwerveReach(400, big), Is.EqualTo(0.0).Within(1e-12),
                "400px is inside a 1680-wide room's deadzone, so the allowance must be nothing");
            // ...while the same 400px is 58% of the way up the standard room's ramp, which is the
            // asymmetry the whole room-scaling rule exists to produce.
            Assert.That(Aim.SwerveReach(400, std), Is.EqualTo(225.0 / 385.0).Within(1e-12),
                "(400 - 175) / (560 - 175)");

            // THE PROPERTY: the ramp can never saturate inside the room it is scaled to. [h:PortedBehaviourTests-8]
            Assert.That(Aim.SwerveReach(std.W * 0.5, std), Is.LessThan(1.0),
                "the standard room's ramp saturates before its own halfway mark");
            Assert.That(Aim.SwerveReach(big.W * 0.5, big), Is.LessThan(1.0),
                "the big room's ramp saturates before its own halfway mark, so distance stops mattering");
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

            // The lag: a shell landing where the body was, while the body has already moved on. [h:PortedBehaviourTests-9]
            Assert.That(Hit.PlayerHit(390, 365, 7, 400, 355), Is.True,
                "a shell 10px behind the body, on the lagged position, should connect");
            Assert.That(Hit.PlayerHit(380, 365, 7, 400, 355), Is.False,
                "and one 20px behind should not - the hitbox is forgiving, not enormous");

            // The offset, stated correctly. [h:PortedBehaviourTests-10]
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
            // "R is deliberately SMALLER than the model: 10 gives a 20px hitbox against a 24px character, so it is 83% of... [h:PortedBehaviourTests-11]
            Assert.That(Balance.PlayerHitR, Is.LessThan(12));
            // And the total reach for a gunner shell (r=7) is 17px, not 19.
            Assert.That(Balance.PlayerHitR + 7, Is.EqualTo(17));
        }

        [Test]
        public void TheSpreadIsAFloorPlusADistanceRamp()
        {
            // EVERY NUMBER IN THIS BLOCK WAS RE-READ from the running JavaScript on 2026-10-03, because the deadzone moved... [h:PortedBehaviourTests-12]
            var std = Balance.Room.Standard;
            var dz = Balance.SwerveDeadzone(std);
            var full = Balance.SwerveFull(std);

            // 200px is inside the deadzone, so a reversing player gets the floor and nothing more.
            Assert.That(dz, Is.EqualTo(175));
            Assert.That(Aim.GunSpread(1.0, 200), Is.EqualTo(0.03948051948051948).Within(1e-15),
                "read from the browser: 25/385 of the ramp is open at 200px, so 0.02 + 0.30*0.0649");

            // and the ramp, sampled across its whole width rather than at one lucky distance
            Assert.That(Aim.GunSpread(1.0, 300), Is.EqualTo(0.1174025974025974).Within(1e-15),
                "read from the browser at 300px");
            Assert.That(Aim.GunSpread(1.0, 350), Is.EqualTo(0.15636363636363634).Within(1e-15),
                "read from the browser at 350px");
            Assert.That(Aim.GunSpread(1.0, 450), Is.EqualTo(0.23428571428571426).Within(1e-15),
                "read from the browser at 450px");
            Assert.That(Aim.GunSpread(1.0, 500), Is.EqualTo(0.27324675324675324).Within(1e-15),
                "read from the browser at 500px");

            // and the top of the ramp, which in the drifted port was 430 and is now 560
            Assert.That(full, Is.EqualTo(560));
            Assert.That(Aim.GunSpread(1.0, 560), Is.EqualTo(0.32).Within(1e-12),
                "read from the browser: 0.02 + 0.30, the ramp fully open at 560px");
            // Past the full-spread distance it is the whole allowance.
            Assert.That(Aim.GunSpread(1.0, 700), Is.EqualTo(0.32).Within(1e-9));

            // And the ramp is monotonic, which is the whole claim about distance.
            double prev = -1;
            for (int d = 0; d <= 800; d += 20)
            {
                double s = Aim.GunSpread(1.0, d);
                Assert.That(s, Is.GreaterThanOrEqualTo(prev), "the spread fell at " + d + "px");
                prev = s;
            }
        }

        [Test]
        public void TheStandoffIsWhatMakesTheLungeAMechanic()
        {
            // A lunge is only interesting if there is a gap to cross. LungeHold is the distance the
            // lunger parks at and LungeReach is the furthest the solution may be and still commit.
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
            // The JavaScript suite seeds Math.random with mulberry32 at 12345 (src/99-tests.js). [h:PortedBehaviourTests-13]
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
