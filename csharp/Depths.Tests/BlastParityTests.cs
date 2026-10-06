using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The right-click blast against the running game: rows recorded by tools/blast-parity.js on
    /// 2026-10-06. Seed 4242, the player at (300, 400), the RNG reseeded just before fireAlt(), then
    /// the projectile phase alone until the blast is gone. Compared: where it was aimed (clamped to
    /// the room), the tick it went off, each body's hp, knock, stun, slow and flash (or death), the
    /// kills, and both streams' draw counts (a kill rolls loot on the run stream).
    /// </summary>
    [TestFixture]
    public sealed class BlastParityTests
    {
        static void Check(double aimX, double aimY, (BodyKind kind, double x, double y)[] bodies, double tx, double ty,
                          int ticks, double[]?[] want, int kills, int jitterDraws, int runDraws)
        {
            var run = new RunState(new Rng(4242)) { dungeon = Dungeon.FromSeed(new Rng(4242)) };
            run.state = "playing";
            run.CurrentRoom!.Secret = null;
            var p = run.player;
            p.x = p.lagX = 300; p.y = p.lagY = 400; p.altMode = "blast"; p.altCooldown = 0; p.shootSlow = 0.5;
            var placed = new System.Collections.Generic.List<Enemy>();
            foreach (var b in bodies) { var e = Enemy.Of(b.kind, b.x, b.y); e.noticeTimer = 0; e.aggroTimer = 0; run.enemies.Add(e); placed.Add(e); }
            int j0 = run.rng.JitterCalls, r0 = run.rng.RunCalls;
            Weapons.FireAlt(run, aimX, aimY);
            Assert.That(p.altCooldown, Is.EqualTo(504));
            Assert.That(p.shootSlow, Is.EqualTo(0.7).Within(1e-9));
            Assert.That(run.projectiles[0].tx, Is.EqualTo(tx).Within(1e-9), "aim clamped to the room");
            Assert.That(run.projectiles[0].ty, Is.EqualTo(ty).Within(1e-9));
            int t = 0;
            while (run.projectiles.Count > 0 && t < 400) { TickOrder.TickProjectiles(run); t++; }
            Assert.That(t, Is.EqualTo(ticks), "the tick the blast went off");
            for (int i = 0; i < placed.Count; i++)
            {
                var e = placed[i];
                if (want[i] == null) { Assert.That(run.enemies, Does.Not.Contain(e), "body " + i + " should be dead"); continue; }
                var w = want[i]!;
                Assert.That(run.enemies, Does.Contain(e), "body " + i + " died");
                Assert.That(e.hp, Is.EqualTo(w[0]).Within(1e-6), "hp " + i);
                Assert.That(e.kvx, Is.EqualTo(w[1]).Within(1e-6), "kvx " + i);
                Assert.That(e.kvy, Is.EqualTo(w[2]).Within(1e-6), "kvy " + i);
                Assert.That(e.stun, Is.EqualTo((int)w[3]), "stun " + i);
                Assert.That(e.slowT, Is.EqualTo((int)w[4]), "slowT " + i);
                Assert.That(e.hitFlash, Is.EqualTo((int)w[5]), "hitFlash " + i);
            }
            Assert.That(run.kills, Is.EqualTo(kills));
            Assert.That(run.rng.JitterCalls - j0, Is.EqualTo(jitterDraws), "jitter draws");
            Assert.That(run.rng.RunCalls - r0, Is.EqualTo(runDraws), "run draws");
        }

        [Test]
        public void Cluster() => Check(450.0, 300.0, new[] { (BodyKind.Lunger, 450.0, 300.0), (BodyKind.Lunger, 480.0, 320.0), (BodyKind.Lunger, 430.0, 350.0) },
            450.0, 300.0, 83, new double[]?[] { new[] { 18.26925960098332, 4.0342024843818, -2.6894683229212, 88.0, 116.0, 27.0 }, new[] { 18.26925960098332, 3.4851329350685765, 0.41767599690590135, 88.0, 116.0, 27.0 }, new[] { 18.26925960098332, 0.10313039007046916, 4.341232538144513, 88.0, 116.0, 27.0 } }, 0, 0, 0);

        [Test]
        public void Contact() => Check(600.0, 300.0, new[] { (BodyKind.Lunger, 420.0, 360.0) },
            600.0, 300.0, 54, new double[]?[] { null }, 1, 0, 1);

        [Test]
        public void Wall() => Check(900.0, 200.0, new[] { (BodyKind.Lunger, 700.0, 250.0) },
            750.0, 200.0, 226, new double[]?[] { null }, 1, 0, 1);

        [Test]
        public void Kill() => Check(500.0, 250.0, new[] { (BodyKind.Shooter, 500.0, 250.0) },
            500.0, 250.0, 120, new double[]?[] { null }, 1, 0, 1);
    }
}
