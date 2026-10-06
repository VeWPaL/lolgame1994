using System.Collections.Generic;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The projectile phase against the running game. Every expectation below was produced by
    /// <c>tools/proj-parity.js</c> on 2026-10-06 (after the loop's `break` was fixed to `continue`),
    /// with the same fixture: seed 4242, a cleared standard room, the player parked at (700, 500), the
    /// bodies placed by hand, one tick. The case names are the probe's.
    /// </summary>
    [TestFixture]
    public sealed class ProjectilePhaseParityTests
    {
        static RunState Room(params Enemy[] bodies)
        {
            var run = new RunState(new Rng(4242)) { dungeon = Dungeon.FromSeed(new Rng(4242)) };
            run.state = "playing";
            run.player.x = 700; run.player.y = 500; run.player.lagX = 700; run.player.lagY = 500;
            run.player.hp = 8; run.player.maxHp = 8;
            foreach (var e in bodies) { e.noticeTimer = 0; e.aggroTimer = 0; run.enemies.Add(e); }
            return run;
        }

        // the probe's defaults: r 3, dmg 1, friendly, muzzle at (400, 300), a band with fMin 1
        static Projectile Shell(double x, double y, double vx, double vy, double dmg = 1, bool friendly = true,
                                int pierce = 0, Enemy? owner = null) =>
            new Projectile { x = x, y = y, vx = vx, vy = vy, r = 3, dmg = dmg, friendly = friendly, ox = 400, oy = 300,
                             fNear = 1, fFar = 1, fMin = 1, pierce = pierce, scale = 1, dx = 1, dy = 0, owner = owner };

        [Test]
        public void AShellWithNoBodiesFliesAndAges()
        {
            var run = Room();
            run.projectiles.Add(Shell(400, 300, 2, 0));
            TickOrder.TickProjectiles(run);
            Assert.That(run.projectiles, Has.Count.EqualTo(1));
            Assert.That(run.projectiles[0].x, Is.EqualTo(402));
        }

        [Test]
        public void TheCullMarginIsThirtyPixels()
        {
            var far = Room();
            far.projectiles.Add(Shell(Balance.RoomLeft - 40, 300, -2, 0));
            TickOrder.TickProjectiles(far);
            Assert.That(far.projectiles, Is.Empty, "a shell 40px outside the west wall is removed");

            var near = Room();
            near.projectiles.Add(Shell(Balance.RoomLeft - 10, 300, -2, 0));
            TickOrder.TickProjectiles(near);
            Assert.That(near.projectiles, Has.Count.EqualTo(1), "a shell 10px outside survives");
        }

        [Test]
        public void AFriendlyShellHitsOneBodyAndIsConsumed()
        {
            var t = Enemy.Of(BodyKind.Lunger, 400, 300);
            var run = Room(t);
            double hp0 = t.hp;
            run.projectiles.Add(Shell(398, 300, 2, 0, dmg: 10));
            TickOrder.TickProjectiles(run);
            Assert.That(run.projectiles, Is.Empty);
            Assert.That(hp0 - t.hp, Is.EqualTo(6.6).Within(1e-6), "10 damage through the lunger's 0.66 armour");
            Assert.That(t.hitFlash, Is.Not.Zero);
            Assert.That(run.hits, Is.EqualTo(1));
        }

        [Test]
        public void AShellInAKnotOfThreeHitsExactlyOne()
        {
            var run = Room(Enemy.Of(BodyKind.Lunger, 404, 300), Enemy.Of(BodyKind.Lunger, 398, 300), Enemy.Of(BodyKind.Lunger, 400, 304));
            var hp0 = run.enemies.ConvertAll(e => e.hp);
            run.projectiles.Add(Shell(392, 300, 4, 0, dmg: 10));
            TickOrder.TickProjectiles(run);
            int damaged = 0;
            for (int i = 0; i < run.enemies.Count; i++) if (run.enemies[i].hp < hp0[i]) damaged++;
            Assert.That(damaged, Is.EqualTo(1));
            Assert.That(run.projectiles, Is.Empty);
        }

        [Test]
        public void APiercingShellPassesThroughAndFallsOff()
        {
            var run = Room(Enemy.Of(BodyKind.Lunger, 402, 300), Enemy.Of(BodyKind.Lunger, 408, 300), Enemy.Of(BodyKind.Lunger, 414, 300));
            run.projectiles.Add(Shell(396, 300, 3, 0, dmg: 10, pierce: 2));
            TickOrder.TickProjectiles(run);
            Assert.That(run.projectiles, Has.Count.EqualTo(1));
            var p = run.projectiles[0];
            Assert.That(p.pierce, Is.EqualTo(1));
            Assert.That(p.scale, Is.EqualTo(0.72).Within(1e-9));
            Assert.That(p.hit, Has.Count.EqualTo(1));
            Assert.That(p.hit![0], Is.SameAs(run.enemies[0]), "the first body along the flight, not the first in the list");
        }

        [Test]
        public void ABrunchAbsorbsAnEnemyShell()
        {
            var br = Enemy.Of(BodyKind.Brunch, 400, 300);
            var run = Room(br);
            run.projectiles.Add(Shell(398, 300, 2, 0, dmg: 10, friendly: false));
            TickOrder.TickProjectiles(run);
            Assert.That(run.projectiles, Is.Empty);
            Assert.That(br.hp, Is.EqualTo(2.7).Within(1e-9), "absorbing costs the Brunch nothing");
            Assert.That(br.hitFlash, Is.GreaterThan(0));
        }

        [Test]
        public void ABrunchSparesTheShellOfTheBodyItGuardsAndEatsTheOther()
        {
            // the guard is IDENTITY: owner-less shells are eaten even if a shooter "fired" them
            var b = Enemy.Of(BodyKind.Brunch, 400, 300);
            var guard = Enemy.Of(BodyKind.Shooter, 450, 300);
            var run = Room(b, guard);
            b.shieldTarget = guard;
            run.projectiles.Add(Shell(398, 300, 2, 0, dmg: 10, friendly: false));
            run.projectiles.Add(Shell(398, 300, 2, 0, dmg: 10, friendly: false));
            TickOrder.TickProjectiles(run);
            Assert.That(run.projectiles, Is.Empty, "neither shell carried an owner");

            var b3 = Enemy.Of(BodyKind.Brunch, 400, 300);
            var g3 = Enemy.Of(BodyKind.Shooter, 450, 300);
            var run3 = Room(b3, g3);
            b3.shieldTarget = g3;
            run3.projectiles.Add(Shell(398, 300, 2, 0, dmg: 10, friendly: false, owner: g3));
            run3.projectiles.Add(Shell(398, 300, 2, 0, dmg: 10, friendly: false));
            TickOrder.TickProjectiles(run3);
            Assert.That(run3.projectiles, Has.Count.EqualTo(1));
            Assert.That(run3.projectiles[0].owner, Is.SameAs(g3));
        }

        [Test]
        public void AnEnemyShellHitsTheLaggedHitboxOffsetDown()
        {
            var run = Room();
            run.player.x = 400; run.player.y = 300; run.player.lagX = 400; run.player.lagY = 300;
            run.projectiles.Add(Shell(402, 312, -2, 0, dmg: 2, friendly: false));
            TickOrder.TickProjectiles(run);
            Assert.That(run.player.hp, Is.EqualTo(6));
            Assert.That(run.projectiles, Is.Empty);
            Assert.That(run.player.kvx, Is.EqualTo(-0.45).Within(1e-9));
            Assert.That(run.player.kvy, Is.EqualTo(0).Within(1e-9));
            Assert.That(run.player.iframes, Is.EqualTo(210));
            Assert.That(run.dmgTaken, Is.EqualTo(2));

            var miss = Room();
            miss.player.x = 400; miss.player.y = 300; miss.player.lagX = 400; miss.player.lagY = 300;
            miss.projectiles.Add(Shell(430, 312, -2, 0, dmg: 2, friendly: false));
            TickOrder.TickProjectiles(miss);
            Assert.That(miss.player.hp, Is.EqualTo(8), "a shell 30px away misses");
            Assert.That(miss.projectiles, Has.Count.EqualTo(1));
        }

        [Test]
        public void ALungerBlocksAnEnemyShell()
        {
            var lg = Enemy.Of(BodyKind.Lunger, 400, 300);
            var run = Room(lg);
            double h = lg.hp;
            run.projectiles.Add(Shell(398, 300, 2, 0, dmg: 10, friendly: false));
            TickOrder.TickProjectiles(run);
            Assert.That(h - lg.hp, Is.EqualTo(1).Within(1e-9), "a blocked shell costs the lunger 1, whatever its damage");
            Assert.That(run.projectiles, Is.Empty);
        }

        // ---- beyond the banked probe

        [Test]
        public void ALandingBoltDoesNotFreezeTheOlderShells()
        {
            var t = Enemy.Of(BodyKind.Lunger, 600, 300);
            t.hp = t.maxHp = 1e9;
            var run = Room(t);
            run.projectiles.Add(Shell(200, 150, 1.5, 0, friendly: false));   // oldest: a hostile shell far away
            run.projectiles.Add(Shell(200, 450, 0, -1.25));                    // a friendly that misses
            run.projectiles.Add(Shell(600, 300, 0.1, 0));                      // newest: lands
            var shell = run.projectiles[0];
            var miss = run.projectiles[1];
            TickOrder.TickProjectiles(run);
            Assert.That(shell.x, Is.EqualTo(201.5).Within(1e-9));
            Assert.That(miss.y, Is.EqualTo(448.75).Within(1e-9));
        }

        [Test]
        public void AKillRollsTheDropFromTheRunStreamExactlyOnce()
        {
            var t = Enemy.Of(BodyKind.Lunger, 400, 300);
            var run = Room(t);
            int calls = run.rng.RunCalls;
            var twin = new Rng(4242);
            for (int i = 0; i < calls; i++) twin.Run();
            double roll = twin.Run();
            run.projectiles.Add(Shell(398, 300, 2, 0, dmg: 1000));
            TickOrder.TickProjectiles(run);
            Assert.That(run.enemies, Is.Empty);
            Assert.That(run.kills, Is.EqualTo(1));
            Assert.That(run.rng.RunCalls - calls, Is.EqualTo(1), "a kill is one draw, drop or not");
            string want = roll < 0.18 ? "heart" : roll < 0.26 ? "armor" : "";
            Assert.That(run.pickups.Count, Is.EqualTo(want == "" ? 0 : 1));
            if (want != "") Assert.That(run.pickups[0].kind, Is.EqualTo(want));
        }

        [Test]
        public void KillingTheWardenTakesItsWallWithIt()
        {
            var boss = Enemy.Of(BodyKind.Boss, 400, 300);
            var run = Room(boss);
            for (int i = 0; i < 5; i++) { var w = Enemy.Of(BodyKind.Brunch, 300 + i * 10, 200); w.packId = Balance.BossWallId; run.enemies.Add(w); }
            Kills.KillEnemy(run, 0);
            Assert.That(run.enemies, Is.Empty);
            Assert.That(run.kills, Is.EqualTo(1), "the statues are not kills");
        }

        [Test]
        public void TheHookRefusesUntilItIsPorted()
        {
            var run = Room();
            run.projectiles.Add(new Projectile { alt = true, mode = "hook", x = 400, y = 300 });
            Assert.Throws<System.NotSupportedException>(() => TickOrder.TickProjectiles(run));
        }
    }
}
