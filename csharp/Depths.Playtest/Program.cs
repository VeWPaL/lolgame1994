using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Depths.Playtest
{
    public static class Json
    {
        // relaxed escaping keeps "A+" readable; the output is a file, never embedded in HTML
        static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        static readonly JsonSerializerOptions Compact = new JsonSerializerOptions { IncludeFields = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static string Write(Batch b) => JsonSerializer.Serialize(b, Pretty).Replace("\r\n", "\n") + "\n";
        public static string Write(RunResult r) => JsonSerializer.Serialize(r, Compact);
        public static Batch Read(string text) => JsonSerializer.Deserialize<Batch>(text, Compact) ?? throw new InvalidDataException("empty playtest file");
        public static string Text(object? o) => JsonSerializer.Serialize(o, Compact);
    }

    /// <summary>Plays the matrix: every profile against every seed, one run after another (the core has static scratch).</summary>
    public static class Matrix
    {
        public static Batch Play(string label, IReadOnlyList<uint> seeds, IReadOnlyList<string> profiles, double minutes,
                                 Action<RunResult>? each = null)
        {
            var b = new Batch { label = label, brunch = Balance.BrunchVariant, tickHz = Balance.TickHz, minutes = minutes,
                                seeds = seeds.ToList(), profiles = profiles.ToList() };
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

    public static class Program
    {
        static string? Opt(string[] a, string k)
        {
            int i = Array.IndexOf(a, "--" + k);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        // playtest/ at the repository root (found by walking up to CONVENTIONS.md), else the working directory
        static string OutDir(string[] a)
        {
            var o = Opt(a, "out");
            if (o != null) return Path.GetFullPath(o);
            for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "CONVENTIONS.md")) && Directory.Exists(Path.Combine(d.FullName, "csharp")))
                    return Path.Combine(d.FullName, "playtest");
            return Path.Combine(Directory.GetCurrentDirectory(), "playtest");
        }

        static string? Git(string dir, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                using var pr = Process.Start(psi);
                if (pr == null) return null;
                string s = pr.StandardOutput.ReadToEnd().Trim();
                pr.WaitForExit();
                return pr.ExitCode == 0 ? s : null;
            }
            catch (Exception) { return null; }
        }

        static void Save(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "play") return Play(args);
                if (args.Length > 1 && args[0] == "report") return ReportCmd(args);
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is JsonException || e is FormatException)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
            Console.Error.WriteLine("usage: play --label X [--seeds 1,7,42] [--profiles novice,average,skilled] [--minutes 20] [--brunch A+] [--out DIR]\n" +
                                    "       report A [B] [--out DIR]");
            return 2;
        }

        static int Play(string[] a)
        {
            string label = Opt(a, "label") ?? "current";
            var seeds = (Opt(a, "seeds") ?? "1,7,42,777,2024,31337").Split(',').Select(s => uint.Parse(s, CultureInfo.InvariantCulture)).ToList();
            var profiles = (Opt(a, "profiles") ?? "novice,average,skilled").Split(',').ToList();
            foreach (var p in profiles) Profile.Get(p);
            double minutes = double.Parse(Opt(a, "minutes") ?? "20", CultureInfo.InvariantCulture);
            var brunch = Opt(a, "brunch");
            if (brunch != null)
            {
                if (brunch != "A" && brunch != "B" && brunch != "A+") throw new ArgumentException("--brunch is A, B or A+");
                Balance.BrunchVariant = brunch;   // the game's own comparison switch, recorded in the header
            }
            string dir = OutDir(a);

            var sw = Stopwatch.StartNew();
            var b = Matrix.Play(label, seeds, profiles, minutes, r =>
                Console.WriteLine($"{r.profile,-8} seed {r.seed,-6} floor {r.floor,-3} {r.end,-7} {Report.F1(r.ticks / (double)Balance.TickHz / 60)}min  " +
                                  $"dmg {Json.Text(r.dmgBySource)}{(r.errors.Count > 0 ? "  ERRORS " + r.errors[0] : "")}"));
            var root = Path.GetDirectoryName(dir) ?? dir;
            var head = Git(root, "rev-parse --short HEAD");
            if (head != null) b.commit = head + (string.IsNullOrEmpty(Git(root, "status --porcelain --untracked-files=no")) ? "" : "+dirty");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, label + ".json");
            Save(file, Json.Write(b));
            Console.WriteLine($"\n{b.runs.Count} runs in {sw.Elapsed.TotalSeconds:0.0}s -> {file}");
            return 0;
        }

        static int ReportCmd(string[] a)
        {
            var labels = new List<string>();
            for (int i = 1; i < a.Length; i++) { if (a[i].StartsWith("--", StringComparison.Ordinal)) { i++; continue; } labels.Add(a[i]); }
            if (labels.Count > 2) throw new ArgumentException("report takes one label, or two to compare");
            string dir = OutDir(a);
            var sets = labels.Select(l => Json.Read(File.ReadAllText(Path.Combine(dir, l + ".json")))).ToList();
            string md = Report.Build(sets);
            string file = Path.Combine(dir, "report-" + string.Join("-vs-", labels) + ".md");
            Save(file, md);
            Console.Write(md);
            Console.WriteLine("\nwrote " + file);
            return 0;
        }
    }
}
