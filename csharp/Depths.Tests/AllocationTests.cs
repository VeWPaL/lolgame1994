using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>CONVENTIONS: no per-tick allocations in C# hot paths (Unity GC hitches). [h:AllocationTests-1]</summary>
    [TestFixture, Category("csharp-only")]
    public sealed class AllocationTests
    {
        static RunState Room(out Enemy shooter, out Enemy gunner, out List<Enemy> pack)
        {
            var run = new RunState(new Rng(4242)) { dungeon = Dungeon.FromSeed(new Rng(4242)) };
            run.state = "playing";
            run.enemies.Clear();
            shooter = new Enemy { kind = BodyKind.Shooter, x = 200, y = Balance.MidY, hp = 20, maxHp = 20 };
            gunner = new Enemy { kind = BodyKind.Gunner, x = 600, y = Balance.MidY, hp = 20, maxHp = 20 };
            run.enemies.Add(shooter); run.enemies.Add(gunner);
            pack = new List<Enemy>();
            for (int p = 1; p <= 2; p++)
                for (int i = 0; i < 4; i++)
                {
                    var e = new Enemy { kind = BodyKind.Brunch, x = 150 + p * 200 + i * 18, y = 300, hp = 10, maxHp = 10, packId = p, packSlot = i };
                    run.enemies.Add(e); pack.Add(e);
                }
            return run;
        }

        [Test]
        public void AssemblingPacksAllocatesNothingOnceWarm()
        {
            var run = Room(out var shooter, out _, out _);
            for (int t = 0; t < 5; t++) { run.frameCount = t; TickOrder.AssemblePacks(run); }
            Assert.That(shooter.shieldGuardFor, Is.Not.Null, "the fixture never guards anything, so it measures nothing");

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 5; t < 405; t++) { run.frameCount = t; TickOrder.AssemblePacks(run); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.EqualTo(0), "400 ticks of pack assembly allocated " + bytes + " bytes");
        }

        [Test, TunedOn(true)]
        public void AFightTickWithNoNewShellsAllocatesNothing()
        {
            // the whole sandbox tick over a melee fight with the playtest tuning on (dodge, rush, wall): only a new
            // shell, pickup or body is a legitimate allocation, and none happens here
            var run = new RunState(new Rng(77)) { dungeon = Dungeon.FromSeed(new Rng(77)) };
            run.state = "playing"; run.readyT = 0; run.fadeT = 0;
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
            var p = run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.RoomBottom - 60;
            p.hp = p.maxHp = 1000;   // the fixture holds: nobody dies, nothing drops
            for (int i = 0; i < 4; i++) { var l = Spawn.Body(run, BodyKind.Lunger, 150 + i * 150, 220); l.hp = l.maxHp = 1e9; l.noticeTimer = 0; run.enemies.Add(l); }
            for (int i = 0; i < 6; i++) { var b = Spawn.Body(run, BodyKind.Brunch, 300 + (i % 3) * 18, 300 + (i / 3) * 18); b.hp = b.maxHp = 1e9; b.packId = 5; b.packSlot = i; b.noticeTimer = 0; run.enemies.Add(b); }
            void Tick() { TickOrder.TickPlayer(run, Input.None); TickOrder.TickProjectiles(run); TickOrder.TickBodies(run); }
            for (int t = 0; t < 120; t++) Tick();   // warm: the pack assembled, every state seen once
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 600; t++) Tick();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(run.projectiles, Is.Empty, "the fixture fired, so it would measure shells");
            // no PER-TICK allocation: the smallest object is 24 bytes, so one a tick would be 14400 here. [h:AllocationTests-2]
            Assert.That(bytes, Is.LessThan(24 * 600 / 10), "600 fight ticks allocated " + bytes + " bytes: something allocates every few ticks");
        }

        [Test]
        public void AGuardListIsReusedAndAnUnguardedBodyStillReadsNull()
        {
            var run = Room(out var shooter, out var gunner, out var pack);
            TickOrder.AssemblePacks(run);
            var first = shooter.shieldGuardFor;
            Assert.That(first, Is.Not.Null);
            int guards = first!.Count;
            TickOrder.AssemblePacks(run);
            Assert.That(shooter.shieldGuardFor, Is.SameAs(first), "a new list every tick");
            Assert.That(shooter.shieldGuardFor!.Count, Is.EqualTo(guards), "the reused list kept last tick's guards");

            // every pack re-targets the gunner: the shooter is unguarded, which reads as null as in the game
            foreach (var e in pack) e.shieldTarget = gunner;
            TickOrder.AssemblePacks(run);
            Assert.That(shooter.shieldGuardFor, Is.Null);
            Assert.That(gunner.shieldGuardFor!.Count, Is.EqualTo(pack.Count));
        }
    }
}
