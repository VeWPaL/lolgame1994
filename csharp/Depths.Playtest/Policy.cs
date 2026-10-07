using System;
using System.Collections.Generic;

namespace Depths.Playtest
{
    /// <summary>What a player does this tick: the held input, plus the Q key (a press, between ticks).</summary>
    public readonly struct BotAction
    {
        public readonly Input Input;
        public readonly bool UseActive;

        public BotAction(Input input, bool useActive = false) { Input = input; UseActive = useActive; }

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
    public sealed class BotPolicy : IPolicy
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
            _strafe = _rnd.Next() < 0.5 ? 1 : -1;
        }

        static double Hyp(double x, double y) => Math.Sqrt(x * x + y * y);
        static int Axis(double v, double dead) => (v > dead ? 1 : 0) - (v < -dead ? 1 : 0);

        public BotAction Decide(RunState run, int t)
        {
            var r = run.CurrentRoom;
            if (r == null || run.trans != null) return BotAction.None;
            var pl = run.player;

            Enemy? target = null, nearest = null;
            double nd = double.PositiveInfinity;
            int live = 0;
            foreach (var e in run.enemies)
            {
                if (e.hp <= 0) continue;
                live++;
                if (target == null && e.kind == BodyKind.Boss) target = e;
                double d = Hyp(e.x - pl.x, e.y - pl.y);
                if (d < nd) { nd = d; nearest = e; }
            }
            return live > 0 ? Fight(run, r, t, target ?? nearest!) : Quiet(run, r);
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
                if (pl.hp + pl.regenHeart <= _p.Heal && _rnd.Next() < 0.5) useActive = true;   // JS hp: every heart
            }

            // dodging: only a shot the bot has had time to react to, heading for it
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
                    if (tc < Balance.SecF(BlinkLookS) && run.blinkCharges > 0 && _rnd.Next() < Balance.Chance(_p.Dodge * 0.15)) blink = true;   // a roll per tick
                    break;
                }
            }

            double a = Math.Atan2(target.y - pl.y, target.x - pl.x) + _aimJ;
            double td = Hyp(target.x - pl.x, target.y - pl.y);
            _aimX = pl.x + Math.Cos(a) * td; _aimY = pl.y + Math.Sin(a) * td;
            var input = new Input(Axis(_mvX, 0.3), Axis(_mvY, 0.3), _fire, alt, blink, _aimX.Value, _aimY.Value);
            return new BotAction(input, useActive);
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

        /// <summary>
        /// BFS over the floor through doors that exist and are open or that the bot holds the key for.
        /// Goals in a careful first-timer's order: unvisited rooms, the item room with a key, the boss.
        /// </summary>
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
