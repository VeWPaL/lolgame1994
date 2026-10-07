using System;
using static Depths.Unity.Audio.Synth;

namespace Depths.Unity.Audio
{
    /// <summary>
    /// The music, rendered offline the way the voices are: one seamless 8-bar loop per area in two
    /// layers that play in sync. CALM is a drone, a slow pad progression and sparse bells; FIGHT adds a
    /// pulse bass, a kick and ticks, faded in while a room is live. Placeholder music in the game's own
    /// synth timbres (the JS game had none). Pure, so it is tested without an audio device.
    /// </summary>
    public static class Score
    {
        public const double Bpm = 76;
        public const int Bars = 8, Beats = 4;
        public static double Beat => 60 / Bpm;
        public static double LoopSeconds => Bars * Beats * Beat;
        public static int LoopSamples => (int)Math.Round(LoopSeconds * Rate);

        // per area: root (MIDI) and a minor-family scale, darker as the climb goes on
        static readonly (int root, int[] scale)[] Keys =
        {
            (50, new[] { 0, 2, 3, 5, 7, 8, 10 }),   // Area1: D aeolian
            (47, new[] { 0, 2, 3, 5, 7, 9, 10 }),   // Area2: B dorian
            (45, new[] { 0, 1, 3, 5, 7, 8, 10 }),   // Area3: A phrygian
            (43, new[] { 0, 1, 3, 5, 6, 8, 10 }),   // Final: G locrian
        };
        // scale degrees of each two-bar chord: i, VI, iv, v
        static readonly int[] Progression = { 0, 5, 3, 4 };

        static double Hz(double midi) => 440 * Math.Pow(2, (midi - 69) / 12);

        /// <summary>The area's key centre (the pad register's root), in Hz.</summary>
        public static double RootHz(Depths.Area area) => Hz(Keys[Math.Min((int)area, Keys.Length - 1)].root);

        static int Degree(int area, int deg)
        {
            var (root, scale) = Keys[area];
            int oct = deg >= 0 ? deg / scale.Length : (deg - scale.Length + 1) / scale.Length;
            return root + scale[deg - oct * scale.Length] + 12 * oct;
        }

        /// <summary>The two layers of an area's loop, each LoopSamples long and seamless end to start.</summary>
        public static (float[] calm, float[] fight) Render(Depths.Area area, uint seed = 1)
        {
            int a = Math.Min((int)area, Keys.Length - 1), n = LoopSamples;
            var calm = new float[n];
            var fight = new float[n];
            uint rs = seed * 2654435761u + (uint)a * 40503u + 1;
            double Rand() { rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5; return rs / 4294967296.0; }

            Drone(calm, Hz(Degree(a, 0) - 24), 0.07);
            Drone(calm, Hz(Degree(a, 4) - 24), 0.04);

            for (int c = 0; c < Progression.Length; c++)
            {
                double t0 = c * 2 * Beats * Beat;
                // a triad on the chord's degree, one voice (three oscillators, one filter: a third of the
                // render); it releases as the next chord swells in: attack + decay + hold = two bars
                var pad = Note("pad", Wave.Triangle, Hz(Degree(a, Progression[c]) - 12), 0.05, 1.4, 1.2, 0.8, 2.2, 2 * Beats * Beat - 2.6);
                foreach (int k in new[] { 2, 4 }) pad.Oscs.Add(new Osc { Wave = Wave.Triangle, Freq = new Param(Hz(Degree(a, Progression[c] + k) - 12)) });
                pad.FilterType = Filter.Lowpass; pad.FilterFreq = new Param(900); pad.Q = 0.7;
                Mix(calm, pad, t0);
                for (int b = 0; b < 2 * Beats; b++)
                    if (Rand() < 0.38)   // sparse bells on the chord, two octaves up
                    {
                        int deg = Progression[c] + new[] { 0, 2, 4, 7 }[(int)(Rand() * 4)];
                        Mix(calm, Note("bell", Wave.Sine, Hz(Degree(a, deg) + 12), 0.06, 0.004, 0.08, 0.3, 0.9, 0.05), t0 + b * Beat);
                    }

                for (int e = 0; e < 4 * Beats; e++)   // eighths
                {
                    double t = t0 + e * Beat / 2;
                    bool accent = e % 4 == 0 || Rand() < 0.2;
                    var bass = Note("bass", Wave.Square, Hz(Degree(a, Progression[c]) - 24), accent ? 0.12 : 0.07, 0.004, 0.06, 0.4, 0.12, 0.08);
                    bass.FilterType = Filter.Lowpass; bass.FilterFreq = new Param(520); bass.Q = 1.1;
                    Mix(fight, bass, t);
                    if (e % 2 == 1)
                    {
                        var tick = Note("tick", Wave.Sine, 1, 0.035, 0.001, 0.015, 0.2, 0.03, 0.01);
                        tick.Oscs.Clear(); tick.Noise = true; tick.FilterType = Filter.Bandpass; tick.FilterFreq = new Param(6500); tick.Q = 1.2;
                        Mix(fight, tick, t);
                    }
                    if (e % 4 == 0)
                    {
                        var kick = Note("kick", Wave.Sine, 120, 0.22, 0.002, 0.08, 0.3, 0.16, 0.05);
                        kick.Oscs[0].Freq = new Param(120).Set(0, 120).Exp(0.18, 42);
                        Mix(fight, kick, t);
                    }
                }
            }
            return (calm, fight);
        }

        static Voice Note(string name, Wave w, double hz, double peak, double a, double d, double sustain, double release, double hold)
        {
            var v = new Voice { Name = name, A = a, D = d + hold, Sustain = sustain, Release = release, Peak = peak };
            v.Stop = v.Len = a + d + hold + release;
            v.Oscs.Add(new Osc { Wave = w, Freq = new Param(hz) });
            return v;
        }

        // adds a rendered note at t, wrapping its tail to the start: what rings past the end of the
        // loop is heard at its beginning, which is what makes the loop seamless
        static void Mix(float[] into, Voice v, double t)
        {
            var d = Synth.Render(v, v.Len);
            int at = (int)Math.Round(t * Rate), n = into.Length;
            for (int i = 0; i < d.Length; i++) into[(at + i) % n] += d[i];
        }

        // a held tone with a slow swell, tuned to a whole number of cycles per loop so it never clicks
        static void Drone(float[] into, double hz, double amp)
        {
            int n = into.Length;
            double cycles = Math.Max(1, Math.Round(hz * LoopSeconds)), swell = 2;   // 2 swells a loop
            for (int i = 0; i < n; i++)
            {
                double p = (double)i / n;
                double s = Math.Sin(2 * Math.PI * cycles * p) * 0.7 + Math.Sin(2 * Math.PI * 2 * cycles * p) * 0.3;
                into[i] += (float)(amp * s * (0.75 + 0.25 * Math.Sin(2 * Math.PI * swell * p)));
            }
        }
    }
}
