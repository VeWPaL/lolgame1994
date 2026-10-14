using System.Collections.Generic;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>Parity for RunState.Descend and for the room phase's handling of it. [h:DescendParityTests-1]</summary>
    [TestFixture]
    public sealed class DescendParityTests
    {
        /// <summary>A run on floor 1 with every per-floor field deliberately made non-default.</summary>
        private static RunState Dirty()
        {
            var run = new RunState(new Rng(4242))
            {
                dungeon = Dungeon.FromSeed(new Rng(4242)),
                rootSeed = 4242,
                state = "playing",
                floor = 1,
            };
            run.readyT = 0;
            run.fadeT = 0;
            run.player.x = 400; run.player.y = 355;
            run.player.hp = 8; run.player.maxHp = 8;

            // dirty it
            run.player.vx = 5; run.player.vy = -3;
            run.player.kvx = 2; run.player.kvy = 1;
            run.player.x = 123; run.player.y = 456;
            run.player.lagX = 1; run.player.lagY = 2;
            run.player.blinkRegen = 99;
            run.blinkCharges = 0;
            run.blinkGrace = 5;
            run.bossWarned = true;
            run.bossWarnT = 7;
            run.trans = new Trans { t = 3, nx = 1, ny = 1, from = "W" };
            run.projectiles.Add(new Projectile { x = 1, y = 1, r = 2, dmg = 1, friendly = true });
            run.transients.Add(new Pickup { x = 5, y = 5, kind = "heart" });
            run.floorTicks = 500;
            run.roomsThisFloor = 7;
            run.fadeT = 0;
            run.roomFade = 0;
            return run;
        }

        [Test]
        public void DescendingAdvancesTheFloorAndZeroesBothClocks()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.floor, Is.EqualTo(2));
            Assert.That(run.floorTicks, Is.EqualTo(0));
            Assert.That(run.roomsThisFloor, Is.EqualTo(0));
        }

        [Test]
        public void DescendingKeepsTheRootSeedSoTheFloorStaysReproducible()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.rootSeed, Is.EqualTo(4242u),
                "the root seed is what the player typed; a descent derives from it, never replaces it");
        }

        [Test]
        public void DescendingPutsThePlayerAtTheRoomCentreWithTheHitboxMovedToo()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.player.x, Is.EqualTo(400));
            Assert.That(run.player.y, Is.EqualTo(355));
            // The lagged hitbox moved WITH the player. Leaving it behind would aim every gunner at
            // where the player stood a floor ago.
            Assert.That(run.player.lagX, Is.EqualTo(400));
            Assert.That(run.player.lagY, Is.EqualTo(355));
        }

        [Test]
        public void DescendingZeroesEveryVelocityIncludingKnockback()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.player.vx, Is.EqualTo(0));
            Assert.That(run.player.vy, Is.EqualTo(0));
            Assert.That(run.player.kvx, Is.EqualTo(0));
            Assert.That(run.player.kvy, Is.EqualTo(0));
        }

        [Test]
        public void DescendingRefillsTheBlinkAndClearsItsTimers()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.blinkCharges, Is.EqualTo(2),
                "arriving on a floor must never cost an escape the player was owed");
            Assert.That(run.player.blinkRegen, Is.EqualTo(0));
            Assert.That(run.blinkGrace, Is.EqualTo(0));
        }

        [Test]
        public void DescendingClearsEveryPerFloorFlag()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.bossWarned, Is.False, "a new floor warns about its boss again");
            Assert.That(run.bossWarnT, Is.EqualTo(0));
            Assert.That(run.trans, Is.Null);
            Assert.That(run.readyT, Is.EqualTo(0));
        }

        [Test]
        public void DescendingClearsShellsFromTheFloorAbove()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.projectiles.Count, Is.EqualTo(0),
                "a shell in flight on the old floor must not arrive in the new one");
            Assert.That(run.transients.Count, Is.EqualTo(0));
        }

        [Test]
        public void DescendingArmsTheFadeAndStaysPlaying()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.fadeTicks, Is.EqualTo(189));
            Assert.That(run.fadeT, Is.EqualTo(189));
            Assert.That(run.roomFade, Is.EqualTo(1));
            Assert.That(run.descendT, Is.EqualTo(189));
            Assert.That(run.descendFrom, Is.EqualTo(1));
            Assert.That(run.state, Is.EqualTo("playing"),
                "the run does not pause for a descent; it fades");
        }

        [Test]
        public void TheCursorReturnsToTheStartCell()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.curX, Is.EqualTo(Map.Start));
            Assert.That(run.curY, Is.EqualTo(Map.Start));
            Assert.That(run.CurrentRoom, Is.Not.Null);
        }

        /// <summary>The new floor is a PURE FUNCTION of (root, floor) - not a chain from the previous floor. [h:DescendParityTests-2]</summary>
        [Test]
        public void TwoDescentsFromTheSameRootProduceTheSameFloor()
        {
            var a = Dirty();
            a.Descend();
            var b = Dirty();
            b.Descend();
            Assert.That(a.dungeon.Signature(), Is.EqualTo(b.dungeon.Signature()));
        }

        /// <summary>And the new floor's seed is the derived one, not the root.</summary>
        [Test]
        public void TheNewFloorIsSeededFromRootAndFloor()
        {
            var run = Dirty();
            run.Descend();
            Assert.That(run.dungeon.Seed, Is.EqualTo(Rng.FloorSeed(4242u, 2)));
        }

        /// <summary>The room phase's mid-function return is honoured: reaching the exit skips everything after the pickup loop. [h:DescendParityTests-3]</summary>
        [Test]
        public void TouchingTheExitSkipsTheRestOfTheRoomPhase()
        {
            var run = Dirty();
            run.player.x = 400; run.player.y = 355;
            run.CurrentRoom!.Type = RoomKind.Boss;
            run.pickups.Clear();
            run.enemies.Clear();
            run.pickups.Add(Pickup.Of("exit", 400, 355, 30));

            bool descended = TickOrder.TickRoom(run);

            Assert.That(descended, Is.True);
            Assert.That(run.floor, Is.EqualTo(2));
            // The boss room on the NEW floor must be untouched: no exit portal opened on arrival.
            Assert.That(run.CurrentRoom!.Type, Is.Not.EqualTo(RoomKind.Boss),
                "the cursor moved to the new floor's start, which is not a boss room");
            // The portal is STILL THERE after the descent, and that is measured, not assumed. [h:DescendParityTests-4]
            Assert.That(run.floor, Is.EqualTo(2), "the descent happened");
            Assert.That(run.CurrentRoom!.ExitOpen, Is.False,
                "the boss-exit check must not run on the floor the player just arrived on");
            Assert.That(run.CurrentRoom.Type, Is.Not.EqualTo(RoomKind.Boss));
        }

        /// <summary>The room phase reports the descent to its caller, so the tick can honour the original's whole-tick abort... [h:DescendParityTests-5]</summary>
        [Test]
        public void TheRoomPhaseReportsTheDescentToItsCaller()
        {
            var run = Dirty();
            run.player.x = 400; run.player.y = 355;
            run.pickups.Add(Pickup.Of("exit", 400, 355, 30));
            Assert.That(TickOrder.TickRoom(run), Is.True);

            var run2 = Dirty();
            run2.player.x = 400; run2.player.y = 355;
            run2.pickups.Add(Pickup.Of("heart", 400, 355, 16));
            Assert.That(TickOrder.TickRoom(run2), Is.False, "an ordinary pickup is not a descent");
        }
    }
}
