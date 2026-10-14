using System.Collections.Generic;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The four helpers the projectile pass calls: falloffMult, alertEnemy, slowEnemy and killEnemy. [h:CombatHelperParityTests-1]</summary>
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
            // Both of these are the same shape of mistake: `fFar <= fNear` makes the denominator zero or negative, and a... [h:CombatHelperParityTests-2]
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
            var e = new Enemy { aggroTimer = 5, noticeTimer = 40 };
            Combat.AlertEnemy(e);
            Assert.Multiple(() =>
            {
                Assert.That(e.aggroTimer, Is.EqualTo(525),
                    "AGGRO_TIME is sec(2.5) at TICK_HZ=210 - a body that has seen you gets the full window");
                Assert.That(e.noticeTimer, Is.Zero,
                    "noticeTimer is the 'has it seen anything yet' dial and is reset by noticing");
                Assert.That(e.alerted, Is.True);
            });

            // and it only ever RAISES. A Max and not an assignment: this is the whole reason a fleeing
            // player cannot shake a gunner by breaking line of sight for a tick.
            var committed = new Enemy { aggroTimer = 99999 };
            Combat.AlertEnemy(committed);
            Assert.That(committed.aggroTimer, Is.EqualTo(99999),
                "alerting a body that is already fully committed must not shorten its commitment - that "
                + "would make a shell landing every so often reset the aggro clock rather than extend it");

            // and a body with no aggro timer at all does not gain one
            var noAggro = new Enemy { aggroTimer = null };
            Combat.AlertEnemy(noAggro);
            Assert.That(noAggro.aggroTimer, Is.Null,
                "the original guards on `e.aggroTimer!==undefined`; treating a missing timer as zero "
                + "would hand a body that had no notion of aggro a full AGGRO_TIME window");
        }

        [Test]
        public void AShellSlowsABodyAndInterruptsItsApproachRamp()
        {
            var e = new Enemy { slowT = 0, pursuit = 77 };
            Combat.SlowEnemy(e);
            Assert.Multiple(() =>
            {
                Assert.That(e.slowT, Is.EqualTo(116),
                    "HIT_SLOW_TICKS is sec(0.55) at TICK_HZ=210");
                Assert.That(e.pursuit, Is.Zero,
                    "the second half, and the one that is easy to drop: `pursuit` is the commitment "
                    + "ramp, so being hit starts the approach over rather than merely slowing it. A "
                    + "slowed Brunch that resumed at its built-up speed would not be what the game does");
            });
        }

        [Test]
        public void KillingABodyRemovesItAndCountsTheKill()
        {
            var run = new RunState(new Rng(1));
            run.enemies.Add(new Enemy { x = 100, y = 200 });
            run.enemies.Add(new Enemy { x = 300, y = 400 });
            var bodies = run.enemies;
            run.kills = 7;

            Kills.KillEnemy(run, 0);

            Assert.Multiple(() =>
            {
                Assert.That(bodies, Has.Count.EqualTo(1));
                Assert.That(bodies[0].x, Is.EqualTo(300),
                    "the splice is by INDEX, so a body removed from the middle must leave the right one");
                Assert.That(run.kills, Is.EqualTo(8));
            });

            // out of range throws rather than silently removing the wrong body. The original would throw
            // on `r.enemies[j]` being undefined too, so this is parity rather than hardening.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => Kills.KillEnemy(run, 5));
        }

    }
}