using System.Collections.Generic;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// Parity for <c>TickOrder.AssemblePacks</c> - the first third of the body phase, and the part
    /// that decides what a Brunch IS rather than how it moves.
    ///
    /// <para>
    /// Every expectation was measured out of the running game by <c>tools/pack-parity.js</c>. The
    /// Brunch work this session produced - the bomb-rush, the shield, the commitment - lived as prose
    /// and tuning numbers until this file pinned it as behaviour.
    /// </para>
    ///
    /// <para>
    /// <b>The property worth most here is the last one.</b> A pack COMMITS to its target: the scan
    /// condition is <c>!shieldTarget &amp;&amp; (frameCount % BrunchScanTicks === 0)</c>, and the
    /// <c>!shieldTarget</c> guard means a pack with a living target never re-picks. Measured in the
    /// browser: a pack guarding a distant gunner did not switch to a shooter standing 20px from its
    /// centroid across 400 ticks. My probe expected a switch and got none, and the game was right -
    /// the probe was wrong about what the guard does.
    /// </para>
    ///
    /// <para>
    /// That is a deliberate design property rather than an oversight. Re-deciding every 20 ticks would
    /// make a Brunch's escort visibly indecisive in a room with several ranged bodies, and it would
    /// let the player pull a pack off its charge by walking a rival into range - which turns a threat
    /// into a switch.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class PackAssemblyParityTests
    {
        private static RunState Room()
        {
            var run = new RunState(new Rng(4242)) { dungeon = Dungeon.FromSeed(new Rng(4242)) };
            run.state = "playing";
            run.readyT = 0;
            run.fadeT = 0;
            run.enemies.Clear();
            run.pickups.Clear();
            run.projectiles.Clear();
            run.player.x = Balance.MidX; run.player.y = Balance.MidY;
            run.player.lagX = Balance.MidX; run.player.lagY = Balance.MidY;
            run.player.hp = 8; run.player.maxHp = 8;
            return run;
        }

        /// <summary>
        /// A ring of Brunch around a point, as the generator makes them. Positions do not matter to
        /// the pack pass - it reads only the centroid - but they are spread so that a test which
        /// accidentally measures a body's own position rather than the centroid's would notice.
        /// </summary>
        private static List<Enemy> Pack(RunState run, int n, double cx, double cy, int packId = 1)
        {
            var made = new List<Enemy>();
            for (int i = 0; i < n; i++)
            {
                double ang = (i / (double)n) * System.Math.PI * 2;
                var e = new Enemy
                {
                    kind = BodyKind.Brunch,
                    x = cx + System.Math.Cos(ang) * 22,
                    y = cy + System.Math.Sin(ang) * 22,
                    hp = 10, maxHp = 10,
                    packId = packId, packSlot = i,
                };
                run.enemies.Add(e);
                made.Add(e);
            }
            return made;
        }

        private static Enemy Ranged(RunState run, double x, double y, BodyKind kind)
        {
            var e = new Enemy { kind = kind, x = x, y = y, hp = 20, maxHp = 20 };
            run.enemies.Add(e);
            return e;
        }

        /// <summary>
        /// A pack with nothing to shield has NO target. Measured: a six-body pack in a room with no
        /// ranged body came out of the assembly pass with every <c>shieldTarget</c> null - which is
        /// what makes it walk at the player instead of assembling a wall.
        /// </summary>
        [Test]
        public void APackWithNoRangedBodyHasNoTarget()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            TickOrder.AssemblePacks(run);
            foreach (var e in pack)
                Assert.That(e.shieldTarget, Is.Null,
                    "a pack with nothing to shield walks at the player - giving it a target anyway "
                    + "is what makes it stand still");
        }

        /// <summary>
        /// A pack with a living target guards it, and the guarded body carries the WHOLE pack in its
        /// guard list. Measured: 6 of 6 guarding, and a guard list of 6.
        /// </summary>
        [Test]
        public void APackGuardsALivingTargetAndTheTargetCarriesTheWholePack()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            var shooter = Ranged(run, 200, Balance.MidY, BodyKind.Shooter);
            foreach (var e in pack) e.shieldTarget = shooter;

            TickOrder.AssemblePacks(run);

            foreach (var e in pack)
                Assert.That(e.shieldTarget, Is.SameAs(shooter));
            Assert.That(shooter.shieldGuardFor, Is.Not.Null);
            Assert.That(shooter.shieldGuardFor!.Count, Is.EqualTo(6),
                "the guard list is what changes the guarded body's standoff, so it has to be "
                +"complete or a partly-guarded body reads as unguarded");
        }

        /// <summary>
        /// The target dies, the pack stops guarding the corpse, and the bodies move. Measured: 6 of 6
        /// guarding before, 0 of 6 after, and the pack had moved more than 2px in 40 ticks.
        /// </summary>
        [Test]
        public void WhenTheGuardedTargetDiesThePackStopsGuardingIt()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            var shooter = Ranged(run, 200, Balance.MidY, BodyKind.Shooter);
            foreach (var e in pack) e.shieldTarget = shooter;
            TickOrder.AssemblePacks(run);
            Assert.That(pack.FindAll(e => e.shieldTarget == shooter).Count, Is.EqualTo(6));

            shooter.hp = 0;
            TickOrder.AssemblePacks(run);

            Assert.That(pack.FindAll(e => e.shieldTarget == shooter).Count, Is.EqualTo(0),
                "a pack does not shield a corpse");
        }

        /// <summary>
        /// A pack that has NO target re-picks on the scan cadence, and picks the NEAREST ranged body.
        /// Measured: with the targets cleared and a shooter 20px from the centroid and a gunner 260px
        /// away, all six switched to the shooter on the very first tick.
        /// </summary>
        [Test]
        public void AnUntargetedPackPicksTheNearestRangedBody()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            var near = Ranged(run, 280, Balance.MidY, BodyKind.Shooter);      // 20px from the centroid
            Ranged(run, 520, Balance.MidY, BodyKind.Gunner);                  // 260px away
            run.frameCount = 0;

            TickOrder.AssemblePacks(run);

            foreach (var e in pack)
                Assert.That(e.shieldTarget, Is.SameAs(near),
                    "a pack shields what is closest, because that is what it can reach");
        }

        /// <summary>
        /// <b>AND IT COMMITS.</b> A pack with a living target does NOT re-pick, however near a rival
        /// walks in. Measured: a pack guarding a gunner 260px away did not switch to a shooter 20px
        /// from its centroid across 400 frames.
        /// <para>
        /// This is the assertion that would catch a "helpful" future change to the scan condition.
        /// Removing the <c>!shieldTarget</c> guard makes a Brunch's escort indecisive in a room with
        /// several ranged bodies, and lets a player pull a pack off its charge by walking a rival into
        /// range.
        /// </para>
        /// </summary>
        [Test]
        public void APackWithALivingTargetNeverRepicks()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            var far = Ranged(run, 520, Balance.MidY, BodyKind.Gunner);
            var near = Ranged(run, 280, Balance.MidY, BodyKind.Shooter);
            foreach (var e in pack) e.shieldTarget = far;

            // 400 frames - twenty scan periods - with the near one standing right there.
            for (int t = 0; t < 400; t++) { run.frameCount = t; TickOrder.AssemblePacks(run); }

            foreach (var e in pack)
                Assert.That(e.shieldTarget, Is.SameAs(far),
                    "a pack commits: it re-targets when its target dies or leaves, and not before");
            Assert.That(near.shieldGuardFor, Is.Null,
                "and the body it did NOT pick up is not being escorted");
        }

        /// <summary>
        /// A target that is no longer in the room counts as gone. The staleness test checks
        /// membership, not just health, because a target removed by the room's own bookkeeping is
        /// alive and absent - and a pack that kept guarding it would hold a wall around nothing.
        /// </summary>
        [Test]
        public void ATargetThatLeavesTheRoomCountsAsGone()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            var shooter = Ranged(run, 200, Balance.MidY, BodyKind.Shooter);
            foreach (var e in pack) e.shieldTarget = shooter;
            TickOrder.AssemblePacks(run);

            run.enemies.Remove(shooter);      // alive, but spliced out of the room
            TickOrder.AssemblePacks(run);

            foreach (var e in pack)
                Assert.That(e.shieldTarget, Is.Not.SameAs(shooter),
                    "a pack must not hold a wall around a body that is no longer in the room");
        }

        /// <summary>The guard lists are rebuilt, not accumulated.</summary>
        [Test]
        public void TheGuardListsAreRebuildedEveryTickNotAccumulated()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            var shooter = Ranged(run, 200, Balance.MidY, BodyKind.Shooter);
            foreach (var e in pack) e.shieldTarget = shooter;

            for (int t = 0; t < 10; t++) TickOrder.AssemblePacks(run);

            Assert.That(shooter.shieldGuardFor!.Count, Is.EqualTo(6),
                "ten ticks of assembling must leave six guards, not sixty");
        }

        /// <summary>A body guarded last tick and not this one carries no stale list.</summary>
        [Test]
        public void ABodyThatStopsBeingGuardedLosesItsGuardList()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            var shooter = Ranged(run, 200, Balance.MidY, BodyKind.Shooter);
            var gunner = Ranged(run, 520, Balance.MidY, BodyKind.Gunner);
            foreach (var e in pack) e.shieldTarget = shooter;
            TickOrder.AssemblePacks(run);
            Assert.That(shooter.shieldGuardFor, Is.Not.Null);

            foreach (var e in pack) e.shieldTarget = gunner;
            TickOrder.AssemblePacks(run);

            Assert.That(shooter.shieldGuardFor, Is.Null,
                "a body that is no longer escorted reads as unguarded, or it keeps a standoff bonus "
                + "it has not earned");
            Assert.That(gunner.shieldGuardFor!.Count, Is.EqualTo(6));
        }

        /// <summary>Only the three ranged kinds are worth shielding.</summary>
        [TestCase(BodyKind.Shooter, true)]
        [TestCase(BodyKind.Gunner, true)]
        [TestCase(BodyKind.Boss, true)]
        [TestCase(BodyKind.Lunger, false)]
        [TestCase(BodyKind.Brunch, false)]
        public void OnlyRangedKindsAreWorthShielding(BodyKind kind, bool expected)
        {
            Assert.That(TickOrder.IsRanged(kind), Is.EqualTo(expected));
        }

        [Test]
        public void AMeleeBodyIsNeverPickedAsAShieldTarget()
        {
            var run = Room();
            var pack = Pack(run, 6, 260, Balance.MidY);
            Ranged(run, 280, Balance.MidY, BodyKind.Lunger);      // nearer than any ranged body
            run.frameCount = 0;

            TickOrder.AssemblePacks(run);

            foreach (var e in pack)
                Assert.That(e.shieldTarget, Is.Null,
                    "nothing can reach a melee body from outside the pack, so shielding it is wasted");
        }

        /// <summary>
        /// Two packs in one room keep SEPARATE centroids and separate targets. Measured as a
        /// structural property rather than a number: a shared dictionary keyed by pack id means one
        /// pack's centroid cannot leak into another's.
        /// </summary>
        [Test]
        public void TwoPacksKeepSeparateCentroidsAndTargets()
        {
            var run = Room();
            var north = Pack(run, 4, 300, 200, packId: 1);
            var south = Pack(run, 4, 300, 500, packId: 2);
            var shooterN = Ranged(run, 320, 200, BodyKind.Shooter);
            var gunnerS = Ranged(run, 320, 500, BodyKind.Gunner);
            run.frameCount = 0;

            TickOrder.AssemblePacks(run);

            foreach (var e in north) Assert.That(e.shieldTarget, Is.SameAs(shooterN));
            foreach (var e in south) Assert.That(e.shieldTarget, Is.SameAs(gunnerS));
        }

        /// <summary>
        /// A pack of one is below <c>BrunchShieldMin</c>, so it has no business forming an arc - which
        /// is checked in the movement pass rather than here. What this asserts is the threshold
        /// itself, because it is the line between "shield" and "bomb rush" and both halves of the
        /// Brunch design hang off it.
        /// </summary>
        [Test]
        public void TheShieldAndWallThresholdsAreOrderedTheWayTheDesignNeeds()
        {
            Assert.That(Balance.BrunchShieldMin, Is.EqualTo(2));
            Assert.That(Balance.BrunchWallMin, Is.EqualTo(3));
            Assert.That(Balance.BrunchShieldMin, Is.LessThan(Balance.BrunchWallMin),
                "a pack of two guards and a pack of three walls; if these crossed, a two-body pack "
                +"would build a wall around a position nobody chose");
        }
    }
}
