using System;
using System.Collections.Generic;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The Sunken King, the final boss of floor 14 (FinalBoss.cs, Balance.TuneFinalBoss): where it spawns, its tells,
    /// its floor, its three phases, its death beat before the win, and that nothing it does hurts without a tell.</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class FinalBossTests
    {
        static RunState OnFloor(uint seed, int floor)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            for (int f = 1; f < floor; f++) run.Descend();
            return run;
        }

        static Enemy BossOf(RunState run)
        {
            var arena = run.dungeon.AllRooms.First(r => r.Type == RoomKind.Boss);
            run.bossUnlocked = true;
            Rooms.EnterRoom(run, arena.X, arena.Y, Dir.S);
            return run.enemies.Single(e => Bodies.IsBoss(e.kind));
        }

        // a quiet arena on floor 14: the player low in the room (off the edge ring), the King awake on its throne
        static (RunState run, Enemy king) Arena(uint seed = 31)
        {
            var run = OnFloor(seed, Balance.FinalFloor);
            run.readyT = 0; run.fadeT = 0; run.trans = null; run.state = "playing";
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
            var p = run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.MidY + 100; p.vx = p.vy = 0;
            var e = Spawn.Body(run, BodyKind.SunkenKing, 0, 0);
            e.noticeTimer = 0;
            run.enemies.Add(e);
            TickOrder.TickPlayer(run, Input.None);   // publishes the hitbox the King aims at
            return (run, e);
        }

        static void Step(RunState run, Input input)
        {
            TickOrder.TickPlayer(run, input);
            TickOrder.TickProjectiles(run);
            TickOrder.TickBodies(run);
        }

        static void Immortal(RunState run)   // the harness: a still target that lives and takes every hit
        {
            run.player.hp = run.player.maxHp; run.player.armor = 0; run.player.iframes = 0; run.blinkGrace = 0;
        }

        static bool Telling(RunState run, Enemy e) =>
            e.castT > 0 || e.move == "tell" || e.move == "rift" || e.move == "leap" || e.move == "quake" || run.projectiles.Any(p => p.lob);

        [Test]
        public void TheKingHoldsFloorFourteenAndTheGatekeeperOneToThirteen()
        {
            for (uint seed = 1; seed <= 3; seed++)
                for (int f = 1; f <= Balance.FinalFloor; f++)
                    Assert.That(BossOf(OnFloor(seed, f)).kind, Is.EqualTo(f < Balance.FinalFloor ? BodyKind.Gatekeeper : BodyKind.SunkenKing), "seed " + seed + " floor " + f);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void UntunedOrSwitchedOffFloorFourteenKeepsTheWarden(bool tuned, bool finalBoss)
        {
            bool was = Balance.Tuned, fb = Balance.TuneFinalBoss;
            try
            {
                Balance.Tuned = tuned; Balance.TuneFinalBoss = finalBoss;
                Assert.That(BossOf(OnFloor(5, Balance.FinalFloor)).kind, Is.EqualTo(BodyKind.Boss));
                if (tuned) Assert.That(BossOf(OnFloor(5, 6)).kind, Is.EqualTo(BodyKind.Gatekeeper), "the switch reached the Gatekeeper");
            }
            finally { Balance.Tuned = was; Balance.TuneFinalBoss = fb; }
        }

        [Test]
        public void ItIsTheFloorsBossAndBiggerThanTheGatekeeper()
        {
            Assert.That(Bodies.IsBoss(BodyKind.SunkenKing));
            Assert.That(Spikes.CanBeElite(BodyKind.SunkenKing), Is.False);
            Assert.That(Bodies.Of(BodyKind.SunkenKing).Radius, Is.GreaterThan(Bodies.Of(BodyKind.Gatekeeper).Radius));
            var (run, e) = Arena();
            Assert.That(e.aggroTimer, Is.EqualTo(9999), "a boss starts committed");
            Assert.That(e.maxHp / Bodies.Of(BodyKind.SunkenKing).Hp, Is.EqualTo(Balance.DepthTough(Balance.FinalFloor)).Within(1e-9), "health through the depth dial");
            Assert.That((e.x, e.y), Is.EqualTo((FinalBoss.ThroneX, FinalBoss.ThroneY)), "it starts on its throne");
        }

        [Test]
        public void EveryTellIsAtLeastPointThreeFiveSeconds()
        {
            int min = Balance.Sec(FinalBoss.TellMin);
            foreach (int t in new[] { FinalBoss.FanTell, FinalBoss.NovaTell, FinalBoss.SlamWind, FinalBoss.SlamFlight, FinalBoss.ChargeTell, FinalBoss.AgainTell,
                                      FinalBoss.RiftTell, FinalBoss.LeapTime, FinalBoss.CrackTell, FinalBoss.QuadTell, FinalBoss.FallTell })
                Assert.That(t, Is.GreaterThanOrEqualTo(min));
        }

        // each attack from its first tick: a tell shows at once, and nothing that can hurt (a shell, a landing, a charging body,
        // an eruption) comes before TellMin seconds
        [TestCase(FinalBoss.Attack.Fan)]
        [TestCase(FinalBoss.Attack.Slam)]
        [TestCase(FinalBoss.Attack.Charge)]
        [TestCase(FinalBoss.Attack.Rift)]
        [TestCase(FinalBoss.Attack.Leap)]
        [TestCase(FinalBoss.Attack.Nova)]
        public void EachAttackShowsItsTellBeforeItCanHurt(FinalBoss.Attack a)
        {
            var (run, e) = Arena();
            e.phase = 3; e.fbLeft = 0; e.bossCd = 1e9;   // the harness: no attack but this one, and no phase change
            e.x = Balance.MidX; e.y = Balance.MidY - 80;
            FinalBoss.Begin(run, e, a);
            Assert.That(Telling(run, e), "the tell is not up as the attack begins");
            int harm = -1, min = Balance.Sec(FinalBoss.TellMin), booms = run.booms;
            for (int t = 1; t <= Balance.Sec(3) && harm < 0; t++)
            {
                bool told = Telling(run, e);
                Step(run, Input.None);
                if (e.fbHot || run.projectiles.Any(p => !p.lob) || run.booms > booms) harm = t;
                else Assert.That(told || Telling(run, e), "no tell up at tick " + t + " before the hit");
            }
            Assert.That(harm, Is.GreaterThan(0), a + " never struck");
            Assert.That(harm, Is.GreaterThanOrEqualTo(min), a + " struck " + harm + " ticks in, under " + FinalBoss.TellMin + " s");
        }

        // each phase's floor pattern: every cell it lights cracks at least the tell before it burns, and good ground is left
        [TestCase(1, FinalBoss.Source.Fault)]
        [TestCase(2, FinalBoss.Source.Checker)]
        [TestCase(3, FinalBoss.Source.Quadrant)]
        public void EachFloorPatternCracksBeforeItBurnsAndLeavesGround(int phase, FinalBoss.Source pattern)
        {
            var (run, e) = Arena();
            e.phase = phase; e.fbLeft = 0; e.bossCd = 1e9;
            if (phase == 3) e.ground!.safe = 2;
            FinalBoss.Begin(run, e, FinalBoss.Attack.Quake);
            var g = e.ground!;
            var cracked = new int[FinalBoss.Ground.N];
            int lit = Enumerable.Range(0, FinalBoss.Ground.N).Count(i => g.warn[i] > 0);
            Assert.That(lit, Is.GreaterThan(10), "fixture: the pattern lit nothing");
            Assert.That(Enumerable.Range(0, FinalBoss.Ground.N).Where(i => g.warn[i] > 0).All(i => g.src[i] == pattern), "lit under another pattern's name");
            int safe = Enumerable.Range(0, FinalBoss.Ground.N).Count(i => g.warn[i] == 0 && !g.pit[i] && (phase < 3 || !FinalBoss.Ground.Edge(i)));
            Assert.That(safe, Is.GreaterThanOrEqualTo(8), "too little good ground left");
            var burnt = new bool[FinalBoss.Ground.N];
            for (int t = 0; t < Balance.Sec(4); t++)
            {
                for (int i = 0; i < FinalBoss.Ground.N; i++) if (g.warn[i] > 0) cracked[i]++;
                Step(run, Input.None);
                for (int i = 0; i < FinalBoss.Ground.N; i++)
                {
                    if (g.burn[i] <= 0 || burnt[i]) continue;
                    burnt[i] = true;
                    Assert.That(cracked[i], Is.GreaterThanOrEqualTo(Balance.Sec(FinalBoss.TellMin)), "cell " + i + " burned off a crack of " + cracked[i] + " ticks");
                }
            }
            int burned = burnt.Count(b => b);
            Assert.That(burned, Is.EqualTo(lit), "a cracked cell never burned");
            if (phase == 3)
            {
                Assert.That(Enumerable.Range(0, FinalBoss.Ground.N).Where(i => !FinalBoss.Ground.Edge(i) && FinalBoss.Ground.Quadrant(i) == 2).All(i => cracked[i] == 0), "the spared quadrant burned");
                Assert.That(g.safe, Is.EqualTo(3), "the next spared quadrant is not the next one clockwise");
            }
        }

        [Test]
        public void TheRisenKingNeverSparesTheQuadrantItFloatsIn()
        {
            // the blind critic's case: the only safe ground beside the King's own body is a trap, not a puzzle
            var (run, e) = Arena();
            e.phase = 3;
            var g = e.ground!;
            int cell = 2 * FinalBoss.Ground.Cols + 7;   // an inner cell of the top-right quadrant
            Assert.That(FinalBoss.Ground.Quadrant(cell), Is.EqualTo(1), "fixture: not the quadrant meant");
            e.x = FinalBoss.Ground.CentreX(cell); e.y = FinalBoss.Ground.CentreY(cell);
            g.safe = 1;
            int shown = FinalBoss.NextSpared(e);   // what the floor shows as spared, the King standing where it does
            Assert.That(shown, Is.Not.EqualTo(1), "the quadrant shown as spared is the one the King floats in");
            FinalBoss.Begin(run, e, FinalBoss.Attack.Quake);
            Assert.That(g.lastSafe, Is.EqualTo(shown), "the quadrant spared is not the one that was shown");
            Assert.That(g.lastSafe, Is.Not.EqualTo(1), "it spared the quadrant it stands in");
            Assert.That(Enumerable.Range(0, FinalBoss.Ground.N).Count(i => !FinalBoss.Ground.Edge(i) && FinalBoss.Ground.Quadrant(i) == g.lastSafe && g.warn[i] == 0),
                Is.GreaterThan(0), "the quadrant it spared instead has no quiet ground");
        }

        [Test]
        public void OverABurningFloorTheKingWaits()
        {
            // the blind critic's case: a rift laid across a burning checker left nowhere to stand
            var (run, e) = Arena();
            e.phase = 2; e.x = Balance.MidX; e.y = Balance.RoomTop + 120;
            var g = e.ground!;
            var begun = new System.Collections.Generic.List<FinalBoss.Attack>();
            for (int k = 0; k < 300; k++)
            {
                g.burn[0] = FinalBoss.Burn;   // a cell burning in a corner, far from everything
                e.move = "idle"; e.bossCd = 0; e.fbLeft = 0; e.castT = 0;
                run.projectiles.Clear();
                TickOrder.TickBodies(run);
                if (e.move != "idle") begun.Add((FinalBoss.Attack)e.fbAttack);
            }
            Assert.That(begun, Is.Empty, "it attacked over a burning floor: the floor is the attack");
        }

        [Test]
        public void AStunnedKingsFloorBurnsOn()
        {
            // the critic's case: a hooked or blasted King froze the floor, so fire under the player did nothing
            var (run, e) = Arena();
            var g = e.ground!;
            FinalBoss.Begin(run, e, FinalBoss.Attack.Quake);
            int lit = Enumerable.Range(0, FinalBoss.Ground.N).Count(i => g.warn[i] > 0);
            Assert.That(lit, Is.GreaterThan(0), "fixture: nothing cracked");
            for (int t = 0; t < FinalBoss.CrackTell + 5; t++) { e.stun = 100; Step(run, Input.None); }
            Assert.That(g.Burning, "the cracks never burned while the King was stunned");
        }

        [Test]
        public void TheFloorThrowsThePlayerTowardGoodGround()
        {
            // the critic's case: thrown away from the cell's centre, a player between it and the wall went deeper in
            var (run, e) = Arena();
            var g = e.ground!;
            int cell = 2 * FinalBoss.Ground.Cols;   // the west edge, the third row
            g.pit[cell] = true;
            var p = run.player;
            p.x = p.lagX = FinalBoss.Ground.CentreX(cell) - 15; p.y = p.lagY = FinalBoss.Ground.CentreY(cell) - Balance.PlayerHitDy;
            p.kvx = p.kvy = 0; p.iframes = 0; run.blinkGrace = 0;
            FinalBoss.StepFloor(run, e);
            Assert.That(p.kvx, Is.GreaterThan(0), "thrown into the wall, deeper into the pit, not toward the good ground east");
        }

        [Test]
        public void TheSlamIsNotAShellsOnlyAttack()
        {
            Assert.That(FinalBoss.Shells(FinalBoss.Attack.Slam), Is.False, "its landing marks are on the floor");
            Assert.That(FinalBoss.Shells(FinalBoss.Attack.Fan) && FinalBoss.Shells(FinalBoss.Attack.Nova));
        }

        [Test]
        public void ASeatedKingShovedOffItsThroneWalksBack()
        {
            var (run, e) = Arena();
            e.phase = 1; e.move = "rest"; e.moveT = 1; e.bossCd = 1e9; e.fbLeft = 0;
            e.x = FinalBoss.ThroneX + 60; e.y = FinalBoss.ThroneY + 30;
            for (int t = 0; t < Balance.Sec(3); t++) Step(run, Input.None);
            Assert.That(Math.Abs(e.x - FinalBoss.ThroneX) + Math.Abs(e.y - FinalBoss.ThroneY), Is.LessThan(2), "it sat down off its throne");
        }

        [Test]
        public void TheRecallBringsThisRunsOwnKindsOncePerPhaseAfterItsRunes()
        {
            // the graft from the Archivist: phase n brings area n's kinds of THIS run, on quiet ground away from the player
            for (int phase = 1; phase <= 3; phase++)
            {
                var (run, e) = Arena(7);
                e.phase = phase;
                var p = run.player;
                double hx = p.lagX, hy = p.lagY + Balance.PlayerHitDy;
                FinalBoss.Begin(run, e, FinalBoss.Attack.Summon);
                Assert.That(e.move, Is.EqualTo("summon"));
                Assert.That(e.kgSumN, Is.EqualTo(FinalBoss.AddCap), "phase " + phase + ": not two runes");
                var kinds = FinalBoss.Recallable(run, phase).ToList();
                Assert.That(kinds.All(k => run.pools![phase - 1].Contains(k)), "phase " + phase + " recalls a kind its area never had");
                for (int i = 0; i < e.kgSumN; i++)
                {
                    double x = e.kgSum[2 * i], y = e.kgSum[2 * i + 1];
                    Assert.That(Math.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy)), Is.GreaterThanOrEqualTo(FinalBoss.SummonFromPlayer), "a rune on the player");
                    int c = FinalBoss.Ground.CellAt(x, y);
                    Assert.That(c >= 0 && !e.ground!.Hot(c) && e.ground.warn[c] == 0, "a rune on bad ground");
                    Assert.That(kinds, Does.Contain(e.kgSumK[i]));
                }
                Assert.That(run.enemies.Count(o => o.kgAdd), Is.EqualTo(0), "an add came before its runes' tell ran out");
                for (int t = 0; t < FinalBoss.SummonTell - 1; t++) { FinalBoss.Step(run, e, 1, 1, 1000, 1); Assert.That(run.enemies.Count(o => o.kgAdd), Is.EqualTo(0)); }
                FinalBoss.Step(run, e, 1, 1, 1000, 1);
                var adds = run.enemies.Where(o => o.kgAdd).ToList();
                Assert.That(adds, Has.Count.EqualTo(2));
                Assert.That(adds.All(a => a.noticeTimer >= FinalBoss.AddWake && a.hp < a.maxHp / FinalBoss.AddHp + 1e-9));
                Assert.That(FinalBoss.CanSummon(run, e), Is.False, "it may recall again in the same phase (or over two live adds)");
            }
        }

        [Test]
        public void TheAddsCrumbleAsTheKingFallsAndTheRunIsWon()
        {
            var (run, e) = Arena();
            var arena = run.dungeon.AllRooms.First(r => r.Type == RoomKind.Boss);
            run.curX = arena.X; run.curY = arena.Y;
            FinalBoss.Begin(run, e, FinalBoss.Attack.Summon);
            for (int t = 0; t < FinalBoss.SummonTell; t++) FinalBoss.Step(run, e, 1, 1, 1000, 1);
            Assert.That(run.enemies.Count(o => o.kgAdd), Is.EqualTo(2), "fixture: no adds");
            Kills.KillEnemy(run, run.enemies.IndexOf(e));
            for (int t = 0; t < FinalBoss.DeathBeat + 5 && run.state == "playing"; t++) TickOrder.TickRoom(run);
            Assert.That(run.enemies.Any(o => o.kgAdd), Is.False, "the adds outlived the King");
            Assert.That(run.state, Is.EqualTo("won"), "the adds kept the run from being won");
        }

        [Test]
        public void ADeathMidRecallStillWinsAfterTheBeatWithEveryAddGone()
        {
            // the critic's gap: whole ticks, two adds standing, a second recall's runes up, then the King dies
            var (run, e) = Arena();
            var arena = run.dungeon.AllRooms.First(r => r.Type == RoomKind.Boss);
            run.curX = arena.X; run.curY = arena.Y;
            FinalBoss.Begin(run, e, FinalBoss.Attack.Summon);
            for (int t = 0; t < FinalBoss.SummonTell; t++) Step(run, Input.None);
            Assert.That(run.enemies.Count(o => o.kgAdd), Is.EqualTo(2), "fixture: no adds");
            e.phase = 2; e.kgSummoned = 0;
            foreach (var o in run.enemies.Where(o => o.kgAdd).ToList()) run.enemies.Remove(o);
            FinalBoss.Begin(run, e, FinalBoss.Attack.Summon);
            for (int t = 0; t < FinalBoss.SummonTell / 2; t++) Step(run, Input.None);
            Assert.That(e.move, Is.EqualTo("summon"), "fixture: not mid-recall");
            e.hp = 0.01;
            run.projectiles.Add(new Projectile { x = e.x, y = e.y, vx = 0.01, r = 6, dmg = 5, friendly = true, scale = 1 });
            for (int t = 0; t < FinalBoss.DeathBeat + 10 && run.state == "playing"; t++)
            {
                Immortal(run);
                Step(run, Input.None);
                TickOrder.TickRoom(run);
            }
            Assert.That(run.enemies.Any(o => o.kgAdd || o.kind == BodyKind.SunkenKing), Is.False, "the King or an add survived");
            Assert.That(run.state, Is.EqualTo("won"));
        }

        [Test]
        public void AnAddsHitIsBookedToTheRecall()
        {
            var (run, e) = Arena();
            var p = run.player;
            var add = Spawn.Body(run, BodyKind.Lunger, p.lagX, p.lagY + Balance.PlayerHitDy);   // standing on the hitbox
            add.kgAdd = true; add.noticeTimer = 0; add.alerted = true; add.aggroTimer = 9999;
            run.enemies.Add(add);
            e.stun = 1000;   // the King out of it
            Immortal(run);
            double before = run.finalBy[(int)FinalBoss.Source.Adds];
            Step(run, Input.None);
            Assert.That(run.finalBy[(int)FinalBoss.Source.Adds], Is.GreaterThan(before), "an add's contact went unbooked");
        }

        [Test]
        public void ThePhasesChangeTheKingsPostureAndTheRoom()
        {
            var (run, e) = Arena();
            // seated: it holds the throne through a stretch of phase 1, never charging, never leaving
            for (int t = 0; t < Balance.Sec(20); t++)
            {
                Immortal(run);
                Step(run, Input.None);
                Assert.That(e.move, Is.Not.EqualTo("charge").And.Not.EqualTo("leap").And.Not.EqualTo("rift"), "phase 1 used a later attack");
            }
            Assert.That(Math.Abs(e.x - FinalBoss.ThroneX) + Math.Abs(e.y - FinalBoss.ThroneY), Is.LessThan(3), "it left its throne in phase 1 (its adds may jostle it a pixel; it walks back)");
            Assert.That(FinalBoss.PostureOf(e), Is.EqualTo(FinalBoss.Posture.Seated));
            var g = e.ground!;
            Assert.That(g.pit.Any(p => p), Is.False, "a pit in phase 1");

            // phase 2: the centre cracks for FallTell and falls for good; it stands and walks
            e.hp = e.maxHp * (Balance.BossPhase1 - 0.01);
            Step(run, Input.None);
            Assert.That(e.phase, Is.EqualTo(2));
            Assert.That(e.move, Is.EqualTo("shift"));
            Assert.That(FinalBoss.PostureOf(e), Is.EqualTo(FinalBoss.Posture.Standing));
            var centre = Enumerable.Range(0, FinalBoss.Ground.N).Where(FinalBoss.Ground.Centre).ToList();
            Assert.That(centre.All(i => g.warn[i] > 0 && g.falls[i]), "the centre is not cracking to fall");
            for (int t = 0; t < FinalBoss.FallTell; t++) Step(run, Input.None);
            Assert.That(centre.All(i => g.pit[i]), "the centre did not fall");
            Assert.That(Enumerable.Range(0, FinalBoss.Ground.N).Count(i => g.pit[i]), Is.EqualTo(8));
            double x0 = e.x, y0 = e.y;
            for (int t = 0; t < Balance.Sec(10); t++) { Immortal(run); Step(run, Input.None); }   // its opener burns first: it waits, then charges
            Assert.That(Math.Abs(e.x - x0) + Math.Abs(e.y - y0), Is.GreaterThan(30), "standing, it never moved");
            Assert.That(centre.All(i => g.pit[i]), "the pit closed in phase 2");

            // phase 3: the centre closes, the edge cracks and falls; it rises
            e.hp = e.maxHp * (Balance.BossPhase2 - 0.01);
            Step(run, Input.None);
            Assert.That(e.phase, Is.EqualTo(3));
            Assert.That(FinalBoss.PostureOf(e), Is.EqualTo(FinalBoss.Posture.Risen));
            Assert.That(centre.Any(i => g.pit[i]), Is.False, "the centre stayed open");
            var edge = Enumerable.Range(0, FinalBoss.Ground.N).Where(FinalBoss.Ground.Edge).ToList();
            Assert.That(edge.All(i => g.warn[i] > 0 && g.falls[i]), "the edge is not cracking to fall");
            for (int t = 0; t < FinalBoss.FallTell; t++) Step(run, Input.None);
            Assert.That(edge.All(i => g.pit[i]) && Enumerable.Range(0, FinalBoss.Ground.N).Count(i => g.pit[i]) == edge.Count, "the edge did not fall, or more did");
        }

        [Test]
        public void EachPhaseOpensWithItsFloorPatternAndCallsOffItsMove()
        {
            var (run, e) = Arena();
            string first = null!;
            for (int t = 0; t < Balance.Sec(5) && first == null; t++) { Step(run, Input.None); if (e.move != "idle") first = e.move; }
            Assert.That(first, Is.EqualTo("quake"), "phase 1 did not open with its floor");
            for (int t = 0; t < Balance.Sec(8); t++) { Immortal(run); Step(run, Input.None); }
            FinalBoss.Begin(run, e, FinalBoss.Attack.Fan);
            Step(run, Input.None);
            e.hp = e.maxHp * (Balance.BossPhase1 - 0.01);
            Step(run, Input.None);
            Assert.That(e.castT, Is.EqualTo(0), "the fan's warning outlived the phase change");
            Assert.That(e.ground!.burn.All(b => b == 0), "a pattern's burn outlived the phase change");
            for (int ph = 2; ph <= 3; ph++)
            {
                first = null!;
                for (int t = 0; t < Balance.Sec(8) && first == null; t++)
                {
                    Immortal(run);
                    Step(run, Input.None);
                    if (e.move != "idle" && e.move != "shift" && e.move != "rest") first = e.move;
                }
                Assert.That(first, Is.EqualTo("quake"), "phase " + ph + " did not open with its floor");
                Assert.That(e.ground!.src.Where((s, i) => e.ground.warn[i] > 0 && !e.ground.falls[i]).Distinct(),
                            Is.EqualTo(new[] { ph == 2 ? FinalBoss.Source.Checker : FinalBoss.Source.Quadrant }));
                if (ph == 2) e.hp = e.maxHp * (Balance.BossPhase2 - 0.01);
            }
        }

        [TestCase(1, 0)]
        [TestCase(2, 8)]
        [TestCase(3, 28)]
        public void SettlingIntoAPhaseLaysItsFallenGroundAndHoldsIt(int phase, int pits)
        {
            var (run, e) = Arena();
            FinalBoss.Settle(e, phase);
            var g = e.ground!;
            Assert.That(e.phase, Is.EqualTo(phase));
            Assert.That(Enumerable.Range(0, FinalBoss.Ground.N).Count(i => g.pit[i]), Is.EqualTo(pits));
            Assert.That(g.Cracking, Is.False, "settled with a crack showing");
            for (int t = 0; t < Balance.Sec(1); t++) { Immortal(run); Step(run, Input.None); }
            Assert.That(e.phase, Is.EqualTo(phase), "the health it was left with moved it on a phase");
        }

        [Test]
        public void EachPhaseHasItsOwnAttacks()
        {
            var seen = new HashSet<FinalBoss.Attack>[4];
            for (int ph = 1; ph <= 3; ph++)
            {
                seen[ph] = new HashSet<FinalBoss.Attack>();
                for (int last = -1; last < FinalBoss.Attacks; last++)
                    for (int i = 0; i < 1000; i++) seen[ph].Add(FinalBoss.Pick(ph, last, i / 1000.0));
            }
            Assert.That(seen[1], Is.EquivalentTo(new[] { FinalBoss.Attack.Fan, FinalBoss.Attack.Slam, FinalBoss.Attack.Quake, FinalBoss.Attack.Summon }));
            Assert.That(seen[2], Is.EquivalentTo(new[] { FinalBoss.Attack.Fan, FinalBoss.Attack.Slam, FinalBoss.Attack.Charge, FinalBoss.Attack.Rift, FinalBoss.Attack.Quake, FinalBoss.Attack.Summon }));
            Assert.That(seen[3], Is.EquivalentTo(new[] { FinalBoss.Attack.Fan, FinalBoss.Attack.Charge, FinalBoss.Attack.Rift, FinalBoss.Attack.Leap, FinalBoss.Attack.Nova, FinalBoss.Attack.Quake, FinalBoss.Attack.Summon }));
            Assert.That(seen.Skip(1).SelectMany(s => s).Where(a => a != FinalBoss.Attack.Quake).Distinct().Count(), Is.GreaterThanOrEqualTo(5), "fewer than five attacks across the fight");
        }

        // a whole fight against a still player that cannot die, over seeds: every hit at most 2 HP and booked to an... [h:FinalBossTests-1]
        [Test]
        public void NothingHurtsWithoutATellAndNoHitIsOverTwo()
        {
            int min = Balance.Sec(FinalBoss.TellMin), hits = 0;
            var bySource = new int[FinalBoss.Sources];
            var known = new HashSet<Projectile>(ReferenceEqualityComparer.Instance);
            var ends = new HashSet<string>();
            for (uint seed = 1; seed <= 4; seed++)
            {
                var (run, e) = Arena(seed);
                var g = e.ground!;
                var crack = new int[FinalBoss.Ground.N];
                int castRun = 0, tellRun = 0, lastTell = 0, riftRun = 0, lastRift = 0, leapRun = 0, lastLeap = 0;
                double phaseHp = 1;
                double px0 = run.player.x, py0 = run.player.y;
                for (int t = 0; t < Balance.Sec(120); t++)
                {
                    Immortal(run);
                    var hold = run.player;   // the harness pins the player: knocks would walk a still target into a pit for good
                    hold.x = hold.lagX = px0; hold.y = hold.lagY = py0; hold.vx = hold.vy = hold.kvx = hold.kvy = 0;
                    if (t == Balance.Sec(40)) phaseHp = 0.5;
                    if (t == Balance.Sec(80)) phaseHp = 0.2;
                    e.hp = e.maxHp * phaseHp;
                    double hp0 = run.dmgTaken;
                    var by0 = (double[])run.finalBy.Clone();
                    int castBefore = castRun;
                    for (int i = 0; i < FinalBoss.Ground.N; i++) crack[i] = g.warn[i] > 0 ? crack[i] + 1 : g.burn[i] > 0 || g.pit[i] ? crack[i] : 0;
                    var pl = run.player;
                    Step(run, Input.None);
                    int cell = FinalBoss.Ground.CellAt(pl.lagX, pl.lagY + Balance.PlayerHitDy);   // where the floor's check stood it
                    double lost = run.dmgTaken - hp0;   // what landed, before armour or the regenerating heart split it
                    Assert.That(double.IsNaN(lost), Is.False);
                    Assert.That(lost, Is.LessThanOrEqualTo(2), "a hit of " + lost + " HP");
                    castRun = e.castT > 0 ? castRun + 1 : 0;
                    if (e.move == "tell") { tellRun++; lastTell = tellRun; } else if (e.move != "charge") tellRun = 0;
                    if (e.move == "rift") { riftRun++; lastRift = riftRun; } else riftRun = 0;
                    if (e.move == "leap") { leapRun++; lastLeap = leapRun; } else leapRun = 0;
                    foreach (var p in run.projectiles)
                    {
                        if (!known.Add(p)) continue;
                        if (p.owner != null && p.owner.kgAdd) continue;   // a recalled add's shells come off its own tells (tested with its kind)
                        if (p.lob) Assert.That(p.flightMax, Is.GreaterThanOrEqualTo(min), "a lob marked too briefly");
                        else if (p.src == (int)FinalBoss.Source.Leap) Assert.That(lastLeap + 1, Is.GreaterThanOrEqualTo(min), "a landing's ring off a leap of " + lastLeap + " ticks");
                        else Assert.That(castBefore + 1, Is.GreaterThanOrEqualTo(min), "a shell off a flash of " + (castBefore + 1) + " ticks");
                    }
                    if (lost <= 0) continue;
                    hits++;
                    int src = Enumerable.Range(0, FinalBoss.Sources).Single(i => run.finalBy[i] > by0[i]);   // every hit is booked, to one attack
                    Assert.That(run.finalBy[src] - by0[src], Is.EqualTo(lost).Within(1e-9), "booked a different amount than it took");
                    bySource[src]++;
                    switch ((FinalBoss.Source)src)
                    {
                        case FinalBoss.Source.Charge: Assert.That(lastTell, Is.GreaterThanOrEqualTo(min), "a charge off a line of " + lastTell + " ticks"); break;
                        case FinalBoss.Source.Rift: Assert.That(lastRift + 1, Is.GreaterThanOrEqualTo(min), "a rift off a strip of " + lastRift + " ticks"); break;
                        case FinalBoss.Source.Leap: Assert.That(lastLeap + 1, Is.GreaterThanOrEqualTo(min), "a leap off a ring of " + lastLeap + " ticks"); break;
                        case FinalBoss.Source.Fault: case FinalBoss.Source.Checker: case FinalBoss.Source.Quadrant: case FinalBoss.Source.Pit:
                            Assert.That(cell, Is.GreaterThanOrEqualTo(0));
                            Assert.That(crack[cell], Is.GreaterThanOrEqualTo(min), "the floor burned under a crack of " + crack[cell] + " ticks");
                            break;
                    }
                }
                ends.Add((int)e.x + "," + (int)e.y);
            }
            Assert.That(hits, Is.GreaterThan(40), "the still target was barely hit: the check proved nothing");
            foreach (var s in new[] { FinalBoss.Source.Fan, FinalBoss.Source.Slam, FinalBoss.Source.Charge, FinalBoss.Source.Rift, FinalBoss.Source.Leap, FinalBoss.Source.Nova })
                Assert.That(bySource[(int)s], Is.GreaterThan(0), s + " never hit the still target: unchecked (" + string.Join(",", bySource) + ")");
            Assert.That(ends.Count, Is.GreaterThan(1), "every fight ended the same: the seeds did nothing");
        }

        [Test]
        public void TheBotFightsTheKingAndBooksEachAttack()
        {
            // a skilled bot on floor 14 with a deep run's hearts (the harness: a fresh kit's 8 HP is not what reaches the King)
            var r = new Runner(7, "skilled", 8);
            for (int f = 1; f < Balance.FinalFloor; f++) r.Run.Descend();
            r.Run.player.maxHp = r.Run.player.hp = 40;
            var res = r.Play();
            var last = res.floors[res.floors.Count - 1];
            Assert.That(res.end, Is.EqualTo("won"), "the bot did not beat the King with 40 HP");
            Assert.That(last.bossTicks, Is.GreaterThan(Balance.Sec(60)), "the fight was over in under a minute");
            Assert.That(last.finalBy, Is.Not.Null.And.Not.Empty, "the fight's damage was not booked by attack");
            Assert.That(last.finalBy!.Values.Sum(), Is.EqualTo(r.Run.finalBy.Sum()).Within(1e-9));
            Assert.That(last.finalBy.Keys, Is.SubsetOf(FinalBoss.SourceNames));
        }

        [Test]
        public void ItDoesNotHurtToTouchWhenItIsNotCharging()
        {
            var (run, e) = Arena();
            e.bossCd = 1e9; e.fbLeft = 0;
            run.player.x = run.player.lagX = e.x; run.player.y = run.player.lagY = e.y + 10;
            for (int t = 0; t < Balance.Sec(1); t++) Step(run, Input.None);
            Assert.That(run.dmgTaken, Is.EqualTo(0), "standing in the idle King hurt");
        }

        [Test]
        public void TheWinComesAfterTheDeathBeat()
        {
            var run = OnFloor(9, Balance.FinalFloor);
            var e = BossOf(run);
            run.readyT = 0; run.fadeT = 0; run.trans = null; run.state = "playing";
            run.projectiles.Add(new Projectile { x = e.x, y = e.y, vx = 1, r = 7, dmg = 1, owner = e, color = FinalBoss.ShellColor });
            Kills.KillEnemy(run, run.enemies.IndexOf(e));
            Assert.That(run.finaleT, Is.EqualTo(FinalBoss.DeathBeat));
            Assert.That((run.finaleX, run.finaleY), Is.EqualTo((e.x, e.y)), "the beat is not where it fell");
            int ticks = 0;
            while (run.state == "playing" && ticks < Balance.Sec(10)) { TickOrder.TickRoom(run); ticks++; }
            Assert.That(run.state, Is.EqualTo("won"));
            Assert.That(ticks, Is.EqualTo(FinalBoss.DeathBeat + 1), "the win came before (or long after) its death beat");
            Assert.That(run.projectiles.Any(p => !p.friendly), Is.False, "its shells outlived it");
            Assert.That(run.pickups.Any(p => p.kind == "exit"), Is.False);
        }

        [Test]
        public void ADeathDuringTheBeatIsADeath()
        {
            var run = OnFloor(9, Balance.FinalFloor);
            var e = BossOf(run);
            run.readyT = 0; run.fadeT = 0; run.trans = null; run.state = "playing";
            Kills.KillEnemy(run, run.enemies.IndexOf(e));
            TickOrder.TickRoom(run);
            run.player.hp = 0; run.player.regenHeart = 0;
            TickOrder.TickRoom(run);
            Assert.That(run.state, Is.EqualTo("gameover"));
        }

        [Test]
        public void TheBotStepsOffACrackAfterItsReactionTimeAndNotBefore()
        {
            var (run, e) = Arena();
            e.phase = 1; e.fbLeft = 0; e.bossCd = 1e9;
            var g = e.ground!;
            var pl = run.player;
            int here = FinalBoss.Ground.CellAt(pl.lagX, pl.lagY + Balance.PlayerHitDy);
            g.Warn(here, FinalBoss.CrackTell, false, FinalBoss.Source.Fault);
            var bot = new BotPolicy(Profile.Get("average"), 1);
            int react = Profile.Get("average").ReactTicks;
            bool blink = false;
            Assert.That(bot.Marks(run, run.CurrentRoom!, 0, false, ref blink), Is.False, "stepped off a crack it has only just seen");
            for (int t = 0; t < react; t++) g.warn[here]--;
            Assert.That(bot.Marks(run, run.CurrentRoom!, react, false, ref blink), "never read the crack under it");
            Assert.That(Math.Abs(bot.Move.x) + Math.Abs(bot.Move.y), Is.GreaterThan(1.5), "stood on the crack");
            // and it does not walk onto a burning cell beside it: east of it burns, the move east is held
            int east = here + 1;
            g.Warn(here, 0, false, FinalBoss.Source.Fault); g.warn[here] = 0;
            g.burn[east] = FinalBoss.Burn;
            pl.x = pl.lagX = FinalBoss.Ground.Left(east) - 8;
            var act = bot.Decide(run, react * 3);
            Assert.That(act.Input.dx, Is.LessThanOrEqualTo(0), "walked onto a burning cell");
        }

        [Test]
        public void TheBotReadsTheRiftAndTheLeap()
        {
            var (run, e) = Arena();
            e.phase = 3; e.fbLeft = 0; e.bossCd = 1e9;
            e.x = run.player.lagX - 200; e.y = run.player.lagY + Balance.PlayerHitDy - 3;   // west of the hitbox, a hair north
            FinalBoss.Begin(run, e, FinalBoss.Attack.Rift);
            var bot = new BotPolicy(Profile.Get("average"), 1);
            int react = Profile.Get("average").ReactTicks;
            bool blink = false;
            Assert.That(bot.Marks(run, run.CurrentRoom!, 0, false, ref blink), Is.False, "read a strip it has only just seen");
            Assert.That(bot.Marks(run, run.CurrentRoom!, react, false, ref blink), "never read the rift's strip");
            Assert.That(Math.Abs(bot.Move.y), Is.GreaterThan(1.5), "stood on the strip");
            Assert.That(Math.Abs(bot.Move.x), Is.LessThan(0.2), "backed along the strip instead of leaving it");

            var (run2, k) = Arena();
            k.phase = 3; k.fbLeft = 0; k.bossCd = 1e9;
            FinalBoss.Begin(run2, k, FinalBoss.Attack.Leap);
            var bot2 = new BotPolicy(Profile.Get("average"), 1);
            Assert.That(bot2.Marks(run2, run2.CurrentRoom!, 0, false, ref blink), Is.False, "read a leap it has only just seen");
            for (int t = 0; t < react; t++) Step(run2, Input.None);
            Assert.That(bot2.Marks(run2, run2.CurrentRoom!, react, false, ref blink), "never read the leap's ring");
            Assert.That(Math.Abs(bot2.Move.x) + Math.Abs(bot2.Move.y), Is.GreaterThan(1.5), "stood under the leap");
        }
    }
}
