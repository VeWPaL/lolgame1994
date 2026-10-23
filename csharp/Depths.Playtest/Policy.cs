using System;
using System.Collections.Generic;

namespace Depths.Playtest
{
    /// <summary>What a player does this tick: the held input, plus the Q key (a press, between ticks).</summary>
    public readonly struct BotAction
    {
        public readonly Input Input;
        public readonly bool UseActive;
        public readonly int WeaponKey;   // a number key pressed this tick: 1-4 holds that gun (GameView's keys); 0 is none

        public BotAction(Input input, bool useActive = false, int weaponKey = 0) { Input = input; UseActive = useActive; WeaponKey = weaponKey; }

        public static BotAction None => new BotAction(Input.None);
    }

    /// <summary>A player: reads the run, returns an action. It never writes to the run; the runner
    /// applies the action.</summary>
    public interface IPolicy
    {
        BotAction Decide(RunState run, int t);
    }

    /// <summary>The JS bot's player (tools/playtest.js playOne): circle and keep a band, dodge shots it
    /// has seen for a reaction time, then take what is worth having and route on.</summary>
    public sealed partial class BotPolicy : IPolicy
    {
        static readonly Dir[] Dirs = { Dir.N, Dir.S, Dir.E, Dir.W };   // the JS route's order

        readonly Profile _p;
        readonly BotRng _rnd;
        readonly int _react;
        // the JS bot's windows, in seconds: a strafe holds 1-3 s; a shot is dodged inside 30 ticks, blinked inside 12 (at 210 Hz)
        const double StrafeS = 1, DodgeLookS = 1 / 7.0, BlinkLookS = 2 / 35.0;

        // when the bot first SAW each hostile shot: its reaction clock
        readonly Dictionary<Projectile, int> _born =
            new Dictionary<Projectile, int>(ReferenceEqualityComparer.Instance);
        readonly HashSet<Room> _tookItemIn = new HashSet<Room>(ReferenceEqualityComparer.Instance);
        // when the bot first SAW each body's tell (a Lunger's wind-up, a Brunch's tremble): the same reaction clock
        readonly Dictionary<Enemy, int> _tell = new Dictionary<Enemy, int>(ReferenceEqualityComparer.Instance);
        readonly List<Enemy> _pruneTell = new List<Enemy>();
        readonly Dictionary<Enemy, string> _kingMove = new Dictionary<Enemy, string>(ReferenceEqualityComparer.Instance);
        Room? _tellRoom;
        Pickup? _itemGoal;   // the item last steered at, so a take is seen even when one tick's step skips the 2px band
        Room? _itemGoalIn;
        readonly List<Projectile> _prune = new List<Projectile>();

        int _decideT, _strafe;
        double _strafeT;
        double _aimJ, _mvX, _mvY;
        double? _aimX, _aimY;   // the mouse stays where the last fight left it, as in the JS
        bool _fire;

        // route scratch, reused
        readonly Queue<Room> _q = new Queue<Room>();
        readonly Dictionary<Room, (Room? from, Dir d)> _prev =
            new Dictionary<Room, (Room?, Dir)>(ReferenceEqualityComparer.Instance);
        readonly List<Room> _reach = new List<Room>();

        public Profile Profile => _p;

        public BotPolicy(Profile profile, uint seed)
        {
            _p = profile;
            _rnd = new BotRng(seed, profile.Name);
            _react = profile.ReactTicks;
            _decide = Math.Max(2, Balance.Sec(profile.DecideS > 0 ? profile.DecideS : profile.ReactS));
            if (profile.Expert && !(profile.AimRate > 0 && profile.AimRate <= 25))
                throw new ArgumentException("an expert profile needs a mouse turn cap, 0 to 25 rad/s exclusive of 0: " + profile.AimRate);
            if (profile.Expert && profile.DecideS < 1.0 / MaxPerSecond)
                throw new ArgumentException("an expert profile decides at most " + MaxPerSecond + " times a second: " + profile.DecideS);
            _strafe = _rnd.Next() < 0.5 ? 1 : -1;
        }

        static double Hyp(double x, double y) => Math.Sqrt(x * x + y * y);
        static int Axis(double v, double dead) => (v > dead ? 1 : 0) - (v < -dead ? 1 : 0);

