using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Depths.Playtest
{
    /// <summary>One row of numbers per (label, profile); definitions are tools/playtest-report.js's.</summary>
    public sealed class Stats
    {
        public int n, deaths, stuck, errors;
        public double floor, minPerFloor, dmgPerFloor, healPerFloor, regenPerFloor, bossS, acc, killsPerMin;
        public Dictionary<string, double> dmgSrc = new Dictionary<string, double>(), healSrc = new Dictionary<string, double>();

        static double Mean(IEnumerable<double> a) { var l = a.ToList(); return l.Count > 0 ? l.Average() : 0; }

        static double Median(IEnumerable<double> a)
        {
            var b = a.OrderBy(x => x).ToList();
            if (b.Count == 0) return 0;
            int m = b.Count >> 1;
            return b.Count % 2 == 1 ? b[m] : (b[m - 1] + b[m]) / 2;
        }

        public static Stats Of(IReadOnlyList<RunResult> runs, int hz)
        {
            var done = runs.SelectMany(r => r.floors.Where(f => f.bossKilled)).ToList();
            var all = runs.SelectMany(r => r.floors).ToList();
            var s = new Stats();
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
            s.bossS = Mean(done.Select(f => (double)f.bossTicks / hz));
            s.acc = runs.Sum(r => (double)r.hits) / Math.Max(1, runs.Sum(r => r.shots));
            s.killsPerMin = runs.Sum(r => (double)r.kills) / Math.Max(1e-9, mins);
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

        static readonly (string key, string name, Func<Stats, double> get, bool isInt)[] Metrics =
        {
            ("floor", "median floor reached", s => s.floor, false),
            ("deaths", "deaths", s => s.deaths, true),
            ("stuck", "stuck runs (bot or softlock)", s => s.stuck, true),
            ("errors", "runs with errors", s => s.errors, true),
            ("minPerFloor", "minutes per cleared floor", s => s.minPerFloor, false),
            ("dmgPerFloor", "damage taken per floor (hp, 2 = 1 heart)", s => s.dmgPerFloor, false),
            ("healPerFloor", "healing per floor (hp, regen included)", s => s.healPerFloor, false),
            ("regenPerFloor", "  of which the regenerating heart", s => s.regenPerFloor, false),
            ("bossS", "boss fight, seconds", s => s.bossS, false),
            ("acc", "accuracy", s => s.acc, false),
            ("killsPerMin", "kills per minute", s => s.killsPerMin, false),
        };

        static string Fmt(string key, double v, bool isInt) =>
            key == "acc" ? Pct(v) : isInt ? ((int)v).ToString(Inv) : F1(v);

        static string Delta(string key, double a, double b, bool isInt)
        {
            double d = b - a;
            if (Math.Abs(d) < (key == "acc" ? 0.005 : 0.05)) return "=";
            return (d > 0 ? "+" : "") + (key == "acc" ? Math.Floor(d * 100 + 0.5).ToString("0", Inv) + "%" : isInt ? ((int)d).ToString(Inv) : F1(d));
        }

        public static string Build(IReadOnlyList<Batch> sets)
        {
            var sb = new StringBuilder();
            bool ab = sets.Count == 2;
            var profiles = sets.SelectMany(s => s.profiles).Distinct().ToList();
            Stats St(Batch b, string? prof) =>
                Stats.Of(b.runs.Where(r => prof == null || r.profile == prof).ToList(), b.tickHz > 0 ? b.tickHz : 210);

            sb.Append("# Depths playtest (C#)").Append(ab ? ": " + sets[0].label + " vs " + sets[1].label : "").Append("\n\n");
            foreach (var s in sets)
                sb.Append("- ").Append(s.label).Append(": ").Append(s.runs.Count).Append(" runs, seeds ")
                  .Append(string.Join(", ", s.seeds)).Append(", up to ").Append(s.minutes.ToString(Inv)).Append(" sim-min each, Brunch ")
                  .Append(s.brunch).Append(", commit ").Append(s.commit ?? "?").Append('\n');
            sb.Append("\nA bot plays every run through the game's own input. Profiles differ in reaction time, aim error, dodging and spacing. It does not hunt secrets.\n");

            var groups = new List<string?> { null };
            groups.AddRange(profiles);
            if (!ab)
            {
                var cols = groups.Select(g => St(sets[0], g)).ToList();
                sb.Append("\n| | all | ").Append(string.Join(" | ", profiles)).Append(" |\n|---|").Append(string.Concat(Enumerable.Repeat("---:|", groups.Count))).Append('\n');
                foreach (var (key, name, get, isInt) in Metrics)
                    sb.Append("| ").Append(name).Append(" | ").Append(string.Join(" | ", cols.Select(c => Fmt(key, get(c), isInt)))).Append(" |\n");
            }
            else
            {
                foreach (var g in groups)
                {
                    var a = St(sets[0], g); var b = St(sets[1], g);
                    sb.Append("\n## ").Append(g ?? "All profiles").Append("\n\n| | ").Append(sets[0].label).Append(" | ").Append(sets[1].label).Append(" | change |\n|---|---:|---:|---:|\n");
                    foreach (var (key, name, get, isInt) in Metrics)
                        sb.Append("| ").Append(name).Append(" | ").Append(Fmt(key, get(a), isInt)).Append(" | ").Append(Fmt(key, get(b), isInt))
                          .Append(" | ").Append(Delta(key, get(a), get(b), isInt)).Append(" |\n");
                }
            }

            sb.Append("\n## Where the damage comes from\n");
            foreach (var s in sets) Sources(sb, s, profiles, St, true);
            sb.Append("\n## Where the healing comes from\n");
            foreach (var s in sets) Sources(sb, s, profiles, St, false);

            foreach (var s in sets)
            {
                int hz = s.tickHz > 0 ? s.tickHz : 210;
                sb.Append("\n## Runs: ").Append(s.label).Append("\n\n");
                foreach (var r in s.runs)
                {
                    sb.Append("- ").Append(r.profile).Append(" seed ").Append(r.seed).Append(": floor ").Append(r.floor).Append(", ").Append(r.end);
                    if (r.end == "death" && r.cause != null) sb.Append(" (").Append(CauseText(r.cause)).Append(')');
                    sb.Append(", ").Append(F1(r.ticks / (double)hz / 60)).Append(" min, dmg ").Append(F1(r.floors.Sum(f => f.dmg)))
                      .Append(", healed ").Append(F1(r.healed)).Append(" (regen ").Append(F1(r.regenHealed)).Append(')');
                    if (r.items.Count > 0) sb.Append(", items ").Append(string.Join(" ", r.items));
                    sb.Append('\n');
                    if (r.end == "stuck") sb.Append("  - STUCK: ").Append(Json.Text(r.cause)).Append('\n');
                    foreach (var e in r.errors.Take(1)) sb.Append("  - ERROR: ").Append(e).Append('\n');
                }
            }
            return sb.ToString();
        }

        static string CauseText(object cause)
        {
            // [source, hp] after a JSON round trip is a JsonElement array
            var t = Json.Text(cause);
            return t.Trim('[', ']').Replace("\"", "");
        }

        static void Sources(StringBuilder sb, Batch s, List<string> profiles, Func<Batch, string?, Stats> st, bool dmg)
        {
            var all = st(s, null);
            var src = dmg ? all.dmgSrc : all.healSrc;
            double tot = Math.Max(1e-9, src.Values.Sum());
            sb.Append("\n**").Append(s.label).Append("** (total ").Append(F1(src.Values.Sum())).Append(" hp): ");
            sb.Append(string.Join(", ", src.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + " " + Pct(kv.Value / tot)))).Append('\n');
            foreach (var p in profiles)
            {
                var ps = st(s, p);
                var d = dmg ? ps.dmgSrc : ps.healSrc;
                double pt = Math.Max(1e-9, d.Values.Sum());
                sb.Append("- ").Append(p).Append(": ").Append(string.Join(", ", d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Take(3).Select(kv => kv.Key + " " + Pct(kv.Value / pt)))).Append('\n');
            }
        }
    }
}
