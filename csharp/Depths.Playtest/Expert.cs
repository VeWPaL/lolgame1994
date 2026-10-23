using System;
using System.Collections.Generic;

namespace Depths.Playtest
{
    // The expert's fight (Profile.Expert). It reads what the screen draws: bodies (where; their speed from two looks),
    // shells (where, and their flight), tells, marks and lines (where, and how far along), the floor's cells. Never an RNG.
    public sealed partial class BotPolicy
    {
        /// <summary>A circle that hurts the hitbox from tick t0 to t1 (from now), at (x, y) on t0 and moving (vx, vy) a tick.</summary>
        internal struct Disc
        {
            public double x, y, vx, vy, r, w;
            public int t0, t1;
        }

        readonly int _decide;                                   // ticks between decisions
        readonly List<Disc> _discs = new List<Disc>();
        readonly Dictionary<Enemy, (double x, double y, int t, int first)> _look =
            new Dictionary<Enemy, (double, double, int, int)>(ReferenceEqualityComparer.Instance);
        readonly Dictionary<Enemy, (double vx, double vy)> _vel =
            new Dictionary<Enemy, (double, double)>(ReferenceEqualityComparer.Instance);
        readonly List<Enemy> _pruneLook = new List<Enemy>();
        readonly double[] _hx = new double[64], _hy = new double[64], _bx = new double[64], _by = new double[64];
        readonly bool[] _safe = new bool[64];
        FinalBoss.Ground? _g;
        double _aim = double.NaN;                               // the mouse's direction from the player, radians
        double _tx, _ty, _tvx, _tvy;                             // the target as last looked at, and its speed
        int _lookT, _xmx, _xmy;
        bool _xfire;

        /// <summary>Decisions taken so far, and the last plan's cost (for the tests).</summary>
        internal int Decisions;
        internal double AimNoise => _aimJ;   // this decision's aim error, radians
        internal int Threatened => _discs.Count;   // the threats the last plan weighed
        internal double LastCost;
        internal const int MaxPerSecond = 8;   // a hand's limit: decisions a second (the beat is never faster)

        const int MaxH = 63, Forever = 1 << 20;
        const double FloorAfterS = 1.0;
        const double FieldRange = 230;   // px kept from anything but the King
        const double Horizon = 0.5, Margin = 10, WallBand = 90, ChargeCost = 0.6, LastChargeCost = 1.4;

        BotAction Expert(RunState run, Room room, int t, Enemy target)
        {
            var pl = run.player;
            foreach (var p in run.projectiles) if (!p.friendly && !_born.ContainsKey(p)) _born[p] = t;
            if (_born.Count > 4 * run.projectiles.Count + 64) Prune(run.projectiles);
            if (room != _tellRoom) { _tell.Clear(); _tellRoom = room; _look.Clear(); _vel.Clear(); }

            bool alt = false, useActive = false, blink = false;
            int key = 0;
            Lines(run, t, NoLine);   // only notes when each tell was first seen: its reaction clock runs from then
            if (t >= _decideT)
            {
                // a fixed beat: a threat waits for the first beat after it has been in sight a reaction time (0.15-0.28 s)
                _decideT = t + _decide;
                Decisions++;
                Look(run, t);
                _aimJ = _rnd.Gauss() * _p.AimErr;
                var (vx, vy) = _vel.TryGetValue(target, out var v) ? v : (0, 0);
                _tx = target.x; _ty = target.y; _tvx = vx; _tvy = vy; _lookT = t;
                double d = Hyp(target.x - pl.x, target.y - pl.y);
                int gun = Gun(run, target, d, pl.weaponIdx);
                if (gun != pl.weaponIdx) key = gun + 1;
                blink = Plan(run, room, t, target, gun);
                _xfire = !target.buried && d < 650;
                int crowd = 0;
                foreach (var e in run.enemies) if (e.hp > 0 && !e.buried && Hyp(e.x - pl.x, e.y - pl.y) < 150) crowd++;
                if (crowd >= 3 && _rnd.Next() < _p.Blast) alt = true;
                if (Balance.Tuned && Balance.TuneItems) useActive = WantsActive(run);
                else if (pl.hp + pl.regenHeart <= _p.Heal && _rnd.Next() < 0.5) useActive = true;
            }

            // the mouse: toward where the target will be when the shot gets there, never faster than a wrist turns
            int gi = key > 0 ? key - 1 : pl.weaponIdx;
            double since = t - _lookT;
            double px = _tx + _tvx * since, py = _ty + _tvy * since;
            double fly = Hyp(px - pl.x, py - pl.y) / Math.Max(1e-6, Weapons.All[gi].Speed);
            px += _tvx * fly; py += _tvy * fly;
            double goal = Math.Atan2(py - pl.y, px - pl.x) + _aimJ;
            if (double.IsNaN(_aim)) _aim = _aimX.HasValue ? Math.Atan2(_aimY!.Value - pl.y, _aimX.Value - pl.x) : goal;
            double step = _p.AimRate / Balance.TickHz;   // the profile is refused without a cap (the constructor)
            _aim += Math.Max(-step, Math.Min(step, Wrap(goal - _aim)));
            _aim = Wrap(_aim);
            double reach = Math.Max(80, Hyp(px - pl.x, py - pl.y));
            _aimX = pl.x + Math.Cos(_aim) * reach; _aimY = pl.y + Math.Sin(_aim) * reach;
            var input = new Input(_xmx, _xmy, _xfire, alt, blink, _aimX.Value, _aimY.Value);
            return new BotAction(input, useActive, key);
        }