        public BotAction Decide(RunState run, int t)
        {
            var r = run.CurrentRoom;
            if (r == null || run.trans != null) return BotAction.None;
            var pl = run.player;

            Enemy? target = null, nearest = null, addNear = null;
            double nd = double.PositiveInfinity;
            int live = 0;
            foreach (var e in run.enemies)
            {
                if (e.hp <= 0) continue;
                live++;
                if (target == null && Bodies.IsBoss(e.kind)) target = e;
                if (e.kgAdd && !e.buried && Hyp(e.x - pl.x, e.y - pl.y) < AddNear) addNear = e;   // the King's recalled add, close: first
                // a buried Burrower cannot be shot: aim at what can, and only face the mound when nothing else is up
                double d = Hyp(e.x - pl.x, e.y - pl.y) + (e.buried ? 1e9 : 0);
                if (d < nd) { nd = d; nearest = e; }
            }
            if (live == 0) return Quiet(run, r);
            return _p.Expert ? Expert(run, r, t, addNear ?? target ?? nearest!) : Fight(run, r, t, addNear ?? target ?? nearest!);
        }

        BotAction Fight(RunState run, Room r, int t, Enemy target)
        {
            var pl = run.player;
            var ps = run.projectiles;
            foreach (var p in ps) if (!p.friendly && !_born.ContainsKey(p)) _born[p] = t;
            if (_born.Count > 4 * ps.Count + 64) Prune(ps);

            bool alt = false, useActive = false, blink = false;
            if (t >= _decideT)
            {
                // re-decide every `react` ticks, as a person does; hold the decision between
                _decideT = t + _react;
                _aimJ = _rnd.Gauss() * _p.AimErr;
                if (t >= _strafeT) { _strafe = -_strafe; _strafeT = t + Balance.TickHz * StrafeS * (1 + _rnd.Next() * 2); }
                double dx = target.x - pl.x, dy = target.y - pl.y, dist = Hyp(dx, dy);
                if (dist == 0) dist = 1;
                double ux = dx / dist, uy = dy / dist;
                double mx = -uy * _strafe, my = ux * _strafe;                   // circle the target
                if (dist < _p.BandMin) { mx -= ux * 1.5; my -= uy * 1.5; }      // too close: back off
                else if (dist > _p.BandMax) { mx += ux * 1.2; my += uy * 1.2; } // too far: close in
                var b = r.Bounds; const double m = 70;                          // and stay off the walls
                if (pl.x < b.L + m) mx += 1;
                if (pl.x > b.R - m) mx -= 1;
                if (pl.y < b.T + m) my += 1;
                if (pl.y > b.B - m) my -= 1;
                _mvX = mx; _mvY = my;
                _fire = dist < 420;
                int crowd = 0;
                foreach (var e in run.enemies) if (e.hp > 0 && Hyp(e.x - pl.x, e.y - pl.y) < 150) crowd++;
                if (crowd >= 3 && _rnd.Next() < _p.Blast) alt = true;
                if (Balance.Tuned && Balance.TuneItems) useActive = WantsActive(run);   // the overhaul's actives, by their rules
                else if (pl.hp + pl.regenHeart <= _p.Heal && _rnd.Next() < 0.5) useActive = true;   // JS hp: every heart
            }

            // dodging: only a shot the bot has had time to react to, heading for it
            bool sidestep = false;
            foreach (var p in ps)
            {
                if (p.friendly || t - _born[p] < _react) continue;
                double rx = p.x - pl.x, ry = p.y - pl.y, vv = p.vx * p.vx + p.vy * p.vy;
                if (vv == 0) continue;
                double tc = -(rx * p.vx + ry * p.vy) / vv;
                if (tc < 0 || tc > Balance.SecF(DodgeLookS)) continue;   // tc is in ticks: shot speeds are px per tick
                double cx = rx + p.vx * tc, cy = ry + p.vy * tc;
                if (Hyp(cx, cy) < p.r + pl.r + 8)
                {
                    double s = (cx * p.vy - cy * p.vx) > 0 ? 1 : -1, vn = Math.Sqrt(vv);
                    _mvX = -p.vy / vn * s * 2; _mvY = p.vx / vn * s * 2;
                    sidestep = true;
                    if (tc < Balance.SecF(BlinkLookS) && run.blinkCharges > 0 && _rnd.Next() < Balance.Chance(_p.Dodge * 0.15)) blink = true;   // a roll per tick
                    break;
                }
            }
            Marks(run, r, t, sidestep, ref blink);
            FloorGuard(run);
            if (blink && BlinksOntoFire(run)) blink = false;   // a player does not blink into a burning or cracking floor

            double a = Math.Atan2(target.y - pl.y, target.x - pl.x) + _aimJ;
            double td = Hyp(target.x - pl.x, target.y - pl.y);
            _aimX = pl.x + Math.Cos(a) * td; _aimY = pl.y + Math.Sin(a) * td;
            var input = new Input(Axis(_mvX, 0.3), Axis(_mvY, 0.3), _fire, alt, blink, _aimX.Value, _aimY.Value);
            return new BotAction(input, useActive);
        }

