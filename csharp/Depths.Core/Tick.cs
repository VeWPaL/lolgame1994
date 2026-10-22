using System.Linq;
using System.Collections.Generic;

namespace Depths
{
    /// <summary>The input one tick reads. [h:Tick-1]</summary>
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

    /// <summary>The tick skeleton, in the original's load-bearing order. [h:Tick-2]</summary>
    public static class TickOrder
    {
        // smoothstep, src/10-art.js:516. Kept private: the only caller is the fade bookkeeping,
        // and a shared smooth() has a way of getting used for something else that then quantises.
        private static double Smooth(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);

        /// <summary>Charges Momentum on movement under pressure, src/60-tick.js:37-44. [h:Tick-3]</summary>
        public static void TickMomentum(RunState run, double moved)
        {
            // NO MOMENTUM IN AN EMPTY ROOM, and this guard was MISSING until the player phase was ported and its... [h:Tick-4]
            if (run.enemies.Count == 0) return;

            var player = run.player;
            player.momentum = moved > Balance.MomentumMoveFloor
                ? System.Math.Min(1, player.momentum + Balance.MomGain * moved)
                : System.Math.Max(0, player.momentum - Balance.MomStallDecay);
        }

        /// <summary>A cast that has finished LEAVES, src/60-tick.js:28-35. [h:Tick-5]</summary>
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
            if (Balance.TickHz != Balance.JsHz) { e.shootCd += e.cdCarry; e.cdCarry = 0; }
            if (Middle.Is(e.kind)) { Middle.Release(run, e); return; }
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

