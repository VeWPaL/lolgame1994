using System;
using NUnit.Framework;

namespace Depths.Tests
{
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
    [TestFixture]
    public sealed class DepthLadderParityTests
    {
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
        [TestCase(1, 1.0000, 1.0000, 0.0000)]
        [TestCase(2, 1.0510, 1.0273, 0.0192)]
        [TestCase(3, 1.1085, 1.0564, 0.0428)]
        [TestCase(4, 1.1733, 1.0876, 0.0717)]
        [TestCase(5, 1.2464, 1.1209, 0.1072)]
        [TestCase(6, 1.3288, 1.1564, 0.1507)]
        [TestCase(7, 1.4218, 1.1943, 0.2041)]
        [TestCase(8, 1.5265, 1.2349, 0.2695)]
        [TestCase(9, 1.6447, 1.2782, 0.3497)]
        [TestCase(10, 1.7779, 1.3245, 0.4481)]
        [TestCase(11, 1.9280, 1.3400, 0.5687)]
        [TestCase(12, 2.0974, 1.3400, 0.7166)]
        [TestCase(13, 2.2883, 1.3400, 0.8980)]
        [TestCase(14, 2.5035, 1.3400, 1.1205)]
        [TestCase(20, 4.5107, 1.3400, 4.0146)]
        [TestCase(30, 13.5839, 1.3400, 28.0000)]
        [TestCase(50, 143.7237, 1.3400, 28.0000)]
        [TestCase(100, 57740.8203, 1.3400, 28.0000)]
        public void TheLadderMatchesTheMeasuredJavaScriptValues(int floor, double tough, double rate, double bodies)
        {
            Assert.That(Balance.DepthTough(floor), Is.EqualTo(tough).Within(0.005),
                "health scaling at floor " + floor);
            Assert.That(Balance.DepthRate(floor), Is.EqualTo(rate).Within(0.005),
                "rate scaling at floor " + floor);
            Assert.That(Balance.DepthBodies(floor, 0), Is.EqualTo(bodies).Within(0.001),
                "density at floor " + floor + " - the JS figure is depthBodies(0), not the whole room");
        }

        [Test]
        public void FloorOneIsAFirstFloor()
        {
            // exactly 1, not approximately. A ladder that starts at 1.0001 has already begun
            // climbing before the player has picked up a controller.
            Assert.That(Balance.DepthTough(1), Is.EqualTo(1.0));
            Assert.That(Balance.DepthRate(1), Is.EqualTo(1.0));
            Assert.That(Balance.DepthBodies(1, 0), Is.EqualTo(0.0));
            // and nothing below the first floor can make it negative
            Assert.That(Balance.DepthTough(0), Is.EqualTo(1.0));
            Assert.That(Balance.DepthTough(-5), Is.EqualTo(1.0));
            Assert.That(Balance.DepthBodies(-5, 3), Is.EqualTo(3.0));
        }

        [Test]
        public void HealthHasNoCeilingAtAll()
        {
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
            Assert.That(double.IsInfinity(Balance.DepthTough(100000)) ||
                        Balance.DepthTough(100000) > 1e6,
                "health at floor 100000 is " + Balance.DepthTough(100000) +
                ", which suggests something is capping it after all");
        }

        [Test]
        public void TheLadderIsMonotoneEverywhere()
        {
            // a ladder that dips somewhere is worse than one that is shallow: descending can make a
            // room easier, and a player who notices will stop descending
            double prevT = 0, prevR = 0;
            for (int f = 1; f <= 500; f++)
            {
                double t = Balance.DepthTough(f), r = Balance.DepthRate(f);
                Assert.That(t, Is.GreaterThanOrEqualTo(prevT), "health dips at floor " + f);
                Assert.That(r, Is.GreaterThanOrEqualTo(prevR), "rate dips at floor " + f);
                prevT = t; prevR = r;
            }
        }

        [Test]
        public void HealthKeepsClimbingPastAnyCeiling()
        {
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
            double t14 = Balance.DepthTough(14), t50 = Balance.DepthTough(50), t200 = Balance.DepthTough(200);
            Assert.That(t50, Is.GreaterThan(t14 * 4),
                "floor 50 is only " + t50 + " against floor 14's " + t14 + ", so the climb has flattened");
            Assert.That(t200, Is.GreaterThan(t50 * 4), "floor 200 is not meaningfully past floor 50");
        }

        [Test]
        public void TheStepsGrowRatherThanShrink()
        {
            // The design claim, and the assertion a monotonic-only test cannot make: a saturating
            // ladder is perfectly monotone while doing the exact opposite of this. An exponential's
            // increments increase, so every floor costs more than the one before it and the run never
            // flattens out.
            double early = Balance.DepthTough(3) - Balance.DepthTough(2);
            double late = Balance.DepthTough(14) - Balance.DepthTough(13);
            Assert.That(late, Is.GreaterThan(early * 2),
                "the per-floor step does not grow (" + early + " early, " + late + " at floor 13), " +
                "so the ladder is flattening rather than steepening");
            // and it has not already peaked and turned over by the last planned floor
            double prior = Balance.DepthTough(13) - Balance.DepthTough(12);
            Assert.That(late, Is.GreaterThan(prior), "the steps peaked before the last floor");
        }

