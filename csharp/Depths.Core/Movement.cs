using System.Collections.Generic;

namespace Depths
{
    /// <summary>How bodies move: the lunger's four-state attack (src/30-enemies.js stepLunge, solveIntercept), the idle... [h:Movement-1]</summary>
    public static class Movement
    {
        static double Hyp(double x, double y) => System.Math.Sqrt(x * x + y * y);

        /// <summary>Approach speed eases from LUNGER_NEAR to LUNGER_FAR over LUNGE_RANGE.</summary>
        public static double ApproachSpeed(double d) =>
            Balance.LungerNear + (Balance.LungerFar - Balance.LungerNear) * System.Math.Min(1, d / Balance.LungeRange);

        /// <summary>Where to lunge: the player's displacement belief (falling back to the trend when the belief is zero), scaled... [h:Movement-2]</summary>
        public static (double dx, double dy, double dist) LungeIntercept(Player p, double gx, double gy)
        {
            double conf = 1 - p.swerve;
            double vx = p.beliefVx, vy = p.beliefVy;
            if (Hyp(vx, vy) < 1e-4) { vx = p.trendVx; vy = p.trendVy; }
            double bvx = vx * Balance.PlayerMove * conf, bvy = vy * Balance.PlayerMove * conf;
            double ax = gx, ay = gy;
            for (int k = 0; k < Balance.LungeIter; k++)
            {
                double n = Hyp(ax, ay) / Balance.LungeSpeed;
                ax = gx + bvx * (Balance.LungeWindup + n);
                ay = gy + bvy * (Balance.LungeWindup + n);
            }
            double d = Hyp(ax, ay);
            if (d == 0) d = 1;
            return (ax / d, ay / d, d);
        }

        public static void StepLunge(RunState run, Enemy e, double ux, double uy, double dist, double sm)
        {
            var p = run.player;
            var room = run.enemies;
            if (e.lungeCd > 0) e.lungeCd--;
            if (e.lungeState != "lunge") e.dashN = 0;   // a dash ends with its state, however it ended
            switch (e.lungeState)
            {
                case "approach":
                    // the standoff: lungers spread around the player rather than queueing on one line
                    if (room.Count > 1)
                    {
                        double bearing = System.Math.Atan2(e.y - p.y, e.x - p.x), err = e.flank - bearing;
                        while (err > System.Math.PI) err -= 2 * System.Math.PI;
                        while (err < -System.Math.PI) err += 2 * System.Math.PI;
                        double side = err >= 0 ? 1 : -1;
                        e.x += -System.Math.Sin(bearing) * side * Balance.LungerSpread;
                        e.y += System.Math.Cos(bearing) * side * Balance.LungerSpread;
                    }
                    if (dist < Balance.LungeHold) e.curSpeed += (0 - e.curSpeed) * Balance.LungerAccel * 8 * Balance.EaseK(Balance.LungerAccel * 8);
                    else
                    {
                        e.curSpeed += (ApproachSpeed(dist) * sm - e.curSpeed) * Balance.LungerAccel * 6 * Balance.EaseK(Balance.LungerAccel * 6);
                        e.x += ux * e.curSpeed;
                        e.y += uy * e.curSpeed;
                    }
                    if (e.lungeCd <= 0)
                    {
                        var sol = LungeIntercept(p, p.x - e.x, p.y - e.y);
                        if (dist >= Balance.LungeMin && sol.dist <= Balance.LungeReach)
                        {
                            e.lungeState = "wind";
                            e.lungeT = Balance.LungeWindup;
                            e.lungeDx = sol.dx; e.lungeDy = sol.dy;
                            e.lungeLen = System.Math.Max(Balance.LungeFloor, sol.dist);
                        }
                    }
                    break;
                case "wind":
                    e.curSpeed = 0;
                    e.lungeT--;
                    if (e.lungeT <= 0)
                    {
                        e.lungeState = "lunge";
                        e.lungeT = (int)System.Math.Ceiling(e.lungeLen / Balance.LungeSpeed);
                        if (Balance.Tuned && Balance.TuneEase) { e.dashN = e.lungeT; e.dashLen = e.lungeT * Balance.LungeSpeed; }
                    }
                    break;
                case "lunge":
                    {
                        double step = e.dashN > 0 ? DashStep(e, Balance.LungeRampIn, Balance.LungeRampOut) : Balance.LungeSpeed;
                        e.x += e.lungeDx * step * sm;
                        e.y += e.lungeDy * step * sm;
                    }
                    e.lungeT--;
                    // two lungers on one line meet mid-charge and both come off worse
                    foreach (var o in room)
                    {
                        if (o == e || o.buried) continue;   // a lunge passes over a Burrower underground
                        double ox = o.x - e.x, oy = o.y - e.y, d = Hyp(ox, oy);
                        if (d > e.r + o.r) continue;
                        double nx = d > 0.01 ? ox / d : System.Math.Cos(e.flank), ny = d > 0.01 ? oy / d : System.Math.Sin(e.flank);
                        Knock(run, e, -nx, -ny, Balance.LungeClash * Balance.KnockGain);
                        Knock(run, o, nx, ny, Balance.LungeClash * Balance.KnockGain * 0.85);
                        e.lungeState = "recover";
                        e.lungeT = Balance.LungeRecover;
                        o.lungeCd = System.Math.Max(o.lungeCd, Balance.LungeCd);
                        break;
                    }
                    if (e.lungeState == "lunge" && e.lungeT <= 0) { e.lungeState = "recover"; e.lungeT = Balance.LungeRecover; }
                    break;
                case "recover":
                    e.curSpeed += (e.walkSpeed * 0.5 * sm - e.curSpeed) * Balance.LungerAccel * 4 * Balance.EaseK(Balance.LungerAccel * 4);
                    e.x += e.lungeDx * e.curSpeed;
                    e.y += e.lungeDy * e.curSpeed;
                    if (--e.lungeT <= 0) { e.lungeState = "approach"; e.lungeCd = Balance.LungeCd; }
                    break;
            }
            // a lunge that reaches a wall is over, not a body grinding along it
            if (e.lungeState == "lunge")
            {
                double cx = System.Math.Max(Balance.RoomLeft, System.Math.Min(Balance.RoomRight, e.x));
                double cy = System.Math.Max(Balance.RoomTop, System.Math.Min(Balance.RoomBottom, e.y));
                if (cx != e.x || cy != e.y) { e.x = cx; e.y = cy; e.lungeState = "recover"; e.lungeT = Balance.LungeRecover; }
            }
        }

