using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Depths.Playtest
{
    /// <summary>One row of numbers per (label, profile); definitions are tools/playtest-report.js's.</summary>
    public sealed class ProfileStats
    {
        public int n, deaths, stuck, errors;
        public double floor, minPerFloor, dmgPerFloor, healPerFloor, regenPerFloor, clearPerFloor, bossS, acc, killsPerMin;
        public double qPerRun, activesPerRun, maskedPerRun;
        public Dictionary<string, double> dmgSrc = new Dictionary<string, double>();
        public Dictionary<string, double> healSrc = new Dictionary<string, double>();

        static double Mean(IEnumerable<double> a) { var l = a.ToList(); return l.Count > 0 ? l.Average() : 0; }

        static double Median(IEnumerable<double> a)
        {
            var b = a.OrderBy(x => x).ToList();
            if (b.Count == 0) return 0;
            int m = b.Count >> 1;
            return b.Count % 2 == 1 ? b[m] : (b[m - 1] + b[m]) / 2;
        }

        public static ProfileStats Of(IReadOnlyList<RunResult> runs, int hz)
        {
            var done = runs.SelectMany(r => r.floors.Where(f => f.bossKilled)).ToList();   // floors the bot left
            var all = runs.SelectMany(r => r.floors).ToList();
            var s = new ProfileStats();
            foreach (var r in runs)
            {
                foreach (var kv in r.dmgBySource) s.dmgSrc[kv.Key] = s.dmgSrc.GetValueOrDefault(kv.Key) + kv.Value;
                foreach (var kv in r.healBy) s.healSrc[kv.Key] = s.healSrc.GetValueOrDefault(kv.Key) + kv.Value;
            }
            double mins = runs.Sum(r => (double)r.ticks) / hz / 60;
            s.n = runs.Count;
            s.floor = Median(runs.Select(r => (double)r.floor));
            s.deaths = runs.Count(r => r.end == "death");
            s.stuck = runs.Count(r => r.end == "stuck");
            s.errors = runs.Count(r => r.errors.Count > 0);
            s.minPerFloor = Mean(done.Select(f => (double)f.ticks / hz / 60));
            s.dmgPerFloor = Mean(all.Select(f => f.dmg));
            s.healPerFloor = Mean(all.Select(f => f.healed));
            s.regenPerFloor = Mean(all.Select(f => f.regen));
            s.clearPerFloor = Mean(all.Select(f => f.clear));
            s.bossS = Mean(done.Select(f => (double)f.bossTicks / hz));
            s.acc = runs.Sum(r => (double)r.hits) / Math.Max(1, runs.Sum(r => r.shots));
            s.killsPerMin = runs.Sum(r => (double)r.kills) / Math.Max(1e-9, mins);
            s.qPerRun = Mean(runs.Select(r => (double)r.qPresses));
            s.activesPerRun = Mean(runs.Select(r => (double)r.actives));
            s.maskedPerRun = Mean(runs.Select(r => (double)r.maskedHits));
            return s;
        }
    }

    /// <summary>`report A` or `report A B`: Markdown tables, readable as plain text too.</summary>
    public static class Report
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // JS (Math.round(v*10)/10).toFixed(1): rounds halves up, not to even
        public static string F1(double v) => (Math.Floor(v * 10 + 0.5) / 10).ToString("0.0", Inv);
        static string Pct(double v) => Math.Floor(v * 100 + 0.5).ToString("0", Inv) + "%";

        static readonly (string key, string name, Func<ProfileStats, double> get, bool isInt)[] Metrics =
        {
            ("floor", "median floor reached", s => s.floor, false),
            ("deaths", "deaths", s => s.deaths, true),
            ("stuck", "stuck runs (bot or softlock)", s => s.stuck, true),
            ("errors", "runs with errors", s => s.errors, true),
            ("minPerFloor", "minutes per cleared floor", s => s.minPerFloor, false),
            ("dmgPerFloor", "damage taken per floor (hp, 2 = 1 heart)", s => s.dmgPerFloor, false),
            ("healPerFloor", "healing per floor (hp, regen included)", s => s.healPerFloor, false),
            ("regenPerFloor", "  of which the regenerating heart", s => s.regenPerFloor, false),
            ("clearPerFloor", "    of which its refill for winning a fight", s => s.clearPerFloor, false),
            ("bossS", "boss fight, seconds", s => s.bossS, false),
            ("acc", "accuracy", s => s.acc, false),
            ("killsPerMin", "kills per minute", s => s.killsPerMin, false),
            ("qPerRun", "Q presses when low, per run", s => s.qPerRun, false),
            ("activesPerRun", "  of which healed, per run", s => s.activesPerRun, false),
            ("maskedPerRun", "hits hidden by a same-tick heal, per run", s => s.maskedPerRun, false),
        };

        // a profile a label did not play has no numbers, not zeros
        static string Fmt(string key, ProfileStats s, double v, bool isInt) =>
            s.n == 0 ? "n/a" : key == "acc" ? Pct(v) : isInt ? ((int)v).ToString(Inv) : F1(v);

        // as the JS: every non-accuracy delta in f1, "=" under the noise floor
        static string Delta(string key, ProfileStats sa, ProfileStats sb, double a, double b)
        {
            if (sa.n == 0 || sb.n == 0) return "n/a";
            double d = b - a;
            if (Math.Abs(d) < (key == "acc" ? 0.005 : 0.05)) return "=";
            return (d > 0 ? "+" : "") + (key == "acc" ? Math.Floor(d * 100 + 0.5).ToString("0", Inv) + "%" : F1(d));
        }

        static int Hz(Batch b) => b.tickHz > 0 ? b.tickHz : 210;

        static ProfileStats St(Batch b, string? prof) =>
            ProfileStats.Of(b.runs.Where(r => prof == null || r.profile == prof).ToList(), Hz(b));

        /// <summary>Deaths paired by seed and profile: runs B saved, runs B lost, and the sign test's p.</summary>
        public static (int saved, int lost, double p) Flips(Batch a, Batch b, string? prof)
        {
            var before = a.runs.Where(r => prof == null || r.profile == prof).ToDictionary(r => (r.seed, r.profile));
            int saved = 0, lost = 0;
            foreach (var r in b.runs.Where(r => prof == null || r.profile == prof))
            {
                if (!before.TryGetValue((r.seed, r.profile), out var o)) continue;
                bool was = o.end == "death", now = r.end == "death";
                if (was && !now) saved++;
                if (!was && now) lost++;
            }
            return (saved, lost, SignTest(saved, lost));
        }

        /// <summary>Exact two-sided sign test on the runs that flipped (computed in logs, so n can be large).</summary>
        public static double SignTest(int x, int y)
        {
            int n = x + y, k = Math.Min(x, y);
            if (n == 0) return 1;
            double LnC(int m) => LnFact(n) - LnFact(m) - LnFact(n - m);
            double tail = 0;
            for (int i = 0; i <= k; i++) tail += Math.Exp(LnC(i) - n * Math.Log(2));
            return Math.Min(1, 2 * tail);
        }

        static double LnFact(int m) { double s = 0; for (int i = 2; i <= m; i++) s += Math.Log(i); return s; }

        static void Paired(StringBuilder sb, Batch a, Batch b, List<string?> groups)
        {
            sb.Append("\n## Deaths, paired by seed and profile\n\nOne changed HP point reroutes a whole run, so compare runs ")
              .Append("that flipped. p under 0.05: unlikely to be chance; the default 18 runs are rarely enough.\n\n")
              .Append("| | ").Append(b.label).Append(" saved | ").Append(b.label).Append(" newly died | p |\n|---|---:|---:|---:|\n");
            foreach (var g in groups)
            {
                var (saved, lost, p) = Flips(a, b, g);
                sb.Append("| ").Append(g ?? "all").Append(" | ").Append(saved).Append(" | ").Append(lost).Append(" | ")
                  .Append(p < 0.001 ? p.ToString("0.0e0", Inv) : p.ToString("0.000", Inv)).Append(" |\n");
            }
        }

        /// <summary>What makes two labels not like for like, one line each; empty when they match.</summary>
        public static List<string> Mismatches(Batch a, Batch b)
        {
            var w = new List<string>();
            void Cmp(string what, string x, string y) { if (x != y) w.Add(what + " differ: " + x + " vs " + y); }
            Cmp("seeds", string.Join(",", a.seeds), string.Join(",", b.seeds));
            Cmp("profiles", string.Join(",", a.profiles), string.Join(",", b.profiles));
            Cmp("minutes", a.minutes.ToString(Inv), b.minutes.ToString(Inv));
            Cmp("tick rates", Hz(a).ToString(Inv), Hz(b).ToString(Inv));
            Cmp("Brunch variants", a.brunch, b.brunch);
            static string On(bool? v) => v == null ? "unrecorded" : v.Value ? "on" : "off";
            Cmp("difficulty curves", On(a.curve), On(b.curve));
            return w;
        }

        public static string Build(IReadOnlyList<Batch> sets)
        {
            var sb = new StringBuilder();
            bool ab = sets.Count == 2;
            var profiles = sets.SelectMany(s => s.profiles).Distinct().ToList();

            sb.Append("# Depths playtest (C#)").Append(ab ? ": " + sets[0].label + " vs " + sets[1].label : "")
              .Append("\n\n");
            foreach (var s in sets)
                sb.Append("- ").Append(s.label).Append(": ").Append(s.runs.Count).Append(" runs, seeds ")
                  .Append(string.Join(", ", s.seeds)).Append(", up to ").Append(s.minutes.ToString(Inv))
                  .Append(" sim-min each at ").Append(Hz(s)).Append(" Hz, Brunch ").Append(s.brunch)
                  .Append(", built from ").Append(s.commit ?? "an unknown commit").Append('\n');
            if (ab)
                foreach (var m in Mismatches(sets[0], sets[1]))
                    sb.Append("\n**Warning: ").Append(m)
                      .Append(".** The deltas below mix that difference with the change.\n");
            sb.Append("\nA bot plays every run through the game's own input. Profiles differ in reaction time, ")
              .Append("aim error, dodging and spacing. It does not hunt secrets.\n");

            var groups = new List<string?> { null };
            groups.AddRange(profiles);
            if (!ab)
            {
                var cols = groups.Select(g => St(sets[0], g)).ToList();
                sb.Append("\n| | all | ").Append(string.Join(" | ", profiles)).Append(" |\n|---|")
                  .Append(string.Concat(Enumerable.Repeat("---:|", groups.Count))).Append('\n');
                foreach (var (key, name, get, isInt) in Metrics)
                    sb.Append("| ").Append(name).Append(" | ")
                      .Append(string.Join(" | ", cols.Select(c => Fmt(key, c, get(c), isInt)))).Append(" |\n");
            }
            else
            {
                foreach (var g in groups)
                {
                    var a = St(sets[0], g); var b = St(sets[1], g);
                    sb.Append("\n## ").Append(g ?? "All profiles").Append("\n\n| | ").Append(sets[0].label)
                      .Append(" | ").Append(sets[1].label).Append(" | change |\n|---|---:|---:|---:|\n");
                    foreach (var (key, name, get, isInt) in Metrics)
                        sb.Append("| ").Append(name).Append(" | ").Append(Fmt(key, a, get(a), isInt)).Append(" | ")
                          .Append(Fmt(key, b, get(b), isInt)).Append(" | ").Append(Delta(key, a, b, get(a), get(b)))
                          .Append(" |\n");
                }
            }

            if (ab) Paired(sb, sets[0], sets[1], groups);

            sb.Append("\n## Where the damage comes from\n");
            foreach (var s in sets) Sources(sb, s, profiles, true);
            sb.Append("\n## Where the healing comes from\n");
            foreach (var s in sets) Sources(sb, s, profiles, false);
            sb.Append("\n`regen` is the regenerating heart's clock; `clear` its refill for winning a fight; `active` a Q item; ")
              .Append("`unseen:<layer>` a heal with no ")
              .Append("pickup leaving the floor (a drop that spawned and was taken in the same tick).\n");

            foreach (var s in sets)
            {
                int hz = Hz(s);
                sb.Append("\n## Runs: ").Append(s.label).Append("\n\n")
                  .Append("| profile | seed | floor | end | cause | min | dmg | healed (regen) | Q (healed) ")
                  .Append("| hidden hits | items |\n")
                  .Append("|---|---:|---:|---|---|---:|---:|---:|---:|---:|---|\n");
                foreach (var r in s.runs)
                    sb.Append("| ").Append(r.profile).Append(" | ").Append(r.seed).Append(" | ").Append(r.floor)
                      .Append(" | ").Append(r.end).Append(" | ").Append(CauseText(r))
                      .Append(" | ").Append(F1(r.ticks / (double)hz / 60)).Append(" | ").Append(F1(r.floors.Sum(f => f.dmg)))
                      .Append(" | ").Append(F1(r.healed)).Append(" (").Append(F1(r.regenHealed)).Append(") | ")
                      .Append(r.qPresses).Append(" (").Append(r.actives).Append(") | ").Append(r.maskedHits)
                      .Append(" | ").Append(string.Join(" ", r.items)).Append(" |\n");
                foreach (var r in s.runs.Where(r => r.errors.Count > 0))
                    sb.Append("\n- ERROR ").Append(r.profile).Append(" seed ").Append(r.seed).Append(": ")
                      .Append(r.errors[0]).Append('\n');
            }
            foreach (var s in sets) Floors(sb, s);
            return sb.ToString();
        }

        /// <summary>Every floor of every run, as the JS report's per-run detail had it.</summary>
        static void Floors(StringBuilder sb, Batch s)
        {
            int hz = Hz(s);
            sb.Append("\n## Floors: ").Append(s.label).Append("\n\n")
              .Append("| profile | seed | floor | min | rooms | dmg | healed (regen) | boss s | left ")
              .Append("| hp in > out | armour in > out |\n")
              .Append("|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|\n");
            foreach (var r in s.runs)
                foreach (var f in r.floors)
                    sb.Append("| ").Append(r.profile).Append(" | ").Append(r.seed).Append(" | ").Append(f.floor)
                      .Append(" | ").Append(F1(f.ticks / (double)hz / 60)).Append(" | ").Append(f.rooms)
                      .Append(" | ").Append(F1(f.dmg)).Append(" | ").Append(F1(f.healed)).Append(" (").Append(F1(f.regen))
                      .Append(") | ").Append(F1(f.bossTicks / (double)hz)).Append(" | ").Append(f.bossKilled ? "yes" : "no")
                      .Append(" | ").Append(F1(f.hpIn)).Append(" > ").Append(F1(f.hpOut ?? f.hpIn))
                      .Append(" | ").Append(F1(f.armorIn)).Append(" > ").Append(F1(f.armorOut ?? f.armorIn)).Append(" |\n");
        }

        /// <summary>The cause column: the top damage source for a death, what the room held for a stuck run.</summary>
        static string CauseText(RunResult r)
        {
            if (r.end == "error") return "see below";
            if (r.cause == null) return "";
            // [source, hp] or the stuck object, before or after a JSON round trip
            using var doc = JsonDocument.Parse(Json.Text(r.cause));
            var c = doc.RootElement;
            if (c.ValueKind == JsonValueKind.Array) return c[0].GetString() + " " + F1(c[1].GetDouble()) + " hp";
            if (c.ValueKind != JsonValueKind.Object) return c.ToString();
            string Get(string k) => c.TryGetProperty(k, out var v) ? v.ToString() : "?";
            var pk = c.TryGetProperty("pickups", out var arr)
                ? string.Join(" ", arr.EnumerateArray().Select(x => x.GetString())) : "";
            return Get("room") + " room " + Get("cell") + ", " + Get("enemies") + " enemies, pickups: " +
                   (pk.Length > 0 ? pk : "none") +
                   (Get("silver") == "True" ? ", silver key" : "") + (Get("gold") == "True" ? ", gold key" : "") +
                   ", boss door " + (Get("bossUnlocked") == "True" ? "open" : "locked") +
                   ", item door " + (Get("itemUnlocked") == "True" ? "open" : "locked");
        }

        static string Top(Dictionary<string, double> src, int take)
        {
            double tot = Math.Max(1e-9, src.Values.Sum());
            return string.Join(", ", src.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(take).Select(kv => kv.Key + " " + Pct(kv.Value / tot)));
        }

        static void Sources(StringBuilder sb, Batch s, List<string> profiles, bool dmg)
        {
            var all = St(s, null);
            var src = dmg ? all.dmgSrc : all.healSrc;
            sb.Append("\n**").Append(s.label).Append("** (total ").Append(F1(src.Values.Sum())).Append(" hp): ")
              .Append(src.Count > 0 ? Top(src, int.MaxValue) : "none").Append('\n');
            foreach (var p in profiles)
            {
                var ps = St(s, p);
                var d = dmg ? ps.dmgSrc : ps.healSrc;
                if (d.Count > 0) sb.Append("- ").Append(p).Append(": ").Append(Top(d, 3)).Append('\n');
            }
        }
    }
}
