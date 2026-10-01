using System;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The body table, the spawner, and the trait - against values read out of the running JavaScript.
    ///
    /// <para>
    /// The one this file exists for is <see cref="TheSpawnerNeverGivesARangedBodyAWalkSpeed"/>. That
    /// is the bug that nearly shipped, it has already happened once in the JavaScript, and the shape
    /// of it is worth being blunt about: a trait meant to make a shooter close faster silently turned
    /// it into a lunger, and the whole ranged kit - shell, cadence, muzzle prediction, standoff rule -
    /// stopped running with nothing anywhere reporting why.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class BodyParityTests
    {
        // ------------------------------------------------- the table

        [Test]
        public void TheTableMatchesTheMeasuredJavaScriptValues()
        {
            // read out of the browser at floor 1, where depthTough and depthRate are both exactly 1
            var lunger = Bodies.Of(BodyKind.Lunger);
            Assert.That(lunger.Mass, Is.EqualTo(1.0));
            Assert.That(lunger.Radius, Is.EqualTo(14));
            Assert.That(lunger.Bar, Is.EqualTo(26));
            Assert.That(lunger.Hp, Is.EqualTo(24.3).Within(1e-9));
            Assert.That(lunger.Walk, Is.EqualTo(0.3456).Within(1e-9));
            Assert.That(lunger.Run, Is.EqualTo(0.94464).Within(1e-9));
            Assert.That(lunger.Armour, Is.EqualTo(0.66));

            var brunch = Bodies.Of(BodyKind.Brunch);
            Assert.That(brunch.Mass, Is.EqualTo(0.5));
            Assert.That(brunch.Radius, Is.EqualTo(8));
            Assert.That(brunch.Bar, Is.EqualTo(11));
            Assert.That(brunch.Hp, Is.EqualTo(2.7).Within(1e-9));
            Assert.That(brunch.Walk, Is.EqualTo(0.744).Within(1e-9));
            Assert.That(brunch.Run, Is.EqualTo(1.35).Within(1e-9));
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
            Assert.That(shooter.Dmg, Is.EqualTo(1.8));
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
            Assert.That(gunner.Dmg, Is.EqualTo(3.6));     // SHOT_DMG*2
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

        /// <summary>
        /// One armour rule for every body, asserted on the TABLE rather than on the values the
        /// per-body test above already reads.
        ///
        /// <para>
        /// The per-body assertions would all still pass if a new row were added tomorrow without an
        /// armour field, because nothing in them walks the table. That is precisely how the Warden
        /// ended up on a different rule from everything else in the JavaScript while this port sat
        /// here asserting it was the correct one: <c>BodyRow.Armour</c> is a plain double, so the
        /// omission read 0.0 here and 1.0 there, and neither side had a check that would have said so.
        /// </para>
        ///
        /// <para>
        /// The rule is about SIZE and not about any one body: every row at least as wide as a lunger
        /// is armoured, and the Brunch - half a lunger's radius, an eighth of its health - is the one
        /// body that is not. The line is read off the table rather than written as a list of names,
        /// so a new body lands on the correct side of it automatically and a renamed one cannot
        /// quietly escape.
        /// </para>
        /// <para>
        /// The expectation is the constant, not a literal. A second place holding a copy of 0.66 is
        /// what let the two ports drift in the first place, and a copy is exactly what this test
        /// would be protecting if it read 0.66 directly.
        /// </para>
        /// </summary>
        [Test]
        public void EveryRowDeclaresTheSameArmour()
        {
            Assert.That(Bodies.Rows.Length, Is.EqualTo(5), "the body table lost or gained a row");

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

        // ------------------------------------------------- the spawner

        [Test]
        public void TheSpawnerBuildsTheRightShapeOfBody()
        {
            var rng = new Rng(1u);
            var lunger = Bodies.Spawn(BodyKind.Lunger, 1, rng);
            Assert.That(lunger.HasWalkSpeed, Is.True, "a lunger came out ranged");
            Assert.That(lunger.CanTakeTrait, Is.False, "a walker is offered a trait it cannot use");
            Assert.That(lunger.CurSpeed, Is.EqualTo(0.3456).Within(1e-9));
            Assert.That(lunger.LungeState, Is.EqualTo("approach"));

            var shooter = Bodies.Spawn(BodyKind.Shooter, 1, rng);
            Assert.That(shooter.HasWalkSpeed, Is.False, "a shooter came out a walker");
            Assert.That(shooter.CanTakeTrait, Is.True);
            Assert.That(shooter.Sense, Is.EqualTo(600));
            Assert.That(shooter.ShootCd, Is.Not.Null, "a ranged body spawns with no first shot scheduled, " +
                "so it stands there until something else tells it to");
            Assert.That(shooter.ShootCd!.Value,
                Is.InRange(shooter.CdMin!.Value, shooter.CdMin!.Value + shooter.CdVar!.Value));
        }

        [Test]
        public void FloorOneIsNeutral()
        {
            // at floor 1 the ladder is exactly 1 on both dials, so a floor-1 body must equal its table
            // row with no scaling applied. Anything else means the scaling is applied twice.
            var rng = new Rng(1u);
            var row = Bodies.Of(BodyKind.Shooter);
            var b = Bodies.Spawn(BodyKind.Shooter, 1, rng);
            Assert.That(b.MaxHp, Is.EqualTo(row.Hp).Within(1e-9));
            Assert.That(b.Speed, Is.EqualTo(row.Base * Balance.PressureRate).Within(1e-9));
            Assert.That(b.CdMin, Is.EqualTo(row.CdMin / Balance.PressureRate).Within(1e-9));
        }

        [Test]
        public void ADeeperFloorMakesARangedBodyFasterAndAWalkerNoFaster()
        {
            // THE asymmetry, and it is the whole reason the rate dial is bounded. A ranged body's
            // approach speed and cadence scale with depth, so the bound on that dial is what keeps a
            // gunner slower than the player. A WALKER'S TOP SPEED DOES NOT SCALE AT ALL - and if that
            // ever changes, a lunger at depth outruns the player and the fairness property is gone.
            var rng = new Rng(1u);
            var near = Bodies.Spawn(BodyKind.Shooter, 1, rng);
            var far = Bodies.Spawn(BodyKind.Shooter, 50, rng);
            Assert.That(far.Speed, Is.GreaterThan(near.Speed!), "depth did not make a ranged body faster");
            Assert.That(far.CdMin, Is.LessThan(near.CdMin!.Value), "depth did not make a ranged body faster to shoot");

            var lNear = Bodies.Spawn(BodyKind.Lunger, 1, rng);
            var lFar = Bodies.Spawn(BodyKind.Lunger, 50, rng);
            Assert.That(lFar.RunSpeed, Is.EqualTo(lNear.RunSpeed!.Value).Within(1e-12),
                "a walker's top speed now scales with depth, so a lunger at floor 50 is faster than " +
                "the player and the game stops being reactive");
            Assert.That(lFar.MaxHp, Is.GreaterThan(lNear.MaxHp), "depth did not make a body tougher");
        }

        [Test]
        public void NoBodyAtAnyDepthOutrunsThePlayer()
        {
            for (int floor = 1; floor <= 1000; floor++)
            {
                var rng = new Rng(1u);
                var g = Bodies.Spawn(BodyKind.Gunner, floor, rng);
                Assert.That(g.Speed!.Value, Is.LessThan(Balance.PlayerMove),
                    "a gunner at floor " + floor + " approaches at " + g.Speed +
                    " px/tick against a player who moves at " + Balance.PlayerMove);
            }
        }

        // ------------------------------------------------- THE bug this file exists for

        [Test]
        public void TheSpawnerNeverGivesARangedBodyAWalkSpeed()
        {
            foreach (BodyKind kind in new[] { BodyKind.Shooter, BodyKind.Gunner })
            {
                for (int floor = 1; floor <= 60; floor++)
                {
                    var b = Bodies.Spawn(kind, floor, new Rng((uint)(floor * 7 + (int)kind)));
                    Assert.That(b.WalkSpeed, Is.Null,
                        kind + " at floor " + floor + " was given a walkSpeed, so the simulation will " +
                        "take the walker branch for a body that has no ranged kit. Its shell, its " +
                        "cadence, its muzzle prediction and its standoff rule all stop running.");
                }
            }
        }

        [Test]
        public void ABodyGivenATraitKeepsItsKit()
        {
            foreach (BodyKind kind in new[] { BodyKind.Shooter, BodyKind.Gunner })
            {
                var rng = new Rng(99u);
                for (int i = 0; i < 400; i++)
                {
                    var b = Bodies.Spawn(kind, 20, rng);
                    Bodies.ApplyTrait(b, Bodies.TraitFor(kind, rng));
                    Assert.That(b.WalkSpeed, Is.Null,
                        "a traited " + kind + " acquired a walkSpeed and is now a walker");
                    Assert.That(b.CanTakeTrait, Is.True, "a traited " + kind + " can no longer take a trait");
                }
            }
        }

        [Test]
        public void AWalkerIsNeverOfferedATrait()
        {
            foreach (BodyKind kind in new[] { BodyKind.Lunger, BodyKind.Brunch })
            {
                var b = Bodies.Spawn(kind, 20, new Rng(5u));
                Assert.That(Bodies.ApplyTrait(b, Trait.Hold), Is.EqualTo(Trait.None),
                    "a walker accepted a trait, and the only fields available to it are the ones " +
                    "that define what kind of body it is");
                Assert.That(Bodies.ApplyTrait(b, Trait.Close), Is.EqualTo(Trait.None));
            }
        }

        // ------------------------------------------------- the trait itself

        [Test]
        public void ATraitMovesTheBandAndKeepsItsWidth()
        {
            var rng = new Rng(7u);
            var plain = Bodies.Spawn(BodyKind.Shooter, 1, rng);
            double plainWidth = plain.Far!.Value - plain.Close!.Value;

            var held = Bodies.Spawn(BodyKind.Shooter, 1, rng);
            Bodies.ApplyTrait(held, Trait.Hold);
            Assert.That(held.Far, Is.GreaterThan(plain.Far!.Value), "HOLD did not move the body out");
            Assert.That(held.Close, Is.GreaterThan(plain.Close!.Value),
                "HOLD moved only Far, so the band WIDENED as a side effect. Both are shifted by the " +
                "same factor so the width is preserved and a body does not start dithering.");
            Assert.That((held.Far!.Value - held.Close!.Value), Is.GreaterThan(plainWidth));

            var closed = Bodies.Spawn(BodyKind.Shooter, 1, rng);
            Bodies.ApplyTrait(closed, Trait.Close);
            Assert.That(closed.Far, Is.LessThan(plain.Far!.Value), "CLOSE did not move the body in");
            Assert.That(closed.Close, Is.LessThan(plain.Close!.Value), "CLOSE moved only Far, so the band widened");
        }

        [Test]
        public void ATraitChangesNoCombatNumberAtAll()
        {
            // THE rule, measured. A trait may change where a fight happens and never how hard it is.
            var rng = new Rng(3u);
            var plain = Bodies.Spawn(BodyKind.Gunner, 20, rng);
            var traited = Bodies.Spawn(BodyKind.Gunner, 20, rng);
            Bodies.ApplyTrait(traited, Trait.Hold);

            Assert.That(traited.MaxHp, Is.EqualTo(plain.MaxHp));
            Assert.That(traited.Mass, Is.EqualTo(plain.Mass));
            Assert.That(traited.Radius, Is.EqualTo(plain.Radius));
            Assert.That(traited.Armour, Is.EqualTo(plain.Armour));
            Assert.That(traited.Dmg, Is.EqualTo(plain.Dmg));
            Assert.That(traited.Speed, Is.EqualTo(plain.Speed), "the trait changed the approach speed, " +
                "which is a difficulty change wearing a behavioural hat");
            Assert.That(traited.CdMin, Is.EqualTo(plain.CdMin), "the trait changed the cadence, which " +
                "is the same failure through a different number");
            Assert.That(traited.Sense, Is.EqualTo(plain.Sense));
            Assert.That(traited.Range, Is.EqualTo(plain.Range));
            Assert.That(traited.Far, Is.Not.EqualTo(plain.Far), "the trait did not do anything at all");
        }

        [Test]
        public void AShiftedBandStaysInsideTheClampsThatKeepTheBodyAbleToFight()
        {
            // far under sense, or a body notices you and then holds somewhere it can never shoot
            // from - which reads as broken rather than as an answer, and the player cannot act on it
            // because nothing is happening.
            var rng = new Rng(11u);
            for (int i = 0; i < 400; i++)
            {
                foreach (BodyKind kind in new[] { BodyKind.Shooter, BodyKind.Gunner })
                {
                    var b = Bodies.Spawn(kind, 30, rng);
                    Bodies.ApplyTrait(b, Bodies.TraitFor(kind, rng));
                    if (b.Trait == Trait.None) continue;
                    Assert.That(b.Far!.Value, Is.LessThan(b.Sense!.Value * Bodies.TraitFarCeil),
                        "a traited body holds beyond its own awareness");
                    Assert.That(b.Far - b.Close, Is.GreaterThanOrEqualTo(Bodies.TraitBandMin - 1e-9),
                        "the band collapsed, so the body dithers on the spot rather than standing somewhere");
                    Assert.That(b.Close, Is.GreaterThan(0));
                }
            }
        }

        [Test]
        public void TheStandoffIsDerivedRatherThanStored()
        {
            // A cached standoff is a second number that has to be kept in step with the two it comes
            // from. Measured against the JavaScript: at full room pressure a plain shooter holds at
            // 165px and at zero pressure at 250px.
            var rng = new Rng(1u);
            var b = Bodies.Spawn(BodyKind.Shooter, 1, rng);
            Assert.That(b.Standoff(1.0, 0.85), Is.EqualTo(165).Within(0.5),
                "full pressure: 250 - (250-150)*0.85");
            Assert.That(b.Standoff(0.0, 0.85), Is.EqualTo(250).Within(0.5),
                "an empty room: the body holds at its far threshold");
        }

        [Test]
        public void TheTraitRollUsesTheRunStreamSoItIsPartOfTheRun()
        {
            // A body's trait is what the run IS - same bodies, same answers, every replay - so it has
            // to come from the run stream. Its spawn JITTER (notice delay, first shot) must not, or a
            // cosmetic roll would shift the dungeon.
            var rng = new Rng(1u);
            Bodies.Spawn(BodyKind.Shooter, 1, rng);
            Assert.That(rng.JitterCalls, Is.GreaterThan(0), "a body drew no jitter at all");
            Assert.That(rng.RunCalls, Is.EqualTo(0), "spawning a body drew from the run stream, so a " +
                "cosmetic roll can shift the dungeon");
        }
    }
}
