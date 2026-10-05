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
    ///  * the order gate itself - the PLAYER first, then projectiles, then bodies, then the room -
    ///    recorded in <see cref="RunState.phaseLog"/> until the passes carry real state. The order was
    ///    wrong here (it said projectiles first) until the game's was measured from a live tick; see
    ///    the comment on the gate below, because the player-first order is load-bearing twice over.
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

            /* THE ORDER GATE, AND THE ORDER WAS WRONG.

               This logged projectiles, bodies, player, room. The game runs PLAYER, projectiles, bodies,
               room - measured, not read, by putting a probe inside `update()` on each pass and
               recording the sequence from one live tick:

                   player -> projectiles -> bodies -> room

               The player integration is at src/60-tick.js:432-517 and the projectile loop does not
               begin until 594. So the player is integrated, walks, can open a door, and only THEN are
               shells advanced and bodies stepped.

               WHY IT MATTERS, and it is not cosmetic ordering. Two of the four passes read state the
               player pass writes on the same tick:

                 - `checkDoorTransition` inside the player pass can set `trans`, and src/60-tick.js:517
                   is `if(trans) return;` - a WHOLE-TICK abort. On a tick where the player walks through
                   a door, projectiles and bodies do not run at all. Run the projectile pass first and
                   that shell advances on a tick it must not, in the tick the player leaves the room.

                 - The gunner's aim is solved against the player's position, and the swerve meter is
                   computed from the player's TARGET velocity BEFORE integration (the comment at
                   src/60-tick.js:466 says so). A gunner that steps before the player has moved leads
                   the position the player is about to be at, not the one they were at.

               So this was not a stale label on a stub. When the passes carry real state, this order
               would make the port play a different fight from the game in every room containing a
               shell and a body. Each pass is still a stub that records it ran; what changed is the
               order they are recorded in, which is the thing the passes will have to honour. */
            run.phaseLog.Clear();
            run.phaseLog.Add("player");
            run.phaseLog.Add("projectiles");
            run.phaseLog.Add("bodies");
            run.phaseLog.Add("room");

            /* THE ROOM PHASE RUNS FOR REAL. The other three still only record that they ran.
               `TickRoom` returns true when the player reached the exit portal, which is the
               original's mid-function `return` - and that return SKIPS everything after the pickup
               loop, so it has to be honoured here or a descent would also re-open the boss exit on
               the same tick. */
            TickRoom(run);
        }

        /// <summary>
        /// The room phase, ported. This is the first of the four passes to carry real behaviour, and
        /// it was chosen because it is the one whose dependencies are all already here: pickups, the
        /// player, the room's own flags, and five constants that <c>Balance</c> already had.
        ///
        /// <para>
        /// <b>What is ported and what is not.</b> The room-cleared bookkeeping, both reward drops,
        /// the whole pickup-touch loop, the weapon swap, the heart and armour top-ups, both keys, the
        /// hook and blast alt-modes, and the boss-death exit are all here. The <c>item</c> branch is
        /// NOT: it calls <c>Items.give</c>, which has no C# counterpart yet, and a stub that quietly
        /// collected nothing would be a port that looks done and plays differently. It throws
        /// throws instead, so the gap is a crash rather than a
        /// behaviour change - which is the right way for a parity port to be incomplete.
        /// </para>
        ///
        /// <para>
        /// <b>Two behaviours that are easy to lose and are kept deliberately.</b>
        /// </para>
        /// <list type="number">
        /// <item>The <c>hold</c> flag. A pickup taken this tick is parked where it stood and ignored
        /// until the player steps off it. That is what lets the weapon swap put the old gun on the
        /// floor at the player's feet - it would otherwise be re-collected on the same tick, in a
        /// loop, and the swap would never settle. The check order matters too: <c>hold</c> is
        /// cleared BEFORE the touch test, not after, so stepping off and back on re-collects.</item>
        /// <item>The exit portal's smaller radius. Every other pickup is collected at
        /// <c>pk.r + player.r</c>; the exit is collected at the bare <c>PLAYER_HIT_R</c>. Walking
        /// into the way down is a deliberate act and should not be something you do by brushing past
        /// the edge of a 30px circle, so the test is tighter on purpose.</item>
        /// </list>
        ///
        /// <para>
        /// Returns true when the phase reached the exit portal and the run descended, which is the
        /// original's <c>return</c> out of the middle of the function - everything after the pickup
        /// loop is skipped that tick. The death check immediately after the loop is deliberately
        /// NOT skipped in that case, because it is outside the loop in the original too: reaching
        /// the exit and dying on the same tick ends the run as a death, and the boss-death check
        /// beneath it does not run.
        /// </para>
        /// </summary>
        public static bool TickRoom(RunState run)
        {
            // The current room. The C# model keeps the CURRENT room's bodies on `RunState.enemies`
            // rather than on `Room`, because the body list is rebuilt per room and belongs to the
            // tick's working set; `Room` carries the durable facts (type, doors, flags). That is a
            // different shape from the JavaScript, where one object holds both, and it is why this
            // line reads `run.enemies` where the original reads `r.enemies` - same list, different
            // owner.
            var room = run.CurrentRoom;
            // The generator cannot produce a cursor on an empty cell, so this is unreachable in a
            // well-formed run. It is handled rather than asserted because a null here would
            // otherwise be a NullReferenceException from inside the tick, which is a crash the
            // player sees rather than a diagnosable failure.
            if (room == null) return false;
            var player = run.player;

            // A room that has just been cleared refunds half a blink charge, ONCE. Without the flag
            // a cleared room pays out every tick for as long as the player stands in it.
            if (run.enemies.Count == 0 && !room.Cleared)
            {
                room.Cleared = true;
                player.blinkRegen = System.Math.Max(player.blinkRegen, Balance.BlinkRecharge * 0.5);
            }

            // The rewards, dropped once each. Two independent one-shots, because a room can hold
            // either or both.
            if (run.enemies.Count == 0)
            {
                if (room.KeyReward && !room.KeySpawned)
                {
                    room.KeySpawned = true;
                    run.pickups.Add(Pickup.Of("key", Balance.MidX, Balance.MidY, 14));
                }
                if (room.GoldReward && !room.GoldSpawned)
                {
                    room.GoldSpawned = true;
                    run.pickups.Add(Pickup.Of("goldkey", Balance.MidX, Balance.MidY, 16));
                }
            }

            if (player.hp <= 0) { run.EndRun(false); return false; }

            bool descended = false;

            // BACKWARDS, because a pickup taken this tick removes it from the list this loop is
            // walking. Forwards would skip the next one.
            for (int i = run.pickups.Count - 1; i >= 0; i--)
            {
                var pk = run.pickups[i];
                double dist = System.Math.Sqrt(
                    (pk.x - player.x) * (pk.x - player.x) + (pk.y - player.y) * (pk.y - player.y));

                // The exit is deliberately the tighter test. See the note above.
                bool touching = pk.kind == "exit"
                    ? dist < Balance.PlayerHitR
                    : dist < pk.r + player.r;

                // `hold` clears BEFORE the touch test, so stepping off a held pickup and back on
                // collects it again - and a held pickup the player is still standing on is not
                // collected a second time.
                if (pk.hold) { if (!touching) pk.hold = false; continue; }
                if (!touching) continue;

                if (pk.kind == "exit") { run.Descend(); descended = true; continue; }

                if (pk.kind == "weapon")
                {
                    int old = player.weaponIdx;
                    player.weaponIdx = pk.w;
                    // The cooldown is the NEW weapon's, and it is a MINIMUM: swapping into a gun
                    // that is already nearly ready does not grant a free shot.
                    player.cooldown = System.Math.Min(player.cooldown, Balance.WeaponCooldown(pk.w));
                    pk.w = old;
                    pk.hold = true;
                    continue;
                }

                if (pk.kind == "heart")
                {
                    if (player.hp >= player.maxHp) continue;   // a full-health heart is left lying
                    player.hp = System.Math.Min(player.maxHp, player.hp + 2);
                }
                else if (pk.kind == "armor")
                {
                    if (player.armor >= Balance.MaxArmor) continue;
                    player.armor = System.Math.Min(Balance.MaxArmor, player.armor + 2);
                }
                else if (pk.kind == "key") player.hasSilver = true;
                else if (pk.kind == "goldkey") player.hasGold = true;
                else if (pk.kind == "hook")
                {
                    if (player.altMode != "hook")
                    {
                        player.altMode = "hook";
                        run.hook = true;
                        // The blast the hook leaves behind. It is placed at the HOOK's position and
                        // held, so it cannot be picked straight back up.
                        run.pickups.Add(new Pickup { x = pk.x, y = pk.y, r = 16, kind = "blast", hold = true });
                    }
                    else run.hook = true;
                }
                else if (pk.kind == "blast") { player.altMode = "blast"; run.hook = false; }
                else if (pk.kind == "item")
                {
                    // NOT PORTED. See the summary: a silent no-op here is a behaviour change.
                    throw new System.NotSupportedException(
                        "tickRoom: the item pickup branch calls Items.give, which has no C# "
                        + "counterpart. Collect it as a stub and the port looks finished while "
                        + "playing differently - so it throws until Items lands.");
                }
                else continue;   // an unknown kind is left on the floor rather than eaten

                run.pickups.RemoveAt(i);
            }

            /* THE ORIGINAL RETURNS OUT OF THE MIDDLE OF THE FUNCTION HERE, and that has to be
               honoured. `descend()` already reseeded, moved the player and rebuilt the dungeon, so
               running the death check and the boss check against the NEW floor would be checking a
               floor the player never saw - and re-opening an exit on it is a second, invisible bug.

               The only thing after the loop that is still correct to skip is the whole tail, so this
               returns rather than falling through. It returns FALSE because the caller wants to know
               whether it descended, and `descended` above already recorded that. */
            if (descended) return true;

            // Death wins ties: a pickup that arrives on the frame the player dies ends the run as a
            // death, and the boss check below does not run.
            if (player.hp <= 0) { run.EndRun(false); return false; }
            else if (room.Type == RoomKind.Boss && run.enemies.Count == 0 && !room.ExitOpen)
            {
                // The way down opens when the boss dies and is walked into, rather than the run
                // ending on the kill. The one-shot flag is what stops a new portal appearing on
                // every tick the player stands in an empty boss room.
                room.ExitOpen = true;
                run.pickups.Add(Pickup.Of("exit", Balance.MidX, Balance.MidY, 30));
            }

            return descended;
        }

    }
}
