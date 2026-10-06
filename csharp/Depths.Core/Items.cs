using System.Collections.Generic;

namespace Depths
{
    /// <summary>
    /// The stats a build is made of (src/06-stats.js): value = min(cap, base + flat + earned). The base
    /// is the character (Wyrd); flat comes only from items, by rebuilding, never by subtracting.
    /// </summary>
    public sealed class Stats
    {
        public static readonly string[] Order = { "strength", "speed", "momentum", "intelligence", "luck", "vigor", "precision" };
        static readonly Dictionary<string, double> Base = new Dictionary<string, double>
        {
            ["strength"] = 3, ["speed"] = 0.25, ["momentum"] = 0, ["intelligence"] = 1, ["luck"] = 0, ["vigor"] = 8, ["precision"] = 0,
        };
        static readonly Dictionary<string, double> Cap = new Dictionary<string, double>
        {
            ["strength"] = double.PositiveInfinity, ["speed"] = 0.40, ["momentum"] = 1, ["intelligence"] = double.PositiveInfinity,
            ["luck"] = double.PositiveInfinity, ["vigor"] = double.PositiveInfinity, ["precision"] = double.PositiveInfinity,
        };
        readonly Dictionary<string, double> _flat = new Dictionary<string, double>();

        public Stats() { Reset(); }
        public void Reset() { foreach (var k in Order) _flat[k] = 0; }
        public void Flat(string k, double v) { if (!_flat.ContainsKey(k)) throw new System.ArgumentException("no stat " + k); _flat[k] += v; }
        public double Value(string k) => System.Math.Min(Cap[k], Base[k] + _flat[k]);
    }

    /// <summary>One item, as data (src/25b-items-roster.js).</summary>
    public sealed class ItemDef
    {
        public string Id = "", Name = "", Rarity = "common";
        public bool Active, Unimplemented;
        public int? Charges;                       // null for a passive; int.MaxValue for "never runs out"
        public (string stat, double v)[] StatFx = new (string, double)[0];
        public (string hook, double arg)[] Hooks = new (string, double)[0];
    }

    public sealed class ItemSlot { public string Id = ""; public int? Charges; public int Slot = -1; }

    /// <summary>
    /// The item system (src/25-items.js): the roster in definition order, the rarity roll (luck
    /// weights the rare end), the pool (one run draw per pick), give/remove by rebuilding, the one
    /// active slot and Q. The hooks are the game's: heal, pull the loot, reveal the room; a companion
    /// that is written down but not built refuses, as it does there.
    /// </summary>
    public static class Items
    {
        public const int ActiveSlot = 0;
        const double LuckRarityGain = 0.55;
        static readonly (string r, double w)[] Rarity = { ("legendary", 3), ("rare", 13), ("uncommon", 44), ("common", 100) };

        static ItemDef P(string id, string name, string rarity, params (string, double)[] stats) =>
            new ItemDef { Id = id, Name = name, Rarity = rarity, StatFx = stats };
        static ItemDef A(string id, string name, string rarity, int charges, (string, double)[] stats, params (string, double)[] hooks) =>
            new ItemDef { Id = id, Name = name, Rarity = rarity, Active = true, Charges = charges, StatFx = stats, Hooks = hooks };

        public static readonly ItemDef[] All =
        {
            P("heavy_hands", "Heavy Hands", "common", ("strength", 1)),
            P("weighted_rod", "Weighted Rod", "common", ("strength", 2)),
            A("tin_cup", "Tin Cup", "common", 3, new (string, double)[0], ("heal_self", 2)),
            A("bone_whistle", "Bone Whistle", "uncommon", int.MaxValue, new (string, double)[0], ("pull_pickups", 1)),
            P("iron_ribs", "Iron Ribs", "common", ("vigor", 2)),
            P("swift_boots", "Swift Boots", "common", ("speed", 0.05)),
            P("lucky_coin", "Lucky Coin", "uncommon", ("luck", 1)),
            P("steady_hand", "Steady Hand", "common", ("precision", 1)),
            P("weighted_grip", "Weighted Grip", "rare", ("precision", 2)),
            A("hunters_mark", "Hunter's Mark", "rare", 2, new[] { ("luck", 1.0) }, ("reveal_room", 1)),
            P("glass_wands", "Glass Wands", "legendary", ("strength", 3), ("luck", -1)),
            P("brass_compass", "Brass Compass", "legendary", ("luck", 1)),
            new ItemDef { Id = "lantern_friend", Name = "Lantern Friend", Rarity = "rare", Active = true, Charges = 1, Unimplemented = true, Hooks = new[] { ("spawn_companion", 1.0) } },
        };

        public static ItemDef Def(string id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            throw new System.ArgumentException("no item called " + id);
        }

        public static ItemSlot? Equipped(RunState run, string id) => run.loadout.Find(s => s.Id == id);
        public static ItemSlot? ActiveItem(RunState run) => run.loadout.Find(s => s.Slot == ActiveSlot && s.Charges.HasValue);

        /// <summary>Rebuild, never subtract: the stats from the whole build, then the vitals.</summary>
        public static void Rebuild(RunState run)
        {
            run.stats.Reset();
            foreach (var s in run.loadout) foreach (var (stat, v) in Def(s.Id).StatFx) run.stats.Flat(stat, v);
            ApplyVitals(run);
        }