        /// <summary>The floor's marks: a Mortar shell's landing and a leaping Burrower's, each a circle that hurts once, as it... [h:Policy-2]</summary>
        internal bool Marks(RunState run, Room room, int t, bool sidestep, ref bool blink)
        {
            var pl = run.player;
            double hx = pl.lagX, hy = pl.lagY + Balance.PlayerHitDy, ex = 0, ey = 0, need = 0;
            int soonest = int.MaxValue, lineSoonest = int.MaxValue;
            if (room != _tellRoom) { _tell.Clear(); _tellRoom = room; }   // a new room: every tell is a fresh one
            foreach (var p in run.projectiles)
            {
                if (p.friendly || !p.lob || !_born.TryGetValue(p, out var born) || t - born < _react) continue;
                Threat(p.tx, p.ty, p.blastR, p.flightMax - p.flight);
            }
            foreach (var e in run.enemies)
            {
                if (e.hp <= 0 || e.kind != BodyKind.Burrower || e.move != "leap") continue;
                if (Middle.BurrowLeap - e.moveT < _react) continue;   // airborne for less than a reaction: not seen yet
                Threat(e.markX, e.markY, Middle.BurrowHitR, e.moveT);
            }
            foreach (var e in run.enemies)
            {
                if (e.hp <= 0 || e.kind != BodyKind.SunkenKing) continue;
                // the King's leap: its landing ring, once it has been in the air a reaction time; then its floor
                if (e.move == "leap" && e.fbTellMax - e.moveT >= _react) Threat(e.markX, e.markY, FinalBoss.LeapR, e.moveT);
                if (e.ground != null) Floor(e.ground);
            }
            Lines(run, t, Line);
            if (soonest == int.MaxValue) return false;
            double n = Hyp(ex, ey);
            if (n < 1e-9) { ex = -pl.vy; ey = pl.vx; n = Hyp(ex, ey); }     // dead centre and still: any way out
            if (n < 1e-9) { ex = _strafe; ey = 0; n = 1; }
            ex = ex / n * 2; ey = ey / n * 2;
            // a wall in the way out: slide along it, away from the nearer corner, instead of pressing into it
            var b = room.Bounds;
            bool Blocked(double x, double y) =>
                (x < -0.3 && pl.x < b.L + WallGap) || (x > 0.3 && pl.x > b.R - WallGap) || (y < -0.3 && pl.y < b.T + WallGap) || (y > 0.3 && pl.y > b.B - WallGap);
            // off a charge's line into a wall: cross to its other side, never slide along it (toward the body, maybe)
            if (lineSoonest == soonest && Blocked(ex, ey) && !Blocked(-ex, -ey)) { ex = -ex; ey = -ey; }
            if ((ex < 0 && pl.x < b.L + WallGap) || (ex > 0 && pl.x > b.R - WallGap)) { ex = 0; ey = Away(ey, pl.y, Balance.MidY); }
            if ((ey < 0 && pl.y < b.T + WallGap) || (ey > 0 && pl.y > b.B - WallGap)) { ey = 0; ex = Away(ex, pl.x, Balance.MidX); }
            // a shell being sidestepped this tick and a mark: both ways at once
            if (sidestep) { _mvX += ex; _mvY += ey; }
            else { _mvX = ex; _mvY = ey; }
            // out of reach in time on foot (at base speed, no bonus), and not still trailing a blink? then one, rolled as for a shot
            bool settled = Hyp(pl.x - pl.lagX, pl.y - pl.lagY) < 4;
            if (settled && need > pl.speed * soonest && run.blinkCharges > 0 && _rnd.Next() < Balance.Chance(_p.Dodge * 0.15)) blink = true;
            return true;

            double Away(double v, double at, double mid) => Math.Abs(v) > 0.3 ? Math.Sign(v) * 2 : at <= mid ? 2 : -2;

            // a body charging along a line: step off it, to the side the bot is already on
            void Line(double ax, double ay, double dx, double dy, double len, double r, double speed, int lead)
            {
                double reach = r + Balance.PlayerHitR + MarkMargin;
                double rx = hx - ax, ry = hy - ay, along = rx * dx + ry * dy;
                if (along < -reach || along > len + reach) return;
                double c = Math.Max(0, Math.Min(len, along));
                double px = rx - dx * c, py = ry - dy * c, d = Hyp(px, py);
                if (d >= reach) return;
                if (d < 1) { px = -dy * _strafe; py = dx * _strafe; d = 1; }   // dead on the line: its strafe side
                int ticksLeft = Math.Max(1, lead + (int)(Math.Max(0, along) / Math.Max(speed, 1e-6)));
                double w = Balance.Sec(1) / (ticksLeft + Balance.Sec(0.1));
                ex += px / d * w * reach; ey += py / d * w * reach;
                need = Math.Max(need, reach - d);
                soonest = Math.Min(soonest, ticksLeft);
                lineSoonest = Math.Min(lineSoonest, ticksLeft);
            }

            // the hitbox (or where it is heading: it trails the body) on a cell that burns, has fallen, or has cracked for a
            // reaction time: to the nearest point well inside a cell that does none
            void Floor(FinalBoss.Ground g)
            {
                double fx = pl.x, fy = pl.y + Balance.PlayerHitDy;
                int c = FinalBoss.Ground.CellAt(hx, hy), f = FinalBoss.Ground.CellAt(fx, fy);
                int bad = f >= 0 && Bad(g, f) ? f : c >= 0 && Bad(g, c) ? c : -1;
                if (bad < 0) return;
                if (c >= 0 && Bad(g, c)) { fx = hx; fy = hy; }   // the hitbox itself is on it: lead it off, not just the body
                double bx = 0, by = 0, bd = double.PositiveInfinity;
                for (int j = 0; j < FinalBoss.Ground.N; j++)
                {
                    if (Bad(g, j)) continue;
                    double l = FinalBoss.Ground.Left(j) + FloorIn, tp = FinalBoss.Ground.Top(j) + FloorIn;
                    double qx = Math.Max(l, Math.Min(l + FinalBoss.Ground.CellW - 2 * FloorIn, fx));
                    double qy = Math.Max(tp, Math.Min(tp + FinalBoss.Ground.CellH - 2 * FloorIn, fy));
                    double d = Hyp(qx - fx, qy - fy);
                    if (d < bd) { bd = d; bx = qx; by = qy; }
                }
                if (double.IsInfinity(bd) || bd < 1) return;   // the body is on good ground already: the hitbox follows it there
                int left = g.warn[bad] > 0 ? g.warn[bad] : 1;
                double w = FloorPull * Balance.Sec(1) / (left + Balance.Sec(0.1));
                ex += (bx - fx) / bd * w; ey += (by - fy) / bd * w;
                need = Math.Max(need, bd);
                soonest = Math.Min(soonest, left);
            }

            void Threat(double mx, double my, double r, int ticksLeft)
            {
                double reach = r + Balance.PlayerHitR + MarkMargin;
                double dx = hx - mx, dy = hy - my, d = Hyp(dx, dy);
                int ahead = Math.Min(ticksLeft, Balance.Sec(CourseS));   // where its present course puts it, a short look ahead
                double fx = dx + pl.vx * ahead, fy = dy + pl.vy * ahead;
                if (d >= reach && Hyp(fx, fy) >= reach) return;
                double w = 1 / Math.Max(d, 1) * Balance.Sec(1) / (ticksLeft + Balance.Sec(0.1));   // the sooner it lands, the more it counts
                ex += dx * w * reach; ey += dy * w * reach;
                need = Math.Max(need, reach - d);
                soonest = Math.Min(soonest, Math.Max(ticksLeft, 1));
            }
        }

