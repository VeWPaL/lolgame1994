using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The run's area groups (Pools, Balance.TunePools; owner 2026-10-13): random per run, each area its own
    /// fight, the next area's newcomers previewed rarely two floors early; and the eggs' forms and the
    /// ranged volleys that keep off their own side.
    /// </summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class PoolTests
    {
        [Test]
        public void EachRunRollsItsOwnValidGroupsAndTheSameSeedTheSameOnes()
        {
            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 60; seed++)
            {
                var g = Pools.Roll(seed);
                Assert.That(Pools.Describe(Pools.Roll(seed)), Is.EqualTo(Pools.Describe(g)), "seed " + seed + " rolls the same groups twice");
                Assert.That(g, Has.Length.EqualTo(4));
                for (int a = 0; a < 4; a++)
                {
                    Assert.That(g[a].Distinct().Count(), Is.EqualTo(Pools.GroupSize), "four different kinds");
                    Assert.That(g[a].Any(Pools.Comes), "something comes to you: " + string.Join(",", g[a]));
                    Assert.That(g[a].Any(Pools.Shoots), "something shoots from afar: " + string.Join(",", g[a]));
                    if (a > 0) Assert.That(g[a].Intersect(g[a - 1]).Count(), Is.LessThanOrEqualTo(Pools.MaxShared), "a new area is a new fight");
                    seen.Add((Area)a + string.Join(",", g[a].OrderBy(k => k)));
                }
            }
            Assert.That(seen.Count, Is.GreaterThan(120), "60 runs x 4 areas: the groups vary");
            var everyKind = Enumerable.Range(1, 60).SelectMany(s => Pools.Roll((uint)s).SelectMany(x => x)).Distinct().Count();
            Assert.That(everyKind, Is.EqualTo(Pools.Kinds.Length), "every kind turns up somewhere");
        }

        [Test]
        public void NewcomersArePreviewedRarelyTwoFloorsEarly()
        {
            var g = new[]
            {
                new[] { BodyKind.Lunger, BodyKind.Brunch, BodyKind.Shooter, BodyKind.Gunner },
                new[] { BodyKind.Lunger, BodyKind.Splitter, BodyKind.Egg, BodyKind.Burrower },
                new[] { BodyKind.Mortar, BodyKind.Sprayer, BodyKind.Brunch, BodyKind.Burrower },
                new[] { BodyKind.Shooter, BodyKind.Gunner, BodyKind.Lunger, BodyKind.Sprayer },
            };
            Assert.That(Pools.Weight(g, 1, BodyKind.Lunger), Is.EqualTo(1));
            Assert.That(Pools.Weight(g, 2, BodyKind.Splitter), Is.EqualTo(0), "not yet on floor 2");
            Assert.That(Pools.Weight(g, 3, BodyKind.Splitter), Is.EqualTo(Pools.PreviewFar));
            Assert.That(Pools.Weight(g, 4, BodyKind.Splitter), Is.EqualTo(Pools.PreviewNear));
            Assert.That(Pools.Weight(g, 5, BodyKind.Splitter), Is.EqualTo(1), "its own area");
            Assert.That(Pools.Weight(g, 5, BodyKind.Shooter), Is.EqualTo(0), "the old area's kinds leave with it");
            Assert.That(Pools.Weight(g, 7, BodyKind.Mortar), Is.EqualTo(Pools.PreviewFar));
            Assert.That(Pools.Weight(g, 12, BodyKind.Gunner), Is.EqualTo(Pools.PreviewNear));
            Assert.That(Pools.Weight(g, 20, BodyKind.Egg), Is.EqualTo(0), "the Final area previews nothing");
            Assert.That(Pools.PreviewFar, Is.LessThan(Pools.PreviewNear).And.LessThan(0.5), "significantly rarer, and rarer the further out");
        }

        // every normal room of twelve floors, entered the way the game enters them
        static IEnumerable<(uint seed, int floor, RunState run)> Rooms(int seeds)
        {
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var run = new RunState(new Rng(seed));
                run.Start(seed);
                for (int floor = 1; floor <= 12; floor++)
                {
                    foreach (var r in run.dungeon.AllRooms.ToList())
                    {
                        if (r.Type != RoomKind.Normal) continue;
                        Depths.Rooms.EnterRoom(run, r.X, r.Y, r.Doors.FirstOrDefault());
                        yield return (seed, floor, run);
                    }
                    run.Descend();
                }
            }
        }

        [Test]
        public void ARunMeetsOnlyItsFloorsKindsAndTheMiddleFiveAmongThem()
        {
            int bodies = 0, middle = 0, previews = 0, groupKinds = 0;
            foreach (var (seed, floor, run) in Rooms(20))
            {
                Assert.That(run.pools, Is.Not.Null);
                int eggs = 0;
                foreach (var e in run.enemies)
                {
                    bodies++;
                    double w = Pools.Weight(run.pools!, floor, e.kind);
                    Assert.That(w, Is.GreaterThan(0), "seed " + seed + " floor " + floor + ": a " + e.kind + " outside " + Pools.Describe(run.pools!));
                    if (w < 1) previews++; else groupKinds++;
                    if (Middle.Is(e.kind)) middle++;
                    if (e.kind == BodyKind.Egg) eggs++;
                }
                Assert.That(eggs, Is.LessThanOrEqualTo(Pools.MaxEggs), "seed " + seed + " floor " + floor);
            }
            Assert.That(bodies, Is.GreaterThan(1000), "the sweep met enough bodies to mean something");
            Assert.That(middle, Is.GreaterThan(bodies / 10), "the middle five fight in runs now");
            Assert.That(previews, Is.GreaterThan(0), "the previews happen");
            Assert.That(previews, Is.LessThan(groupKinds / 5), "and are rare");
        }

        [Test]
        public void RailsOnlyInTheKilnWorksAndOrbitsRoundAnotherBody()
        {
            int rails = 0, orbits = 0;
            foreach (var (seed, floor, run) in Rooms(40))
                foreach (var e in run.enemies.Where(x => x.kind == BodyKind.Egg))
                {
                    if (e.eggForm == EggForm.Rail)
                    {
                        rails++;
                        Assert.That(AreaRules.AreaForFloor(floor), Is.EqualTo(Area.Area2), "seed " + seed + ": a rail on floor " + floor);
                    }
                    if (e.eggForm == EggForm.Orbit)
                    {
                        orbits++;
                        Assert.That(run.enemies, Does.Contain(e.eggHost), "it circles a body of its own room");
                        Assert.That(e.eggHost!.kind, Is.Not.EqualTo(BodyKind.Egg));
                    }
                }
            Assert.That(rails, Is.GreaterThan(0), "rails turn up");
            Assert.That(orbits, Is.GreaterThan(0), "orbits turn up");
        }

        [Test]
        public void UntunedARunHasNoPools()
        {
            Balance.TunePools = false;
            try
            {
                var run = new RunState(new Rng(5));
                run.Start(5);
                Assert.That(run.pools, Is.Null);
            }
            finally { Balance.TunePools = true; }
        }

        // ---- the eggs ----

        static RunState Room()
        {
            var run = new RunState(new Rng(31));
            run.Start(31);
            run.readyT = 0; run.fadeT = 0; run.state = "playing";
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
            var p = run.player;
            p.x = p.lagX = Balance.RoomLeft + 40; p.y = p.lagY = Balance.RoomBottom - 40;
            return run;
        }

        static void Step(RunState run)
        {
            TickOrder.TickPlayer(run, Input.None);
            TickOrder.TickProjectiles(run);
            TickOrder.TickBodies(run);
        }

        static Enemy Add(RunState run, BodyKind k, double x, double y)
        {
            var e = Spawn.Body(run, k, x, y);
            e.noticeTimer = 0; e.alerted = true;
            run.enemies.Add(e);
            return e;
        }

        [Test]
        public void AnOrbitingEggCirclesItsHostAndStaysWhenItFalls()
        {
            var run = Room();
            var host = Add(run, BodyKind.Shooter, Balance.MidX, Balance.MidY);
            host.shootCd = int.MaxValue / 2;
            var egg = Add(run, BodyKind.Egg, 0, 0);
            Eggs.Orbit(egg, host, 0);
            double a0 = egg.eggA;
            for (int t = 0; t < Balance.Sec(1); t++)
            {
                Step(run);
                double d = Math.Sqrt((egg.x - host.x) * (egg.x - host.x) + (egg.y - host.y) * (egg.y - host.y));
                Assert.That(d, Is.EqualTo(host.r + Eggs.OrbitGap).Within(3), "on its orbit at tick " + t);
            }
            Assert.That(egg.eggA - a0, Is.EqualTo(Eggs.OrbitSpin * Balance.Sec(1)).Within(Eggs.OrbitSpin * 2), "a second of turning");
            run.enemies.Remove(host);
            double x = egg.x, y = egg.y;
            for (int t = 0; t < Balance.Sec(1); t++) Step(run);
            Assert.That(egg.eggForm, Is.EqualTo(EggForm.Fixed));
            Assert.That(Math.Abs(egg.x - x) + Math.Abs(egg.y - y), Is.LessThan(1), "its host gone, it stays");
        }

        [Test]
        public void AnOrbitingEggsRingIsSmallerAndSlower()
        {
            var run = Room();
            var host = Add(run, BodyKind.Shooter, Balance.MidX, Balance.MidY - 150);
            var egg = Add(run, BodyKind.Egg, 0, 0);
            Eggs.Orbit(egg, host, Math.PI / 2);
            run.enemies.Remove(host);   // nothing for the ring to keep off
            egg.eggForm = EggForm.Orbit; egg.eggHost = null;
            Eggs.Ring(run, egg);
            Assert.That(run.projectiles, Has.Count.EqualTo(Eggs.OrbitRing));
            double v = Math.Sqrt(run.projectiles[0].vx * run.projectiles[0].vx + run.projectiles[0].vy * run.projectiles[0].vy);
            Assert.That(v, Is.EqualTo(egg.pspd * Eggs.OrbitShellSpeed).Within(1e-9));
        }

        [Test]
        public void EveryTrackStaysInsideTheRoomAndIsLongEnoughToRide()
        {
            for (int p = 0; p < Eggs.Patterns; p++)
                foreach (bool fx in new[] { false, true })
                    foreach (bool fy in new[] { false, true })
                    {
                        var (pts, loop) = Eggs.Track(p, fx, fy);
                        Assert.That(pts.Length, Is.GreaterThanOrEqualTo(8), Eggs.PatternNames[p]);
                        for (int i = 0; i < pts.Length; i += 2)
                        {
                            Assert.That(pts[i], Is.InRange(Balance.RoomLeft + Eggs.RailInset - 1, Balance.RoomRight - Eggs.RailInset + 1), Eggs.PatternNames[p] + " x");
                            Assert.That(pts[i + 1], Is.InRange(Balance.RoomTop + Eggs.RailInset - 1, Balance.RoomBottom - Eggs.RailInset + 1), Eggs.PatternNames[p] + " y");
                        }
                        Assert.That(Eggs.Length(pts, loop), Is.GreaterThan(600), Eggs.PatternNames[p] + " is a ride, not a hop");
                    }
            var names = Enumerable.Range(0, Eggs.Patterns).Select(p => Eggs.Track(p, false, false)).Select(t => string.Join(",", t.pts.Take(6).Select(v => Math.Round(v)))).Distinct().Count();
            Assert.That(names, Is.EqualTo(Eggs.Patterns), "eight different tracks");
        }

        [Test]
        public void ARailEggRidesItsTrackBackAndForthOrRound()
        {
            foreach (int pattern in new[] { 2, 0 })   // the zigzag is open, the loop is closed
            {
                var run = Room();
                var egg = Add(run, BodyKind.Egg, 0, 0);
                Eggs.OnRail(run, egg, pattern);
                egg.shootCd = int.MaxValue / 2;
                bool flipped = false;
                int dir = egg.railDir;
                double travelled = 0, px = egg.x, py = egg.y;
                for (int t = 0; t < Balance.Sec(30); t++)
                {
                    Step(run);
                    Eggs.At(egg.rail!, egg.railLoop, egg.railS, out var tx, out var ty);
                    Assert.That(Math.Abs(egg.x - tx) + Math.Abs(egg.y - ty), Is.LessThan(1e-6), "on the track");
                    travelled += Math.Sqrt((egg.x - px) * (egg.x - px) + (egg.y - py) * (egg.y - py));
                    px = egg.x; py = egg.y;
                    flipped |= egg.railDir != dir;
                }
                Assert.That(travelled, Is.EqualTo(Eggs.RailSpeed * Balance.Sec(30)).Within(Eggs.RailSpeed * Balance.Sec(30) * 0.25), "it rides at its speed");
                Assert.That(flipped, Is.EqualTo(!egg.railLoop), egg.railLoop ? "a loop goes round" : "an open track turns at its ends");
            }
        }

        // ---- friendly fire ----

        [Test]
        public void AnEggsRingKeepsOffALungerBesideIt()
        {
            var run = Room();
            var egg = Add(run, BodyKind.Egg, Balance.MidX, Balance.MidY);
            var lunger = Add(run, BodyKind.Lunger, Balance.MidX + 60, Balance.MidY);
            lunger.noticeTimer = int.MaxValue / 2;
            for (int k = 0; k < 20; k++)
            {
                run.projectiles.Clear();
                egg.castAim = k * 0.05;   // every ring rotation, one of which sends a shell straight through it
                Eggs.Ring(run, egg);
                foreach (var p in run.projectiles)
                    Assert.That(Movement.LineClear(run, egg, Math.Atan2(p.vy, p.vx), 420, p.r), "rotation " + k + ": a shell on the Lunger's line");
                Assert.That(run.projectiles.Count, Is.GreaterThanOrEqualTo(Middle.EggRing - 1), "turned, not emptied");
            }
        }

        [Test]
        public void ASprayersConeNeverCrossesAFriend()
        {
            var run = Room();
            var p = run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.RoomBottom - 60;
            var s = Add(run, BodyKind.Sprayer, Balance.MidX, Balance.RoomBottom - 300);
            var lunger = Add(run, BodyKind.Lunger, Balance.MidX + 30, Balance.RoomBottom - 190);   // between them, a little aside
            lunger.noticeTimer = int.MaxValue / 2; lunger.hp = lunger.maxHp = 1000;
            int volleys = 0;
            for (int t = 0; t < Balance.Sec(15) && volleys < 3; t++)
            {
                int n = run.projectiles.Count;
                Step(run);
                if (run.projectiles.Count > n) volleys++;
                lunger.x = Balance.MidX + 30; lunger.y = Balance.RoomBottom - 190;   // it holds still
                s.x = Balance.MidX; s.y = Balance.RoomBottom - 300;
            }
            for (int t = 0; t < Balance.Sec(2); t++) { Step(run); lunger.x = Balance.MidX + 30; lunger.y = Balance.RoomBottom - 190; }
            Assert.That(volleys, Is.GreaterThan(0), "it fired");
            Assert.That(lunger.hp, Is.EqualTo(1000), "no shell of its cone hit the Lunger");
        }

        // ---- the cold critic's findings, 2026-10-13 ----

        [Test]
        public void AMortarHitsAPlayerWhoRanThenStoodStill()
        {
            // the belief keeps its last heading after a stop: a lead on it marked 252 px off a standing player
            var run = Room();
            var p = run.player;
            p.x = p.lagX = Balance.RoomLeft + 100; p.y = p.lagY = Balance.MidY + 100;
            var m = Add(run, BodyKind.Mortar, Balance.MidX, Balance.RoomTop + 40);
            m.shootCd = int.MaxValue / 2;
            for (int t = 0; t < Balance.Sec(1); t++) TickOrder.TickPlayer(run, new Input(1, 0));
            for (int t = 0; t < Balance.Sec(3); t++) TickOrder.TickPlayer(run, Input.None);
            Assert.That(Math.Abs(p.beliefVx), Is.GreaterThan(0.5), "the fixture keeps a stale heading, or it tests nothing");
            Middle.Release(run, m);
            var lob = run.projectiles.Single();
            double off = Math.Sqrt((lob.tx - TickOrder.HitboxX) * (lob.tx - TickOrder.HitboxX) + (lob.ty - TickOrder.HitboxY - Balance.PlayerHitDy) * (lob.ty - TickOrder.HitboxY - Balance.PlayerHitDy));
            Assert.That(off, Is.LessThan(Middle.LobBlast / 2), "a standing player is under the mark");
        }

        [Test]
        public void TheMiddleRetuneIsOneSwitch()
        {
            Balance.TuneMiddle = false;
            try
            {
                Assert.That(Middle.SplitRange, Is.EqualTo(170)); Assert.That(Middle.SplitSpread, Is.EqualTo(0.42));
                Assert.That(Middle.LobBlast, Is.EqualTo(34)); Assert.That(Middle.SprayCone, Is.EqualTo(0.35));
                Assert.That(Middle.AimJitter(BodyKind.Sprayer), Is.EqualTo(1));
                double off = Spawn.Body(Room(), BodyKind.Sprayer, Balance.MidX, Balance.MidY).cdMin;
                Balance.TuneMiddle = true;
                double on = Spawn.Body(Room(), BodyKind.Sprayer, Balance.MidX, Balance.MidY).cdMin;
                Assert.That(on, Is.EqualTo(off * Middle.SprayCadence).Within(1e-9), "the Sprayer's faster cadence is part of it");
            }
            finally { Balance.TuneMiddle = true; }
            Assert.That(Middle.SplitRange, Is.EqualTo(120)); Assert.That(Middle.LobBlast, Is.EqualTo(56));
        }

        [Test]
        public void AnOrbitAgainstAWallStaysInsideAndShovesNoOne()
        {
            var run = Room();
            var host = Add(run, BodyKind.Shooter, Balance.RoomLeft + 20, Balance.MidY);
            host.shootCd = int.MaxValue / 2;
            var egg = Add(run, BodyKind.Egg, 0, 0);
            Eggs.Orbit(egg, host, Math.PI);   // its circle runs through the wall
            double hx = host.x;
            for (int t = 0; t < Balance.Sec(2); t++)
            {
                Step(run);
                Assert.That(egg.x, Is.GreaterThanOrEqualTo(Balance.RoomLeft + egg.r), "inside the room at tick " + t);
            }
            Assert.That(Math.Abs(host.x - hx), Is.LessThan(30), "its host is not shoved off the wall by a mass-40 egg");
        }

        [Test]
        public void ARidingEggKeepsRidingThroughItsNoticeDelay()
        {
            var run = Room();
            var egg = Add(run, BodyKind.Egg, 0, 0);
            Eggs.OnRail(run, egg, 4);
            egg.noticeTimer = Balance.Sec(1);
            double s0 = egg.railS;
            for (int t = 0; t < Balance.Sec(0.5); t++) Step(run);
            Assert.That(Math.Abs(egg.railS - s0), Is.GreaterThan(Eggs.RailSpeed * Balance.Sec(0.4)), "it rides before it notices");
        }

        [Test]
        public void OnlyMomentumGoesPastTheSpeedCap()
        {
            var run = Room();
            run.enemies.Add(Enemy.Of(BodyKind.Shooter, Balance.MidX, Balance.RoomTop + 30));
            double Top(double stat, double mom)
            {
                var p = run.player;
                p.x = Balance.RoomLeft + 60; p.y = Balance.MidY; p.vx = p.vy = 0; p.momentum = mom;
                double best = 0;
                for (int t = 0; t < Balance.Sec(0.6); t++)
                {
                    double x0 = p.x;
                    p.momentum = mom;
                    TickOrder.TickPlayer(run, new Input(1, 0), stat);
                    best = Math.Max(best, p.x - x0);
                }
                return best;
            }
            Assert.That(Top(0.6, 0), Is.EqualTo(Top(Balance.MoveSpeedHardCap, 0)).Within(1e-9), "a fast build with an empty meter keeps the JS cap");
            Assert.That(Top(Balance.MoveSpeedHardCap, 1), Is.GreaterThan(Top(Balance.MoveSpeedHardCap, 0) * 1.1), "momentum still adds past it");
        }

        // ---- the mortar's lead ----

        [Test]
        public void AMortarLeadsARunnerAndItsBlastIsWide()
        {
            var run = Room();
            var p = run.player;
            p.x = p.lagX = Balance.RoomLeft + 100; p.y = p.lagY = Balance.MidY + 100;
            var m = Add(run, BodyKind.Mortar, Balance.MidX, Balance.RoomTop + 40);
            m.shootCd = int.MaxValue / 2;
            for (int t = 0; t < Balance.Sec(1); t++) TickOrder.TickPlayer(run, new Input(1, 0));   // running right, settled
            Middle.Release(run, m);
            var lob = run.projectiles.Single();
            Assert.That(lob.tx - TickOrder.HitboxX, Is.GreaterThan(Balance.PlayerMove * Middle.LobFlight * 0.6), "it lands where a straight run takes them");
            Assert.That(lob.blastR, Is.EqualTo(Middle.LobBlast));
            Assert.That(Middle.LobBlast, Is.GreaterThanOrEqualTo(50), "significantly wider than the first 34 px");
        }
    }
}
