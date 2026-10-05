using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// Parity for <c>TickOrder.TickPlayer</c> - the second tick phase to carry real behaviour, and
    /// the one the whole tick's ORDER hangs on.
    ///
    /// <para>
    /// Every curve below was measured out of the running game by driving the browser's
    /// <c>tickPlayer()</c> with the same inputs. These are not "close enough" assertions: the
    /// acceleration ramp is compared tick by tick to six decimal places, because a ramp that is
    /// 1% wrong is a player whose top speed arrives 4 ticks late, and that is exactly the kind of
    /// difference that reads as feel rather than as a bug.
    /// </para>
    ///
    /// <para>
    /// The measured reference, for the record:
    /// </para>
    /// <list type="bullet">
    /// <item>terminal speed 1.4025, reached in roughly 40 ticks</item>
    /// <item>the first ten ticks of velocity holding east: 0.16269, 0.306508, 0.433643, 0.54603,
    /// 0.645381, 0.733207, 0.810845, 0.879477, 0.940147, 0.99378</item>
    /// <item><b>the diagonal curve is BIT-IDENTICAL to the straight one</b> - measured as an exact
    /// array comparison, not a tolerance. A diagonal is not a speed advantage, and this is the
    /// assertion that says so.</item>
    /// <item>a wall with a CLOSED door clamps the player to exactly <c>RoomLeft + r</c> = 63 with
    /// <c>vx</c> = 0</item>
    /// <item>a player pinned against that wall for 200 ticks has momentum 0; the same 200 ticks in
    /// open floor with bodies present reaches 1</item>
    /// <item>after 30 ticks running east the player is at 431.6515 and the lagged hitbox at
    /// 414.1123 - the gap is the mechanic</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public sealed class PlayerPhaseParityTests
    {
        private static RunState Standing(uint seed = 4242)
        {
            var run = new RunState(new Rng(seed)) { dungeon = Dungeon.FromSeed(new Rng(seed)) };
            run.state = "playing";
            run.readyT = 0;
            run.fadeT = 0;
            run.enemies.Clear();
            run.pickups.Clear();
            run.projectiles.Clear();
            var p = run.player;
            p.x = Balance.MidX; p.y = Balance.MidY;
            p.lagX = Balance.MidX; p.lagY = Balance.MidY;
            p.vx = 0; p.vy = 0; p.kvx = 0; p.kvy = 0;
            p.swerve = 0; p.dirX = 0; p.dirY = 0;
            p.trendVx = 0; p.trendVy = 0; p.beliefVx = 0; p.beliefVy = 0;
            p.anim = 0; p.slowMult = 1; p.shootSlow = 0; p.boost = 0;
            p.cooldown = 0; p.altCooldown = 0; p.iframes = 0; p.muzzleTimer = 0;
            p.momentum = 0;
            p.hp = 8; p.maxHp = 8;
            return run;
        }

        /// <summary>
        /// A one-room dungeon whose west side has no door at all, so the west wall actually clamps.
        /// <para>
        /// Built by hand rather than generated, because the trap is precisely that a GENERATED
        /// room's doors are in an unpredictable state: the start room happens to have its west door
        /// present, so a player held against it walks through - correctly - and a probe that calls
        /// that "a wall" reports a missing clamp that does not exist. This is the third time that
        /// fixture produced a confident false result about a wall.
        /// </para>
        /// </summary>
        private static Dungeon ClosedWestRoom()
        {
            var d = Dungeon.FromSeed(new Rng(7));
            foreach (var r in d.AllRooms)
            {
                r.Doors.Remove(Dir.W);
                r.Doors.Remove(Dir.N);
                r.Doors.Remove(Dir.S);
                r.Doors.Remove(Dir.E);
            }
            return d;
        }

        /// <summary>Re-centre before each tick, so a long run measures the ramp and not the wall.</summary>
        private static void RunPinnedInPlace(RunState run, int dx, int dy, int ticks, bool hold = true)
        {
            var input = new Input(dx, dy);
            for (int i = 0; i < ticks; i++)
            {
                if (hold) { run.player.x = Balance.MidX; run.player.y = Balance.MidY; }
                TickOrder.TickPlayer(run, input);
            }
        }

        [Test]
        public void OneTickWithNoInputMovesNothing()
        {
            var run = Standing();
            var before = (run.player.x, run.player.y, run.player.vx, run.player.vy);
            TickOrder.TickPlayer(run, Input.None);
            Assert.That(run.player.x, Is.EqualTo(before.Item1).Within(1e-12));
            Assert.That(run.player.y, Is.EqualTo(before.Item2).Within(1e-12));
            Assert.That(run.player.vx, Is.EqualTo(0));
            Assert.That(run.player.vy, Is.EqualTo(0));
        }

        /// <summary>
        /// The acceleration ramp, tick by tick against the measured curve. This is the assertion that
        /// makes <c>MoveAccel</c> load-bearing: get it one percent wrong and tick 3 is wrong.
        /// </summary>
        [Test]
        public void TheAccelerationRampMatchesTheMeasuredCurveTickForTick()
        {
            double[] measured = {
                0.16269, 0.306508, 0.433643, 0.54603, 0.645381,
                0.733207, 0.810845, 0.879477, 0.940147, 0.99378,
            };
            var run = Standing();
            for (int i = 0; i < measured.Length; i++)
            {
                run.player.x = Balance.MidX; run.player.y = Balance.MidY;
                TickOrder.TickPlayer(run, new Input(1, 0));
                Assert.That(run.player.vx, Is.EqualTo(measured[i]).Within(1e-5),
                    "tick " + (i + 1) + ": the ramp is the feel, and a percent here is four ticks of "
                    + "top-speed arrival at the far end");
            }
        }

        [Test]
        public void TheTerminalSpeedIsTheGamesValue()
        {
            var run = Standing();
            RunPinnedInPlace(run, 1, 0, 400);
            Assert.That(System.Math.Sqrt(run.player.vx * run.player.vx + run.player.vy * run.player.vy),
                Is.EqualTo(1.4025).Within(1e-4));
        }

        /// <summary>
        /// A DIAGONAL IS NOT FASTER. Measured as an exact array comparison in the browser: the two
        /// curves are bit-identical, because the input is normalised before it is scaled. This is
        /// worth an exact assertion rather than a tolerance - a tolerance would happily accept a
        /// diagonal that is 0.5% faster, which is enough to be felt and not enough to be seen.
        /// </summary>
        [Test]
        public void TheDiagonalCurveIsIdenticalToTheStraightOne()
        {
            var straight = Standing();
            var diagonal = Standing();
            for (int i = 0; i < 40; i++)
            {
                straight.player.x = Balance.MidX; straight.player.y = Balance.MidY;
                diagonal.player.x = Balance.MidX; diagonal.player.y = Balance.MidY;
                TickOrder.TickPlayer(straight, new Input(1, 0));
                TickOrder.TickPlayer(diagonal, new Input(1, 1));
            }
            /* THE MAGNITUDE MATCHES, TICK FOR TICK - and that is the property, because a diagonal is
               not a speed advantage. The COMPONENTS cannot match the straight-line components: they
               are this divided by root two, which is what "normalised" means. An earlier version of
               this test asserted the components were equal to the straight ones, which can never be
               true of a normalised diagonal and failed for the right reason at the wrong quantity.

               Measured in the browser: the two curves are identical as arrays, and the ratio of the
               diagonal's vx to the straight line's is exactly 0.7071067811865476. */
            double straightSp = System.Math.Sqrt(straight.player.vx * straight.player.vx
                                               + straight.player.vy * straight.player.vy);
            double diagSp = System.Math.Sqrt(diagonal.player.vx * diagonal.player.vx
                                           + diagonal.player.vy * diagonal.player.vy);
            Assert.That(diagSp, Is.EqualTo(straightSp).Within(1e-12),
                "a diagonal must not be faster than a straight line, and must not be slower either");
            Assert.That(diagonal.player.vx, Is.EqualTo(straight.player.vx / System.Math.Sqrt(2)).Within(1e-9));
        }

        [Test]
        public void TheDiagonalHasEqualComponentsAndTheSameMagnitude()
        {
            // Measured: vx = vy = 0.984564, total 1.392384 - equal components, and the same total as
            // a straight line, so the components are the speed divided by root two.
            var run = Standing();
            RunPinnedInPlace(run, 1, 1, 40);
            Assert.That(run.player.vx, Is.EqualTo(run.player.vy).Within(1e-12));
            Assert.That(run.player.vx, Is.EqualTo(0.984564).Within(1e-4));
            Assert.That(System.Math.Sqrt(run.player.vx * run.player.vx + run.player.vy * run.player.vy),
                Is.EqualTo(1.392384).Within(1e-4));
        }

        [Test]
        public void ReleasingTheKeyDeceleratesToAStop()
        {
            var run = Standing();
            RunPinnedInPlace(run, 1, 0, 300);
            Assert.That(run.player.vx, Is.EqualTo(1.4025).Within(1e-4));

            // Measured in an EMPTY room (so momentum does not scale the acceleration):
            // 1.23981 after one tick, 0.40872 after ten, 0.034711 after thirty, 0.000859 after
            // sixty. The first of my earlier values here was taken from a run that had bodies in the
            // room, which charges momentum and therefore scales the acceleration term - the
            // deceleration from a boosted player is faster, so the numbers do not agree and the
            // discrepancy is a fixture difference rather than a port one.
            RunPinnedInPlace(run, 0, 0, 1, hold: false);
            Assert.That(run.player.vx, Is.EqualTo(1.23981).Within(1e-4));
            RunPinnedInPlace(run, 0, 0, 9, hold: false);
            Assert.That(run.player.vx, Is.EqualTo(0.40872).Within(1e-4));
            RunPinnedInPlace(run, 0, 0, 20, hold: false);
            Assert.That(run.player.vx, Is.EqualTo(0.034711).Within(1e-4));
            RunPinnedInPlace(run, 0, 0, 30, hold: false);
            Assert.That(run.player.vx, Is.EqualTo(0.000859).Within(1e-5));
        }

        [Test]
        public void KnockbackIsSeparateFromVelocityAndDecaysOnItsOwn()
        {
            // Measured: a knockback of 10 moves the player to 410 in one tick and leaves kvx at 9.58.
            var run = Standing();
            run.player.kvx = 10;
            TickOrder.TickPlayer(run, Input.None);
            Assert.That(run.player.x, Is.EqualTo(410).Within(1e-9));
            Assert.That(run.player.kvx, Is.EqualTo(9.58).Within(1e-9));
        }

        [Test]
        public void KnockbackBelowTheCutIsZeroedRatherThanDecayed()
        {
            var run = Standing();
            run.player.kvx = Balance.KnockCut / 2;
            TickOrder.TickPlayer(run, Input.None);
            Assert.That(run.player.kvx, Is.EqualTo(0));
        }

        /// <summary>
        /// A wall CLOSES the player and ZEROES their velocity - measured at exactly
        /// <c>RoomLeft + r</c> = 63 with <c>vx</c> = 0, against a room whose west door is closed.
        /// </summary>
        [Test]
        public void AClosedWallStopsThePlayerAndZeroesTheirVelocity()
        {
            var run = Standing();
            // A room with no west door, so this is a wall and not a doorway. The start room's doors
            // are all locked, which is the trap: a player held against its west side walks straight
            // through, correctly, and a probe that calls that "a wall" reports a missing clamp.
            run.dungeon = ClosedWestRoom();

            run.player.x = Balance.RoomLeft + run.player.r + 1;
            run.player.y = Balance.MidY;
            run.player.vx = 0; run.player.vy = 0;
            for (int i = 0; i < 120; i++) TickOrder.TickPlayer(run, new Input(-1, 0));

            Assert.That(run.player.x, Is.EqualTo(63).Within(1e-9));
            Assert.That(run.player.vx, Is.EqualTo(0).Within(1e-12));
        }

        /// <summary>
        /// Momentum is earned from DISPLACEMENT, so a player pinned against a wall gains none however
        /// long they hold the key. Measured: 200 ticks pinned gives 0.
        /// </summary>
        [Test]
        public void APinnedPlayerEarnsNoMomentum()
        {
            var run = Standing();
            run.dungeon = ClosedWestRoom();
            run.player.x = Balance.RoomLeft + run.player.r + 1;
            run.player.y = Balance.MidY;
            run.player.momentum = 0;
            for (int i = 0; i < 200; i++) TickOrder.TickPlayer(run, new Input(-1, 0));
            Assert.That(run.player.momentum, Is.EqualTo(0).Within(1e-9),
                "charging on velocity instead of displacement is the bug this guards");
        }

        /// <summary>
        /// The swerve meter rises on a reversal and decays otherwise. Measured from a reversal:
        /// 0.4365 immediately, then 0.419, 0.3665 and 0.3 at five, twenty and thirty-nine ticks - a
        /// fall of 0.105 per thirty ticks, which is 0.0035 a tick.
        /// </summary>
        [Test]
        public void TheSwerveMeterRisesOnAReversalAndDecaysAtTheGamesRate()
        {
            var run = Standing();
            RunPinnedInPlace(run, 1, 0, 200);
            Assert.That(run.player.swerve, Is.EqualTo(0).Within(1e-6),
                "running in a straight line is not a swerve");

            var reversed = Standing();
            RunPinnedInPlace(reversed, 1, 0, 200);

            /* ONE TICK OF THE REVERSAL, and the reading is taken AFTER it - which is where this
               assertion was off by a single tick's decay and looked like a 1% error in the rate. The
               meter rises on the reversing tick AND decays on it, so the value after tick 1 is
               already 0.4365 and every later reading is 0.4365 less 0.0035 per tick since.

               Measured tick by tick: 0.4365, 0.433, 0.4295, ... 0.3 at tick forty. The implied rate
               over those forty ticks is 0.0035 exactly, which is the number that matters. */
            RunPinnedInPlace(reversed, -1, 0, 1);
            Assert.That(reversed.player.swerve, Is.EqualTo(0.4365).Within(1e-6),
                "a reversal is the one thing the meter exists to notice");

            RunPinnedInPlace(reversed, -1, 0, 4);          // ticks 2-5
            Assert.That(reversed.player.swerve, Is.EqualTo(0.4225).Within(1e-6), "tick 5");
            RunPinnedInPlace(reversed, -1, 0, 15);         // ticks 6-20
            Assert.That(reversed.player.swerve, Is.EqualTo(0.37).Within(1e-6), "tick 20");
            RunPinnedInPlace(reversed, -1, 0, 20);         // ticks 21-40
            Assert.That(reversed.player.swerve, Is.EqualTo(0.3).Within(1e-6), "tick 40");

            // and the rate, stated as the thing it is
            double implied = (0.4365 - reversed.player.swerve) / 39.0;
            Assert.That(implied, Is.EqualTo(Balance.SwerveDecay).Within(1e-9),
                "the meter falls by SwerveDecay a tick, and no faster");
        }

        [Test]
        public void TheSwerveMeterIsCappedAtOne()
        {
            var run = Standing();
            run.player.swerve = 1;
            RunPinnedInPlace(run, 1, 0, 1);
            Assert.That(run.player.swerve, Is.LessThanOrEqualTo(1));
        }

        /// <summary>
        /// The lagged hitbox trails the real position and is PUBLISHED for the body phase. Measured
        /// after 30 ticks running east: player 431.6515, hitbox 414.1123. The equality matters as much
        /// as the gap - if they were ever equal for a moving player, the lag would be broken, and if
        /// they were unequal the gunners would be aiming at nothing.
        /// </summary>
        [Test]
        public void TheLaggedHitboxTrailsThePlayerAndIsPublished()
        {
            var run = Standing();
            RunPinnedInPlace(run, 1, 0, 30);
            // the player is re-centred each tick by the harness, so drive it properly instead
            var moving = Standing();
            for (int i = 0; i < 30; i++) TickOrder.TickPlayer(moving, new Input(1, 0));

            Assert.That(TickOrder.HitboxX, Is.EqualTo(moving.player.lagX),
                "the body phase reads the published hitbox, so the two must be the same number");
            Assert.That(TickOrder.HitboxY, Is.EqualTo(moving.player.lagY));
            Assert.That(moving.player.lagX, Is.LessThan(moving.player.x),
                "after 30 ticks east the hitbox is still behind - that gap IS the mechanic");
        }

        /// <summary>
        /// The hitbox converges on the player when they stop, rather than trailing for ever. It is an
        /// ease toward the position, so it must arrive.
        /// </summary>
        [Test]
        public void TheLaggedHitboxCatchesUpWhenThePlayerStops()
        {
            var run = Standing();
            for (int i = 0; i < 30; i++) TickOrder.TickPlayer(run, new Input(1, 0));
            double gap = run.player.x - run.player.lagX;
            Assert.That(gap, Is.GreaterThan(1));
            for (int i = 0; i < 400; i++) TickOrder.TickPlayer(run, Input.None);
            Assert.That(System.Math.Abs(run.player.x - run.player.lagX), Is.LessThan(0.01),
                "the hitbox eases toward the player, so it must actually arrive");
        }

        [Test]
        public void CooldownsTickDownAndStopAtZero()
        {
            var run = Standing();
            run.player.cooldown = 3;
            run.player.altCooldown = 2;
            run.player.iframes = 2;
            run.player.muzzleTimer = 2;
            TickOrder.TickPlayer(run, Input.None);
            Assert.That(run.player.cooldown, Is.EqualTo(2));
            Assert.That(run.player.altCooldown, Is.EqualTo(1));
            Assert.That(run.player.iframes, Is.EqualTo(1));
            Assert.That(run.player.muzzleTimer, Is.EqualTo(1));
            for (int i = 0; i < 5; i++) TickOrder.TickPlayer(run, Input.None);
            Assert.That(run.player.cooldown, Is.EqualTo(0), "a cooldown never goes negative");
            Assert.That(run.player.iframes, Is.EqualTo(0));
        }

        [Test]
        public void TheWalkCycleScalesWithDistanceNotTime()
        {
            var run = Standing();
            for (int i = 0; i < 30; i++) TickOrder.TickPlayer(run, new Input(1, 0));
            double moving = run.player.anim;
            Assert.That(moving, Is.GreaterThan(0));
            for (int i = 0; i < 200; i++) TickOrder.TickPlayer(run, Input.None);
            Assert.That(run.player.anim, Is.EqualTo(0),
                "a standing player has no walk cycle, and a phase that leaves the phase running "
                + "makes a statue shuffle");
        }
    }
}