        // every body charging or about to charge along a line the bot has watched for its reaction time: its start, its
        // way, its length, its radius, its speed (px a tick) and the ticks before it moves
        void Lines(RunState run, int t, Action<double, double, double, double, double, double, double, int> line)
        {
            foreach (var e in run.enemies)
            {
                bool lunger = e.kind == BodyKind.Lunger && (e.lungeState == "wind" || e.lungeState == "lunge");
                bool brunch = e.kind == BodyKind.Brunch && (e.rushWind > 0 || e.rushing);
                bool keeper = e.kind == BodyKind.Gatekeeper && (e.move == "tell" || e.move == "charge");
                bool king = e.kind == BodyKind.SunkenKing && (e.move == "tell" || e.move == "charge" || e.move == "rift");
                if (e.hp <= 0 || !(lunger || brunch || keeper || king)) { _tell.Remove(e); _kingMove.Remove(e); continue; }
                if (king)
                {
                    // the risen King's second line is a new tell: its reaction time starts again
                    if (_kingMove.TryGetValue(e, out var was) && was == "charge" && e.move == "tell") _tell.Remove(e);
                    _kingMove[e] = e.move;
                }
                if (!_tell.TryGetValue(e, out var seen)) _tell[e] = seen = t;
                if (t - seen < _react) continue;
                if (king && e.move == "rift")
                {
                    // the King's rift: its strips are the whole tell, and they erupt all at once as the tell runs out
                    var g = e.ground!;
                    for (int k = 0; k < g.rifts; k++) line(g.riftX, g.riftY, g.rdx[k], g.rdy[k], g.rlen[k], FinalBoss.RiftW / 2, 1e9, e.moveT);
                }
                else if (king)
                {
                    bool tell = e.move == "tell";
                    line(e.x, e.y, e.fbDx, e.fbDy, e.fbLen - (tell ? 0 : e.fbGone), e.r, FinalBoss.ChargeSpeed, tell ? e.moveT : 0);
                }
                else if (keeper)
                {
                    // the Gatekeeper's line is locked as its tell begins and drawn whole: the ground it has left to cover
                    bool tell = e.move == "tell";
                    line(e.x, e.y, e.fbDx, e.fbDy, e.fbLen - (tell ? 0 : e.fbGone), e.r, FloorBoss.ChargeSpeed, tell ? e.moveT : 0);
                }
                else if (lunger)
                {
                    // the whole lunge, rounded up to its last full step; mid-lunge the same length from here (never short of
                    // what is left: an eased lunge starts slow)
                    bool wind = e.lungeState == "wind";
                    double len = Math.Ceiling(e.lungeLen / Balance.LungeSpeed) * Balance.LungeSpeed;
                    line(e.x, e.y, e.lungeDx, e.lungeDy, len, e.r, Balance.LungeSpeed, wind ? e.lungeT : 0);
                }
                else if (e.rushing)
                {
                    double v = TickOrder.DashSpeed(e);
                    line(e.x, e.y, e.rushDx, e.rushDy, e.rushT * v, e.r, v, 0);
                }
                else
                {
                    // the tell points at the player and the dash runs past them: the bot reads it as reaching its hitbox and beyond
                    var pl = run.player;
                    double gx = pl.lagX - e.x, gy = pl.lagY + Balance.PlayerHitDy - e.y, g = Hyp(gx, gy);
                    if (g > 1e-9) line(e.x, e.y, gx / g, gy / g, g + Balance.BrunchDashOver, e.r, TickOrder.DashSpeed(e), e.rushWind);
                }
            }
            if (_tell.Count <= 64) return;
            _pruneTell.Clear();
            foreach (var e in _tell.Keys) if (!run.enemies.Contains(e)) _pruneTell.Add(e);
            foreach (var e in _pruneTell) _tell.Remove(e);
        }