        static readonly Action<double, double, double, double, double, double, double, int> NoLine = (a, b, c, d, e, f, g, h) => { };

        static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }

        // two looks at each body, a decision apart, give its speed: what an eye reads off a moving sprite
        void Look(RunState run, int t)
        {
            foreach (var e in run.enemies)
            {
                if (e.hp <= 0) continue;
                if (_look.TryGetValue(e, out var o))
                {
                    int dt = t - o.t;
                    if (dt > 0) _vel[e] = dt <= 3 * _decide ? ((e.x - o.x) / dt, (e.y - o.y) / dt) : (0, 0);
                    _look[e] = (e.x, e.y, t, o.first);
                }
                else _look[e] = (e.x, e.y, t, t);
            }
            if (_look.Count <= 64) return;
            _pruneLook.Clear();
            foreach (var e in _look.Keys) if (!run.enemies.Contains(e)) _pruneLook.Add(e);
            foreach (var e in _pruneLook) { _look.Remove(e); _vel.Remove(e); }
        }

        /// <summary>The gun with the most damage a second at this range on this body, as its owner knows the four
        /// (the table, the falloff, the spread); a switch only for a clear gain.</summary>
        internal int Gun(RunState run, Enemy target, double d, int held)
        {
            double strength = run.stats.Value("strength"), precision = run.stats.Value("precision");
            int best = held;
            double bv = GunDps(held, d, target, strength, precision) * 1.15;
            for (int g = 0; g < Weapons.All.Length; g++)
            {
                double v = GunDps(g, d, target, strength, precision);
                if (v > bv) { bv = v; best = g; }
            }
            return best;
        }

        internal double GunDps(int g, double d, Enemy target, double strength, double precision)
        {
            var w = Weapons.All[g];
            double fall = 1;
            if (w.FNear.HasValue && d > w.FNear.Value)
                fall = Math.Max(w.FMin, 1 - (1 - w.FMin) * (d - w.FNear.Value) / Math.Max(1, w.FFar - w.FNear.Value));
            double spread = w.MuzzleJitter.HasValue ? w.PelletAngle
                : w.SpreadFromPrecision ? w.Spread * Math.Max(Weapons.PrecisionSpreadFloor, 1 - Weapons.PrecisionStep * Math.Max(0, precision))
                : w.Spread;
            double lateral = (spread + _p.AimErr) * d + (w.MuzzleJitter ?? 0) + 1;
            double hit = Math.Min(1, (target.r + w.R) / lateral);
            double taken = Balance.Tuned && Balance.TuneBeam && target.kind == BodyKind.Brunch && g == Weapons.BeamIndex ? Balance.BrunchBeamTaken : 1;
            return (w.Dmg * w.Count + strength) * fall * hit * taken / w.CooldownS;
        }

        // the range the held gun wants; farther from a boss while its cast flash or charge line is drawn
        double Range(Enemy target, int gun)
        {
            double r = Weapons.All[gun].FNear ?? 150;
            r = Math.Min(r, 170) - 10;
            bool boss = target.kind == BodyKind.SunkenKing || target.kind == BodyKind.Gatekeeper;
            if (boss && (target.move == "cast" || target.move == "tell")) r += 50;   // a cast flash: room to read what comes
            if (target.kind != BodyKind.SunkenKing) r = Math.Max(r, FieldRange);   // a room of shooters: far enough to read their shots
            return r;
        }