        public static void IdleWander(RunState run, Enemy e)
        {
            e.idleTimer--;
            if (e.idleTimer <= 0)
            {
                e.idleDirX = run.rng.Jitter() - 0.5;
                e.idleDirY = run.rng.Jitter() - 0.5;
                e.idleTimer = Balance.WanderTicks / 2 + (int)(run.rng.Jitter() * Balance.WanderTicks / 2);
            }
            double len = Hyp(e.idleDirX, e.idleDirY);
            if (len == 0) len = 1;
            e.x += e.idleDirX / len * Balance.WanderSpeed;
            e.y += e.idleDirY / len * Balance.WanderSpeed;
        }

        /// <summary>knockEnemy: a shove, capped at KNOCK_MAX, that stuns. A zero direction draws a random one.</summary>
        public static void Knock(RunState run, Enemy e, double dx, double dy, double force)
        {
            double len = Hyp(dx, dy);
            if (len < 1) { double a = run.rng.Jitter() * 6.283; dx = System.Math.Cos(a); dy = System.Math.Sin(a); }
            else { dx /= len; dy /= len; }
            // the force is a speed per JS tick; KnockScale keeps the distance it coasts at this rate
            e.kvx += dx * force * Balance.KnockScale / e.mass;
            e.kvy += dy * force * Balance.KnockScale / e.mass;
            double sp = Hyp(e.kvx, e.kvy);
            if (sp > Balance.KnockMax) { e.kvx *= Balance.KnockMax / sp; e.kvy *= Balance.KnockMax / sp; }
            e.stun = System.Math.Max(e.stun, Balance.KnockStun);
            e.noticeTimer = 0;
        }

        public static void Clamp(Enemy e)
        {
            double minX = Balance.RoomLeft + e.r, maxX = Balance.RoomRight - e.r, minY = Balance.RoomTop + e.r, maxY = Balance.RoomBottom - e.r;
            if (e.x < minX) { e.x = minX; if (e.kvx < 0) e.kvx = -e.kvx * Balance.KnockBounce; }
            else if (e.x > maxX) { e.x = maxX; if (e.kvx > 0) e.kvx = -e.kvx * Balance.KnockBounce; }
            if (e.y < minY) { e.y = minY; if (e.kvy < 0) e.kvy = -e.kvy * Balance.KnockBounce; }
            else if (e.y > maxY) { e.y = maxY; if (e.kvy > 0) e.kvy = -e.kvy * Balance.KnockBounce; }
        }

