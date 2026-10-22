using System.Collections.Generic;

namespace Depths
{
    /// <summary>The turret egg's three forms (C# only, owner 2026-10-13). [h:Eggs-1]</summary>
    public enum EggForm { Fixed, Orbit, Rail }

    public static class Eggs
    {
        // ---- orbit ----
        public const int OrbitRing = 5;                          // shells (the fixed egg: 8)
        public const double OrbitShellSpeed = 0.8;               // x the egg's shell speed
        public const double OrbitGap = 34;                       // px between the host's edge and the egg's centre
        public static double OrbitSpin => Balance.PerSec(1.6);   // radians a second
        public const double OrbitChance = 0.45;                  // an egg with a body to circle does, this often

        // ---- rail ----
        public const int RailRing = 6;
        public static double RailSpeed => Balance.PerSec(140);   // px a second along the track
        public const double RailScaleMin = 0.38, RailScaleMax = 0.55;   // a track is this share of the room's, rolled per egg
        public const double RailChance = 0.6;                    // a Kiln Works egg rides a rail, this often
        public const double RailInset = 80;                      // px the track keeps from the walls: the doors stay clear

        const double RingReach = 420;                            // px along each line checked for a friend
        const int RingTurns = 6;                                 // rotations of the ring tried for the clearest

        /// <summary>Gives a wave's egg its form, as a room is entered: a rail in the Kiln Works (sometimes),
        /// otherwise an orbit round another body of the wave (sometimes), otherwise fixed. Run stream draws.</summary>
        public static void Dress(RunState run, Enemy e)
        {
            if (AreaRules.AreaForFloor(run.floor) == Area.Area2 && run.rng.Run() < RailChance) { OnRail(run, e, (int)(run.rng.Run() * Patterns)); return; }
            int hosts = 0;
            foreach (var o in run.enemies) if (CanHost(o)) hosts++;
            if (hosts == 0 || run.rng.Run() >= OrbitChance) return;
            int pick = (int)(run.rng.Run() * hosts);
            foreach (var o in run.enemies) if (CanHost(o) && pick-- == 0) { Orbit(e, o, run.rng.Run() * 2 * System.Math.PI); return; }
        }

        static bool CanHost(Enemy o) => o.kind != BodyKind.Egg && !Bodies.IsBoss(o.kind) && o.kind != BodyKind.Brunch && o.hp > 0;

        public static void Orbit(Enemy e, Enemy host, double angle)
        {
            e.eggForm = EggForm.Orbit; e.eggHost = host; e.eggA = angle; e.eggR = host.r + OrbitGap;
            Place(e);
        }

        public static void OnRail(RunState run, Enemy e, int pattern)
        {
            double scale = RailScaleMin + (RailScaleMax - RailScaleMin) * run.rng.Run();
            var (pts, loop) = Track(pattern, run.rng.Run() < 0.5, run.rng.Run() < 0.5, scale, run.rng.Run(), run.rng.Run());
            e.eggForm = EggForm.Rail; e.rail = pts; e.railLoop = loop; e.railLen = Length(pts, loop);
            e.railS = run.rng.Run() * e.railLen;
            e.railDir = run.rng.Run() < 0.5 ? 1 : -1;
            At(pts, loop, e.railS, out e.x, out e.y);
        }

        /// <summary>One tick of the egg's ride. An orbit whose host has fallen stays where it is, a fixed egg.</summary>
        public static void Move(RunState run, Enemy e)
        {
            if (e.eggForm == EggForm.Orbit)
            {
                if (e.eggHost == null || e.eggHost.hp <= 0 || !run.enemies.Contains(e.eggHost)) { e.eggForm = EggForm.Fixed; e.eggHost = null; return; }
                e.eggA += OrbitSpin;
                if (e.eggA > 2 * System.Math.PI) e.eggA -= 2 * System.Math.PI;
                Place(e);
            }
            else if (e.eggForm == EggForm.Rail && e.rail != null)
            {
                e.railS += RailSpeed * e.railDir;
                if (e.railLoop) { e.railS %= e.railLen; if (e.railS < 0) e.railS += e.railLen; }
                else if (e.railS > e.railLen) { e.railS = 2 * e.railLen - e.railS; e.railDir = -1; }
                else if (e.railS < 0) { e.railS = -e.railS; e.railDir = 1; }
                At(e.rail, e.railLoop, e.railS, out e.x, out e.y);
            }
        }

