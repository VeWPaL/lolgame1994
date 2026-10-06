using System.Collections.Generic;

namespace Depths
{
    /// <summary>
    /// How bodies move: the lunger's four-state attack (src/30-enemies.js stepLunge, solveIntercept),
    /// the idle wander, the knock, the wall clamp and the separation pass (src/40-combat.js). Every
    /// random choice here is on the JITTER stream, as in the game. LungerParityTests pins it against
    /// trajectories recorded from the running game by tools/lunger-parity.js.
    /// </summary>
    public static class Movement
    {
        static double Hyp(double x, double y) => System.Math.Sqrt(x * x + y * y);

        /// <summary>Approach speed eases from LUNGER_NEAR to LUNGER_FAR over LUNGE_RANGE.</summary>
        public static double ApproachSpeed(double d) =>
            Balance.LungerNear + (Balance.LungerFar - Balance.LungerNear) * System.Math.Min(1, d / Balance.LungeRange);

        /// <summary>
        /// Where to lunge: the player's displacement belief (falling back to the trend when the belief
        /// is zero), scaled by how settled they look (1 - swerve), solved over LUNGE_ITER passes.
        /// </summary>
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
                    if (dist < Balance.LungeHold) e.curSpeed += (0 - e.curSpeed) * Balance.LungerAccel * 8;
                    else
                    {
                        e.curSpeed += (ApproachSpeed(dist) * sm - e.curSpeed) * Balance.LungerAccel * 6;
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
                    }
                    break;
                case "lunge":
                    e.x += e.lungeDx * Balance.LungeSpeed * sm;
                    e.y += e.lungeDy * Balance.LungeSpeed * sm;
                    e.lungeT--;
                    // two lungers on one line meet mid-charge and both come off worse
                    foreach (var o in room)
                    {
                        if (o == e) continue;
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
                    e.curSpeed += (e.walkSpeed * 0.5 * sm - e.curSpeed) * Balance.LungerAccel * 4;
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
            e.kvx += dx * force / e.mass;
            e.kvy += dy * force / e.mass;
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

        /// <summary>
        /// Overlapping bodies are pushed apart, pairs in index order. The game uses a 96px grid from 8
        /// bodies up and sorts candidates back into index order, so it tests the same overlapping pairs
        /// in the same order - except a pair that only comes into contact DURING the pass and sits in
        /// non-neighbouring cells, which the grid skips and this loop does not. Port the grid (without
        /// its string keys and per-tick allocations) before claiming parity for large rooms.
        /// </summary>
        public static void Separate(RunState run, List<Enemy> list)
        {
            int n = list.Count;
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++) Bounce(run, list[a], list[b]);
        }

        static void Bounce(RunState run, Enemy a, Enemy b)
        {
            double dx = b.x - a.x, dy = b.y - a.y, d = Hyp(dx, dy), min = a.r + b.r;
            if (d >= min) return;
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
    }
}
