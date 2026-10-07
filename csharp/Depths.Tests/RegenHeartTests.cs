using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The regenerating heart (C# only, 2026-10-06): the last of the starting 8 HP is its own layer,
    /// between the red hearts and the armour. It takes damage before the red, and refills in a fight
    /// after RegenDelay without a hit, 1 HP then 1 HP per RegenStep, and fully when a fought room is
    /// cleared. Pickups heal red only, Vigor adds red only, and Balance.JsReference turns it off.
    /// </summary>
    [TestFixture, Category("csharp-only")]
    public sealed class RegenHeartTests
    {
        static RunState Started()
        {
            var run = new RunState(new Rng(4242));
            run.Start(4242);
            run.readyT = 0; run.fadeT = 0;
            run.enemies.Clear(); run.pickups.Clear();
            return run;
        }

        static void WithABody(RunState run) => run.enemies.Add(Enemy.Of(BodyKind.Lunger, Balance.RoomRight - 40, Balance.RoomBottom - 40));

        [Test]
        public void ARunStartsWithSixRedAndTwoRegenerating()
        {
            var p = Started().player;
            Assert.That(p.hp, Is.EqualTo(6));
            Assert.That(p.maxHp, Is.EqualTo(6));
            Assert.That(p.regenHeart, Is.EqualTo(Balance.RegenHp));
            Assert.That(p.regenHeartMax, Is.EqualTo(2));
            Assert.That(p.hp + p.regenHeart, Is.EqualTo(8), "the total is unchanged: one red heart became the regenerating one");
        }

        [Test]
        public void VigorAddsRedHeartsOnly()
        {
            var run = Started();
            run.player.regenHeart = 0; run.player.regenHeartT = 77;
            Items.Give(run, "iron_ribs");   // +2 Vigor
            Assert.That(run.player.maxHp, Is.EqualTo(8));
            Assert.That(run.player.regenHeartMax, Is.EqualTo(2));
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "the item rebuild refilled the regenerating heart");
            Assert.That(run.player.regenHeartT, Is.EqualTo(77), "the item rebuild touched its clock");
        }

        [Test]
        public void TheRegenHeartTakesDamageBeforeTheRed()
        {
            var run = Started();
            Combat.DamagePlayer(run, 1, 0, 0, 0);
            Assert.That(run.player.regenHeart, Is.EqualTo(1));
            Assert.That(run.player.hp, Is.EqualTo(6));
        }

        [Test]
        public void WhatTheRegenHeartCannotCoverReachesTheRed()
        {
            var run = Started();
            run.player.regenHeart = 1;
            Combat.DamagePlayer(run, 4, 0, 0, 0);   // a gunner shot
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
            Assert.That(run.player.hp, Is.EqualTo(3));
        }

        [Test]
        public void ArmourComesFirstAndItsSpillGoesToTheRegenHeart()
        {
            var run = Started();
            run.player.armor = 1;
            Combat.DamagePlayer(run, 4, 0, 0, 0);   // costs 2 armour; 1 covers half, the other 2 HP spill
            Assert.That(run.player.armor, Is.EqualTo(0));
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
            Assert.That(run.player.hp, Is.EqualTo(6), "the spill is used up by the regenerating heart");
        }

        [Test]
        public void ArmourIsSpentBeforeTheRegenHeartIsTouched()
        {
            var run = Started();   // 6 red, regen 2/2
            run.player.armor = 2;
            Combat.DamagePlayer(run, 1, 0, 0, 0);   // a lunger: armour pays 1, nothing passes on
            Assert.That(run.player.armor, Is.EqualTo(1));
            Assert.That(run.player.regenHeart, Is.EqualTo(2), "the regenerating heart was hit while armour was left");
            Assert.That(run.player.hp, Is.EqualTo(6));
        }

        [Test]
        public void ArmourThatCoversPartOfAHitLeavesTheRegenHeartPartlyFull()
        {
            var run = Started();
            run.player.armor = 1;
            Combat.DamagePlayer(run, 2, 0, 0, 0, true);   // Warden contact: costs 2, armour pays 1, 1 HP passes on
            Assert.That(run.player.armor, Is.EqualTo(0));
            Assert.That(run.player.regenHeart, Is.EqualTo(1));
            Assert.That(run.player.hp, Is.EqualTo(6));
        }

        [Test]
        public void ItRefillsInAFightAfterTheDelayThenOneHpPerStep()
        {
            var run = Started();
            WithABody(run);
            run.player.regenHeart = 0; run.player.regenHeartT = 0;
            for (int t = 1; t < Balance.RegenDelay; t++) TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "refilled before the delay");
            TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(1), "the first half heart comes at the delay");
            for (int t = 1; t < Balance.RegenStep; t++) TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(1));
            TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(2), "the second half heart comes one step later");
            for (int t = 0; t < Balance.RegenStep * 3; t++) TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(2), "it never fills past its own heart");
        }

        [Test]
        public void AHitRestartsTheClock()
        {
            var run = Started();
            WithABody(run);
            run.player.regenHeart = 0; run.player.regenHeartT = 0;
            for (int t = 1; t < Balance.RegenDelay; t++) TickOrder.TickRegen(run);
            run.player.armor = 2;
            Combat.DamagePlayer(run, 1, 0, 0, 0);   // armour-only, and it still counts as a hit
            Assert.That(run.player.regenHeartT, Is.EqualTo(0));
            TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "the old clock carried over the hit");
        }

        [Test]
        public void AnEmptyRoomPausesTheClock()
        {
            var run = Started();
            run.player.regenHeart = 0;
            run.player.regenHeartT = Balance.RegenDelay - 1;   // one tick short when the room cleared
            for (int t = 0; t < Balance.RegenDelay * 3; t++) TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "it refilled with no fight in the room");
            Assert.That(run.player.regenHeartT, Is.EqualTo(Balance.RegenDelay - 1), "the empty room moved the clock");
            WithABody(run);
            TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(1), "time without a hit did not carry into the next fight");
        }

        [Test]
        public void HalfHeartsHealRedOnlyToo()
        {
            var run = Started();
            run.player.regenHeart = 0;
            run.pickups.Add(Pickup.Of("halfheart", run.player.x, run.player.y, 10));
            TickOrder.TickRoom(run);
            Assert.That(run.pickups, Has.Count.EqualTo(1), "a half heart was eaten by a full red bar");
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
            run.player.hp = 5;
            TickOrder.TickRoom(run);
            Assert.That(run.pickups, Is.Empty);
            Assert.That(run.player.hp, Is.EqualTo(6));
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "the half heart went to the regenerating heart");
        }

        [Test]
        public void AFinishedDoorTransitionLeavesTheHeartAndItsClockAlone()
        {
            var run = QuietFight();
            run.player.regenHeart = 1; run.player.regenHeartT = 123;
            run.trans = new Trans { t = 0, nx = run.curX, ny = run.curY, from = "N" };
            for (int t = 0; t < Balance.FadeOut + 5 && run.trans != null; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.trans, Is.Null, "the transition never finished; EnterRoom was not reached");
            Assert.That(run.player.regenHeart, Is.EqualTo(1), "entering a room touched the regenerating heart");
            Assert.That(run.player.regenHeartT, Is.EqualTo(123), "entering a room touched its clock");
        }

        [Test]
        public void EnteringAClearedRoomOrANewOneLeavesTheHeartAndItsClockAlone()
        {
            var run = Started();
            var start = run.CurrentRoom!;
            TickOrder.TickRoom(run);   // no bodies: the game marks the start room cleared
            Assert.That(start.Cleared, Is.True, "the start room is not marked cleared; the test is not measuring a cleared room");
            Room? next = null; Dir way = Dir.N;
            foreach (var d in start.Doors) { next = run.dungeon.Neighbour(start, d); way = d; if (next != null) break; }
            Assert.That(next, Is.Not.Null, "the start room has no neighbour to walk into");
            Assert.That(next!.Visited, Is.False);
            run.player.regenHeart = 0; run.player.regenHeartT = 123;
            Rooms.EnterRoom(run, next.X, next.Y, way);   // a room never entered before
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "a new room refilled the regenerating heart");
            Assert.That(run.player.regenHeartT, Is.EqualTo(123), "a new room touched its clock");
            run.enemies.Clear();
            Rooms.EnterRoom(run, start.X, start.Y, way);   // back into the cleared start room
            Assert.That(start.Cleared, Is.True);
            Assert.That(run.enemies, Is.Empty, "the start room is not empty; the test is not measuring a cleared room");
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "a cleared room refilled the regenerating heart");
            Assert.That(run.player.regenHeartT, Is.EqualTo(123), "a cleared room touched its clock");
        }

        [Test]
        public void ANewFloorLeavesTheHeartAndItsClockAlone()
        {
            var run = Started();
            run.player.regenHeart = 1; run.player.regenHeartT = 123;
            run.Descend();
            Assert.That(run.floor, Is.EqualTo(2));
            Assert.That(run.player.regenHeart, Is.EqualTo(1));
            Assert.That(run.player.regenHeartMax, Is.EqualTo(2));
            Assert.That(run.player.regenHeartT, Is.EqualTo(123));
        }

        [Test]
        public void UnderTheJsRulesAnItemRebuildKeepsEightRed()
        {
            Balance.JsReference = true;
            try
            {
                var run = Started();
                Items.Give(run, "lucky_coin");   // any item: the rebuild reapplies Vigor
                Assert.That(run.player.maxHp, Is.EqualTo(8));
                Items.Give(run, "iron_ribs");
                Assert.That(run.player.maxHp, Is.EqualTo(10));
            }
            finally { Balance.JsReference = false; }
        }

        [Test]
        public void AnEmptyRoomDoesNotRefillEvenWithTheClockOnARefillTick()
        {
            var run = Started();
            run.player.regenHeart = 1;
            run.player.regenHeartT = Balance.RegenDelay;   // the first half came in the fight, then the room cleared
            for (int t = 0; t < Balance.RegenStep * 3; t++) TickOrder.TickRegen(run);
            Assert.That(run.player.regenHeart, Is.EqualTo(1), "the heart filled with no one in the room");
            Assert.That(run.player.regenHeartT, Is.EqualTo(Balance.RegenDelay));
        }

        [Test]
        public void ClearingARoomWhoseWaveSpawnedNoFightLeavesTheHeartAndItsClockAlone()
        {
            var run = QuietFight();   // the start room: its wave spawned nothing, the lunger is added by hand
            for (int t = 0; t < 500; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.player.regenHeartT, Is.EqualTo(500));
            Kills.KillEnemy(run, 0);   // the last body
            for (int t = 0; t < 50; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.CurrentRoom!.Cleared, Is.True, "the room did not clear; the test is not measuring it");
            Assert.That(run.player.regenHeartT, Is.EqualTo(500), "clearing the room moved the clock");
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "clearing the room refilled the heart");
        }

        [Test]
        public void KillsBlinksPickupsAndTheCupLeaveTheHeartAndItsClockAlone()
        {
            var run = QuietFight();
            var p = run.player;
            p.regenHeartT = 321; p.hp = 2;
            void Same(string what)
            {
                Assert.That(p.regenHeart, Is.EqualTo(0), what + " refilled the regenerating heart");
                Assert.That(p.regenHeartT, Is.EqualTo(321), what + " moved its clock");
            }
            run.enemies.Add(Enemy.Of(BodyKind.Lunger, Balance.RoomLeft + 30, Balance.RoomTop + 30));
            Kills.KillEnemy(run, run.enemies.Count - 1);
            Same("a kill");
            run.pickups.Clear();
            foreach (var kind in new[] { "armor", "halfarmor", "heart", "halfheart" })
            {
                run.pickups.Add(Pickup.Of(kind, p.x, p.y, 10));
                TickOrder.TickRoom(run);
                Same("a " + kind + " pickup");
            }
            Items.Give(run, "tin_cup");
            Assert.That(Items.UseActive(run), Is.True);
            Same("the Tin Cup");
            int charges = run.blinkCharges;
            TickOrder.DoBlink(run, new Input(1, 0, blink: true));
            Assert.That(run.blinkCharges, Is.LessThan(charges), "no blink happened; the test is not measuring one");
            Same("a blink");
        }

        // --- a won fight refills the heart (owner, 2026-10-06) ---

        /// <summary>A started run standing in the first Normal room next to the start, its wave spawned.</summary>
        static RunState InAFoughtRoom()
        {
            var run = Started();
            var start = run.CurrentRoom!;
            foreach (var d in start.Doors)
            {
                var n = run.dungeon.Neighbour(start, d);
                if (n == null || n.Type != RoomKind.Normal) continue;
                Rooms.EnterRoom(run, n.X, n.Y, d);
                run.readyT = 0; run.fadeT = 0;
                return run;
            }
            Assert.Fail("seed 4242 has no Normal room beside the start");
            return run;
        }

        static void KillAll(RunState run) { while (run.enemies.Count > 0) Kills.KillEnemy(run, run.enemies.Count - 1); }

        [Test]
        public void ANormalRoomsWaveMarksItFoughtAndARoomWithoutOneIsNot()
        {
            var run = InAFoughtRoom();
            Assert.That(run.enemies, Is.Not.Empty);
            Assert.That(run.CurrentRoom!.Fought, Is.True);
            var fresh = Started();
            TickOrder.TickRoom(fresh);
            Assert.That(fresh.CurrentRoom!.Fought, Is.False, "the start room counts as a fight");
        }

        [Test]
        public void ClearingAFoughtRoomRefillsTheHeartAndZeroesItsClock()
        {
            var run = InAFoughtRoom();
            run.player.hp = 4; run.player.regenHeart = 0; run.player.regenHeartT = 500;
            KillAll(run);
            TickOrder.Update(run, Input.None);
            Assert.That(run.CurrentRoom!.Cleared, Is.True, "the room did not clear; the test is not measuring it");
            Assert.That(run.player.regenHeart, Is.EqualTo(2), "a won fight did not refill the heart");
            Assert.That(run.player.regenHeartT, Is.EqualTo(0));
            Assert.That(run.player.hp, Is.EqualTo(4), "the refill touched the red hearts");
        }

        [Test]
        public void TheRefillHappensOnceNotEveryTickInTheClearedRoom()
        {
            var run = InAFoughtRoom();
            KillAll(run);
            TickOrder.Update(run, Input.None);
            run.player.regenHeart = 0; run.player.regenHeartT = 77;   // as if hit after the clear
            for (int t = 0; t < Balance.RegenDelay * 2; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "standing in a cleared room kept refilling");
            Assert.That(run.player.regenHeartT, Is.EqualTo(77), "the empty room moved the clock");
        }

        [Test]
        public void KillingTheWardenRefillsTheHeart()
        {
            var run = Started();
            var boss = run.dungeon.AllRooms.Single(r => r.Type == RoomKind.Boss);
            Rooms.EnterRoom(run, boss.X, boss.Y, Dir.N);
            run.readyT = 0; run.fadeT = 0;
            Assert.That(boss.Fought, Is.True);
            run.player.regenHeart = 0;
            KillAll(run);
            TickOrder.Update(run, Input.None);
            Assert.That(run.player.regenHeart, Is.EqualTo(2));
        }

        [Test]
        public void TheItemRoomNeverRefillsTheHeart()
        {
            var run = Started();
            var item = run.dungeon.AllRooms.Single(r => r.Type == RoomKind.Item);
            run.player.regenHeart = 0;
            Rooms.EnterRoom(run, item.X, item.Y, Dir.N);
            run.readyT = 0; run.fadeT = 0;
            for (int t = 0; t < 20; t++) TickOrder.Update(run, Input.None);
            Assert.That(item.Cleared, Is.True, "the item room was never marked cleared; the test is not measuring it");
            Assert.That(item.Fought, Is.False);
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "an item room handed out a refill");
        }

        [Test]
        public void ADeathOnTheClearingTickGetsNoRefill()
        {
            var run = InAFoughtRoom();
            KillAll(run);
            var p = run.player;
            p.hp = 1; p.regenHeart = 0; p.iframes = 0;
            run.projectiles.Add(new Projectile { x = p.lagX, y = p.lagY + Balance.PlayerHitDy, vx = 0.01, r = 5, dmg = 2, friendly = false });
            TickOrder.Update(run, Input.None);
            Assert.That(p.hp, Is.LessThanOrEqualTo(0), "the shell did not kill; the test is not measuring a death");
            Assert.That(run.CurrentRoom!.Cleared, Is.True);
            Assert.That(p.regenHeart, Is.EqualTo(0), "a dead player was handed the refill");
        }

        [Test]
        public void ReEnteringAClearedFightRoomDoesNotRefillAgain()
        {
            var run = InAFoughtRoom();
            var fought = run.CurrentRoom!;
            KillAll(run);
            TickOrder.Update(run, Input.None);
            var start = run.dungeon.AllRooms.Single(r => r.Type == RoomKind.Start);
            Rooms.EnterRoom(run, start.X, start.Y, Dir.N);
            run.player.regenHeart = 0;
            Rooms.EnterRoom(run, fought.X, fought.Y, Dir.N);
            run.readyT = 0; run.fadeT = 0;
            for (int t = 0; t < 20; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "walking back into a won room refilled the heart");
        }

        [Test]
        public void TheSecretRoomNeverRefillsTheHeart()
        {
            var run = Started();
            var secret = run.dungeon.AllRooms.Single(r => r.Type == RoomKind.Secret);
            run.player.regenHeart = 0;
            Rooms.EnterRoom(run, secret.X, secret.Y, Dir.N);
            run.readyT = 0; run.fadeT = 0;
            for (int t = 0; t < 20; t++) TickOrder.Update(run, Input.None);
            Assert.That(secret.Cleared, Is.True, "the secret room was never marked cleared; the test is not measuring it");
            Assert.That(secret.Fought, Is.False);
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "a secret room handed out a refill");
        }

        [Test]
        public void UnderTheJsRulesAWonFightChangesNothing()
        {
            Balance.JsReference = true;
            try
            {
                var run = InAFoughtRoom();
                run.player.hp = 5;
                KillAll(run);
                TickOrder.Update(run, Input.None);
                Assert.That(run.player.regenHeart, Is.EqualTo(0));
                Assert.That(run.player.hp, Is.EqualTo(5));
            }
            finally { Balance.JsReference = false; }
        }

        [Test]
        public void TheDelayIsFourSecondsAndTheStepOne()
        {
            Assert.That(Balance.RegenDelay, Is.EqualTo(Balance.Sec(4)));
            Assert.That(Balance.RegenStep, Is.EqualTo(Balance.Sec(1)));
            Assert.That(Balance.RegenHp, Is.EqualTo(2), "one heart");
        }

        [Test]
        public void AHitThatDoesNotLandLeavesTheClockAlone()
        {
            var run = Started();
            run.player.regenHeartT = 500;
            run.player.iframes = 10;
            Assert.That(Combat.DamagePlayer(run, 2, 0, 0, 0), Is.False);
            Assert.That(run.player.regenHeartT, Is.EqualTo(500), "a hit refused by i-frames restarted the clock");
            run.player.iframes = 0;
            run.blinkGrace = 10; run.graceSpent = false;
            Assert.That(Combat.DamagePlayer(run, 2, 0, 0, 0), Is.False);
            Assert.That(run.player.regenHeartT, Is.EqualTo(500), "a hit forgiven by the blink grace restarted the clock");
            Assert.That(run.player.regenHeart, Is.EqualTo(2));
        }

        [Test]
        public void AHeartThatOverflowsTheRedIsNotPouredIntoTheRegenHeart()
        {
            var run = Started();
            run.player.hp = 5; run.player.regenHeart = 0;
            run.pickups.Add(Pickup.Of("heart", run.player.x, run.player.y, 10));
            TickOrder.TickRoom(run);
            Assert.That(run.pickups, Is.Empty);
            Assert.That(run.player.hp, Is.EqualTo(6));
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "the overflow leaked into the regenerating heart");
            Items.Give(run, "tin_cup");
            run.player.hp = 5;
            Assert.That(Items.UseActive(run), Is.True);
            Assert.That(run.player.hp, Is.EqualTo(6));
            Assert.That(run.player.regenHeart, Is.EqualTo(0), "the cup's overflow leaked into the regenerating heart");
        }

        // --- the clock inside the real tick, not called by hand ---

        /// <summary>A started run in a live fight that cannot touch the player: one lunger, stunned in a far corner.</summary>
        static RunState QuietFight()
        {
            var run = Started();
            var e = Enemy.Of(BodyKind.Lunger, Balance.RoomRight - 30, Balance.RoomBottom - 30);
            e.stun = 1e9;
            run.enemies.Add(e);
            run.player.regenHeart = 0; run.player.regenHeartT = 0;
            return run;
        }

        static int TicksToRefill(RunState run, int limit)
        {
            for (int t = 1; t <= limit; t++)
            {
                TickOrder.Update(run, Input.None);
                if (run.player.regenHeart > 0) return t;
            }
            return -1;
        }

        [Test]
        public void TheGameLoopRefillsItAtTheDelay()
        {
            var run = QuietFight();
            Assert.That(TicksToRefill(run, Balance.RegenDelay * 2), Is.EqualTo(Balance.RegenDelay));
            for (int t = 0; t < Balance.RegenStep; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.player.regenHeart, Is.EqualTo(2));
            Assert.That(run.player.hp, Is.EqualTo(6), "the fight touched the player");
        }

        [Test]
        public void TheReadyFreezeDoesNotRunTheClock()
        {
            var run = QuietFight();
            run.readyT = 100;
            Assert.That(TicksToRefill(run, Balance.RegenDelay * 2), Is.EqualTo(Balance.RegenDelay + 100));
        }

        [Test]
        public void ADoorTransitionDoesNotRunTheClock()
        {
            var run = QuietFight();
            run.trans = new Trans { t = 0, nx = run.curX, ny = run.curY, from = "N" };
            for (int t = 0; t < Balance.FadeOut - 1; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.trans, Is.Not.Null, "the transition finished early; the test is not measuring it");
            Assert.That(run.player.regenHeartT, Is.EqualTo(0));
        }

        [Test]
        public void TheClockDoesNotRunAfterDeath()
        {
            var run = QuietFight();
            run.player.hp = 0;
            for (int t = 0; t < Balance.RegenDelay * 2; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.state, Is.EqualTo("gameover"));
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
            Assert.That(run.player.regenHeartT, Is.EqualTo(0));
        }

        [Test]
        public void AnEnemyShotInTheGameLoopRestartsTheClock()
        {
            var run = QuietFight();
            run.player.regenHeart = 2;
            for (int t = 0; t < 100; t++) TickOrder.Update(run, Input.None);
            var p = run.player;
            run.projectiles.Add(new Projectile { x = p.lagX, y = p.lagY, vx = 0.01, vy = 0, r = 5, dmg = 2, friendly = false });
            TickOrder.Update(run, Input.None);
            Assert.That(p.regenHeart, Is.EqualTo(0), "the shot did not land on the regenerating heart");
            Assert.That(p.hp, Is.EqualTo(6));
            Assert.That(p.regenHeartT, Is.EqualTo(0), "the hit did not restart the clock");
            Assert.That(TicksToRefill(run, Balance.RegenDelay * 2), Is.EqualTo(Balance.RegenDelay));
        }

        [Test]
        public void AWardenHitGoesThroughArmourAtFullWeightIntoTheRegenHeart()
        {
            var run = Started();
            run.player.armor = 1;
            run.player.regenHeartT = 600;
            Combat.DamagePlayer(run, 3, 0, 0, 0, true);   // the sweep: armour pays 1 of 3, 2 HP pass on
            Assert.That(run.player.regenHeartT, Is.EqualTo(0), "a Warden hit did not restart the clock");
            Assert.That(run.player.armor, Is.EqualTo(0));
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
            Assert.That(run.player.hp, Is.EqualTo(6));
            run.player.iframes = 0;
            run.player.armor = 0; run.player.regenHeart = 1;
            Combat.DamagePlayer(run, 3, 0, 0, 0, true);   // regen 1 of 3, red takes 2
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
            Assert.That(run.player.hp, Is.EqualTo(4));
        }

        [Test]
        public void PickupsHealRedOnlyAndAFullRedPlayerLeavesThemLying()
        {
            var run = Started();
            run.player.regenHeart = 0;
            run.pickups.Add(Pickup.Of("heart", run.player.x, run.player.y, 10));
            TickOrder.TickRoom(run);
            Assert.That(run.pickups, Has.Count.EqualTo(1), "a heart was eaten by a full red bar");
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
            run.player.hp = 3;
            TickOrder.TickRoom(run);
            Assert.That(run.pickups, Is.Empty);
            Assert.That(run.player.hp, Is.EqualTo(5));
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
        }

        [Test]
        public void TheTinCupHealsRedOnly()
        {
            var run = Started();
            Items.Give(run, "tin_cup");
            run.player.regenHeart = 0;
            Assert.That(Items.UseActive(run), Is.False, "a full red bar drinks nothing, whatever the regen heart holds");
            run.player.hp = 4;
            Assert.That(Items.UseActive(run), Is.True);
            Assert.That(run.player.hp, Is.EqualTo(6));
            Assert.That(run.player.regenHeart, Is.EqualTo(0));
        }

        [Test]
        public void TheJsRulesHaveNoRegenHeart()
        {
            Balance.JsReference = true;
            try
            {
                var p = Started().player;
                Assert.That(p.hp, Is.EqualTo(8));
                Assert.That(p.maxHp, Is.EqualTo(8));
                Assert.That(p.regenHeart, Is.EqualTo(0));
                Assert.That(p.regenHeartMax, Is.EqualTo(0));
            }
            finally { Balance.JsReference = false; }
        }

        [Test]
        public void DeathStillComesWhenTheRedRunsOut()
        {
            var run = Started();
            run.player.hp = 1; run.player.regenHeart = 0;
            Combat.DamagePlayer(run, 2, 0, 0, 0);
            Assert.That(run.player.hp, Is.LessThanOrEqualTo(0));
            TickOrder.Update(run, Input.None);
            Assert.That(run.state, Is.EqualTo("gameover"), "the run did not end when the red ran out");
        }
    }
}