        /// <summary>The Warden's turn, src/60-tick.js stepBoss: the move clock, the statue sweep by age, the wall's own expiry... [h:Tick-6]</summary>
        public static void StepBoss(RunState run, Enemy e)
        {
            var room = run.enemies;
            if (e.moveT > 0) e.moveT--;
            for (int i = room.Count - 1; i >= 0; i--)
            {
                var b = room[i];
                if (b.packId != Balance.BossWallId) continue;
                b.wallAge = b.wallAge.HasValue ? b.wallAge + 1 : 0;
                if (b.wallAge > Balance.BossWallLife) { room.RemoveAt(i); if (Balance.Tuned) b.hp = 0; }
            }
            if (e.wallBodies != null)
            {
                e.wallT++;
                bool standing = false;
                foreach (var b in e.wallBodies) if (b.hp > 0) { standing = true; break; }
                if (e.wallT > Balance.BossWallLife || !standing)
                {
                    foreach (var b in e.wallBodies) { int i = room.IndexOf(b); if (i >= 0) room.RemoveAt(i); if (Balance.Tuned) b.hp = 0; }
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
            double bossOver = e.bossCd;
            e.bossCd = (e.cdMin + run.rng.Jitter() * e.cdVar) * (1 - Balance.PressureCadence * Balance.RoomPressure(room.Count));
            if (Balance.TickHz != Balance.JsHz) e.bossCd += bossOver;   // the overrun carried, as for a ranged body
            BeginBoss(run, e, pick);
        }

        public static void BossPhase(Enemy e, int n)
        {
            e.phase = n;
            e.move = "idle";
            e.moveT = Balance.BossRecover;      // the phase change is itself a beat of recovery
            e.volleyLeft = 0;
            if (Balance.Tuned) e.castT = 0;   // a volley cut by the phase change must not keep its warning flash
            e.bossCd = Balance.BossPhaseCd;
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
                // tuned: the statues read as a broken pack standing still, so they wake on a normal beat and guard the Warden
                if (Balance.Tuned && Balance.TuneWarden) { b.noticeTimer = Balance.Sec(0.43); b.aggroTimer = Balance.AggroTime; }
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

        /// <summary>The update() gate, src/60-tick.js:256-340. [h:Tick-7]</summary>
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

            // THE DEATH BACKSTOP. Checked on the way in, before any early return below can fire, and the lab is protected... [h:Tick-8]
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

            // THE ORDER GATE, AND THE ORDER WAS WRONG. [h:Tick-9]

            run.phaseLog.Clear();
            run.phaseLog.Add("player");
            run.phaseLog.Add("projectiles");
            run.phaseLog.Add("bodies");
            run.phaseLog.Add("room");

            // THE ROOM PHASE RUNS FOR REAL. [h:Tick-10]
            TickProjectiles(run);
            TickBodies(run);
            TickRoom(run);
        }

        /// <summary>The body phase, src/60-tick.js tickBodies, for the LUNGER: knock drift, slow and stun clocks, notice, aggro... [h:Tick-11]</summary>
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

        /// <summary>A Brunch on the hunt (src/60-tick.js tickBodies, the Brunch branch): the speed ramp, then a slot in the wall... [h:Tick-12]</summary>
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
            double reach = Balance.BrunchChaseCommit;
            if (anchor.HasValue)
            {
                var slot = BrunchArcSlot(anchor.Value.X, anchor.Value.Y, p.x, p.y, pc!.Count, e.packSlot, tgt!.r);
                if (slot.HasValue) { mdx = slot.Value.X - e.x; mdy = slot.Value.Y - e.y; }
                // tuned: a player inside the Scatter's reach and nearer than its slot is rushed, not walked past to a
                // wall across the room. A formed wall stays a wall: its slot is nearer than the player approaching it
                reach = System.Math.Min(Balance.CloseRange, Dist(mdx, mdy));
            }
            if (Balance.Tuned && Balance.TuneRush) { if (Rush(e, p, edx, edy, reach, sm)) return; }
            else if (e.rushing || e.rushWind > 0) CallOffRush(e);   // the switch turned off mid-attack
            bool reachable = Dist(mdx, mdy) <= Balance.BrunchDeadzone + e.r;
            double tx = reachable ? edx : mdx, ty = reachable ? edy : mdy, td = Dist(tx, ty);
            if (td == 0) td = 1;
            double desired = anchor.HasValue ? Balance.BrunchShieldSpeed : e.curSpeed;
            if (anchor.HasValue && Balance.Tuned && Balance.TuneWall && !reachable)   // tuned: the wall forms as the player walks in - fast, easing into the slot
                desired = System.Math.Max(Balance.BrunchShieldSpeed, System.Math.Min(Balance.BrunchGuardSpeed, td / Balance.BrunchGuardArrive));
            double share = desired > e.curSpeed ? Balance.BrunchAccel : Balance.BrunchDecel;
            if (anchor.HasValue && Balance.Tuned && Balance.TuneWall) share = Balance.BrunchDecel;   // and turns onto it as quickly as it stops
            e.vx += (tx / td * desired - e.vx) * share;
            e.vy += (ty / td * desired - e.vy) * share;
            if (System.Math.Abs(e.vx) < Balance.BrunchStill) e.vx = 0;
            if (System.Math.Abs(e.vy) < Balance.BrunchStill) e.vy = 0;
            e.x += e.vx * sm;
            e.y += e.vy * sm;
        }

        // tuned: a Brunch within `reach` of the player stops and trembles (the tell), dashes along the line it faced as the tell
        // ended, then skids to a stop; true while it does any of that. A player who backs out of reach calls the tell off
        static bool Rush(Enemy e, Player p, double edx, double edy, double reach, double sm)
        {
            edy += Balance.PlayerHitDy;   // aimed at where contact is tested (tuned: the shells' hitbox), not the hitbox's top
            double toPlayer = Dist(edx, edy), dash = DashSpeed(e);
            if (e.rushT > 0)
            {
                e.vx = e.rushDx * dash; e.vy = e.rushDy * dash;
                e.x += e.vx * sm; e.y += e.vy * sm;
                if (--e.rushT == 0) { e.rushing = false; e.rushRest = Balance.BrunchRushRest; }
                return true;
            }
            if (e.rushRest > 0) { e.rushRest--; Brake(e, sm); return true; }
            if (e.rushWind == 0)
            {
                if (toPlayer >= reach) return false;
                e.rushWind = Balance.BrunchRushWind;
                Brake(e, sm);
                return true;
            }
            if (toPlayer >= System.Math.Max(reach * Balance.BrunchTellHold, reach + Balance.BrunchRushRelease)) { e.rushWind = 0; return false; }
            Brake(e, sm);
            if (--e.rushWind > 0) return true;
            double d = toPlayer > 0 ? toPlayer : 1;
            e.rushDx = edx / d; e.rushDy = edy / d;
            // long enough to pass where the player will be: walking straight back is caught, a step off the line is not
            double away = System.Math.Min(System.Math.Max(0, p.vx * e.rushDx + p.vy * e.rushDy), dash * Balance.BrunchRetreatCap);
            double len = (toPlayer + Balance.BrunchDashOver) / (1 - away / dash);
            e.rushT = System.Math.Max(1, System.Math.Min(Balance.BrunchDashMax, (int)System.Math.Ceiling(len / dash)));
            e.rushing = true;
            return true;
        }

        /// <summary>A Brunch's dash speed in px a tick: its run x the gain, each pack slot a step slower.</summary>
        public static double DashSpeed(Enemy e) => e.runSpeed * Balance.BrunchDashGain * (1 - Balance.BrunchDashStagger * (e.packSlot % 4));

        // the tell or dash ends where it is: a fresh tell is needed, after the skid's rest
        static void CallOffRush(Enemy e)
        {
            e.rushWind = 0; e.rushT = 0; e.rushing = false;
            e.rushRest = Balance.BrunchRushRest;
        }

        static void Brake(Enemy e, double sm)
        {
            e.vx -= e.vx * Balance.BrunchDecel; e.vy -= e.vy * Balance.BrunchDecel;
            if (System.Math.Abs(e.vx) < Balance.BrunchStill) e.vx = 0;
            if (System.Math.Abs(e.vy) < Balance.BrunchStill) e.vy = 0;
            e.x += e.vx * sm; e.y += e.vy * sm;
        }

        /// <summary>A shooter or gunner in sight of the player: hold a standoff that shrinks as the room empties (longer while... [h:Tick-13]</summary>
        static void StepRanged(RunState run, Enemy e, double edx, double edy, double dist, double sm, double roomPress)
        {
            var p = run.player;
            int cast = Middle.CastTime(e.kind);   // Balance.CastTime for the shooter and gunner
            bool guarded = e.shieldGuardFor != null && e.shieldGuardFor.Count > 0;
            double standoff = (guarded ? e.far * Balance.GuardStandoffMult : e.far) - (e.far - e.close) * roomPress * Balance.PressureClosure;
            bool roots = e.kind == BodyKind.Gunner;
            if (Balance.Tuned && Balance.TuneEase)   // the same walk, but it glides into and out of it rather than snapping
            {
                double walk = e.castT > 0 && roots ? 0 : dist < e.close ? -1 : dist > standoff ? 1 : 0;
                double k = Balance.RangedWalkEase;
                e.vx += (edx / dist * e.speed * walk - e.vx) * k;
                e.vy += (edy / dist * e.speed * walk - e.vy) * k;
                e.x += e.vx * sm; e.y += e.vy * sm;
            }
            else if (e.castT <= 0 || !roots)
            {
                if (dist < e.close) { e.x -= edx / dist * e.speed * sm; e.y -= edy / dist * e.speed * sm; }
                else if (dist > standoff) { e.x += edx / dist * e.speed * sm; e.y += edy / dist * e.speed * sm; }
            }
            e.shootCd--;
            if (e.castT > 0 || e.castReady) { FireCommittedShot(run, e, roomPress); return; }
            if (e.shootCd > 0) return;
            double conf = System.Math.Max(0, 1 - p.swerve * (roots ? Balance.SwerveTrustRooted : Balance.SwerveTrustWalking));
            double bvx = p.vx * conf, bvy = p.vy * conf, ty = HitboxY + Balance.PlayerHitDy;
            double mx = e.x, my = e.y;
            if (!roots)
            {
                double step = cast / 24.0, v = e.speed * sm * step;
                for (double t = step; t <= cast + 0.5; t += step)
                {
                    double px = HitboxX + bvx * t, py = ty + bvy * t, ax = px - mx, ay = py - my;
                    double ad = System.Math.Sqrt(ax * ax + ay * ay);
                    if (ad == 0) ad = 1;
                    if (ad < e.close) { mx -= ax / ad * v; my -= ay / ad * v; }
                    else if (ad > standoff) { mx += ax / ad * v; my += ay / ad * v; }
                }
            }
            double aim;
            if (Balance.Tuned && Balance.TuneAim)
                aim = e.kind == BodyKind.Splitter
                    ? Aim.Intercept(mx, my, HitboxX, ty, bvx, bvy, cast, e.pspd, Middle.SplitRange, Middle.SplitShardSpeed)
                    : Aim.Intercept(mx, my, HitboxX, ty, bvx, bvy, cast, e.pspd);
            else
            {
                double sx = HitboxX - mx, sy = ty - my;
                for (int k = 0; k < Balance.GunIter; k++)
                {
                    double need = cast + System.Math.Sqrt(sx * sx + sy * sy) / e.pspd;
                    sx = HitboxX + bvx * need - mx;
                    sy = ty + bvy * need - my;
                }
                aim = System.Math.Atan2(sy, sx);
            }
            double want = aim + (run.rng.Jitter() - 0.5) * 2 * Aim.GunSpread(p.swerve, dist);
            var shot = e.kind == BodyKind.Mortar ? want : Movement.ClearShot(run, e, want);   // a lob goes over bodies
            // off the JS rate the tick's overrun is kept, so a cadence is exact in seconds (held fire does the same)
            double over = Balance.TickHz == Balance.JsHz ? 0 : e.shootCd;
            if (shot.HasValue) { e.castAim = shot.Value; e.castT = cast; e.cdCarry = over - Balance.CastLag; }
            else if (Balance.TickHz == Balance.JsHz) e.shootCd = e.cdMin * 0.25;
            else e.shootCd = e.cdMin * 0.25 + over;
        }

        public static void TickBodies(RunState run)
        {
            AssemblePacks(run);
            var list = run.enemies;
            run.slowSkip = false;
            if (run.timeSlowT > 0)
            {
                // Time Glass: the bodies run on a share of the ticks: their steps, timers, wind-ups and contact alike
                run.timeSlowT--;
                run.slowAcc += Items.TimeSlow;
                if (run.slowAcc < 1) { run.slowSkip = true; return; }
                run.slowAcc -= 1;
            }
            Snapshot.Clear();
            Snapshot.AddRange(list);   // iterate a snapshot, as the game does (r.enemies.slice()), without a per-tick allocation
            int aggro = Balance.AggroRange(Balance.Room.Standard);
            int live = 0;
            foreach (var b in list) if (b.hp > 0) live++;
            double roomPress = Balance.RoomPressure(live);
            foreach (var e in Snapshot)
            {
                if (e.hp <= 0) continue;   // killed earlier this tick
                e.lifeT++;
                bool ranged = e.kind == BodyKind.Shooter || e.kind == BodyKind.Gunner || Middle.Ranged(e.kind) || e.kind == BodyKind.Egg;
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
                double sm = (e.slowT > 0 ? Balance.HitSlowMult : 1) * (e.elite ? Spikes.EliteRate : 1);   // an elite moves quicker
                if (e.burnT > 0 && !e.buried)   // Ember Rounds: a point a second on its own phase while it burns
                {
                    e.burnT--;
                    if (--e.burnTick <= 0)
                    {
                        e.burnTick = Items.BurnEvery;
                        e.hp -= Items.BurnDmg; e.hitFlash = Balance.HitFlash;
                        if (e.hp <= 0) { int j = list.IndexOf(e); if (j >= 0) Kills.KillEnemy(run, j); continue; }
                    }
                }
                if (e.stun > 0)
                {
                    if (e.kind == BodyKind.SunkenKing) FinalBoss.StepFloor(run, e);   // the floor burns on, the King stunned or not
                    if (e.lungeState == "wind" || e.lungeState == "lunge" || e.lungeState == "dodge")
                    {
                        e.lungeState = "approach"; e.lungeT = 0; e.lungeLen = 0; e.lungeCd = Balance.LungeCd;
                    }
                    if (e.rushWind > 0 || e.rushT > 0) CallOffRush(e);   // a stun breaks a Brunch's tell or dash, as it does a lunge
                    if (ranged) FireCommittedShot(run, e, roomPress);   // a finished cast still leaves, stunned or not
                    e.stun--;
                    e.anim = 0;
                    Movement.Clamp(e);
                    continue;
                }
                if (Balance.Tuned && Balance.TuneDodge && e.kind == BodyKind.Lunger)   // the dodge comes before noticing, so a startled body can take it
                {
                    if (e.dodgeCd > 0) e.dodgeCd--;
                    if (e.lungeState == "dodge" || Movement.TryLungerDodge(run, e))
                    {
                        double bx = e.x, by = e.y;
                        Movement.StepDodge(e, sm);
                        Movement.Clamp(e);
                        e.anim += System.Math.Sqrt((e.x - bx) * (e.x - bx) + (e.y - by) * (e.y - by)) / Balance.Stride;
                        continue;   // no contact while dashing off a shot: it is leaving, not striking
                    }
                }
                if (e.noticeTimer > 0) { e.noticeTimer--; e.anim = 0; if (Eggs.Riding(e)) Eggs.Move(run, e); continue; }
                double ox = e.x, oy = e.y;
                if (e.kind == BodyKind.Gunner) Movement.GunnerDodge(run, e);
                double edx = HitboxX - e.x, edy = HitboxY - e.y, dist = System.Math.Sqrt(edx * edx + edy * edy);
                if (dist == 0) dist = 1;
                if (e.kind == BodyKind.Boss) StepBoss(run, e);
                else if (e.kind == BodyKind.Gatekeeper) FloorBoss.Step(run, e, edx, edy, dist, sm);
                else if (e.kind == BodyKind.SunkenKing) FinalBoss.Step(run, e, edx, edy, dist, sm);
                else if (e.kind == BodyKind.Egg) Middle.StepEgg(run, e, roomPress);
                else if (e.kind == BodyKind.Burrower) Middle.StepBurrower(run, e, edx, edy, dist, sm);
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
                double hitDy = Balance.Tuned ? -Balance.PlayerHitDy : Balance.PlayerHitDy;   // tuned: the shells' hitbox, not one 20 px above it
                double cy = edy - hitDy;
                // off the JS rate the body's move this tick is swept in JS-tick parts, as Substeps does for shells
                bool touching = !e.buried && System.Math.Sqrt(edx * edx + cy * cy) < e.r + Balance.PlayerHitR;
                for (int k = 1, sub = Balance.Substeps; !touching && !e.buried && k < sub; k++)
                {
                    double qx = HitboxX - (ox + (e.x - ox) * k / sub), qy = HitboxY - hitDy - (oy + (e.y - oy) * k / sub);
                    touching = System.Math.Sqrt(qx * qx + qy * qy) < e.r + Balance.PlayerHitR;
                }
                if (touching && e.kind == BodyKind.Gatekeeper && !FloorBoss.Hurts(e)) touching = false;   // it hurts only as it charges
                if (touching && e.kind == BodyKind.SunkenKing && !FinalBoss.Hurts(e)) touching = false;   // the King likewise
                if (touching)
                {
                    // a Brunch that reaches you is not pushed back unless it holds a slot; it spends itself
                    bool brunch = e.kind == BodyKind.Brunch;
                    bool holdingSlot = e.shieldTarget != null && e.shieldTarget.hp > 0 && !e.rushing;   // a rushing Brunch spends itself, as a chasing one does
                    bool boss = Bodies.IsBoss(e.kind);
                    if (e.kind == BodyKind.SunkenKing) run.hitSrc = (int)FinalBoss.Source.Charge;
                    bool landed = Combat.DamagePlayer(run, boss ? 2 : 1, edx / dist, edy / dist, (boss ? 8 : 5.5) * Balance.KnockPGain, e.kind == BodyKind.Boss);
                    run.hitSrc = -1;
                    if (landed && (!brunch || holdingSlot))
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

        /// <summary>The blink key (keydown in the game, so it lands BETWEEN ticks): only in a live room with a charge, no pause... [h:Tick-14]</summary>
        public static bool TryBlink(RunState run, Input input)
        {
            if (run.state != "playing" || run.trans != null || run.readyT > 0 || run.blinkCharges <= 0) return false;
            DoBlink(run, input);
            return true;
        }

        /// <summary>doBlink: BLINK_DIST along the held keys (else the current velocity, else the aim), clamped; the enemies'... [h:Tick-15]</summary>
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

        /// <summary>The projectile phase, src/60-tick.js tickProjectiles. [h:Tick-16]</summary>
        public static void TickProjectiles(RunState run)
        {
            var ps = run.projectiles;
            int sub = Balance.Substeps;
            double f = 1.0 / sub;
            for (int i = ps.Count - 1; i >= 0; i--)
                for (int s = 0; s < sub; s++)
                    if (StepShell(run, i, f, s == 0)) break;
        }

        // One shell over a fraction f of a tick; true once it is gone (or done for the tick).
        static bool StepShell(RunState run, int i, double f, bool first)
        {
            var ps = run.projectiles;
            var en = run.enemies;
            var p = ps[i];
            if (p.lob)   // a mortar shell: moves once a tick, touches nothing in flight, lands on its mark
            {
                if (first && !run.slowSkip && Middle.StepLob(run, p)) { ps.RemoveAt(i); return true; }
                return false;
            }
            if (p.alt)
            {
                // stop on the point, not past it
                if (Dist(p.tx - p.x, p.ty - p.y) <= p.speed * f)
                {
                    p.x = p.tx; p.y = p.ty;
                    Blast.Explode(run, p.tx, p.ty, p.mode);
                    ps.RemoveAt(i);
                    return true;
                }
            }
            if (first) p.age++;
            double slow = !p.friendly && run.timeSlowT > 0 ? Items.TimeSlow : 1;   // Time Glass holds the hostile shells too
            p.x += p.vx * f * slow;
            p.y += p.vy * f * slow;
            if (p.bounce > 0 && Bounce(p)) p.bounce--;   // Ricochet Shells: off the wall instead of out of the room
            if (p.x < Balance.RoomLeft - 30 || p.x > Balance.RoomRight + 30
                || p.y < Balance.RoomTop - 30 || p.y > Balance.RoomBottom + 30)
            {
                if (p.alt) Blast.Explode(run, p.x, p.y, p.mode);   // on the mode it was cast with
                ps.RemoveAt(i);
                return true;
            }
            if (p.alt && p.phase) return false;   // the hook flies through bodies and acts only at its point
            if (p.splitAt > 0 && Dist(p.x - p.ox, p.y - p.oy) >= p.splitAt) { Middle.Split(run, p); ps.RemoveAt(i); return true; }
            if (p.friendly)
            {
                int best = -1;
                double bestA = double.PositiveInfinity;
                for (int j = en.Count - 1; j >= 0; j--)
                {
                    var e = en[j];
                    if (e.buried || (p.hit != null && p.hit.Contains(e))) continue;
                    if (Dist(p.x - e.x, p.y - e.y) >= p.r + e.r) continue;
                    if (p.pierce == 0) { best = j; break; }
                    double a = (e.x - p.ox) * p.dx + (e.y - p.oy) * p.dy;
                    if (a < bestA) { bestA = a; best = j; }
                }
                if (best < 0) return false;
                if (p.alt) { Blast.Explode(run, p.x, p.y); ps.RemoveAt(i); return true; }   // the blast goes off on the first body
                var t = en[best];
                double taken = Balance.Tuned && Balance.TuneBeam && t.kind == BodyKind.Brunch && p.gun == Weapons.BeamIndex ? Balance.BrunchBeamTaken : 1;   // tuned: a wall the Beam cannot erase
                double dealt = p.dmg * Combat.FalloffMult(p) * t.Vuln * p.scale * taken * (p.alt ? 1 : Items.ShotMult(run));
                t.hp -= dealt;
                t.hitFlash = Balance.HitFlash;
                run.hits++;
                if (!p.alt) ItemsOnHit(run, t, dealt);
                Combat.AlertEnemy(t);
                Combat.SlowEnemy(t);
                if (t.hp <= 0) { int k = en.IndexOf(t); if (k >= 0) Kills.KillEnemy(run, k); }   // a chained kill may have moved it
                if (p.pierce > 0)
                {
                    p.pierce--;
                    p.scale *= Balance.PierceFalloff;
                    (p.hit ??= new List<Enemy>()).Add(t);
                }
                else { ps.RemoveAt(i); return true; }
                return false;
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
                run.hitSrc = p.src;
                Combat.DamagePlayer(run, p.dmg, p.vx / pn, p.vy / pn, 1.5 * Balance.KnockPGain, p.owner != null && p.owner.kind == BodyKind.Boss);
                run.hitSrc = -1;
                hitSomething = true;
            }
            if (hitSomething) ps.RemoveAt(i);
            return hitSomething;
        }

        /// <summary>The room phase, ported. [h:Tick-17]</summary>
        public static bool TickRoom(RunState run)
        {
            // The current room. The C# model keeps the CURRENT room's bodies on `RunState.enemies` rather than on `Room`... [h:Tick-18]
            var room = run.CurrentRoom;
            // The generator cannot produce a cursor on an empty cell, so this is unreachable in a well-formed run. [h:Tick-19]
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
                if (room.Fought && player.hp > 0) Items.FightWon(run);   // a recharging active gains a room
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

                // `hold` clears BEFORE the touch test, so stepping off a held pickup and back on collects it again - and a held... [h:Tick-20]
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
                run.picked++;
            }

            // THE ORIGINAL RETURNS OUT OF THE MIDDLE OF THE FUNCTION HERE, and that has to be honoured. [h:Tick-21]
            if (descended) return true;

            // Death wins ties: a pickup that arrives on the frame the player dies ends the run as a
            // death, and the boss check below does not run.
            if (player.hp <= 0) { run.EndRun(false); return false; }
            else if (room.Type == RoomKind.Boss && run.enemies.Count == 0 && !room.ExitOpen && run.finalFloor)
            {
                if (run.finaleT > 0)
                {
                    run.finaleT--;   // the King's body is still breaking: the win waits for its death beat, its shells go with it
                    run.projectiles.RemoveAll(q => !q.friendly);
                }
                else
                {
                    room.ExitOpen = true;   // the final boss is down: the run is won, there is no way further down
                    run.EndRun(true);
                }
            }
            else if (room.Type == RoomKind.Boss && run.enemies.Count == 0 && !room.ExitOpen)
            {
                // The way down opens when the boss dies and is walked into, rather than the run ending on the kill. [h:Tick-22]
                room.ExitOpen = true;
                run.pickups.Add(Pickup.Of("exit", Balance.MidX, Balance.MidY, 30));
            }

            return descended;
        }

        // a friendly shell off the room's wall: the side it crossed reflects it back in
        static bool Bounce(Projectile p)
        {
            bool hit = false;
            if (p.x < Balance.RoomLeft && p.vx < 0 || p.x > Balance.RoomRight && p.vx > 0)
            {
                double wall = p.vx < 0 ? Balance.RoomLeft : Balance.RoomRight;
                p.vx = -p.vx; p.dx = -p.dx; p.ox = 2 * wall - p.ox; hit = true;   // mirrored: the fall-off reads the path flown
            }
            if (p.y < Balance.RoomTop && p.vy < 0 || p.y > Balance.RoomBottom && p.vy > 0)
            {
                double wall = p.vy < 0 ? Balance.RoomTop : Balance.RoomBottom;
                p.vy = -p.vy; p.dy = -p.dy; p.oy = 2 * wall - p.oy; hit = true;
            }
            return hit;
        }

        // what a shot's hit does besides its damage, from what the player carries (Ember Rounds, Chain Spark)
        static void ItemsOnHit(RunState run, Enemy t, double dealt)
        {
            if (Items.Has(run, "ember_rounds") && t.hp > 0)
            {
                if (t.burnT <= 0) t.burnTick = Items.BurnEvery;   // a fresh burn starts its phase; a hit only lengthens it
                t.burnT = Items.BurnFor;
            }
            if (!Items.Has(run, "chain_spark")) return;
            Enemy? next = null;
            double best = Items.ChainReach;
            foreach (var o in run.enemies)
            {
                if (o == t || o.hp <= 0 || o.buried) continue;
                double d = Dist(o.x - t.x, o.y - t.y);
                if (d < best) { best = d; next = o; }
            }
            if (next == null) return;
            next.hp -= dealt * Items.ChainShare;
            next.hitFlash = Balance.HitFlash;
            Combat.AlertEnemy(next);
            if (next.hp <= 0) { int j = run.enemies.IndexOf(next); if (j >= 0) Kills.KillEnemy(run, j); }
        }

        /// <summary>The player's lagged hitbox, published once per tick by TickPlayer and read by the gunner intercept in the... [h:Tick-23]</summary>
        public static double HitboxX, HitboxY;

        /// <summary>The player phase: input, acceleration, movement, the wall clamp, the lagged hitbox, and the cooldowns. [h:Tick-24]</summary>
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
            // moveSpeedBonus() in the game: the speed STAT plus the Momentum meter's share, capped. [h:Tick-25]
            double bonus = System.Math.Min(Balance.MomCap, System.Math.Min(Balance.MoveSpeedHardCap, moveSpeedBonus ?? run.stats.Value("speed")) + p.momentum * Balance.MomSpeed);
            double spd = p.speed * p.slowMult * (1 + bonus) * boostMult;
            double targetVx = len != 0 ? (dx / len) * spd : 0;
            double targetVy = len != 0 ? (dy / len) * spd : 0;

            // THE ACCELERATION RAMP IS THE FEEL, and it is why this is not a velocity assignment. [h:Tick-26]
            double accel = Balance.Ease(Balance.MoveAccel * (1 + p.momentum * Balance.MomAccel));
            p.vx += (targetVx - p.vx) * accel;
            p.vy += (targetVy - p.vy) * accel;

            // The direction-of-travel estimate, for the lungers. [h:Tick-27]
            p.trendVx += (p.vx - p.trendVx) * Balance.LungeTrack;
            p.trendVy += (p.vy - p.trendVy) * Balance.LungeTrack;

            // The second, separate estimate - the player's BELIEF about their own direction, used by the gunners. [h:Tick-28]
            double bsp = System.Math.Sqrt(p.vx * p.vx + p.vy * p.vy);
            if (bsp > Balance.PlayerSpeedEps)
            {
                p.beliefVx += (p.vx / bsp - p.beliefVx) * Balance.LungeBeliefTrack;
                p.beliefVy += (p.vy / bsp - p.beliefVy) * Balance.LungeBeliefTrack;
                double bl = System.Math.Sqrt(p.beliefVx * p.beliefVx + p.beliefVy * p.beliefVy);
                if (bl > 1e-4) { p.beliefVx /= bl; p.beliefVy /= bl; }
            }

            // THE SWERVE METER. It rises when the player's new direction disagrees with the one they were already committed... [h:Tick-29]
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

            // THE WALL CLAMP, AND WHY IT ZEROES VELOCITY RATHER THAN JUST THE POSITION. [h:Tick-30]
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

            // MOMENTUM IS EARNED FROM DISPLACEMENT, NOT FROM VELOCITY, and the knockback is subtracted first. [h:Tick-31]
            double moved = System.Math.Sqrt((p.x - wasX) * (p.x - wasX) + (p.y - wasY) * (p.y - wasY));
            TickMomentum(run, System.Math.Max(0, moved - knk));

            // The walk cycle scales with distance covered, so a slow walk animates slowly.
            p.anim = sp > Balance.PlayerAnimEps ? p.anim + sp / Balance.Stride : 0;

            TickBlink(run);
            // off the JS rate the cooldown keeps the fraction it overran by for one tick, so held fire is exact in seconds
            if (Balance.TickHz == Balance.JsHz) { if (p.cooldown > 0) p.cooldown = System.Math.Max(0, p.cooldown - 1); }
            else p.cooldown = p.cooldown > 0 ? p.cooldown - 1 : 0;
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
            // tuned, only a fresh press detonates a flying hook: a held button used to blow it 0.18 s out
            if (input.alt) Weapons.FireAlt(run, input.aimX, input.aimY, !Balance.Tuned || !p.altHeld);
            p.altHeld = input.alt;

            // THE LAGGED HITBOX, and the publish. [h:Tick-32]
            p.lagX += (p.x - p.lagX) * Balance.HitboxLagEase;
            p.lagY += (p.y - p.lagY) * Balance.HitboxLagEase;
            HitboxX = p.lagX;
            HitboxY = p.lagY;

            return true;
        }

        /// <summary>Keeps the player inside the room. [h:Tick-33]</summary>
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

        /// <summary>Whether the player is standing in a doorway on side d of the room. [h:Tick-34]</summary>
        public static bool DoorPassable(RunState run, Room room, Dir d) => Rooms.DoorPassable(run, room, d);


        /// <summary>Whether the player is standing in a doorway, and therefore whether the tick should start a room transition. [h:Tick-35]</summary>
        public static bool CheckDoorTransition(RunState run) => Rooms.CheckDoorTransition(run);


        /// <summary>A pack's centroid, computed once per tick and shared by every body in it. [h:Tick-36]</summary>
        static readonly Dictionary<int, PackCentroid> Packs = new Dictionary<int, PackCentroid>();
        static readonly List<PackCentroid> CentroidPool = new List<PackCentroid>();

        private sealed class PackCentroid
        {
            public double X, Y;
            public int Count;
        }

        /// <summary>The pack assembly pass: centroids, target selection, and the guard lists. [h:Tick-37]</summary>
        public static void AssemblePacks(RunState run)
        {
            var room = run.CurrentRoom;
            if (room == null) return;

            // --- centroids. One pass to sum, one to divide, which is what the original does and is cheaper than carrying a... [h:Tick-38]
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
                // STALE MEANS GONE, DEAD, OR NOT IN THIS ROOM. [h:Tick-39]
                bool stale = tgt == null || tgt.hp <= 0 || !run.enemies.Contains(tgt);

                if (stale || (e.shieldTarget == null && run.frameCount % Balance.BrunchScanTicks == 0))
                    e.shieldTarget = PickShield(run.enemies, c);
            }

            // --- guard lists, rebuilt from scratch every tick. [h:Tick-40]
            foreach (var o in run.enemies)
                if (IsRanged(o.kind)) o.shieldGuardFor = null;

            foreach (var e in run.enemies)
            {
                if (e.kind != BodyKind.Brunch || !e.packId.HasValue || e.shieldTarget == null) continue;
                var t = e.shieldTarget;
                if (t.shieldGuardFor == null) { t.shieldGuardFor = t.guardBuf ??= new List<Enemy>(); t.shieldGuardFor.Clear(); }   // null stays "unguarded"; the list is reused
                t.shieldGuardFor.Add(e);
            }
        }

        /// <summary>The nearest living ranged body to a pack centroid, or null if there is none. [h:Tick-41]</summary>
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

        /// <summary>The three body kinds that shoot from range, and therefore the three worth shielding. [h:Tick-42]</summary>
        public static bool IsRanged(BodyKind k) =>
            k == BodyKind.Shooter || k == BodyKind.Gunner || Bodies.IsBoss(k);


        /// <summary>A point in the world, returned by the arc geometry. [h:Tick-43]</summary>
        public readonly struct Pt
        {
            public readonly double X, Y;
            public Pt(double x, double y) { X = x; Y = y; }
        }

        /// <summary>Where slot slot of an n-body pack stands when shielding a body at (tx, ty) against a player at (px, py). [h:Tick-44]</summary>
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
            // Symmetric about the centreline, in HALF-STEPS - and the half-step is NOT optional. [h:Tick-45]
            double col = System.Math.Floor(slot / 2.0) - ((cols - 1) / 2.0);

            // The stand-off. Two clamps, both of which were bugs once: - never further out than a share of the gap, or the... [h:Tick-46]
            double radius = System.Math.Min(d * Balance.BrunchShieldFrac, d - BrunchRadius - 2)
                          + (rank != 0 ? Balance.BrunchWallRank : 0);

            // The cone: the target's own apparent width at the arc's radius, plus one body's margin either side. [h:Tick-47]
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

        /// <summary>A Brunch's collision radius. [h:Tick-48]</summary>
        public const double BrunchRadius = 8;

    }
}
