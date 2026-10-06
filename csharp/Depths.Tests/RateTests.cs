using System;
using System.Collections.Generic;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The sim at 60 Hz plays the game the 210 Hz one does: each test runs one behaviour at both rates
    /// and compares it in seconds or pixels. Every tolerance is a named discrete-time error (a wait
    /// rounded to whole ticks, a step of travel, the knock cut-off), never a tuned margin.
    /// </summary>
    [TestFixture, Category("csharp-only")]
    public sealed class RateTests
    {
        const double Tick60 = 1.0 / 60, Tick210 = 1.0 / 210;
        const double OneTickEach = Tick60 + Tick210;   // one wait rounded differently at each rate

        static (T at60, T at210) Both<T>(Func<T> measure)
        {
            int was = Balance.TickHz;
            try
            {
                Balance.TickHz = 60;
                var a = measure();
                Balance.TickHz = 210;
                var b = measure();
                return (a, b);
            }
            finally { Balance.TickHz = was; }
        }

        static double Seconds(int ticks) => ticks / (double)Balance.TickHz;

        static RunState Started()
        {
            var run = new RunState(new Rng(4242));
            run.Start(4242);
            run.readyT = 0; run.fadeT = 0;
            run.enemies.Clear(); run.pickups.Clear();
            return run;
        }

        // a body far in a corner: the room is a live fight, and nothing reaches the player
        static Enemy Bystander(RunState run)
        {
            var e = Enemy.Of(BodyKind.Lunger, Balance.RoomRight - 40, Balance.RoomBottom - 40);
            e.stun = 1e9;   // the harness holds it, so it never walks
            run.enemies.Add(e);
            return e;
        }

        // ---------------------------------------------------------------- the rate setting itself

        [Test]
        public void TheGameRateIsSixtyAndTheParityRateIsTheJsRate()
        {
            Assert.That(Balance.GameHz, Is.EqualTo(60));
            Assert.That(Balance.JsHz, Is.EqualTo(210));
            Assert.That(new Cli().Hz, Is.Null, "the bot plays at the game's own rate unless told otherwise");
        }

        [Test]
        public void ChangingTheRateTakesEffectEverywhereAndComesBack()
        {
            // nothing derived from the rate may be frozen at type load
            Balance.TickHz = 60;
            Assert.That(Balance.Sec(1), Is.EqualTo(60));
            Assert.That(Balance.HookHold, Is.EqualTo(42));
            Assert.That(Balance.KnockFriction, Is.EqualTo(Math.Pow(0.976, 3.5)).Within(1e-15));
            Assert.That(Bodies.Of(BodyKind.Shooter).CdMin, Is.EqualTo(30));
            Assert.That(Bodies.Of(BodyKind.Brunch).Run, Is.EqualTo(441 / 60.0).Within(1e-12));
            Assert.That(Weapons.All[0].Cooldown, Is.EqualTo(38).Within(1e-9));
            Assert.That(Weapons.MuzzleTicks, Is.EqualTo(6));
            Assert.That(new Player().speed, Is.EqualTo(0.935 * 252 / 60).Within(1e-12));
            Balance.TickHz = 210;
            Assert.That(Balance.Sec(1), Is.EqualTo(210));
            Assert.That(Bodies.Of(BodyKind.Shooter).CdMin, Is.EqualTo(105));
            Assert.That(Weapons.All[0].Cooldown, Is.EqualTo(133));
            Assert.That(new Player().speed, Is.EqualTo(1.122));
        }

        [Test, TickRate(60)]
        public void AFixtureCanAskForTheGameRate() => Assert.That(Balance.TickHz, Is.EqualTo(60));

        [Test]
        public void AtTheJsRateEveryConvertedDialIsItsOldValueExactly()
        {
            Assert.That(Balance.TickHz, Is.EqualTo(210), "the suite's own rate");
            // the per-tick numbers the port carried before the switch, read back bit for bit
            var was = new (string name, double now, double before)[]
            {
                ("PlayerMove", Balance.PlayerMove, 1.2), ("player speed", new Player().speed, 1.122),
                ("MomentumStallDecay", Balance.MomentumStallDecay, 0.006), ("MomentumMoveFloor", Balance.MomentumMoveFloor, 0.3),
                ("SwerveDecay", Balance.SwerveDecay, 0.0035), ("ShootSlowRecover", Balance.ShootSlowRecover, 0.105),
                ("PlayerSpeedEps", Balance.PlayerSpeedEps, 0.05), ("MoveEps", Balance.MoveEps, 0.05),
                ("PlayerAnimEps", Balance.PlayerAnimEps, 0.12), ("BlinkDirEps", Balance.BlinkDirEps, 0.1),
                ("BrunchStill", Balance.BrunchStill, 0.02),
                ("SlowEase", Balance.SlowEase, 0.079), ("HitboxLagEase", Balance.HitboxLagEase, 0.05),
                ("LungeTrack", Balance.LungeTrack, 0.014), ("LungeBeliefTrack", Balance.LungeBeliefTrack, 0.14),
                ("BrunchAccel", Balance.BrunchAccel, 0.09), ("BrunchDecel", Balance.BrunchDecel, 0.16),
                ("Ease(MoveAccel)", Balance.Ease(Balance.MoveAccel), 0.116), ("EaseK", Balance.EaseK(0.0348), 1),
                ("KnockFriction", Balance.KnockFriction, 0.976), ("KnockPFriction", Balance.KnockPFriction, 0.958),
                ("KnockScale", Balance.KnockScale, 1), ("KnockPScale", Balance.KnockPScale, 1),
                ("KnockMax", Balance.KnockMax, 5), ("KnockTrade", Balance.KnockTrade, 0.09),
                ("KnockCut", Balance.KnockCut, 0.006), ("KnockPCut", Balance.KnockPCut, 0.006),
                ("GunnerDodgeChance", Balance.GunnerDodgeChance, 0.5), ("GunnerDodgeKick", Balance.GunnerDodgeKick, 0.6),
                ("AltSpeed", Balance.AltSpeed, 1.87), ("HookSpeed", Balance.HookSpeed, 2.04), ("HookSuck", Balance.HookSuck, 0.24),
                ("HookFieldStun", Balance.HookFieldStun, 3), ("HookFieldFlash", Balance.HookFieldFlash, 1),
                ("BossShotSpeed", Balance.BossShotSpeed, 1.9),
                ("BrunchShieldSpeed", Balance.BrunchShieldSpeed, 0.72), ("BrunchRun", Balance.BrunchRun, 2.1),
                ("BrunchWalkSpeed", Balance.BrunchWalkSpeed, 0.624), ("WanderSpeed", Balance.WanderSpeed, 0.25),
                ("LungeSpeed", Balance.LungeSpeed, 4.2), ("LungerNear", Balance.LungerNear, 0.55),
                ("LungerFar", Balance.LungerFar, 1.3), ("LungerSpread", Balance.LungerSpread, 0.9),
                ("shooter Base", Bodies.Of(BodyKind.Shooter).Base!.Value, 0.45 * 0.96),
                ("gunner Base", Bodies.Of(BodyKind.Gunner).Base!.Value, 0.3 * 0.96),
                ("boss Base", Bodies.Of(BodyKind.Boss).Base!.Value, 0.6 * 0.96),
                ("shooter shot", Bodies.Of(BodyKind.Shooter).PShotSpeed!.Value, 2.2),
                ("gunner shot", Bodies.Of(BodyKind.Gunner).PShotSpeed!.Value, 2.05),
                ("lunger walk", Bodies.Of(BodyKind.Lunger).Walk!.Value, 0.3 * 0.96 * 1.2),
                ("lunger run", Bodies.Of(BodyKind.Lunger).Run!.Value, 0.82 * 0.96 * 1.2),
                ("Bolt", Weapons.All[0].Cooldown, 133), ("Scatter", Weapons.All[1].Cooldown, 231),
                ("Arcane Beam", Weapons.All[2].Cooldown, 18.2), ("Voidball", Weapons.All[3].Cooldown, 84),
                ("Bolt speed", Weapons.All[0].Speed, 2.8), ("Scatter speed", Weapons.All[1].Speed, 2.4),
                ("Beam speed", Weapons.All[2].Speed, 4), ("Voidball speed", Weapons.All[3].Speed, 3.2),
                ("notice window", Balance.SecF(16 / 60.0), 56),
            };
            var off = was.Where(w => w.now != w.before).Select(w => w.name + " " + w.now.ToString("R") + " (was " + w.before.ToString("R") + ")").ToList();
            Assert.That(off, Is.Empty);

            var ticks = new (string name, int now, int before)[]
            {
                ("HitFlash", Balance.HitFlash, 27), ("BrunchAbsorbFlash", Balance.BrunchAbsorbFlash, 21),
                ("KnockStun", Balance.KnockStun, 88), ("WanderTicks", Balance.WanderTicks, 210),
                ("GunnerDodgeCd", Balance.GunnerDodgeCd, 126), ("AltCooldown", Balance.AltCooldown, 756),
                ("BossVolleyGap", Balance.BossVolleyGap, 116), ("BossWallLife", Balance.BossWallLife, 2940),
                ("HookHold", Balance.HookHold, 147), ("HookField", Balance.HookField, 378),
                ("HookForget", Balance.HookForget, 1470), ("HookEarlyMin", Balance.HookEarlyMin, 38),
                ("BrunchScanTicks", Balance.BrunchScanTicks, 20), ("BossWarnTime", Balance.BossWarnTime, 672),
                ("BlinkRecharge", Balance.BlinkRecharge, 735), ("FadeDescend", RunState.FadeDescend, 189),
                ("Iframes", Balance.Iframes, 210), ("RegenDelay", Balance.RegenDelay, 840),
                ("UnlockTime", Balance.UnlockTime, 105), ("FadeClear", Balance.FadeClear, 32),
                ("BlinkIframes", Balance.BlinkIframes, 36), ("BlinkGrace", Balance.BlinkGrace, 126),
                ("DashTrail", Balance.DashTrail, 48), ("MuzzleTicks", Weapons.MuzzleTicks, 21),
                ("shooter CdMin", (int)Bodies.Of(BodyKind.Shooter).CdMin!.Value, 105),
                ("gunner CdVar", (int)Bodies.Of(BodyKind.Gunner).CdVar!.Value, 126),
            };
            var offT = ticks.Where(w => w.now != w.before).Select(w => w.name + " " + w.now + " (was " + w.before + ")").ToList();
            Assert.That(offT, Is.Empty);
        }

        // ---------------------------------------------------------------- the player

        [Test]
        public void EachWeaponFiresAsOftenPerSecond()
        {
            // held fire shoots when the cooldown has run out, so a period is the cooldown rounded up to whole ticks
            for (int w = 0; w < Weapons.All.Length; w++)
            {
                int weapon = w;
                var (p60, p210) = Both(() =>
                {
                    var run = Started();
                    run.player.weaponIdx = weapon;
                    var fired = new List<int>();
                    int shots = 0;
                    for (int t = 0; t < 10 * Balance.TickHz; t++)
                    {
                        TickOrder.TickPlayer(run, new Input(0, 0, fire: true, aimX: Balance.RoomRight, aimY: Balance.MidY));
                        if (run.shots != shots) { shots = run.shots; fired.Add(t); }
                    }
                    return Seconds(fired[fired.Count - 1] - fired[0]) / (fired.Count - 1);
                });
                Assert.That(p60, Is.EqualTo(p210).Within(OneTickEach), Weapons.All[w].Name + ": seconds between shots");
            }
        }

        [Test]
        public void ThePlayerRunsTheSameGroundAndChargesTheSameMomentum()
        {
            // a live fight, so Momentum charges; the ease is exact, the travel differs by a step at each rate
            var (r60, r210) = Both(() =>
            {
                var run = Started();
                Bystander(run);
                run.player.x = run.player.lagX = Balance.RoomLeft + 60;
                double x0 = run.player.x, at1 = 0, m = 0;
                for (int t = 1; t <= Balance.TickHz; t++)
                {
                    TickOrder.TickPlayer(run, new Input(1, 0));
                    if (t == Balance.TickHz / 2) at1 = run.player.x - x0;
                    if (t == Balance.TickHz / 4) m = run.player.momentum;   // still charging (full by about 0.4 s)
                }
                return (half: at1, one: run.player.x - x0, speed: run.player.vx * Balance.TickHz, momentum: m);
            });
            double top = 1.6045 * 210;   // px/s at full momentum, the most one step can carry
            double step = top * OneTickEach;
            Assert.That(r60.half, Is.EqualTo(r210.half).Within(step), "ground covered in half a second, px");
            Assert.That(r60.one, Is.EqualTo(r210.one).Within(step), "ground covered in a second, px");
            Assert.That(r60.speed, Is.EqualTo(r210.speed).Within(1e-6 * r210.speed), "speed after a second, px/s: the ease is exact once momentum is full");
            Assert.That(r60.momentum, Is.EqualTo(r210.momentum).Within(Balance.MomentumGain * step), "momentum after a quarter second: it is earned per px");
        }

        [Test]
        public void TheSwerveAndMomentumMetersDrainPerSecond()
        {
            var (m60, m210) = Both(() =>
            {
                var run = Started();
                Bystander(run);
                run.player.swerve = 1; run.player.momentum = 1;
                double momentum = 0;
                for (int t = 1; t <= Balance.TickHz; t++)
                {
                    TickOrder.TickPlayer(run, Input.None);
                    if (t == Balance.TickHz / 2) momentum = run.player.momentum;
                }
                return (run.player.swerve, momentum);
            });
            // both drains are linear, so there is no step error, only float rounding
            Assert.That(m60.swerve, Is.EqualTo(1 - 0.735).Within(1e-9), "the swerve meter after a second");
            Assert.That(m210.swerve, Is.EqualTo(1 - 0.735).Within(1e-9));
            Assert.That(m60.momentum, Is.EqualTo(1 - 1.26 / 2).Within(1e-9), "momentum standing still for half a second");
            Assert.That(m210.momentum, Is.EqualTo(1 - 1.26 / 2).Within(1e-9));
        }

        [Test]
        public void IframesLastASecond()
        {
            var (s60, s210) = Both(() =>
            {
                var run = Started();
                Combat.DamagePlayer(run, 1, 0, 0, 0);
                int t = 0;
                while (run.player.iframes > 0) { TickOrder.TickPlayer(run, Input.None); t++; }
                return Seconds(t);
            });
            Assert.That(s60, Is.EqualTo(1.0).Within(Tick60));
            Assert.That(s60, Is.EqualTo(s210).Within(OneTickEach));
        }

        [Test]
        public void AKnockedPlayerSlidesTheSameDistance()
        {
            // the knock's coast distance is kept exactly; only the cut-off tail (under 0.006 px a JS tick) differs
            var (d60, d210) = Both(() =>
            {
                var run = Started();
                double x0 = run.player.x;
                Combat.DamagePlayer(run, 1, 1, 0, 5.5 * Balance.KnockPGain);
                for (int t = 0; t < 3 * Balance.TickHz; t++) TickOrder.TickPlayer(run, Input.None);
                return run.player.x - x0;
            });
            double tail = 2 * 0.006 / (1 - Balance.KnockPFrictionJs);
            Assert.That(d210, Is.EqualTo(5.5 * 0.3 / (1 - 0.958)).Within(tail), "the JS-rate slide, px");
            Assert.That(d60, Is.EqualTo(d210).Within(tail));
        }

        [Test]
        public void TheRegenHeartRefillsOnTheSameClock()
        {
            var (s60, s210) = Both(() =>
            {
                var run = Started();
                Bystander(run);
                run.player.regenHeart = 0;
                int first = -1, t = 0;
                while (run.player.regenHeart < run.player.regenHeartMax)
                {
                    TickOrder.TickRegen(run); t++;
                    if (first < 0 && run.player.regenHeart > 0) first = t;
                }
                return (first: Seconds(first), full: Seconds(t));
            });
            Assert.That(s60.first, Is.EqualTo(4.0).Within(Tick60), "the first half heart after 4 s");
            Assert.That(s60.full, Is.EqualTo(5.0).Within(Tick60), "then one each second");
            Assert.That(s60.first, Is.EqualTo(s210.first).Within(OneTickEach));
            Assert.That(s60.full, Is.EqualTo(s210.full).Within(OneTickEach));
        }

        [Test]
        public void TheBlinkGoesAsFarAndRechargesAsFast()
        {
            var (b60, b210) = Both(() =>
            {
                var run = Started();
                Bystander(run);
                double x0 = run.player.x;
                TickOrder.DoBlink(run, new Input(1, 0));
                double dist = run.player.x - x0, safe = Seconds(run.player.iframes);
                int t = 0;
                while (run.blinkCharges < 2) { TickOrder.TickBlink(run); t++; }
                double live = Seconds(t);
                run.enemies.Clear();
                run.blinkCharges = 1; run.player.blinkRegen = 0; t = 0;
                while (run.blinkCharges < 2) { TickOrder.TickBlink(run); t++; }
                return (dist, safe, live, cleared: Seconds(t));
            });
            Assert.That(b60.dist, Is.EqualTo(Balance.BlinkDist), "blink distance, px");
            Assert.That(b210.dist, Is.EqualTo(Balance.BlinkDist));
            Assert.That(b60.safe, Is.EqualTo(b210.safe).Within(2 * OneTickEach), "i-frames: two rounded waits");
            Assert.That(b60.live, Is.EqualTo(3.5).Within(Tick60), "a charge in a fight, s");
            Assert.That(b60.live, Is.EqualTo(b210.live).Within(OneTickEach));
            // nine steps a tick in a cleared room: the charge lands on the tick that crosses the line
            Assert.That(b60.cleared, Is.EqualTo(b210.cleared).Within(OneTickEach), "a charge in a cleared room, s");
        }

        // ---------------------------------------------------------------- bodies

        [Test]
        public void ALungerChasesAtTheSameSpeed()
        {
            var (c60, c210) = Both(() =>
            {
                var run = Started();
                var p = run.player;
                p.x = p.lagX = Balance.RoomLeft + 40;
                var e = Enemy.Of(BodyKind.Lunger, Balance.RoomRight - 40, Balance.MidY);
                e.lungeCd = int.MaxValue / 2;   // the harness holds the lunge back: this measures the approach
                run.enemies.Add(e);
                double x0 = e.x, half = 0;
                for (int t = 1; t <= Balance.TickHz; t++)
                {
                    double dx = p.x - e.x, dy = p.y - e.y, d = Math.Sqrt(dx * dx + dy * dy);
                    Movement.StepLunge(run, e, dx / d, dy / d, d, 1);
                    if (t == Balance.TickHz / 2) half = x0 - e.x;
                }
                return (half, one: x0 - e.x, speed: e.curSpeed * Balance.TickHz);
            });
            double step = 273 * OneTickEach;   // LungerFar, px/s: the most one step carries
            Assert.That(c60.half, Is.EqualTo(c210.half).Within(step), "ground closed in half a second, px");
            Assert.That(c60.one, Is.EqualTo(c210.one).Within(step), "ground closed in a second, px");
            Assert.That(c60.speed, Is.EqualTo(c210.speed).Within(1e-6 * c210.speed), "chase speed after a second, px/s");
            Assert.That(c210.speed, Is.EqualTo(273).Within(1), "and it is the far approach speed");
        }

        static Enemy HeldLunger(RunState run, double x, double y)
        {
            var e = Enemy.Of(BodyKind.Lunger, x, y);
            e.stun = 1e9;   // the harness holds it stunned, so only the knock moves it
            run.enemies.Add(e);
            return e;
        }

        [Test]
        public void AKnockedBodySlidesTheSameDistance()
        {
            var (d60, d210) = Both(() =>
            {
                var run = Started();
                var e = HeldLunger(run, 300, Balance.MidY);
                Movement.Knock(run, e, 1, 0, 4 * Balance.KnockGain);
                for (int t = 0; t < 3 * Balance.TickHz; t++) TickOrder.TickBodies(run);
                return e.x - 300;
            });
            double tail = 2 * 0.006 / (1 - Balance.KnockFrictionJs);
            Assert.That(d210, Is.EqualTo(4 * 0.294 / (1 - 0.976)).Within(tail), "the JS-rate slide, px");
            Assert.That(d60, Is.EqualTo(d210).Within(tail));
        }

        [Test]
        public void TheHookPullsAsFarAndBurnsAsMuch()
        {
            var (h60, h210) = Both(() =>
            {
                var run = Started();
                var e = HeldLunger(run, 300, Balance.MidY);
                Blast.Explode(run, 400, Balance.MidY, "hook");
                run.hookFields.Clear();   // the pull alone: the field's suction is measured next
                for (int t = 0; t < 3 * Balance.TickHz; t++) TickOrder.TickBodies(run);
                double pulled = e.x - 300;

                var f = HeldLunger(run, 400, Balance.MidY + 100);
                f.stun = 0;
                double hp0 = f.hp;
                Blast.Explode(run, 400, Balance.MidY + 100, "hook");
                double held = Seconds((int)f.stun);
                while (run.hookFields.Count > 0) Blast.TickFields(run);
                return (pulled, held, burned: hp0 - f.hp);
            });
            double tail = 2 * 0.006 / (1 - Balance.KnockFrictionJs);
            Assert.That(h210.pulled, Is.EqualTo(1.2 * 100).Within(tail), "the hook pulls a body 1.2x its distance, px");
            Assert.That(h60.pulled, Is.EqualTo(h210.pulled).Within(tail));
            Assert.That(h60.held, Is.EqualTo(h210.held).Within(OneTickEach), "the hold, s");
            // the field burns HookDps a second for its life, less its last tick at either rate
            double dps = Balance.HookDps * Bodies.Armour;
            Assert.That(h210.burned, Is.EqualTo(dps * 1.8).Within(dps * Tick210 * 1.01));
            Assert.That(h60.burned, Is.EqualTo(h210.burned).Within(dps * OneTickEach));
        }

        [Test]
        public void TheWardenMovesOnTheSameClock()
        {
            // the same moves (the draws per move do not depend on the rate), each started within the
            // rounded waits so far: a cooldown, up to three wind-up and volley gaps, and a recover per move
            const int Moves = 8;
            var (w60, w210) = Both(() =>
            {
                var run = Started();
                run.player.x = Balance.MidX; run.player.y = Balance.RoomBottom - 30;
                var boss = Spawn.Body(run, BodyKind.Boss, Balance.MidX, Balance.RoomTop + 80);
                run.enemies.Add(boss);
                var starts = new List<(string move, double at)>();
                string last = boss.move;
                for (int t = 1; starts.Count < Moves && t < 120 * Balance.TickHz; t++)
                {
                    TickOrder.StepBoss(run, boss);
                    if (last == "idle" && boss.move != "idle") starts.Add((boss.move, Seconds(t)));
                    last = boss.move;
                }
                return starts;
            });
            Assert.That(w60.Select(s => s.move), Is.EqualTo(w210.Select(s => s.move)), "the same moves in the same order");
            for (int i = 0; i < Moves; i++)
                Assert.That(w60[i].at, Is.EqualTo(w210[i].at).Within((i + 1) * 5 * OneTickEach), "move " + i + " (" + w60[i].move + ") starts at, s");
        }

        [Test]
        public void ABrunchPackRampsToTheSameChaseSpeed()
        {
            var (s60, s210) = Both(() =>
            {
                var run = Started();
                var e = Enemy.Of(BodyKind.Brunch, Balance.RoomRight - 40, Balance.MidY);
                e.aggroTimer = 1000000;
                run.enemies.Add(e);
                run.player.x = run.player.lagX = Balance.RoomLeft + 40;
                TickOrder.HitboxX = run.player.x; TickOrder.HitboxY = run.player.y;
                var speeds = new List<double>();
                for (int t = 1; t <= 3 * Balance.TickHz / 2; t++)
                {
                    TickOrder.TickBodies(run);
                    if (t % (Balance.TickHz / 2) == 0) speeds.Add(e.curSpeed * Balance.TickHz);
                }
                return speeds;
            });
            // the ramp's gain is a function of seconds in the chase; the ease is exact for a fixed gain, and
            // the gain moves by one tick's worth between samples, so a percent covers it
            for (int i = 0; i < s60.Count; i++)
                Assert.That(s60[i], Is.EqualTo(s210[i]).Within(0.01 * s210[i]), "chase speed at " + (i + 1) * 0.5 + " s, px/s");
        }

        // ---------------------------------------------------------------- the bot at the game rate

        [Test, TickRate(60)]
        public void TheBotIsDeterministicAtTheGameRateAndSaysSo()
        {
            var a = Matrix.Play("a", new uint[] { 7 }, new[] { "average" }, 0.5);
            var b = Matrix.Play("a", new uint[] { 7 }, new[] { "average" }, 0.5);
            Assert.That(a.tickHz, Is.EqualTo(60), "the header records the rate it played at");
            Assert.That(Json.Write(a), Is.EqualTo(Json.Write(b)));
            Assert.That(a.runs[0].ticks, Is.EqualTo(30 * 60), "half a minute is 1800 ticks at 60 Hz");
        }

        [Test]
        public void TheBotsReactionsAreTheJsBotsInSeconds()
        {
            // 55 / 38 / 24 JS ticks at 210 Hz, the same time at 60
            var (r60, r210) = Both(() => Profile.All.Select(p => Seconds(p.ReactTicks)).ToList());
            Assert.That(r210, Is.EqualTo(new[] { 55 / 210.0, 38 / 210.0, 24 / 210.0 }));
            for (int i = 0; i < r60.Count; i++) Assert.That(r60[i], Is.EqualTo(r210[i]).Within(Tick60 / 2 + 0.001));
        }

        [Test]
        public void TheBotTakesHzOnTheCommandLine()
        {
            Assert.That(Cli.Parse(new[] { "play", "--hz", "210" }).Hz, Is.EqualTo(210));
            Assert.Throws<UsageException>(() => Cli.Parse(new[] { "play", "--hz", "0" }));
            Assert.Throws<UsageException>(() => Cli.Parse(new[] { "play", "--hz", "sixty" }));
        }
    }
}