        /// <summary>A floor cell the bot keeps off: fallen, burning, or cracking for at least its reaction time (a crack it has
        /// not had time to see is ground like any other).</summary>
        internal bool Bad(FinalBoss.Ground g, int i) => g.pit[i] || g.burn[i] > 0 || (g.warn[i] > 0 && g.warnMax[i] - g.warn[i] >= _react);

        // its move, held off a cell it would walk into that burns, has fallen or has cracked: one axis if the other is clear
        void FloorGuard(RunState run)
        {
            FinalBoss.Ground? g = null;
            foreach (var e in run.enemies) if (e.hp > 0 && e.ground != null) g = e.ground;
            if (g == null) return;
            var pl = run.player;
            double hx = pl.x, hy = pl.y + Balance.PlayerHitDy;   // the body, which the trailing hitbox follows
            int c = FinalBoss.Ground.CellAt(hx, hy);
            if (c >= 0 && Bad(g, c)) return;   // already on bad ground: Marks is walking it off
            int sx = Axis(_mvX, 0.3), sy = Axis(_mvY, 0.3);
            if (sx == 0 && sy == 0) return;
            bool Into(int ax, int ay) { int k = FinalBoss.Ground.CellAt(hx + ax * FloorLook, hy + ay * FloorLook); return k >= 0 && Bad(g, k); }
            if (!Into(sx, sy)) return;
            if (sx != 0 && sy != 0 && !Into(sx, 0)) { _mvY = 0; return; }
            if (sx != 0 && sy != 0 && !Into(0, sy)) { _mvX = 0; return; }
            _mvX = 0; _mvY = 0;
        }

