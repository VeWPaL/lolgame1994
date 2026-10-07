using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Depths.Playtest
{
    /// <summary>One band (or floor) of one profile: floor entries, deaths, and what was taken and healed there.</summary>
    public sealed class BandStat
    {
        public int entries, deaths;
        public double dmg, healed;
        public readonly SortedDictionary<string, double> src = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, double> healBy = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public bool srcKnown = true;   // false when a floor in it came from a file without per-floor sources
        public List<int> floors = new List<int>();

        public double Hazard => entries > 0 ? (double)deaths / entries : 0;
        public double DmgPerFloor => entries > 0 ? dmg / entries : 0;
        public double HealPerFloor => entries > 0 ? healed / entries : 0;

        /// <summary>The largest single source's share of the band's damage (0 with no damage).</summary>
        public (string? key, double share) TopSource()
        {
            double tot = src.Values.Sum();
            if (tot <= 0) return (null, 0);
            var top = src.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First();
            return (top.Key, top.Value / tot);
        }

        public void Add(BandStat f)
        {
            entries += f.entries; deaths += f.deaths; dmg += f.dmg; healed += f.healed;
            srcKnown &= f.srcKnown;
            foreach (var kv in f.src) src[kv.Key] = src.GetValueOrDefault(kv.Key) + kv.Value;
            foreach (var kv in f.healBy) healBy[kv.Key] = healBy.GetValueOrDefault(kv.Key) + kv.Value;
            floors.AddRange(f.floors);
        }
    }

    /// <summary>A profile's curve: per-floor hazard, the three bands, and the other numbers the targets read.</summary>
    public sealed class ProfileCurve
    {
        public string profile = "";
        public int runs;
        public SortedDictionary<int, BandStat> perFloor = new SortedDictionary<int, BandStat>();
        public BandStat start = new BandStat(), middle = new BandStat(), end = new BandStat();
        public double medianFloor;
        public double? hpLeavingStart;   // median HP out of the last START floor, over the runs that left it
        public double heartShare;        // heart and half-heart pickups' share of all healing

        /// <summary>END's first and second halves by floor (an odd middle floor sits in neither).</summary>
        public (BandStat first, BandStat second) EndHalves()
        {
            var fl = end.floors.OrderBy(x => x).ToList();
            var a = new BandStat(); var b = new BandStat();
            int h = fl.Count / 2;
            for (int i = 0; i < h; i++) a.Add(perFloor[fl[i]]);
            for (int i = fl.Count - h; i < fl.Count; i++) b.Add(perFloor[fl[i]]);
            return (a, b);
        }
    }

    /// <summary>
    /// The `curve` command: band hazards, HP and orderings from a playtest file, checked against
    /// <see cref="Curve.Targets"/> (the same file the game's shape lives in). Any miss: exit 2.
    /// </summary>
    public static class CurveCheck
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static ProfileCurve Measure(Batch b, string profile)
        {
            var runs = b.runs.Where(r => r.profile == profile).ToList();
            var c = new ProfileCurve { profile = profile, runs = runs.Count };
            foreach (var r in runs)
            {
                foreach (var f in r.floors)
                {
                    if (!c.perFloor.TryGetValue(f.floor, out var s)) c.perFloor[f.floor] = s = new BandStat { floors = { f.floor } };
                    s.entries++; s.dmg += f.dmg; s.healed += f.healed;
                    if (f.dmgBySource == null) s.srcKnown = false;
                    else foreach (var kv in f.dmgBySource) s.src[kv.Key] = s.src.GetValueOrDefault(kv.Key) + kv.Value;
                    if (f.healBy != null) foreach (var kv in f.healBy) s.healBy[kv.Key] = s.healBy.GetValueOrDefault(kv.Key) + kv.Value;
                }
                if (r.end == "death" && c.perFloor.TryGetValue(r.floor, out var d)) d.deaths++;
            }
            foreach (var kv in c.perFloor)
            {
                if (kv.Value.entries < Curve.MinEntries) continue;
                int fl = kv.Key;
                if (fl <= Curve.StartLast) c.start.Add(kv.Value);
                else if (fl >= Curve.MiddleFirst && fl <= Curve.MiddleLast) c.middle.Add(kv.Value);
                else if (fl >= Curve.EndFirst) c.end.Add(kv.Value);
            }
            c.medianFloor = Median(runs.Select(r => (double)r.floor));
            var left = runs.SelectMany(r => r.floors.Where(f => f.floor == Curve.StartLast && f.bossKilled && f.hpOut != null))
                           .Select(f => f.hpOut!.Value).ToList();
            c.hpLeavingStart = left.Count > 0 ? Median(left) : (double?)null;
            double heal = runs.Sum(r => r.healBy.Values.Sum());
            double hearts = runs.Sum(r => r.healBy.GetValueOrDefault("heart") + r.healBy.GetValueOrDefault("halfheart"));
            c.heartShare = heal > 0 ? hearts / heal : 0;
            return c;
        }

        static double Median(IEnumerable<double> a)
        {
            var s = a.OrderBy(x => x).ToList();
            if (s.Count == 0) return 0;
            int m = s.Count >> 1;
            return s.Count % 2 == 1 ? s[m] : (s[m - 1] + s[m]) / 2;
        }

        static string P(double v) => (v * 100).ToString("0.00", Inv) + "%";
        static string N(double v) => v.ToString("0.##", Inv);

        static string Range(Curve.Band t) =>
            t.Max >= 1 ? ">= " + P(t.Min) : t.Min <= 0 ? "<= " + P(t.Max) : P(t.Min) + "-" + P(t.Max);

        static string Shares(SortedDictionary<string, double> d, int entries)
        {
            if (d.Count == 0 || entries == 0) return "none recorded";
            return string.Join(", ", d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + " " + (kv.Value / entries).ToString("0.00", Inv)));
        }

        /// <summary>
        /// Measures every target profile and checks every target: the text is Markdown with one "ok" or
        /// "MISS" line per target, led by its rubric number; misses are the failed lines.
        /// </summary>
        public static (string text, List<string> misses) Check(Batch b)
        {
            var lines = new List<(bool ok, string what)>();
            void T(bool ok, string what) => lines.Add((ok, what));
            var sb = new StringBuilder();
            sb.Append("# Difficulty curve: ").Append(b.label).Append("\n\n")
              .Append(b.runs.Count).Append(" runs, ").Append(b.seeds.Count).Append(" seeds, ").Append(N(b.minutes))
              .Append(" sim-min at ").Append(b.tickHz > 0 ? b.tickHz : 210).Append(" Hz, curve ")
              .Append(b.curve == null ? "unrecorded" : b.curve.Value ? "on" : "off")
              .Append(b.dials != null ? " dialled " + string.Join(",", b.dials) : "")
              .Append(", built from ").Append(b.commit ?? "an unknown commit").Append(".\n");
            if (b.tickHz != Balance.GameHz || b.minutes != 60 || b.seeds.Count < 200)
                sb.Append("\nNote: the targets are for the game rate, 60 minutes and seeds 101-300; this file is not that.\n");
            sb.Append("\nHazard = deaths / floor entries; START floors 1-").Append(Curve.StartLast)
              .Append(", MIDDLE ").Append(Curve.MiddleFirst).Append('-').Append(Curve.MiddleLast)
              .Append(", END ").Append(Curve.EndFirst).Append("+, each floor counted only with ").Append(Curve.MinEntries)
              .Append("+ runs of the profile.\n\n")
              .Append("| profile | START | MIDDLE | END | END halves | HP leaving ").Append(Curve.StartLast)
              .Append(" | median floor | dmg / heal START | dmg / heal MIDDLE | top START source | hearts of healing |\n")
              .Append("|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|\n");

            var curves = new List<ProfileCurve>();
            foreach (var t in Curve.Targets)
            {
                if (!b.profiles.Contains(t.Profile)) { T(false, "1-5 " + t.Profile + ": not played"); continue; }
                var c = Measure(b, t.Profile);
                curves.Add(c);
                var (h1, h2) = c.EndHalves();
                var (topKey, topShare) = c.start.TopSource();
                sb.Append("| ").Append(t.Profile).Append(" | ").Append(P(c.start.Hazard)).Append(" | ").Append(P(c.middle.Hazard))
                  .Append(" | ").Append(P(c.end.Hazard)).Append(" | ").Append(P(h1.Hazard)).Append(" > ").Append(P(h2.Hazard))
                  .Append(" | ").Append(c.hpLeavingStart.HasValue ? N(c.hpLeavingStart.Value) : "n/a")
                  .Append(" | ").Append(N(c.medianFloor))
                  .Append(" | ").Append(N(Math.Round(c.start.DmgPerFloor, 2))).Append(" / ").Append(N(Math.Round(c.start.HealPerFloor, 2)))
                  .Append(" | ").Append(N(Math.Round(c.middle.DmgPerFloor, 2))).Append(" / ").Append(N(Math.Round(c.middle.HealPerFloor, 2)))
                  .Append(" | ").Append(!c.start.srcKnown ? "n/a" : topKey == null ? "none" : topKey + " " + P(topShare))
                  .Append(" | ").Append(P(c.heartShare)).Append(" |\n");

                void InBand(string rubric, string band, BandStat s, Curve.Band want)
                {
                    if (s.entries == 0) { T(false, rubric + " " + t.Profile + " " + band + ": no floor reached by " + Curve.MinEntries + "+ runs"); return; }
                    T(s.Hazard >= want.Min && s.Hazard <= want.Max, rubric + " " + t.Profile + " " + band + " hazard " + P(s.Hazard) + ", wants " + Range(want));
                }
                InBand("1", "START", c.start, t.Start);
                InBand("2", "MIDDLE", c.middle, t.Middle);
                InBand("3", "END", c.end, t.End);
                T(c.middle.entries > 0 && c.middle.Hazard < c.start.Hazard && c.middle.Hazard < c.end.Hazard,
                  "2 " + t.Profile + " MIDDLE " + P(c.middle.Hazard) + " below both START " + P(c.start.Hazard) + " and END " + P(c.end.Hazard));
                if (h1.entries == 0 || h2.entries == 0) T(false, "3 " + t.Profile + " END has fewer than two floors to compare");
                else
                {
                    double se = Math.Sqrt(h1.Hazard * (1 - h1.Hazard) / h1.entries + h2.Hazard * (1 - h2.Hazard) / h2.entries);
                    T(h2.Hazard >= h1.Hazard - Curve.EndFallSe * se, "3 " + t.Profile + " END halves " + P(h1.Hazard) + " then " + P(h2.Hazard) +
                      ", not falling by more than " + N(Curve.EndFallSe) + " standard errors (" + P(Curve.EndFallSe * se) + ")");
                }
                T(c.medianFloor >= t.MedianFloorMin && c.medianFloor <= t.MedianFloorMax, "4 " + t.Profile + " median floor " + N(c.medianFloor) +
                  ", wants " + N(t.MedianFloorMin) + (t.MedianFloorMax < 1e9 ? "-" + N(t.MedianFloorMax) : "+"));
                T(c.start.HealPerFloor < c.start.DmgPerFloor, "5 " + t.Profile + " START heals " + N(Math.Round(c.start.HealPerFloor, 2)) +
                  " a floor, below its " + N(Math.Round(c.start.DmgPerFloor, 2)) + " damage");
                T(c.middle.HealPerFloor < c.middle.DmgPerFloor, "5 " + t.Profile + " MIDDLE heals " + N(Math.Round(c.middle.HealPerFloor, 2)) +
                  " a floor, below its " + N(Math.Round(c.middle.DmgPerFloor, 2)) + " damage");
                if (!c.start.srcKnown) T(false, "1 " + t.Profile + " START damage sources not recorded (a file from before per-floor sources)");
                else T(topShare <= Curve.StartTopSourceMax, "1 " + t.Profile + " top START source " + (topKey ?? "none") + " " + P(topShare) +
                       ", at most " + P(Curve.StartTopSourceMax));
                T(c.heartShare >= Curve.HeartShareMin, "5 " + t.Profile + " heart pickups " + P(c.heartShare) + " of healing, at least " + P(Curve.HeartShareMin));
            }

            // the least skilled profile is the one the start has to cost hearts
            var first = curves.FirstOrDefault(c => c.profile == Curve.Targets[0].Profile);
            if (first != null)
                T(first.hpLeavingStart <= Curve.NoviceHpLeavingStartMax, "1 " + first.profile + " median HP leaving floor " + Curve.StartLast + " " +
                  (first.hpLeavingStart.HasValue ? N(first.hpLeavingStart.Value) : "n/a") + ", at most " + N(Curve.NoviceHpLeavingStartMax));

            // skill matters: in every band each profile dies less than the one before it in Targets
            if (curves.Count == Curve.Targets.Length)
                foreach (var (band, get) in new (string, Func<ProfileCurve, BandStat>)[] { ("START", c => c.start), ("MIDDLE", c => c.middle), ("END", c => c.end) })
                {
                    bool ok = true;
                    for (int i = 1; i < curves.Count; i++) ok &= get(curves[i]).Hazard < get(curves[i - 1]).Hazard;
                    T(ok, "4 " + band + " orders " + string.Join(" > ", curves.Select(c => c.profile + " " + P(get(c).Hazard))));
                }

            sb.Append("\n| floor |");
            foreach (var c in curves) sb.Append(' ').Append(c.profile).Append(" reach / die / hazard |");
            sb.Append("\n|---:|").Append(string.Concat(curves.Select(_ => "---:|"))).Append('\n');
            foreach (int fl in curves.SelectMany(c => c.perFloor.Keys).Distinct().OrderBy(x => x))
            {
                sb.Append("| ").Append(fl).Append(" |");
                foreach (var c in curves)
                    sb.Append(' ').Append(c.perFloor.TryGetValue(fl, out var s) ? s.entries + " / " + s.deaths + " / " + P(s.Hazard) : "").Append(" |");
                sb.Append('\n');
            }

            sb.Append("\n## Healing by source, HP a floor\n\n");
            foreach (var c in curves)
                sb.Append("- ").Append(c.profile).Append(" START: ").Append(Shares(c.start.healBy, c.start.entries))
                  .Append("; MIDDLE: ").Append(Shares(c.middle.healBy, c.middle.entries)).Append('\n');

            sb.Append("\n## Targets (rubric number first)\n\n");
            foreach (var (ok, what) in lines) sb.Append(ok ? "ok   " : "MISS ").Append(what).Append('\n');
            var misses = lines.Where(l => !l.ok).Select(l => l.what).ToList();
            sb.Append('\n').Append(misses.Count == 0 ? "PASS: every target met" : "FAIL: " + misses.Count + " of " + lines.Count + " targets missed").Append('\n');
            return (sb.ToString(), misses);
        }
    }
}
