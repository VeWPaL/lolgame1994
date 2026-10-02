namespace Depths
{
    /// <summary>
    /// The input one tick reads. The original accumulates <c>keys</c> at module scope; this is the
    /// engine-independent line, so the tick takes it as an argument and never touches the DOM.
    /// </summary>
    public readonly struct Input
    {
        public readonly double dx, dy;
        public readonly bool fire, alt, blink;

        public Input(double dx, double dy, bool fire = false, bool alt = false, bool blink = false)
        {
            this.dx = dx;
            this.dy = dy;
            this.fire = fire;
            this.alt = alt;
            this.blink = blink;
        }

        public static Input None => new Input(0, 0);
    }

    /// <summary>
    /// The tick skeleton, in the original's load-bearing order. "The one function ... where the
    /// rules interleave" (src/60-tick.js:1-11). The full ~870-line body is out of scope for this
    /// pass; what lands here, and must not regress, is:
    ///
    ///  * the state gate and the two health checks that bracket everything,
    ///  * the run and floor clocks, which advance on every tick that passes the gate - including
    ///    transition and ready ticks, because neither branch ticks gameplay but both tick time,
    ///  * the trans / ready / fade early returns, with the fade bookkeeping placed above them for
    ///    the same reason it is in the original: presentation that must keep running through
    ///    states where the simulation has stopped lives above the returns,
    ///  * the order gate itself - projectiles, then bodies, then the player, then the room -
    ///    recorded in <see cref="RunState.phaseLog"/> until the passes carry real state.
    ///
    /// <para>
    /// RNG discipline is the same split the original's suite insists on: the boss move pick is a
    /// <c>run</c> draw (it decides what the run is), every cooldown and spread roll is a
    /// <c>jitter</c> draw (behaviour noise), and nothing in the update path touches <c>art</c>.
    /// An <see cref="RunState"/> that never fires a move still counts what it spends, which is how
    /// a test proves an extra <c>run</c> draw cannot hide inside a stub.
    /// </para>
    /// </summary>
    public static class TickOrder
    {
        // smoothstep, src/10-art.js:516. Kept private: the only caller is the fade bookkeeping,
        // and a shared smooth() has a way of getting used for something else that then quantises.
        private static double Smooth(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);

        /// <summary>
        /// Charges Momentum on movement under pressure, src/60-tick.js:37-44. Runs no RNG and
        /// reads only the player; the empty-room guard awaits the per-room enemy list, which
        /// belongs to the room port and is noted as remaining work.
        /// </summary>
        public static void TickMomentum(RunState run, double moved)
        {
            var player = run.player;
            player.momentum = moved > Balance.MomentumMoveFloor
                ? System.Math.Min(1, player.momentum + Balance.MomentumGain * moved)
                : System.Math.Max(0, player.momentum - Balance.MomentumStallDecay);
        }

        /// <summary>
        /// A cast that has finished LEAVES, src/60-tick.js:28-35. The cooldown roll is a jitter
        /// draw - behaviour noise that must never reach into the run stream - and the pressure
        /// term shortens it in a crowded room.
        /// </summary>
        public static void FireCommittedShot(RunState run, Enemy e, double roomPress)
        {
            if (e.castT > 0)
            {
                if (--e.castT <= 0) e.castReady = true;
                return;
            }
            if (!e.castReady) return;
            e.castReady = false;
            e.shootCd = (e.cdMin + run.rng.Jitter() * e.cdVar) * (1 - Balance.PressureCadence * roomPress);
            run.projectiles.Add(new Projectile
            {
                x = e.x,
                y = e.y,
                vx = System.Math.Cos(e.castAim) * e.pspd,
                vy = System.Math.Sin(e.castAim) * e.pspd,
                r = e.pr,
                dmg = e.dmg,
                friendly = false,
                heavy = e.kind == BodyKind.Gunner,
            });
        }

        /// <summary>
        /// The Warden's move cadence, src/60-tick.js:146-181. This is the skeleton of the move
        /// machine: phase thresholds, the idle/tell bookkeeping, the weighted bag, one run draw
        /// for the pick and one jitter draw for the cooldown. The move bodies (volley, sweep,
        /// wall) land with the boss fight itself.
        /// </summary>
        public static void StepBoss(RunState run, Enemy e, double roomPress)
        {
            if (e.maxHp > 0)
            {
                double frac = e.hp / e.maxHp;
                if (e.phase == 1 && frac <= Balance.BossPhase1) BossPhase(e, 2);
                else if (e.phase == 2 && frac <= Balance.BossPhase2) BossPhase(e, 3);
            }

            if (e.move != "idle") { ResolveBoss(run, e); return; }
            if (e.moveT > 0) { e.moveT--; return; }

            e.bossCd -= 1;
            if (e.bossCd > 0) return;

            // The bag by phase, so a phase is a different fight and not the same one with the
            // dice rolled differently (src/60-tick.js:161-177).
            var bag = new System.Collections.Generic.List<string> { "volley", "volley", "sweep" };
            if (e.phase >= 2) bag.Add("wall");
            if (e.phase >= 3) { bag.Add("volley"); bag.Add("sweep"); bag.Add("sweep"); }

            string pick = bag[(int)(run.rng.Run() * bag.Count)];
            e.bossCd = (e.cdMin > 0 ? e.cdMin : Balance.BossCdMin)
                     + run.rng.Jitter() * (e.cdVar > 0 ? e.cdVar : Balance.BossCdVar);
            e.bossCd *= (1 - Balance.PressureCadence * roomPress);
            BeginBoss(run, e, pick);
        }

        public static void BossPhase(Enemy e, int n)
        {
            e.phase = n;
            e.move = "idle";
            e.moveT = Balance.BossRecover;      // the phase change is itself a beat of recovery
            e.bossCd = Balance.Sec(0.4);
        }

        public static void BeginBoss(RunState run, Enemy e, string pick)
        {
            // 'run' is accepted for symmetry with the rest of the boss machine; the real begin
            // reads the player position, which arrives with the player-step port.
            e.move = pick;
            // Every move opens with the cast tell, src/60-tick.js:199-211. The shell and the
            // sweep land when the fight is ported; what must not move is that the tell precedes
            // the hit.
            e.moveT = pick == "wall" ? Balance.BossRecover : Balance.CastTime;
            e.castReady = false;
        }

        public static void ResolveBoss(RunState run, Enemy e)
        {
            e.move = "idle";
            e.moveT = Balance.BossRecover;
        }

        /// <summary>
        /// The update() gate, src/60-tick.js:256-340. Only the load-bearing skeleton for now:
        /// gates, clocks, the early returns, and the order gate.
        /// </summary>
        public static void Update(RunState run, Input input)
        {
            // roomFade is progress-driven, and it runs ABOVE the state gate for the same reason
            // it does in the original: a fade has to finish when the simulation is stopped.
            if (run.fadeT > 0 && run.trans == null)
            {
                run.fadeT--;
                run.roomFade = 1 - Smooth(1 - (double)run.fadeT / run.fadeTicks);
            }
            else if (run.trans == null) run.roomFade = 0;

            if (run.state != "playing" && run.state != "dev") return;

            // THE DEATH BACKSTOP. Checked on the way in, before any early return below can fire,
            // and the lab is protected one level down in the original at damagePlayer. A zero-hp
            // player must not keep playing out a transition.
            if (run.player.hp <= 0) { run.state = "gameover"; return; }

            run.ticks++;
            run.floorTicks++;

            if (run.trans != null)
            {
                run.trans.t++;
                run.roomFade = Smooth(System.Math.Min(1, (double)run.trans.t / Balance.FadeOut));
                if (run.trans.t >= Balance.FadeOut)
                {
                    // enterRoom in the original also restarts the arrival fade; the constants
                    // line up: fadeT counts down across the arrival, roomFade eases out of black.
                    run.trans = null;
                    run.fadeT = run.fadeTicks;
                }
                return;
            }
            if (run.readyT > 0) { run.readyT--; return; }

            // THE ORDER GATE. Projectiles resolve, then bodies, then the player, then the room.
            // Each pass is a stub that records it ran; an appended name is the observable the
            // mutation test leans on until the passes carry real state.
            run.phaseLog.Clear();
            run.phaseLog.Add("projectiles");
            run.phaseLog.Add("bodies");
            run.phaseLog.Add("player");
            run.phaseLog.Add("room");
        }
    }
}
