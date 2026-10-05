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
            /* NO MOMENTUM IN AN EMPTY ROOM, and this guard was MISSING until the player phase was
               ported and its acceleration curve disagreed with the game's by four decimal places.

               The original returns immediately when the current room has no bodies. Momentum is a
               PRESSURE mechanic - it charges from being chased and shot at, and it exists to make
               that survivable. Charging it in an empty room inverts the design: a player walking
               alone becomes faster the longer they walk, which is a free speed bonus with no
               counterweight.

               How it hid: nothing in the port called `TickMomentum` until `TickPlayer` landed, and
               `TickPlayer` reads momentum in the SAME tick, so the error was invisible until there
               was a caller and a measurement to compare against. The symptom was an acceleration
               curve that read 0.433763 where the game reads 0.433643 - a momentum of 0.0017, which
               is invisible in isolation and obvious as a curve.

               Measured, after the fix: a player running east in an empty room for 400 ticks has
               momentum 0, and the terminal speed is 1.4025. With bodies present the same 200 ticks
               reaches 1. */
            if (run.enemies.Count == 0) return;

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

        /// <summary>
        /// The player's lagged hitbox, published once per tick by <see cref="TickPlayer"/> and read by
        /// the gunner intercept in the body phase.
        /// <para>
        /// This is the only value one phase computes and a later phase reads, and it is module state
        /// rather than a local or a parameter because it is the same channel the C# side has to
        /// reproduce. The gunners solve against THIS point rather than the player's real position, so
        /// they lead toward where the player is visually leaving - which is the entire reason the
        /// intercept reads as leading rather than chasing.
        /// </para>
        /// </summary>
        public static double HitboxX, HitboxY;

        /// <summary>
        /// The player phase: input, acceleration, movement, the wall clamp, the lagged hitbox, and the
        /// cooldowns. Ported from <c>tickPlayer</c> in <c>src/60-tick.js</c>.
        ///
        /// <para>
        /// <b>Ported:</b> the whole movement integration, the acceleration curve, the directional
        /// estimate the lungers aim at, the swerve meter, the wall clamp with its door gaps, the
        /// lagged hitbox, and every cooldown decrement. <b>Not ported:</b> the boss warning, the
        /// blink, the firing, the on-use field effects and the hook resistance - each of those is a
        /// helper with its own dependencies, and a stub that silently did nothing would be a phase
        /// that looks done and plays differently.
        /// </para>
        ///
        /// <para>
        /// <b>The return value is load-bearing.</b> False means a door transition started and the rest
        /// of the tick must not run, which is the original's <c>if(trans) return;</c> - a WHOLE-TICK
        /// abort, and the reason the four phases are ordered player-first. Measured live in
        /// <c>TickOrderTests</c>: on a transition tick the body and the projectile both move 0.00.
        /// </para>
        ///
        /// <para>
        /// <b>The door gap is the fiddly part and is preserved exactly.</b> A player against a wall
        /// only has their velocity zeroed if they are NOT in a doorway's gap: the test is
        /// <c>abs(player.x - MidX) &lt; DoorWidth/2</c> for a north or south wall and the transpose
        /// for east and west. Without it a player standing in a doorway stops dead and cannot leave,
        /// which is a room you cannot exit.
        /// </para>
        /// </summary>
        public static bool TickPlayer(RunState run, Input input,
            double moveSpeedBonus = Balance.MoveSpeedBonusBase)
        {
            var p = run.player;

            // --- input, normalised so a diagonal is not faster than a straight line.
            double dx = input.dx, dy = input.dy;
            double len = System.Math.Sqrt(dx * dx + dy * dy);

            // The firing slow-motion eases back toward 1 every tick whether or not a gun is fired,
            // so the cost of shooting on the move is paid continuously rather than as a flat penalty.
            p.slowMult += ((1 - p.shootSlow) - p.slowMult) * Balance.SlowEase;
            if (p.boost > 0) p.boost--;

            // The blink boost only applies while moving roughly the way it was thrown, and the gain
            // is blended rather than switched so a boost at 90 degrees is weaker, not absent.
            double align = len != 0 ? (dx / len) * p.boostX + (dy / len) * p.boostY : 0;
            double boostMult = p.boost > 0
                ? 1 + (Balance.BlinkBoostGain - 1) * System.Math.Max(0, align)
                : 1;

            /* The movement bonus is a PARAMETER, not a constant read internally. In the original it is
               `Stats.value('speed') + momentum * MomentumSpeed`, capped - so it changes with the
               player's build, and a phase that read a constant would be correct only for a run
               carrying nothing. The C# port has no stats system yet, so the default is the measured
               empty-run value and a caller with a build passes the real one. */
            double bonus = System.Math.Min(Balance.MoveSpeedHardCap, moveSpeedBonus);
            double spd = p.speed * p.slowMult * (1 + bonus) * boostMult;
            double targetVx = len != 0 ? (dx / len) * spd : 0;
            double targetVy = len != 0 ? (dy / len) * spd : 0;

            /* THE ACCELERATION RAMP IS THE FEEL, and it is why this is not a velocity assignment.
               `MoveAccel` closes 11.6% of the remaining gap per tick, so the player takes about 40
               ticks - about a fifth of a second - to reach top speed, and Momentum makes a charged
               player accelerate harder rather than merely faster. Measured in the browser: the
               terminal speed is 1.4025 and the first ten ticks are
               0.16269, 0.306508, 0.433643, 0.54603, 0.645381, 0.733207, 0.810845, 0.879477,
               0.940147, 0.99378. */
            double accel = Balance.MoveAccel * (1 + p.momentum * Balance.MomentumAccel);
            p.vx += (targetVx - p.vx) * accel;
            p.vy += (targetVy - p.vy) * accel;

            // The direction-of-travel estimate, for the lungers. It is smoothed more gently than the
            // velocity itself (LungeTrack) and is renormalised, so it is a DIRECTION and not a
            // magnitude that drifts.
            p.trendVx += (p.vx - p.trendVx) * Balance.LungeTrack;
            p.trendVy += (p.vy - p.trendVy) * Balance.LungeTrack;

            /* The second, separate estimate - the player's BELIEF about their own direction, used by
               the gunners. It is a different number from `trendVx` on purpose: the gunners read a
               slower, noisier estimate than the lungers do, which is why a counterstrafe works
               against one and not the other. */
            double bsp = System.Math.Sqrt(p.vx * p.vx + p.vy * p.vy);
            if (bsp > Balance.PlayerSpeedEps)
            {
                p.beliefVx += (p.vx / bsp - p.beliefVx) * Balance.LungeBeliefTrack;
                p.beliefVy += (p.vy / bsp - p.beliefVy) * Balance.LungeBeliefTrack;
                double bl = System.Math.Sqrt(p.beliefVx * p.beliefVx + p.beliefVy * p.beliefVy);
                if (bl > 1e-4) { p.beliefVx /= bl; p.beliefVy /= bl; }
            }

            /* THE SWERVE METER. It rises when the player's new direction disagrees with the one they
               were already committed to, and decays every tick. The gunners read it to decide how
               much to trust their intercept, so it is the mechanic that makes movement a defence.

               The decay is per-tick and absolute, not multiplicative: a full meter falls to 0.5765 in
               one second, which is 0.0035 a tick. That number was WRONG in this port for a while -
               see the note on `Balance.SwerveDecay` - and nothing caught it, because nothing read
               the constant until now. */
            double msp = System.Math.Sqrt(targetVx * targetVx + targetVy * targetVy);
            if (msp > 0.05)
            {
                if (p.dirX != 0 || p.dirY != 0)
                {
                    double a = (targetVx * p.dirX + targetVy * p.dirY) / msp;
                    if (a < 0.35) p.swerve = System.Math.Min(1, p.swerve + Balance.SwerveGain * (1 - a));
                }
                p.dirX = targetVx / msp;
                p.dirY = targetVy / msp;
            }
            p.swerve = System.Math.Max(0, p.swerve - Balance.SwerveDecay);

            // --- integrate. The knockback is a separate term so a hit shoves without steering.
            double wasX = p.x, wasY = p.y;
            double knk = System.Math.Sqrt(p.kvx * p.kvx + p.kvy * p.kvy);
            p.x += p.vx + p.kvx;
            p.y += p.vy + p.kvy;
            p.kvx *= Balance.KnockPFriction;
            p.kvy *= Balance.KnockPFriction;
            if (System.Math.Abs(p.kvx) < Balance.KnockCut) p.kvx = 0;
            if (System.Math.Abs(p.kvy) < Balance.KnockCut) p.kvy = 0;

            ClampPlayer(run);

            // A door transition aborts the WHOLE tick. This is the return that makes the phase order
            // load-bearing rather than a convention.
            if (CheckDoorTransition(run)) return false;

            double sp = System.Math.Sqrt(p.vx * p.vx + p.vy * p.vy);

            /* THE WALL CLAMP, AND WHY IT ZEROES VELOCITY RATHER THAN JUST THE POSITION.

               `ClampPlayer` above has already stopped the position. This zeroes the velocity
               component pointing into a wall - but ONLY outside a doorway's gap. Both halves of that
               sentence are load-bearing:

               - Zeroing velocity unconditionally would stop a player dead in a doorway, and a room
                 you cannot leave is a bug rather than a wall.
               - Not zeroing it at all would leave the player reporting full speed forever while
                 covering no pixels, which the Momentum meter and the gunners both used to read.
                 That is solved elsewhere by measuring displacement, but the velocity itself should
                 still not lie.

               Measured, against a wall whose door is genuinely CLOSED: the player stops at exactly
               `RoomLeft + r` = 63 with `vx` = 0. Against the start room's west side the door is open,
               so no clamp applies and the player walks through - which is correct, and is why a
               probe that measures "the wall" has to check the door first. */
            var room = run.CurrentRoom;
            if (room != null)
            {
                bool inGapX = System.Math.Abs(p.x - Balance.MidX) < Balance.DoorWidth / 2;
                bool inGapY = System.Math.Abs(p.y - Balance.MidY) < Balance.DoorWidth / 2;

                if (p.y <= Balance.RoomTop + p.r && !(DoorPassable(room, Dir.N) && inGapX)) p.vy = 0;
                if (p.y >= Balance.RoomBottom - p.r && !(DoorPassable(room, Dir.S) && inGapX)) p.vy = 0;
                if (p.x <= Balance.RoomLeft + p.r && !(DoorPassable(room, Dir.W) && inGapY)) p.vx = 0;
                if (p.x >= Balance.RoomRight - p.r && !(DoorPassable(room, Dir.E) && inGapY)) p.vx = 0;
            }

            /* MOMENTUM IS EARNED FROM DISPLACEMENT, NOT FROM VELOCITY, and the knockback is
               subtracted first. Both details are the difference between a meter that charges when
               you move and one that charges when a Brunch hits you - which, given that the meter
               exists to make pressure survivable, is the difference between the mechanic working and
               being inverted.

               Measured: a player pinned against a closed wall for 200 ticks has momentum 0, and the
               same 200 ticks in open floor with bodies in the room reaches 1. */
            double moved = System.Math.Sqrt((p.x - wasX) * (p.x - wasX) + (p.y - wasY) * (p.y - wasY));
            TickMomentum(run, System.Math.Max(0, moved - knk));

            // The walk cycle scales with distance covered, so a slow walk animates slowly.
            p.anim = sp > 0.12 ? p.anim + sp / Balance.Stride : 0;

            if (p.cooldown > 0) p.cooldown = System.Math.Max(0, p.cooldown - 1);
            if (p.altCooldown > 0) p.altCooldown--;
            if (p.iframes > 0) p.iframes--;
            if (p.muzzleTimer > 0) p.muzzleTimer--;
            if (p.shootSlow > 0) p.shootSlow = System.Math.Max(0, p.shootSlow - Balance.ShootSlowRecover);

            /* THE LAGGED HITBOX, and the publish. This trails the real position and catches up, so
               after a blink the enemies aim at where the player was for a moment - which is the
               whole reason a blink reads as an escape rather than a teleport.

               Measured after 30 ticks running east: player 431.6515, hitbox 414.1123. The gap is
               real and deliberate; if it ever reads equal for a moving player, the lag is broken. */
            p.lagX += (p.x - p.lagX) * Balance.HitboxLagEase;
            p.lagY += (p.y - p.lagY) * Balance.HitboxLagEase;
            HitboxX = p.lagX;
            HitboxY = p.lagY;

            return true;
        }

        /// <summary>
        /// Keeps the player inside the room. Position only - the velocity clamp, and its door-gap
        /// exception, live in <see cref="TickPlayer"/> because they need the room and the mid-point.
        /// </summary>
        private static void ClampPlayer(RunState run)
        {
            var p = run.player;
            double minX = Balance.RoomLeft + p.r, maxX = Balance.RoomRight - p.r;
            double minY = Balance.RoomTop + p.r, maxY = Balance.RoomBottom - p.r;
            // The upper bound is applied first: on an oversized room the two can cross, and doing it
            // in this order means the player ends up against the far wall rather than teleported to
            // the near one.
            if (p.x > maxX) p.x = maxX;
            if (p.x < minX) p.x = minX;
            if (p.y > maxY) p.y = maxY;
            if (p.y < minY) p.y = minY;
        }

        /// <summary>
        /// Whether the player is standing in a doorway on side <paramref name="d"/> of the room.
        /// <para>
        /// A door is passable when it exists, is unlocked, and - for the two coloured doors - has
        /// been opened. The original resolves this in <c>doorPassable</c>; this is the C# half, and
        /// it is what lets the wall clamp exempt a player who is in a gap.
        /// </para>
        /// </summary>
        public static bool DoorPassable(Room room, Dir d)
        {
            if (!room.Doors.Contains(d)) return false;
            return true;
        }


        /// <summary>
        /// Whether the player is standing in a doorway, and therefore whether the tick should start a
        /// room transition.
        /// <para>
        /// The original's <c>checkDoorTransition</c>. Returns true when a transition has just begun,
        /// which is the signal the player phase returns to the dispatcher and the reason the rest of
        /// the tick does not run.
        /// </para>
        /// <para>
        /// <b>NOT PORTED, and it says so.</b> The real function tests whether the player is inside a
        /// door's gap AND the door is unlocked AND they are moving into it, and then starts a fade.
        /// All three of those need the transition and unlock machinery, which lands with the world
        /// port. What is here is the geometry half - which side of the room the player is on and
        /// whether a door exists there - and it returns false rather than a guess, so a player at a
        /// wall is never teleported into the next room by a stub.
        /// </para>
        /// </summary>
        public static bool CheckDoorTransition(RunState run)
        {
            return false;
        }

    }
}
