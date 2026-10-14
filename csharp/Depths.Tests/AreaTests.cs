using System;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The themed-area split of the climb. [h:AreaTests-1]</summary>
    [TestFixture]
    public sealed class AreaTests
    {
        [TestCase(1, Area.Area1)]
        [TestCase(4, Area.Area1)]
        [TestCase(5, Area.Area2)]
        [TestCase(8, Area.Area2)]
        [TestCase(9, Area.Area3)]
        [TestCase(12, Area.Area3)]
        [TestCase(13, Area.Final)]
        [TestCase(14, Area.Final)]
        [TestCase(15, Area.Final)]
        [TestCase(100, Area.Final)]
        public void AreaForFloorFollowsTheLadder(int floor, Area want)
        {
            Assert.That(AreaRules.AreaForFloor(floor), Is.EqualTo(want),
                $"floor {floor} should be {want}");
        }

        [Test]
        public void EndlessStaysFinal()
        {
            // The theme does not cycle back past the boss. A wrap to Area1 at floor 15 would make
            // the late game a second copy of the early game wearing a different coat.
            Assert.That(AreaRules.AreaForFloor(15), Is.EqualTo(Area.Final));
            Assert.That(AreaRules.AreaForFloor(500), Is.EqualTo(Area.Final));
        }

        [Test]
        public void Area1IsTheMeasuredMix()
        {
            // Area1 must be the pre-area numbers exactly, or every pinned parity row moves. These
            // are the measured values; the other areas are explicit guesses and are NOT pinned.
            var m = Bodies.MixFor(Area.Area1);
            Assert.That(m.Lunger, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(m.Heavy, Is.EqualTo(0.55).Within(1e-12));
            Assert.That(m.Brunch, Is.EqualTo(Bodies.BrunchChance).Within(1e-12));
        }

        [Test]
        public void DefaultAreaIsArea1ByteForByte()
        {
            // Same seed, same floor, same direction: omitting the area argument must produce the
            // same plan as asking for Area1, or the parity rows quietly changed meaning.
            var a = new RunState(new Rng(4242u));
            a.dungeon.Generate();
            var b = new RunState(new Rng(4242u));
            b.dungeon.Generate();

            var pa = a.planner.PlanWave(6, Dir.E);
            var pb = b.planner.PlanWave(6, Dir.E, Area.Area1);

            Assert.That(pa.Bodies.Select(x => (x.Kind, x.X, x.Y, x.PackId)),
                        Is.EqualTo(pb.Bodies.Select(x => (x.Kind, x.X, x.Y, x.PackId))),
                        "the default-area plan differs from the explicit Area1 plan");
            Assert.That(pa.Slots.Select(x => (x.Kind, x.X, x.Y, x.Pack)),
                        Is.EqualTo(pb.Slots.Select(x => (x.Kind, x.X, x.Y, x.Pack))));
            Assert.That(a.rng.RunCalls, Is.EqualTo(b.rng.RunCalls),
                "the two plans consumed the run stream differently - the default is not free");
        }

        [Test]
        public void ADifferentAreaPlansADifferentRoomSomeOfTheTime()
        {
            // Across a deterministic set of seeds, asking for a non-Area1 area must change what gets planned at least once... [h:AreaTests-2]
            var changed = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var a = new RunState(new Rng(seed));
                a.dungeon.Generate();
                var b = new RunState(new Rng(seed));
                b.dungeon.Generate();

                var pa = a.planner.PlanWave(9, Dir.N);
                var pb = b.planner.PlanWave(9, Dir.N, Area.Final);

                if (!pa.Bodies.Select(x => x.Kind).SequenceEqual(pb.Bodies.Select(x => x.Kind)) ||
                    pa.Bodies.Count != pb.Bodies.Count)
                    changed++;
            }
            Assert.That(changed, Is.GreaterThan(0),
                "40 seeds and the area argument never changed a plan - the dial is not connected");
        }

        [Test]
        public void ThePlannerActuallyReadsTheMix()
        {
            // The dials are decorative unless the planner consumes them. [h:AreaTests-3]
            int CountKind(Area area, BodyKind want)
            {
                var total = 0;
                for (uint seed = 1; seed <= 60; seed++)
                {
                    var rs = new RunState(new Rng(seed));
                    rs.dungeon.Generate();
                    total += rs.planner.PlanWave(1, Dir.N, area).Bodies
                              .Count(b => b.Kind == want);
                }
                return total;
            }
            Assert.That(CountKind(Area.Final, BodyKind.Gunner),
                        Is.Not.EqualTo(CountKind(Area.Area1, BodyKind.Gunner)),
                "60 seeds, identical gunner counts in Area1 and Final - the heavy dial is not wired");

            // Lunger SHARE among the two melee-or-shell slot kinds. [h:AreaTests-4]
            double LungerShare(Area area)
            {
                var l = 0; var s = 0;
                for (uint seed = 1; seed <= 120; seed++)
                {
                    var rs = new RunState(new Rng(seed));
                    rs.dungeon.Generate();
                    var bodies = rs.planner.PlanWave(1, Dir.N, area).Bodies;
                    l += bodies.Count(b => b.Kind == BodyKind.Lunger);
                    s += bodies.Count(b => b.Kind == BodyKind.Shooter);
                }
                return l / (double)(l + s);
            }
            var a1 = LungerShare(Area.Area1);
            var a2 = LungerShare(Area.Area2);
            Assert.That(Math.Abs(a1 - a2), Is.GreaterThan(0.01),
                $"lunger share Area1 {a1:F3} vs Area2 {a2:F3} - the type roll is not wired");
        }

        [Test]
        public void TheAreaLookupSpendsNoRng()
        {
            // AreaForFloor is pure: calling it between draws must not move the stream, or every
            // seeded replay after floor 1 would desync from the JavaScript.
            var rng = new Rng(99u);
            rng.Run();
            AreaRules.AreaForFloor(7);
            var after = rng.Run();
            var rng2 = new Rng(99u);
            rng2.Run();
            var straight = rng2.Run();
            Assert.That(after, Is.EqualTo(straight),
                "AreaForFloor consumed from the run stream");
        }
    }
}