        /// <summary>Overlapping bodies are pushed apart, pairs in index order. [h:Movement-3]</summary>
        public static void Separate(RunState run, List<Enemy> list)
        {
            int n = list.Count;
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++) Bounce(run, list[a], list[b]);
        }

        static void Bounce(RunState run, Enemy a, Enemy b)
        {
            double dx = b.x - a.x, dy = b.y - a.y, d = Hyp(dx, dy), min = a.r + b.r;
            if (d >= min || a.buried || b.buried) return;   // a Burrower underground passes under the others
            if (Eggs.Riding(a) || Eggs.Riding(b)) return;   // a riding egg goes where its ride puts it, through bodies
            double nx, ny;
            if (d < 0.001) { double ang = run.rng.Jitter() * 6.283; nx = System.Math.Cos(ang); ny = System.Math.Sin(ang); }
            else { nx = dx / d; ny = dy / d; }
            double tot = a.mass + b.mass, push = min - d;
            a.x -= nx * push * b.mass / tot; a.y -= ny * push * b.mass / tot;
            b.x += nx * push * a.mass / tot; b.y += ny * push * a.mass / tot;
            if (Hyp(a.kvx, a.kvy) < Balance.KnockTrade && Hyp(b.kvx, b.kvy) < Balance.KnockTrade) return;
            double rel = (a.kvx - b.kvx) * nx + (a.kvy - b.kvy) * ny;
            if (rel > 0)
            {
                double j = (1 + Balance.KnockBounce) * rel / (1 / a.mass + 1 / b.mass);
                a.kvx -= j * nx / a.mass; a.kvy -= j * ny / a.mass;
                b.kvx += j * nx / b.mass; b.kvy += j * ny / b.mass;
                int shove = (int)System.Math.Floor(Balance.KnockStun / 3.0 + 0.5);   // JS Math.round, not half-to-even
                a.stun = System.Math.Max(a.stun, shove); b.stun = System.Math.Max(b.stun, shove);
            }
        }

        static readonly double[] SweepOffsets = { 0, 0.1, -0.1, 0.2, -0.2, 0.3, -0.3, 0.42, -0.42 };

