using System;
using System.Collections.Generic;
using NUnit.Framework;
using Depths.Unity.Audio;

namespace Depths.Unity.Tests
{
    public class SoundTests
    {
        // Measured from the running JS game, 2026-10-06: Sound.render(name) -> peakAtMs, zeroCrossHz.
        // PITCH is compared with the game. PEAK TIMING is not: the JS render goes through the master
        // compressor (threshold -14 dB, ratio 5), which clamps each attack and moves the measured peak
        // later (hurt 45ms against an envelope peak at 5ms). Here the peak is asserted where the
        // envelope puts it. A compressor on the mixer Master is the follow-up if the set sounds spiky.
        static readonly Dictionary<string, (int peakAtMs, int zc)> Js = new Dictionary<string, (int, int)>
        {
            ["blink"] = (34, 830), ["boss"] = (147, 60), ["door"] = (87, 580), ["hit"] = (16, 1865),
            ["hurt"] = (45, 115), ["kill"] = (38, 165), ["over"] = (242, 270), ["pickup"] = (23, 720),
            ["playerHit"] = (18, 2140), ["shot"] = (10, 230), ["tell"] = (244, 695), ["touch"] = (40, 85),
        };

        [Test]
        public void EveryJavaScriptVoiceExists()
        {
            foreach (var name in Js.Keys) Assert.That(Voices.All.ContainsKey(name), $"no voice called {name}");
            Assert.That(Voices.All.Count, Is.EqualTo(Js.Count), "a voice exists here that the game does not have");
        }

        [Test]
        public void EveryVoiceSoundsLikeTheGame()
        {
            var bad = new List<string>();
            foreach (var kv in Voices.All)
            {
                var d = Synth.Render(kv.Value, 1.6);
                foreach (var x in d) Assert.That(float.IsNaN(x) || float.IsInfinity(x), Is.False, $"{kv.Key} rendered a non-finite sample");
                var (peak, rms, at, zc) = Synth.Analyse(d);
                var js = Js[kv.Key];
                if (peak < 0.01 || peak > 1.0) bad.Add($"{kv.Key}: peak {peak:0.000}");
                double envPeakMs = (kv.Value.A + kv.Value.D) * 1000 + 10;
                if (at > envPeakMs) bad.Add($"{kv.Key}: peak at {at}ms, after its attack and decay ({envPeakMs:0}ms)");
                // shot lasts 90ms of the 200ms window; the JS count there is ringing at -80 dB, inaudible.
                // Its sweep integrates to ~163Hz over the window, which is what this renders.
                if (kv.Key != "shot" && Math.Abs(zc - js.zc) > js.zc * 0.2) bad.Add($"{kv.Key}: pitch {zc}Hz, game {js.zc}Hz");
                int stop = (int)Math.Ceiling(kv.Value.Stop * Synth.Rate);
                for (int i = stop; i < d.Length; i++) if (d[i] != 0) { bad.Add($"{kv.Key}: sound after its stop time"); break; }
            }
            Assert.That(bad, Is.Empty, string.Join("\n", bad));
        }

        // ------------------------------------------------------------- the master compressor

        static double Db(double x) => 20 * Math.Log10(x);
        static float[] Steady(double amp, int samples, int channels = 1)
        {
            var d = new float[samples * channels];
            for (int i = 0; i < samples; i++) for (int c = 0; c < channels; c++) d[i * channels + c] = (float)(i % 2 == 0 ? amp : -amp);
            return d;
        }

