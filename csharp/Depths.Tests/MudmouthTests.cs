using System;
using System.Collections.Generic;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Depths.Tests
{
    /// <summary>Sets <see cref="Balance.TuneBosses"/> (the region bosses' kit) for a test and restores it after.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TunedBossesAttribute : Attribute, ITestAction
    {
        readonly bool _on;
        readonly Stack<bool> _was = new Stack<bool>();
        public TunedBossesAttribute(bool on) { _on = on; }
        public ActionTargets Targets => ActionTargets.Test;
        public void BeforeTest(ITest test) { _was.Push(Balance.TuneBosses); Balance.TuneBosses = _on; }
        public void AfterTest(ITest test) => Balance.TuneBosses = _was.Pop();
    }

    /// <summary>The region bosses' kit (Bosses.cs: the draw, flat health, the books) and Old Mudmouth (Mudmouth.cs), from
    /// docs/bosses.md sections 1, 3, 8 and S5.</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class MudmouthTests
    {
        [TearDown] public void NoForce() => Bosses.Force = null;

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

        // a quiet arena: the player at the bottom, Mudmouth awake at the top
        static (RunState run, Enemy boss) Arena(int floor = 1, uint seed = 31)
        {
            var run = OnFloor(seed, floor);
            run.readyT = 0; run.fadeT = 0; run.trans = null; run.state = "playing";
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
            var p = run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.MidY + 160; p.vx = p.vy = 0;
            var e = Spawn.Body(run, BodyKind.Mudmouth, Balance.MidX, Balance.MidY - 120);
            e.noticeTimer = 0;
            run.enemies.Add(e);
            TickOrder.TickPlayer(run, Input.None);
            return (run, e);
        }

        static void Step(RunState run, Input input)
        {
            TickOrder.TickPlayer(run, input);
            TickOrder.TickProjectiles(run);
            TickOrder.TickBodies(run);
        }

        // ------------------------------------------------------------------ the draw

        [Test]
        public void EachAreaDrawsFourOfItsSixWithoutRepeatsAndFloorThirteenOneOfThree()
        {
            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 200; seed++)
            {
                var d = Bosses.Draw(seed);
                Assert.That(d[0], Is.Null); Assert.That(d[Balance.FinalFloor], Is.Null);
                for (int a = 0; a < 3; a++)
                {
                    var four = Enumerable.Range(a * 4 + 1, 4).Select(f => d[f]!).ToList();
                    Assert.That(four.Distinct().Count(), Is.EqualTo(4), "seed " + seed + ": a boss twice in area " + a);
                    Assert.That(four.All(id => Bosses.Rosters[a].Contains(id)), "seed " + seed + ": a boss from another area");
                }
                Assert.That(Bosses.Rosters[3], Does.Contain(d[13]));
                Assert.That(Bosses.Draw(seed), Is.EqualTo(d), "the draw is the seed's");
                seen.Add(string.Join(",", d.Skip(1).Take(13)));
            }
            Assert.That(seen.Count, Is.GreaterThan(190), "the seeds draw different bosses");
            // every roster entry turns up somewhere, floor 13's three included
            var all = Enumerable.Range(1, 200).SelectMany(s => Bosses.Draw((uint)s).Where(x => x != null)).Distinct().Count();
            Assert.That(all, Is.EqualTo(21));
        }

        [Test]
        public void TheDrawMovesNoOtherRoll()
        {
            // the draw has its own stream, and Mudmouth spawns on the Gatekeeper's draws: kit on or off, a Mudmouth floor's
            // streams, layout, pools and the boss's place match once its room is entered
            int tried = 0;
            for (uint seed = 1; seed <= 80; seed++)
            {
                if (Bosses.Draw(seed)[1] != "mudmouth") continue;
                tried++;
                string Shape(bool on)
                {
                    bool was = Balance.TuneBosses;
                    try
                    {
                        Balance.TuneBosses = on;
                        var run = OnFloor(seed, 1);
                        var rooms = string.Join(";", run.dungeon.AllRooms.Select(r => r.X + "," + r.Y + "," + r.Type + "," + r.Spike));
                        var b = BossOf(run);
                        Assert.That(b.kind, Is.EqualTo(on ? BodyKind.Mudmouth : BodyKind.Gatekeeper), "fixture");
                        return run.rng.RunCalls + "|" + run.rng.JitterCalls + "|" + b.x + "," + b.y + "|" + rooms + "|" + Pools.Describe(run.pools!);
                    }
                    finally { Balance.TuneBosses = was; }
                }
                Assert.That(Shape(true), Is.EqualTo(Shape(false)), "seed " + seed);
            }
            Assert.That(tried, Is.GreaterThan(3), "fixture: few seeds drew Mudmouth on floor 1");
        }

        [Test]
        public void ADrawnBossNotBuiltYetIsTheGatekeeperAndMudmouthIsItself()
        {
            int mud = 0, keeper = 0;
            for (uint seed = 1; seed <= 12; seed++)
                for (int f = 1; f <= 5; f++)
                {
                    var run = OnFloor(seed, f);
                    var kind = BossOf(run).kind;
                    Assert.That(kind, Is.EqualTo(run.bosses![f] == "mudmouth" ? BodyKind.Mudmouth : BodyKind.Gatekeeper), "seed " + seed + " floor " + f);
                    if (kind == BodyKind.Mudmouth) mud++; else keeper++;
                }
            Assert.That(mud, Is.GreaterThan(0)); Assert.That(keeper, Is.GreaterThan(0));
        }

        [Test, TunedBosses(false)]
        public void WithTheKitOffEveryFloorIsTheGatekeeper()
        {
            for (uint seed = 1; seed <= 12; seed++)
                Assert.That(BossOf(OnFloor(seed, 1 + (int)(seed % 4))).kind, Is.EqualTo(BodyKind.Gatekeeper));
        }

        [Test]
        public void TheForceHookMakesEveryShallowsDrawMudmouthAndNoOtherArea()
        {
            Bosses.Force = "mudmouth";
            for (int f = 1; f <= 6; f++)
                Assert.That(BossOf(OnFloor(77, f)).kind, f <= 4 ? Is.EqualTo(BodyKind.Mudmouth) : Is.EqualTo(BodyKind.Gatekeeper), "floor " + f);
        }

        // ------------------------------------------------------------------ health, the books

        [Test]
        public void ItsHealthIsFlatAcrossTheShallowsNotScaledByDepth()
        {
            var hp = new List<double>();
            for (int f = 1; f <= 4; f++) hp.Add(Arena(f).boss.maxHp);
            Assert.That(hp.Distinct().Count(), Is.EqualTo(1), string.Join(", ", hp));
            Assert.That(hp[0], Is.EqualTo(Bosses.AreaHp[0] * Mudmouth.HpScale).Within(1e-9));
            Assert.That(Balance.DepthTough(4), Is.GreaterThan(Balance.DepthTough(1)), "fixture: depth does toughen bodies");
            Assert.That(Bosses.Health(6, 1), Is.EqualTo(2100)); Assert.That(Bosses.Health(10, 1), Is.EqualTo(2150)); Assert.That(Bosses.Health(13, 1), Is.EqualTo(1150));
        }

        [Test]
        public void EveryHitItLandsIsBookedToItsAttack()
        {
            // a leap landing on a still player, a spit lob, a ring shell and a brood landing: each booked under its key
            var (run, e) = Arena();
            var p = run.player;
            e.move = "leap"; e.fbAttack = (int)Mudmouth.Attack.Leap; e.leapX = e.x; e.leapY = e.y;
            e.markX = p.lagX; e.markY = p.lagY + Balance.PlayerHitDy; e.moveT = e.fbTellMax = 1; e.buried = true;
            double before = run.dmgTaken;
            TickOrder.TickBodies(run);
            Assert.That(run.dmgTaken - before, Is.EqualTo(Mudmouth.LeapDmg));
            Assert.That(run.bossBy["mudmouth:leap"], Is.EqualTo(Mudmouth.LeapDmg));

            p.iframes = 0;
            run.projectiles.Add(new Projectile { x = p.lagX, y = p.lagY, tx = p.lagX, ty = p.lagY + Balance.PlayerHitDy, lob = true, flight = 0, flightMax = 1, r = 8, dmg = 2, blastR = 40, owner = e, book = "mudmouth:spit" });
            TickOrder.TickProjectiles(run);
            Assert.That(run.bossBy["mudmouth:spit"], Is.EqualTo(2));

            p.iframes = 0;
            var s = new Projectile { x = p.lagX, y = p.lagY + Balance.PlayerHitDy, vx = 0.01, r = 7, dmg = 1, owner = e, book = "mudmouth:ring" };
            run.projectiles.Add(s);
            TickOrder.TickProjectiles(run);
            Assert.That(run.bossBy["mudmouth:ring"], Is.EqualTo(1));
            Assert.That(run.dmgTaken - before, Is.EqualTo(run.bossBy.Values.Sum()), "every hit booked, nothing unbooked");
            Assert.That(run.hitBook, Is.Null, "the book is closed after each hit");
        }

        // ------------------------------------------------------------------ the rubric

        static readonly Mudmouth.Attack[] All = (Mudmouth.Attack[])Enum.GetValues(typeof(Mudmouth.Attack));

        [Test]
        public void EveryTellIsAtLeastPointThreeFiveSecondsAndNoHitIsOverTwo()
        {
            foreach (var t in new[] { Mudmouth.CrackTell, Mudmouth.LeapTime, Mudmouth.SpitFlight, Mudmouth.TunnelTell, Mudmouth.BroodWind })
                Assert.That(t, Is.GreaterThanOrEqualTo(Balance.Sec(Mudmouth.TellMin)));
            Assert.That(Middle.BurrowLeap, Is.GreaterThanOrEqualTo(Balance.Sec(Mudmouth.TellMin)), "its brood's landing mark");
            foreach (var d in new[] { Mudmouth.LeapDmg, Mudmouth.SpitDmg, Mudmouth.RingDmg, Mudmouth.TunnelDmg, Middle.BurrowDmg })
                Assert.That(d, Is.LessThanOrEqualTo(2));
        }

        // each attack begun on a still player: the first tick anything hurts comes at least TellMin after the attack's
        // hurting tell (a crack, a mark, a line) first shows, and the tell shows before the body is buried out of sight
        [TestCase(Mudmouth.Attack.Leap)]
        [TestCase(Mudmouth.Attack.Spit)]
        [TestCase(Mudmouth.Attack.Ring)]
        [TestCase(Mudmouth.Attack.Double)]
        [TestCase(Mudmouth.Attack.Tunnel)]
        public void EachAttackIsToldAtLeastTellMinBeforeItCanHurt(Mudmouth.Attack a)
        {
            int told = -1, hurt = -1;
            for (int off = 0; off < 8 && hurt < 0; off++)   // a ring has gaps: the still player is moved along until one hits
            {
            var (run, e) = Arena();
            run.player.x = run.player.lagX = Balance.MidX + off * 9;
            e.phase = 3; e.hp = e.maxHp * 0.2;
            Mudmouth.Begin(run, e, a);
            told = -1;
            for (int t = 0; t < Balance.Sec(6) && hurt < 0; t++)
            {
                run.player.iframes = 0;
                double before = run.dmgTaken;
                Step(run, Input.None);
                bool tell = e.move == "crack" || e.move == "leap" || e.move == "dirt" || run.projectiles.Any(q => q.lob && q.owner == e);
                if (tell && told < 0) told = t;
                if (run.dmgTaken > before) hurt = t;
                if (run.dmgTaken > before) Assert.That(run.dmgTaken - before, Is.LessThanOrEqualTo(2), "one hit over 2 HP");
            }
            }
            Assert.That(hurt, Is.GreaterThanOrEqualTo(0), "fixture: a still player was never hit by " + a);
            Assert.That(told, Is.GreaterThanOrEqualTo(0));
            Assert.That(hurt - told, Is.GreaterThanOrEqualTo(Balance.Sec(Mudmouth.TellMin)), a + ": told " + told + ", hurt " + hurt);
        }

        [Test]
        public void ItsBodyNeverHurtsByContact()
        {
            var (run, e) = Arena();
            var p = run.player;
            e.bossCd = 99999;   // it never starts an attack: only its resting body is tested
            for (int t = 0; t < Balance.Sec(2); t++)
            {
                p.x = p.lagX = e.x; p.y = p.lagY = e.y - Balance.PlayerHitDy;
                Step(run, Input.None);
                p.iframes = 0;
            }
            Assert.That(run.dmgTaken, Is.EqualTo(0));
        }

        [Test]
        public void ItsGapsAreTheRubricsAtFloorOneAndDividedByTheDepthRate()
        {
            var e1 = Arena(1).boss;
            Assert.That(e1.cdMin / Balance.TickHz, Is.EqualTo(0.16).Within(0.5 / Balance.TickHz));
            Assert.That((e1.cdMin + e1.cdVar) / Balance.TickHz, Is.EqualTo(0.46).Within(1.0 / Balance.TickHz));
            var e4 = Arena(4).boss;
            Assert.That(Balance.DepthRate(4), Is.GreaterThan(Balance.DepthRate(1)), "fixture: floor 4 is quicker");
            Assert.That(e4.cdMin * Balance.DepthRate(4), Is.EqualTo(e1.cdMin * Balance.DepthRate(1)).Within(1e-9));
            Assert.That(e4.speed / Balance.DepthRate(4), Is.EqualTo(e1.speed / Balance.DepthRate(1)).Within(1e-9));
        }

        // ------------------------------------------------------------------ phases

        [TestCase(1, 0.65, 2)]
        [TestCase(2, 0.32, 3)]
        public void APhaseChangeIsToldCallsOffTheMoveAndChangesTheRoom(int from, double frac, int to)
        {
            var (run, e) = Arena();
            e.phase = from;
            Mudmouth.Begin(run, e, Mudmouth.Attack.Leap);
            for (int t = 0; t < Balance.Sec(1) && e.move != "under"; t++) Step(run, Input.None);
            Assert.That(e.move, Is.EqualTo("under")); Assert.That(e.buried);
            e.hp = e.maxHp * frac;
            Step(run, Input.None);
            Assert.That(e.phase, Is.EqualTo(to));
            Assert.That(e.move, Is.EqualTo("shift"), "the leap in hand was called off");
            Assert.That(e.buried, Is.False, "it surfaces for its phase change");
            int shift = 1;
            while (e.move == "shift") { Step(run, Input.None); shift++; }
            Assert.That(shift, Is.EqualTo(Mudmouth.ShiftTime).Within(1));
            Assert.That(Mudmouth.ShiftTime, Is.EqualTo(Balance.Sec(2.4)));
        }

        [Test]
        public void ThePhaseWaitsWhileItLiesUnearthed()
        {
            var (run, e) = Arena();
            Mudmouth.Unearth(run, e);
            e.hp = e.maxHp * 0.5;
            Step(run, Input.None);
            Assert.That(e.phase, Is.EqualTo(1)); Assert.That(e.move, Is.EqualTo("stunned"));
        }

        [Test]
        public void EachPhaseHasItsOwnAttacks()
        {
            var rolls = Enumerable.Range(0, 1000).Select(i => i / 1000.0).ToList();
            HashSet<Mudmouth.Attack> In(int ph) => new HashSet<Mudmouth.Attack>(rolls.Select(r => Mudmouth.Pick(ph, -1, r)));
            Assert.That(In(1), Is.EquivalentTo(new[] { Mudmouth.Attack.Leap, Mudmouth.Attack.Spit, Mudmouth.Attack.Ring }));
            Assert.That(In(2), Does.Contain(Mudmouth.Attack.Double).And.Not.Contain(Mudmouth.Attack.Tunnel));
            Assert.That(In(3), Does.Contain(Mudmouth.Attack.Tunnel).And.Contain(Mudmouth.Attack.Brood));
            Assert.That(rolls.Select(r => Mudmouth.Pick(3, -1, r, false)), Has.None.EqualTo(Mudmouth.Attack.Brood), "no brood while one lives");
        }

        // ------------------------------------------------------------------ the signature: the blast on the crack

        static (RunState run, Enemy e) Cracking()
        {
            var (run, e) = Arena();
            Mudmouth.Begin(run, e, Mudmouth.Attack.Leap);
            for (int t = 0; t < Balance.Sec(4) && e.move != "crack"; t++) Step(run, Input.None);
            Assert.That(e.move, Is.EqualTo("crack"), "fixture: it never cracked");
            Assert.That(e.buried, "fixture: it is underground while it cracks");
            return (run, e);
        }

        [Test]
        public void ABlastOnTheCrackUnearthsItStunnedTwoSecondsAtDoubleDamage()
        {
            var (run, e) = Cracking();
            double plain = e.Vuln, hp = e.hp;
            Blast.Explode(run, e.x + 60, e.y);
            Assert.That(e.move, Is.EqualTo("stunned")); Assert.That(e.buried, Is.False);
            Assert.That(e.unearthed, Is.EqualTo(1));
            Assert.That(e.hp, Is.LessThan(hp), "the blast that unearths it also hits it");
            Assert.That(e.Vuln, Is.EqualTo(plain * 2).Within(1e-12), "double damage taken (the spec)");
            int t = 0;
            while (e.move == "stunned") { e.bossCd = 9999; Step(run, Input.None); t++; }
            Assert.That(t, Is.EqualTo(Mudmouth.StunTime).Within(1));
            Assert.That(Mudmouth.StunTime, Is.EqualTo(Balance.Sec(2)));
            Assert.That(e.Vuln, Is.EqualTo(plain).Within(1e-12), "back to plain damage after");
        }

        [Test]
        public void ABlastAwayFromTheCrackOrNotOnACrackOrAHookDoesNothing()
        {
            var (run, e) = Cracking();
            Blast.Explode(run, e.x + Balance.AltAoe + e.r + 5, e.y);
            Assert.That(e.move, Is.EqualTo("crack"), "out of reach");
            Blast.Explode(run, e.x, e.y, "hook");
            Assert.That(e.move, Is.EqualTo("crack"), "the hook is not the blast");
            var (run2, e2) = Arena();
            Mudmouth.Begin(run2, e2, Mudmouth.Attack.Leap);
            for (int t = 0; t < Balance.Sec(1) && e2.move != "under"; t++) Step(run2, Input.None);
            Blast.Explode(run2, e2.x, e2.y);
            Assert.That(e2.move, Is.EqualTo("under")); Assert.That(e2.buried, "underground and not cracking: untouchable");
        }

        // ------------------------------------------------------------------ the room: soft mud

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void FromPhaseTwoTheMudItPassesUnderStaysSoftForThreeSeconds(int phase, bool soft)
        {
            var (run, e) = Arena();
            e.phase = phase; e.hp = e.maxHp * (phase == 2 ? 0.5 : 1);
            Mudmouth.Begin(run, e, Mudmouth.Attack.Leap);
            for (int t = 0; t < Balance.Sec(1.2) && e.move != "crack"; t++) Step(run, Input.None);
            int live = e.mud!.life.Count(l => l > 0);
            Assert.That(live > 0, Is.EqualTo(soft), "patches laid: " + live);
            if (!soft) return;
            Assert.That(e.mud.life.Max(), Is.LessThanOrEqualTo(Mudmouth.MudLife));
            for (int t = 0; t < Mudmouth.MudLife; t++) e.mud.Tick();
            Assert.That(e.mud.life.Count(l => l > 0), Is.EqualTo(0), "dry after 3 s");
        }

        [Test]
        public void SoftMudSlowsThePlayer()
        {
            double Run(bool mud)
            {
                var (run, e) = Arena();
                e.bossCd = 99999;
                var p = run.player;
                p.x = p.lagX = Balance.MidX - 200;
                if (mud) for (int i = 0; i < 20; i++) { e.mud!.Lay(p.x + i * Mudmouth.MudStep, p.y + Balance.PlayerHitDy); }
                double x0 = p.x;
                for (int t = 0; t < Balance.Sec(0.8); t++) { Step(run, new Input(1, 0)); for (int i = 0; i < Mudmouth.Mud.Max; i++) if (e.mud!.life[i] > 0) e.mud.life[i] = Mudmouth.MudLife; }
                return p.x - x0;
            }
            double dry = Run(false), wet = Run(true);
            Assert.That(dry, Is.GreaterThan(50), "fixture: the player moved");
            Assert.That(wet / dry, Is.EqualTo(Mudmouth.MudSlow).Within(0.08));
        }

        // ------------------------------------------------------------------ the brood and the death beat

        [Test]
        public void TheBroodIsTwoBurrowersOfItsOwnAndCrumblesWhenItFalls()
        {
            var (run, e) = Arena();
            e.phase = 3; e.hp = e.maxHp * 0.2;
            Mudmouth.Begin(run, e, Mudmouth.Attack.Brood);
            for (int t = 0; t <= Mudmouth.BroodWind; t++) Step(run, Input.None);
            var brood = run.enemies.Where(o => o.rbAdd).ToList();
            Assert.That(brood.Count, Is.EqualTo(2));
            Assert.That(brood.All(b => b.kind == BodyKind.Burrower && b.book == "mudmouth:brood"));
            Assert.That(Mudmouth.BroodAlive(run), Is.EqualTo(2));
            // it falls: the brood crumbles as the death beat begins, the way down waits for the beat
            var room = run.CurrentRoom!;
            room.Type = RoomKind.Boss; room.ExitOpen = false;
            Kills.KillEnemy(run, run.enemies.IndexOf(e));
            Assert.That(run.finaleT, Is.EqualTo(Mudmouth.DeathBeat));
            int beat = 0;
            while (!run.pickups.Any(pk => pk.kind == "exit") && beat < Balance.Sec(5)) { TickOrder.TickRoom(run); beat++; }
            Assert.That(run.enemies.Count(o => o.rbAdd), Is.EqualTo(0), "the brood crumbled");
            Assert.That(beat, Is.EqualTo(Mudmouth.DeathBeat + 1).Within(1), "the stairs after the death beat");
            Assert.That(run.state, Is.EqualTo("playing"));
        }

        [Test]
        public void ADroppedFightIsBeatableAndItsBooksAddUp()
        {
            // a long fight against a player that stands and fires: every hit is booked, none over 2 HP
            var (run, e) = Arena();
            var p = run.player;
            double max = 0;
            for (int t = 0; t < Balance.Sec(90) && e.hp > 0 && run.enemies.Contains(e); t++)
            {
                double before = run.dmgTaken;
                Step(run, new Input(0, 0, true, false, false, e.x, e.y));
                max = Math.Max(max, run.dmgTaken - before);
                p.hp = p.maxHp;   // the harness keeps the player alive (said so): only the books are measured
            }
            Assert.That(run.dmgTaken, Is.GreaterThan(0), "fixture: it never hit");
            Assert.That(max, Is.LessThanOrEqualTo(2));
            Assert.That(run.bossBy.Values.Sum(), Is.EqualTo(run.dmgTaken).Within(1e-9), "a hit went unbooked");
            Assert.That(run.bossBy.Keys.All(k => k.StartsWith("mudmouth:")));
        }

        // ------------------------------------------------------------------ the bot reads it

        [Test]
        public void TheBotBlastsASeenCrackAfterItsReactionTimeOnItsProfilesWillingness()
        {
            int react = Profile.Get("average").ReactTicks, threw = 0, seeds = 60;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var (run, e) = Cracking();
                var bot = new BotPolicy(Profile.Get("average"), seed);
                Assert.That(bot.Decide(run, 1000).Input.alt, Is.False, "it has not had time to see the crack");
                var later = bot.Decide(run, 1000 + react);
                if (!later.Input.alt) continue;
                threw++;
                Assert.That(Math.Atan2(later.Input.aimY - run.player.y, later.Input.aimX - run.player.x),
                            Is.EqualTo(Math.Atan2(e.y - run.player.y, e.x - run.player.x)).Within(0.3), "aimed at the crack");
                run.player.altCooldown = 10;
                Assert.That(bot.Decide(run, 1001 + react).Input.alt, Is.False, "no blast ready, no throw");
            }
            // average's willingness is Profile.Blast 0.55: some cracks are let go, most are thrown at
            Assert.That(threw / (double)seeds, Is.EqualTo(Profile.Get("average").Blast).Within(0.2), "threw " + threw + " of " + seeds);
        }

        [Test]
        public void TheBotWalksOffSoftMudAndNotIntoIt()
        {
            // a patch just to the right of its feet: every seed's bot (whichever way it circles) steps left, off it
            for (uint seed = 1; seed <= 12; seed++)
            {
                var (run, e) = Arena();
                e.bossCd = 99999;
                var p = run.player;
                var bot = new BotPolicy(Profile.Get("average"), seed);
                e.mud!.Lay(p.x + 14, p.y + Balance.PlayerHitDy);
                var a = bot.Decide(run, 0).Input;
                Assert.That(a.dx, Is.LessThan(0), "seed " + seed + ": it did not step off the mud");
            }
        }
    }
}
