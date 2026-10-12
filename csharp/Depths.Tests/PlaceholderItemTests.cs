using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The two placeholder item effects (C# only, 2026-10-06, until the item overhaul): Hunter's Mark
    /// marks the room so it takes more damage, and the Brass Compass opens the floor's fake wall when
    /// carried into its room. The JS has neither, so these are assertions, not parity rows.
    /// </summary>
    [TestFixture, Category("csharp-only")]
    public sealed class PlaceholderItemTests
    {
        static RunState Run()
        {
            var run = new RunState(new Rng(4242)) { dungeon = Dungeon.FromSeed(new Rng(4242)) };
            run.state = "playing";
            return run;
        }

        [Test]
        public void HuntersMarkMarksEveryLiveBodyAndSpendsACharge()
        {
            var run = Run();
            var a = Enemy.Of(BodyKind.Lunger, 300, 300);
            var b = Enemy.Of(BodyKind.Shooter, 500, 300);
            run.enemies.Add(a); run.enemies.Add(b);
            Items.Give(run, "hunters_mark");
            Assert.That(Items.UseActive(run), Is.True);
            Assert.That(Items.ActiveItem(run)!.Charges, Is.EqualTo(1));
            foreach (var e in new[] { a, b })
            {
                Assert.That(e.markT, Is.EqualTo(Balance.MarkTicks));
                Assert.That(e.alerted, Is.True, "the mark still wakes the room");
                Assert.That(e.Vuln, Is.EqualTo(e.armour * Balance.MarkVuln));
            }
            Assert.That(Items.UseActive(run), Is.True, "re-marking a live room is not a wasted charge");
            Assert.That(Items.ActiveItem(run), Is.Null, "the second charge was the last");
        }

        [Test]
        public void HuntersMarkOnAnEmptyRoomCostsNothing()
        {
            var run = Run();
            Items.Give(run, "hunters_mark");
            Assert.That(Items.UseActive(run), Is.False);
            Assert.That(Items.ActiveItem(run)!.Charges, Is.EqualTo(2));
        }

        [Test]
        public void HuntersMarkOnAClearedRoomWithLootCostsNothing()
        {
            // the JS "reveal" of pickups draws nothing in the port, so loot alone must not spend a charge
            var run = Run();
            run.pickups.Add(Pickup.Of("heart", 400, 300, 16));
            Items.Give(run, "hunters_mark");
            Assert.That(Items.UseActive(run), Is.False);
            Assert.That(Items.ActiveItem(run)!.Charges, Is.EqualTo(2));
        }

        [Test]
        public void AMarkedBodyTakesHalfAgainTheBlast()
        {
            double Loss(bool marked)
            {
                var run = Run();
                run.CurrentRoom!.Secret = null;
                var e = Enemy.Of(BodyKind.Lunger, 400, 350);
                if (marked) e.markT = Balance.MarkTicks;
                run.enemies.Add(e);
                double before = e.hp;
                Blast.Explode(run, 400, 350);
                return before - e.hp;
            }
            double plain = Loss(false);
            Assert.That(plain, Is.GreaterThan(0));
            Assert.That(Loss(true), Is.EqualTo(plain * Balance.MarkVuln).Within(1e-9));
        }

        [Test]
        public void TheMarkWearsOff()
        {
            var run = Run();
            run.player.x = run.player.lagX = 100; run.player.y = run.player.lagY = 100;
            var e = Enemy.Of(BodyKind.Lunger, 700, 500);
            e.markT = Balance.MarkTicks;
            run.enemies.Add(e);
            for (int t = 0; t < Balance.MarkTicks; t++) TickOrder.TickBodies(run);
            Assert.That(e.markT, Is.EqualTo(0));
            Assert.That(e.Vuln, Is.EqualTo(e.armour), "an unmarked body takes exactly what it took before");
        }

        static (RunState run, Room beside) BesideTheSecret()
        {
            var run = Run();
            var beside = run.dungeon.AllRooms.Single(r => r.Secret.HasValue);
            return (run, beside);
        }

        [Test]
        public void TheCompassOpensTheFakeWallOnEntry()
        {
            var (run, beside) = BesideTheSecret();
            var d = beside.Secret!.Value;
            Items.Give(run, "brass_compass");
            Rooms.EnterRoom(run, beside.X, beside.Y, Dir.N);
            Assert.That(beside.Secret, Is.Null, "the wall is still fake");
            Assert.That(beside.Doors, Does.Contain(d));
            Assert.That(run.secret, Is.True);
        }

        [Test]
        public void TheCompassOpensTheWallWhenPickedUpBesideIt()
        {
            var (run, beside) = BesideTheSecret();
            Rooms.EnterRoom(run, beside.X, beside.Y, Dir.N);
            Assert.That(beside.Secret, Is.Not.Null, "without the compass the wall must stay hidden");
            Items.Give(run, "brass_compass");
            Assert.That(beside.Secret, Is.Null);
            Assert.That(run.secret, Is.True);
        }
    }
}