        // where a blink along the held move would land (the game's own rule, Tick.DoBlink, for a held direction) is bad ground
        bool BlinksOntoFire(RunState run)
        {
            FinalBoss.Ground? g = null;
            foreach (var e in run.enemies) if (e.hp > 0 && e.ground != null) g = e.ground;
            int sx = Axis(_mvX, 0.3), sy = Axis(_mvY, 0.3);
            if (g == null || (sx == 0 && sy == 0)) return false;
            var pl = run.player;
            double n = Hyp(sx, sy);
            int k = FinalBoss.Ground.CellAt(pl.x + sx / n * Balance.BlinkDist, pl.y + Balance.PlayerHitDy + sy / n * Balance.BlinkDist);
            return k >= 0 && Bad(g, k);
        }

        const double FloorIn = 26, FloorLook = 26, FloorPull = 400;   // px kept inside a safe cell; px looked ahead; the floor's pull

        /// <summary>The move the bot holds now (its decision, after any dodging), for the tests.</summary>
        internal (double x, double y) Move => (_mvX, _mvY);

        const double AddNear = 260;   // px: a recalled add this close is shot before the King
        const double MarkMargin = 8, WallGap = 40, CourseS = 0.5;   // px kept from a mark; px from a wall that blocks a run; s of course looked ahead

        /// <summary>The overhaul's actives, each by its plain rule (docs/items.md), and only when charged.</summary>
        internal static bool WantsActive(RunState run)
        {
            if (!Items.Ready(run)) return false;
            var pl = run.player;
            int near(double reach)
            {
                int n = 0;
                foreach (var e in run.enemies) if (e.hp > 0 && !e.buried && Hyp(e.x - pl.x, e.y - pl.y) < reach) n++;
                return n;
            }
            switch (Items.ActiveItem(run)!.Id)
            {
                case "tin_cup": return pl.maxHp - pl.hp >= Balance.HeartHeal;   // it heals red hearts only
                case "hush_bell":
                {
                    int close = 0;
                    foreach (var s in run.projectiles)
                    {
                        if (s.friendly || s.lob) continue;
                        double d = Hyp(s.x - pl.x, s.y - pl.y);
                        if (d < 40) return true;
                        if (d < 150) close++;
                    }
                    return close >= 3;
                }
                case "time_glass": return near(250) >= 4 || (near(double.PositiveInfinity) > 0 && pl.hp + pl.regenHeart <= (pl.maxHp + pl.regenHeartMax) / 2);
                case "hunters_mark": return near(double.PositiveInfinity) >= 3;
                default: return false;
            }
        }

