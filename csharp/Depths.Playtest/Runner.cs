using System;
using System.Collections.Generic;

namespace Depths.Playtest
{
    /// <summary>
    /// Owns one run: the loop, the metrics and stuck detection. The policy only returns input; the
    /// runner applies it the way a keyboard would (blink and Q between ticks, then one Update).
    /// Damage and healing are observed as HP changes across each step, never hooked into the game.
    /// </summary>
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
        readonly List<(BodyKind kind, double x, double y)> _bodies0 = new List<(BodyKind, double, double)>();

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
            _f = new FloorRecord { floor = Run.floor, hpIn = Run.player.hp, maxHpIn = Run.player.maxHp };
            Result.floors.Add(_f);
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
                double hpBefore = Run.player.hp;
                try
                {
                    if (Run.trans != null) { Step(Input.None); continue; }
                    bool live = false;
                    foreach (var e in Run.enemies) if (e.hp > 0) { live = true; break; }
                    if (!live && r.Cleared && Run.enemies.Count == 0 && lastCleared != r) { lastCleared = r; _f.cleared++; }

                    var act = _policy.Decide(Run, t);
                    if (act.UseActive && Run.state == "playing") UseActive();
                    if (act.Input.blink && TickOrder.TryBlink(Run, act.Input)) { o.blinks++; o.dodgeBlinks++; }
                    Step(act.Input);
                    if (Run.floor != floorBefore)
                    {
                        _f.hpOut = hpBefore; _f.bossKilled = true;
                        NewFloor();
                        lastProgress = t;
                    }
                }
                catch (Exception e)
                {
                    // a step that threw after the descent still went down: close the floor it left
                    if (Run.floor != floorBefore) { _f.hpOut = hpBefore; _f.bossKilled = true; }
                    o.errors.Add(Trim(e.GetType().Name + ": " + e.Message + " @ " + TopFrame(e)));
                    o.end = "error";
                    break;
                }
            }
            if (o.end == "timeout" && Run.state != "playing") o.end = Run.state == "gameover" ? "death" : Run.state;
            o.floor = Run.floor; o.kills = Run.kills; o.shots = Run.shots; o.hits = Run.hits;
            o.secrets = Run.secret ? 1 : 0;
            o.weapon = Weapons.All[Run.player.weaponIdx].Name;
            if (o.cause == null && o.end == "death") o.cause = TopSource(o.dmgBySource);
            _f.hpOut ??= Run.player.hp;
            return o;
        }

        void UseActive()
        {
            var p = Run.player;
            double hp = p.hp, armor = p.armor;
            if (!Items.UseActive(Run)) return;
            Result.actives++;
            double gain = Math.Max(0, p.hp - hp) + Math.Max(0, p.armor - armor);
            if (gain > 0) Heal("active", gain);
        }

        /// <summary>One Update, with the HP layers compared across it: losses are damage, gains are healing.</summary>
        void Step(Input input)
        {
            var p = Run.player;
            double hp0 = p.hp, ar0 = p.armor, rg0 = p.regenHeart;
            int floor0 = Run.floor, cx0 = Run.curX, cy0 = Run.curY;
            _pk0.Clear(); _pk0.AddRange(Run.pickups);
            _shots0.Clear();
            foreach (var s in Run.projectiles) if (!s.friendly) _shots0.Add(s);
            _bodies0.Clear();
            foreach (var e in Run.enemies) _bodies0.Add((e.kind, e.x, e.y));

            try { TickOrder.Update(Run, input); }
            finally { Observe(hp0, ar0, rg0, floor0, cx0, cy0); }   // a step that throws is still counted
        }

        void Observe(double hp0, double ar0, double rg0, int floor0, int cx0, int cy0)
        {
            var p = Run.player;
            double dHp = p.hp - hp0, dAr = p.armor - ar0, dRg = p.regenHeart - rg0;
            double lost = Math.Max(0, -dHp) + Math.Max(0, -dAr) + Math.Max(0, -dRg);
            if (lost > 0)
            {
                string src = Source();
                Result.dmgBySource[src] = Result.dmgBySource.TryGetValue(src, out var v) ? v + lost : lost;
                _f.dmg += lost;
            }
            if (dRg > 0) { Heal("regen", dRg); _f.regen += dRg; Result.regenHealed += dRg; }
            double gain = Math.Max(0, dHp) + Math.Max(0, dAr);
            bool sameRoom = Run.floor == floor0 && Run.curX == cx0 && Run.curY == cy0;
            string? by = null;
            foreach (var pk in _pk0)
            {
                if (sameRoom && Run.pickups.Contains(pk)) continue;
                if (!sameRoom) break;
                if (pk.kind == "heart" || pk.kind == "halfheart" || pk.kind == "armor" || pk.kind == "halfarmor") by ??= pk.kind;
                if (pk.kind == "item") Result.items.Add(pk.id + "@" + floor0);
            }
            // nothing left the floor: a drop that landed under the player and was taken on the same tick
            if (gain > 0 && by == null) by = dAr > 0 ? (dAr >= 2 ? "armor" : "halfarmor") : dHp > 0 ? (dHp >= 2 ? "heart" : "halfheart") : "other";
            if (gain > 0) Heal(by!, gain);
        }

        void Heal(string by, double gain)
        {
            Result.healed += gain; _f.healed += gain;
            Result.healBy[by] = Result.healBy.TryGetValue(by, out var v) ? v + gain : gain;
        }

        /// <summary>
        /// The JS bot's rule, seen from outside: a hostile shot within 40px of the player (or its lagged
        /// hitbox) that is gone after the step is the source; else the nearest body.
        /// </summary>
        string Source()
        {
            var p = Run.player;
            double best = 40;
            Projectile? near = null;
            foreach (var s in _shots0)
            {
                if (Run.projectiles.Contains(s)) continue;
                // shells land on the lagged hitbox, which can trail the body by 40px after a blink
                double d = Math.Min(Math.Sqrt((s.x - p.x) * (s.x - p.x) + (s.y - p.y) * (s.y - p.y)),
                                    Math.Sqrt((s.x - p.lagX) * (s.x - p.lagX) + (s.y - p.lagY - Balance.PlayerHitDy) * (s.y - p.lagY - Balance.PlayerHitDy)));
                if (d < best) { best = d; near = s; }
            }
            if (near != null) return "shot:" + (near.owner != null ? Name(near.owner.kind) : "?");
            double bd = double.PositiveInfinity;
            string kind = "?";
            if (Run.enemies.Count > 0)
            {
                foreach (var e in Run.enemies)
                {
                    double d = Math.Sqrt((e.x - p.x) * (e.x - p.x) + (e.y - p.y) * (e.y - p.y));
                    if (d < bd) { bd = d; kind = Name(e.kind); }
                }
            }
            else
            {
                foreach (var (k, x, y) in _bodies0)
                {
                    double d = Math.Sqrt((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y));
                    if (d < bd) { bd = d; kind = Name(k); }
                }
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

        // the innermost game or bot frame, without the machine's paths
        static string TopFrame(Exception e)
        {
            var frames = (e.StackTrace ?? "").Split('\n');
            string pick = frames[0];
            foreach (var f in frames) if (f.Contains("Depths.")) { pick = f; break; }
            pick = pick.Trim();
            int at = pick.IndexOf(" in ", StringComparison.Ordinal);
            return at < 0 ? pick : pick.Substring(0, at);
        }

        static string Trim(string s) => s.Length > 300 ? s.Substring(0, 300) : s;
    }
}
