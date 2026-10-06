using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// Whole-number HP (C# only, 2026-10-06): 1 HP is half a heart; every enemy hit
    /// is a whole number; armour takes 0.6x a normal enemy's hit, rounded down but at least 1, and
    /// the Warden's at full weight; pickups come whole (2) and half (1). The JS has none of this, so
    /// these are assertions, not parity rows. The player here is a bare one (8 red, no regenerating
    /// heart), so each rule is seen alone; RegenHeartTests covers the real starting body.
    /// </summary>
    [TestFixture]
    public sealed class HeartTests
    {
        static RunState Run()
        {
            var run = new RunState(new Rng(4242)) { dungeon = Dungeon.FromSeed(new Rng(4242)) };
            run.state = "playing";
            run.readyT = 0; run.fadeT = 0;
            run.enemies.Clear(); run.pickups.Clear();
            run.player.x = 400; run.player.y = 355;
            run.player.hp = 8; run.player.maxHp = 8; run.player.armor = 0;
            return run;
        }

        static bool Whole(double v) => v == System.Math.Floor(v);

        [Test]
        public void EveryHitIsAWholeNumber()
        {
            Assert.That(Enemy.Of(BodyKind.Shooter, 0, 0).dmg, Is.EqualTo(2), "a shooter's shot is a heart");
            Assert.That(Enemy.Of(BodyKind.Gunner, 0, 0).dmg, Is.EqualTo(4), "a gunner's is two");
            var boss = Enemy.Of(BodyKind.Boss, 0, 0);
            Spawn.BossInit(boss);
            Assert.That(boss.dmg, Is.EqualTo(2), "a Warden shell is a heart");
            Assert.That(Balance.BossSweepDmg, Is.EqualTo(3));
            foreach (var v in new[] { Bodies.ShotDmg, Balance.BossShellDmg, Balance.BossSweepDmg })
                Assert.That(Whole(v), Is.True, v + " is not a whole number of half-hearts");
        }

        [Test]
        public void HeartsTakeTheWholeHit()
        {
            var run = Run();
            Combat.DamagePlayer(run, 4, 0, 0, 0);
            Assert.That(run.player.hp, Is.EqualTo(4));
        }

        [TestCase(1.0, 1.0)]   // contact: 0.6 rounds to 0, and a hit always costs something
        [TestCase(2.0, 1.0)]   // shooter
        [TestCase(4.0, 2.0)]   // gunner
        public void ArmourTakesSixTenthsRoundedDownAtLeastOne(double hit, double cost)
        {
            var run = Run();
            run.player.armor = 4;
            Combat.DamagePlayer(run, hit, 0, 0, 0);
            Assert.That(run.player.armor, Is.EqualTo(4 - cost));
            Assert.That(run.player.hp, Is.EqualTo(8), "armour that covers the hit keeps the hearts whole");
        }

        [Test]
        public void TheWardenHitsArmourAtFullWeight()
        {
            var run = Run();
            run.player.armor = 4;
            Combat.DamagePlayer(run, 3, 0, 0, 0, true);
            Assert.That(run.player.armor, Is.EqualTo(1));
            Assert.That(run.player.hp, Is.EqualTo(8));
        }

        [Test]
        public void WhatArmourCannotCoverReachesTheHeartsAtFullWeight()
        {
            var run = Run();
            run.player.armor = 1;
            Combat.DamagePlayer(run, 4, 0, 0, 0);   // costs 2 armour; 1 covers half the hit
            Assert.That(run.player.armor, Is.EqualTo(0));
            Assert.That(run.player.hp, Is.EqualTo(6), "the uncovered half of a 4 is 2");
            run.player.iframes = 0;
            run.player.armor = 1;
            Combat.DamagePlayer(run, 3, 0, 0, 0, true);   // Warden: costs 3; 1 covers a third
            Assert.That(run.player.hp, Is.EqualTo(4), "the uncovered two thirds of a 3 is 2");
        }

        [TestCase("heart", 2.0, 0.0)]
        [TestCase("halfheart", 1.0, 0.0)]
        [TestCase("armor", 0.0, 2.0)]
        [TestCase("halfarmor", 0.0, 1.0)]
        public void PickupsComeWholeAndHalf(string kind, double hp, double armour)
        {
            var run = Run();
            run.player.hp = 4;
            run.pickups.Add(Pickup.Of(kind, 400, 355, 10));
            TickOrder.TickRoom(run);
            Assert.That(run.pickups, Is.Empty, kind + " was not collected");
            Assert.That(run.player.hp, Is.EqualTo(4 + hp));
            Assert.That(run.player.armor, Is.EqualTo(armour));
        }

        [Test]
        public void AFullPlayerLeavesAHalfHeartLying()
        {
            var run = Run();
            run.pickups.Add(Pickup.Of("halfheart", 400, 355, 10));
            TickOrder.TickRoom(run);
            Assert.That(run.pickups, Has.Count.EqualTo(1));
        }

        [Test]
        public void LootSplitsEachBandIntoHalvesAndWholes()
        {
            var rng = new Rng(77);
            var n = new System.Collections.Generic.Dictionary<string, int>();
            const int draws = 200000;
            for (int i = 0; i < draws; i++)
            {
                var d = Loot.Drop(rng, 0, 0);
                string k = d == null ? "none" : d.kind;
                n[k] = n.TryGetValue(k, out var c) ? c + 1 : 1;
            }
            double F(string k) => n.TryGetValue(k, out var c) ? (double)c / draws : 0;
            Assert.That(F("halfheart"), Is.EqualTo(0.09).Within(0.005));
            Assert.That(F("heart"), Is.EqualTo(0.09).Within(0.005));
            Assert.That(F("halfarmor"), Is.EqualTo(0.04).Within(0.005));
            Assert.That(F("armor"), Is.EqualTo(0.04).Within(0.005));
            Assert.That(F("none"), Is.EqualTo(0.74).Within(0.005), "the drop rate itself is unchanged");
        }
    }
}