        /// <summary>The Lunger's dodge (C# only, Balance.Tuned; owner 2026-10-13): a short dash off a shot it can see coming ... [h:Movement-4]</summary>
        public static bool TryLungerDodge(RunState run, Enemy e)
        {
            // never out of its wind-up, its lunge or its recovery: the recovery is the player's punish window
            if (e.dodgeCd > 0 || e.lungeState == "wind" || e.lungeState == "lunge" || e.lungeState == "recover" || e.stun > 0) return false;
            // startled: new to the room (or not yet noticing), once a life. Jumpy: no reaction floor and no
            // close-range exemption, so a Lunger spawned beside you is not a free kill; after that, close kills
            bool startled = !e.startleSpent && (e.noticeTimer > 0 || e.lifeT < Balance.LungerStartle);
            double px = TickOrder.HitboxX - e.x, py = TickOrder.HitboxY - e.y;
            if (!startled && Hyp(px, py) < Balance.CloseRange) return false;
            int react = startled ? 0 : Balance.LungerDodgeReact;
            double best = double.PositiveInfinity, dx = 0, dy = 0, need = 0;
            bool burst = false;   // away from a burst point (no flipping), or off a line (either side)
            double bx = 0, by = 0;
            foreach (var p in run.projectiles)
            {
                if (!p.friendly || p.lob) continue;
                double vv = p.vx * p.vx + p.vy * p.vy;
                if (p.alt)
                {
                    double aoe = p.mode == "hook" ? Balance.HookAoe : Balance.AltAoe;
                    // where it bursts: its mark, or (a blast) the first body its path meets - this one or a neighbour
                    double t = Hyp(p.tx - p.x, p.ty - p.y) / System.Math.Max(1e-6, p.speed), qx = p.tx, qy = p.ty;
                    if (!p.phase && vv > 1e-9)
                        foreach (var o in run.enemies)
                        {
                            if (o.buried || o.hp <= 0) continue;
                            double rx = o.x - p.x, ry = o.y - p.y, tc = (rx * p.vx + ry * p.vy) / vv;
                            if (tc < 0 || tc >= t) continue;
                            if (Hyp(rx - p.vx * tc, ry - p.vy * tc) < o.r + p.r) { t = tc; qx = p.x + p.vx * tc; qy = p.y + p.vy * tc; }
                        }
                    double ox = e.x - qx, oy = e.y - qy, od = Hyp(ox, oy);
                    if (od < aoe + e.r && t >= react && t <= Balance.LungerDodgeHorizon && t < best)
                    {
                        if (od < 1e-6) { ox = System.Math.Cos(e.flank); oy = System.Math.Sin(e.flank); od = 1; }
                        best = t; dx = ox / od; dy = oy / od; burst = true; bx = qx; by = qy;
                        need = System.Math.Min(Balance.LungerDodgeBlastMax, System.Math.Max(Balance.LungerDodgeDist, aoe + e.r - od + 8));
                    }
                    continue;
                }
                if (vv < 1e-9) continue;
                double sx = e.x - p.x, sy = e.y - p.y, tca = (sx * p.vx + sy * p.vy) / vv;   // ticks to the closest approach
                if (tca < react || tca > Balance.LungerDodgeHorizon || tca >= best) continue;
                double mx = sx - p.vx * tca, my = sy - p.vy * tca, miss = Hyp(mx, my);
                if (miss >= e.r + p.r + 3) continue;
                if (miss < 1e-6) { mx = -p.vy; my = p.vx; miss = System.Math.Sqrt(vv); if (System.Math.Sin(e.flank) < 0) { mx = -mx; my = -my; } }
                best = tca; dx = mx / miss; dy = my / miss; burst = false;
                need = Balance.LungerDodgeDist;
            }
            if (double.IsPositiveInfinity(best)) return false;
            if (!Clear(e, dx, dy, need, burst, bx, by))
            {
                // the dash runs into a wall. Off a line: the other side. From a burst: along the wall, whichever
                // way ends further from the burst; never back through it
                if (!burst) { dx = -dx; dy = -dy; if (!Clear(e, dx, dy, need, false, 0, 0)) return false; }
                else
                {
                    double ax = -dy, ay = dx;
                    bool a = Clear(e, ax, ay, need, true, bx, by), b = Clear(e, -ax, -ay, need, true, bx, by);
                    if (!a && !b) return false;
                    if (a && b) { if (Hyp(e.x - ax * need - bx, e.y - ay * need - by) > Hyp(e.x + ax * need - bx, e.y + ay * need - by)) { ax = -ax; ay = -ay; } }
                    else if (b) { ax = -ax; ay = -ay; }
                    dx = ax; dy = ay;
                }
            }
            e.lungeState = "dodge"; e.lungeDx = dx; e.lungeDy = dy;
            e.lungeT = System.Math.Max(1, (int)System.Math.Ceiling(need / Balance.LungeSpeed));
            e.dashN = 0;
            if (Balance.TuneEase)   // the same distance as the flat dash, on a curve whose top is DodgePeak x lunge speed
            {
                e.dashLen = e.lungeT * Balance.LungeSpeed;
                e.lungeT = e.dashN = (int)System.Math.Ceiling(e.dashLen / (Balance.LungeSpeed * Balance.DodgePeak * DashArea(Balance.DodgeRampIn, Balance.DodgeRampOut)));
            }
            e.dodgeCd = Balance.LungerDodgeCd;
            if (startled) { e.startleSpent = true; e.noticeTimer = 0; e.alerted = true; e.aggroTimer = Balance.AggroTime; }
            return true;
        }

        // the dash's end is inside the room, and (from a burst) leaves the burst further away than it was
        static bool Clear(Enemy e, double dx, double dy, double need, bool burst, double bx, double by)
        {
            double ex = e.x + dx * need, ey = e.y + dy * need, m = e.r + 4;
            if (ex < Balance.RoomLeft + m || ex > Balance.RoomRight - m || ey < Balance.RoomTop + m || ey > Balance.RoomBottom - m) return false;
            return !burst || Hyp(ex - bx, ey - by) > Hyp(e.x - bx, e.y - by);
        }

        /// <summary>One tick of a dodge, at lunge speed (eased: along its curve); it comes out of it still approaching.</summary>
        public static void StepDodge(Enemy e, double sm)
        {
            double step = e.dashN > 0 ? DashStep(e, Balance.DodgeRampIn, Balance.DodgeRampOut) : Balance.LungeSpeed;
            e.x += e.lungeDx * step * sm;
            e.y += e.lungeDy * step * sm;
            if (--e.lungeT <= 0) { e.lungeState = "approach"; e.lungeT = 0; e.dashN = 0; }
        }

