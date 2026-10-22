using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>Spike rooms (Spikes.cs, owner 2026-10-22): floors 5-12, an elite or a crowded wave, a sure drop.</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class SpikeTests
    {
        // a run on `floor` of `seed`, reached the game's way
        static RunState OnFloor(uint seed, int floor)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            for (int f = 1; f < floor; f++) run.Descend();
            return run;
        }

        static IEnumerable<Room> SpikeRooms(RunState run) => run.dungeon.AllRooms.Where(r => r.Spike != SpikeKind.None);

        [Test]
        public void SpikesAreOnFloorsFiveToTwelveOnlyAtMostTwoNeverBesideTheStartAndAlwaysNormal()
        {
            var perFloor = new double[15];
            int elite = 0, crowded = 0;
            for (uint seed = 1; seed <= 120; seed++)
            {
                var run = OnFloor(seed, 1);
                for (int f = 1; f <= 14; f++)
                {
                    if (f > 1) run.Descend();
                    var s = SpikeRooms(run).ToList();
                    if (f < Spikes.FirstFloor || f > Spikes.LastFloor) Assert.That(s, Is.Empty, "a spike on floor " + f);
                    Assert.That(s.Count, Is.LessThanOrEqualTo(Spikes.MostAFloor));
                    foreach (var r in s)
                    {
                        Assert.That(r.Type, Is.EqualTo(RoomKind.Normal), "a spike that is not a normal room");
                        Assert.That(Math.Abs(r.X - Map.Start) + Math.Abs(r.Y - Map.Start), Is.GreaterThan(1), "a spike beside the start");
                    }
                    perFloor[f] += s.Count;
                    elite += s.Count(r => r.Spike == SpikeKind.Elite); crowded += s.Count(r => r.Spike == SpikeKind.Crowded);
                }
            }
            Assert.That(elite, Is.GreaterThan(30)); Assert.That(crowded, Is.GreaterThan(30), "both forms turn up");
            Assert.That(perFloor[12], Is.GreaterThan(perFloor[5]), "spikes grow commoner with depth");
            Assert.That(perFloor[5] / 120, Is.InRange(0.3, 1.6), "about one a floor on floor 5");
        }

        [Test]
        public void MarkingDrawsNothingFromTheRunStreamAndIsTheSameEveryTime()
        {
            string Rooms(RunState r) => string.Join(",", r.dungeon.AllRooms.Select(x => x.X + ":" + x.Y + ":" + x.Spike));
            for (uint seed = 1; seed <= 20; seed++)
            {
                var a = OnFloor(seed, 8);
                var b = OnFloor(seed, 8);
                Assert.That(Rooms(b), Is.EqualTo(Rooms(a)), "seed " + seed + " marks different rooms twice");
                bool was = Balance.TuneSpike;
                try
                {
                    Balance.TuneSpike = false;
                    var off = OnFloor(seed, 8);
                    Assert.That(off.rng.RunCalls, Is.EqualTo(a.rng.RunCalls), "marking spikes took draws from the run stream");
                    Assert.That(off.dungeon.Signature(), Is.EqualTo(a.dungeon.Signature()), "marking spikes changed the floor");
                    Assert.That(SpikeRooms(off), Is.Empty, "switched off, there are still spikes");
                }
                finally { Balance.TuneSpike = was; }
            }
        }

        [Test]
        public void ACrowdedWaveHasHalfAgainTheBodiesAndAnExtraPack()
        {
            int more = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var normal = new WavePlanner(new Rng(seed)).PlanWave(8, Dir.S, Area.Area2);
                var crowded = new WavePlanner(new Rng(seed)).PlanWave(8, Dir.S, Area.Area2, crowd: Spikes.Crowd);
                Assert.That(crowded.Slots.Count, Is.GreaterThanOrEqualTo((int)Math.Floor(normal.Slots.Count * Spikes.Crowd) - 1), "seed " + seed);
                Assert.That(crowded.Slots.Count(s => s.Pack > 0), Is.GreaterThanOrEqualTo(normal.Slots.Count(s => s.Pack > 0) + 1), "no extra pack");
                if (crowded.Bodies.Count > normal.Bodies.Count) more++;
            }
            Assert.That(more, Is.EqualTo(40), "a crowded wave was not bigger");
        }

        [Test]
        public void EnteringASpikeRoomSpawnsItsSpike()
        {
            // the same room on the same seed, entered as itself and as each spike: the wave answers to the mark
            (int bodies, int elites) Enter(uint seed, SpikeKind k)
            {
                var run = OnFloor(seed, 7);
                var r = run.dungeon.AllRooms.First(x => x.Type == RoomKind.Normal && !x.Spawned);
                r.Spike = k;
                Rooms.EnterRoom(run, r.X, r.Y, Dir.S);
                return (run.enemies.Count, run.enemies.Count(e => e.elite));
            }
            int bigger = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                var plain = Enter(seed, SpikeKind.None);
                Assert.That(plain.elites, Is.EqualTo(0));
                if (Enter(seed, SpikeKind.Crowded).bodies > plain.bodies) bigger++;
                var elite = Enter(seed, SpikeKind.Elite);
                Assert.That(elite.elites, Is.EqualTo(1), "an elite room without its elite (seed " + seed + ")");
                Assert.That(elite.bodies, Is.InRange(plain.bodies, plain.bodies + 1), "an elite room changed its wave beyond the one body a crownless wave gains");
            }
            Assert.That(bigger, Is.EqualTo(20), "entering a crowded room did not bring the bigger wave");
        }

        [Test]
        public void TheEliteIsTheWavesStrongestBodyNeverABrunchOrAnEgg()
        {
            var run = OnFloor(7, 6);
            run.enemies.Clear();
            var lunger = Spawn.Body(run, BodyKind.Lunger, 300, 300);
            var gunner = Spawn.Body(run, BodyKind.Gunner, 400, 300);
            var brunch = Spawn.Body(run, BodyKind.Brunch, 500, 300);
            var egg = Spawn.Body(run, BodyKind.Egg, 600, 300);
            var burrower = Spawn.Body(run, BodyKind.Burrower, 650, 300);
            brunch.maxHp = egg.maxHp = burrower.maxHp = 1e6;   // the biggest bodies here, and still never the elite
            lunger.maxHp = gunner.maxHp / 2;                   // the gunner is the strongest that may be: a ranged body, so its cadence shows
            run.enemies.AddRange(new[] { lunger, gunner, brunch, egg, burrower });
            var strongest = gunner;
            double hp = strongest.maxHp, cd = strongest.cdMin;
            Assert.That(cd, Is.GreaterThan(0), "fixture: the gunner has no cadence to quicken");
            var e = Spikes.MakeElite(run);
            Assert.That(e, Is.SameAs(strongest));
            Assert.That(e!.elite && !brunch.elite && !egg.elite);
            Assert.That(e.maxHp, Is.EqualTo(hp * Spikes.EliteHp).Within(1e-9));
            Assert.That(e.hp, Is.EqualTo(e.maxHp));
            Assert.That(e.cdMin, Is.EqualTo(cd / Spikes.EliteRate).Within(1e-9), "its cadence is not quicker");
            Assert.That(burrower.elite, Is.False, "a Burrower was made the elite");
            run.enemies.Clear(); run.enemies.Add(egg);
            Assert.That(Spikes.MakeElite(run), Is.Null, "an egg alone was made elite");
        }

        [Test]
        public void AWaveWithNoBodyToCrownGainsALungerAsItsEliteOffTheSlotsBody()
        {
            var run = OnFloor(7, 6);
            run.enemies.Clear();
            var egg = Spawn.Body(run, BodyKind.Egg, 600, 250);
            run.enemies.Add(egg);
            run.enemies.Add(Spawn.Body(run, BodyKind.Brunch, 300, 300));
            var e = Spikes.Crown(run, egg.x, egg.y);
            Assert.That(e.kind, Is.EqualTo(Spikes.JoinKind(run)));
            Assert.That(Pools.Weight(run.pools!, run.floor, e.kind), Is.GreaterThan(0), "the joining elite is not one of the floor's kinds");
            Assert.That(e.elite);
            Assert.That(run.enemies, Has.Count.EqualTo(3));
            Assert.That(Math.Sqrt((e.x - egg.x) * (e.x - egg.x) + (e.y - egg.y) * (e.y - egg.y)), Is.EqualTo(Spikes.CrownStep).Within(1e-6), "it stands on the egg");
        }

        [Test]
        public void AnElitesLungeEndsAtItsDrawnLine()
        {
            // the telegraph (WorldView) and the bot read lungeLen: an elite's pace must not carry the lunge past it
            var run = OnFloor(3, 6);
            run.enemies.Clear(); run.projectiles.Clear(); run.readyT = 0; run.fadeT = 0; run.state = "playing";
            var e = Spawn.Body(run, BodyKind.Lunger, Balance.MidX - 150, Balance.MidY);
            e.noticeTimer = 0; e.alerted = true; e.aggroTimer = int.MaxValue / 2; e.elite = true;
            e.lungeState = "wind"; e.lungeT = 1; e.lungeDx = 1; e.lungeDy = 0; e.lungeLen = 120;
            run.enemies.Add(e);
            var p = run.player;
            p.x = p.lagX = Balance.RoomRight - 60; p.y = p.lagY = Balance.RoomTop + 40;   // well off its line
            TickOrder.HitboxX = p.x; TickOrder.HitboxY = p.y;
            double x0 = e.x;
            for (int t = 0; t < Balance.Sec(1) && e.lungeState != "recover"; t++) { TickOrder.TickPlayer(run, Input.None); TickOrder.TickBodies(run); }
            Assert.That(e.lungeState, Is.EqualTo("recover"), "fixture: the lunge never ended");
            Assert.That(e.x - x0, Is.LessThanOrEqualTo(e.lungeLen + Balance.LungeSpeed + 1e-6), "an elite lunged past its line");
        }

        [Test]
        public void SpikesNeedTheLootRuleThatPaysThem()
        {
            bool was = Balance.TuneLoot;
            try
            {
                Balance.TuneLoot = false;
                for (uint seed = 1; seed <= 20; seed++) Assert.That(SpikeRooms(OnFloor(seed, 9)), Is.Empty, "a spike room that cannot pay");
            }
            finally { Balance.TuneLoot = was; }
        }

        [Test]
        public void AnEliteMovesAFifthQuicker()
        {
            double Moved(bool elite)
            {
                var run = OnFloor(3, 6);
                run.enemies.Clear(); run.projectiles.Clear(); run.readyT = 0; run.fadeT = 0; run.state = "playing";
                var e = Spawn.Body(run, BodyKind.Shooter, Balance.RoomLeft + 60, Balance.MidY);
                e.noticeTimer = 0; e.alerted = true; e.shootCd = 1e9; e.elite = elite;
                run.enemies.Add(e);
                var p = run.player;
                p.x = p.lagX = Balance.RoomRight - 40; p.y = p.lagY = Balance.MidY;
                TickOrder.HitboxX = p.x; TickOrder.HitboxY = p.y;
                double x0 = e.x;
                for (int t = 0; t < Balance.Sec(1); t++) { TickOrder.TickPlayer(run, Input.None); TickOrder.TickBodies(run); }
                return e.x - x0;
            }
            double plain = Moved(false), quick = Moved(true);
            Assert.That(plain, Is.GreaterThan(20), "fixture: the shooter never walked in");
            Assert.That(quick / plain, Is.EqualTo(Spikes.EliteRate).Within(0.06));
        }

        [Test]
        public void ASpikeRoomAlwaysDropsAndItsEliteDropsArmourToo()
        {
            List<Pickup> Drops(uint seed, SpikeKind spike, bool elite)
            {
                var run = OnFloor(seed, 6);
                run.CurrentRoom!.Spike = spike;
                run.enemies.Clear(); run.pickups.Clear();
                var e = Spawn.Body(run, BodyKind.Lunger, 300, 300);
                e.elite = elite;
                run.enemies.Add(e);
                Kills.KillEnemy(run, 0);
                return run.pickups;
            }
            for (uint seed = 1; seed <= 40; seed++)
                Assert.That(Drops(seed, SpikeKind.Crowded, false), Has.Count.EqualTo(1), "a spike room's last kill dropped nothing (seed " + seed + ")");
            var both = Drops(5, SpikeKind.Elite, true);
            Assert.That(both, Has.Count.EqualTo(2), "the elite's armour and the room's drop");
            Assert.That(both[0].kind, Is.EqualTo("armor"));
            // control: an ordinary room's last kill drops only some of the time (the 75% rule), over many runs
            int none = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var run = OnFloor(seed, 6);
                run.CurrentRoom!.Spike = SpikeKind.None;
                run.enemies.Clear(); run.pickups.Clear();
                run.enemies.Add(Spawn.Body(run, BodyKind.Lunger, 300, 300));
                Kills.KillEnemy(run, 0);
                if (run.pickups.Count == 0) none++;
            }
            Assert.That(none, Is.GreaterThan(3), "control: ordinary rooms always dropped, so the test proves nothing");
        }
    }
}