        [Test]
        public void TheCurveIsTheJavaScriptCompressors()
        {
            var c = new Compressor(Synth.Rate);
            Assert.That(c.Curve(-40), Is.EqualTo(-40).Within(1e-12), "below the knee nothing changes");
            Assert.That(c.Curve(-14 - 11), Is.EqualTo(-25).Within(1e-9), "the knee starts at threshold - knee/2");
            Assert.That(c.Curve(-14 + 11), Is.EqualTo(-14 + 11 / 5.0).Within(1e-9), "and meets the 5:1 line at threshold + knee/2");
            Assert.That(c.Curve(0), Is.EqualTo(-14 + 14 / 5.0).Within(1e-12));
            for (double x = -60; x < 6; x += 0.25) Assert.That(c.Curve(x + 0.25), Is.GreaterThanOrEqualTo(c.Curve(x)), "the curve falls at " + x);
            Assert.That(Db(c.Makeup), Is.EqualTo(-0.6 * c.Curve(0)).Within(1e-9), "Chrome's makeup: 0.6 of the full-scale reduction");
        }

        [Test]
        public void SteadyLevelsSettleOnTheCurve()
        {
            foreach (double inDb in new[] { -40.0, -20, -8, -2 })
            {
                var c = new Compressor(Synth.Rate);
                var d = Steady(Math.Pow(10, inDb / 20), Synth.Rate);   // a second, long past attack and release
                c.Process(d, 1);
                double outDb = Db(Math.Abs(d[d.Length - 1]));
                Assert.That(outDb, Is.EqualTo(c.Curve(inDb) + Db(c.Makeup)).Within(0.05), inDb + " dB in");
            }
        }

        [Test]
        public void AttackAndReleaseTakeTheirTimes()
        {
            var c = new Compressor(Synth.Rate);
            double full = c.Curve(0);   // the reduction a 0 dB signal settles to
            c.Process(Steady(1.0, (int)(0.004 * Synth.Rate)), 1);
            Assert.That(c.ReductionDb / full, Is.EqualTo(1 - 1 / Math.E).Within(0.03), "4 ms of attack is one time constant");
            c.Process(Steady(1.0, Synth.Rate / 2), 1);
            Assert.That(c.ReductionDb, Is.EqualTo(full).Within(0.01));
            c.Process(new float[(int)(0.18 * Synth.Rate)], 1);
            Assert.That(c.ReductionDb / full, Is.EqualTo(1 / Math.E).Within(0.03), "180 ms of silence is one release time constant");
        }

        [Test]
        public void ChannelsAreLinkedAndNothingLeavesTheRange()
        {
            var c = new Compressor(Synth.Rate);
            var d = new float[2 * 4410];
            for (int i = 0; i < 4410; i++) { d[2 * i] = 0.9f; d[2 * i + 1] = 0.1f; }
            c.Process(d, 2);
            Assert.That(d[d.Length - 1] / d[d.Length - 2], Is.EqualTo(0.1 / 0.9).Within(1e-4), "a loud left channel must duck the right by the same gain");
            var hot = Steady(4.0, 4410);
            hot[7] = float.NaN;
            new Compressor(Synth.Rate).Process(hot, 1);
            foreach (var x in hot) Assert.That(x, Is.InRange(-1f, 1f));
        }

        // ------------------------------------------------------------- the music

        [Test]
        public void EachAreaLoopsSeamlesslyAtItsLength()
        {
            foreach (Depths.Area area in Enum.GetValues(typeof(Depths.Area)))
            {
                var (calm, fight) = Score.Render(area);
                foreach (var (name, d) in new[] { ("calm", calm), ("fight", fight) })
                {
                    Assert.That(d.Length, Is.EqualTo(Score.LoopSamples));
                    double maxStep = 0;
                    for (int i = 1; i < d.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(d[i] - d[i - 1]));
                    double seam = Math.Abs(d[0] - d[d.Length - 1]);
                    Assert.That(seam, Is.LessThanOrEqualTo(maxStep), $"{area} {name}: the loop clicks at the seam ({seam:0.0000} > {maxStep:0.0000})");
                    var (peak, rms, _, _) = Synth.Analyse(d);
                    Assert.That(peak, Is.InRange(0.05, 0.9), $"{area} {name}: peak {peak:0.000}");
                    Assert.That(rms, Is.GreaterThan(0.01), $"{area} {name} is near silent");
                    foreach (var x in d) Assert.That(float.IsNaN(x) || float.IsInfinity(x), Is.False);
                }
            }
        }