        /// <summary>The expert's move: every way it can walk (or blink) for the next half second, scored on a danger map of
        /// what it has seen, plus the gun's range and the walls. Sets the held move; true for a blink.</summary>
        internal bool Plan(RunState run, Room room, int t, Enemy target, int gun)
        {
            var pl = run.player;
            Threats(run, t);
            int h = Math.Min(MaxH, Math.Max(2, Balance.Sec(Horizon)));
            double want = Range(target, gun);
            double best = double.PositiveInfinity;
            int bx = _xmx, by = _xmy;
            bool bBlink = false;
            bool settled = Hyp(pl.x - pl.lagX, pl.y - pl.lagY) < 6;
            bool canBlink = run.blinkCharges > 0 && settled;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1 && !canBlink) break;
                for (int mx = -1; mx <= 1; mx++)
                    for (int my = -1; my <= 1; my++)
                    for (int stop = 0; stop < 2; stop++)
                    {
                        bool blink = pass == 1;
                        if ((blink || stop == 1) && mx == 0 && my == 0) continue;
                        // held the whole look ahead, or a step to the next decision and then a stand
                        double c = Score(run, mx, my, blink, h, stop == 0 ? h : Math.Min(h, _decide), target, want);
                        if (blink) c += run.blinkCharges >= 2 ? ChargeCost : LastChargeCost;
                        if (mx == 0 && my == 0) c += 0.04;                       // momentum: keep moving while bodies live
                        if (mx != _xmx || my != _xmy) c += 0.01;                  // and do not dither
                        if (c < best) { best = c; bx = mx; by = my; bBlink = blink; }
                    }
            }
            _xmx = bx; _xmy = by; _mvX = bx; _mvY = by;
            LastCost = best;
            return bBlink;
        }

        // every threat seen for a reaction time, as discs over the next half second
        void Threats(RunState run, int t)
        {
            _discs.Clear();
            _g = null;
            foreach (var p in run.projectiles)
            {
                if (p.friendly || !_born.TryGetValue(p, out var born) || t - born < _react) continue;
                if (p.lob) Add(p.tx, p.ty, 0, 0, p.blastR, Math.Max(0, (int)(p.flightMax - p.flight)), -1, 2);
                else Add(p.x, p.y, p.vx, p.vy, p.r, 0, Forever, Math.Max(1, p.dmg));
            }
            foreach (var e in run.enemies)
            {
                if (e.hp <= 0) continue;
                if (e.ground != null) _g = e.ground;
                if (e.kind == BodyKind.Burrower && e.move == "leap")
                {
                    if (Middle.BurrowLeap - e.moveT >= _react) Add(e.markX, e.markY, 0, 0, Middle.BurrowHitR, e.moveT, -1, 2);
                    continue;   // in the air: its body does not touch
                }
                if (e.kind == BodyKind.SunkenKing)
                {
                    if (e.move == "leap" && e.fbTellMax - e.moveT >= _react) Add(e.markX, e.markY, 0, 0, FinalBoss.LeapR, e.moveT, -1, 2);
                    continue;   // its body hurts only as it charges: the lines below
                }
                if (e.kind == BodyKind.Gatekeeper || e.buried) continue;
                if (!_look.TryGetValue(e, out var lk) || t - lk.first < _react) continue;   // a body just come: not seen yet
                var (vx, vy) = _vel.TryGetValue(e, out var v) ? v : (0, 0);
                Add(e.x, e.y, vx, vy, e.r, 0, Forever, 1);
            }
            Lines(run, t, (ax, ay, dx, dy, len, r, speed, lead) =>
            {
                if (speed >= 1e8)
                {
                    // a rift's strip erupts whole as its tell runs out: discs along it, for its burst
                    int n = (int)Math.Ceiling(len / r) + 1;
                    for (int k = 0; k < n; k++) Add(ax + dx * len * k / Math.Max(1, n - 1), ay + dy * len * k / Math.Max(1, n - 1), 0, 0, r, lead, lead + FinalBoss.RiftFx, 2);
                    return;
                }
                int dur = (int)Math.Ceiling(len / Math.Max(speed, 1e-6));
                Add(ax, ay, dx * speed, dy * speed, r, lead, lead + dur, 2);
            });
        }

        void Add(double x, double y, double vx, double vy, double r, int t0, int t1, double w)
        {
            if (t1 < 0) t1 = t0;
            _discs.Add(new Disc { x = x, y = y, vx = vx, vy = vy, r = r, t0 = t0, t1 = t1, w = w });
        }

        // one way to go, played forward on the game's own movement rules; its hitbox trails, as the game's does
        double Score(RunState run, int mx, int my, bool blink, int h, int hold, Enemy target, double want)
        {
            var pl = run.player;
            double x = pl.x, y = pl.y, vx = pl.vx, vy = pl.vy, lx = pl.lagX, ly = pl.lagY;
            int ifr = pl.iframes;
            double n = Hyp(mx, my);
            if (blink)
            {
                lx = x; ly = y;
                x += mx / n * Balance.BlinkDist; y += my / n * Balance.BlinkDist;
                Clamp(ref x, ref y, pl.r);
                ifr = Math.Max(ifr, Balance.BlinkIframes + Balance.DashTrail);
            }
            double bonus = Math.Min(Balance.MomCap, Math.Min(Balance.MoveSpeedHardCap, run.stats.Value("speed")) + pl.momentum * Balance.MomSpeed);
            double spd = pl.speed * (1 + bonus);
            double tvx = n > 0 ? mx / n * spd : 0, tvy = n > 0 ? my / n * spd : 0;
            double acc = Balance.Ease(Balance.MoveAccel * (1 + pl.momentum * Balance.MomAccel)), lag = Balance.HitboxLagEase;
            double place = 0;
            for (int k = 0; k < h; k++)
            {
                if (k == hold) { tvx = 0; tvy = 0; }
                vx += (tvx - vx) * acc; vy += (tvy - vy) * acc;
                double ox = x, oy = y;
                x += vx; y += vy;
                if (Clamp(ref x, ref y, pl.r)) { vx = x - ox; vy = y - oy; }
                lx += (x - lx) * lag; ly += (y - ly) * lag;
                _bx[k] = x; _by[k] = y;
                _hx[k] = lx; _hy[k] = ly + Balance.PlayerHitDy;
                _safe[k] = k + 1 < ifr;
                if (k >= h / 2)
                {
                    double d = Hyp(target.x + 0 - x, target.y - y);
                    double off = (d - want) / 120;
                    double wall = Math.Min(Math.Min(x - Balance.RoomLeft, Balance.RoomRight - x), Math.Min(y - Balance.RoomTop, Balance.RoomBottom - y));
                    double wb = wall < WallBand ? (WallBand - wall) / WallBand : 0;
                    place += Math.Min(1.5, off * off) * 0.25 + wb * wb * 0.6;
                }
            }
            double cost = place / (h - h / 2);
            foreach (var dsc in _discs)
            {
                double worst = 0, reach = dsc.r + Balance.PlayerHitR;
                bool still = dsc.vx == 0 && dsc.vy == 0;
                int t0 = dsc.t0, t1 = dsc.t1;
                double far = 1;
                if (still && t0 >= h) { t0 = t1 = h - 1; far = 0.6; }   // a mark landing past the look ahead: be off it by then
                for (int k = Math.Max(0, t0 - 1); k < h && k <= t1 + 1; k++)
                {
                    if (_safe[k]) continue;
                    double tt = Math.Max(0, k + 1 - dsc.t0), ex = dsc.x + dsc.vx * tt, ey = dsc.y + dsc.vy * tt;
                    double dd = Hyp(_hx[k] - ex, _hy[k] - ey);
                    double hv = dd < reach ? 1 : dd < reach + Margin ? 0.3 * (1 - (dd - reach) / Margin) : 0;
                    if (hv > worst) { worst = hv; if (worst >= 1) break; }
                }
                cost += worst * dsc.w * far * 3;
            }
            if (_g != null) cost += FloorCost(_g, h) * 2 * 3;
            return cost;
        }

        static bool Clamp(ref double x, ref double y, double r)
        {
            double cx = Math.Max(Balance.RoomLeft + r, Math.Min(Balance.RoomRight - r, x));
            double cy = Math.Max(Balance.RoomTop + r, Math.Min(Balance.RoomBottom - r, y));
            bool hit = cx != x || cy != y;
            x = cx; y = cy;
            return hit;
        }

        // the King's floor under the trailing hitbox: fallen, burning, or (cracked long enough to be seen) about to burn
        double FloorCost(FinalBoss.Ground g, int h)
        {
            double on = 0, near = 0, end = 0;
            for (int k = 0; k < h; k++)
            {
                if (_safe[k]) continue;
                int c = FinalBoss.Ground.CellAt(_hx[k], _hy[k]);
                if (c >= 0 && BadAt(g, c, k + 1)) { on = Math.Max(on, 1) + 1.0 / h; continue; }   // on it at all: a hit; the sooner off, the better
                if (near < 0.3)
                    for (int s = 0; s < 4; s++)
                    {
                        int o = FinalBoss.Ground.CellAt(_hx[k] + (s == 0 ? 8 : s == 1 ? -8 : 0), _hy[k] + (s == 2 ? 8 : s == 3 ? -8 : 0));
                        if (o >= 0 && BadAt(g, o, k + 1)) { near = 0.3; break; }
                    }
            }
            // where the body ends, which the hitbox closes on: still good ground for a while after the look ahead?
            int e = FinalBoss.Ground.CellAt(_bx[h - 1], _by[h - 1] + Balance.PlayerHitDy);
            for (int s = 0; s <= 4 && e >= 0; s++) if (BadAt(g, e, h + s * Balance.Sec(FloorAfterS / 4))) { end = 0.8; break; }
            return on + near + end;
        }

        bool BadAt(FinalBoss.Ground g, int i, int ahead)
        {
            if (g.pit[i] || g.burn[i] > ahead) return true;
            if (g.warn[i] > 0 && g.warnMax[i] - g.warn[i] >= _react) return g.warn[i] <= ahead && (g.falls[i] || g.warn[i] + FinalBoss.Burn >= ahead);
            return false;
        }
    }
}