        // on its circle, flattened against a wall rather than through it: never Clamp's to fix, nor a shove for its host
        static void Place(Enemy e)
        {
            var h = e.eggHost!;
            double m = e.r + 2;
            e.x = System.Math.Max(Balance.RoomLeft + m, System.Math.Min(Balance.RoomRight - m, h.x + System.Math.Cos(e.eggA) * e.eggR));
            e.y = System.Math.Max(Balance.RoomTop + m, System.Math.Min(Balance.RoomBottom - m, h.y + System.Math.Sin(e.eggA) * e.eggR));
        }

        /// <summary>An egg on a ride (orbit or rail) is placed by it each tick: bodies do not shove it, nor it them.</summary>
        public static bool Riding(Enemy e) => e.kind == BodyKind.Egg && e.eggForm != EggForm.Fixed;

        /// <summary>The ring as the cast ends: the form's count and speed, turned (of a few tries) to the rotation
        /// whose lines cross the fewest of its own; a shell whose line still would is held.</summary>
        public static void Ring(RunState run, Enemy e)
        {
            int n = e.eggForm == EggForm.Orbit ? OrbitRing : e.eggForm == EggForm.Rail ? RailRing : Middle.EggRing;
            double speed = e.pspd * (e.eggForm == EggForm.Orbit ? OrbitShellSpeed : 1), step = 2 * System.Math.PI / n;
            double best = e.castAim; int fewest = Middle.Sharp ? int.MaxValue : 0;   // the lab-era ring: no check for its own
            for (int t = 0; t < RingTurns && fewest > 0; t++)
            {
                double rot = e.castAim + t * step / RingTurns;
                int blocked = 0;
                for (int i = 0; i < n; i++) if (!Movement.LineClear(run, e, rot + i * step, RingReach, e.pr)) blocked++;
                if (blocked < fewest) { fewest = blocked; best = rot; }
            }
            for (int i = 0; i < n; i++)
            {
                double a = best + i * step;
                if (fewest > 0 && !Movement.LineClear(run, e, a, RingReach, e.pr)) continue;
                run.projectiles.Add(Middle.Shell(e, a, speed, e.pr, e.dmg, Middle.EggColor, 0));
            }
        }

        // ---- tracks ----

        public const int Patterns = 8;
        public static readonly string[] PatternNames = { "loop", "figure eight", "zigzag", "serpentine", "ring", "spiral", "diamond", "clover" };