        void Prune(List<Projectile> ps)
        {
            _prune.Clear();
            foreach (var kv in _born) if (!ps.Contains(kv.Key)) _prune.Add(kv.Key);
            foreach (var p in _prune) _born.Remove(p);
        }

        BotAction Quiet(RunState run, Room r)
        {
            // the quiet room: pickups worth having, then the way on
            var pl = run.player;
            if (_itemGoal != null && _itemGoalIn == r && !run.pickups.Contains(_itemGoal)) _tookItemIn.Add(r);
            _itemGoal = null;
            Pickup? want = null, exit = null;
            foreach (var pk in run.pickups)
            {
                if (pk.kind == "exit") { exit ??= pk; continue; }
                if (want != null || pk.hold || !Wanted(run, r, pk)) continue;
                want = pk;
            }
            var goal = want ?? exit;
            if (goal != null)
            {
                if (goal.kind == "item" && Hyp(goal.x - pl.x, goal.y - pl.y) < goal.r + pl.r + 2) _tookItemIn.Add(r);
                if (goal.kind == "item") { _itemGoal = goal; _itemGoalIn = r; }
                return Steer(pl, goal.x, goal.y, 3);
            }
            var d = Route(run);
            if (d == null) return Steer(pl, pl.x, pl.y, 4);
            var (px, py) = Rooms.DoorPoint(d.Value);
            return Steer(pl, px + Math.Sign(px - Balance.MidX) * 40, py + Math.Sign(py - Balance.MidY) * 40, 4);
        }

        bool Wanted(RunState run, Room r, Pickup pk)
        {
            var pl = run.player;
            switch (pk.kind)
            {
                case "heart": case "halfheart": return pl.hp < pl.maxHp;
                case "armor": case "halfarmor": return pl.armor < Balance.MaxArmor;
                case "item": return !_tookItemIn.Contains(r);
                case "weapon": case "hook": case "blast": return false;
                default: return true;   // keys, and anything new
            }
        }

        BotAction Steer(Player pl, double tx, double ty, double stop) =>
            new BotAction(new Input(Axis(tx - pl.x, stop), Axis(ty - pl.y, stop),
                                    aimX: _aimX ?? pl.x + 200, aimY: _aimY ?? pl.y));

        /// <summary>BFS over the floor through doors that exist and are open or that the bot holds the key for. [h:Policy-1]</summary>
        internal Dir? Route(RunState run)
        {
            var start = run.CurrentRoom!;
            _q.Clear(); _prev.Clear(); _reach.Clear();
            _prev[start] = (null, Dir.N); _reach.Add(start); _q.Enqueue(start);
            while (_q.Count > 0)
            {
                var r = _q.Dequeue();
                foreach (var d in Dirs)
                {
                    if (!r.Doors.Contains(d)) continue;
                    var n = run.dungeon.Neighbour(r, d);
                    if (n == null || _prev.ContainsKey(n)) continue;
                    if (Rooms.DoorSealed(run, r, d) && !Rooms.HasKeyFor(run, r, d)) continue;
                    _prev[n] = (r, d); _reach.Add(n); _q.Enqueue(n);
                }
            }
            Room? unvisited = null, item = null, boss = null;
            foreach (var x in _reach)
            {
                if (unvisited == null && !x.Visited && x.Type != RoomKind.Boss) unvisited = x;
                if (item == null && x.Type == RoomKind.Item && !_tookItemIn.Contains(x)) item = x;
                if (boss == null && x.Type == RoomKind.Boss) boss = x;
            }
            var pick = unvisited ?? item ?? boss;
            if (pick == null || pick == start) return null;
            var step = pick;
            while (_prev[step].from != start) step = _prev[step].from!;
            return _prev[step].d;
        }
    }

    /// <summary>A player who never touches the controls: for the stuck-detection test.</summary>
    public sealed class IdlePolicy : IPolicy
    {
        public BotAction Decide(RunState run, int t) => BotAction.None;
    }
}
