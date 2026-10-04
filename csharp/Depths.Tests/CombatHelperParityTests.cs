using System.Collections.Generic;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The four helpers the projectile pass calls: <c>falloffMult</c>, <c>alertEnemy</c>,
    /// <c>slowEnemy</c> and <c>killEnemy</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every expected value here was READ OUT OF THE RUNNING JAVASCRIPT, never computed. That is the
    /// only legitimate way to acquire a parity value, and it is not a formality: hand-computing one
    /// produced three wrong numbers out of nine once already, and the test was what failed each time.
    /// The read was done by evaluating the real functions in a browser at TICK_HZ=210.
    /// </para>
    /// <para>
    /// The degenerate cases are here because they are where a transcription breaks. A zero-width falloff
    /// band and a NEGATIVE one both have to land on the floor rather than dividing by zero or inverting
    /// the ramp into a damage bonus — and the game's answer to both is the same number, which is not
    /// obvious from reading <c>Math.max(1, fFar-fNear)</c> and is obvious from measuring it.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class CombatHelperParityTests
    {
        private static Projectile Shell(double travelled, double? fNear = null,
                                        double? fMin = null, double? fFar = null)
            => new Projectile { x = travelled, y = 0, ox = 0, oy = 0 };

        [Test]
        public void AShellWithNoFalloffBandDealsFullDamage()
        {
            // src/40-combat.js:134 - `if(p.fNear===undefined) return 1;`
            Assert.That(Combat.FalloffMult(Shell(0)), Is.EqualTo(1.0));
            Assert.That(Combat.FalloffMult(Shell(9999)), Is.EqualTo(1.0),
                "the distance is irrelevant when there is no band at all, so a shell that ignores "
                + "falloff cannot be made to lose damage by travelling further");
        }

        // band 100..300 with a floor of 0.4, so full damage inside 100 and 40% at 300
        [TestCase(0, 1.0)]
        [TestCase(50, 1.0)]
        [TestCase(100, 1.0)]
        [TestCase(150, 0.85)]
        [TestCase(200, 0.7)]
        [TestCase(250, 0.55)]
        [TestCase(300, 0.4)]
        [TestCase(400, 0.4)]
        public void FalloffIsLinearFromTheNearEdgeToTheFarOne(int travelled, double expected)
        {
            Assert.That(Combat.FalloffMult(Shell(travelled), 100, 0.4, 300),
                Is.EqualTo(expected).Within(1e-12),
                $"at {travelled}px from the muzzle, read from the browser");
        }

        [Test]
        public void ADegenerateBandLandsOnTheFloorRatherThanDividingByZeroOrPayingMore()
        {
            // Both of these are the same shape of mistake: `fFar <= fNear` makes the denominator zero
            // or negative, and a naive transcription produces a division by zero, a NaN, or a ramp that
            // goes UP with distance — which would be a damage bonus for shooting from further away.
            Assert.Multiple(() =>
            {
                Assert.That(Combat.FalloffMult(Shell(150), 100, 0.4, 100), Is.EqualTo(0.4),
                    "a zero-width band: the max(1, ...) guard makes it the floor, not a division");
                Assert.That(Combat.FalloffMult(Shell(150), 100, 0.4, 50), Is.EqualTo(0.4),
                    "a band whose far edge is BEFORE its near edge must not invert into a bonus");
                Assert.That(Combat.FalloffMult(Shell(150), 100, 0.4, 50), Is.LessThan(1.0),
                    "further is never better, whatever the band says");
            });
        }

        [Test]
        public void AlertingABodyRaisesItsAggroTimerAndNeverLowersIt()
        {
            var e = new Body { AggroTimer = 5, NoticeTimer = 40 };
            Combat.AlertEnemy(e);
            Assert.Multiple(() =>
            {
                Assert.That(e.AggroTimer, Is.EqualTo(525),
                    "AGGRO_TIME is sec(2.5) at TICK_HZ=210 - a body that has seen you gets the full window");
                Assert.That(e.NoticeTimer, Is.Zero,
                    "noticeTimer is the 'has it seen anything yet' dial and is reset by noticing");
                Assert.That(e.Alerted, Is.True);
            });

            // and it only ever RAISES. A Max and not an assignment: this is the whole reason a fleeing
            // player cannot shake a gunner by breaking line of sight for a tick.
            var committed = new Body { AggroTimer = 99999 };
            Combat.AlertEnemy(committed);
            Assert.That(committed.AggroTimer, Is.EqualTo(99999),
                "alerting a body that is already fully committed must not shorten its commitment - that "
                + "would make a shell landing every so often reset the aggro clock rather than extend it");

            // and a body with no aggro timer at all does not gain one
            var noAggro = new Body { AggroTimer = 0 };
            Combat.AlertEnemy(noAggro, hasAggro: false);
            Assert.That(noAggro.AggroTimer, Is.Zero,
                "the original guards on `e.aggroTimer!==undefined`; treating a missing timer as zero "
                + "would hand a body that had no notion of aggro a full AGGRO_TIME window");
        }

        [Test]
        public void AShellSlowsABodyAndInterruptsItsApproachRamp()
        {
            var e = new Body { SlowT = 0, Pursuit = 77 };
            Combat.SlowEnemy(e);
            Assert.Multiple(() =>
            {
                Assert.That(e.SlowT, Is.EqualTo(116),
                    "HIT_SLOW_TICKS is sec(0.55) at TICK_HZ=210");
                Assert.That(e.Pursuit, Is.Zero,
                    "the second half, and the one that is easy to drop: `pursuit` is the commitment "
                    + "ramp, so being hit starts the approach over rather than merely slowing it. A "
                    + "slowed Brunch that resumed at its built-up speed would not be what the game does");
            });
        }

        [Test]
        public void KillingABodyRemovesItAndCountsTheKill()
        {
            var run = new RunState(new Rng(1));
            var bodies = new List<Body>
            {
                new Body { X = 100, Y = 200 },
                new Body { X = 300, Y = 400 },
            };
            run.kills = 7;

            Kills.KillEnemy(run, bodies, 0);

            Assert.Multiple(() =>
            {
                Assert.That(bodies, Has.Count.EqualTo(1));
                Assert.That(bodies[0].X, Is.EqualTo(300),
                    "the splice is by INDEX, so a body removed from the middle must leave the right one");
                Assert.That(run.kills, Is.EqualTo(8));
            });

            // out of range throws rather than silently removing the wrong body. The original would throw
            // on `r.enemies[j]` being undefined too, so this is parity rather than hardening.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => Kills.KillEnemy(run, bodies, 5));
        }

        [Test]
        public void TheDropIsTheOneDeliberatelyIncompletePiece()
        {
            // Asserted rather than left implicit: a partial function is the shape that gets mistaken for
            // a complete one, so the fact that killEnemy drops nothing today is stated as a fact that
            // will FAIL when the loot table lands and someone forgets this test.
            Assert.That(Kills.DropsLoot, Is.False,
                "the loot table is not ported. When it is, this goes true and the drop must draw from "
                + "run.rng.run in the same place the original does - a drop that spent a draw would "
                + "move every spawn-plan number, and those are asserted");
        }
    }
}