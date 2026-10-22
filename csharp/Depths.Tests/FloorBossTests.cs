using System;
using System.Collections.Generic;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The Gatekeeper, the floor boss of floors 1-13 (FloorBoss.cs, Balance.TuneFloorBoss): where it spawns, its
    /// tells, its phases, its area weights, and that nothing it does hurts without a tell.</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class FloorBossTests
    {
        static RunState OnFloor(uint seed, int floor)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            for (int f = 1; f < floor; f++) run.Descend();
            return run;
        }

        // the boss room of the floor, entered the game's way; the boss it spawned
        static Enemy BossOf(RunState run)
        {
            var arena = run.dungeon.AllRooms.First(r => r.Type == RoomKind.Boss);
            run.bossUnlocked = true;
            Rooms.EnterRoom(run, arena.X, arena.Y, Dir.S);
            return run.enemies.Single(e => Bodies.IsBoss(e.kind));
        }

        // a quiet arena on `floor`: the player at the bottom, the Gatekeeper awake at the top
        static (RunState run, Enemy boss) Arena(int floor = 1, uint seed = 31)
        {
            var run = OnFloor(seed, floor);
            run.readyT = 0; run.fadeT = 0; run.trans = null; run.state = "playing";
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
            var p = run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.MidY + 160; p.vx = p.vy = 0;
            var e = Spawn.Body(run, BodyKind.Gatekeeper, Balance.MidX, Balance.MidY - 120);
            e.noticeTimer = 0;
            run.enemies.Add(e);
            TickOrder.TickPlayer(run, Input.None);   // publishes the hitbox the boss aims at
            return (run, e);
        }

        static void Step(RunState run, Input input)
        {
            TickOrder.TickPlayer(run, input);
            TickOrder.TickProjectiles(run);
            TickOrder.TickBodies(run);
        }

        static bool Telling(Enemy e) => e.move == "tell" || e.castT > 0;

        [Test]
        public void TheGatekeeperHoldsFloorsOneToThirteenAndTheWardenTheLast()
        {
            for (uint seed = 1; seed <= 3; seed++)
            {
                var run = OnFloor(seed, 1);
                for (int f = 1; f <= Balance.FinalFloor; f++)
                {
                    if (f > 1) run.Descend();
                    var copy = OnFloor(seed, f);
                    var kind = BossOf(copy).kind;
                    Assert.That(kind, Is.EqualTo(f < Balance.FinalFloor ? BodyKind.Gatekeeper : BodyKind.Boss), "seed " + seed + " floor " + f);
                }
            }
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void UntunedOrSwitchedOffEveryFloorKeepsTheWarden(bool tuned, bool floorBoss)
        {
            bool was = Balance.Tuned, fb = Balance.TuneFloorBoss;
            try
            {
                Balance.Tuned = tuned; Balance.TuneFloorBoss = floorBoss;
                foreach (int f in new[] { 1, 6, 13 })
                    Assert.That(BossOf(OnFloor(5, f)).kind, Is.EqualTo(BodyKind.Boss), "floor " + f);
            }
            finally { Balance.Tuned = was; Balance.TuneFloorBoss = fb; }
        }

        [Test]
        public void ItIsTheFloorsBossToEveryoneThatAsks()
        {
            Assert.That(Bodies.IsBoss(BodyKind.Gatekeeper) && Bodies.IsBoss(BodyKind.Boss));
            Assert.That(Enum.GetValues(typeof(BodyKind)).Cast<BodyKind>().Count(Bodies.IsBoss), Is.EqualTo(2));
            Assert.That(Spikes.CanBeElite(BodyKind.Gatekeeper), Is.False, "a spike room's elite");
            var (run, e) = Arena();
            Assert.That(e.aggroTimer, Is.EqualTo(9999), "a boss starts committed");
            var bot = new BotPolicy(Profile.Get("average"), 1);
            run.enemies.Insert(0, Spawn.Body(run, BodyKind.Lunger, run.player.x + 60, run.player.y));
            bot.Decide(run, 0);   // the bot aims at the boss before the nearer body
            Assert.That(bot.Decide(run, 0).Input.aimY, Is.LessThan(run.player.y - 100), "the bot shot the nearer body, not the boss");
        }

        [Test]
        public void ItsDeathOpensTheWayDown()
        {
            var run = OnFloor(9, 3);
            var e = BossOf(run);
            run.readyT = 0; run.fadeT = 0; run.trans = null; run.state = "playing";
            run.pickups.Clear();
            Kills.KillEnemy(run, run.enemies.IndexOf(e));
            TickOrder.TickRoom(run);
            Assert.That(run.pickups.Any(p => p.kind == "exit"), "no stairs after the floor boss");
            Assert.That(run.state, Is.EqualTo("playing"), "a floor boss won the run");
        }

        // each attack from its first tick: the tell shows at once, and nothing that can hurt (a shell, a landing lob, a
        // charging body) comes before TellMin seconds
        [TestCase(FloorBoss.Attack.Charge)]
        [TestCase(FloorBoss.Attack.Slam)]
        [TestCase(FloorBoss.Attack.Fan)]
        [TestCase(FloorBoss.Attack.Ring)]
        [TestCase(FloorBoss.Attack.Rampage)]
        public void EachAttackShowsItsTellAtLeastPointThreeSecondsBeforeItCanHurt(FloorBoss.Attack a)
        {
            var (run, e) = Arena();
            e.bossCd = 1e9;   // the harness: no attack but this one
            FloorBoss.Begin(run, e, a);
            Assert.That(Telling(e), "the tell is not up as the attack begins");
            int harm = -1, min = Balance.Sec(FloorBoss.TellMin);
            for (int t = 1; t <= Balance.Sec(3) && harm < 0; t++)
            {
                bool told = Telling(e) || run.projectiles.Any(p => p.lob);
                Step(run, Input.None);
                bool landed = run.projectiles.Count == 0 && run.booms > 0;
                if (e.fbHot || run.projectiles.Any(p => !p.lob) || landed) harm = t;
                else Assert.That(told || Telling(e) || run.projectiles.Any(p => p.lob), "no tell on the floor at tick " + t + " before the hit");
                foreach (var p in run.projectiles.Where(p => p.lob))
                    Assert.That(p.flightMax, Is.GreaterThanOrEqualTo(min), "a lob's mark shows for less than the floor");
            }
            Assert.That(harm, Is.GreaterThan(0), a + " never struck");
            Assert.That(harm, Is.GreaterThanOrEqualTo(min), a + " struck " + harm + " ticks in, under " + FloorBoss.TellMin + " s");
        }

        [Test]
        public void EveryTellIsAtLeastPointThreeSeconds()
        {
            int min = Balance.Sec(FloorBoss.TellMin);
            foreach (int t in new[] { FloorBoss.ChargeTell, FloorBoss.RampageTell, FloorBoss.FanTell, FloorBoss.RingTell, FloorBoss.SlamWind, FloorBoss.SlamFlight })
                Assert.That(t, Is.GreaterThanOrEqualTo(min));
        }

        [Test]
        public void ARampageTellsEachOfItsThreeCharges()
        {
            var (run, e) = Arena();
            e.bossCd = 1e9;
            FloorBoss.Begin(run, e, FloorBoss.Attack.Rampage);
            int charges = 0, tellRun = 0;
            bool was = false;
            for (int t = 0; t < Balance.Sec(6); t++)
            {
                bool tellNow = e.move == "tell";
                Step(run, Input.None);
                if (e.move == "charge" && !was)
                {
                    charges++;
                    Assert.That(tellRun, Is.GreaterThanOrEqualTo(Balance.Sec(FloorBoss.TellMin)), "charge " + charges + " came off a short tell");
                }
                tellRun = e.move == "tell" ? tellRun + 1 : tellNow && e.move == "charge" ? tellRun : 0;
                was = e.move == "charge";
            }
            Assert.That(charges, Is.EqualTo(FloorBoss.RampageN));
        }

        [Test]
        public void EachPhaseUnlocksOneMoreAttack()
        {
            var seen = new HashSet<FloorBoss.Attack>[4];
            for (int ph = 1; ph <= 3; ph++)
            {
                seen[ph] = new HashSet<FloorBoss.Attack>();
                foreach (Area area in Enum.GetValues(typeof(Area)))
                    for (int last = -1; last < FloorBoss.Attacks; last++)
                        for (int i = 0; i < 1000; i++) seen[ph].Add(FloorBoss.Pick(area, ph, last, i / 1000.0));
            }
            Assert.That(seen[1], Is.EquivalentTo(new[] { FloorBoss.Attack.Charge, FloorBoss.Attack.Slam, FloorBoss.Attack.Fan }));
            Assert.That(seen[2], Is.EquivalentTo(new[] { FloorBoss.Attack.Charge, FloorBoss.Attack.Slam, FloorBoss.Attack.Fan, FloorBoss.Attack.Ring }));
            Assert.That(seen[3], Has.Count.EqualTo(5));
        }

        [Test]
        public void ItChangesPhaseAtTheWardensThresholdsAndCallsOffItsMove()
        {
            var (run, e) = Arena();
            FloorBoss.Begin(run, e, FloorBoss.Attack.Fan);
            Step(run, Input.None);
            Assert.That(e.castT, Is.GreaterThan(0));
            e.hp = e.maxHp * (Balance.BossPhase1 - 0.01);
            Step(run, Input.None);
            Assert.That(e.phase, Is.EqualTo(2));
            Assert.That(e.castT, Is.EqualTo(0), "the fan's warning outlived the phase change");
            Assert.That(e.move, Is.EqualTo("rest"));
            e.hp = e.maxHp * (Balance.BossPhase2 - 0.01);
            Step(run, Input.None);
            Assert.That(e.phase, Is.EqualTo(3));
        }

        [Test]
        public void EachAreaLeansOnItsOwnAttack()
        {
            int[] Count(Area area)
            {
                var n = new int[FloorBoss.Attacks];
                for (int i = 0; i < 10000; i++) n[(int)FloorBoss.Pick(area, 3, -1, i / 10000.0)]++;
                return n;
            }
            // the leaning attack comes at least 1.3 times as often as any other: a tie (equal weights) is no lean
            int Top(int[] n) { int i = Array.IndexOf(n, n.Max()); return n.Where((c, j) => j != i).All(c => n[i] > 1.3 * c) ? i : -1; }
            Assert.That((FloorBoss.Attack)Top(Count(Area.Area1)), Is.EqualTo(FloorBoss.Attack.Charge), "the Shallows");
            Assert.That((FloorBoss.Attack)Top(Count(Area.Area2)), Is.EqualTo(FloorBoss.Attack.Slam), "the Kiln Works");
            Assert.That((FloorBoss.Attack)Top(Count(Area.Area3)), Is.EqualTo(FloorBoss.Attack.Fan), "the Flooded Galleries");
            var deep = Count(Area.Final);
            Assert.That(deep.Max() - deep.Min(), Is.LessThanOrEqualTo(2), "the Deep weighs them evenly");
        }

        // the live boss, not the table: over a long fight it is not allowed to win, the area's own attack comes most
        [TestCase(2, FloorBoss.Attack.Charge)]
        [TestCase(6, FloorBoss.Attack.Slam)]
        [TestCase(10, FloorBoss.Attack.Fan)]
        public void ALiveFightUsesItsAreasWeights(int floor, FloorBoss.Attack top)
        {
            var (run, e) = Arena(floor);
            var n = new int[FloorBoss.Attacks];
            string prev = e.move;
            for (int t = 0; t < Balance.Sec(240); t++)
            {
                run.player.hp = run.player.maxHp;   // the harness: the player cannot die, and stands still
                e.hp = e.maxHp * 0.2;               // in phase 3, every attack open
                Step(run, Input.None);
                if ((prev == "idle" || prev == "rest") && (e.move == "tell" || e.move == "cast")) n[e.fbAttack]++;
                prev = e.move;
            }
            Assert.That(n.Sum(), Is.GreaterThan(60), "too few attacks to read");
            Assert.That(n.Count(c => c > 0), Is.EqualTo(5), "an attack never came in phase 3");
            Assert.That(n.Where((c, j) => j != (int)top).All(c => n[(int)top] > 1.2 * c), string.Join(",", n) + ": " + top + " is not clearly the most used");
        }

        // a whole fight against a still player that cannot die, over seeds and depths: every hit at most 2 HP, every
        // shell left off a cast flash shown for at least TellMin, every lob marked that long, every contact on a told charge
        [Test]
        public void NothingHurtsWithoutATellAndNoHitIsOverTwo()
        {
            int min = Balance.Sec(FloorBoss.TellMin), hits = 0, shells = 0, lobs = 0, charges = 0;
            var known = new HashSet<Projectile>(ReferenceEqualityComparer.Instance);
            var starts = new HashSet<int>();
            foreach (int floor in new[] { 1, 7, 13 })
                for (uint seed = 1; seed <= 3; seed++)
                {
                    var (run, e) = Arena(floor, seed);
                    int castRun = 0, tellRun = 0, lastTell = 0;
                    double phaseHp = 1;
                    for (int t = 0; t < Balance.Sec(90); t++)
                    {
                        run.player.hp = run.player.maxHp; run.player.armor = 0; run.player.iframes = 0;   // the harness: a still target that lives
                        e.hp = e.maxHp * phaseHp;
                        if (t == Balance.Sec(30)) phaseHp = 0.5;
                        if (t == Balance.Sec(60)) phaseHp = 0.2;
                        double hp0 = run.player.hp;
                        int castBefore = castRun, flying = run.projectiles.Count;
                        bool hot = e.fbHot;
                        Step(run, Input.None);
                        double lost = hp0 - run.player.hp;
                        Assert.That(double.IsNaN(lost), Is.False);
                        Assert.That(lost, Is.LessThanOrEqualTo(2), "a hit of " + lost + " HP");
                        if (lost > 0) hits++;
                        castRun = e.castT > 0 ? castRun + 1 : 0;
                        if (e.move == "tell") { tellRun++; lastTell = tellRun; } else if (e.move != "charge") tellRun = 0;
                        if (e.fbHot)
                        {
                            charges++;
                            Assert.That(lastTell, Is.GreaterThanOrEqualTo(min), "a charge off a tell of " + lastTell + " ticks");
                        }
                        foreach (var p in run.projectiles)
                        {
                            if (!known.Add(p)) continue;
                            if (p.lob) { lobs++; Assert.That(p.flightMax, Is.GreaterThanOrEqualTo(min), "a lob marked too briefly"); }
                            else { shells++; Assert.That(castBefore + 1, Is.GreaterThanOrEqualTo(min), "a shell off a flash of " + (castBefore + 1) + " ticks"); }
                        }
                        if (lost > 0 && !e.fbHot && !hot)
                            Assert.That(flying, Is.GreaterThan(0), "damage with no shell, lob or charge");
                    }
                    starts.Add((int)e.x * 1000 + (int)e.y);
                }
            Assert.That(hits, Is.GreaterThan(20), "the still target was barely hit: the check proved nothing");
            Assert.That(shells, Is.GreaterThan(50)); Assert.That(lobs, Is.GreaterThan(10)); Assert.That(charges, Is.GreaterThan(10));
            Assert.That(starts.Count, Is.GreaterThan(1), "every fight ended the same: the seeds did nothing");
        }

        [Test]
        public void ItDoesNotHurtToTouchWhenItIsNotCharging()
        {
            var (run, e) = Arena();
            e.bossCd = 1e9;
            run.player.x = run.player.lagX = e.x; run.player.y = run.player.lagY = e.y + 10;
            for (int t = 0; t < Balance.Sec(1); t++) Step(run, Input.None);
            Assert.That(run.dmgTaken, Is.EqualTo(0), "standing in the idle boss hurt");
        }

        [Test]
        public void TheDepthScalesItThroughTheDepthDials()
        {
            var (r1, b1) = Arena(1);
            var (r9, b9) = Arena(10);
            Assert.That(b9.maxHp / b1.maxHp, Is.EqualTo(Balance.DepthTough(10) / Balance.DepthTough(1)).Within(1e-9), "health");
            Assert.That(b1.cdMin / b9.cdMin, Is.EqualTo(Balance.DepthRate(10) / Balance.DepthRate(1)).Within(1e-9), "pace");
            Assert.That(b9.speed / b1.speed, Is.EqualTo(Balance.DepthRate(10) / Balance.DepthRate(1)).Within(1e-9), "walk");
        }

        [Test]
        public void TheBotStepsOffTheChargeLineAfterItsReactionTime()
        {
            var (run, e) = Arena();
            e.x = run.player.lagX - 150; e.y = run.player.lagY + Balance.PlayerHitDy - 4;   // west of the hitbox, a hair north
            FloorBoss.Begin(run, e, FloorBoss.Attack.Charge);
            var bot = new BotPolicy(Profile.Get("average"), 1);
            int react = Profile.Get("average").ReactTicks;
            bool blink = false;
            Assert.That(bot.Marks(run, run.CurrentRoom!, 0, false, ref blink), Is.False, "stepped off a line it has only just seen");
            Assert.That(bot.Marks(run, run.CurrentRoom!, react, false, ref blink), "never read the Gatekeeper's line");
            Assert.That(Math.Abs(bot.Move.y), Is.GreaterThan(1.5), "stood on the line");
            Assert.That(Math.Abs(bot.Move.x), Is.LessThan(0.2), "backed along the line instead of leaving it");
            e.move = "charge"; e.fbGone = 20;
            Assert.That(bot.Marks(run, run.CurrentRoom!, react + 1, false, ref blink), "blind to the charge itself");
        }
    }
}