        /// <summary>applyVitals: Vigor IS maximum health; a lower maximum clips the current.</summary>
        public static void ApplyVitals(RunState run)
        {
            var p = run.player;
            double max = System.Math.Max(1, run.stats.Value("vigor"));
            if (max != p.maxHp) { p.maxHp = max; if (p.hp > max) p.hp = max; }
        }

        /// <summary>give: stack charges, stack a passive, or take the active slot and drop what was in it.</summary>
        public static (bool taken, string? dropped, int? droppedCharges) Give(RunState run, string id, int? charges = null)
        {
            var d = Def(id);
            var already = Equipped(run, id);
            int? start = charges ?? d.Charges;
            if (already != null && already.Charges.HasValue && already.Charges != int.MaxValue)
            {
                already.Charges += start ?? 1;
                Rebuild(run);
                return (true, null, null);
            }
            if (already != null && !d.Active && !already.Charges.HasValue)
            {
                run.loadout.Add(new ItemSlot { Id = id, Charges = null, Slot = -1 });
                Rebuild(run);
                return (true, null, null);
            }
            if (already != null) return (false, null, null);
            int slot = d.Active ? ActiveSlot : -1;
            string? dropped = null; int? droppedCharges = null;
            if (slot >= 0)
            {
                var prev = run.loadout.Find(s => s.Slot == slot);
                if (prev != null) { dropped = prev.Id; droppedCharges = prev.Charges; run.loadout.Remove(prev); }
            }
            run.loadout.Add(new ItemSlot { Id = id, Charges = start, Slot = slot });
            Rebuild(run);
            return (true, dropped, droppedCharges);
        }

        public static bool Remove(RunState run, string id)
        {
            var s = Equipped(run, id);
            if (s == null) return false;
            run.loadout.Remove(s);
            Rebuild(run);
            return true;
        }

        /// <summary>Q: run the active's hooks; a charge is spent only if one did something.</summary>
        public static bool UseActive(RunState run)
        {
            var a = ActiveItem(run);
            if (a == null || a.Charges == 0) return false;
            var d = Def(a.Id);
            int ran = 0;
            foreach (var (hook, arg) in d.Hooks) if (RunHook(run, hook, arg)) ran++;
            if (ran == 0) return false;
            if (a.Charges.HasValue && a.Charges != int.MaxValue) a.Charges--;
            if (a.Charges == 0) Remove(run, a.Id);
            return true;
        }

        static bool RunHook(RunState run, string hook, double arg)
        {
            var p = run.player;
            switch (hook)
            {
                case "heal_self":
                {
                    double before = p.hp;
                    p.hp = System.Math.Min(p.maxHp, p.hp + (arg != 0 ? arg : 2));
                    return p.hp > before;
                }
                case "reveal_room":
                {
                    int n = 0;
                    foreach (var e in run.enemies) if (!e.alerted) { e.alerted = true; n++; }
                    foreach (var pk in run.pickups) if (!pk.shown) { pk.shown = true; n++; }
                    return n > 0;
                }
                case "pull_pickups":
                {
                    int n = 0;
                    foreach (var pk in run.pickups)
                    {
                        if (pk.kind == "exit") continue;
                        double d = System.Math.Sqrt((pk.x - p.x) * (pk.x - p.x) + (pk.y - p.y) * (pk.y - p.y));
                        if (d == 0) d = 1;
                        if (d > 200 || d <= 90) continue;
                        double nx = p.x + (pk.x - p.x) / d * 90, ny = p.y + (pk.y - p.y) / d * 90;
                        pk.x = System.Math.Max(Balance.RoomLeft + pk.r, System.Math.Min(Balance.RoomRight - pk.r, nx));
                        pk.y = System.Math.Max(Balance.RoomTop + pk.r, System.Math.Min(Balance.RoomBottom - pk.r, ny));
                        n++;
                    }
                    return n > 0;
                }
                default: return false;   // spawn_companion: written down, not built, and it says so by refusing
            }
        }

        /// <summary>rollRarity: one run draw over the weights, luck multiplying all but common.</summary>
        public static string RollRarity(Rng rng, double luck)
        {
            double l = System.Math.Max(0, luck), total = 0;
            var w = new double[Rarity.Length];
            for (int i = 0; i < Rarity.Length; i++) { w[i] = Rarity[i].w * (Rarity[i].r == "common" ? 1 : 1 + LuckRarityGain * l); total += w[i]; }
            double r = rng.Run() * total;
            for (int i = 0; i < Rarity.Length; i++) { r -= w[i]; if (r < 0) return Rarity[i].r; }
            return "common";
        }

        /// <summary>pool: n distinct items of a rarity (any if null), excluding some, in definition order, one draw per pick.</summary>
        public static List<string> Pool(Rng rng, int n, string? rarity, ICollection<string>? exclude)
        {
            var ids = new List<string>();
            foreach (var d in All)
            {
                if (rarity != null && d.Rarity != rarity) continue;
                if (exclude != null && exclude.Contains(d.Id)) continue;
                if (d.Unimplemented) continue;
                ids.Add(d.Id);
            }
            var outp = new List<string>();
            for (int i = 0; i < n && ids.Count > 0; i++)
            {
                int j = (int)(rng.Run() * ids.Count);
                outp.Add(ids[j]);
                ids.RemoveAt(j);
            }
            return outp;
        }
    }
}
