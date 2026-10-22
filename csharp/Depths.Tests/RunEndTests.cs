using System.Collections.Generic;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The run's end (Balance.TuneEnd, owner 2026-10-22): 14 floors, the last an antechamber and the final boss,
    /// whose death wins the run. Untuned, the JS's endless descent.</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class RunEndTests
    {
        static RunState OnFloor(uint seed, int floor)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            for (int f = 1; f < floor; f++) run.Descend();
            return run;
        }

        [Test]
        public void FloorFourteenIsAnAntechamberAndTheArenaNorthOfItOpen()
        {
            for (uint seed = 1; seed <= 10; seed++)
            {
                var run = OnFloor(seed, Balance.FinalFloor);
                var rooms = run.dungeon.AllRooms;
                Assert.That(rooms, Has.Count.EqualTo(2), "seed " + seed);
                var ante = run.CurrentRoom!;
                Assert.That(ante.Type, Is.EqualTo(RoomKind.Start));
                Assert.That(ante.Visited && ante.Spawned, "the antechamber is not walked like every floor's start (the bot stood in it for ever)");
                Assert.That(ante.Doors, Is.EquivalentTo(new[] { Dir.N }));
                var arena = run.dungeon.Neighbour(ante, Dir.N)!;
                Assert.That(arena.Type, Is.EqualTo(RoomKind.Boss));
                Assert.That(arena.Doors, Is.EquivalentTo(new[] { Dir.S }));
                Assert.That(run.bossUnlocked, "the arena is locked and there is no key down here");
                Assert.That(Rooms.DoorSealed(run, ante, Dir.N), Is.False);
                Assert.That(run.pickups.Select(p => p.kind), Is.EquivalentTo(new[] { "heart" }), "the antechamber's breath");
                Assert.That(rooms.Any(r => r.KeyReward || r.GoldReward || r.Spike != SpikeKind.None), Is.False);
            }
            Assert.That(OnFloor(3, Balance.FinalFloor - 1).dungeon.AllRooms.Count, Is.GreaterThan(5), "floor 13 is an ordinary floor");
        }

        static RunState InTheArena(int floor)
        {
            var run = OnFloor(9, floor);
            var arena = run.dungeon.AllRooms.First(r => r.Type == RoomKind.Boss);
            run.bossUnlocked = true;
            Rooms.EnterRoom(run, arena.X, arena.Y, Dir.S);
            run.readyT = 0; run.fadeT = 0; run.trans = null; run.state = "playing";
            Assert.That(run.enemies.Any(e => e.kind == BodyKind.Boss), "fixture: no boss in the arena");
            run.enemies.Clear();   // the boss (and its wall) down
            run.pickups.Clear();
            return run;
        }

        [Test]
        public void TheFinalBossesDeathWinsTheRunAndOpensNoWayDown()
        {
            var run = InTheArena(Balance.FinalFloor);
            TickOrder.TickRoom(run);
            Assert.That(run.state, Is.EqualTo("won"));
            Assert.That(run.pickups.Any(p => p.kind == "exit"), Is.False, "a way down below the last floor");
            TickOrder.TickRoom(run);
            Assert.That(run.state, Is.EqualTo("won"), "the win is not taken back");
        }

        [Test]
        public void ADeathOnTheTickTheFinalBossFallsIsADeath()
        {
            var run = InTheArena(Balance.FinalFloor);
            run.player.hp = 0; run.player.regenHeart = 0;
            TickOrder.TickRoom(run);
            Assert.That(run.state, Is.EqualTo("gameover"), "a dead player won the run");
        }

        [Test]
        public void ARunNeverGoesBelowTheLastFloor()
        {
            var run = InTheArena(Balance.FinalFloor);
            for (int t = 0; t < 10; t++) TickOrder.TickRoom(run);
            Assert.That(run.floor, Is.EqualTo(Balance.FinalFloor));
            Assert.That(run.finalFloor);
            Assert.That(OnFloor(2, Balance.FinalFloor - 1).finalFloor, Is.False);
        }

        [Test]
        public void TheBotWinsAWholeFinalFloorAndBooksTheFight()
        {
            // a skilled bot set down on floor 14: it walks the antechamber, fights the stand-in finale, and wins
            var r = new Runner(5, "skilled", 6);
            for (int f = 1; f < Balance.FinalFloor; f++) r.Run.Descend();
            var res = r.Play();
            Assert.That(res.end, Is.EqualTo("won"), "the bot did not win the last floor");
            var last = res.floors[res.floors.Count - 1];
            Assert.That(last.bossKilled, "a won floor is booked as left without its boss beaten");
            Assert.That(last.bossTicks, Is.GreaterThan(0));
        }

        [Test]
        public void ABossDeathAboveTheLastFloorOpensTheWayDownAsBefore()
        {
            var run = InTheArena(Balance.FinalFloor - 1);
            TickOrder.TickRoom(run);
            Assert.That(run.state, Is.EqualTo("playing"));
            Assert.That(run.pickups.Any(p => p.kind == "exit"), "no way down from floor 13");
        }

        [Test]
        public void UntunedTheDescentNeverEnds()
        {
            bool was = Balance.TuneEnd;
            try
            {
                Balance.TuneEnd = false;
                var run = OnFloor(4, Balance.FinalFloor);
                Assert.That(run.dungeon.AllRooms.Count, Is.GreaterThan(5), "switched off, floor 14 is still the final floor");
                var deep = InTheArena(Balance.FinalFloor);
                TickOrder.TickRoom(deep);
                Assert.That(deep.state, Is.EqualTo("playing"));
                Assert.That(deep.pickups.Any(p => p.kind == "exit"));
            }
            finally { Balance.TuneEnd = was; }
        }

        [Test]
        public void TheReportCountsWinsAndTheMinutesToOne()
        {
            var runs = new List<RunResult>
            {
                new RunResult { end = "won", ticks = 40 * 60 * 60 },
                new RunResult { end = "won", ticks = 50 * 60 * 60 },
                new RunResult { end = "won", ticks = 44 * 60 * 60 },
                new RunResult { end = "death", ticks = 10 * 60 * 60 },
            };
            var s = ProfileStats.Of(runs, 60);
            Assert.That(s.wins, Is.EqualTo(3));
            Assert.That(s.deaths, Is.EqualTo(1));
            Assert.That(s.minToWin, Is.EqualTo(44).Within(1e-9), "the median of the winning runs' minutes");
        }
    }
}
