using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// Regression (found by the C# playtest bot, 2026-10-06): touching the exit while other pickups
    /// still lay in the room threw in TickRoom, because Descend empties run.pickups mid-loop.
    /// </summary>
    [TestFixture, Category("csharp-only")]
    public sealed class ExitPickupTests
    {
        static RunState AtTheExit(params string[] leftovers)
        {
            var run = new RunState(new Rng(4242));
            run.Start(4242);
            run.readyT = 0; run.fadeT = 0;
            run.enemies.Clear(); run.pickups.Clear();
            var p = run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.MidY;
            foreach (var kind in leftovers) run.pickups.Add(Pickup.Of(kind, Balance.MidX, Balance.MidY, 10));
            run.pickups.Add(Pickup.Of("exit", Balance.MidX, Balance.MidY, 30));   // last, so the loop meets it first
            return run;
        }

        [Test]
        public void TheExitWithAHeartLeftLyingDescendsWithoutThrowing()
        {
            var run = AtTheExit("heart");   // full red HP: the heart is left on the floor
            Assert.DoesNotThrow(() => TickOrder.TickRoom(run));
            Assert.That(run.floor, Is.EqualTo(2));
            Assert.That(run.player.hp, Is.EqualTo(run.player.maxHp));
        }

        [Test]
        public void TheExitWithSeveralLeftoversDescendsAndTakesNoneOfThem()
        {
            var run = AtTheExit("armor", "halfheart", "key");
            run.player.hp = 3;
            Assert.DoesNotThrow(() => TickOrder.TickRoom(run));
            Assert.That(run.floor, Is.EqualTo(2));
            Assert.That(run.player.hp, Is.EqualTo(3), "a pickup of the old floor was taken after the descent");
            Assert.That(run.player.armor, Is.EqualTo(0));
            Assert.That(run.player.hasSilver, Is.False);
        }

        [Test]
        public void APickupTheLoopMeetsBeforeTheExitIsStillTaken()
        {
            // the loop walks backwards, so a pickup after the exit in the list is met first, as in the JS
            var run = AtTheExit();
            run.pickups.Add(Pickup.Of("heart", Balance.MidX, Balance.MidY, 10));
            run.player.hp = 3;
            TickOrder.TickRoom(run);
            Assert.That(run.floor, Is.EqualTo(2));
            Assert.That(run.player.hp, Is.EqualTo(5), "the heart met before the exit was not taken");
        }

        [Test]
        public void TheOldRoomKeepsItsLeftovers()
        {
            var run = AtTheExit("heart", "armor");
            var old = run.CurrentRoom!;
            TickOrder.TickRoom(run);
            Assert.That(old.Pickups.ConvertAll(p => p.kind), Is.EquivalentTo(new[] { "heart", "armor", "exit" }));
        }

        [Test]
        public void TheNewFloorStartsWithNoPickupsCarriedOver()
        {
            var run = AtTheExit("heart", "armor");
            TickOrder.TickRoom(run);
            Assert.That(run.pickups, Is.Empty, "the old room's pickups followed the player down");
        }
    }
}