        /// <summary>The share of a dash covered by time u (0..1): speed rises over the first rampIn of it and falls over the last... [h:Movement-5]</summary>
        public static double DashShare(double u, double rampIn, double rampOut)
        {
            u = System.Math.Max(0, System.Math.Min(1, u));
            // the integral of smoothstep s(x) = 3x^2 - 2x^3 from 0 to x is x^3 - x^4 / 2 (and 1/2 over the whole ramp)
            static double G(double x) => x * x * x - x * x * x * x / 2;
            double covered;
            if (u < rampIn) covered = rampIn * G(u / rampIn);
            else if (u <= 1 - rampOut) covered = rampIn / 2 + (u - rampIn);
            else covered = rampIn / 2 + (1 - rampIn - rampOut) + rampOut * (0.5 - G((1 - u) / rampOut));
            return covered / DashArea(rampIn, rampOut);
        }

        /// <summary>A dash's mean speed over its top speed.</summary>
        public static double DashArea(double rampIn, double rampOut) => 1 - rampIn / 2 - rampOut / 2;

        // this tick's distance along an eased dash (lungeT still counts the ticks left, this one included)
        static double DashStep(Enemy e, double rampIn, double rampOut)
        {
            int done = e.dashN - e.lungeT;
            return e.dashLen * (DashShare((done + 1) / (double)e.dashN, rampIn, rampOut) - DashShare(done / (double)e.dashN, rampIn, rampOut));
        }

        /// <summary>clearShot: the first angle, sweeping out from the wanted one, whose line to the player passes no body of ours... [h:Movement-6]</summary>
        public static double? ClearShot(RunState run, Enemy e, double want)
        {
            double tx = run.player.x, ty = run.player.y, reach = Hyp(tx - e.x, ty - e.y);
            foreach (var off in SweepOffsets)
            {
                double a = want + off, dx = System.Math.Cos(a), dy = System.Math.Sin(a);
                bool clear = true;
                foreach (var o in run.enemies)
                {
                    if (o == e || o.buried) continue;   // shells pass over a buried Burrower, so it blocks no line
                    if (e.shieldGuardFor != null && e.shieldGuardFor.Contains(o)) continue;
                    double ox = o.x - e.x, oy = o.y - e.y, along = ox * dx + oy * dy;
                    if (along <= 0 || along >= reach) continue;
                    if (System.Math.Abs(ox * dy - oy * dx) < o.r + e.pr) { clear = false; break; }
                }
                if (clear) return a;
            }
            return null;
        }

        /// <summary>A line from e at angle a that passes no body of ours within reach px (a shell of radius r would graze it). [h:Movement-7]</summary>
        public static bool LineClear(RunState run, Enemy e, double a, double reach, double r)
        {
            double dx = System.Math.Cos(a), dy = System.Math.Sin(a);
            foreach (var o in run.enemies)
            {
                if (o == e || o.buried || o.hp <= 0) continue;
                double ox = o.x - e.x, oy = o.y - e.y, along = ox * dx + oy * dy;
                if (along <= 0 || along >= reach) continue;
                if (System.Math.Abs(ox * dy - oy * dx) < o.r + r) return false;
            }
            return true;
        }

        /// <summary>The first cone centre, sweeping out from <paramref name="want"/> as ClearShot does, whose every
        /// line is clear; null when none is.</summary>
        public static double? ClearCone(RunState run, Enemy e, double want, double half, int count, double reach)
        {
            foreach (var off in SweepOffsets)
            {
                bool clear = true;
                for (int i = 0; i < count && clear; i++)
                    clear = LineClear(run, e, want + off + half * (2.0 * i / (count - 1) - 1), reach, e.pr);
                if (clear) return want + off;
            }
            return null;
        }

        /// <summary>The gunner sidesteps a bolt that is actually coming, half the time, on a cooldown.</summary>
        public static void GunnerDodge(RunState run, Enemy e)
        {
            if (e.dodgeCd > 0) { e.dodgeCd--; return; }
            Projectile? threat = null;
            foreach (var p in run.projectiles)
            {
                if (!p.friendly || p.owner == e) continue;
                double dx = e.x - p.x, dy = e.y - p.y;
                if (Hyp(dx, dy) < Balance.GunnerDodgeSight && dx * p.vx + dy * p.vy > 0) { threat = p; break; }
            }
            if (threat == null || run.rng.Jitter() >= Balance.GunnerDodgeChance) return;
            double sp = Hyp(threat.vx, threat.vy);
            if (sp == 0) sp = 1;
            double side = run.rng.Jitter() < 0.5 ? 1 : -1;
            e.kvx += -threat.vy / sp * side * Balance.GunnerDodgeKick;
            e.kvy += threat.vx / sp * side * Balance.GunnerDodgeKick;
            e.dodgeCd = Balance.GunnerDodgeCd;
        }
    }
}
