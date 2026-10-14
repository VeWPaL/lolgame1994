using System;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The body table, the spawner, and the trait - against values read out of the running JavaScript. [h:BodyParityTests-1]</summary>
    [TestFixture]
    public sealed class BodyParityTests
    {
        // ------------------------------------------------- the table

        [Test]
        public void TheTableMatchesTheMeasuredJavaScriptValues()
        {
            // read out of the browser at floor 1 (depthTough = depthRate = 1); re-read 2026-10-06
            var lunger = Bodies.Of(BodyKind.Lunger);
            Assert.That(lunger.Mass, Is.EqualTo(1.0));
            Assert.That(lunger.Radius, Is.EqualTo(14));
            Assert.That(lunger.Bar, Is.EqualTo(26));
            Assert.That(lunger.Hp, Is.EqualTo(20.25).Within(1e-9));
            Assert.That(lunger.Walk, Is.EqualTo(0.3456).Within(1e-9));
            Assert.That(lunger.Run, Is.EqualTo(0.94464).Within(1e-9));
            Assert.That(lunger.Armour, Is.EqualTo(0.66));

            var brunch = Bodies.Of(BodyKind.Brunch);
            Assert.That(brunch.Mass, Is.EqualTo(0.5));
            Assert.That(brunch.Radius, Is.EqualTo(8));
            Assert.That(brunch.Bar, Is.EqualTo(11));
            Assert.That(brunch.Hp, Is.EqualTo(2.7).Within(1e-9));
            Assert.That(brunch.Walk, Is.EqualTo(0.624).Within(1e-9));
            Assert.That(brunch.Run, Is.EqualTo(2.1).Within(1e-9),
                "ENEMY.brunch.run in the running game (BRUNCH_CHASE_SPEED). This test pinned a stale 1.75 "
                + "by hand for a while, and passed; the values here were re-read from the browser 2026-10-06");
            Assert.That(brunch.Armour, Is.EqualTo(1.0).Within(1e-9),
                "the Brunch is the one body below the size line, so it takes full damage; armouring "
                + "the chip body broke the alt blast's promise that one budget deletes a small group");

            var shooter = Bodies.Of(BodyKind.Shooter);
            Assert.That(shooter.Hp, Is.EqualTo(7.56).Within(1e-9));
            Assert.That(shooter.Base, Is.EqualTo(0.432).Within(1e-9));
            Assert.That(shooter.Sense, Is.EqualTo(600));
            Assert.That(shooter.Range, Is.EqualTo(520));
            Assert.That(shooter.Close, Is.EqualTo(150));
            Assert.That(shooter.Far, Is.EqualTo(250));
            Assert.That(shooter.CdMin, Is.EqualTo(105));   // sec(0.5)
            Assert.That(shooter.CdVar, Is.EqualTo(84));    // sec(0.4)
            Assert.That(shooter.Dmg, Is.EqualTo(2), "whole HP since 2026-10-06; the JS 1.8 is Balance.JsShotDmg");
            Assert.That(shooter.PShotSpeed, Is.EqualTo(2.2));
            Assert.That(shooter.PShotRadius, Is.EqualTo(5));
            Assert.That(shooter.Walk, Is.Null, "a shooter with a walk speed stops being a shooter");
            Assert.That(shooter.Armour, Is.EqualTo(0.66).Within(1e-9));

            var gunner = Bodies.Of(BodyKind.Gunner);
            Assert.That(gunner.Mass, Is.EqualTo(2.4));
            Assert.That(gunner.Radius, Is.EqualTo(22));
            Assert.That(gunner.Bar, Is.EqualTo(32));
            Assert.That(gunner.Hp, Is.EqualTo(10.8).Within(1e-9));
            Assert.That(gunner.Sense, Is.EqualTo(700));
            Assert.That(gunner.Close, Is.EqualTo(120));
            Assert.That(gunner.Far, Is.EqualTo(200));
            Assert.That(gunner.CdMin, Is.EqualTo(168));   // sec(0.8)
            Assert.That(gunner.CdVar, Is.EqualTo(126));   // sec(0.6)
            Assert.That(gunner.Dmg, Is.EqualTo(4));       // SHOT_DMG*2; the JS 3.6 under JsReference
            Assert.That(gunner.PShotRadius, Is.EqualTo(7));
            Assert.That(gunner.Armour, Is.EqualTo(0.66).Within(1e-9));

            var boss = Bodies.Of(BodyKind.Boss);
            Assert.That(boss.Mass, Is.EqualTo(4.0));
            Assert.That(boss.Radius, Is.EqualTo(28));
            Assert.That(boss.Bar, Is.EqualTo(40));
            Assert.That(boss.Hp, Is.EqualTo(463.05).Within(1e-9));   // 343*TOUGH
            Assert.That(boss.Walk, Is.EqualTo(0.504).Within(1e-9));
            Assert.That(boss.Run, Is.EqualTo(0.864).Within(1e-9));
            Assert.That(boss.Base, Is.EqualTo(0.576).Within(1e-9));
            Assert.That(boss.Armour, Is.EqualTo(0.66).Within(1e-9));
        }

        /// <summary>One armour rule for every body, asserted on the TABLE rather than on the values the per-body test above... [h:BodyParityTests-2]</summary>
        [Test]
        public void EveryRowDeclaresTheSameArmour()
        {
            Assert.That((int)BodyKind.Splitter, Is.EqualTo(5), "the JS body table (the first five kinds) lost or gained a row");
            Assert.That(Bodies.Rows.Length, Is.EqualTo(Enum.GetValues(typeof(BodyKind)).Length), "C# appends the middle enemies after it, a row each");

            var line = Bodies.Of(BodyKind.Lunger).Radius;
            var wrong = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Bodies.Rows.Length; i++)
            {
                double want = Bodies.Rows[i].Radius >= line ? Bodies.Armour : 1.0;
                if (Math.Abs(Bodies.Rows[i].Armour - want) > 1e-12)
                    wrong.Add($"row {i} (r{Bodies.Rows[i].Radius}) = {Bodies.Rows[i].Armour}, wanted {want}");
            }

            Assert.That(wrong, Is.Empty,
                "a body is either unarmoured (1) or armoured (0.66), decided by size, so the same "
                + "shot must not land differently on two bodies of the same radius: " + string.Join(", ", wrong));
        }

        // ------------------------------------------------- the spawner (Spawn.Body, the one the game uses)

        static Enemy Make(BodyKind kind, int floor, uint seed = 1, int trait = 0)
        {
            var run = new RunState(new Rng(seed)) { floor = floor };
            return Spawn.Body(run, kind, 0, 0, trait);
        }

        [Test]
        public void TheSpawnerBuildsTheRightShapeOfBody()
        {
            var lunger = Make(BodyKind.Lunger, 1);
            Assert.That(lunger.walkSpeed, Is.GreaterThan(0), "a lunger came out ranged");
            Assert.That(lunger.curSpeed, Is.EqualTo(0.3456).Within(1e-9));
            Assert.That(lunger.lungeState, Is.EqualTo("approach"));

            var shooter = Make(BodyKind.Shooter, 1);
            Assert.That(shooter.walkSpeed, Is.EqualTo(0), "a shooter came out a walker");
            Assert.That(shooter.sense, Is.EqualTo(600));
            Assert.That(shooter.shootCd, Is.InRange(shooter.cdMin, shooter.cdMin + shooter.cdVar),
                "a ranged body spawns with its first shot scheduled");
        }

        [Test]
        public void FloorOneIsNeutral()
        {
            // at floor 1 the ladder is exactly 1 on both dials: a floor-1 body is its table row, tempo-scaled once
            var row = Bodies.Of(BodyKind.Shooter);
            var b = Make(BodyKind.Shooter, 1);
            Assert.That(b.maxHp, Is.EqualTo(row.Hp).Within(1e-9));
            Assert.That(b.speed, Is.EqualTo(row.Base!.Value * Balance.TempoRate).Within(1e-9));
            Assert.That(b.cdMin, Is.EqualTo(row.CdMin!.Value / Balance.TempoRate).Within(1e-9));
        }

        [Test]
        public void ADeeperFloorMakesARangedBodyFasterAndAWalkerNoFaster()
        {
            var near = Make(BodyKind.Shooter, 1);
            var far = Make(BodyKind.Shooter, 50);
            Assert.That(far.speed, Is.GreaterThan(near.speed), "depth did not make a ranged body faster");
            Assert.That(far.cdMin, Is.LessThan(near.cdMin), "depth did not make a ranged body faster to shoot");
            var lNear = Make(BodyKind.Lunger, 1);
            var lFar = Make(BodyKind.Lunger, 50);
            Assert.That(lFar.runSpeed, Is.EqualTo(lNear.runSpeed).Within(1e-12), "a walker's top speed must not scale with depth");
            Assert.That(lFar.maxHp, Is.GreaterThan(lNear.maxHp), "depth did not make a body tougher");
        }

        [Test]
        public void NoBodyAtAnyDepthOutrunsThePlayer()
        {
            for (int floor = 1; floor <= 1000; floor++)
            {
                var g = Make(BodyKind.Gunner, floor);
                Assert.That(g.speed, Is.LessThan(Balance.PlayerMove), "a gunner at floor " + floor + " approaches at " + g.speed);
            }
        }

        [Test]
        public void TheSpawnerNeverGivesARangedBodyAWalkSpeed()
        {
            foreach (var kind in new[] { BodyKind.Shooter, BodyKind.Gunner })
                for (int floor = 1; floor <= 60; floor++)
                    Assert.That(Make(kind, floor, (uint)(floor * 7 + (int)kind)).walkSpeed, Is.EqualTo(0),
                        kind + " at floor " + floor + " was given a walk speed: the tick would take the walker branch");
        }

        [Test]
        public void AWalkerIsNeverGivenATrait()
        {
            foreach (var kind in new[] { BodyKind.Lunger, BodyKind.Brunch })
                foreach (int t in new[] { 1, 2 })
                {
                    var b = Make(kind, 20, 5, t);
                    Assert.That(b.trait, Is.EqualTo(0), kind + " accepted a trait");
                    Assert.That(b.far, Is.EqualTo(0));
                }
        }

        [Test]
        public void ATraitMovesTheBandAndKeepsItsKit()
        {
            var plain = Make(BodyKind.Shooter, 1, 7);
            var held = Make(BodyKind.Shooter, 1, 7, 1);
            var closed = Make(BodyKind.Shooter, 1, 7, 2);
            Assert.That(held.far, Is.GreaterThan(plain.far), "HOLD did not move the body out");
            Assert.That(held.close, Is.GreaterThan(plain.close), "HOLD moved only far");
            Assert.That(closed.far, Is.LessThan(plain.far), "CLOSE did not move the body in");
            Assert.That(closed.close, Is.LessThan(plain.close), "CLOSE moved only far");
            Assert.That(held.far, Is.EqualTo(Math.Min(plain.far * Spawn.TraitHoldScale, plain.sense * Spawn.TraitFarCeil)).Within(1e-9), "the hold scale");
            Assert.That(closed.far, Is.EqualTo(Math.Max(plain.far * Spawn.TraitCloseScale, Spawn.TraitFarFloor)).Within(1e-9), "the close scale");
            Assert.That(held.walkSpeed, Is.EqualTo(0), "a traited body is still a ranged body");
        }

        [Test]
        public void ATraitChangesNoCombatNumberAtAll()
        {
            // a trait may change where a fight happens and never how hard it is
            var plain = Make(BodyKind.Gunner, 20, 3);
            var traited = Make(BodyKind.Gunner, 20, 3, 1);
            Assert.That(traited.maxHp, Is.EqualTo(plain.maxHp));
            Assert.That(traited.mass, Is.EqualTo(plain.mass));
            Assert.That(traited.r, Is.EqualTo(plain.r));
            Assert.That(traited.armour, Is.EqualTo(plain.armour));
            Assert.That(traited.dmg, Is.EqualTo(plain.dmg));
            Assert.That(traited.speed, Is.EqualTo(plain.speed), "the trait changed the approach speed");
            Assert.That(traited.cdMin, Is.EqualTo(plain.cdMin), "the trait changed the cadence");
            Assert.That(traited.sense, Is.EqualTo(plain.sense));
            Assert.That(traited.far, Is.Not.EqualTo(plain.far), "the trait did nothing at all");
        }

        [Test]
        public void AShiftedBandStaysInsideTheClampsThatKeepTheBodyAbleToFight()
        {
            var rng = new Rng(11u);
            for (int i = 0; i < 400; i++)
                foreach (var kind in new[] { BodyKind.Shooter, BodyKind.Gunner, BodyKind.Splitter, BodyKind.Mortar, BodyKind.Sprayer })
                {
                    var b = Make(kind, 30, (uint)i + 1, Spawn.RollTrait(rng, i % 4));
                    if (b.trait == 0) continue;
                    Assert.That(b.far, Is.LessThanOrEqualTo(b.sense * Spawn.TraitFarCeil + 1e-9), "a traited body holds beyond its own awareness");
                    Assert.That(b.far, Is.GreaterThanOrEqualTo(Spawn.TraitFarFloor - 1e-9));
                    Assert.That(b.far - b.close, Is.GreaterThanOrEqualTo(Spawn.TraitBandMin - 1e-9), "the band collapsed");
                }
        }

        [Test]
        public void ABodyWithAPlannedTraitDrawsNoRunNumber()
        {
            // the wave planner rolls the trait; the spawn itself must not spend a run draw on it again
            var rng = new Rng(1u);
            var run = new RunState(rng) { floor = 1 };
            long before = rng.RunCalls;
            Spawn.Body(run, BodyKind.Shooter, 0, 0, 0);
            Spawn.Body(run, BodyKind.Lunger, 0, 0);
            Assert.That(rng.RunCalls, Is.EqualTo(before), "a spawn drew from the run stream");
            Assert.That(rng.JitterCalls, Is.GreaterThan(0), "a body drew no jitter at all");
            Spawn.Body(run, BodyKind.Shooter, 0, 0);
            Assert.That(rng.RunCalls, Is.EqualTo(before + 1), "a lone ranged spawn rolls its trait: one run draw");
        }
    }
}
