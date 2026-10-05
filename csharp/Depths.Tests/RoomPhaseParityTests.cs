using System.Collections.Generic;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// Parity for <c>TickOrder.TickRoom</c> - the first of the four tick phases ported with real
    /// behaviour, and the first set of expectations in this project generated rather than written.
    ///
    /// <para>
    /// EVERY number here was measured out of the running game by <c>tools/room-parity.js</c>, which
    /// drives the browser's <c>tickRoom()</c> through these same fixtures. That is the point: a
    /// hand-transcribed expectation is only a second opinion about what the code should do, whereas
    /// two implementations of one function disagreeing is a real finding. Where the two disagreed
    /// during this port, three of the disagreements were the fixture's fault and are recorded below
    /// - they are kept because each mistake is repeatable.
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><b>The exit does not remove itself.</b> The original calls <c>descend()</c> and RETURNS,
    /// so the portal is still in the list on the tick after it is touched. Asserting on the list
    /// makes a distance of ZERO read as "not collected" - the exact opposite of what happened, and
    /// what this file's first version reported. The observable is the floor.</item>
    /// <item><b>The reward drops land on the player.</b> They spawn at <c>(MIDX, MIDY)</c>, and a
    /// player standing there collects them on the tick they are created. A fixture parked at the
    /// centre sees an empty list and concludes the drop is broken.</item>
    /// <item><b>The touch test is strict.</b> A pickup at exactly its own radius is NOT collected, so
    /// the exit at exactly 10px does not descend. "At the boundary" is where an off-by-one hides,
    /// so the boundary is asserted rather than assumed.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public sealed class RoomPhaseParityTests
    {
        /// <summary>
        /// A cleared, empty standard room with the player at the room centre and a known state, so a
        /// test only has to state what it is actually testing.
        /// </summary>
        private static RunState Room(uint seed = 4242)
        {
            // One Rng for the run and another for the dungeon, because `Dungeon.FromSeed` consumes
            // its stream: sharing one would generate a floor from a different position than the run
            // expects, and the mismatch would only show up as a room the tests cannot find.
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

        /// <summary>
        /// A room that has just been cleared refunds half a blink charge - and only the first time.
        /// Measured: <c>BLINK_RECHARGE</c> is 735, so the refund is 367.5. The fraction is kept
        /// because the original stores it in a float field; rounding it to 368 would make every
        /// cleared room half a tick more generous.
        /// </summary>
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

        /// <summary>
        /// A heart at full health is left lying rather than eaten. Measured: 8/8 leaves it, 5/8 heals
        /// to 7 and takes it, 7/8 heals to 8 and takes it, 6/8 heals to 8 and takes it.
        /// </summary>
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

        /// <summary>
        /// The swap parks the OLD weapon where the player is standing, and marks it held. Without the
        /// hold it would be re-collected on the same tick and the swap would oscillate forever.
        /// </summary>
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

        /// <summary>
        /// The cooldown after a swap is a MINIMUM against the new weapon's, so swapping into a gun
        /// that is already nearly ready grants no free shot.
        /// </summary>
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

        /// <summary>
        /// The exit is collected at the bare <c>PLAYER_HIT_R</c> of 10, while every other pickup is
        /// collected at <c>pk.r + player.r</c>. Walking into the way down is a deliberate act and
        /// should not happen by brushing the edge of a 30px circle.
        /// <para>
        /// Asserted on the FLOOR, because touching the exit calls <c>descend()</c> and returns: the
        /// portal is still in the list on the next tick. The test is strict, so exactly 10px does
        /// NOT descend - measured, not assumed.
        /// </para>
        /// </summary>
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
            Assert.That(run.pickups.Count, Is.EqualTo(1),
                "descend() returns out of the loop; it does not remove the portal");
        }

        /// <summary>
        /// A normal pickup 12px away IS collected - which is what makes the exit's 10px a real
        /// distinction rather than an accident of the same number appearing twice.
        /// </summary>
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

        /// <summary>
        /// The silver and gold rewards drop once each, at the room centre, with their own radii
        /// (14 and 16). The player is in a corner here: at the centre the drop is collected on the
        /// tick it is created and the list looks empty.
        /// </summary>
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
            run.enemies.Add(new Body { Kind = BodyKind.Lunger });

            TickOrder.TickRoom(run);

            Assert.That(run.pickups.Count, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ the boss exit

        /// <summary>
        /// A cleared boss room opens ONE exit, once - not a new portal on every tick the player
        /// stands in an empty boss room, which is what the one-shot flag is for.
        /// </summary>
        [Test]
        public void AClearedBossRoomOpensOneExitOnce()
        {
            var run = Room();
            run.player.x = 120; run.player.y = 200;
            run.CurrentRoom!.Type = RoomKind.Boss;

            run.enemies.Add(new Body { Kind = BodyKind.Boss });
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

        /// <summary>
        /// Death wins ties. A pickup arriving on the frame the player dies ends the run as a death,
        /// and the boss-exit check beneath it does not run.
        /// </summary>
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

        /// <summary>
        /// Picking up a hook leaves a held blast where the hook stood, and cannot be re-collected on
        /// the same tick.
        /// </summary>
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

        /// <summary>
        /// The item branch is NOT ported, and it says so by throwing. A silent no-op here would be a
        /// port that looks finished and plays differently; a crash is a gap you cannot miss.
        /// </summary>
        [Test]
        public void AnItemPickupThrowsUntilItemsIsPorted()
        {
            var run = Room();
            run.pickups.Add(new Pickup { x = 400, y = 355, r = 16, kind = "item", id = "heart", charges = 1 });

            Assert.Throws<System.NotSupportedException>(() => TickOrder.TickRoom(run));
        }

        /// <summary>
        /// An unknown pickup kind is left on the floor rather than silently eaten. Being strict about
        /// a name the port does not know is what stops a typo in a spawn call from deleting a reward.
        /// </summary>
        [Test]
        public void AnUnknownPickupKindIsLeftWhereItIs()
        {
            var run = Room();
            run.pickups.Add(Pickup.Of("somethingNew", 400, 355, 16));

            TickOrder.TickRoom(run);

            Assert.That(run.pickups.Count, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ a constant that was wrong

        /// <summary>
        /// The swerve meter's per-tick decay, pinned because it WAS wrong and nothing noticed.
        ///
        /// <para>
        /// <c>Balance.SwerveDecay</c> held 0.011 against the original's 0.0035 - a little over three
        /// times the rate, which means the game would have forgotten a reversal roughly three times
        /// faster than it does, and every gunner in the game would have aimed at where the player is
        /// instead of where they were going. No test failed. The suite asserted
        /// <c>SWERVE_DECAY &gt; 0</c> and that a reversal survives a second, and 0.011 satisfies both.
        /// Nothing in the port read the constant, so a wrong number sat in the file whose entire job
        /// is to hold the game's numbers.
        /// </para>
        ///
        /// <para>
        /// Two things catch it now. The bound is arithmetic rather than a restatement: the original's
        /// own test requires a reversal to still be fully remembered after
        /// <c>sec(1)</c> = 210 ticks, which caps the per-tick decay at about 0.0033 and rules 0.011
        /// out on the game's own terms. And the value is pinned exactly, measured from a meter filled
        /// to 1 and left alone: 0.5765 after one second, which is 0.0035 a tick.
        /// </para>
        /// </summary>
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
