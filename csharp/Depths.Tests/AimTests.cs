using System;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>Aim.Intercept and the ranged bodies that use it (Balance.TuneAim; owner 2026-10-14).</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class AimTests
    {
        // where a shell fired at angle a from (mx, my) at speed s after a delay is when the target is closest to it
        static double Miss(double a, double mx, double my, double px, double py, double vx, double vy, double delay, double s)
        {
            double best = double.MaxValue;
            for (double t = 0; t < 400; t += 0.25)
            {
                double sx = mx + Math.Cos(a) * s * t, sy = my + Math.Sin(a) * s * t;
                double tx = px + vx * (delay + t), ty = py + vy * (delay + t);
                best = Math.Min(best, Math.Sqrt((sx - tx) * (sx - tx) + (sy - ty) * (sy - ty)));
            }
            return best;
        }

        [Test]
        public void AStillTargetIsShotStraightAt()
        {
            double a = Aim.Intercept(0, 0, 100, 100, 0, 0, 10, 5);
            Assert.That(a, Is.EqualTo(Math.PI / 4).Within(1e-9));
        }

        [Test]
        public void ASlowerTargetIsMetWhereItWillBe()
        {
            var r = new Random(4);
            for (int i = 0; i < 200; i++)
            {
                double px = r.Next(-300, 300), py = r.Next(-300, 300), vx = r.NextDouble() * 4 - 2, vy = r.NextDouble() * 4 - 2, delay = r.Next(0, 30);
                double a = Aim.Intercept(0, 0, px, py, vx, vy, delay, 6);
                Assert.That(Miss(a, 0, 0, px, py, vx, vy, delay, 6), Is.LessThan(3), "case " + i);
            }
        }

        // the JS's fixed-point loop, as StepRanged ran it
        static double Loop(double mx, double my, double px, double py, double vx, double vy, double delay, double s)
        {
            double sx = px - mx, sy = py - my;
            for (int k = 0; k < 14; k++)
            {
                double need = delay + Math.Sqrt(sx * sx + sy * sy) / s;
                sx = px + vx * need - mx; sy = py + vy * need - my;
            }
            return Math.Atan2(sy, sx);
        }

        [Test]
        public void ATargetFasterThanTheShellIsAimedAtNotDownItsPathToInfinity()
        {
            // 6.5 px/tick against a 5 px/tick shell: the JS loop's lead grows every pass, so it aims at a point far
            // out of the room along the target's path; the solve aims where the shell gets closest, inside the horizon
            int ranAway = 0;
            for (int i = 0; i < 32; i++)
            {
                double ang = i * Math.PI / 16, vx = Math.Cos(ang) * 6.5, vy = Math.Sin(ang) * 6.5;
                double straight = Miss(Math.PI / 2, 0, -200, 0, 0, vx, vy, 30, 5);
                double a = Aim.Intercept(0, -200, 0, 0, vx, vy, 30, 5);
                Assert.That(Miss(a, 0, -200, 0, 0, vx, vy, 30, 5), Is.LessThanOrEqualTo(straight + 1e-6), "heading " + i + ": never worse than not leading");
                if (Math.Abs(Math.IEEERemainder(Loop(0, -200, 0, 0, vx, vy, 30, 5) - ang, 2 * Math.PI)) < 0.05) ranAway++;
            }
            Assert.That(ranAway, Is.GreaterThan(8), "the JS loop aims straight down the target's path on many headings");
        }

        [Test]
        public void TheSplittersTwoSpeedsAreSolvedAsOne()
        {
            // 3 px/tick to its split at 120 px, then 8: the solve lands where the shards, not the shell, meet the target
            double vx = 2.5, delay = 20;
            double a = Aim.Intercept(0, 0, 0, 300, vx, 0, delay, 3, 120, 8);
            double t = 0, x = 0, y = 0;
            while (Math.Sqrt(x * x + y * y) < 120) { x += Math.Cos(a) * 3; y += Math.Sin(a) * 3; t++; }
            double best = double.MaxValue;
            for (int k = 0; k < 200; k++, t++)
            {
                x += Math.Cos(a) * 8; y += Math.Sin(a) * 8;
                best = Math.Min(best, Math.Sqrt((x - vx * (delay + t)) * (x - vx * (delay + t)) + (y - 300) * (y - 300)));
            }
            Assert.That(best, Is.LessThan(8));
        }

        // every shot a body fires at a full-momentum player running straight back and forth: how far off its line it leaves
        static double WorstOff(BodyKind kind, bool tuned)
        {
            bool was = Balance.TuneAim;
            Balance.TuneAim = tuned;
            try
            {
                var run = new RunState(new Rng(777)); run.Start(777);
                run.readyT = 0; run.fadeT = 0; run.state = "playing";
                run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
                var p = run.player; p.x = p.lagX = Balance.RoomLeft + 60; p.y = p.lagY = Balance.MidY + 100;
                var e = Spawn.Body(run, kind, Balance.MidX, Balance.RoomTop + 60); e.noticeTimer = 0; e.alerted = true; run.enemies.Add(e);
                int dir = 1, shots = 0, castDir = 0;
                double worst = 0;
                for (int t = 0; t < Balance.Sec(10); t++)
                {
                    if (p.x > Balance.RoomRight - 60) dir = -1; else if (p.x < Balance.RoomLeft + 60) dir = 1;
                    p.momentum = 1; p.swerve = 0;   // a long straight run: the bodies trust its line fully
                    int n = run.projectiles.Count;
                    bool casting = e.castT > 0;
                    TickOrder.TickPlayer(run, new Input(dir, 0)); TickOrder.TickProjectiles(run); TickOrder.TickBodies(run);
                    if (!casting && e.castT > 0) castDir = dir;
                    double ahead = dir > 0 ? Balance.RoomRight - 60 - p.x : p.x - Balance.RoomLeft - 60;
                    for (int i = n; i < run.projectiles.Count; i++)
                    {
                        var q = run.projectiles[i];
                        // the runner turned during the cast, or will before this shot lands: no fair answer to grade
                        if (castDir != dir || ahead < 200) continue;
                        if (q.splitAt <= 0 && kind == BodyKind.Splitter) continue;   // its shards: their parent was the shot
                        // against the shot it should have been: where the runner is when a perfect shell would arrive
                        double sp = Math.Sqrt(q.vx * q.vx + q.vy * q.vy);
                        double ideal = kind == BodyKind.Splitter
                            ? Aim.Intercept(e.x, e.y, p.x, p.y + Balance.PlayerHitDy, p.vx, p.vy, 0, sp, Middle.SplitRange, Middle.SplitShardSpeed)
                            : Aim.Intercept(e.x, e.y, p.x, p.y + Balance.PlayerHitDy, p.vx, p.vy, 0, sp);
                        double off = Math.Abs(Math.IEEERemainder(Math.Atan2(q.vy, q.vx) - ideal, 2 * Math.PI));
                        worst = Math.Max(worst, off - (kind == BodyKind.Sprayer ? Middle.SprayCone : 0));
                        shots++;
                    }
                }
                Assert.That(shots, Is.GreaterThan(1), kind + " fired with room ahead of the runner");
                return worst;
            }
            finally { Balance.TuneAim = was; }
        }

        [Test]
        public void NoRangedBodyShootsAwayFromAStraightRunner()
        {
            double js = WorstOff(BodyKind.Splitter, false), now = WorstOff(BodyKind.Splitter, true);
            TestContext.WriteLine("Splitter worst shot off the meeting line: JS aim " + js.ToString("0.00") + " rad, now " + now.ToString("0.00"));
            Assert.That(js, Is.GreaterThan(now), "the JS loop aims the slow Splitter worse at a fast runner");
            foreach (var k in new[] { BodyKind.Shooter, BodyKind.Gunner, BodyKind.Splitter, BodyKind.Sprayer })
                Assert.That(WorstOff(k, true), Is.LessThan(0.6), k + ": every shot leaves near the line that meets the runner");
        }

        [Test]
        public void TheSplitterAndSprayerFanTighterThanTheyWere()
        {
            Assert.That(Middle.SplitSpread, Is.LessThan(0.3));
            Assert.That(Middle.SprayCone, Is.LessThan(0.26));
        }

        [Test]
        public void AMortarsMarkVariesAndNeverLeadsTooFar()
        {
            var marks = new System.Collections.Generic.List<double>();
            for (uint s = 1; s <= 30; s++)
            {
                var run = new RunState(new Rng(s)); run.Start(s);
                run.readyT = 0; run.fadeT = 0; run.state = "playing";
                run.enemies.Clear(); run.projectiles.Clear();
                var p = run.player; p.x = p.lagX = Balance.RoomLeft + 100; p.y = p.lagY = Balance.MidY + 100;
                var m = Spawn.Body(run, BodyKind.Mortar, Balance.MidX, Balance.RoomTop + 40); m.shootCd = int.MaxValue / 2; run.enemies.Add(m);
                for (int t = 0; t < Balance.Sec(0.7); t++) { p.momentum = 1; TickOrder.TickPlayer(run, new Input(1, 0)); }
                Middle.Release(run, m);
                marks.Add(run.projectiles[0].tx - TickOrder.HitboxX);
            }
            double lo = double.MaxValue, hi = 0;
            foreach (var d in marks) { lo = Math.Min(lo, d); hi = Math.Max(hi, d); }
            Assert.That(hi, Is.LessThanOrEqualTo(Middle.LobLeadMax + 1e-6), "never more than its cap ahead");
            Assert.That(lo, Is.GreaterThan(30), "always some way ahead of a runner");
            Assert.That(hi - lo, Is.GreaterThan(40), "and not the same spot every shell");
        }
    }
}
