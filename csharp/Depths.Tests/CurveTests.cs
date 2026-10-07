using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The difficulty curve (C# only): the shape of Curve.Stages, that the game and the bot call each hook,
    /// the switch the parity rows rely on, and the bot's `curve` check on hand-built files.
    /// </summary>
    [TestFixture, Category("csharp-only"), CurveOn(true)]
    public sealed class CurveTests
    {
        static Curve.Stage S(string name) => Curve.Stages.First(x => x.Name == name);

        // the pressure a floor's shape puts on the bare ladder: health times cadence times room size
        static double Press(int floor)
        {
            var s = Curve.At(floor);
            return s.Tough * s.Rate * (3.5 + s.Bodies) / 3.5;
        }

        static double Mean(IEnumerable<double> a) => a.Average();

        static RunState Started(int floor)
        {
            var run = new RunState(new Rng(4242));
            run.Start(4242);
            run.enemies.Clear(); run.pickups.Clear();
            run.floor = floor;   // the harness moves the floor number only; the hooks read nothing else
            return run;
        }

        // ---- the shape

        [Test]
        public void TheStagesRunInFloorOrderFromTheFirstFloor()
        {
            Assert.That(Curve.Stages[0].First, Is.EqualTo(1));
            for (int i = 1; i < Curve.Stages.Length; i++)
                Assert.That(Curve.Stages[i].First, Is.GreaterThan(Curve.Stages[i - 1].First), Curve.Stages[i].Name);
            Assert.That(Curve.Stages.Select(x => x.Name).Distinct().Count(), Is.EqualTo(Curve.Stages.Length), "names are dial addresses");
            Assert.That(Curve.At(7), Is.SameAs(S("middle")), "a stage holds until the next one");
            Assert.That(Curve.At(100), Is.SameAs(Curve.Stages[Curve.Stages.Length - 1]), "the last stage holds from its floor on");
        }

        [Test]
        public void TheStartIsABumpOverTheMiddle()
        {
            double start = Mean(new[] { 1, 2 }.Select(Press));
            double middle = Mean(Enumerable.Range(3, 8).Select(Press));
            Assert.That(start, Is.GreaterThan(middle * 1.2), "START presses harder than MIDDLE");
            Assert.That(new[] { 1, 2 }.Max(f => Curve.At(f).Drops), Is.LessThan(Enumerable.Range(3, 8).Min(f => Curve.At(f).Drops)),
                "the start pays fewer pickups than any middle floor: no kit yet, no free hearts");
        }

        [Test]
        public void TheMiddleIsADipBelowBothEnds()
        {
            double middleMax = Enumerable.Range(3, 8).Max(Press);
            Assert.That(Press(2), Is.GreaterThan(middleMax));
            Assert.That(Press(11), Is.GreaterThan(middleMax));
            Assert.That(Balance.DepthTough(Curve.EndFirst), Is.GreaterThan(Enumerable.Range(3, 8).Max(Balance.DepthTough)),
                "END bodies are tougher than any MIDDLE body");
            for (int f = 1; f <= 40; f++)
                Assert.That(Curve.At(f).Heavy + Bodies.MixFor(AreaRules.AreaForFloor(f)).Heavy, Is.InRange(0, 1),
                    "the gunner chance stays a chance on floor " + f);
        }

        [Test]
        public void FromTheRampOnNoFloorIsEasierThanTheOneBefore()
        {
            // the curve's own stat terms: body health, the room's extra bodies (whole ones), packs, gunners
            int Extra(int f) => (int)Math.Floor(Balance.DepthBodies(f, 0));
            double Gun(int f) => Bodies.MixFor(AreaRules.AreaForFloor(f)).Heavy + Curve.At(f).Heavy;
            double Pack(int f) => Balance.DepthPack(f, Bodies.MixFor(AreaRules.AreaForFloor(f)).Brunch);
            for (int f = 11; f < 40; f++)
            {
                Assert.That(Balance.DepthTough(f + 1), Is.GreaterThan(Balance.DepthTough(f)), "health at floor " + (f + 1));
                Assert.That(Extra(f + 1), Is.GreaterThanOrEqualTo(Extra(f)), "bodies at floor " + (f + 1));
                Assert.That(Gun(f + 1), Is.GreaterThanOrEqualTo(Gun(f)), "gunners at floor " + (f + 1));
                Assert.That(Pack(f + 1), Is.GreaterThanOrEqualTo(Pack(f)), "packs at floor " + (f + 1));
            }
            Assert.That(Balance.DepthTough(Curve.EndFirst), Is.GreaterThanOrEqualTo(Balance.DepthTough(Curve.EndFirst - 1) * 1.05),
                "the last area opens tougher than the floor before it");
        }

        [Test]
        public void TheExtraBodiesComeOneStepAtATime()
        {
            // a whole extra body per room is the curve's biggest single step; no floor in reach adds two at once,
            // and the first END floors do not add one on top of the area change
            int Extra(int f) => (int)Math.Floor(Balance.DepthBodies(f, 0));
            for (int f = 11; f < 18; f++) Assert.That(Extra(f + 1) - Extra(f), Is.LessThanOrEqualTo(1), "floor " + (f + 1));
            Assert.That(Extra(12), Is.EqualTo(Extra(11) + 1), "the step comes on the ramp");
            Assert.That(Extra(16), Is.EqualTo(Extra(12)), "and holds through 16");
            Assert.That(Extra(17), Is.EqualTo(Extra(16)), "the deep stage holds the second step off floor 17");
        }

        [Test]
        public void TheEndClimbsAndKeepsClimbing()
        {
            for (int f = Curve.EndFirst; f < 40; f++)
            {
                Assert.That(Balance.DepthTough(f + 1), Is.GreaterThan(Balance.DepthTough(f)), "health at floor " + (f + 1));
                Assert.That(Balance.DepthBodies(f + 1, 0), Is.GreaterThanOrEqualTo(Balance.DepthBodies(f, 0)), "density at floor " + (f + 1));
            }
        }

        [Test]
        public void TheRateCapStillBindsWithTheCurve()
        {
            for (int f = 1; f <= 60; f++)
                Assert.That(Balance.DepthRate(f), Is.LessThanOrEqualTo(Balance.DepthRateCap), "floor " + f);
            for (int f = 1; f <= 60; f++)
                Assert.That(Balance.DepthPack(f, 0), Is.GreaterThanOrEqualTo(0), "a negative pack step is floored at 0, floor " + f);
        }

        [Test]
        public void TheShapeReachesTheLadder()
        {
            var s = Curve.At(1);
            Assert.That(Balance.DepthTough(1), Is.EqualTo(s.Tough).Within(1e-12));
            Assert.That(Balance.DepthRate(1), Is.EqualTo(Math.Min(Balance.DepthRateCap, s.Rate)).Within(1e-12));
            var m = Curve.At(5);
            Assert.That(Balance.DepthBodies(5, 3), Is.EqualTo(3 + 0.085 * (Math.Exp(0.12 * 1.7 * 4) - 1) + m.Bodies).Within(1e-12));
            Assert.That(Balance.DepthPack(5, 0.5), Is.EqualTo(Math.Max(0, 0.5 + 0.035 * 4 + m.Pack)).Within(1e-12));
            Assert.That(Spawn.Body(Started(5), BodyKind.Lunger, 300, 300).maxHp,
                Is.EqualTo(Enemy.Of(BodyKind.Lunger, 0, 0).maxHp * Balance.DepthTough(5)).Within(1e-9), "a spawned body carries it");
        }

        [Test, CurveOn(false)]
        public void OffIsTheJsLadder()
        {
            Assert.That(Curve.At(1).Tough, Is.EqualTo(1));
            Assert.That(Balance.DepthTough(1), Is.EqualTo(1.0));
            Assert.That(Balance.DepthTough(14), Is.EqualTo(2.5035).Within(0.005), "the parity row for floor 14");
            Assert.That(Curve.At(1).Drops, Is.EqualTo(1));
            Assert.That(Curve.At(1).Heavy, Is.EqualTo(0));
        }

        [Test]
        public void TheSuiteDefaultIsOff()
        {
            // this fixture turns it on; the assembly default the parity rows run under is off
            Assert.That(Curve.On, Is.True);
            var a = (CurveOnAttribute)Attribute.GetCustomAttribute(typeof(CurveTests).Assembly, typeof(CurveOnAttribute))!;
            Assert.That(a, Is.Not.Null);
            a.BeforeTest(null!);
            try { Assert.That(Curve.On, Is.False); }
            finally { a.AfterTest(null!); }
            Assert.That(Curve.On, Is.True);
        }

        // ---- the game calls each hook

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
            var a = new Rng(3); var b = new Rng(3);
            Loot.Drop(a, 0, 0, 1); Loot.Drop(b, 0, 0, 0);
            Assert.That(a.Run(), Is.EqualTo(b.Run()), "one draw whatever the scale: the run stream stays put");
        }

        // a kill's loot, through the game's own kill path, at a floor
        static double KillDropRate(int floor, int n = 6000)
        {
            var run = Started(floor);
            for (int i = 0; i < n; i++)
            {
                run.enemies.Add(Enemy.Of(BodyKind.Lunger, 300, 300));
                Kills.KillEnemy(run, 0);
            }
            return run.pickups.Count / (double)n;
        }

        [Test]
        public void AKillDropsWhatItsFloorSays()
        {
            Assert.That(KillDropRate(1), Is.EqualTo(0), "START: no kill drops");
            Assert.That(KillDropRate(5), Is.EqualTo(0.26 * Curve.At(5).Drops).Within(0.012), "MIDDLE: scarce");
            Assert.That(KillDropRate(Curve.EndFirst), Is.EqualTo(0.26 * Curve.At(Curve.EndFirst).Drops).Within(0.02));
        }

        [Test, CurveOn(false)]
        public void OffAKillDropsTheJsChance() => Assert.That(KillDropRate(1), Is.EqualTo(0.26).Within(0.02));

        // gunners per three-plus-body wave, through the game's own planner, at a floor
        static double GunnerRate(int floor)
        {
            var planner = new WavePlanner(new Rng(99));
            int waves = 0, gunners = 0;
            for (int i = 0; i < 4000; i++)
            {
                var w = planner.PlanWave(floor, Dir.N, AreaRules.AreaForFloor(floor));
                if (w.Slots.Count < 3) continue;
                waves++;
                if (w.Slots.Any(x => x.Kind == BodyKind.Gunner)) gunners++;
            }
            return gunners / (double)waves;
        }

        [Test]
        public void AWavesGunnerChanceIsTheAreasPlusItsFloors()
        {
            foreach (int f in new[] { 1, 5, 12 })
            {
                double want = Bodies.MixFor(AreaRules.AreaForFloor(f)).Heavy + Curve.At(f).Heavy;
                Assert.That(GunnerRate(f), Is.EqualTo(want).Within(0.03), "floor " + f);
            }
        }

        [Test, CurveOn(false)]
        public void OffAWavesGunnerChanceIsTheAreas() =>
            Assert.That(GunnerRate(1), Is.EqualTo(Bodies.MixFor(Area.Area1).Heavy).Within(0.03));

        [Test]
        public void AWaveCarriesItsFloorsDensity()
        {
            // the soft floor takes a body off every wave; a wave never falls under two
            var on = new WavePlanner(new Rng(5));
            double sum = 0;
            for (int i = 0; i < 3000; i++)
            {
                var w = on.PlanWave(3, Dir.N, Area.Area1);
                Assert.That(w.Slots.Count, Is.GreaterThanOrEqualTo(2));
                sum += w.Slots.Count;
            }
            Curve.On = false;
            try
            {
                var off = new WavePlanner(new Rng(5));
                double sumOff = 0;
                for (int i = 0; i < 3000; i++) sumOff += off.PlanWave(3, Dir.N, Area.Area1).Slots.Count;
                Assert.That(sum / 3000, Is.LessThan(sumOff / 3000 - 0.6), "about one slot fewer");
            }
            finally { Curve.On = true; }
        }

        // ---- the bot books it

        [Test]
        public void AFloorsSourcesSumToItsDamageAndHealing()
        {
            var r = new Runner(31337, "novice", 2.5).Play();
            Assert.That(r.floors.Sum(f => f.dmg), Is.GreaterThan(0), "the fixture took no damage");
            Assert.That(r.floors.Sum(f => f.healed), Is.GreaterThan(0), "the fixture healed nothing");
            foreach (var f in r.floors)
            {
                Assert.That(f.dmgBySource!.Values.Sum(), Is.EqualTo(f.dmg).Within(1e-9), "floor " + f.floor);
                Assert.That(f.healBy!.Values.Sum(), Is.EqualTo(f.healed).Within(1e-9), "floor " + f.floor);
            }
            foreach (var kv in r.dmgBySource)
                Assert.That(r.floors.Sum(f => f.dmgBySource!.GetValueOrDefault(kv.Key)), Is.EqualTo(kv.Value).Within(1e-9), kv.Key);
            foreach (var kv in r.healBy)
                Assert.That(r.floors.Sum(f => f.healBy!.GetValueOrDefault(kv.Key)), Is.EqualTo(kv.Value).Within(1e-9), kv.Key);
        }

        // ---- the check

        sealed class Spec
        {
            public double[] Start = { 0.10, 0.10 }, Middle = { 0.03 }, Ramp = { 0.3 }, End = { 0.35, 0.4, 0.45, 0.5, 0.55 };
            public double HpOut2 = 5, HpOut10 = 7, Dmg = 10, Heal = 8, StartHeal = 8, Regen = 0, HeartHeal = 1;
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
                        r.floors.Add(new FloorRecord { floor = f, dmg = spec.Dmg, healed = f <= Curve.StartLast ? spec.StartHeal : spec.Heal, bossKilled = !dies && !end,
                            regen = f <= Curve.StartLast ? 0 : spec.Regen, hpIn = 7, maxHpIn = 8,
                            hpOut = dies ? 0 : f == 2 ? spec.HpOut2 : f == Curve.MiddleLast ? spec.HpOut10 : 7,
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
            Assert.That(text, Does.Contain("PASS: every target met"));
            var nov = CurveCheck.Measure(Synth(), "novice");
            Assert.That(nov.start.Hazard, Is.EqualTo(0.10).Within(0.002));
            Assert.That(nov.start.floors, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(nov.middle.floors, Is.EqualTo(Enumerable.Range(3, 8)));
            Assert.That(nov.end.floors.Min(), Is.EqualTo(13));
            Assert.That(nov.end.floors.All(f => nov.perFloor[f].entries >= Curve.MinEntries));
            Assert.That(nov.medianFloor, Is.InRange(7, 12));
        }

        [Test]
        public void EachMissIsNamedWithItsRubricNumber()
        {
            Assert.That(Misses(Synth(novice: new Spec { Start = new[] { 0.2, 0.2 } })),
                Has.Some.StartsWith("1 novice START hazard"));
            Assert.That(Misses(Synth(skilled: new Spec { Start = new[] { 0.01, 0.01 }, Middle = new[] { 0.02 }, Ramp = new[] { 0.02 },
                                                         End = new[] { 0.03, 0.04, 0.05, 0.06, 0.07 }, HpOut2 = 7 })),
                Has.Some.Match("^2 skilled MIDDLE .* below both"));
            Assert.That(Misses(Synth(average: new Spec { Start = new[] { 0.04, 0.04 }, Middle = new[] { 0.01 }, Ramp = new[] { 0.05 },
                                                         End = new[] { 0.6, 0.6, 0.6, 0.02, 0.02 }, HpOut2 = 7 })),
                Has.Some.StartsWith("3 average END halves"));
            Assert.That(Misses(Synth(skilled: Average())), Has.Some.StartsWith("4 START orders"));
            Assert.That(Misses(Synth(novice: new Spec { HpOut2 = 6 })), Has.Some.StartsWith("1 novice median HP leaving floor 2 6"));
            Assert.That(Misses(Synth(novice: new Spec { Heal = 10 })), Has.Some.StartsWith("5 novice MIDDLE healing a floor 10, wants below its damage 10"));
            Assert.That(Misses(Synth(novice: new Spec { Src = new Dictionary<string, double> { ["a"] = 6, ["b"] = 4 } })),
                Has.Some.StartsWith("1 novice top START source a 60.00%"));
            Assert.That(Misses(Synth(novice: new Spec { Sources = false })), Has.Some.Contains("not recorded"));
            Assert.That(Misses(Synth(novice: new Spec { HeartHeal = 0 })), Has.Some.StartsWith("5 novice heart pickups 0.00%"));
            Assert.That(Misses(Synth(novice: new Spec { Ramp = new[] { 0.0 }, End = new[] { 0.10, 0.12, 0.14, 0.16, 0.18 } })),
                Has.Some.StartsWith("4 novice median floor"));
            var noSkilled = Synth();
            noSkilled.runs.RemoveAll(r => r.profile == "skilled"); noSkilled.profiles.Remove("skilled");
            Assert.That(Misses(noSkilled), Has.Some.EqualTo("1-5 skilled: not played"));
        }

        [Test]
        public void AStartThatHealsAsMuchAsItTakesFails()
        {
            var misses = Misses(Synth(average: new Spec { Start = new[] { 0.04, 0.04 }, Middle = new[] { 0.01 }, Ramp = new[] { 0.05 },
                                                          End = new[] { 0.06, 0.08, 0.10, 0.12, 0.14 }, HpOut2 = 7, StartHeal = 10 }));
            Assert.That(misses, Is.EqualTo(new[] { "5 average START healing a floor 10, wants below its damage 10" }), "that line, and only it");
        }

        [Test]
        public void EveryTargetPrintsOneLineOkOrMiss()
        {
            var (text, _) = CurveCheck.Check(Synth());
            var lines = text.Split('\n').Where(l => l.StartsWith("ok   ") || l.StartsWith("MISS ")).ToList();
            Assert.That(lines.Count, Is.EqualTo(3 * 10 + 1 + 3), "ten a profile, the novice HP, three orderings");
            Assert.That(lines.All(l => char.IsDigit(l[5])), "each led by its rubric number");
            Assert.That(text, Does.Contain("PASS: every target met"));
            Assert.That(text, Does.Contain("## Healing by source"));
        }

        static Spec Mid(Spec s, double heal, double regen, double hpOut10 = 7)
        {
            s.Heal = heal; s.Regen = regen; s.HpOut10 = hpOut10; return s;
        }

        static Batch MidBatch(double heal, double regen, double hpOut10 = 7) =>
            Synth(Mid(Novice(), heal, regen, hpOut10), Mid(Average(), heal, regen, hpOut10), Mid(Skilled(), heal, regen, hpOut10));

        [Test]
        public void TheMiddleRuleIsSelectableAndEachReadingIsItsFormula()
        {
            Assert.That(Curve.MidRule, Is.EqualTo(Curve.MiddleRule.Original), "the rubric as written until the owner chooses");
            // healing 10 a floor against 10 damage, 5 of it the regenerating heart: original misses, pickups holds
            var b = MidBatch(10, 5);
            Assert.That(CurveCheck.Check(b, Curve.MiddleRule.Original).misses, Has.Some.StartsWith("5 novice MIDDLE healing a floor 10"));
            Assert.That(CurveCheck.Check(b, Curve.MiddleRule.Pickups).misses, Is.Empty);
            Assert.That(CurveCheck.Check(b, Curve.MiddleRule.Recovery).misses, Is.Empty, "7 in, 7 out: nothing given back");
            Assert.That(CurveCheck.Check(MidBatch(10, 0), Curve.MiddleRule.Pickups).misses,
                Has.Some.StartsWith("5 average MIDDLE (pickups) pickup healing a floor 10, wants below its damage 10"));
            // entering 3 at 7 of 8 and max HP staying 8: leaving 10 at 8 is the 1 START took, at 9 one more
            Assert.That(CurveCheck.Check(MidBatch(8, 0, 8), Curve.MiddleRule.Recovery).misses, Is.Empty);
            Assert.That(CurveCheck.Check(MidBatch(8, 0, 9), Curve.MiddleRule.Recovery).misses,
                Has.Some.StartsWith("5 skilled MIDDLE (recovery) gives back 2 HP + armour, wants at most START's loss plus new max HP 1"));
            var saved = Curve.MidRule;
            try
            {
                Curve.MidRule = Curve.MiddleRule.Pickups;
                Assert.That(CurveCheck.Check(b).misses, Is.Empty, "the check reads the line the game ships");
            }
            finally { Curve.MidRule = saved; }
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
                File.WriteAllText(Path.Combine(dir, "mid.json"), Json.Write(MidBatch(10, 5)));
                Assert.That(Program.Main(new[] { "curve", "mid", "--out", dir }), Is.EqualTo(2), "the rubric as written");
                Assert.That(Program.Main(new[] { "curve", "mid", "--mid-rule", "pickups", "--out", dir }), Is.EqualTo(0));
                Assert.That(Program.Main(new[] { "curve", "mid", "--mid-rule", "recovery", "--out", dir }), Is.EqualTo(0));
                Assert.That(File.ReadAllText(Path.Combine(dir, "curve-mid.md")), Does.Contain("read as: recovery"));
                Assert.That(Program.Main(new[] { "curve", "mid", "--mid-rule", "2", "--out", dir }), Is.EqualTo(1));
                Assert.That(Program.Main(new[] { "curve", "mid", "--mid-rule", "kind", "--out", dir }), Is.EqualTo(1));
                Assert.That(Program.Main(new[] { "play", "--mid-rule", "pickups" }), Is.EqualTo(1), "a check option, not a play one");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void PlayRecordsTheCurveAndTheDials()
        {
            Assert.That(Cli.Parse(new[] { "play" }).Curve, Is.True);
            Assert.That(Cli.Parse(new[] { "play", "--curve", "off" }).Curve, Is.False);
            Assert.Throws<UsageException>(() => Cli.Parse(new[] { "play", "--curve", "maybe" }));
            string dir = Path.Combine(Path.GetTempPath(), "depths-curve-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Batch Played(params string[] extra)
                {
                    var args = new List<string> { "play", "--label", "p", "--seeds", "3", "--profiles", "novice", "--minutes", "0.5", "--out", dir };
                    args.AddRange(extra);
                    Assert.That(Program.Main(args.ToArray()), Is.EqualTo(0));
                    return Json.Read(File.ReadAllText(Path.Combine(dir, "p.json")), "p");
                }
                var off = Played("--curve", "off");
                Assert.That(off.curve, Is.False, "--curve off reaches the game");
                Curve.On = false;
                var direct = Matrix.Play("p", new uint[] { 3 }, new[] { "novice" }, 0.5);
                Curve.On = true;
                Assert.That(Json.Text(off.runs), Is.EqualTo(Json.Text(direct.runs)), "and plays the JS ladder");
                var on = Played();
                Assert.That(on.curve, Is.True);
                Assert.That(on.dials, Is.Null);
                Assert.That(Json.Text(on.runs), Is.Not.EqualTo(Json.Text(off.runs)), "the curve changes the run");
                double was = S("start").Tough;
                var dialled = Played("--dial", "start.Tough=3,start.Drops=1");
                Assert.That(dialled.dials, Is.EqualTo(new[] { "start.Tough=3", "start.Drops=1" }), "recorded in the header");
                Assert.That(S("start").Tough, Is.EqualTo(was), "and undone after the play");
                Assert.That(Json.Text(dialled.runs), Is.Not.EqualTo(Json.Text(on.runs)), "a dial changes the run");
                Assert.That(Report.Mismatches(on, dialled), Has.Some.Contains("curve dials differ"));
                Assert.That(Report.Mismatches(off, on), Has.Some.EqualTo("difficulty curves differ: off vs on"));
                Assert.That(Report.Mismatches(on, on), Is.Empty);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void ADialIsAStageFieldAndABadOneIsRefused()
        {
            var (st, f, v) = CurveDials.Parse("middle.Drops=0.15");
            Assert.That(st, Is.SameAs(S("middle")));
            Assert.That(f.Name, Is.EqualTo("Drops"));
            Assert.That(v, Is.EqualTo(0.15));
            Assert.Throws<UsageException>(() => CurveDials.Parse("nowhere.Tough=1"));
            Assert.Throws<UsageException>(() => CurveDials.Parse("middle.Nothing=1"));
            Assert.Throws<UsageException>(() => CurveDials.Parse("middle.First=4"), "a stage's floor is not a dial");
            Assert.Throws<UsageException>(() => CurveDials.Parse("middle.Tough=x"));
            Assert.Throws<UsageException>(() => CurveDials.Parse("middle.Tough"));
            Assert.Throws<UsageException>(() => Cli.Parse(new[] { "play", "--dial", "end.Rate=NaN" }));
            Assert.That(Program.Main(new[] { "play", "--dial", "bad" }), Is.EqualTo(1));
        }
    }
}
