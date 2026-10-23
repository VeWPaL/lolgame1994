using System;
using System.Collections.Generic;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The playtest bot's expert profile: stronger than the other three, held to a human's limits (docs/STATUS.md).</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true), CurveOn(true), TickRate(Balance.GameHz)]
    public sealed class ExpertBotTests
    {
        static Profile Expert => Profile.Get("expert");

        static RunState OnFloor(uint seed, int floor)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            for (int f = 1; f < floor; f++) run.Descend();
            return run;
        }

        // a quiet floor-14 room: the player low in it, the King awake on its throne
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
            TickOrder.TickPlayer(run, Input.None);
            return (run, e);
        }

        // the bot's action applied as the runner applies it: the number key, the blink, then the tick's phases
        static void Play(RunState run, BotAction a)
        {
            if (a.WeaponKey > 0) run.player.weaponIdx = a.WeaponKey - 1;
            if (a.Input.blink) TickOrder.TryBlink(run, a.Input);
            TickOrder.TickPlayer(run, a.Input);
            TickOrder.TickProjectiles(run);
            TickOrder.TickBodies(run);
        }

        static Projectile ShellAt(RunState run, double fromX, double fromY)
        {
            var p = run.player;
            double hx = p.lagX, hy = p.lagY + Balance.PlayerHitDy, d = Math.Sqrt((hx - fromX) * (hx - fromX) + (hy - fromY) * (hy - fromY));
            double v = Balance.PerSec(420);
            return new Projectile { x = fromX, y = fromY, vx = (hx - fromX) / d * v, vy = (hy - fromY) / d * v, r = 7, dmg = 1, friendly = false };
        }

        [Test]
        public void ItIsAProfileTheCliTakes()
        {
            Assert.That(Profile.All.Select(p => p.Name), Does.Contain("expert"));
            Assert.That(Cli.Parse(new[] { "play", "--profiles", "skilled,expert" }).Profiles, Is.EqualTo(new[] { "skilled", "expert" }));
            var bad = Assert.Throws<UsageException>(() => Cli.Parse(new[] { "play", "--profiles", "pro" }))!;
            Assert.That(bad.Message, Does.Contain("expert"));
            Assert.That(Profile.All.Where(p => p.Name != "expert").All(p => !p.Expert), "a JS-bot profile plays the expert's brain");
        }

        // limit 1: nothing new is answered before 0.15 s of seeing it
        [Test]
        public void ItAnswersANewShellOnlyAfterItsReactionTime()
        {
            Assert.That(Expert.ReactS, Is.GreaterThanOrEqualTo(0.15));
            var (run, e) = Arena();
            var (ctl, _) = Arena();
            var bot = new BotPolicy(Expert, 1);
            var twin = new BotPolicy(Expert, 1);
            var p = run.player;
            run.projectiles.Add(ShellAt(run, p.x - 150, p.lagY + Balance.PlayerHitDy));   // from the west, straight at the hitbox
            int react = Expert.ReactTicks;
            Assert.That(react, Is.GreaterThanOrEqualTo(Balance.Sec(0.15)));
            for (int t = 0; t < react; t++)
            {
                var a = bot.Decide(run, t); var b = twin.Decide(ctl, t);
                Assert.That(bot.Threatened, Is.EqualTo(0), $"tick {t}: it weighed a shell it had not seen for a reaction time");
                Assert.That((a.Input.dx, a.Input.dy, a.Input.blink), Is.EqualTo((b.Input.dx, b.Input.dy, b.Input.blink)), $"tick {t}: it answered the shell early");
            }
            int before = bot.Decisions;
            bot.Decide(run, react);
            Assert.That(bot.Decisions, Is.EqualTo(before + 1), "it did not decide the moment the shell had been seen for its reaction time");
            Assert.That(bot.Threatened, Is.EqualTo(1), "it never weighed the shell");
        }

        // limit 2: the aim has noise, and the mouse turns at a wrist's rate, never a snap
        [Test]
        public void ItsAimIsNoisyAndTurnsAtACappedRate()
        {
            Assert.That(Expert.AimErr, Is.GreaterThanOrEqualTo(Profile.Get("skilled").AimErr));
            Assert.That(Expert.AimRate, Is.GreaterThan(0).And.LessThanOrEqualTo(25), "a mouse a strong human can turn");
            var (run, e) = Arena();
            var bot = new BotPolicy(Expert, 7);
            double cap = Expert.AimRate / Balance.TickHz, top = 0, last = double.NaN;
            var noise = new List<double>();
            int decided = 0;
            for (int t = 0; t < Balance.Sec(6); t++)
            {
                if (t == Balance.Sec(3)) { e.x = run.player.x; e.y = Balance.RoomBottom - 40; }   // the target jumps behind the player
                run.projectiles.Clear();
                e.bossCd = 9999; e.move = "idle";
                var a = bot.Decide(run, t);
                double ang = Math.Atan2(a.Input.aimY - run.player.y, a.Input.aimX - run.player.x);
                if (!double.IsNaN(last))
                {
                    double d = Math.Abs(Math.IEEERemainder(ang - last, 2 * Math.PI));
                    top = Math.Max(top, d);
                }
                if (bot.Decisions > decided) { decided = bot.Decisions; noise.Add(bot.AimNoise); }
                last = ang;
                Play(run, a);
            }
            Assert.That(top, Is.LessThanOrEqualTo(cap + 1e-9), "the aim turned faster than the cap");
            Assert.That(top, Is.GreaterThan(cap * 0.9), "fixture: the turn never reached the cap, so the cap was not tested");
            double mean = noise.Average(), sd = Math.Sqrt(noise.Average(x => (x - mean) * (x - mean)));
            Assert.That(sd, Is.GreaterThan(Expert.AimErr * 0.35), "the aim has no noise");
        }

        // limit 3: it reads the screen, not the game's dice or a cooldown with no tell
        [Test]
        public void HiddenStateDoesNotChangeWhatItDoes()
        {
            // one real fight, read by two twins: one sees the true state, the other the same screen with every hidden
            // timer and the game's RNG swapped for others (put back before the game steps on the first twin's input)
            var (run, e) = Arena();
            var seen = new BotPolicy(Expert, 3);
            var blind = new BotPolicy(Expert, 3);
            var other = new Rng(987654);
            int differ = -1;
            for (int t = 0; t < Balance.Sec(8) && run.state == "playing" && differ < 0; t++)
            {
                var a = seen.Decide(run, t);
                var keep = run.rng;
                var timers = run.enemies.Select(x => (x.bossCd, x.cdMin, x.cdVar, x.shootCd, x.cdCarry)).ToList();
                run.rng = other;
                foreach (var x in run.enemies) { x.bossCd = 1 + (t * 7) % 50; x.cdMin = 3; x.cdVar = 77; x.shootCd = (t * 3) % 40; x.cdCarry = 2; }
                var b = blind.Decide(run, t);
                run.rng = keep;
                for (int i = 0; i < run.enemies.Count; i++)
                    (run.enemies[i].bossCd, run.enemies[i].cdMin, run.enemies[i].cdVar, run.enemies[i].shootCd, run.enemies[i].cdCarry) = timers[i];
                if ((a.Input.dx, a.Input.dy, a.Input.fire, a.Input.blink, a.Input.aimX, a.Input.aimY, a.WeaponKey, a.UseActive)
                    != (b.Input.dx, b.Input.dy, b.Input.fire, b.Input.blink, b.Input.aimX, b.Input.aimY, b.WeaponKey, b.UseActive)) differ = t;
                Play(run, a);
            }
            Assert.That(differ, Is.EqualTo(-1), $"tick {differ}: a hidden timer or the game's RNG changed the expert's play");
            Assert.That(seen.Decisions, Is.GreaterThan(20), "fixture: hardly a fight");
        }

        sealed class Spy : IPolicy
        {
            public readonly BotPolicy Bot;
            public readonly List<int> DecidedAt = new List<int>();
            public readonly List<Input> Inputs = new List<Input>();
            public Spy(BotPolicy b) => Bot = b;
            public BotAction Decide(RunState run, int t)
            {
                int before = Bot.Decisions;
                var a = Bot.Decide(run, t);
                if (Bot.Decisions > before) DecidedAt.Add(t);
                Inputs.Add(a.Input);
                return a;
            }
        }

        // limit 4: at most 8 decisions in any second, and a held input is held at least 2 ticks
        [Test]
        public void ItDecidesAtMostEightTimesASecondAndHoldsItsInputs()
        {
            var spy = new Spy(new BotPolicy(Expert, 104));
            var r = new Runner(104, "expert", 1.5, spy);
            for (int f = 1; f < Balance.FinalFloor; f++) r.Run.Descend();
            r.Play();
            Assert.That(spy.DecidedAt.Count, Is.GreaterThan(100), "fixture: hardly a fight");
            int most = 0;
            for (int i = 0; i < spy.DecidedAt.Count; i++)
                most = Math.Max(most, spy.DecidedAt.Count(x => x >= spy.DecidedAt[i] && x < spy.DecidedAt[i] + Balance.TickHz));
            Assert.That(most, Is.LessThanOrEqualTo(BotPolicy.MaxPerSecond).And.LessThanOrEqualTo(8), "more decisions in a second than a hand makes");
            Assert.That(most, Is.GreaterThanOrEqualTo(6), "fixture: it never decided fast, so the cap was not tested");
            for (int i = 1; i < spy.DecidedAt.Count; i++)
                Assert.That(spy.DecidedAt[i] - spy.DecidedAt[i - 1], Is.GreaterThanOrEqualTo(2), "two decisions a tick apart");
            int run = 1;
            for (int i = 1; i < spy.Inputs.Count; i++)
            {
                var a = spy.Inputs[i - 1]; var b = spy.Inputs[i];
                bool same = a.dx == b.dx && a.dy == b.dy && a.fire == b.fire;
                if (same) { run++; continue; }
                if (i > 1) Assert.That(run, Is.GreaterThanOrEqualTo(2), $"an input held a single tick, at {i - 1}");
                run = 1;
            }
        }

        [Test]
        public void ANewShellEveryFewTicksStillGetsAtMostEightDecisionsASecond()
        {
            var (run, e) = Arena();
            var bot = new BotPolicy(Expert, 5);
            var at = new List<int>();
            for (int t = 0; t < Balance.Sec(3); t++)
            {
                // a shell every 3 ticks, far off and flying away: each one is new, and each matures on its own tick
                if (t % 3 == 0) run.projectiles.Add(new Projectile { x = Balance.RoomLeft + 20, y = Balance.RoomTop + 20 + t % 90, vx = -0.5, r = 5, dmg = 1 });
                int before = bot.Decisions;
                bot.Decide(run, t);
                if (bot.Decisions > before) at.Add(t);
            }
            int most = at.Select(s => at.Count(x => x >= s && x < s + Balance.TickHz)).Max();
            Assert.That(most, Is.LessThanOrEqualTo(8), "more than 8 decisions in a second");
            Assert.That(most, Is.EqualTo(8), "fixture: the shells did not push it to the cap");
        }

        [Test]
        public void ItPicksTheGunForTheRangeAndTheBody()
        {
            var (run, king) = Arena();
            var bot = new BotPolicy(Expert, 1);
            Assert.That(bot.Gun(run, king, 160, 0), Is.EqualTo(Weapons.BeamIndex), "not the Beam on the King at its best range");
            var lunger = Spawn.Body(run, BodyKind.Lunger, 0, 0);
            Assert.That(bot.Gun(run, lunger, 120, 0), Is.EqualTo(1), "not the Scatter on a small body up close");
            Assert.That(bot.Gun(run, lunger, 120, 1), Is.EqualTo(1), "it left the Scatter up close");
        }

        [Test]
        public void ItWalksOffTheFaultLinesBeforeTheyBurn()
        {
            var (run, e) = Arena();
            var g = e.ground!;
            FinalBoss.Begin(run, e, FinalBoss.Attack.Quake);
            var bot = new BotPolicy(Expert, 1);
            int on = Enumerable.Range(0, FinalBoss.Ground.N).First(i => g.warn[i] > 0 && !FinalBoss.Ground.Edge(i) && FinalBoss.Ground.Row(i) >= 2);
            var pl = run.player;
            pl.x = pl.lagX = FinalBoss.Ground.CentreX(on); pl.y = pl.lagY = FinalBoss.Ground.CentreY(on) - Balance.PlayerHitDy;
            double hp0 = pl.hp + pl.armor + pl.regenHeart;
            for (int t = 0; t < Balance.Sec(2.5); t++)
            {
                run.projectiles.Clear();
                Play(run, bot.Decide(run, t));
            }
            Assert.That(run.finalBy[(int)FinalBoss.Source.Fault], Is.EqualTo(0), $"burned on the fault lines, at {pl.x:0},{pl.y:0}");
            Assert.That(pl.hp + pl.armor + pl.regenHeart, Is.EqualTo(hp0));
        }

        [Test]
        public void ItBeatsTheKingWithTheStartingKitMoreOftenThanTheSkilledProfile()
        {
            int Wins(string profile)
            {
                int w = 0;
                for (uint s = 101; s < 109; s++)
                {
                    var r = new Runner(s, profile, 6);
                    for (int f = 1; f < Balance.FinalFloor; f++) r.Run.Descend();
                    if (r.Play().end == "won") w++;
                }
                return w;
            }
            int expert = Wins("expert"), skilled = Wins("skilled");
            Assert.That(expert, Is.GreaterThanOrEqualTo(6), "the expert lost the finale more than a quarter of the time");
            Assert.That(expert, Is.GreaterThan(skilled));
        }
    }
}
