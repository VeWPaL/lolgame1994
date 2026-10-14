using System;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The middle enemies (C# only, 2026-10-12; Middle.cs). [h:MiddleTests-1]</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class MiddleTests
    {
        static readonly BodyKind[] Five = { BodyKind.Splitter, BodyKind.Mortar, BodyKind.Burrower, BodyKind.Sprayer, BodyKind.Egg };

        static RunState Started(uint seed = 777)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            run.readyT = 0; run.fadeT = 0; run.state = "playing";
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
            Place(run, Balance.MidX, Balance.MidY + 120);
            return run;
        }

        static void Place(RunState run, double x, double y)
        {
            var p = run.player;
            p.x = p.lagX = x; p.y = p.lagY = y; p.vx = p.vy = 0; p.kvx = p.kvy = 0;
        }

        static Enemy Add(RunState run, BodyKind k, double x, double y)
        {
            var e = Spawn.Body(run, k, x, y);
            e.noticeTimer = 0; e.alerted = true;
            run.enemies.Add(e);
            return e;
        }

        // the sandbox's tick: player (it sets the lagged hitbox the bodies read), shells, bodies
        static void Step(RunState run, int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
            {
                TickOrder.TickPlayer(run, Input.None);
                TickOrder.TickProjectiles(run);
                TickOrder.TickBodies(run);
            }
        }

        static double Health(RunState run) => run.player.hp + run.player.regenHeart + run.player.armor;

        [Test]
        public void TheOldBodiesAreUntouched()
        {
            foreach (var k in new[] { BodyKind.Lunger, BodyKind.Brunch, BodyKind.Shooter, BodyKind.Gunner, BodyKind.Boss })
            {
                Assert.That(Middle.Is(k), Is.False, k.ToString());
                Assert.That(Middle.CastTime(k), Is.EqualTo(Balance.CastTime), k + " keeps the shared cast time");
            }
            foreach (var k in Five) Assert.That(Middle.Is(k), k.ToString());
            Assert.That(Bodies.Rows.Length, Is.EqualTo(Enum.GetValues(typeof(BodyKind)).Length), "a row per kind, in kind order");
        }

        [Test, TunedOn(false)]
        public void UntunedNoneOfThemSpawnsInARun()
        {
            // every room of twelve floors on twenty seeds, entered the way the game enters them
            int bodies = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                var run = new RunState(new Rng(seed));
                run.Start(seed);
                for (int floor = 1; floor <= 12; floor++)
                {
                    foreach (var r in run.dungeon.AllRooms.ToList())
                    {
                        if (r.Type != RoomKind.Normal) continue;
                        Rooms.EnterRoom(run, r.X, r.Y, r.Doors.FirstOrDefault());
                        foreach (var e in run.enemies) { bodies++; Assert.That(Middle.Is(e.kind), Is.False, "seed " + seed + " floor " + floor + ": " + e.kind); }
                    }
                    run.Descend();
                }
            }
            Assert.That(bodies, Is.GreaterThan(1000), "the sweep met enough bodies to mean something");
        }

        [Test]
        public void ASplitterShellBurstsIntoThreeShardsAtItsRange()
        {
            var run = Started();
            Add(run, BodyKind.Splitter, Balance.MidX, Balance.RoomTop + 60);
            Projectile? shell = null;
            for (int t = 0; t < Balance.Sec(8) && shell == null; t++) { Step(run); shell = run.projectiles.FirstOrDefault(p => p.splitAt > 0); }
            Assert.That(shell, Is.Not.Null, "a splitter casts within eight seconds");
            Place(run, Balance.RoomLeft + 30, Balance.RoomBottom - 30);   // out of the way: the shell must reach its range
            for (int t = 0; t < Balance.Sec(3) && run.projectiles.Contains(shell!); t++) Step(run);
            var shards = run.projectiles.Where(p => p.dmg == Middle.SplitShardDmg && p.r == Middle.SplitShardR).ToList();
            Assert.That(shards.Count, Is.EqualTo(Middle.SplitCount));
            double from = Math.Sqrt((shards[0].ox - shell!.ox) * (shards[0].ox - shell.ox) + (shards[0].oy - shell.oy) * (shards[0].oy - shell.oy));
            Assert.That(from, Is.EqualTo(Middle.SplitRange).Within(shell.speed + Math.Sqrt(shell.vx * shell.vx + shell.vy * shell.vy)), "it bursts at its range, give or take one step");
            var heads = shards.Select(p => Math.Atan2(p.vy, p.vx)).OrderBy(a => a).ToList();
            Assert.That(heads[2] - heads[0], Is.EqualTo(2 * Middle.SplitSpread).Within(1e-9), "fanned evenly about the heading");
        }

        [Test]
        public void AMortarLandsOnTheMarkAfterItsFlightAndOnlyThere()
        {
            foreach (bool stay in new[] { true, false })
            {
                var run = Started();
                var m = Add(run, BodyKind.Mortar, Balance.MidX, Balance.RoomTop + 40);
                TickOrder.TickPlayer(run, Input.None);   // the hitbox the release reads
                Middle.Release(run, m);
                var lob = run.projectiles.Single();
                Assert.That(lob.lob && lob.tx == TickOrder.HitboxX, "it marks where the player stands");
                if (!stay) Place(run, run.player.x + 120, run.player.y);
                double before = Health(run);
                run.enemies.Clear();   // only the shell acts
                for (int t = 1; t < Middle.LobFlight; t++) { Step(run); Assert.That(run.projectiles, Has.Count.EqualTo(1), "still in flight at tick " + t); }
                Step(run);
                Assert.That(run.projectiles, Is.Empty, "it lands after exactly its flight");
                Assert.That(before - Health(run), Is.EqualTo(stay ? Middle.LobDmg : 0), stay ? "standing on the mark costs a heart" : "moving off the mark costs nothing");
            }
        }

        [Test]
        public void ABuriedBurrowerCannotBeShotBlastedOrTouched()
        {
            var run = Started();
            var b = Add(run, BodyKind.Burrower, run.player.x, run.player.y - 300);   // far off: still running for a while
            b.buried = true; b.move = "under"; b.moveT = 1000; b.curSpeed = 0; b.digTrust = 0.5;
            double hp = b.hp, health = Health(run);
            run.projectiles.Add(new Projectile { x = b.x, y = b.y, ox = b.x - 50, oy = b.y, vx = 0.01, r = 6, dmg = 50, friendly = true, dx = 1 });
            Blast.Explode(run, b.x, b.y);
            Blast.Explode(run, b.x, b.y, "hook");
            Assert.That(b.kvx, Is.EqualTo(0)); Assert.That(b.kvy, Is.EqualTo(0)); Assert.That(b.stun, Is.EqualTo(0), "no shove, no hold");
            Step(run, 5);
            Assert.That(b.move, Is.EqualTo("under"), "the fixture is in the state it claims");
            Assert.That(b.hp, Is.EqualTo(hp), "no shell, blast or field reaches it underground");
            Assert.That(Health(run), Is.EqualTo(health), "nor does it touch the player");
        }

        // dig, then follow it down: where it comes up, the health it took, where the player was, its speeds under the floor
        static (double x, double y, double took, double px, double py, double first, double top) Dive(uint seed, Action<RunState, int>? move)
        {
            var run = Started(seed);
            var b = Add(run, BodyKind.Burrower, Balance.MidX, Balance.RoomTop + 60);
            Place(run, Balance.RoomLeft + 120, Balance.MidY + 60);
            int t = 0;
            while (b.move != "under" && t++ < Balance.Sec(4)) Step(run);
            Assert.That(b.buried, "it dug in within four seconds");
            double before = Health(run), first = -1, top = 0;
            int under = 0;
            while (b.buried && t++ < Balance.Sec(8))
            {
                double x0 = b.x, y0 = b.y;
                bool diving = b.move == "under";
                if (move != null) move(run, under++);
                else Step(run);
                if (!diving) continue;
                double v = Math.Sqrt((b.x - x0) * (b.x - x0) + (b.y - y0) * (b.y - y0));
                if (first < 0) first = v;
                top = Math.Max(top, v);
            }
            Assert.That(b.buried, Is.False, "it came up");
            return (b.x, b.y, before - Health(run), run.player.x, run.player.y + Balance.PlayerHitDy, first, top);
        }

        static void WalkRight(RunState run, int _)
        {
            TickOrder.TickPlayer(run, new Input(1, 0));
            TickOrder.TickProjectiles(run);
            TickOrder.TickBodies(run);
        }

        const int Dives = 24;
        static double Near => Middle.BurrowSlop + Middle.BurrowSurfaceAt + 3;   // the most a dive at a stander can miss by

        [Test]
        public void ABurrowerComesUpUnderAPlayerWhoStandsStill()
        {
            int hits = 0;
            double lo = double.MaxValue, hi = double.MinValue;
            for (uint s = 1; s <= Dives; s++)
            {
                var d = Dive(s, null);
                Assert.That(Math.Sqrt((d.x - d.px) * (d.x - d.px) + (d.y - d.py) * (d.y - d.py)), Is.LessThan(Near), "seed " + s);
                if (d.took > 0) hits++;
                lo = Math.Min(lo, d.x - d.px); hi = Math.Max(hi, d.x - d.px);
            }
            Assert.That(hits, Is.GreaterThan(Dives * 3 / 4), "standing still is punished");
            Assert.That(hi - lo, Is.GreaterThan(12), "but not on the same spot every dive");
        }

        [Test]
        public void ABurrowerChasesUnderground()
        {
            // the player is elsewhere by the time it is down: it follows, it does not dig up an empty spot
            for (uint s = 1; s <= Dives; s++)
            {
                var d = Dive(s, (run, i) => { if (i == 0) Place(run, run.player.x + 220, run.player.y); Step(run); });
                Assert.That(Math.Abs(d.x - d.px), Is.LessThan(Near), "seed " + s + ": it surfaced under the new spot");
            }
        }

        [Test]
        public void ABurrowerGathersSpeedUnderTheFloor()
        {
            var d = Dive(3, (run, i) => { Place(run, i % 2 == 0 ? Balance.RoomRight - 60 : Balance.RoomLeft + 60, Balance.RoomBottom - 40); Step(run); });
            double walk = Middle.BurrowWalkSharp * Balance.DepthRate(1), run = Middle.BurrowRun * Balance.DepthRate(1);
            Assert.That(d.first, Is.LessThan(walk * 1.1), "it leaves at about its walk, not at full speed");
            Assert.That(d.top, Is.GreaterThan(run * 0.8), "and builds to its top speed");
        }

        [Test]
        public void ABurrowerLeadsARunnerButNotAlwaysTheSameAndATurnBeatsIt()
        {
            int straightHits = 0, turnedHits = 0;
            var ahead = new System.Collections.Generic.List<double>();
            for (uint s = 1; s <= Dives; s++)
            {
                var straight = Dive(s, WalkRight);
                if (straight.took > 0) straightHits++;
                var turned = Dive(s, (run, i) =>
                {
                    var b = run.enemies.First(e => e.kind == BodyKind.Burrower);
                    if (b.move == "rise") { TickOrder.TickPlayer(run, new Input(-1, 0)); TickOrder.TickProjectiles(run); TickOrder.TickBodies(run); }
                    else WalkRight(run, i);
                });
                if (turned.took > 0) turnedHits++;
                ahead.Add(turned.x - turned.px);
            }
            Assert.That(straightHits, Is.GreaterThan(Dives / 2), "a straight line is punished more often than not");
            Assert.That(turnedHits, Is.LessThan(Dives / 4), "turning on the tell beats it");
            Assert.That(ahead.Average(), Is.GreaterThan(20), "it comes up ahead of a runner");

        }

        // a player running in a circle (radius ~ speed / turn), the escape the owner found on 2026-10-14
        static (int dives, int hits) Circling(double turnPerSec)
        {
            int dives = 0, hits = 0;
            for (uint s = 1; s <= 24; s++)
            {
                var run = Started(s);
                Place(run, Balance.MidX, Balance.MidY);
                var b = Add(run, BodyKind.Burrower, Balance.MidX + 220, Balance.MidY - 120);
                double ang = 0, step = turnPerSec / Balance.TickHz;
                for (int t = 0; t < Balance.Sec(12); t++)
                {
                    ang += step;
                    double before = Health(run);
                    bool rising = b.move == "rise";
                    TickOrder.TickPlayer(run, new Input(Math.Cos(ang), Math.Sin(ang)));
                    TickOrder.TickProjectiles(run);
                    TickOrder.TickBodies(run);
                    if (rising && b.move != "rise") { dives++; if (Health(run) < before) hits++; }
                    run.player.hp = run.player.maxHp; run.player.regenHeart = run.player.regenHeartMax; run.player.iframes = 0;
                }
            }
            return (dives, hits);
        }

        // a player pacing wall to wall, reversing every `every` seconds
        static (int dives, int hits) Pacing(double every)
        {
            int dives = 0, hits = 0;
            for (uint s = 1; s <= 24; s++)
            {
                var run = Started(s);
                Place(run, Balance.MidX, Balance.MidY + 40);
                var b = Add(run, BodyKind.Burrower, Balance.MidX + 200, Balance.RoomTop + 60);
                int dir = 1, flip = Balance.Sec(every);
                for (int t = 0; t < Balance.Sec(12); t++)
                {
                    if (t % flip == 0) dir = -dir;
                    var p = run.player;
                    if (p.x > Balance.RoomRight - 40) dir = -1; else if (p.x < Balance.RoomLeft + 40) dir = 1;
                    bool rising = b.move == "rise";
                    double before = Health(run);
                    TickOrder.TickPlayer(run, new Input(dir, 0));
                    TickOrder.TickProjectiles(run);
                    TickOrder.TickBodies(run);
                    if (rising && b.move != "rise") { dives++; if (Health(run) < before) hits++; }
                    p.hp = p.maxHp; p.regenHeart = p.regenHeartMax; p.iframes = 0;
                }
            }
            return (dives, hits);
        }

        [Test]
        public void PacingBackAndForthDoesNotBeatABurrower()
        {
            // the cold critic's case: a reversal read as a 19 rad/s turn curled the lead onto the player's feet
            var (dives, hits) = Pacing(1.2);
            TestContext.WriteLine("pacing: " + hits + " of " + dives);
            Assert.That(dives, Is.GreaterThan(30));
            Assert.That(hits, Is.GreaterThan(dives / 5), "pacing must not be a safe answer");
        }

        [Test]
        public void RunningInCirclesDoesNotBeatABurrower()
        {
            foreach (double turn in new[] { 2.5, 4.0 })
            {
                var (dives, hits) = Circling(turn);
                TestContext.WriteLine("circling at " + turn + " rad/s: " + hits + " of " + dives + " dives hit");
                Assert.That(dives, Is.GreaterThan(30), "the fixture dives");
                Assert.That(hits, Is.GreaterThan(dives / 3), "circling at " + turn + " rad/s must not be a safe answer");
            }
        }

        [Test]
        public void ABurrowerSettlesOntoItsMarkInsteadOfCirclingIt()
        {
            // the critic's case: a stander, the Burrower 200 px off; every dive must end by reaching the mark, not the timer
            int dives = 0, timedOut = 0;
            for (uint s = 1; s <= 40; s++)
            {
                var run = Started(s);
                var b = Add(run, BodyKind.Burrower, run.player.x + 200, run.player.y - 60);
                for (int t = 0; t < Balance.Sec(14); t++)
                {
                    bool under = b.move == "under";
                    int left = b.moveT;
                    Step(run);
                    if (under && b.move == "rise") { dives++; if (left <= 1) timedOut++; }
                    run.player.hp = run.player.maxHp;
                }
            }
            Assert.That(dives, Is.GreaterThan(80), "the fixture dives");
            Assert.That(timedOut, Is.EqualTo(0), "no dive at a standing player runs out its clock circling");
        }

        [Test]
        public void TheSprayerWindsUpLongThenSendsAWeakCone()
        {
            var run = Started();
            var s = Add(run, BodyKind.Sprayer, Balance.MidX, run.player.y - 150);
            int t = 0;
            while (s.castT <= 0 && t++ < Balance.Sec(8)) Step(run);
            Assert.That(s.castT, Is.EqualTo(Middle.SprayWind), "the tell is the long wind-up, not the shooter's");
            int wind = 0;
            while (run.projectiles.Count == 0 && wind++ < Balance.Sec(3)) Step(run);
            Assert.That(wind, Is.EqualTo(Middle.SprayWind).Within(1));
            Assert.That(run.projectiles.Count, Is.EqualTo(Middle.SprayCount));
            Assert.That(run.projectiles.All(p => p.dmg == 1), "each shot is weak");
            var heads = run.projectiles.Select(p => Math.Atan2(p.vy, p.vx)).OrderBy(a => a).ToList();
            Assert.That(heads.Last() - heads.First(), Is.EqualTo(2 * Middle.SprayCone).Within(1e-9));
        }

        [Test]
        public void TheEggNeverWalksAndFiresAnEvenRing()
        {
            var run = Started();
            var e = Add(run, BodyKind.Egg, Balance.MidX, Balance.MidY - 60);
            double x = e.x, y = e.y;
            Place(run, Balance.RoomLeft + 40, Balance.RoomBottom - 40);
            int volleys = 0, t = 0, first = -1, second = -1;
            while (volleys < 2 && t++ < Balance.Sec(12))
            {
                int n = run.projectiles.Count;
                Step(run);
                if (run.projectiles.Count >= n + Middle.EggRing)
                {
                    volleys++;
                    if (first < 0) first = t; else second = t;
                    var ring = run.projectiles.Skip(run.projectiles.Count - Middle.EggRing).Select(p => Math.Atan2(p.vy, p.vx)).OrderBy(a => a).ToList();
                    for (int i = 1; i < ring.Count; i++) Assert.That(ring[i] - ring[i - 1], Is.EqualTo(2 * Math.PI / Middle.EggRing).Within(1e-9));
                    run.projectiles.Clear();
                }
            }
            Assert.That(volleys, Is.EqualTo(2));
            Assert.That(e.x, Is.EqualTo(x)); Assert.That(e.y, Is.EqualTo(y));
            // the live body's cooldown (tempo- and depth-scaled at spawn, as every ranged body is), less the crowd discount
            double lo = e.cdMin * (1 - Balance.PressureCadence) + Middle.EggWind - 2, hi = e.cdMin + e.cdVar + Middle.EggWind + 2;
            Assert.That(second - first, Is.InRange(lo, hi), "a volley every couple of seconds");
            Assert.That((second - first) / (double)Balance.TickHz, Is.GreaterThan(1.5), "slow enough to read the gaps in the ring");
        }

        [Test]
        public void ALungeOverABuriedBurrowerShovesNothing()
        {
            // the critic's repro: the lunge clash looped every body, so a charge knocked and stunned a Burrower underground
            var run = Started();
            var b = Add(run, BodyKind.Burrower, Balance.MidX, Balance.MidY - 80);
            b.buried = true; b.move = "under"; b.moveT = 1000; b.markX = b.x; b.markY = b.y - 200;   // a mark it is still running at
            var l = Add(run, BodyKind.Lunger, b.x - 10, b.y);
            l.lungeState = "lunge"; l.lungeT = 20; l.lungeDx = 1; l.lungeDy = 0;
            TickOrder.TickPlayer(run, Input.None);
            TickOrder.TickBodies(run);
            Assert.That(b.kvx, Is.EqualTo(0)); Assert.That(b.kvy, Is.EqualTo(0)); Assert.That(b.stun, Is.EqualTo(0));
            Assert.That(l.lungeState, Is.EqualTo("lunge"), "the lunger met nothing");
        }

        [Test]
        public void ABuriedBurrowerBlocksNoOtherBodysShot()
        {
            var run = Started();
            var s = Add(run, BodyKind.Shooter, run.player.x, run.player.y - 300);
            var b = Add(run, BodyKind.Burrower, run.player.x, run.player.y - 150);   // squarely on the line
            double want = Math.Atan2(run.player.y - s.y, run.player.x - s.x);
            Assert.That(Movement.ClearShot(run, s, want), Is.Not.EqualTo(want), "standing, it is in the way");
            b.buried = true;
            Assert.That(Movement.ClearShot(run, s, want), Is.EqualTo(want), "buried, the line is clear");
        }

        [Test]
        public void TheMortarsOwnCastLeavesAsALobOnThePlayersSpot()
        {
            // the real path: standoff, cast clock, the shared release - not Release called by hand
            var run = Started();
            Add(run, BodyKind.Mortar, Balance.MidX + 40, Balance.RoomTop + 40);
            Add(run, BodyKind.Lunger, Balance.MidX + 20, (Balance.RoomTop + 40 + run.player.y) / 2).noticeTimer = int.MaxValue / 2;   // a body on its line: a lob goes over
            Projectile? lob = null;
            for (int t = 0; t < Balance.Sec(10) && lob == null; t++) { Step(run); lob = run.projectiles.FirstOrDefault(p => p.lob); }
            Assert.That(lob, Is.Not.Null, "it lobbed within ten seconds over a body in the way");
            Assert.That(lob!.tx, Is.EqualTo(TickOrder.HitboxX).Within(3));
            Assert.That(lob.ty, Is.EqualTo(TickOrder.HitboxY + Balance.PlayerHitDy).Within(3));
        }

        [Test]
        public void TheEggStaysPutUnderTheHook()
        {
            var run = Started();
            var e = Add(run, BodyKind.Egg, Balance.MidX, Balance.MidY - 60);
            double x = e.x, y = e.y;
            Blast.Explode(run, x + 80, y, "hook");
            Assert.That(e.stun, Is.GreaterThan(0), "the hook still holds it");
            for (int t = 0; t < Balance.Sec(2); t++) { Blast.TickFields(run); TickOrder.TickBodies(run); }
            Assert.That(e.x, Is.EqualTo(x).Within(1e-9)); Assert.That(e.y, Is.EqualTo(y).Within(1e-9));
        }

        [Test]
        public void ABurrowerHookedWhileDiggingStillDivesAndComesUp()
        {
            var run = Started();
            var b = Add(run, BodyKind.Burrower, Balance.MidX, Balance.RoomTop + 80);
            int t = 0;
            while (b.move != "dig" && t++ < Balance.Sec(4)) Step(run);
            Blast.Explode(run, b.x, b.y, "hook");   // stunned mid-dig
            Assert.That(b.stun, Is.GreaterThan(0));
            bool dove = false;
            while (t++ < Balance.Sec(12) && !(dove && b.move == "walk")) { Step(run); dove |= b.buried; }
            Assert.That(dove, "it went under after the stun");
            Assert.That(b.move, Is.EqualTo("walk")); Assert.That(b.buried, Is.False, "and came back up");
        }

        [Test]
        public void ReleaseRefusesAKindWithoutOne()
        {
            var run = Started();
            Assert.Throws<InvalidOperationException>(() => Middle.Release(run, Enemy.Of(BodyKind.Burrower, 100, 100)));
        }

        [Test]
        public void ABurrowerCycleTakesTheSameSecondsAtBothRates()
        {
            double Cycle(int hz)
            {
                int was = Balance.TickHz;
                Balance.TickHz = hz;
                try
                {
                    var run = Started();
                    var b = Add(run, BodyKind.Burrower, Balance.MidX, Balance.RoomTop + 60);
                    int t = 0;
                    while (b.move != "dig" && t < hz * 10) { Step(run); t++; }
                    int start = t;
                    while (b.move != "walk" && t < hz * 20) { Step(run); t++; }   // dig, under, rise, up
                    return (t - start) / (double)hz;
                }
                finally { Balance.TickHz = was; }
            }
            double at60 = Cycle(60), at210 = Cycle(210);
            Assert.That(at60, Is.GreaterThan(1.0));
            Assert.That(at60, Is.EqualTo(at210).Within(2.0 / 60), "the dive is timed in seconds");
        }
    }
}