        [Test]
        public void AGunnerNeverOutrunsThePlayer()
        {
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
            for (int f = 1; f <= 1000; f += 1)
            {
                double approach = 0.3 * Balance.PressureRate * Balance.DepthRate(f);
                Assert.That(approach, Is.LessThanOrEqualTo(0.87 + 1e-9),
                    "at floor " + f + " a ranged body approaches at " + approach +
                    " px/tick, past the 0.87 the reaction-time rule allows");
                Assert.That(approach, Is.LessThan(Balance.PlayerMove),
                    "at floor " + f + " a gunner approaches at " + approach +
                    " px/tick against a player who moves at " + Balance.PlayerMove);
            }
        }

        [Test]
        public void TheLadderIsPlayableAtEveryFloor()
        {
            // Density is capped too, and for a DIFFERENT reason than the rate: not fairness but
            // playability. Uncapped it is 246 bodies by floor 40 and 1869 by floor 50, which is not a
            // hard fight, it is a hang, and the old ladder put floor 50 at 23. Past the point where
            // the room stops being playable the ladder leans on health, which costs the player
            // attention rather than the machine its frame budget.
            for (int f = 1; f <= 1000; f += 1)
            {
                Assert.That(Balance.DepthBodies(f, 0), Is.LessThanOrEqualTo(Balance.DepthBodyCap),
                    "the depth density bonus reaches " + Balance.DepthBodies(f, 0) + " at floor " + f +
                    ", past the " + Balance.DepthBodyCap + " cap");
            }
            // ...and the health pool is the dial that carries the deep floors instead
            Assert.That(Balance.DepthTough(50), Is.GreaterThan(Balance.DepthTough(1) * 10),
                "at floor 50 nothing is still climbing except health, so a deep floor is a longer " +
                "fight rather than a busier one");
        }

        [Test]
        public void ADeepGunnerStaysReadable()
        {
            // the same dial read through the cadence instead of the approach. At the old linear
            // ladder, floor 50 came to 24.8 shells a second.
            for (int f = 1; f <= 1000; f += 1)
            {
                double cdTicks = System.Math.Max(1, Balance.Sec(0.8) / (Balance.PressureRate * Balance.DepthRate(f)));
                double shellsPerSecond = Balance.TickHz / cdTicks;
                Assert.That(shellsPerSecond, Is.LessThan(6.0),
                    "at floor " + f + " a gunner fires " + shellsPerSecond.ToString("F2") +
                    " shells a second, which is not a fight the player can read");
            }
        }

        [Test]
        public void ThePackChanceStops()
        {
            // it is the one dial still on a linear ramp, because its ceiling was the point all along
            Assert.That(Balance.DepthPack(1000, 0.45), Is.EqualTo(Balance.DepthPackCap).Within(1e-9));
            Assert.That(Balance.DepthPack(100000, 0.45), Is.LessThanOrEqualTo(Balance.DepthPackCap));
            Assert.That(Balance.DepthPack(1, 0.45), Is.EqualTo(0.45).Within(1e-9),
                "floor 1 already has the base chance and nothing added to it");
        }

        [Test]
        public void TheLadderNeverReadsThePlayer()
        {
            // There is nothing to assert here that is not in the signature: none of these take a
            // player, a stat, a health number or an item count. That IS the assertion, and it is
            // enforced by the shape rather than by a check - which is the right way to enforce it,
            // because a check can be deleted and a signature cannot be deleted by accident.
            // The discipline is restated here so the reason is on the page next to the thing.
            var tough = typeof(Balance).GetMethod("DepthTough");
            var rate = typeof(Balance).GetMethod("DepthRate");
            var bodies = typeof(Balance).GetMethod("DepthBodies");
            Assert.That(tough, Is.Not.Null, "DepthTough is missing, so the reflection below proves nothing");
            Assert.That(rate, Is.Not.Null, "DepthRate is missing, so the reflection below proves nothing");
            Assert.That(bodies, Is.Not.Null, "DepthBodies is missing, so the reflection below proves nothing");
            Assert.That(tough!.GetParameters().Length, Is.EqualTo(1), "DepthTough takes a floor and nothing else");
            Assert.That(rate!.GetParameters().Length, Is.EqualTo(1), "DepthRate takes a floor and nothing else");
            Assert.That(bodies!.GetParameters().Length, Is.EqualTo(2), "DepthBodies takes a floor and the existing roll");
        }
    }
}
