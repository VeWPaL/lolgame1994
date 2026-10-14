using System;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>Drawing between ticks (Interp): the blend, its two refusals, and that the game never feels it.</summary>
    [TestFixture, Category("csharp-only")]
    public sealed class InterpTests
    {
        static RunState Room()
        {
            var run = new RunState(new Rng(9)) { dungeon = Dungeon.FromSeed(new Rng(9)) };
            run.state = "playing"; run.readyT = 0; run.fadeT = 0;
            run.enemies.Clear(); run.projectiles.Clear();
            return run;
        }

        [Test]
        public void AFrameDrawsBetweenTheSnapAndTheTickByTheClocksFraction()
        {
            var run = Room();
            var e = new Enemy { kind = BodyKind.Lunger, x = 100, y = 200 };
            run.enemies.Add(e);
            Interp.Snap(run);
            e.x = 110; e.y = 196;   // what a tick did
            Interp.At(run, e, 0, out var x0, out var y0);
            Interp.At(run, e, 0.25, out var xq, out var yq);
            Interp.At(run, e, 1, out var x1, out var y1);
            Assert.That((x0, y0), Is.EqualTo((100.0, 200.0)), "alpha 0 is the last tick");
            Assert.That(xq, Is.EqualTo(102.5).Within(1e-9)); Assert.That(yq, Is.EqualTo(199.0).Within(1e-9));
            Assert.That((x1, y1), Is.EqualTo((110.0, 196.0)), "alpha 1 is this tick");
        }

        [Test]
        public void SomethingBornSinceTheSnapIsDrawnWhereItIs()
        {
            var run = Room();
            Interp.Snap(run);
            var q = new Projectile { x = 300, y = 300 };   // fired by the tick: px/py are 0, which must never be drawn
            run.projectiles.Add(q);
            Interp.At(run, q, 0, out var x, out var y);
            Assert.That((x, y), Is.EqualTo((300.0, 300.0)));
        }

        [Test]
        public void BeforeAnySnapEverythingIsDrawnWhereItIs()
        {
            var run = Room();   // a fresh run: every stamp is 0, every px/py 0
            Interp.At(run, run.player, 0, out var x, out var y);
            Assert.That((x, y), Is.EqualTo((run.player.x, run.player.y)));
        }

        [Test]
        public void AJumpIsNeverSweptAcrossTheRoom()
        {
            var run = Room();
            var p = run.player;
            p.x = 100; p.y = 300;
            Interp.Snap(run);
            p.x = 100 + Interp.MaxStep + 1;   // a door or a blink: drawn where it landed
            Interp.At(run, p, 0.1, out var x, out _);
            Assert.That(x, Is.EqualTo(p.x));
            p.x = 100 + Interp.MaxStep - 1;   // a fast step still blends
            Interp.At(run, p, 0.5, out x, out _);
            Assert.That(x, Is.EqualTo(100 + (Interp.MaxStep - 1) / 2).Within(1e-9));
        }

        [Test]
        public void NothingAtTickSpeedOutrunsTheJumpCutoff()
        {
            // the cutoff must sit above every real per-tick step at the game's 60 Hz (this suite ticks faster), with room... [h:InterpTests-1]
            double lunge = Balance.LungeSpeed * Math.Max(1 / Movement.DashArea(Balance.LungeRampIn, Balance.LungeRampOut), Balance.DodgePeak);
            double deep = Balance.DepthRate(40);
            double perSecond = lunge * deep * Balance.TickHz;
            foreach (var w in Weapons.All) perSecond = Math.Max(perSecond, 2 * w.SpeedPs);
            perSecond = Math.Max(perSecond, Math.Max(Middle.BurrowRun * deep, Balance.BrunchGuardSpeed) * Balance.TickHz);
            double perTick = perSecond / Balance.GameHz;
            Assert.That(perTick, Is.LessThan(Interp.MaxStep), "fastest per-tick step " + perTick.ToString("0.0") + " px at " + Balance.GameHz + " Hz");
        }

        [Test]
        public void SnappingNeverChangesTheGame()
        {
            // the same run played twice, one with a snap before every tick: every number the game keeps must match
            string Play(bool snap)
            {
                var run = new RunState(new Rng(31337));
                run.Start(31337);
                for (int t = 0; t < 3000; t++)
                {
                    if (snap) Interp.Snap(run);
                    int k = t / 90 % 4;
                    var input = new Input(k == 0 ? 1 : k == 2 ? -1 : 0, k == 1 ? 1 : k == 3 ? -1 : 0, fire: true, aimX: Balance.MidX, aimY: Balance.MidY);
                    TickOrder.Update(run, input);
                }
                var p = run.player;
                return string.Join(",", run.state, run.ticks, run.floor, run.kills, p.x, p.y, p.hp, run.enemies.Count, run.projectiles.Count, run.rng.Run());
            }
            Assert.That(Play(true), Is.EqualTo(Play(false)));
        }

        [Test]
        public void ASnapAllocatesNothing()
        {
            var run = Room();
            for (int i = 0; i < 12; i++) run.enemies.Add(new Enemy { x = i, y = i });
            for (int i = 0; i < 40; i++) run.projectiles.Add(new Projectile { x = i, y = i });
            Interp.Snap(run);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 1000; t++) Interp.Snap(run);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
