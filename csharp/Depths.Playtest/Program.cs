using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Depths.Playtest
{
    public static class Json
    {
        // relaxed escaping keeps "A+" readable; the output is a file, never embedded in HTML
        static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions
        { IncludeFields = true, WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        static readonly JsonSerializerOptions Compact = new JsonSerializerOptions
        { IncludeFields = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static string Write(Batch b) => JsonSerializer.Serialize(b, Pretty).Replace("\r\n", "\n") + "\n";
        public static string Write(RunResult r) => JsonSerializer.Serialize(r, Compact);
        public static string Text(object? o) => JsonSerializer.Serialize(o, Compact);

        public static Batch Read(string text, string name)
        {
            Batch? b;
            try { b = JsonSerializer.Deserialize<Batch>(text, Compact); }
            catch (JsonException e) { throw new UsageException(name + " is not a playtest file: " + e.Message); }
            if (b == null || b.runs == null || b.profiles == null || b.seeds == null || b.label == null)
                throw new UsageException(name + " is not a playtest file (no runs)");
            // well-formed JSON with holes is still not a playtest file
            foreach (var r in b.runs)
                if (r == null || r.profile == null || r.end == null || r.floors == null || r.floors.Contains(null!)
                    || r.dmgBySource == null || r.healBy == null || r.items == null || r.errors == null)
                    throw new UsageException(name + " is not a playtest file (a run is missing fields)");
                else if (!CauseOk(r.cause))
                    throw new UsageException(name + " is not a playtest file (a cause is not [source, hp] or an object)");
            return b;
        }

        // a run's cause is null, [source, hp] (a death) or an object (what a stuck room held)
        static bool CauseOk(object? cause)
        {
            if (cause == null) return true;
            if (!(cause is JsonElement c)) return false;
            return c.ValueKind == JsonValueKind.Null || c.ValueKind == JsonValueKind.Object
                || (c.ValueKind == JsonValueKind.Array && c.GetArrayLength() == 2
                    && c[0].ValueKind == JsonValueKind.String && c[1].ValueKind == JsonValueKind.Number);
        }
    }

    /// <summary>Plays the matrix: every profile against every seed, one run after another (the core
    /// has static scratch).</summary>
    public static class Matrix
    {
        public static Batch Play(string label, IReadOnlyList<uint> seeds, IReadOnlyList<string> profiles,
                                 double minutes, Action<RunResult>? each = null)
        {
            var b = new Batch { label = label, commit = BuildInfo.Commit, brunch = Balance.BrunchVariant,
                                tickHz = Balance.TickHz, minutes = minutes, seeds = seeds.ToList(),
                                profiles = profiles.ToList() };
            foreach (var p in profiles)
                foreach (var s in seeds)
                {
                    var r = new Runner(s, p, minutes).Play();
                    b.runs.Add(r);
                    each?.Invoke(r);
                }
            return b;
        }
    }

    /// <summary>The commit this bot was BUILT from, stamped by the csproj, so a baseline built in another
    /// worktree records its own commit wherever its JSON is written.</summary>
    public static class BuildInfo
    {
        public static string? Commit => typeof(BuildInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GitCommit")?.Value;
    }

    /// <summary>A bad command line: printed as one line, exit code 1.</summary>
    public sealed class UsageException : Exception
    {
        public UsageException(string message) : base(message) { }
    }

    /// <summary>The parsed command line. Parse throws UsageException for anything it does not accept.</summary>
    public sealed class Cli
    {
        public string Command = "";
        public string Label = "current";
        public List<uint> Seeds = new List<uint> { 1, 7, 42, 777, 2024, 31337 };
        public List<string> Profiles = new List<string> { "novice", "average", "skilled" };
        public double Minutes = 20;
        public string? Brunch, Out;
        public int? Hz;   // the sim's tick rate; the game's own when not given
        public List<string> Labels = new List<string>();

        static readonly string[] PlayOpts = { "label", "seeds", "profiles", "minutes", "brunch", "out", "hz" };

        public const string Usage = "usage: play [--label X] [--seeds 1,7,42] [--profiles novice,average,skilled] " +
            "[--minutes 20] [--brunch A+] [--hz 60] [--out DIR]  |  report A [B] [--out DIR]";

        static string Name(string s, string what)
        {
            if (s.Length == 0 || s.IndexOfAny(new[] { '/', '\\', ':' }) >= 0
                || s.StartsWith(".", StringComparison.Ordinal))
                throw new UsageException(what + " '" + s + "' must be a plain name (no path)");
            return s;
        }

        public static Cli Parse(string[] a)
        {
            if (a.Length == 0 || (a[0] != "play" && a[0] != "report")) throw new UsageException(Usage);
            var c = new Cli { Command = a[0] };
            for (int i = 1; i < a.Length; i++)
            {
                if (!a[i].StartsWith("--", StringComparison.Ordinal))
                {
                    if (c.Command != "report") throw new UsageException("unexpected argument '" + a[i] + "'");
                    c.Labels.Add(Name(a[i], "label"));
                    continue;
                }
                string k = a[i].Substring(2);
                if (c.Command == "play" ? Array.IndexOf(PlayOpts, k) < 0 : k != "out")
                    throw new UsageException("unknown option --" + k);
                if (i + 1 >= a.Length) throw new UsageException("--" + k + " needs a value");
                string v = a[++i];
                switch (k)
                {
                    case "label": c.Label = Name(v, "--label"); break;
                    case "out":
                        if (v.Trim().Length == 0) throw new UsageException("--out needs a folder");
                        c.Out = v;
                        break;
                    case "seeds":
                        c.Seeds = v.Split(',').Select(s =>
                            uint.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var u) ? u
                            : throw new UsageException("--seeds: '" + s + "' is not a whole number from 0 to 4294967295"))
                            .ToList();
                        break;
                    case "profiles":
                        c.Profiles = v.Split(',').ToList();
                        foreach (var p in c.Profiles)
                            if (Profile.All.All(x => x.Name != p))
                                throw new UsageException("--profiles: no profile '" + p + "' (novice, average, skilled)");
                        break;
                    case "minutes":
                        if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out c.Minutes)
                            || !(c.Minutes > 0) || c.Minutes > 10000)
                            throw new UsageException("--minutes: '" + v + "' must be a number above 0 (at most 10000)");
                        break;
                    case "hz":
                        if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var hz) || hz < 1 || hz > 10000)
                            throw new UsageException("--hz: '" + v + "' must be a whole number of ticks a second, 1 to 10000");
                        c.Hz = hz;
                        break;
                    case "brunch":
                        if (v != "A" && v != "B" && v != "A+") throw new UsageException("--brunch is A, B or A+");
                        c.Brunch = v;
                        break;
                }
            }
            if (c.Command == "play" && c.Seeds.Count == 0) throw new UsageException("--seeds is empty");
            if (c.Command == "report" && (c.Labels.Count < 1 || c.Labels.Count > 2))
                throw new UsageException("report takes one label, or two to compare");
            return c;
        }
    }

    public static class Program
    {
        // playtest/ at the repository root (walking up from the working directory to CONVENTIONS.md), else ./playtest
        static string OutDir(Cli c)
        {
            if (c.Out != null) return Path.GetFullPath(c.Out);
            for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "CONVENTIONS.md"))
                    && Directory.Exists(Path.Combine(d.FullName, "csharp")))
                    return Path.Combine(d.FullName, "playtest");
            return Path.Combine(Directory.GetCurrentDirectory(), "playtest");
        }

        static void Save(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));

        /// <summary>Every failure is one line on stderr and a non-zero exit: 1 for bad input or files.</summary>
        public static int Main(string[] args)
        {
            try
            {
                var c = Cli.Parse(args);
                return c.Command == "play" ? Play(c) : ReportCmd(c);
            }
            catch (UsageException e) { Console.Error.WriteLine(e.Message); return 1; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("internal error: " + e.GetType().Name + ": " + e.Message);
                return 3;
            }
        }

        static int Play(Cli c)
        {
            // the game's own comparison switch, recorded in the header
            if (c.Brunch != null) Balance.BrunchVariant = c.Brunch;
            if (c.Hz != null) Balance.TickHz = c.Hz.Value;   // recorded in the header too
            string dir = OutDir(c);
            Directory.CreateDirectory(dir);
            var sw = Stopwatch.StartNew();
            var b = Matrix.Play(c.Label, c.Seeds, c.Profiles, c.Minutes, r =>
                Console.WriteLine($"{r.profile,-8} seed {r.seed,-6} floor {r.floor,-3} {r.end,-7} " +
                                  $"{Report.F1(r.ticks / (double)Balance.TickHz / 60)}min  " +
                                  $"dmg {Json.Text(r.dmgBySource)}" +
                                  (r.errors.Count > 0 ? "  ERRORS " + r.errors[0] : "")));
            string file = Path.Combine(dir, c.Label + ".json");
            Save(file, Json.Write(b));
            Console.WriteLine($"\n{b.runs.Count} runs in {sw.Elapsed.TotalSeconds:0.0}s, " +
                              $"built from {b.commit ?? "an unknown commit"} -> {file}");
            return 0;
        }

        static int ReportCmd(Cli c)
        {
            string dir = OutDir(c);
            var sets = new List<Batch>();
            foreach (var l in c.Labels)
            {
                string f = Path.Combine(dir, l + ".json");
                if (!File.Exists(f))
                    throw new UsageException("no playtest file " + f + " (play it with --label " + l + ")");
                sets.Add(Json.Read(File.ReadAllText(f), f));
            }
            string md = Report.Build(sets);
            string file = Path.Combine(dir, "report-" + string.Join("-vs-", c.Labels) + ".md");
            Save(file, md);
            Console.Write(md);
            Console.WriteLine("\nwrote " + file);
            return 0;
        }
    }
}
