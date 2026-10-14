using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The spawn planner, checked against plans read out of the running JavaScript. [h:SpawnPlanParityTests-1]</summary>
    [TestFixture]
    public sealed class SpawnPlanParityTests
    {
        // READ OUT OF THE RUNNING JAVASCRIPT: startGame(seed); run.floor = floor; room =... [h:SpawnPlanParityTests-2]
        [TestCase(1u, 1, Dir.N, 649,
            "g142,499 l666,458 l405,497 s163,215",
            "g142,499 l666,458 l405,497 s163,215")]
        [TestCase(1u, 1, Dir.S, 649,
            "g662,210 l128,261 l392,207 s652,496",
            "g662,210 l128,261 l392,207 s652,496")]
        [TestCase(12345u, 1, Dir.N, 653,
            "g134,498 l665,452 b394,477pk6 s131,238 s668,213",
            "g134,498 l665,452 b407,477 b406,499 b387,488 b369,477 b387,466 b406,455 s131,238 s668,213")]
        [TestCase(12345u, 1, Dir.N, 653,
            "g134,498 l665,452 b394,477pk6 s131,238 s668,213",
            "g134,498 l665,452 b407,477 b406,499 b387,488 b369,477 b387,466 b406,455 s131,238 s668,213")]
        [TestCase(12345u, 5, Dir.E, 652,
            "g134,498 l131,238 b391,239pk6 s473,498 l668,213",
            "g134,498 l131,238 b404,239 b403,261 b384,250 b366,239 b384,228 b403,217 s473,498 l668,213")]
        [TestCase(99999u, 12, Dir.W, 649,
            "g674,223 b655,503pk5 l128,503",
            "g674,223 b668,503 b663,527 b644,511 b635,488 b659,491 l128,503")]
        [TestCase(99999u, 1, Dir.N, 649,
            "g128,503 b671,466pk5 l405,502",
            "g128,503 b684,466 b679,489 b661,473 b651,451 b675,453 l405,502")]
        [TestCase(777u, 1, Dir.S, 644,
            "l170,207 l669,279",
            "l170,207 l669,279")]
        [TestCase(31337u, 3, Dir.N, 650,
            "g650,498 l140,486 l408,496 l131,224 s664,215",
            "g650,498 l140,486 l408,496 l131,224 s664,215")]
        [TestCase(42u, 1, Dir.W, 647,
            "s670,216 b665,498pk4",
            "s670,216 b678,498 b665,523 b652,498 b665,473")]
        public void APlanMatchesTheJavaScriptExactly(
            uint seed, int floor, Dir dir, int draws, string slots, string bodies)
        {
            // The JavaScript measured from a freshly started run, and startGame consumes draws for the
            // dungeon. So: seed, generate a floor to burn the same stream, and only then plan.
            var rng = new Rng(seed);
            var dungeon = Dungeon.FromSeed(rng);
            Assert.That(dungeon.AllRooms.Count, Is.GreaterThan(0), $"seed {seed} generated no floor");

            var planner = new WavePlanner(rng);
            var before = rng.RunCalls;
            var plan = planner.PlanWave(floor, dir);
            var used = rng.RunCalls - before;

            Assert.That(used, Is.EqualTo(draws),
                $"seed {seed} floor {floor} from {dir} drew {used} times, the JavaScript draws {draws}. " +
                "A count difference means the planner consumed the stream differently - look at the draw " +
                "order before the geometry.");
            Assert.That(Render(plan.Slots), Is.EqualTo(slots),
                $"seed {seed} floor {floor} from {dir}: slots differ.\n  port: {Render(plan.Slots)}" +
                $"\n  java: {slots}");
            Assert.That(Render(plan.Bodies), Is.EqualTo(bodies),
                $"seed {seed} floor {floor} from {dir}: bodies differ.\n  port: {Render(plan.Bodies)}" +
                $"\n  java: {bodies}");
        }

        /// <summary>The plan itself, positionally, at the precision it can actually be compared at. [h:SpawnPlanParityTests-3]</summary>
        [Test]
        public void PositionsAreExactAndDistancesAreWithinAThing()
        {
            var rng = new Rng(12345);
            var dungeon = Dungeon.FromSeed(rng);
            var planner = new WavePlanner(rng);
            var plan = planner.PlanWave(1, Dir.N);

            foreach (var s in plan.Slots)
            {
                Assert.That(s.X, Is.InRange(50.0 + Balance.SpawnMargin - 0.001, 750.0 - Balance.SpawnMargin + 0.001),
                    $"a slot at x={s.X} is inside the spawn margin");
                Assert.That(s.Y, Is.InRange(130.0 + Balance.SpawnMargin - 0.001, 580.0 - Balance.SpawnMargin + 0.001),
                    $"a slot at y={s.Y} is inside the spawn margin");
                // The middle of the room stays walkable. A body standing in it is a fight the player
                // cannot choose to walk into.
                var mid = Math.Sqrt((s.X - 400) * (s.X - 400) + (s.Y - 355) * (s.Y - 355));
                Assert.That(mid, Is.GreaterThanOrEqualTo(Balance.SpawnMid - 0.001),
                    $"a slot at ({s.X},{s.Y}) is {mid:F1}px from the middle, inside SPAWN_MID {Balance.SpawnMid}");
            }
        }

        /// <summary>No shooter starts inside the range a Scatter answers in. [h:SpawnPlanParityTests-4]</summary>
        [Test]
        public void NoShooterStartsInsideTheComfortableRange()
        {
            foreach (var seed in Seeds(300))
            {
                var rng = new Rng(seed);
                Dungeon.FromSeed(rng);
                var planner = new WavePlanner(rng);
                foreach (var dir in new[] { Dir.N, Dir.E, Dir.S, Dir.W })
                {
                    var plan = planner.PlanWave(1, dir);
                    foreach (var s in plan.Slots)
                    {
                        Assert.That(s.Kind == BodyKind.Shooter && s.FromEntry < Balance.SpawnFar,
                            Is.False,
                            $"seed {seed} from {dir}: a shooter starts {s.FromEntry:F1}px from the door, " +
                            $"inside SPAWN_FAR {Balance.SpawnFar}");
                    }
                }
            }
        }

        /// <summary>The wall can still form. [h:SpawnPlanParityTests-5]</summary>
        [Test]
        public void EveryPackIsOneIdSharedByEnoughBodiesToBeAWall()
        {
            var packsSeen = 0;
            var bodiesSeen = 0;
            var wallsFormed = 0;
            var sizesSeen = new HashSet<int>();

            foreach (var seed in Seeds(300))
            {
                var rng = new Rng(seed);
                Dungeon.FromSeed(rng);
                var planner = new WavePlanner(rng);
                for (var floor = 1; floor <= 4; floor++)
                {
                    var plan = planner.PlanWave(floor, Dir.N);
                    var byPack = plan.Bodies
                        .Where(b => b.PackId > 0)
                        .GroupBy(b => b.PackId)
                        .ToList();
                    foreach (var pack in byPack)
                    {
                        packsSeen++;
                        bodiesSeen += pack.Count();
                        sizesSeen.Add(pack.Count());
                        Assert.That(pack.Select(b => b.PackSlot).OrderBy(i => i).SequenceEqual(
                                Enumerable.Range(0, pack.Count())),
                            Is.True,
                            $"seed {seed} floor {floor}: pack {pack.Key} has slots " +
                            string.Join(",", pack.Select(b => b.PackSlot)) + " rather than 0.." +
                            (pack.Count() - 1) + ", so the wall has a hole in it");
                        if (pack.Count() >= Bodies.BrunchWallMin) wallsFormed++;
                    }
                }
            }

            Assert.That(packsSeen, Is.GreaterThan(100),
                $"only {packsSeen} packs across 1200 waves, so the wall rule is barely being tested");
            Assert.That(bodiesSeen, Is.GreaterThan(packsSeen * Bodies.BrunchPack[0]),
                $"only {bodiesSeen} bodies across {packsSeen} packs, so packs are not carrying a whole " +
                "wall's worth of bodies");
            Assert.That(wallsFormed, Is.GreaterThan(50),
                $"only {wallsFormed} of {packsSeen} packs reached BrunchWallMin " +
                $"{Bodies.BrunchWallMin}, so no pack can form a wall");

            // NOT "SOME PACKS DO NOT FORM WALLS". [h:SpawnPlanParityTests-6]
            Assert.That(sizesSeen.Count, Is.EqualTo(Bodies.BrunchPack.Length),
                $"pack sizes seen were {string.Join(",", sizesSeen.OrderBy(i => i))}, so a pack is not " +
                "one shape");
        }

        /// <summary>Pack ids are unique per PACK and never reused, and never reset by a floor change. [h:SpawnPlanParityTests-7]</summary>
        [Test]
        public void PackIdsAreUniquePerPackAndNeverReused()
        {
            var rng = new Rng(12345);
            Dungeon.FromSeed(rng);
            var planner = new WavePlanner(rng);

            var seen = new List<int>();
            for (var floor = 1; floor <= 12; floor++)
            {
                var plan = planner.PlanWave(floor, Dir.N);
                foreach (var id in plan.Bodies.Where(b => b.PackId > 0).Select(b => b.PackId).Distinct())
                {
                    Assert.That(seen.Contains(id), Is.False,
                        $"floor {floor} handed out pack id {id}, which an earlier floor already used");
                    seen.Add(id);
                }
            }

            Assert.That(seen.Count, Is.GreaterThan(5), "twelve floors produced almost no packs to check");
            Assert.That(seen.Distinct().Count(), Is.EqualTo(seen.Count), "a pack id was handed out twice");

            // AND THE CURSOR ADVANCES BY EXACTLY ONE PER PACK. [h:SpawnPlanParityTests-8]
            Assert.That(planner.PacksIssued, Is.EqualTo(seen.Count),
                $"the planner issued {planner.PacksIssued} ids for {seen.Count} packs, so the counter " +
                "is advancing somewhere other than once per pack - which is the shape finding #1 had " +
                "before anyone noticed that no two bodies agreed on which pack they were in");

            // Two planners must not share a cursor, which is what makes the scoping structural rather
            // than a matter of remembering to reset something.
            var fresh = new WavePlanner(rng);
            Assert.That(fresh.PacksIssued, Is.EqualTo(0), "a new planner started mid-run");
        }

        /// <summary>The pack sizes and their weights, read out of the browser.</summary>
        [Test]
        public void RollPackMatchesTheJavaScript()
        {
            // Measured from a FRESH seed, not from a started run. [h:SpawnPlanParityTests-9]
            var rng = new Rng(12345);
            var planner = new WavePlanner(rng);
            var got = new List<int>();
            for (var i = 0; i < 60; i++) got.Add(planner.RollPack());
            var expected = new[]
            {
                8, 5, 5, 7, 5, 5, 4, 7, 8, 7, 5, 8, 7, 8, 6, 4, 5, 5, 7, 7, 6, 7, 7, 6, 5, 4, 5, 4,
                7, 4, 4, 4, 6, 8, 5, 4, 4, 6, 7, 4, 6, 6, 8, 4, 6, 8, 6, 7, 5, 4, 4, 5, 6, 5, 4, 7,
                5, 7, 4, 4,
            };
            Assert.That(got, Is.EqualTo(expected),
                $"rollPack drew {string.Join(",", got)}\n  java: {string.Join(",", expected)}");
        }

        /// <summary>The pack sizes come out at the weights they claim. [h:SpawnPlanParityTests-10]</summary>
        [Test]
        public void PackSizesComeOutAtTheirClaimedWeights()
        {
            Assert.That(Bodies.BrunchPack, Is.EqualTo(new[] { 4, 5, 6, 7, 8 }));
            Assert.That(Bodies.BrunchWeight.Sum(), Is.EqualTo(1.0).Within(1e-12),
                "the weights must sum to exactly 1, because the roll falls off the end of the table");

            var n = 200000;
            var counts = new Dictionary<int, int>();
            var rng = new Rng(1);
            var planner = new WavePlanner(rng);
            for (var i = 0; i < n; i++)
            {
                rng.Set((uint)(i + 1) * 0x9E3779B1u);
                var size = planner.RollPack();
                counts.TryGetValue(size, out var c);
                counts[size] = c + 1;
            }

            for (var i = 0; i < Bodies.BrunchPack.Length; i++)
            {
                var want = Bodies.BrunchWeight[i] * n;
                var got = counts[Bodies.BrunchPack[i]];
                Assert.That(got, Is.EqualTo((int)want).Within((int)(want * 0.01)),
                    $"pack size {Bodies.BrunchPack[i]} came out {got} times in {n} rolls, the weight " +
                    $"{Bodies.BrunchWeight[i]} predicts about {want:F0}");
            }
        }

        /// <summary>A room never has more bodies than slots ask for, or fewer than two.</summary>
        [Test]
        public void EveryWaveHasAtLeastTwoBodiesAndOneForEachSlot()
        {
            foreach (var seed in Seeds(200))
            {
                var rng = new Rng(seed);
                Dungeon.FromSeed(rng);
                var planner = new WavePlanner(rng);
                for (var floor = 1; floor <= 6; floor++)
                {
                    var plan = planner.PlanWave(floor, Dir.W);
                    Assert.That(plan.Bodies.Count, Is.GreaterThanOrEqualTo(2),
                        $"seed {seed} floor {floor} planned {plan.Bodies.Count} bodies, and a one-body " +
                        "room is an obstacle rather than a fight");
                    Assert.That(plan.Slots.Count, Is.GreaterThanOrEqualTo(2),
                        $"seed {seed} floor {floor} planned {plan.Slots.Count} slots");
                    // At most one gunner: it is the body that outranges the rest and one is a choice,
                    // three is a firing line.
                    Assert.That(plan.Slots.Count(s => s.Kind == BodyKind.Gunner), Is.LessThanOrEqualTo(1),
                        $"seed {seed} floor {floor} planned more than one gunner");
                    // A pack never takes the gunner's slot.
                    Assert.That(plan.Slots[0].Kind == BodyKind.Brunch, Is.False,
                        $"seed {seed} floor {floor}: the pack took slot 0, which is the gunner's");
                }
            }
        }

        /// <summary>The entry point is inside the wall, not on it.</summary>
        [Test]
        public void TheEntryPointIsWhereTheDoorIs()
        {
            Assert.That(WavePlanner.EntryPoint(Dir.N), Is.EqualTo((400.0, 130.0 + 36.0)));
            Assert.That(WavePlanner.EntryPoint(Dir.S), Is.EqualTo((400.0, 580.0 - 36.0)));
            Assert.That(WavePlanner.EntryPoint(Dir.E), Is.EqualTo((750.0 - 36.0, 355.0)));
            Assert.That(WavePlanner.EntryPoint(Dir.W), Is.EqualTo((50.0 + 36.0, 355.0)));
        }

        // `Letter(k) + Math.Round(x)` IS ARITHMETIC IN C#, NOT CONCATENATION. [h:SpawnPlanParityTests-11]
        private static string Render(List<SpawnSlot> slots) =>
            string.Join(" ", slots.Select(s =>
                Letter(s.Kind) + Math.Round(s.X).ToString() + "," + Math.Round(s.Y).ToString() +
                (s.Pack > 0 ? "pk" + s.Pack : "")));

        /// <summary>The lowercase letter the JavaScript room/body strings use, written out rather than derived from the enum... [h:SpawnPlanParityTests-12]</summary>
        private static char Letter(BodyKind k) => k switch
        {
            BodyKind.Lunger => 'l',
            BodyKind.Brunch => 'b',
            BodyKind.Shooter => 's',
            BodyKind.Gunner => 'g',
            BodyKind.Boss => 'o',
            _ => '?',
        };

        /// <summary>Bodies rendered by position and kind, with pack membership NOT rendered. [h:SpawnPlanParityTests-13]</summary>
        private static string Render(List<PlannedBody> bodies) =>
            string.Join(" ", bodies.Select(b =>
                Letter(b.Kind) + Math.Round(b.X).ToString() + "," + Math.Round(b.Y).ToString()));

        private static IEnumerable<uint> Seeds(int n)
        {
            for (uint i = 0; i < n; i++) yield return i * 0x9E3779B1u;
        }
    }
}