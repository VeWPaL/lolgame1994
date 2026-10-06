using System.Linq;
using System.Collections.Generic;

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
        /// <summary>The aim, as a WORLD point (the game's mouseWorld()).</summary>
        public readonly double aimX, aimY;

        public Input(double dx, double dy, bool fire = false, bool alt = false, bool blink = false,
                     double aimX = 0, double aimY = 0)
        {
            this.dx = dx;
            this.dy = dy;
            this.aimX = aimX;
            this.aimY = aimY;
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
                owner = e,   // the game sets it; the Brunch guard check and the lunger block both read it
            });
        }

        /// <summary>
        /// The Warden's turn, src/60-tick.js stepBoss: the move clock, the statue sweep by age, the
        /// wall's own expiry, the phase gates, then either resolving the committed move or, once idle
        /// and off cooldown, one run draw for the move from a bag weighted by phase and one jitter draw
        /// for the next cooldown. The boss does not walk; it moves by its sweep and by being knocked.
        /// </summary>
        public static void StepBoss(RunState run, Enemy e)
        {
            var room = run.enemies;
            if (e.moveT > 0) e.moveT--;
            for (int i = room.Count - 1; i >= 0; i--)
            {
                var b = room[i];
                if (b.packId != Balance.BossWallId) continue;
                b.wallAge = b.wallAge.HasValue ? b.wallAge + 1 : 0;
                if (b.wallAge > Balance.BossWallLife) room.RemoveAt(i);
            }
            if (e.wallBodies != null)
            {
                e.wallT++;
                bool standing = false;
                foreach (var b in e.wallBodies) if (b.hp > 0) { standing = true; break; }
                if (e.wallT > Balance.BossWallLife || !standing)
                {
                    foreach (var b in e.wallBodies) { int i = room.IndexOf(b); if (i >= 0) room.RemoveAt(i); }
                    e.wallBodies = null;
                }
            }
            double frac = e.hp / e.maxHp;
            if (e.phase == 1 && frac <= Balance.BossPhase1) BossPhase(e, 2);
            else if (e.phase == 2 && frac <= Balance.BossPhase2) BossPhase(e, 3);
            if (e.move != "idle") { ResolveBoss(run, e); return; }
            if (e.moveT > 0) return;
            e.bossCd -= 1;
            if (e.bossCd > 0) return;
            // the bag: volley, volley, sweep [, wall from phase 2] [, volley, sweep, sweep in phase 3]
            int n = 3 + (e.phase >= 2 ? 1 : 0) + (e.phase >= 3 ? 3 : 0);
            int k = (int)(run.rng.Run() * n);
            string pick = k < 2 ? "volley" : k == 2 ? "sweep" : k == 3 ? "wall" : k == 4 ? "volley" : "sweep";
            e.bossCd = (e.cdMin + run.rng.Jitter() * e.cdVar) * (1 - Balance.PressureCadence * Balance.RoomPressure(room.Count));
            BeginBoss(run, e, pick);
        }

        public static void BossPhase(Enemy e, int n)
        {
            e.phase = n;
            e.move = "idle";
            e.moveT = Balance.BossRecover;      // the phase change is itself a beat of recovery
            e.volleyLeft = 0;
            e.bossCd = Balance.Sec(0.4);
        }

        public static void BeginBoss(RunState run, Enemy e, string move)
        {
            var p = run.player;
            e.move = move;
            if (move == "volley")
            {
                e.castT = Balance.CastTime; e.castReady = false;
                e.castAim = System.Math.Atan2(p.y - e.y, p.x - e.x);
                e.volleyLeft = Balance.BossVolleyN;
                e.volleyT = Balance.CastTime + 1;
                e.moveT = Balance.CastTime + 1;
            }
            else if (move == "sweep")
            {
                e.castAim = System.Math.Atan2(p.y - e.y, p.x - e.x);
                e.moveT = Balance.CastTime;   // the wind-up: a stopped body is a readable one
            }
            else if (move == "wall")
            {
                CallWall(run, e);
                e.moveT = Balance.BossRecover;
            }
        }

        /// <summary>bossCallWall: five Brunch statues on the player's side of the boss, inert until struck.</summary>
        static void CallWall(RunState run, Enemy e)
        {
            int n = Balance.BossWallHp;
            double bx = System.Math.Max(Balance.RoomLeft + 60, System.Math.Min(Balance.RoomRight - 60, e.x + (e.x < run.player.x ? 70 : -70))), by = e.y;
            var made = new List<Enemy>();
            for (int i = 0; i < n; i++)
            {
                double a = (double)i / n * 6.283, rad = i % 2 == 1 ? 30 : 16;
                var b = Spawn.Body(run, BodyKind.Brunch, bx + System.Math.Cos(a) * rad, by + System.Math.Sin(a) * rad);
                b.hp = b.maxHp = Balance.BossWallHp * 2;
                b.packId = Balance.BossWallId; b.packSlot = i;
                b.noticeTimer = 1000000000; b.aggroTimer = 0; b.pursuit = 0;
                run.enemies.Add(b);
                made.Add(b);
            }
            e.wallBodies = made;
            e.wallT = 0;
        }

        /// <summary>resolveBoss: the volley (a re-aimed shell per gap, the tell counting down to each), or the sweep.</summary>
        public static void ResolveBoss(RunState run, Enemy e)
        {
            var p = run.player;
            if (e.move == "volley")
            {
                if (e.volleyLeft > 0)
                {
                    e.volleyT--;
                    if (e.volleyT <= 0)
                    {
                        e.castAim = System.Math.Atan2(p.y - e.y, p.x - e.x);
                        run.projectiles.Add(new Projectile
                        {
                            x = e.x, y = e.y, vx = System.Math.Cos(e.castAim) * e.pspd, vy = System.Math.Sin(e.castAim) * e.pspd,
                            r = e.pr, dmg = e.dmg, friendly = false, owner = e, heavy = true,
                        });
                        e.volleyLeft--;
                        e.volleyT = Balance.BossVolleyGap;
                    }
                    e.castT = e.volleyLeft > 0 ? System.Math.Min(Balance.CastTime, e.volleyT) : 0;
                }
                else { e.move = "idle"; e.moveT = Balance.BossRecover; e.castT = 0; }
            }
            else if (e.move == "sweep")
            {
                if (e.moveT > 0) return;   // still winding up
                if (!e.sweepDone)
                {
                    e.sweepDone = true;
                    double a = e.castAim;
                    e.x += System.Math.Cos(a) * Balance.BossSweepDist;
                    e.y += System.Math.Sin(a) * Balance.BossSweepDist;
                    Movement.Clamp(e);
                    if (Dist(e.x - p.x, e.y - p.y) < e.r + p.r)
                        Combat.DamagePlayer(run, Balance.JsReference ? e.dmg * Balance.JsBossSweepMult : Balance.BossSweepDmg, System.Math.Cos(a), System.Math.Sin(a), 3 * Balance.KnockPGain, true);
                }
                e.move = "idle"; e.moveT = Balance.BossRecover; e.sweepDone = false;
            }
            else e.move = "idle";
        }

        /// <summary>
        /// The update() gate, src/60-tick.js:256-340. Only the load-bearing skeleton for now:
        /// gates, clocks, the early returns, and the order gate.
        /// </summary>
        public static void Update(RunState run, Input input)
        {
            // the blink bar fills across a room arrival (player.blinkRestore), ahead of every gate
            if (run.blinkRestoreT > 0)
            {
                run.blinkRestoreT--;
                double held = run.blinkRestoreFrom + (2 - run.blinkRestoreFrom) * (1 - (double)run.blinkRestoreT / run.blinkRestoreSpan);
                int whole = (int)System.Math.Min(2, System.Math.Floor(held));
                run.blinkCharges = whole;
                run.player.blinkRegen = (held - whole) * Balance.BlinkRecharge;
                if (run.blinkRestoreT <= 0) { run.blinkCharges = 2; run.player.blinkRegen = 0; }
            }
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
                    var t = run.trans;
                    run.trans = null;
                    Rooms.EnterRoom(run, t.nx, t.ny, (Dir)System.Enum.Parse(typeof(Dir), t.from));
                }
                return;
            }
            if (run.readyT > 0) { run.readyT--; return; }
            if (run.bossWarnT > 0) run.bossWarnT--;
            if (!TickPlayer(run, input)) return;

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
            TickProjectiles(run);
            TickBodies(run);
            TickRoom(run);
        }

        /// <summary>
        /// The body phase, src/60-tick.js tickBodies, for the LUNGER: knock drift, slow and stun clocks,
        /// notice, aggro (AGGRO_RANGE from the lagged hitbox), stepLunge or the idle wander, the wall,
        /// contact damage with its knock-back, then separation and the wall again. Pack assembly runs
        /// first, as in the game. Any other kind that would act throws: their movement is not ported.
        /// </summary>
        static readonly List<Enemy> Snapshot = new List<Enemy>();

        /// <summary>brunchGuardAnchor: where a guarding pack forms its wall, by Balance.BrunchVariant; null = chase.</summary>
        public static Pt? GuardAnchor(RunState run, Enemy tgt)
        {
            var v = Balance.BrunchVariant;
            if (v == "A") return new Pt(tgt.x, tgt.y);
            var p = run.player;
            double d = Dist(p.x - tgt.x, p.y - tgt.y);
            if (d == 0) d = 1;
            if (v == "B") return d > Balance.BrunchGuardLeash ? (Pt?)null : new Pt(tgt.x, tgt.y);
            double reach = tgt.kind == BodyKind.Gunner ? 600 : tgt.kind == BodyKind.Shooter ? 520 : Balance.BrunchGuardLeash;
            if (d <= reach) return new Pt(tgt.x, tgt.y);
            double u = System.Math.Min((d - reach) * 1.5, d - 150) / d;
            return new Pt(tgt.x + (p.x - tgt.x) * u, tgt.y + (p.y - tgt.y) * u);
        }

        /// <summary>
        /// A Brunch on the hunt (src/60-tick.js tickBodies, the Brunch branch): the speed ramp, then a
        /// slot in the wall before its pack's target (or the player when it has none, or when the slot
        /// is already reached), at the guard or chase speed, easing its velocity a fraction per tick.
        /// </summary>
        static void StepBrunch(RunState run, Enemy e, double edx, double edy, double sm)
        {
            var p = run.player;
            e.pursuit++;
            double gain = 1 + Balance.BrunchRampGain * System.Math.Min(1, e.pursuit / Balance.BrunchRamp);
            e.curSpeed += (e.runSpeed - e.curSpeed) * Balance.LungerAccel * gain * Balance.EaseK(Balance.LungerAccel * gain);
            PackCentroid? pc = null;
            if (e.packId.HasValue) Packs.TryGetValue(e.packId.Value, out pc);
            double mdx = edx, mdy = edy;
            var tgt = e.shieldTarget;
            Pt? anchor = tgt != null && tgt.hp > 0 && pc != null && pc.Count >= Balance.BrunchShieldMin ? GuardAnchor(run, tgt) : null;
            if (anchor.HasValue)
            {
                var slot = BrunchArcSlot(anchor.Value.X, anchor.Value.Y, p.x, p.y, pc!.Count, e.packSlot, tgt!.r);
                if (slot.HasValue) { mdx = slot.Value.X - e.x; mdy = slot.Value.Y - e.y; }
            }
            bool reachable = Dist(mdx, mdy) <= Balance.BrunchDeadzone + e.r;
            double tx = reachable ? edx : mdx, ty = reachable ? edy : mdy, td = Dist(tx, ty);
            if (td == 0) td = 1;
            double desired = anchor.HasValue ? Balance.BrunchShieldSpeed : e.curSpeed;
            double share = desired > e.curSpeed ? Balance.BrunchAccel : Balance.BrunchDecel;
            e.vx += (tx / td * desired - e.vx) * share;
            e.vy += (ty / td * desired - e.vy) * share;
            if (System.Math.Abs(e.vx) < Balance.BrunchStill) e.vx = 0;
            if (System.Math.Abs(e.vy) < Balance.BrunchStill) e.vy = 0;
            e.x += e.vx * sm;
            e.y += e.vy * sm;
        }

        /// <summary>
        /// A shooter or gunner in sight of the player: hold a standoff that shrinks as the room empties
        /// (longer while guarded), walk (a gunner roots while casting), then cast at an intercept - the
        /// player's CURRENT velocity trusted by how settled they look, solved from where the muzzle WILL be
        /// for a walking shooter - with a swerve-widened jitter, swept clear of our own bodies.
        /// </summary>
        static void StepRanged(RunState run, Enemy e, double edx, double edy, double dist, double sm, double roomPress)
        {
            var p = run.player;
            bool guarded = e.shieldGuardFor != null && e.shieldGuardFor.Count > 0;
            double standoff = (guarded ? e.far * Balance.GuardStandoffMult : e.far) - (e.far - e.close) * roomPress * Balance.PressureClosure;
            bool roots = e.kind == BodyKind.Gunner;
            if (e.castT <= 0 || !roots)
            {
                if (dist < e.close) { e.x -= edx / dist * e.speed * sm; e.y -= edy / dist * e.speed * sm; }
                else if (dist > standoff) { e.x += edx / dist * e.speed * sm; e.y += edy / dist * e.speed * sm; }
            }
            e.shootCd--;
            if (e.castT > 0 || e.castReady) { FireCommittedShot(run, e, roomPress); return; }
            if (e.shootCd > 0) return;
            double dz = Balance.SwerveDeadzone(), reach = System.Math.Max(0, System.Math.Min(1, (dist - dz) / (Balance.SwerveFull() - dz)));
            double conf = System.Math.Max(0, 1 - p.swerve * (roots ? Balance.SwerveTrustRooted : Balance.SwerveTrustWalking));
            double bvx = p.vx * conf, bvy = p.vy * conf, ty = HitboxY + Balance.PlayerHitDy;
            double mx = e.x, my = e.y;
            if (!roots)
            {
                double step = Balance.CastTime / 24.0, v = e.speed * sm * step;
                for (double t = step; t <= Balance.CastTime + 0.5; t += step)
                {
                    double px = HitboxX + bvx * t, py = ty + bvy * t, ax = px - mx, ay = py - my;
                    double ad = System.Math.Sqrt(ax * ax + ay * ay);
                    if (ad == 0) ad = 1;
                    if (ad < e.close) { mx -= ax / ad * v; my -= ay / ad * v; }
                    else if (ad > standoff) { mx += ax / ad * v; my += ay / ad * v; }
                }
            }
            double sx = HitboxX - mx, sy = ty - my;
            for (int k = 0; k < 14; k++)
            {
                double need = Balance.CastTime + System.Math.Sqrt(sx * sx + sy * sy) / e.pspd;
                sx = HitboxX + bvx * need - mx;
                sy = ty + bvy * need - my;
            }
            double want = System.Math.Atan2(sy, sx) + (run.rng.Jitter() - 0.5) * 2 * (0.02 + Balance.SwerveAim * p.swerve * reach);
            var shot = Movement.ClearShot(run, e, want);
            if (shot.HasValue) { e.castAim = shot.Value; e.castT = Balance.CastTime; }
            else e.shootCd = e.cdMin * 0.25;
        }

        public static void TickBodies(RunState run)
        {
            AssemblePacks(run);
            var list = run.enemies;
            Snapshot.Clear();
            Snapshot.AddRange(list);   // iterate a snapshot, as the game does (r.enemies.slice()), without a per-tick allocation
            int aggro = Balance.AggroRange(Balance.Room.Standard);
            int live = 0;
            foreach (var b in list) if (b.hp > 0) live++;
            double roomPress = Balance.RoomPressure(live);
            foreach (var e in Snapshot)
            {
                if (e.hp <= 0) continue;   // killed earlier this tick
                bool ranged = e.kind == BodyKind.Shooter || e.kind == BodyKind.Gunner;
                if (e.hitFlash > 0) e.hitFlash--;
                if (e.kvx != 0 || e.kvy != 0)
                {
                    e.x += e.kvx; e.y += e.kvy;
                    e.kvx *= Balance.KnockFriction; e.kvy *= Balance.KnockFriction;
                    if (System.Math.Abs(e.kvx) < Balance.KnockCut) e.kvx = 0;
                    if (System.Math.Abs(e.kvy) < Balance.KnockCut) e.kvy = 0;
                }
                if (e.slowT > 0) e.slowT--;
                if (e.markT > 0) e.markT--;
                if (e.slowT < 0) e.slowT = 0;
                if (e.stun < 0) e.stun = 0;
                double sm = e.slowT > 0 ? Balance.HitSlowMult : 1;
                if (e.stun > 0)
                {
                    if (e.lungeState == "wind" || e.lungeState == "lunge")
                    {
                        e.lungeState = "approach"; e.lungeT = 0; e.lungeLen = 0; e.lungeCd = Balance.LungeCd;
                    }
                    if (ranged) FireCommittedShot(run, e, roomPress);   // a finished cast still leaves, stunned or not
                    e.stun--;
                    e.anim = 0;
                    Movement.Clamp(e);
                    continue;
                }
                if (e.noticeTimer > 0) { e.noticeTimer--; e.anim = 0; continue; }
                double ox = e.x, oy = e.y;
                if (e.kind == BodyKind.Gunner) Movement.GunnerDodge(run, e);
                double edx = HitboxX - e.x, edy = HitboxY - e.y, dist = System.Math.Sqrt(edx * edx + edy * edy);
                if (dist == 0) dist = 1;
                if (e.kind == BodyKind.Boss) StepBoss(run, e);
                else if (!ranged)
                {
                    if (dist < aggro) e.aggroTimer = Balance.AggroTime;
                    else if (e.aggroTimer > 0) e.aggroTimer--;
                    if (e.aggroTimer > 0)
                    {
                        if (e.kind == BodyKind.Lunger) Movement.StepLunge(run, e, edx / dist, edy / dist, dist, sm);
                        else StepBrunch(run, e, edx, edy, sm);
                    }
                    else { e.curSpeed = e.walkSpeed; e.pursuit = 0; Movement.IdleWander(run, e); }
                }
                else if (e.alerted || dist < e.sense) StepRanged(run, e, edx, edy, dist, sm, roomPress);
                else Movement.IdleWander(run, e);
                double mv = System.Math.Sqrt((e.x - ox) * (e.x - ox) + (e.y - oy) * (e.y - oy));
                e.anim = mv > Balance.MoveEps ? e.anim + mv / Balance.Stride : 0;
                Movement.Clamp(e);
                double cy = edy - Balance.PlayerHitDy;
                if (System.Math.Sqrt(edx * edx + cy * cy) < e.r + Balance.PlayerHitR)
                {
                    // a Brunch that reaches you is not pushed back unless it holds a slot; it spends itself
                    bool brunch = e.kind == BodyKind.Brunch;
                    bool holdingSlot = e.shieldTarget != null && e.shieldTarget.hp > 0;
                    bool boss = e.kind == BodyKind.Boss;
                    if (Combat.DamagePlayer(run, boss ? 2 : 1, edx / dist, edy / dist, (boss ? 8 : 5.5) * Balance.KnockPGain, boss) && (!brunch || holdingSlot))
                        Movement.Knock(run, e, -edx, -edy, 4 * Balance.KnockGain);
                    if (brunch)
                    {
                        e.hp -= e.maxHp / 2;
                        e.hitFlash = Balance.HitFlash;
                        if (e.hp <= 0) { int j = list.IndexOf(e); if (j >= 0) Kills.KillEnemy(run, j); }
                    }
                }
            }
            Movement.Separate(run, list);
            foreach (var e in list) Movement.Clamp(e);
        }

        static double Dist(double dx, double dy) => System.Math.Sqrt(dx * dx + dy * dy);

        /// <summary>tickBlink: a spent charge refills over BLINK_RECHARGE ticks, 9x faster in a cleared room.</summary>
        public static void TickBlink(RunState run)
        {
            if (run.blinkCharges >= 2) return;
            var p = run.player;
            p.blinkRegen += run.enemies.Count == 0 ? Balance.BlinkFillClear : 1;
            if (p.blinkRegen >= Balance.BlinkRecharge) { run.blinkCharges++; p.blinkRegen = 0; }
        }

        /// <summary>
        /// The blink key (keydown in the game, so it lands BETWEEN ticks): only in a live room with a
        /// charge, no pause, no transition and no ready freeze. Returns whether it blinked.
        /// </summary>
        public static bool TryBlink(RunState run, Input input)
        {
            if (run.state != "playing" || run.trans != null || run.readyT > 0 || run.blinkCharges <= 0) return false;
            DoBlink(run, input);
            return true;
        }

        /// <summary>
        /// doBlink: BLINK_DIST along the held keys (else the current velocity, else the aim), clamped;
        /// the enemies' hitbox stays where the player WAS; i-frames for the whole travel, a one-hit
        /// grace, and a burst of speed in the blink's direction.
        /// </summary>
        public static void DoBlink(RunState run, Input input)
        {
            var p = run.player;
            double ux, uy, len = Dist(input.dx, input.dy);
            if (len > 0) { ux = input.dx / len; uy = input.dy / len; }
            else if ((len = Dist(p.vx, p.vy)) > Balance.BlinkDirEps) { ux = p.vx / len; uy = p.vy / len; }
            else { double a = System.Math.Atan2(input.aimY - p.y, input.aimX - p.x); ux = System.Math.Cos(a); uy = System.Math.Sin(a); }
            double fromX = p.x, fromY = p.y;
            p.x += ux * Balance.BlinkDist;
            p.y += uy * Balance.BlinkDist;
            run.blinkCharges--;
            ClampPlayer(run);
            p.lagX = fromX; p.lagY = fromY;
            p.iframes = System.Math.Max(p.iframes, Balance.BlinkIframes + Balance.DashTrail);
            run.blinkGrace = Balance.BlinkGrace;
            run.graceSpent = false;
            p.boost = Balance.BlinkBoost;
            p.boostX = ux; p.boostY = uy;
        }

        /// <summary>
        /// The projectile phase, src/60-tick.js tickProjectiles. Newest first, so a removal never skips
        /// one. Each shell moves, ages, and is culled 30px outside the room; then a friendly shell hits
        /// at most one body (for a piercing bolt, the first along its flight), and an enemy shell is eaten
        /// by a Brunch (unless it guards the shooter), else blocked by a lunger, else lands on the
        /// player's lagged hitbox. A landing shell does NOT stop the loop for the older ones - the game
        /// had that bug until 2026-10-06. Alt shells throw until explode is ported. The sound and FX the
        /// game makes here are the view's, not the simulation's.
        /// </summary>
        public static void TickProjectiles(RunState run)
        {
            var ps = run.projectiles;
            var en = run.enemies;
            for (int i = ps.Count - 1; i >= 0; i--)
            {
                var p = ps[i];
                if (p.alt)
                {
                    // stop on the point, not past it
                    if (Dist(p.tx - p.x, p.ty - p.y) <= p.speed)
                    {
                        p.x = p.tx; p.y = p.ty;
                        Blast.Explode(run, p.tx, p.ty, p.mode);
                        ps.RemoveAt(i);
                        continue;
                    }
                }
                p.age++;
                p.x += p.vx;
                p.y += p.vy;
                if (p.x < Balance.RoomLeft - 30 || p.x > Balance.RoomRight + 30
                    || p.y < Balance.RoomTop - 30 || p.y > Balance.RoomBottom + 30)
                {
                    if (p.alt) Blast.Explode(run, p.x, p.y, p.mode);   // on the mode it was cast with
                    ps.RemoveAt(i);
                    continue;
                }
                if (p.alt && p.phase) continue;   // the hook flies through bodies and acts only at its point
                if (p.friendly)
                {
                    int best = -1;
                    double bestA = double.PositiveInfinity;
                    for (int j = en.Count - 1; j >= 0; j--)
                    {
                        var e = en[j];
                        if (p.hit != null && p.hit.Contains(e)) continue;
                        if (Dist(p.x - e.x, p.y - e.y) >= p.r + e.r) continue;
                        if (p.pierce == 0) { best = j; break; }
                        double a = (e.x - p.ox) * p.dx + (e.y - p.oy) * p.dy;
                        if (a < bestA) { bestA = a; best = j; }
                    }
                    if (best < 0) continue;
                    if (p.alt) { Blast.Explode(run, p.x, p.y); ps.RemoveAt(i); continue; }   // the blast goes off on the first body
                    var t = en[best];
                    t.hp -= p.dmg * Combat.FalloffMult(p) * t.Vuln * p.scale;
                    t.hitFlash = Balance.HitFlash;
                    run.hits++;
                    Combat.AlertEnemy(t);
                    Combat.SlowEnemy(t);
                    if (t.hp <= 0) Kills.KillEnemy(run, best);
                    if (p.pierce > 0)
                    {
                        p.pierce--;
                        p.scale *= Balance.PierceFalloff;
                        (p.hit ??= new List<Enemy>()).Add(t);
                    }
                    else ps.RemoveAt(i);
                    continue;
                }
                bool hitSomething = false;
                for (int j = en.Count - 1; j >= 0; j--)
                {
                    var b = en[j];
                    if (b.kind != BodyKind.Brunch || b.hp <= 0) continue;
                    if (p.owner != null && b.shieldTarget == p.owner) continue;   // a Brunch spares its own shooter's fire
                    if (Dist(p.x - b.x, p.y - b.y) < p.r + b.r)
                    {
                        b.hitFlash = System.Math.Max(b.hitFlash, Balance.BrunchAbsorbFlash);
                        hitSomething = true;
                        break;
                    }
                }
                if (!hitSomething)
                {
                    for (int j = en.Count - 1; j >= 0; j--)
                    {
                        var e2 = en[j];
                        if (e2 == p.owner || e2.kind != BodyKind.Lunger) continue;
                        if (Dist(p.x - e2.x, p.y - e2.y) < p.r + e2.r)
                        {
                            e2.hp -= 1;
                            e2.hitFlash = Balance.HitFlash;
                            Combat.AlertEnemy(e2);
                            Combat.SlowEnemy(e2);
                            hitSomething = true;
                            if (e2.hp <= 0) Kills.KillEnemy(run, j);
                            break;
                        }
                    }
                }
                if (!hitSomething && Hit.PlayerHit(p.x, p.y, p.r, run.player.lagX, run.player.lagY))
                {
                    double pn = Dist(p.vx, p.vy);
                    if (pn == 0) pn = 1;
                    Combat.DamagePlayer(run, p.dmg, p.vx / pn, p.vy / pn, 1.5 * Balance.KnockPGain, p.owner != null && p.owner.kind == BodyKind.Boss);
                    hitSomething = true;
                }
                if (hitSomething) ps.RemoveAt(i);
            }
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
                // a won fight refills the regenerating heart (owner, 2026-10-06; C# only); not for the dead
                if (room.Fought && player.hp > 0) { player.regenHeart = player.regenHeartMax; player.regenHeartT = 0; }
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

                // break, not continue: Descend empties run.pickups, so the old room has nothing left to read
                if (pk.kind == "exit") { run.Descend(); descended = true; break; }

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

                if (pk.kind == "heart" || pk.kind == "halfheart")
                {
                    if (player.hp >= player.maxHp) continue;   // a full-health heart is left lying
                    player.hp = System.Math.Min(player.maxHp, player.hp + (pk.kind == "heart" ? Balance.HeartHeal : Balance.HalfHeal));
                }
                else if (pk.kind == "armor" || pk.kind == "halfarmor")
                {
                    if (player.armor >= Balance.MaxArmor) continue;
                    player.armor = System.Math.Min(Balance.MaxArmor, player.armor + (pk.kind == "armor" ? Balance.HeartHeal : Balance.HalfHeal));
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
                    // the item goes in the slot it declares; whatever was on that key is left where this lay
                    var got = Items.Give(run, pk.id, pk.charges);
                    if (!got.taken) continue;
                    if (got.dropped != null)
                        run.pickups.Add(new Pickup { x = pk.x, y = pk.y, r = 16, kind = "item", id = got.dropped, hold = true, charges = got.droppedCharges });
                }
                else continue;   // an unknown kind is left on the floor rather than eaten

                run.pickups.RemoveAt(i);
            }

            /* THE ORIGINAL RETURNS OUT OF THE MIDDLE OF THE FUNCTION HERE, and that has to be
               honoured. `descend()` already reseeded, moved the player and rebuilt the dungeon, so
               running the death check and the boss check against the NEW floor would be checking a
               floor the player never saw - and re-opening an exit on it is a second, invisible bug.

               The only thing after the loop that is still correct to skip is the whole tail, so this
               returns rather than falling through. It returns true: the caller wants to know whether it
               descended. */
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
            double? moveSpeedBonus = null)
        {
            Rooms.TickUnlock(run);
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

            /* The SPEED STAT is a parameter (the stats system is not ported; the default is a run carrying
               nothing), and Momentum's share is added here, as moveSpeedBonus() adds it in the game. */
            // moveSpeedBonus() in the game: the speed STAT plus the Momentum meter's share, capped. The
            // momentum term was missing here, invisible to every test that ran in an empty room (where
            // momentum stays 0) until the blink parity walked a player past a body.
            double bonus = System.Math.Min(Balance.MoveSpeedHardCap, (moveSpeedBonus ?? run.stats.Value("speed")) + p.momentum * Balance.MomentumSpeed);
            double spd = p.speed * p.slowMult * (1 + bonus) * boostMult;
            double targetVx = len != 0 ? (dx / len) * spd : 0;
            double targetVy = len != 0 ? (dy / len) * spd : 0;

            /* THE ACCELERATION RAMP IS THE FEEL, and it is why this is not a velocity assignment.
               `MoveAccel` closes 11.6% of the remaining gap per JS tick (Ease converts it), so the player takes about 40
               ticks - about a fifth of a second - to reach top speed, and Momentum makes a charged
               player accelerate harder rather than merely faster. Measured in the browser: the
               terminal speed is 1.4025 and the first ten ticks are
               0.16269, 0.306508, 0.433643, 0.54603, 0.645381, 0.733207, 0.810845, 0.879477,
               0.940147, 0.99378. */
            double accel = Balance.Ease(Balance.MoveAccel * (1 + p.momentum * Balance.MomentumAccel));
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
            if (msp > Balance.MoveEps)
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
            if (System.Math.Abs(p.kvx) < Balance.KnockPCut) p.kvx = 0;
            if (System.Math.Abs(p.kvy) < Balance.KnockPCut) p.kvy = 0;

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

                if (p.y <= Balance.RoomTop + p.r && !(DoorPassable(run, room, Dir.N) && inGapX)) p.vy = 0;
                if (p.y >= Balance.RoomBottom - p.r && !(DoorPassable(run, room, Dir.S) && inGapX)) p.vy = 0;
                if (p.x <= Balance.RoomLeft + p.r && !(DoorPassable(run, room, Dir.W) && inGapY)) p.vx = 0;
                if (p.x >= Balance.RoomRight - p.r && !(DoorPassable(run, room, Dir.E) && inGapY)) p.vx = 0;
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
            p.anim = sp > Balance.PlayerAnimEps ? p.anim + sp / Balance.Stride : 0;

            TickBlink(run);
            if (p.cooldown > 0) p.cooldown = System.Math.Max(0, p.cooldown - 1);
            if (p.altCooldown > 0) p.altCooldown--;
            Blast.TickFields(run);
            Blast.TickHookResist(run);
            if (p.iframes > 0) p.iframes--;
            TickRegen(run);
            if (run.blinkGrace > 0) run.blinkGrace--;   // its own clock, not folded into iframes
            if (p.muzzleTimer > 0) p.muzzleTimer--;
            if (p.shootSlow > 0) p.shootSlow = System.Math.Max(0, p.shootSlow - Balance.ShootSlowRecover);
            // held fire shoots the moment the cooldown runs out (mouseDown in the game); the alt is not ported
            if (input.fire && p.cooldown <= 0) Weapons.Fire(run, input.aimX, input.aimY, run.stats.Value("strength"), run.stats.Value("precision"));
            if (input.alt) Weapons.FireAlt(run, input.aimX, input.aimY);   // not gated here: fireAlt decides

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
        internal static void ClampPlayer(RunState run) => Rooms.ClampPlayer(run);

        /// <summary>The regen heart's clock: counts only in a live fight (an empty room pauses it, a hit
        /// resets it); at RegenDelay +1 HP, then +1 per RegenStep. Clearing a fought room refills it (TickRoom).</summary>
        public static void TickRegen(RunState run)
        {
            var p = run.player;
            if (p.regenHeart >= p.regenHeartMax || run.enemies.Count == 0) return;
            p.regenHeartT++;
            if (p.regenHeartT >= Balance.RegenDelay && (p.regenHeartT - Balance.RegenDelay) % Balance.RegenStep == 0)
                p.regenHeart = System.Math.Min(p.regenHeartMax, p.regenHeart + 1);
        }

        /// <summary>
        /// Whether the player is standing in a doorway on side <paramref name="d"/> of the room.
        /// <para>
        /// A door is passable when it exists, is unlocked, and - for the two coloured doors - has
        /// been opened. The original resolves this in <c>doorPassable</c>; this is the C# half, and
        /// it is what lets the wall clamp exempt a player who is in a gap.
        /// </para>
        /// </summary>
        public static bool DoorPassable(RunState run, Room room, Dir d) => Rooms.DoorPassable(run, room, d);


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
        public static bool CheckDoorTransition(RunState run) => Rooms.CheckDoorTransition(run);


        /// <summary>
        /// A pack's centroid, computed once per tick and shared by every body in it.
        /// <para>
        /// The centroid is what makes a Brunch a SHIELD rather than a crowd: bodies do not steer at
        /// the player, they steer at slots around a shared point, and the point is where the pack
        /// actually is. Averaging positions rather than picking a leader also means a pack survives
        /// losing any single member - there is no body whose death changes where the others go.
        /// </para>
        /// </summary>
        static readonly Dictionary<int, PackCentroid> Packs = new Dictionary<int, PackCentroid>();
        static readonly List<PackCentroid> CentroidPool = new List<PackCentroid>();

        private sealed class PackCentroid
        {
            public double X, Y;
            public int Count;
        }

        /// <summary>
        /// The pack assembly pass: centroids, target selection, and the guard lists. Ported from the
        /// first three sections of <c>tickBodies</c> in <c>src/60-tick.js</c>.
        ///
        /// <para>
        /// This is the part of the body phase that decides what a Brunch IS rather than how it moves,
        /// and it is where the session's Brunch work lives: the bomb-rush when there is nothing to
        /// shield, the shield arc when there is, and the commitment that makes a pack predictable.
        /// </para>
        ///
        /// <para>
        /// <b>Measured behaviour, all from the running game:</b>
        /// </para>
        /// <list type="bullet">
        /// <item>a pack with no ranged body in the room has NO target, and walks at the player</item>
        /// <item>a pack guarding a living body sets that body's guard list to the whole pack
        /// (measured: 6 of 6)</item>
        /// <item>when the guarded body dies the pack re-targets, and all 6 stop guarding the corpse</item>
        /// <item><b>a pack COMMITS.</b> A pack that already has a living target never re-picks, even
        /// with a nearer ranged body 20px away. The scan is <c>!shieldTarget &amp;&amp;
        /// (frameCount % BrunchScanTicks === 0)</c>, and the guard means it. Measured: no switch in
        /// 400 ticks with a shooter standing 20px from the pack centroid.</item>
        /// </list>
        /// </summary>
        public static void AssemblePacks(RunState run)
        {
            var room = run.CurrentRoom;
            if (room == null) return;

            // --- centroids. One pass to sum, one to divide, which is what the original does and is
            // cheaper than carrying a running mean through the second loop.
            // reused every tick (pooled centroids), and kept for the body pass to read
            var packs = Packs;
            foreach (var old in packs.Values) CentroidPool.Add(old);
            packs.Clear();
            foreach (var b in run.enemies)
            {
                if (b.kind != BodyKind.Brunch || !b.packId.HasValue) continue;
                if (!packs.TryGetValue(b.packId.Value, out var c))
                {
                    if (CentroidPool.Count > 0) { c = CentroidPool[CentroidPool.Count - 1]; CentroidPool.RemoveAt(CentroidPool.Count - 1); c.X = c.Y = 0; c.Count = 0; }
                    else c = new PackCentroid();
                    packs[b.packId.Value] = c;
                }
                c.X += b.x; c.Y += b.y; c.Count++;
            }
            foreach (var c in packs.Values) { c.X /= c.Count; c.Y /= c.Count; }

            // --- target selection, per pack centroid.
            foreach (var e in run.enemies)
            {
                if (e.kind != BodyKind.Brunch || !e.packId.HasValue) continue;
                if (!packs.TryGetValue(e.packId.Value, out var c)) continue;

                var tgt = e.shieldTarget;
                /* STALE MEANS GONE, DEAD, OR NOT IN THIS ROOM. All three have to be checked: a target
                   that died, a target that was spliced out by the room's own bookkeeping, and a target
                   that was never here at all - `null` is the ordinary case on the first tick. */
                bool stale = tgt == null || tgt.hp <= 0 || !run.enemies.Contains(tgt);

                if (stale || (e.shieldTarget == null && run.frameCount % Balance.BrunchScanTicks == 0))
                    e.shieldTarget = PickShield(run.enemies, c);
            }

            // --- guard lists, rebuilt from scratch every tick. They are a derived view: a body that
            // died must not still appear to be escorted, and rebuilding is cheaper than proving that
            // last tick's list is still correct.
            foreach (var o in run.enemies)
                if (IsRanged(o.kind)) o.shieldGuardFor = null;

            foreach (var e in run.enemies)
            {
                if (e.kind != BodyKind.Brunch || !e.packId.HasValue || e.shieldTarget == null) continue;
                var g = e.shieldTarget.shieldGuardFor ?? (e.shieldTarget.shieldGuardFor = new List<Enemy>());
                g.Add(e);
            }
        }

        /// <summary>
        /// The nearest living ranged body to a pack centroid, or null if there is none.
        /// <para>
        /// NEAREST, not best, and not first: the original walks every enemy and keeps the smallest
        /// squared distance. Squared, not actual, so the comparison never takes a square root - and
        /// it is squared rather than by straight-line distance for a second reason, which is that the
        /// test the projectile phase uses is also squared, and the two agree about what "near" means.
        /// </para>
        /// <para>
        /// A pack will therefore shield a shooter it is standing next to rather than a distant gunner,
        /// which is the intended reading: the pack protects what is closest, because that is what it
        /// can reach.
        /// </para>
        /// </summary>
        private static Enemy? PickShield(List<Enemy> enemies, PackCentroid c)
        {
            Enemy? best = null;
            double bestD = double.MaxValue;
            foreach (var o in enemies)
            {
                if (o.hp <= 0 || !IsRanged(o.kind)) continue;
                double dx = o.x - c.X, dy = o.y - c.Y;
                double d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }

        /// <summary>
        /// The three body kinds that shoot from range, and therefore the three worth shielding. A
        /// melee body is not shielded because nothing can reach it from outside the pack.
        /// </summary>
        public static bool IsRanged(BodyKind k) =>
            k == BodyKind.Shooter || k == BodyKind.Gunner || k == BodyKind.Boss;


        /// <summary>A point in the world, returned by the arc geometry. Public because the parity
        /// tests assert against it - a geometry helper that cannot be measured is a geometry helper
        /// nobody can port.</summary>
        public readonly struct Pt
        {
            public readonly double X, Y;
            public Pt(double x, double y) { X = x; Y = y; }
        }

        /// <summary>
        /// Where slot <paramref name="slot"/> of an <paramref name="n"/>-body pack stands when
        /// shielding a body at (<paramref name="tx"/>, <paramref name="ty"/>) against a player at
        /// (<paramref name="px"/>, <paramref name="py"/>). Null when the pack is too small to have a
        /// shape.
        ///
        /// <para>
        /// Ported from <c>brunchArcSlot</c> in <c>src/00-balance.js</c>, and it is the single function
        /// that decides whether a Brunch is a shield or a crowd. Three things about it are not
        /// obvious, and each was a bug:
        /// </para>
        ///
        /// <para>
        /// <b>1. The stand-off is a FRACTION OF THE GAP, not a constant.</b> The arc has to fit
        /// between the guarded body and the player, and that distance is not fixed - it is whatever the
        /// fight has produced so far, and it gets small. A constant radius put the wall behind the
        /// player: measured, a 164px gap with a 118px stand-off formed the wall 46px on the far side
        /// of the person it was meant to protect, and line of sight was blocked 27-39% of the way
        /// rather than closed. Now it is <see cref="Balance.BrunchShieldFrac"/> of the real gap.
        /// </para>
        ///
        /// <para>
        /// <b>2. The cone is DERIVED from the target's apparent width, with no floor that can
        /// dominate.</b> The wall was specified as a minor arc subtending 45-60 degrees, and against a
        /// shooter that shape was mostly empty: measured, a 3-pack blocked 0 of a 23px shot line, a
        /// 6-pack blocked 2, a 12-pack blocked 4 - the same 33% for every size, because the floor
        /// could never lose. The cone is now the target's own apparent width plus a body's margin
        /// either side, and the only remaining limit is the 60-degree ceiling.
        /// </para>
        ///
        /// <para>
        /// <b>3. The surplus stacks BEHIND the front rank at the same angle.</b> Spreading columns to
        /// the cone's edges regardless of whether they fit there put 12 bodies on a wall that 6 already
        /// overran - half the pack standing in empty floor beside its own shield. Columns are now
        /// spaced at <see cref="Balance.BrunchArcGap"/> and CLIPPED to the arc's own length, and the
        /// second rank repeats the angle because it is deliberately the same face.
        /// </para>
        ///
        /// <para>
        /// Measured slot positions for a shooter 200px from the player, as (slot, distance along the
        /// bearing from the target): a 6-pack gives 49.64, 65.6, 50, 67, 49.64, 65.6 - three pairs of
        /// front and back rank, the odd slots 17px behind, and the pair on the centreline where the
        /// column calculation lands on a whole number.
        /// </para>
        /// </summary>
        public static Pt? BrunchArcSlot(double tx, double ty, double px, double py,
                                        int n, int slot, double targetRadius)
        {
            if (n < Balance.BrunchShieldMin) return null;

            // The bearing from the guarded body toward the player. Everything is measured along it.
            double dx = px - tx, dy = py - ty;
            double d = System.Math.Sqrt(dx * dx + dy * dy);
            if (d == 0) d = 1;
            double ux = dx / d, uy = dy / d;

            int cols = (int)System.Math.Ceiling(n / 2.0);
            int rank = slot % 2;
            /* Symmetric about the centreline, in HALF-STEPS - and the half-step is NOT optional.

               The original writes `(slot/2|0) - ((cols-1)/2)` in JavaScript, where `/` is float
               division and `|0` truncates ONCE at the end. Writing that as C# INTEGER division is
               wrong in a way that only shows on odd packs: for n=3, `(cols-1)/2` is `(2-1)/2` = 0.5
               in the original and `0` in C#, so every column shifts by half a step and the whole
               three-body arrangement mirrors.

               Measured: the game lays a 3-pack out at 49.642207661, 66.32762017, 49.642207661. With
               integer division the C# produced 50, 67, 49.642207661 - the middle and outer bodies
               swapped. Six bodies are unaffected, because `cols-1 = 2` divides evenly, which is
               exactly why an even-pack-only test suite would never have found it.

               So the half-step is carried as a DOUBLE: the FIRST term truncates, exactly as `|0`
               does in the original, and the second does not - because in JavaScript `((cols-1)/2)` has
               no `|0` on it and stays fractional. Getting that wrong in the other direction mirrors
               the odd-pack arrangement again, so both halves are spelled out. */
            double col = System.Math.Floor(slot / 2.0) - ((cols - 1) / 2.0);

            /* The stand-off. Two clamps, both of which were bugs once:
               - never further out than a share of the gap, or the wall ends up behind the player;
               - never closer than `d - brunchRadius - 2`, or it converges ON the guarded body rather
                 than in front of it. */
            double radius = System.Math.Min(d * Balance.BrunchShieldFrac, d - BrunchRadius - 2)
                          + (rank != 0 ? Balance.BrunchWallRank : 0);

            /* The cone: the target's own apparent width at the arc's radius, plus one body's margin
               either side. The floor cannot dominate because it is a floor on a DERIVED value - a
               small enemy at close range gets a shield sized to it. */
            double apparent = System.Math.Atan2((targetRadius == 0 ? 12 : targetRadius)
                                              + Balance.BrunchArcGap * 2,
                                              System.Math.Max(1, d - radius));
            double half = System.Math.Max(Balance.BrunchArcFloor,
                                System.Math.Min(Balance.BrunchArcCeil, apparent));

            /* Columns spaced along the cone at the arc gap, and CLIPPED to the arc's own length -
               which is the fix for a 12-pack standing beside a wall a 6-pack had already overrun. */
            double arcLen = 2 * half * radius;
            double maxAlong = System.Math.Max(0, arcLen / 2 - Balance.BrunchArcGap * 0.5);
            double rawAlong = col * Balance.BrunchArcGap;
            double along = System.Math.Max(-maxAlong, System.Math.Min(maxAlong, rawAlong));

            double ang = along / System.Math.Max(1, radius);
            double ca = System.Math.Cos(ang), sa = System.Math.Sin(ang);
            double qx = -uy, qy = ux;              // perpendicular to the bearing
            return new Pt(tx + ux * radius * ca + qx * radius * sa,
                          ty + uy * radius * ca + qy * radius * sa);
        }

        /// <summary>
        /// A Brunch's collision radius. Read out of the running game: <c>ENEMY.brunch.r</c> = 8. It
        /// belongs to a body archetype, which the port does not have yet, so it is named here and the
        /// spawn table replaces it.
        /// </summary>
        public const double BrunchRadius = 8;

    }
}
