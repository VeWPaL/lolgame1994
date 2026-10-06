using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Depths.Playtest
{
    /// <summary>Owns one run: the loop, metrics and stuck detection. It applies the policy's input as a
    /// keyboard would and observes HP across each step, never hooking the game.</summary>
    public sealed class Runner
    {
        public const double StuckSeconds = 60;

        public readonly RunState Run;
        public readonly RunResult Result;
        readonly IPolicy _policy;
        readonly int _cap, _stuck, _hz;
        FloorRecord _f = null!;

        // per-step snapshots, reused so the loop does not allocate
        readonly List<Pickup> _pk0 = new List<Pickup>();
        readonly List<Projectile> _shots0 = new List<Projectile>();
        readonly List<(BodyKind kind, double x, double y, double r)> _bodies0 =
            new List<(BodyKind, double, double, double)>();
        double _hp0, _ar0, _rg0, _taken0;
        int _rgT0, _live0, _floor0, _cx0, _cy0;

        // a scratch run the game's own regen and damage functions are replayed on; the real run is never touched
        readonly RunState _probe = new RunState(new Rng(0));
        readonly Enemy _probeBody = Enemy.Of(BodyKind.Lunger, 0, 0);

        public Runner(uint seed, string profile, double minutes, IPolicy? policy = null)
        {
            Run = new RunState(new Rng(seed));
            Run.Start(seed);
            _policy = policy ?? new BotPolicy(Profile.Get(profile), seed);
            _hz = Balance.TickHz;
            _cap = (int)Math.Round(minutes * 60 * _hz);
            _stuck = (int)(StuckSeconds * _hz);
            Result = new RunResult { seed = seed, profile = profile };
        }

        void NewFloor()
        {
            var p = Run.player;
            _f = new FloorRecord { floor = Run.floor, hpIn = Hearts(p), maxHpIn = p.maxHp + p.regenHeartMax,
                                   armorIn = p.armor };
            Result.floors.Add(_f);
        }

        // closed after the descending step, so that step's damage and the HP out are on the same floor
        void CloseFloor()
        {
            _f.hpOut = Hearts(Run.player); _f.armorOut = Run.player.armor;
        }

        public RunResult Play()
        {
            var o = Result;
            NewFloor();
            int lastX = -1, lastY = -1, lastFloor = -1;
            Room? lastCleared = null;
            int lastProgress = 0, prevKills = 0;
            double lastHpSum = 0;
            for (int t = 0; t < _cap; t++)
            {
                if (Run.state != "playing") { o.end = Run.state == "gameover" ? "death" : Run.state; break; }
                var r = Run.CurrentRoom!;
                o.ticks++; _f.ticks++;
                if (r.Type == RoomKind.Boss && Run.enemies.Count > 0) _f.bossTicks++;
                if (Run.curX != lastX || Run.curY != lastY || Run.floor != lastFloor)
                {
                    lastX = Run.curX; lastY = Run.curY; lastFloor = Run.floor;
                    _f.rooms++; lastProgress = t;
                }
                if (Run.kills != prevKills) { prevKills = Run.kills; lastProgress = t; }
                // damage dealt is progress too: a deep boss fight runs past a minute without a kill
                double hpSum = 0;
                foreach (var e in Run.enemies) hpSum += Math.Max(0, e.hp);
                if (hpSum < lastHpSum - 1e-9) lastProgress = t;
                lastHpSum = hpSum;
                if (t - lastProgress > _stuck) { o.end = "stuck"; o.cause = StuckCause(r); break; }

                int floorBefore = Run.floor;
                string where = "game";   // who threw: the bot's decision, or the game's own code
                try
                {
                    if (Run.trans != null) { Step(Input.None); continue; }
                    bool live = false;
                    foreach (var e in Run.enemies) if (e.hp > 0) { live = true; break; }
                    if (!live && r.Cleared && Run.enemies.Count == 0 && lastCleared != r) { lastCleared = r; _f.cleared++; }

                    where = "bot";
                    var act = _policy.Decide(Run, t);
                    where = "game";
                    if (act.UseActive && Run.state == "playing") UseActive();
                    if (act.Input.blink && TickOrder.TryBlink(Run, act.Input)) { o.blinks++; o.dodgeBlinks++; }
                    Step(act.Input);
                    if (Run.floor != floorBefore)
                    {
                        _f.bossKilled = true; CloseFloor();
                        NewFloor();
                        lastProgress = t;
                    }
                }
                catch (Exception e)
                {
                    // a step that threw after the descent still went down: close the floor it left
                    if (Run.floor != floorBefore) _f.bossKilled = true;
                    o.errors.Add(ErrorText(where, e));
                    o.end = "error";
                    break;
                }
            }
            if (o.end == "timeout" && Run.state != "playing") o.end = Run.state == "gameover" ? "death" : Run.state;
            o.floor = Run.floor; o.kills = Run.kills; o.shots = Run.shots; o.hits = Run.hits;
            o.secrets = Run.secret ? 1 : 0;
            o.weapon = Weapons.All[Run.player.weaponIdx].Name;
            if (o.cause == null && o.end == "death") o.cause = TopSource(o.dmgBySource);
            if (_f.hpOut == null) CloseFloor();
            return o;
        }

        // the JS bot's hp: every heart, red and regenerating
        static double Hearts(Player p) => p.hp + p.regenHeart;

        // as the JS: a use counts only if it raised HP; a press that did not is kept visible too
        void UseActive()
        {
            var p = Run.player;
            double hp = p.hp, armor = p.armor;
            Result.qPresses++;
            Items.UseActive(Run);
            double gain = Math.Max(0, p.hp - hp) + Math.Max(0, p.armor - armor);
            if (gain > 0) { Result.actives++; Heal("active", gain); }
        }

        /// <summary>One Update, with the HP layers compared across it: losses are damage, gains are healing.</summary>
        void Step(Input input)
        {
            var p = Run.player;
            _hp0 = p.hp; _ar0 = p.armor; _rg0 = p.regenHeart; _rgT0 = p.regenHeartT; _taken0 = Run.dmgTaken;
            _live0 = Run.enemies.Count; _floor0 = Run.floor; _cx0 = Run.curX; _cy0 = Run.curY;
            _pk0.Clear(); _pk0.AddRange(Run.pickups);
            _shots0.Clear();
            foreach (var s in Run.projectiles) if (!s.friendly) _shots0.Add(s);
            _bodies0.Clear();
            foreach (var e in Run.enemies) _bodies0.Add((e.kind, e.x, e.y, e.r));

            try { TickOrder.Update(Run, input); }
            finally { Observe(); }   // a step that throws is still counted
        }

        void Observe()
        {
            var p = Run.player;
            bool sameRoom = Run.floor == _floor0 && Run.curX == _cx0 && Run.curY == _cy0;
            string? by = null;   // the heal pickup that left the floor this step, if one did
            if (sameRoom)
                foreach (var pk in _pk0)
                {
                    if (Run.pickups.Contains(pk)) continue;
                    bool heals = pk.kind == "heart" || pk.kind == "halfheart" || pk.kind == "armor" || pk.kind == "halfarmor";
                    if (heals) by ??= pk.kind;
                    if (pk.kind == "item") Result.items.Add(pk.id + "@" + _floor0);
                }

            // the tick runs regen, then at most one landed hit (i-frames), then pickups; with a hit,
            // the first two are replayed on the probe and what is left over is the pickups
            double hpX = _hp0, arX = _ar0, rgX = _rg0, hit = 0;
            string? src = null;
            if (Run.dmgTaken > _taken0)
            {
                src = Source();
                var q = _probe.player;
                q.hp = _hp0; q.maxHp = p.maxHp; q.armor = _ar0; q.regenHeart = _rg0; q.regenHeartMax = p.regenHeartMax;
                q.regenHeartT = _rgT0; q.iframes = 0;
                _probe.blinkGrace = 0; _probe.graceSpent = false; _probe.state = "playing";
                _probe.enemies.Clear();
                if (_live0 > 0) _probe.enemies.Add(_probeBody);
                TickOrder.TickRegen(_probe);
                double refill = q.regenHeart - _rg0;
                if (refill > 0) Regen(refill);
                double h0 = q.hp, a0 = q.armor, r0 = q.regenHeart;
                bool boss = src.EndsWith(":boss", StringComparison.Ordinal);   // the Warden's hits cost armour in full
                Combat.DamagePlayer(_probe, Run.dmgTaken - _taken0, 0, 0, 0, boss);
                hit = (h0 - q.hp) + (a0 - q.armor) + (r0 - q.regenHeart);
                hpX = q.hp; arX = q.armor; rgX = q.regenHeart;
                double lost = Math.Max(0, _hp0 - p.hp) + Math.Max(0, _ar0 - p.armor) + Math.Max(0, _rg0 - p.regenHeart);
                if (hit > lost + 1e-9) { Result.maskedHits++; Result.maskedHp += hit - lost; }
            }
            // what the hit (if any) does not explain: gains are pickups or regen, losses are damage
            double dHp = p.hp - hpX, dAr = p.armor - arX, dRg = p.regenHeart - rgX;
            double dmg = hit + Math.Max(0, -dHp) + Math.Max(0, -dAr) + Math.Max(0, -dRg);
            if (dmg > 0)
            {
                src ??= Source();
                Result.dmgBySource[src] = Result.dmgBySource.TryGetValue(src, out var v) ? v + dmg : dmg;
                _f.dmg += dmg;
            }
            if (dRg > 0) Regen(dRg);
            // no pickup left the floor: a drop spawned and taken in the same tick; only its layer is known
            if (dHp > 0) Heal(by ?? "unseen:red", dHp);
            if (dAr > 0) Heal(by ?? "unseen:armour", dAr);
        }

        void Regen(double hp)
        {
            Heal("regen", hp);
            _f.regen += hp; Result.regenHealed += hp;
        }

        void Heal(string by, double gain)
        {
            Result.healed += gain; _f.healed += gain;
            Result.healBy[by] = Result.healBy.TryGetValue(by, out var v) ? v + gain : gain;
        }

        /// <summary>A hostile shot gone after the step within 40px of the player or its shot hitbox; else
        /// the body nearest the contact hitbox, by distance less its radius, as the game tests it.</summary>
        string Source()
        {
            var p = Run.player;
            double best = 40;
            Projectile? near = null;
            double sx = p.lagX, sy = p.lagY + Balance.PlayerHitDy;   // shells land on the lagged hitbox
            foreach (var s in _shots0)
            {
                if (Run.projectiles.Contains(s)) continue;
                double d = Math.Min(Math.Sqrt((s.x - p.x) * (s.x - p.x) + (s.y - p.y) * (s.y - p.y)),
                                    Math.Sqrt((s.x - sx) * (s.x - sx) + (s.y - sy) * (s.y - sy)));
                if (d < best) { best = d; near = s; }
            }
            if (near != null) return "shot:" + (near.owner != null ? Name(near.owner.kind) : "?");
            // the game's body contact is tested at (lagX, lagY - PlayerHitDy)
            double hx = p.lagX, hy = p.lagY - Balance.PlayerHitDy, bd = double.PositiveInfinity;
            string kind = "?";
            foreach (var e in Run.enemies)
            {
                double d = Math.Sqrt((e.x - hx) * (e.x - hx) + (e.y - hy) * (e.y - hy)) - e.r;
                if (d < bd) { bd = d; kind = Name(e.kind); }
            }
            foreach (var (k, x, y, r) in _bodies0)   // a body the hit killed (a Brunch spends itself)
            {
                double d = Math.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy)) - r;
                if (d < bd) { bd = d; kind = Name(k); }
            }
            return "contact:" + kind;
        }

        static string Name(BodyKind k) => k.ToString().ToLowerInvariant();

        object StuckCause(Room r)
        {
            var kinds = new List<string>();
            foreach (var pk in Run.pickups) kinds.Add(pk.kind);
            return new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["room"] = r.Type.ToString().ToLowerInvariant(),
                ["enemies"] = Run.enemies.Count,
                ["pickups"] = kinds,
                ["gold"] = Run.player.hasGold,
                ["silver"] = Run.player.hasSilver,
                ["bossUnlocked"] = Run.bossUnlocked,
                ["itemUnlocked"] = Run.itemUnlocked,
                ["at"] = new[] { Math.Round(Run.player.x), Math.Round(Run.player.y) },
                ["cell"] = Run.curX + "," + Run.curY,
            };
        }

        static object? TopSource(SortedDictionary<string, double> src)
        {
            string? k = null; double v = 0;
            foreach (var kv in src) if (k == null || kv.Value > v) { k = kv.Key; v = kv.Value; }
            return k == null ? null : new object[] { k, v };
        }

        /// <summary>"game: Type: message @ Class.Method": who threw, and the first frame in that side's
        /// assembly by name only (no paths, offsets or line numbers).</summary>
        static string ErrorText(string where, Exception e)
        {
            var asm = where == "bot" ? typeof(Runner).Assembly : typeof(RunState).Assembly;
            string frame = "?";
            foreach (var f in new StackTrace(e, false).GetFrames())
            {
                var m = f.GetMethod();
                if (m?.DeclaringType == null || m.DeclaringType.Assembly != asm) continue;
                frame = m.DeclaringType.Name + "." + m.Name;
                break;
            }
            string s = where + ": " + e.GetType().Name + ": " + e.Message + " @ " + frame;
            return s.Length > 300 ? s.Substring(0, 300) : s;
        }
    }
}
