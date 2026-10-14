using System;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The depth ladder, checked against the values measured in the JavaScript. [h:DepthLadderParityTests-1]</summary>
    [TestFixture]
    public sealed class DepthLadderParityTests
    {
        // READ OUT OF THE RUNNING JAVASCRIPT at these floors, with rolled=0. [h:DepthLadderParityTests-2]
[TestCase(1, 1.0000, 1.0000, 0.4500, 0.0000, Area.Area1)]
        [TestCase(2, 1.0510, 1.0273, 0.4850, 0.0192, Area.Area1)]
        [TestCase(3, 1.1085, 1.0564, 0.5200, 0.0428, Area.Area1)]
        [TestCase(4, 1.1733, 1.0876, 0.5550, 0.0717, Area.Area1)]
        [TestCase(5, 1.2464, 1.1209, 0.6400, 0.1072, Area.Area2)]
        [TestCase(6, 1.3288, 1.1564, 0.6750, 0.1507, Area.Area2)]
        [TestCase(7, 1.4218, 1.1943, 0.7100, 0.2041, Area.Area2)]
        [TestCase(8, 1.5265, 1.2349, 0.7450, 0.2695, Area.Area2)]
        [TestCase(9, 1.6447, 1.2782, 0.8500, 0.3497, Area.Area3)]
        [TestCase(10, 1.7779, 1.3245, 0.8500, 0.4481, Area.Area3)]
        [TestCase(11, 1.9280, 1.3400, 0.8500, 0.5687, Area.Area3)]
        [TestCase(12, 2.0974, 1.3400, 0.8500, 0.7166, Area.Area3)]
        [TestCase(13, 2.2883, 1.3400, 0.8500, 0.8980, Area.Final)]
        [TestCase(14, 2.5035, 1.3400, 0.8500, 1.1205, Area.Final)]
        [TestCase(20, 4.5107, 1.3400, 0.8500, 4.0146, Area.Final)]
        [TestCase(30, 13.5839, 1.3400, 0.8500, 28.0000, Area.Final)]
        [TestCase(50, 143.7237, 1.3400, 0.8500, 28.0000, Area.Final)]
        [TestCase(100, 57740.8203, 1.3400, 0.8500, 28.0000, Area.Final)]
        public void TheLadderMatchesTheMeasuredJavaScriptValues(
            int floor, double tough, double rate, double pack, double bodies, Area area)
        {
            Assert.That(Balance.DepthTough(floor), Is.EqualTo(tough).Within(0.005),
                "health scaling at floor " + floor);
            Assert.That(Balance.DepthRate(floor), Is.EqualTo(rate).Within(0.005),
                "rate scaling at floor " + floor);
            // The pack chance is not a pure function of the floor on the JS side: `depthPack()` takes no argument and reads... [h:DepthLadderParityTests-3]
            Assert.That(Balance.DepthPack(floor, Bodies.MixFor(area).Brunch),
                Is.EqualTo(pack).Within(0.0005),
                "pack chance at floor " + floor + " - the base comes from the area, the step from the floor");
            Assert.That(Balance.DepthBodies(floor, 0), Is.EqualTo(bodies).Within(0.001),
                "density at floor " + floor + " - the JS figure is depthBodies(0), not the whole room");
            Assert.That(AreaRules.AreaForFloor(floor), Is.EqualTo(area),
                "area at floor " + floor + " - the theme block, which decides the enemy mix");
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
            // BothDialsStopAtTheirCeiling is GONE, and its removal is the change rather than an omission. [h:DepthLadderParityTests-4]
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
            // The ladder is EXPONENTIAL and health is UNBOUNDED, which inverts this file's previous contract. [h:DepthLadderParityTests-5]
            double t14 = Balance.DepthTough(14), t50 = Balance.DepthTough(50), t200 = Balance.DepthTough(200);
            Assert.That(t50, Is.GreaterThan(t14 * 4),
                "floor 50 is only " + t50 + " against floor 14's " + t14 + ", so the climb has flattened");
            Assert.That(t200, Is.GreaterThan(t50 * 4), "floor 200 is not meaningfully past floor 50");
        }

        [Test]
        public void TheStepsGrowRatherThanShrink()
        {
            // The design claim, and the assertion a monotonic-only test cannot make: a saturating ladder is perfectly... [h:DepthLadderParityTests-6]
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
            // THE fairness property, and the reason the RATE dial is capped even though health is not. [h:DepthLadderParityTests-7]
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
            // Density is capped too, and for a DIFFERENT reason than the rate: not fairness but playability. [h:DepthLadderParityTests-8]
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
            // There is nothing to assert here that is not in the signature: none of these take a player, a stat, a health... [h:DepthLadderParityTests-9]
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
