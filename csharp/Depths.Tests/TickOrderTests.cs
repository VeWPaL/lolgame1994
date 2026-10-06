using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The tick skeleton: gates, clocks, the order gate, and RNG discipline.
    ///
    /// <para>
    /// These pin the shape of <see cref="TickOrder.Update"/> rather than the fight. Where the
    /// passes are still stubs, the observable is the recorded phase log and the stream counts -
    /// which is deliberate: a swap of two passes, and an extra <c>Rnd.run</c> draw, must each be
    /// caught by a failing assertion and not merely change numbers nobody reads.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class TickOrderTests
    {
        private static RunState PlayingRun(uint seed = 42u)
        {
            var run = new RunState(new Rng(seed));
            run.state = "playing";
            run.player.hp = 8;
            run.player.maxHp = 8;
            return run;
        }

        // ------------------------------------------------- counters and gates

        [Test]
        public void TheRunAndFloorClocksAdvanceOnEveryGatedTick()
        {
            var run = PlayingRun();
            TickOrder.Update(run, Input.None);
            TickOrder.Update(run, Input.None);
            TickOrder.Update(run, Input.None);
            Assert.That(run.ticks, Is.EqualTo(3), "the run clock must count ticks, not visits");
            Assert.That(run.floorTicks, Is.EqualTo(3),
                "the floor clock advances with the run clock; only a floor descent resets it");
            Assert.That(run.hits, Is.EqualTo(0), "hits are counted where damage lands, not per tick");
        }

        [Test]
        public void NotPlayingStatesDoNotAdvanceTheClocks()
        {
            var run = new RunState(new Rng(1u));
            run.player.hp = 8;
            TickOrder.Update(run, Input.None);      // state is 'start', like a fresh run
            run.state = "dev";                     // dev still ticks
            TickOrder.Update(run, Input.None);
            run.state = "gameover";
            TickOrder.Update(run, Input.None);
            Assert.That(run.ticks, Is.EqualTo(1), "only 'playing' and 'dev' tick the run");
            Assert.That(run.floorTicks, Is.EqualTo(1));
        }

        [Test]
        public void TheDeathBackstopFiresBeforeAnyEarlyReturn()
        {
            // A zero-health player must never survive into the transition / ready / fade returns,
            // which is why the check bracketing those returns exists in the original and here.
            var run = PlayingRun();
            run.player.hp = 0;
            TickOrder.Update(run, Input.None);
            Assert.That(run.state, Is.EqualTo("gameover"));
            Assert.That(run.ticks, Is.EqualTo(0), "a dead player ticks no time out");
        }

        [Test]
        public void TheReadyWindowTicksTimeButNotTheSimulation()
        {
            var run = PlayingRun();
            run.readyT = 2;
            TickOrder.Update(run, Input.None);
            Assert.That(run.ticks, Is.EqualTo(1), "the clock runs through the ready window");
            Assert.That(run.readyT, Is.EqualTo(1));
            Assert.That(run.phaseLog, Is.Empty, "no pass may run while the room is still settling");

            TickOrder.Update(run, Input.None);
            Assert.That(run.readyT, Is.EqualTo(0));
            Assert.That(run.phaseLog, Is.Empty, "the last ready tick still runs no pass");

            TickOrder.Update(run, Input.None);
            Assert.That(run.phaseLog, Has.Count.EqualTo(4), "the passes resume the moment ready clears");
        }

        [Test]
        public void TheTransitionTicksTimeAndFadesOutThenReleases()
        {
            var run = PlayingRun();
            // a real floor and a real neighbour of the start room: releasing the transition now ENTERS it
            run.dungeon = Dungeon.FromSeed(new Rng(42u));
            Dir way = System.Linq.Enumerable.First(run.CurrentRoom!.Doors);
            var dest = run.dungeon.Neighbour(run.CurrentRoom!, way)!;
            run.trans = new Trans { t = 0, nx = dest.X, ny = dest.Y, from = "W" };
            TickOrder.Update(run, Input.None);
            Assert.That(run.ticks, Is.EqualTo(1), "transition ticks are run time");
            Assert.That(run.trans!.t, Is.EqualTo(1), "the transition ages one tick per call");
            Assert.That(run.phaseLog, Is.Empty, "no gameplay ticks during a transition");
            Assert.That(run.roomFade, Is.GreaterThan(0), "the fade is easing in");

            for (int i = 1; i < Balance.FadeOut + 1; i++) TickOrder.Update(run, Input.None);
            Assert.That(run.trans, Is.Null, "the transition releases at FADE_OUT");
            Assert.That(run.fadeT, Is.EqualTo(run.fadeTicks - 1),
                "releasing hands off to the arrival fade, which has already taken its first tick");
        }

        [Test]
        public void TheFadeBookkeepingRunsAboveTheStateGate()
        {
            // roomFade ages on the title screen too: the descent banner and the arrival fade are
            // exactly the things that must be visible when nothing is simulating.
            var run = new RunState(new Rng(1u));
            run.state = "start";
            run.player.hp = 8;
            run.fadeT = run.fadeTicks;
            TickOrder.Update(run, Input.None);
            Assert.That(run.fadeT, Is.EqualTo(run.fadeTicks - 1), "the fade ages even off the playing state");
            Assert.That(run.roomFade, Is.GreaterThan(0.99), "the arrival fade starts at full black");
            Assert.That(run.ticks, Is.EqualTo(0), "but no run time is banked while it does");
        }

        // ------------------------------------------------- the order gate

        /// <summary>
        /// The pass order, and WHY IT IS THIS ORDER - re-measured from the running game on
        /// 2026-10-04 rather than taken from the comment that states it.
        /// <para>
        /// The order is player, projectiles, bodies, room. The reason it matters is not tidiness:
        /// on a tick where the player walks through a door, <c>trans</c> is set inside the player
        /// pass and the tick returns immediately - so the projectile and body passes do not run at
        /// all on that tick. Measured, in the live game, with a body and a projectile placed in the
        /// room and the transition forced exactly as a player opening a door would set it:
        /// <para>
        ///     body moved       0.00
        ///     projectile moved 0.00
        /// <para>
        /// So the shell a player would have dodged does not advance on the tick they leave through
        /// the door, and a body does not step. Port the passes in any other order and that shell
        /// moves on a tick it must not - which is a different fight from the game's in every room
        /// containing both a shooter and a body.
        /// </para>
        /// </summary>
        [Test]
        public void TheTickOrderIsPlayerThenProjectilesThenBodiesThenRoom()
        {
            // Several bugs in update() were ORDER bugs - a hit landing after the body it hit had
            // already moved. Until the passes carry real state, the recorded sequence is the pin;
            // a swap of two of them must fail this, not change a number nobody reads.
            //
            // AND THIS ORDER WAS MEASURED, because it was wrong here first. The test asserted
            // projectiles, bodies, player, room and named itself after that. The game runs PLAYER
            // FIRST: the integration is at src/60-tick.js:432-517 and the projectile loop does not
            // begin until 594. Found by putting a probe inside update() on each pass and reading the
            // sequence out of one live tick.
            //
            // Reading the source does not settle it, because the four passes are not four named blocks
            // - `update()` is one function with the loops inline, so "projectiles then bodies" looks
            // true to anyone scanning for the loops in order and is only true from line 594 onward.
            // The player pass is above them all.
            //
            // It is load-bearing twice over, which is why a wrong order here is not a cosmetic label:
            //   - the player pass can set `trans` and src/60-tick.js:517 is `if(trans) return;`, a
            //     WHOLE-TICK abort, so on a tick the player walks through a door the other three passes
            //     must not run at all;
            //   - a gunner steps after the player has moved, so its aim is solved against where the
            //     player IS rather than where they were.
            var run = PlayingRun();
            TickOrder.Update(run, Input.None);
            Assert.That(run.phaseLog, Is.EqualTo(new[] { "player", "projectiles", "bodies", "room" }));
        }

        [Test]
        public void ANewTickBeginsANewPhaseLog()
        {
            var run = PlayingRun();
            TickOrder.Update(run, Input.None);
            TickOrder.Update(run, Input.None);
            Assert.That(run.phaseLog, Has.Count.EqualTo(4),
                "the log holds one tick's worth of passes, not an accumulated history");
        }

        // ------------------------------------------------- the pass skelotons

        /// <summary>
        /// Momentum charges on movement UNDER PRESSURE and bleeds away when stalled.
        /// <para>
        /// The "under pressure" part is load-bearing and this test used to leave it out. The original
        /// returns from <c>tickMomentum</c> immediately when the room has no bodies - the meter is a
        /// reward for being chased, not for walking - and this port had lost that guard. It was found
        /// by porting the player phase and comparing its acceleration curve against the game's, which
        /// disagreed in the fourth decimal place: the C# player was quietly charging momentum in an
        /// empty room, which is a free speed bonus with no counterweight.
        /// </para>
        /// <para>
        /// The room is populated below, and the empty-room case is asserted separately, because the
        /// guard is the whole point of the test.
        /// </para>
        /// </summary>
        [Test]
        public void TickMomentumChargesOnPressurisedMovementAndDecaysWhenStalled()
        {
            var run = PlayingRun();
            run.enemies.Add(new Enemy { kind = BodyKind.Lunger });   // pressure, or no charge
            TickOrder.TickMomentum(run, 2.0);
            Assert.That(run.player.momentum, Is.EqualTo(Balance.MomentumGain * 2.0).Within(1e-12));
            TickOrder.TickMomentum(run, 0.0);
            Assert.That(run.player.momentum, Is.EqualTo(Balance.MomentumGain * 2.0 - Balance.MomentumStallDecay).Within(1e-12));

            run.player.momentum = 0.999;
            TickOrder.TickMomentum(run, 500.0);
            Assert.That(run.player.momentum, Is.EqualTo(1.0), "the meter caps rather than overshooting");
            TickOrder.TickMomentum(run, 0.0); TickOrder.TickMomentum(run, 0.0);
            for (int i = 0; i < 200; i++) TickOrder.TickMomentum(run, 0.0);
            Assert.That(run.player.momentum, Is.EqualTo(0.0), "a stalled body bleeds the meter to zero");
        }

        /// <summary>
        /// No bodies in the room means no momentum at all, however far the player walks. Measured in
        /// the running game: 400 ticks running east in an empty room leave the meter at exactly 0,
        /// and the terminal speed is 1.4025 rather than something higher.
        /// </summary>
        [Test]
        public void TickMomentumChargesNothingInAnEmptyRoom()
        {
            var run = PlayingRun();
            run.enemies.Clear();
            TickOrder.TickMomentum(run, 500.0);
            Assert.That(run.player.momentum, Is.EqualTo(0.0),
                "momentum is a pressure reward; in an empty room it is a free speed bonus");
        }

        [Test]
        public void TickMomentumDrawsNoRandomness()
        {
            // Movement reads actual velocity; it must never roll. A draw here would reach into
            // a stream it is not seeded for.
            var run = PlayingRun();
            TickOrder.TickMomentum(run, 3.0);
            Assert.That(run.rng.RunCalls, Is.EqualTo(0));
            Assert.That(run.rng.JitterCalls, Is.EqualTo(0));
            Assert.That(run.rng.ArtCalls, Is.EqualTo(0));
        }

        [Test]
        public void FireCommittedShotSpendsOneJitterDrawAndEmitsOneProjectile()
        {
            var run = PlayingRun();
            var e = new Enemy
            {
                kind = BodyKind.Gunner, castT = 0, castReady = true,
                cdMin = 10, cdVar = 5, pspd = 2.0, pr = 5, dmg = 1.8, castAim = 0,
            };
            TickOrder.FireCommittedShot(run, e, 0);
            Assert.That(run.rng.JitterCalls, Is.EqualTo(1), "the cooldown roll is one jitter draw");
            Assert.That(run.rng.RunCalls, Is.EqualTo(0), "a cooldown must never reach into the run stream");
            Assert.That(run.rng.ArtCalls, Is.EqualTo(0));
            Assert.That(e.castReady, Is.False, "the tell was spent, not consumed twice");
            Assert.That(e.shootCd, Is.GreaterThanOrEqualTo(10).And.LessThanOrEqualTo(15),
                "shootCd lands inside cdMin..cdMin+cdVar at zero pressure");
            Assert.That(run.projectiles, Has.Count.EqualTo(1));
            var p = run.projectiles[0];
            Assert.That(p.friendly, Is.False);
            Assert.That(p.heavy, Is.True, "a gunner's shell is heavy");
            Assert.That(p.vx, Is.EqualTo(2.0).Within(1e-12), "castAim 0 fires along +x");
        }

        [Test]
        public void FireCommittedShotHonorsTheCastTellBeforeFiring()
        {
            // The stun does not break the tell - a body that was held when its cast finished
            // still fires (src/60-tick.js:17-27). The cast clock is jitter-free bookkeeping.
            var run = PlayingRun();
            var e = new Enemy { kind = BodyKind.Shooter, castT = 2, castReady = false, cdMin = 4, cdVar = 2, pspd = 2.2, pr = 5, dmg = 1.8 };
            TickOrder.FireCommittedShot(run, e, 0);
            Assert.That(e.castT, Is.EqualTo(1));
            Assert.That(e.castReady, Is.False);
            Assert.That(run.projectiles, Is.Empty);
            TickOrder.FireCommittedShot(run, e, 0);
            Assert.That(e.castT, Is.EqualTo(0));
            Assert.That(e.castReady, Is.True, "the cast completing arms the body");
            Assert.That(run.projectiles, Is.Empty);
            TickOrder.FireCommittedShot(run, e, 0);
            Assert.That(run.projectiles, Has.Count.EqualTo(1), "the armed body fires on the next tick");
            Assert.That(run.rng.RunCalls, Is.EqualTo(0));
            Assert.That(run.rng.ArtCalls, Is.EqualTo(0));
        }

        [Test]
        public void StepBossSpendsExactlyOneRunAndOneJitterPerPick()
        {
            var run = PlayingRun();
            var boss = new Enemy
            {
                kind = BodyKind.Boss, move = "idle", moveT = 0, bossCd = 0,
                phase = 1, hp = 100, maxHp = 100,
            };
            TickOrder.StepBoss(run, boss);
            Assert.That(run.rng.RunCalls, Is.EqualTo(1),
                "the boss move pick is exactly one run draw - a second draw here renumbers every seed");
            Assert.That(run.rng.JitterCalls, Is.EqualTo(1), "the cadence roll is exactly one jitter draw");
            Assert.That(run.rng.ArtCalls, Is.EqualTo(0), "no art may draw in the update path");
            Assert.That(boss.move, Is.Not.EqualTo("idle"), "the pick commits the boss to a move");

            // the committed move plays out (a volley takes hundreds of ticks) and spends no randomness
            for (int t = 0; t < 2000 && boss.move != "idle"; t++) TickOrder.StepBoss(run, boss);
            Assert.That(boss.move, Is.EqualTo("idle"), "the move never finished");
            Assert.That(run.rng.RunCalls, Is.EqualTo(1), "resolving a commit does not re-roll the move");
            Assert.That(run.rng.JitterCalls, Is.EqualTo(1));
        }

        [Test]
        public void StepBossPhaseTransitionsSpendNoRandomness()
        {
            var run = PlayingRun();
            var boss = new Enemy { kind = BodyKind.Boss, move = "idle", moveT = 0, bossCd = -5, phase = 1, hp = 60, maxHp = 100 };
            TickOrder.StepBoss(run, boss);
            Assert.That(boss.phase, Is.EqualTo(2));
            Assert.That(boss.move, Is.EqualTo("idle"));
            Assert.That(run.rng.RunCalls, Is.EqualTo(0), "a phase gate is a health check, not a roll");
            Assert.That(run.rng.JitterCalls, Is.EqualTo(0));
        }

        [Test]
        public void AFullTickThroughUpdateSpendsNoRandomness()
        {
            // The gate itself is bookkeeping: no boss firing, no committed shot, no draw. An extra
            // draw anywhere in Update would shift every later seed-driven decision, and that is
            // precisely the failure this test fails loudly.
            var run = PlayingRun();
            TickOrder.Update(run, Input.None);
            Assert.That(run.rng.RunCalls, Is.EqualTo(0));
            Assert.That(run.rng.JitterCalls, Is.EqualTo(0));
            Assert.That(run.rng.ArtCalls, Is.EqualTo(0));
        }

        [Test]
        public void TheJitterStreamIsNeverReachedThroughTheRunStream()
        {
            // The separation the mutation suite leans on: mixing the streams is one extra draw
            // away at every call site, so pin the counts after one of each jitter consumer.
            var run = PlayingRun();
            var e = new Enemy { kind = BodyKind.Gunner, castT = 0, castReady = true, cdMin = 5, cdVar = 3, pspd = 1.9, pr = 8, dmg = 1.44 };
            TickOrder.FireCommittedShot(run, e, 0);
            var boss = new Enemy { kind = BodyKind.Boss, move = "idle", moveT = 0, bossCd = 0, phase = 3, hp = 100, maxHp = 100 };
            TickOrder.StepBoss(run, boss);
            Assert.That(run.rng.RunCalls, Is.EqualTo(1), "only the boss pick draws from run");
            Assert.That(run.rng.JitterCalls, Is.EqualTo(2), "one cadence roll per fired commitment");
            Assert.That(run.rng.ArtCalls, Is.EqualTo(0), "nothing in the update path touches art");
        }
    }
}