        /// <summary>A track as points (x0, y0, x1, y1, ...), a loop or an open line [h:Eggs-2], at `scale` of the room's size,
        /// centred at (u, v) of the room's free span (0..1 each way; 0.5, 0.5 is the middle).</summary>
        public static (double[] pts, bool loop) Track(int pattern, bool flipX, bool flipY, double scale = 1, double u = 0.5, double v = 0.5)
        {
            double l = Balance.RoomLeft + RailInset, r = Balance.RoomRight - RailInset, t = Balance.RoomTop + RailInset, b = Balance.RoomBottom - RailInset;
            double cx = (l + r) / 2, cy = (t + b) / 2, hw = (r - l) / 2, hh = (b - t) / 2;
            double ox = l + hw * scale + (r - l - 2 * hw * scale) * u, oy = t + hh * scale + (b - t - 2 * hh * scale) * v;
            var p = new List<double>();
            void Add(double x, double y)
            {
                x = flipX ? 2 * cx - x : x; y = flipY ? 2 * cy - y : y;
                p.Add(ox + (x - cx) * scale); p.Add(oy + (y - cy) * scale);
            }
            const double Tau = 2 * System.Math.PI;
            bool loop;
            switch (((pattern % Patterns) + Patterns) % Patterns)
            {
                case 0:   // loop: the room's rim, corners rounded
                {
                    double rr = 60;
                    void Arc(double ax, double ay, double a0) { for (int i = 0; i <= 6; i++) { double a = a0 + i * (Tau / 4) / 6; Add(ax + System.Math.Cos(a) * rr, ay + System.Math.Sin(a) * rr); } }
                    Arc(r - rr, t + rr, -Tau / 4); Arc(r - rr, b - rr, 0); Arc(l + rr, b - rr, Tau / 4); Arc(l + rr, t + rr, Tau / 2);
                    loop = true; break;
                }
                case 1:   // figure eight, crossing in the middle of the room
                    for (int i = 0; i < 64; i++) { double a = i * Tau / 64; Add(cx + hw * System.Math.Sin(a), cy + hh * System.Math.Sin(2 * a)); }
                    loop = true; break;
                case 2:   // zigzag: across the room in five legs
                    for (int i = 0; i <= 5; i++) Add(l + 2 * hw * i / 5, i % 2 == 0 ? t : b);
                    loop = false; break;
                case 3:   // serpentine: three lanes down the room, turned in half circles at alternate ends
                {
                    double rr = hh / 2, x0 = l + rr, x1 = r - rr;
                    for (int lane = 0; lane < 3; lane++)
                    {
                        double y = t + lane * hh;
                        bool right = lane % 2 == 0;
                        Add(right ? x0 : x1, y); Add(right ? x1 : x0, y);
                        if (lane < 2)
                            for (int i = 1; i < 10; i++)
                            {
                                double a = -Tau / 4 + i * (Tau / 2) / 10;
                                Add(right ? x1 + System.Math.Cos(a) * rr : x0 - System.Math.Cos(a) * rr, y + rr + System.Math.Sin(a) * rr);
                            }
                    }
                    loop = false; break;
                }
                case 4:   // ring round the middle
                    for (int i = 0; i < 48; i++) { double a = i * Tau / 48; Add(cx + System.Math.Cos(a) * hh * 0.85, cy + System.Math.Sin(a) * hh * 0.85); }
                    loop = true; break;
                case 5:   // spiral out from the middle, two turns and a bit
                    for (int i = 0; i <= 72; i++) { double k = i / 72.0, a = k * Tau * 2.2; Add(cx + System.Math.Cos(a) * (30 + k * (hw - 30)), cy + System.Math.Sin(a) * (30 + k * (hh - 30))); }
                    loop = false; break;
                case 6:   // diamond through the middle of each side
                    Add(cx, t); Add(r, cy); Add(cx, b); Add(l, cy);
                    loop = true; break;
                default:  // clover: four petals, each through the middle
                    for (int i = 0; i < 96; i++) { double a = i * Tau / 96, k = System.Math.Cos(2 * a); Add(cx + hw * k * System.Math.Cos(a), cy + hh * k * System.Math.Sin(a)); }
                    loop = true; break;
            }
            return (p.ToArray(), loop);
        }

        /// <summary>The track's length, its closing segment included when it is a loop.</summary>
        public static double Length(double[] pts, bool loop)
        {
            double len = 0;
            int n = pts.Length / 2;
            for (int i = 1; i < n; i++) len += Seg(pts, i - 1, i);
            if (loop && n > 1) len += Seg(pts, n - 1, 0);
            return len;
        }

        /// <summary>The point <paramref name="s"/> px along the track (clamped to its ends; wrapped on a loop).</summary>
        public static void At(double[] pts, bool loop, double s, out double x, out double y)
        {
            int n = pts.Length / 2;
            x = pts[0]; y = pts[1];
            if (n < 2) return;
            int segs = loop ? n : n - 1;
            if (s < 0) s = 0;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                double d = Seg(pts, i, j);
                if (s <= d || i == segs - 1)
                {
                    double k = d > 0 ? System.Math.Min(1, s / d) : 0;
                    x = pts[2 * i] + (pts[2 * j] - pts[2 * i]) * k;
                    y = pts[2 * i + 1] + (pts[2 * j + 1] - pts[2 * i + 1]) * k;
                    return;
                }
                s -= d;
            }
        }

        static double Seg(double[] p, int i, int j)
        {
            double dx = p[2 * j] - p[2 * i], dy = p[2 * j + 1] - p[2 * i + 1];
            return System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
