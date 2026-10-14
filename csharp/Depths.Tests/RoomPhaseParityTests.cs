using System.Collections.Generic;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>Parity for TickOrder.TickRoom - the first of the four tick phases ported with real behaviour, and the first... [h:RoomPhaseParityTests-1]</summary>
    [TestFixture]
    public sealed class RoomPhaseParityTests
    {
        /// <summary>A cleared, empty standard room with the player at the room centre and a known state, so a test only has to... [h:RoomPhaseParityTests-2]</summary>
        private static RunState Room(uint seed = 4242)
        {
            // One Rng for the run and another for the dungeon, because `Dungeon.FromSeed` consumes its stream: sharing one... [h:RoomPhaseParityTests-3]
            var run = new RunState(new Rng(seed)) { dungeon = Dungeon.FromSeed(new Rng(seed)) };
            run.state = "playing";
            run.readyT = 0;
            run.fadeT = 0;
            run.floor = 1;
            run.enemies.Clear();
            run.pickups.Clear();
            run.player.x = 400; run.player.y = 355;
            run.player.hp = 8; run.player.maxHp = 8;
            run.player.armor = 0;
            run.player.hasSilver = false;
            run.player.hasGold = false;
            run.player.weaponIdx = 0;
            run.player.cooldown = 0;
            run.player.altMode = "blast";
            run.player.r = 13;
            run.player.blinkRegen = 0;
            run.hook = false;
            return run;
        }

        private static int Exits(RunState run) =>
            run.pickups.FindAll(p => p.kind == "exit").Count;

        // ------------------------------------------------------------------ the blink refund

        /// <summary>A room that has just been cleared refunds half a blink charge - and only the first time. [h:RoomPhaseParityTests-4]</summary>
        [Test]
        public void AClearedRoomRefundsHalfABlinkOnceAndOnlyOnce()
        {
            var run = Room();
            Assert.That(run.player.blinkRegen, Is.EqualTo(0));

            TickOrder.TickRoom(run);
            Assert.That(run.player.blinkRegen, Is.EqualTo(367.5).Within(1e-9));
            Assert.That(run.CurrentRoom!.Cleared, Is.True);

            for (int i = 0; i < 3; i++) TickOrder.TickRoom(run);
            Assert.That(run.player.blinkRegen, Is.EqualTo(367.5).Within(1e-9),
                "a cleared room pays the refund once, not once per tick");
        }

        /// <summary>A refund never REDUCES an existing charge - it is a maximum, not a grant.</summary>
        [Test]
        public void TheRefundRaisesTheChargeAndNeverLowersIt()
        {
            var run = Room();
            run.player.blinkRegen = 600;
            TickOrder.TickRoom(run);
            Assert.That(run.player.blinkRegen, Is.EqualTo(600).Within(1e-9));
        }

        // ------------------------------------------------------------------ pickups

        /// <summary>A heart at full health is left lying rather than eaten. [h:RoomPhaseParityTests-5]</summary>
        [TestCase(8, 8, 1)]
        [TestCase(5, 7, 0)]
        [TestCase(7, 8, 0)]
        [TestCase(6, 8, 0)]
        public void AHeartIsLeftAtFullHealthAndOtherwiseHealsTwoAndIsTaken(
            double hp, double expectedHp, int expectedPickups)
        {
            var run = Room();
            run.player.hp = hp;
            run.pickups.Add(new Pickup { x = 400, y = 355, r = 16, kind = "heart" });

            TickOrder.TickRoom(run);

            Assert.That(run.player.hp, Is.EqualTo(expectedHp).Within(1e-9));
            Assert.That(run.pickups.Count, Is.EqualTo(expectedPickups));
        }

        /// <summary>Armour behaves the same way, and caps at four.</summary>
        [TestCase(4, 4, 1)]
        [TestCase(1, 3, 0)]
        [TestCase(3, 4, 0)]
        public void ArmourIsLeftAtTheCapAndOtherwiseAddsTwoUpToFour(
            int armor, int expectedArmor, int expectedPickups)
        {
            var run = Room();
            run.player.armor = armor;
            run.pickups.Add(new Pickup { x = 400, y = 355, r = 16, kind = "armor" });

            TickOrder.TickRoom(run);

            Assert.That(run.player.armor, Is.EqualTo(expectedArmor));
            Assert.That(run.pickups.Count, Is.EqualTo(expectedPickups));
        }

        /// <summary>Both keys, at their own radii, are collected. Measured: both flags set, list empty.</summary>
        [Test]
        public void BothKeysAreTaken()
        {
            var run = Room();
            run.pickups.Add(new Pickup { x = 400, y = 355, r = 14, kind = "key" });
            run.pickups.Add(new Pickup { x = 400, y = 355, r = 16, kind = "goldkey" });

            TickOrder.TickRoom(run);

            Assert.That(run.player.hasSilver, Is.True);
            Assert.That(run.player.hasGold, Is.True);
            Assert.That(run.pickups.Count, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ the weapon swap

        /// <summary>The swap parks the OLD weapon where the player is standing, and marks it held. [h:RoomPhaseParityTests-6]</summary>
        [Test]
        public void AWeaponSwapParksTheOldOneHeldRatherThanDeletingIt()
        {
            var run = Room();
            run.pickups.Add(Pickup.Weapon(400, 355, 2));

            TickOrder.TickRoom(run);

            Assert.That(run.player.weaponIdx, Is.EqualTo(2));
            Assert.That(run.pickups.Count, Is.EqualTo(1), "no weapon is ever lost");
            Assert.That(run.pickups[0].kind, Is.EqualTo("weapon"));
            Assert.That(run.pickups[0].w, Is.EqualTo(0), "the parked pickup carries the OLD index");
            Assert.That(run.pickups[0].hold, Is.True);
        }

        /// <summary>The cooldown after a swap is a MINIMUM against the new weapon's, so swapping into a gun that is already... [h:RoomPhaseParityTests-7]</summary>
        [Test]
        public void ASwapNeverShortensAnAlreadyShorterCooldown()
        {
            var run = Room();
            run.player.cooldown = 5;          // already below the Arcane Beam's 18.2
            run.pickups.Add(Pickup.Weapon(400, 355, 2));

            TickOrder.TickRoom(run);

            Assert.That(run.player.cooldown, Is.EqualTo(5).Within(1e-9));
        }

        /// <summary>Stepping off clears the hold, so stepping back on re-collects and the swap settles.</summary>
        [Test]
        public void SteppingOffClearsTheHoldSoTheSwapSettles()
        {
            var run = Room();
            run.pickups.Add(Pickup.Weapon(400, 355, 2));
            TickOrder.TickRoom(run);
            Assert.That(run.player.weaponIdx, Is.EqualTo(2));

            run.player.x = 600;
            TickOrder.TickRoom(run);
            Assert.That(run.pickups[0].hold, Is.False, "stepping off releases the hold");

            run.player.x = 400;
            TickOrder.TickRoom(run);
            Assert.That(run.player.weaponIdx, Is.EqualTo(0), "the old gun comes back");
            Assert.That(run.pickups.Count, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ the exit

        /// <summary>The exit is collected at the bare PLAYER_HIT_R of 10, while every other pickup is collected at pk.r +... [h:RoomPhaseParityTests-8]</summary>
        [TestCase(12.0, 0)]
        [TestCase(10.0, 0)]   // exactly at the radius: `<`, so not collected
        [TestCase(9.0, 1)]
        [TestCase(0.0, 1)]
        public void TheExitIsCollectedAtTheBareHitRadiusAndDescends(double offset, int expectedFloors)
        {
            var run = Room();
            run.pickups.Add(Pickup.Of("exit", 400 + offset, 355, 30));

            bool descended = TickOrder.TickRoom(run);

            Assert.That(run.floor, Is.EqualTo(1 + expectedFloors));
            Assert.That(descended, Is.EqualTo(expectedFloors == 1));
            // descend() returns out of the loop and does not remove the portal: it stays in the OLD room.
            // With rooms holding their own contents, the new floor's start room begins empty.
            Assert.That(run.pickups.Count, Is.EqualTo(expectedFloors == 1 ? 0 : 1),
                "after a descent the current room is the new floor's start room, which holds nothing");
        }

        /// <summary>A normal pickup 12px away IS collected - which is what makes the exit's 10px a real distinction rather than... [h:RoomPhaseParityTests-9]</summary>
        [Test]
        public void ANormalPickupAtTwelvePixelsIsCollectedWhereTheExitIsNot()
        {
            var run = Room();
            run.player.hp = 5;
            run.pickups.Add(new Pickup { x = 412, y = 355, r = 16, kind = "heart" });

            TickOrder.TickRoom(run);

            Assert.That(run.player.hp, Is.EqualTo(7).Within(1e-9));
            Assert.That(run.pickups.Count, Is.EqualTo(0), "12 < 16 + 13");
        }

        // ------------------------------------------------------------------ the rewards

        /// <summary>The silver and gold rewards drop once each, at the room centre, with their own radii (14 and 16). [h:RoomPhaseParityTests-10]</summary>
        [Test]
        public void RewardsDropOnceEachAtTheCentreWithTheirOwnRadii()
        {
            var run = Room();
            run.player.x = 120; run.player.y = 200;
            run.CurrentRoom!.KeyReward = true;
            run.CurrentRoom.GoldReward = true;

            TickOrder.TickRoom(run);

            Assert.That(run.pickups.Count, Is.EqualTo(2));
            var key = run.pickups.Find(p => p.kind == "key");
            var gold = run.pickups.Find(p => p.kind == "goldkey");
            Assert.That(key, Is.Not.Null);
            Assert.That(gold, Is.Not.Null);
            Assert.That(key!.r, Is.EqualTo(14));
            Assert.That(gold!.r, Is.EqualTo(16));
            Assert.That(key.x, Is.EqualTo(Balance.MidX));
            Assert.That(key.y, Is.EqualTo(Balance.MidY));

            TickOrder.TickRoom(run);
            Assert.That(run.pickups.Count, Is.EqualTo(2), "once each, not once per tick");
            Assert.That(run.CurrentRoom!.KeySpawned, Is.True);
            Assert.That(run.CurrentRoom.GoldSpawned, Is.True);
        }

        /// <summary>No reward drops while the room still has bodies in it.</summary>
        [Test]
        public void NoRewardDropsWhileTheRoomIsStillFought()
        {
            var run = Room();
            run.player.x = 120; run.player.y = 200;
            run.CurrentRoom!.KeyReward = true;
            run.enemies.Add(new Enemy { kind = BodyKind.Lunger });

            TickOrder.TickRoom(run);

            Assert.That(run.pickups.Count, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ the boss exit

        /// <summary>A cleared boss room opens ONE exit, once - not a new portal on every tick the player stands in an empty boss... [h:RoomPhaseParityTests-11]</summary>
        [Test]
        public void AClearedBossRoomOpensOneExitOnce()
        {
            var run = Room();
            run.player.x = 120; run.player.y = 200;
            run.CurrentRoom!.Type = RoomKind.Boss;

            run.enemies.Add(new Enemy { kind = BodyKind.Boss });
            TickOrder.TickRoom(run);
            Assert.That(Exits(run), Is.EqualTo(0), "nothing opens while the boss lives");

            run.enemies.Clear();
            TickOrder.TickRoom(run);
            Assert.That(Exits(run), Is.EqualTo(1));

            TickOrder.TickRoom(run);
            Assert.That(Exits(run), Is.EqualTo(1), "one portal, not one per tick");
        }

        /// <summary>A cleared NORMAL room opens no exit at all.</summary>
        [Test]
        public void AClearedNormalRoomOpensNoExit()
        {
            var run = Room();
            run.player.x = 120; run.player.y = 200;
            TickOrder.TickRoom(run);
            TickOrder.TickRoom(run);
            Assert.That(Exits(run), Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ death

        /// <summary>Death wins ties. A pickup arriving on the frame the player dies ends the run as a death, and the boss-exit... [h:RoomPhaseParityTests-12]</summary>
        [Test]
        public void ADeadPlayerEndsTheRunAndDoesNotOpenAnExit()
        {
            var run = Room();
            run.player.x = 120; run.player.y = 200;
            run.CurrentRoom!.Type = RoomKind.Boss;
            run.player.hp = 0;

            TickOrder.TickRoom(run);

            Assert.That(run.state, Is.EqualTo("gameover"));
            Assert.That(Exits(run), Is.EqualTo(0));
        }

        /// <summary>A dead player collects nothing, not even a key standing under them.</summary>
        [Test]
        public void ADeadPlayerCollectsNothing()
        {
            var run = Room();
            run.player.hp = 0;
            run.pickups.Add(new Pickup { x = 400, y = 355, r = 14, kind = "key" });

            TickOrder.TickRoom(run);

            Assert.That(run.player.hasSilver, Is.False);
            Assert.That(run.pickups.Count, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ the alt weapons

        /// <summary>Picking up a hook leaves a held blast where the hook stood, and cannot be re-collected on the same tick. [h:RoomPhaseParityTests-13]</summary>
        [Test]
        public void AHookLeavesABlastBehindItAndIsHeld()
        {
            var run = Room();
            run.pickups.Add(Pickup.Of("hook", 400, 355, 16));

            TickOrder.TickRoom(run);

            Assert.That(run.player.altMode, Is.EqualTo("hook"));
            Assert.That(run.hook, Is.True);
            Assert.That(run.pickups.Count, Is.EqualTo(1));
            Assert.That(run.pickups[0].kind, Is.EqualTo("blast"));
            Assert.That(run.pickups[0].hold, Is.True);
        }

        /// <summary>A second hook does not leave a second blast.</summary>
        [Test]
        public void ASecondHookDoesNotLeaveASecondBlast()
        {
            var run = Room();
            run.player.altMode = "hook";
            run.pickups.Add(Pickup.Of("hook", 400, 355, 16));

            TickOrder.TickRoom(run);

            Assert.That(run.hook, Is.True);
            Assert.That(run.pickups.Count, Is.EqualTo(0), "already on the hook, so nothing is left behind");
        }

        /// <summary>A blast swaps the alt mode back and clears the hook flag.</summary>
        [Test]
        public void ABlastSwapsBackAndClearsTheHook()
        {
            var run = Room();
            run.player.altMode = "hook";
            run.hook = true;
            run.pickups.Add(Pickup.Of("blast", 400, 355, 16));

            TickOrder.TickRoom(run);

            Assert.That(run.player.altMode, Is.EqualTo("blast"));
            Assert.That(run.hook, Is.False);
            Assert.That(run.pickups.Count, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ the known gap

        /// <summary>The item branch is NOT ported, and it says so by throwing. [h:RoomPhaseParityTests-14]</summary>
        [Test]
        public void AnItemPickupGivesTheItemAndSwapsTheActive()
        {
            // the spec (src/60-tick.js tickRoom + 25-items.js give): a passive raises its stat and is gone from the floor... [h:RoomPhaseParityTests-15]
            var run = Room();
            run.pickups.Add(new Pickup { x = 400, y = 355, r = 16, kind = "item", id = "weighted_rod" });
            TickOrder.TickRoom(run);
            Assert.That(run.stats.Value("strength"), Is.EqualTo(5), "Weighted Rod is +2 on the base 3");
            Assert.That(run.pickups, Is.Empty);

            Items.Give(run, "tin_cup");
            Items.ActiveItem(run)!.Charges = 1;   // a half-spent cup
            run.pickups.Add(new Pickup { x = 400, y = 355, r = 16, kind = "item", id = "hunters_mark" });
            TickOrder.TickRoom(run);
            Assert.That(Items.ActiveItem(run)!.Id, Is.EqualTo("hunters_mark"));
            Assert.That(run.pickups, Has.Count.EqualTo(1));
            var cup = run.pickups[0];
            Assert.That(cup.id, Is.EqualTo("tin_cup"));
            Assert.That(cup.charges, Is.EqualTo(1), "the dropped cup keeps the charge it had");
            Assert.That(cup.hold, Is.True, "a just-dropped item cannot be taken back on the same spot");
            Assert.That(run.stats.Value("luck"), Is.EqualTo(1), "Hunter's Mark carries +1 Luck");
        }

        [Test]
        public void UsingTheActiveSpendsAChargeOnlyWhenItDoesSomething()
        {
            var run = Room();
            Items.Give(run, "tin_cup");
            run.player.hp = 8;
            Assert.That(Items.UseActive(run), Is.False, "a full-health drink does nothing and costs nothing");
            Assert.That(Items.ActiveItem(run)!.Charges, Is.EqualTo(3));
            run.player.hp = 3;
            Assert.That(Items.UseActive(run), Is.True);
            Assert.That(run.player.hp, Is.EqualTo(5), "Tin Cup heals 2 (one heart)");
            Assert.That(Items.ActiveItem(run)!.Charges, Is.EqualTo(2));
        }

        /// <summary>An unknown pickup kind is left on the floor rather than silently eaten. [h:RoomPhaseParityTests-16]</summary>
        [Test]
        public void AnUnknownPickupKindIsLeftWhereItIs()
        {
            var run = Room();
            run.pickups.Add(Pickup.Of("somethingNew", 400, 355, 16));

            TickOrder.TickRoom(run);

            Assert.That(run.pickups.Count, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ a constant that was wrong

        /// <summary>The swerve meter's per-tick decay, pinned because it WAS wrong and nothing noticed. [h:RoomPhaseParityTests-17]</summary>
        [Test]
        public void TheSwerveDecayIsTheGamesRateAndNotAFasterOne()
        {
            // Read out of the running game.
            Assert.That(Balance.SwerveDecay, Is.EqualTo(0.0035).Within(1e-12));

            // And the property the original's own test states: a reversal is still more than half
            // remembered after a full second. At 0.011 this fails.
            Assert.That(Balance.SwerveDecay * 210, Is.LessThan(0.75),
                "a reversal must still be mostly remembered after a second, or the only safe play "
                + "is to never move at all");
            Assert.That(Balance.SwerveDecay * 210, Is.GreaterThan(0.5));
        }
    }
}