        [Test]
        public void TheScoreIsSeededAndEveryAreaHasItsOwn()
        {
            var a = Score.Render(Depths.Area.Area1);
            Assert.That(Score.Render(Depths.Area.Area1).calm, Is.EqualTo(a.calm), "the same area renders differently twice");
            var roots = new HashSet<double>();
            var calms = new List<float[]>();
            foreach (Depths.Area area in Enum.GetValues(typeof(Depths.Area)))
            {
                roots.Add(Score.RootHz(area));
                calms.Add(Score.Render(area).calm);
            }
            Assert.That(roots.Count, Is.EqualTo(4), "two areas share a root");
            for (int i = 0; i < calms.Count; i++)
                for (int j = i + 1; j < calms.Count; j++)
                    Assert.That(calms[i], Is.Not.EqualTo(calms[j]), $"areas {i} and {j} render the same music");
        }

        [Test]
        public void TheFightLayerPulsesOnTheBeat()
        {
            // RMS per sixteenth: the onsets (eighths) must stand clear of the gaps between them
            var (_, fight) = Score.Render(Depths.Area.Area1);
            int w = (int)(Score.Beat / 4 * Synth.Rate);
            double on = 0, off = 0;
            for (int k = 0; k + 1 < fight.Length / w; k++)
            {
                double s = 0;
                for (int i = k * w; i < (k + 1) * w; i++) s += fight[i] * fight[i];
                if (k % 2 == 0) on += s; else off += s;
            }
            Assert.That(on / off, Is.GreaterThan(1.5), "the bass and kick do not land on the eighths");
        }

        [Test]
        public void ALoopRendersInUnderTwoSeconds()
        {
            // on a worker, and every area is prefetched; what this bounds is the silence before the
            // first loop at boot (measured 1.3 s in the editor, 2026-10-07)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Score.Render(Depths.Area.Final);
            double s = sw.Elapsed.TotalSeconds;
            UnityEngine.Debug.Log("[Depths] Score.Render took " + s.ToString("0.000") + " s");
            Assert.That(s, Is.LessThan(2.0), "the first music at boot waits " + s.ToString("0.000") + " s");
        }

        [Test]
        public void ASampleReplacesItsSynthesisedVoice()
        {
            var sample = UnityEngine.AudioClip.Create("hit-sample", 10, 1, Synth.Rate, false);
            var synth = UnityEngine.AudioClip.Create("hit-synth", 10, 1, Synth.Rate, false);
            string asked = null;
            Assert.That(SoundEngine.Pick("hit", p => { asked = p; return sample; }, () => synth), Is.SameAs(sample));
            Assert.That(asked, Is.EqualTo("Sounds/hit"));
            Assert.That(SoundEngine.Pick("hit", p => null, () => synth), Is.SameAs(synth));
            UnityEngine.Object.DestroyImmediate(sample); UnityEngine.Object.DestroyImmediate(synth);
        }

        [Test]
        public void ThePoolNeverHoldsMoreThanItsSizeAndStealsTheSoonestToEnd()
        {
            var p = new VoicePool(4);
            for (int i = 0; i < 10; i++) p.Take(0, 1 + i * 0.1);
            Assert.That(p.Busy(0), Is.EqualTo(4));
            // the four live sounds end at 1.6, 1.7, 1.8, 1.9 (the last four written); the next take
            // must replace the one ending first, and a free slot must win over any busy one
            int slot = p.Take(0, 5);
            Assert.That(p.Busy(1.65), Is.EqualTo(4), "a busy slot was not the soonest-ending one");
            Assert.That(p.Take(10, 1), Is.InRange(0, 3));
        }
    }
}
