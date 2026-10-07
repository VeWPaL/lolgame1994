using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The difficulty curve (C# only): the shape of Curve.Default, how it reaches the ladder, the hit and
    /// the drops, the switch the parity rows rely on, and the bot's `curve` check on hand-built files.
    /// </summary>
    [TestFixture, Category("csharp-only"), CurveOn(true)]
    public sealed class CurveTests
    {
        static Curve.Step Row(int floor) => Curve.Default[Math.Min(floor, Curve.Default.Length) - 1];

        // the pressure a floor's shape puts on the bare ladder: health times cadence times room size
        static double Press(Curve.Step s) => s.Tough * s.Rate * (3.5 + s.Bodies) / 3.5;

        static double Mean(IEnumerable<double> a) => a.Average();

        // ---- the shape

        [Test]
        public void TheStartIsABumpOverTheMiddle()
        {
            double start = Mean(new[] { 1, 2 }.Select(f => Press(Row(f))));
            double middle = Mean(Enumerable.Range(3, 8).Select(f => Press(Row(f))));
            Assert.That(start, Is.GreaterThan(middle * 1.2), "START presses harder than MIDDLE");
            Assert.That(new[] { 1, 2 }.Max(f => Row(f).Drops), Is.LessThan(Enumerable.Range(3, 8).Min(f => Row(f).Drops)),
                "the start pays fewer pickups than any middle floor: no kit yet, no free hearts");
        }

        [Test]
        public void TheMiddleIsADipBelowBothEnds()
        {
            double middleMax = Enumerable.Range(3, 8).Max(f => Press(Row(f)));
            Assert.That(Press(Row(2)), Is.GreaterThan(middleMax));
            Assert.That(Press(Row(Curve.EndFirst)), Is.GreaterThan(middleMax));
            for (int f = 3; f <= 10; f++)
                Assert.That(Row(f).Heavy + Bodies.MixFor(AreaRules.AreaForFloor(f)).Heavy, Is.GreaterThanOrEqualTo(0),
                    "the gunner chance never goes negative on floor " + f);
        }

        [Test]
        public void TheEndClimbsAndKeepsClimbing()
        {
            for (int f = 11; f < Curve.Default.Length; f++)
                Assert.That(Press(Row(f + 1)), Is.GreaterThanOrEqualTo(Press(Row(f))), "shape from floor " + f);
            for (int f = Curve.EndFirst; f < 40; f++)
                Assert.That(Balance.DepthTough(f + 1), Is.GreaterThan(Balance.DepthTough(f)), "health at floor " + (f + 1));
            Assert.That(Curve.At(100).Tough, Is.EqualTo(Curve.Default[Curve.Default.Length - 1].Tough),
                "floors past the table repeat its last row");
        }

        [Test]
        public void TheRateCapStillBindsWithTheCurve()
        {
            for (int f = 1; f <= 60; f++)
                Assert.That(Balance.DepthRate(f), Is.LessThanOrEqualTo(Balance.DepthRateCap), "floor " + f);
            Assert.That(Balance.DepthPack(1, 0), Is.GreaterThanOrEqualTo(0));
            Assert.That(Balance.DepthPack(5, 0), Is.GreaterThanOrEqualTo(0), "a negative pack step is floored at 0");
        }

        [Test]
        public void TheShapeReachesTheLadder()
        {
            var s = Curve.At(1);
            Assert.That(Balance.DepthTough(1), Is.EqualTo(s.Tough).Within(1e-12));
            Assert.That(Balance.DepthRate(1), Is.EqualTo(Math.Min(Balance.DepthRateCap, s.Rate)).Within(1e-12));
            Assert.That(Balance.DepthBodies(1, 3), Is.EqualTo(3 + s.Bodies).Within(1e-12));
            Assert.That(Balance.DepthPack(1, 0.45), Is.EqualTo(Math.Max(0, 0.45 + s.Pack)).Within(1e-12));
        }

        [Test, CurveOn(false)]
        public void OffIsTheJsLadder()
        {
            Assert.That(Curve.At(1).Tough, Is.EqualTo(1));
            Assert.That(Balance.DepthTough(1), Is.EqualTo(1.0));
            Assert.That(Balance.DepthTough(14), Is.EqualTo(2.5035).Within(0.005), "the parity row for floor 14");
            Assert.That(Curve.HitAt(1, 4), Is.EqualTo(4));
            Assert.That(Curve.At(1).Drops, Is.EqualTo(1));
        }

        [Test]
        public void TheSuiteDefaultIsOff()
        {
            // this fixture turns it on; the assembly default the parity rows run under is off
            Assert.That(Curve.On, Is.True);
            var a = (CurveOnAttribute)Attribute.GetCustomAttribute(typeof(CurveTests).Assembly, typeof(CurveOnAttribute))!;
            Assert.That(a, Is.Not.Null);
            bool was = Curve.On;
            a.BeforeTest(null!);
            try { Assert.That(Curve.On, Is.False); }
            finally { a.AfterTest(null!); }
            Assert.That(Curve.On, Is.EqualTo(was));
        }

        // ---- the hit and the drops

        [Test]
        public void AHitIsWholeHpRoundedHalfUpAndNeverBelowOne()
        {
            var saved = Curve.Table;
            try
            {
                Curve.Table = new[] { new Curve.Step(1, 1, 0, 0, 0, 0.75, 1), new Curve.Step(1, 1, 0, 0, 0, 1.5, 1) };
                Assert.That(Curve.HitAt(1, 1), Is.EqualTo(1), "contact 0.75 stays 1");
                Assert.That(Curve.HitAt(1, 2), Is.EqualTo(2), "a shell 1.5 rounds up to 2");
                Assert.That(Curve.HitAt(1, 4), Is.EqualTo(3), "a gunner shell 3");
                Assert.That(Curve.HitAt(1, 3), Is.EqualTo(2), "the sweep 2.25 is 2");
                Assert.That(Curve.HitAt(2, 1), Is.EqualTo(2), "1.5 rounds half up");
                Assert.That(Curve.HitAt(2, 0), Is.EqualTo(0), "no hit stays no hit");
                Balance.JsReference = true;
                Assert.That(Curve.HitAt(2, 1.8), Is.EqualTo(1.8), "the JS damage rules are left alone");
            }
            finally { Curve.Table = saved; Balance.JsReference = false; }
        }

        [Test]
        public void DropsScaleTheLootRollAndSpendOneDraw()
        {
            int Count(double scale, string kind)
            {
                var rng = new Rng(7);
                int n = 0;
                for (int i = 0; i < 20000; i++) { var d = Loot.Drop(rng, 0, 0, scale); if (d != null && (kind == "" || d.kind == kind)) n++; }
                return n;
            }
            Assert.That(Count(0, ""), Is.EqualTo(0), "scale 0: nothing drops");
            int full = Count(1, ""), half = Count(0.5, "");
            Assert.That(full / 20000.0, Is.EqualTo(0.26).Within(0.01));
            Assert.That(half / (double)full, Is.EqualTo(0.5).Within(0.03));
            Assert.That(Count(0.5, "heart") / (double)Count(1, "heart"), Is.EqualTo(0.5).Within(0.08), "every band shrinks alike");
            // one draw each, whatever the scale: the run stream stays where it was
            var a = new Rng(3); var b = new Rng(3);
            Loot.Drop(a, 0, 0, 1); Loot.Drop(b, 0, 0, 0);
            Assert.That(a.Run(), Is.EqualTo(b.Run()));
        }

        // ---- the check

        sealed class Spec
        {
            public double[] Start = { 0.10, 0.10 }, Middle = { 0.03 }, Ramp = { 0.3 }, End = { 0.35, 0.4, 0.45, 0.5, 0.55 };
            public double HpOut2 = 5, Dmg = 10, Heal = 8, HeartHeal = 1;
            public bool Sources = true;
            public Dictionary<string, double> Src = new Dictionary<string, double> { ["a"] = 5, ["b"] = 5 };

            public double Hazard(int floor) =>
                floor <= 2 ? Start[floor - 1] : floor <= 10 ? Middle[Math.Min(floor - 3, Middle.Length - 1)]
                : floor <= 12 ? Ramp[Math.Min(floor - 11, Ramp.Length - 1)] : End[Math.Min(floor - 13, End.Length - 1)];
        }

        static Spec Novice() => new Spec();
        static Spec Average() => new Spec { Start = new[] { 0.04, 0.04 }, Middle = new[] { 0.01 }, Ramp = new[] { 0.05 },
                                            End = new[] { 0.06, 0.08, 0.10, 0.12, 0.14 }, HpOut2 = 7 };
        static Spec Skilled() => new Spec { Start = new[] { 0.01, 0.01 }, Middle = new[] { 0.003 }, Ramp = new[] { 0.02 },
                                            End = new[] { 0.03, 0.04, 0.05, 0.06, 0.07 }, HpOut2 = 7 };

        // N runs a profile; at each floor the first round(h x alive) of the runs still alive die there
        static Batch Synth(Spec? novice = null, Spec? average = null, Spec? skilled = null, int n = 1000, int last = 18)
        {
            var b = new Batch { label = "synth", tickHz = 60, minutes = 60, brunch = "A+",
                                seeds = Enumerable.Range(1, 200).Select(i => (uint)i).ToList() };
            foreach (var (name, spec) in new[] { ("novice", novice ?? Novice()), ("average", average ?? Average()), ("skilled", skilled ?? Skilled()) })
            {
                b.profiles.Add(name);
                var alive = Enumerable.Range(0, n).Select(i => new RunResult { seed = (uint)i, profile = name }).ToList();
                foreach (var r in alive)
                {
                    r.healBy["heart"] = spec.HeartHeal; r.healBy["clear"] = 10 - spec.HeartHeal;
                }
                var all = alive.ToList();
                for (int f = 1; f <= last && alive.Count > 0; f++)
                {
                    int k = (int)Math.Round(spec.Hazard(f) * alive.Count);
                    for (int i = 0; i < alive.Count; i++)
                    {
                        var r = alive[i];
                        bool dies = i < k, end = f == last;
                        r.floors.Add(new FloorRecord { floor = f, dmg = spec.Dmg, healed = spec.Heal, bossKilled = !dies && !end,
                            hpOut = dies ? 0 : f == 2 ? spec.HpOut2 : 7,
                            dmgBySource = spec.Sources ? new SortedDictionary<string, double>(spec.Src) : null });
                        r.floor = f;
                        if (dies) r.end = "death";
                    }
                    alive.RemoveRange(0, k);
                }
                b.runs.AddRange(all);
            }
            return b;
        }

        static List<string> Misses(Batch b) => CurveCheck.Check(b).misses;

        [Test]
        public void AFileThatMeetsEveryTargetPasses()
        {
            var (text, misses) = CurveCheck.Check(Synth());
            Assert.That(misses, Is.Empty, text);
            Assert.That(text, Does.Contain("Every target met."));
            var nov = CurveCheck.Measure(Synth(), "novice");
            Assert.That(nov.start.Hazard, Is.EqualTo(0.10).Within(0.002));
            Assert.That(nov.start.floors, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(nov.middle.floors, Is.EqualTo(Enumerable.Range(3, 8)));
            Assert.That(nov.end.floors.Min(), Is.EqualTo(13));
            Assert.That(nov.end.floors.All(f => nov.perFloor[f].entries >= Curve.MinEntries));
            Assert.That(nov.medianFloor, Is.InRange(7, 12));
        }

        [Test]
        public void EachMissIsNamed()
        {
            Assert.That(Misses(Synth(novice: new Spec { Start = new[] { 0.2, 0.2 } })),
                Has.Some.Contains("novice START hazard"));
            Assert.That(Misses(Synth(skilled: new Spec { Start = new[] { 0.01, 0.01 }, Middle = new[] { 0.02 }, Ramp = new[] { 0.02 },
                                                         End = new[] { 0.03, 0.04, 0.05, 0.06, 0.07 }, HpOut2 = 7 })),
                Has.Some.Match("^skilled: MIDDLE .* is not below both"));
            Assert.That(Misses(Synth(average: new Spec { Start = new[] { 0.04, 0.04 }, Middle = new[] { 0.01 }, Ramp = new[] { 0.05 },
                                                         End = new[] { 0.6, 0.6, 0.6, 0.02, 0.02 }, HpOut2 = 7 })),
                Has.Some.Contains("average: END falls"));
            Assert.That(Misses(Synth(skilled: Average())), Has.Some.Contains("START: skilled"));
            Assert.That(Misses(Synth(novice: new Spec { HpOut2 = 6 })), Has.Some.Contains("median HP leaving floor 2 is 6"));
            Assert.That(Misses(Synth(novice: new Spec { Heal = 10 })), Has.Some.Contains("novice: MIDDLE heals 10"));
            Assert.That(Misses(Synth(novice: new Spec { Src = new Dictionary<string, double> { ["a"] = 6, ["b"] = 4 } })),
                Has.Some.Contains("novice: a deals 60.00% of START damage"));
            Assert.That(Misses(Synth(novice: new Spec { Sources = false })), Has.Some.Contains("not recorded"));
            Assert.That(Misses(Synth(novice: new Spec { HeartHeal = 0 })), Has.Some.Contains("heart pickups are 0.00%"));
            Assert.That(Misses(Synth(novice: new Spec { Ramp = new[] { 0.0 }, End = new[] { 0.10, 0.12, 0.14, 0.16, 0.18 } })),
                Has.Some.Contains("novice: median floor"));
            var noSkilled = Synth();
            noSkilled.runs.RemoveAll(r => r.profile == "skilled"); noSkilled.profiles.Remove("skilled");
            Assert.That(Misses(noSkilled), Has.Some.EqualTo("skilled: not played"));
        }

        [Test]
        public void TheCheckReadsTheTargetsTheGameShips()
        {
            var bad = Synth(novice: new Spec { Start = new[] { 0.2, 0.2 } });
            var saved = Curve.Targets[0];
            try
            {
                Curve.Targets[0] = new Curve.Target("novice", new Curve.Band(0.06, 0.25), saved.Middle, saved.End,
                                                    saved.MedianFloorMin, saved.MedianFloorMax);
                Assert.That(Misses(bad), Has.None.Contains("novice START hazard"));
            }
            finally { Curve.Targets[0] = saved; }
            Assert.That(Misses(bad), Has.Some.Contains("novice START hazard"));
        }

        [Test]
        public void TheCommandExitsTwoOnAMissAndZeroOnAPass()
        {
            string dir = Path.Combine(Path.GetTempPath(), "depths-curve-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "good.json"), Json.Write(Synth()));
                File.WriteAllText(Path.Combine(dir, "bad.json"), Json.Write(Synth(novice: new Spec { HpOut2 = 8 })));
                Assert.That(Program.Main(new[] { "curve", "good", "--out", dir }), Is.EqualTo(0));
                Assert.That(Program.Main(new[] { "curve", "bad", "--out", dir }), Is.EqualTo(2));
                Assert.That(File.Exists(Path.Combine(dir, "curve-bad.md")));
                Assert.That(Program.Main(new[] { "curve", "missing", "--out", dir }), Is.EqualTo(1));
                Assert.That(Program.Main(new[] { "curve", "good", "bad", "--out", dir }), Is.EqualTo(1));
                Assert.That(Program.Main(new[] { "curve", "good", "--seeds", "1" }), Is.EqualTo(1));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void PlayRecordsTheCurveAndCanTurnItOff()
        {
            Assert.That(Cli.Parse(new[] { "play" }).Curve, Is.True);
            Assert.That(Cli.Parse(new[] { "play", "--curve", "off" }).Curve, Is.False);
            Assert.Throws<UsageException>(() => Cli.Parse(new[] { "play", "--curve", "maybe" }));
            var b = Matrix.Play("x", new uint[] { 1 }, new[] { "novice" }, 0.05);
            Assert.That(b.curve, Is.True);
            Assert.That(b.runs[0].floors[0].dmgBySource, Is.Not.Null, "damage sources are kept per floor");
        }
    }
}
