using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// CONVENTIONS: no per-tick allocations in C# hot paths (Unity GC hitches). Measured with the
    /// runtime's per-thread allocation counter, after a warm-up that may size pools and buffers.
    /// </summary>
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
