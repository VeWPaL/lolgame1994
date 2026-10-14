namespace Depths
{
    /// <summary>Drawing between ticks (C# only). [h:Interp-1]</summary>
    public static class Interp
    {
        /// <summary>A step longer than this in one tick is a jump (a door, a blink, a room loaded): drawn
        /// where it landed, never swept across the room for a frame.</summary>
        public const double MaxStep = 40;

        /// <summary>Remember where everything is, just before a tick moves it.</summary>
        public static void Snap(RunState run)
        {
            int s = ++run.snap;
            var p = run.player;
            p.px = p.x; p.py = p.y; p.snap = s;
            for (int i = 0; i < run.enemies.Count; i++) { var e = run.enemies[i]; e.px = e.x; e.py = e.y; e.snap = s; }
            for (int i = 0; i < run.projectiles.Count; i++) { var q = run.projectiles[i]; q.px = q.x; q.py = q.y; q.snap = s; }
        }

        /// <summary>Where to draw a thing now at (x, y) that was at (px, py) when the last snap stamped it.
        /// Born since that snap (stamp differs), or jumped: drawn where it is.</summary>
        public static void At(RunState run, int snap, double px, double py, double x, double y, double alpha, out double dx, out double dy)
        {
            double sx = x - px, sy = y - py;
            if (snap == 0 || snap != run.snap || sx * sx + sy * sy > MaxStep * MaxStep) { dx = x; dy = y; return; }
            double k = 1 - alpha;
            dx = x - sx * k; dy = y - sy * k;
        }

        public static void At(RunState run, Enemy e, double alpha, out double x, out double y) => At(run, e.snap, e.px, e.py, e.x, e.y, alpha, out x, out y);
        public static void At(RunState run, Projectile q, double alpha, out double x, out double y) => At(run, q.snap, q.px, q.py, q.x, q.y, alpha, out x, out y);
        public static void At(RunState run, Player p, double alpha, out double x, out double y) => At(run, p.snap, p.px, p.py, p.x, p.y, alpha, out x, out y);
    }
}
