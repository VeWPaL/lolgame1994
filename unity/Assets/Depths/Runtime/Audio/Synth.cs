using System;
using System.Collections.Generic;

namespace Depths.Unity.Audio
{
    /// <summary>
    /// A small offline synthesizer: just enough of WebAudio to render the JavaScript game's voices
    /// (src/45-sound.js) into sample buffers. Oscillators with exponential pitch sweeps, the same
    /// deterministic noise buffer, RBJ biquads, and the shared envelope. Pure C#, so it is tested
    /// without an audio device; SoundEngine turns the buffers into AudioClips.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;

        public enum Wave { Sine, Triangle, Square, Sawtooth }
        public enum Filter { None, Lowpass, Bandpass }

        /// <summary>A parameter automated the WebAudio way: set, linear ramp, exponential ramp.</summary>
        public sealed class Param
        {
            readonly List<(int kind, double t, double v)> _ev = new List<(int, double, double)>();
            double _start;
            public Param(double v) { _start = v; }
            public Param Set(double t, double v) { _ev.Add((0, t, v)); return this; }
            public Param Linear(double t, double v) { _ev.Add((1, t, v)); return this; }
            public Param Exp(double t, double v) { _ev.Add((2, t, v)); return this; }

            public double At(double t)
            {
                double v = _start, t0 = 0;
                foreach (var e in _ev)
                {
                    if (t < e.t)
                    {
                        if (e.kind == 0) return v;
                        double f = (t - t0) / Math.Max(1e-9, e.t - t0);
                        if (f < 0) return v;
                        return e.kind == 1 ? v + (e.v - v) * f : v * Math.Pow(e.v / v, f);
                    }
                    v = e.v; t0 = e.t;
                }
                return v;
            }
        }

        public sealed class Osc { public Wave Wave; public Param Freq = new Param(440); }

        /// <summary>One voice, as data. Sources are summed, filtered, then shaped by the envelope.</summary>
        public sealed class Voice
        {
            public string Name = "";
            public double Gain = 1, Jitter, Len;
            public readonly List<Osc> Oscs = new List<Osc>();
            public bool Noise;
            public Filter FilterType;
            public Param FilterFreq = new Param(1000);
            public double Q = 1;
            public double A, D, Sustain, Release, Peak = 1;   // env(g, 0, a, d, sustain, release, peak)
            public double Stop;
        }

        static float[] _noise;
        /// <summary>The JS noise buffer, sample for sample: the same xorshift, the same seed, 1.2s.</summary>
        public static float[] Noise()
        {
            if (_noise != null) return _noise;
            int n = (int)Math.Floor(Rate * 1.2);
            var d = new float[n];
            uint s = 0x9e3779b9;
            for (int i = 0; i < n; i++) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; d[i] = (float)(s / 4294967296.0 * 2 - 1); }
            return _noise = d;
        }

        static double WaveAt(Wave w, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Sine: return Math.Sin(2 * Math.PI * p);
                case Wave.Square: return p < 0.5 ? 1 : -1;
                case Wave.Sawtooth: return 2 * p - 1;
                default: return p < 0.25 ? 4 * p : p < 0.75 ? 2 - 4 * p : 4 * p - 4;   // triangle
            }
        }

        /// <summary>Renders a voice to <paramref name="seconds"/> of mono samples at <see cref="Rate"/>.</summary>
        public static float[] Render(Voice v, double seconds)
        {
            int n = (int)Math.Ceiling(Rate * seconds);
            var outp = new float[n];
            var noise = v.Noise ? Noise() : null;
            var phase = new double[v.Oscs.Count];
            var env = new Param(0).Set(0, 0).Linear(v.A, v.Peak).Linear(v.A + v.D, v.Peak * v.Sustain)
                .Exp(v.A + v.D + v.Release, 0.0001);
            double z1 = 0, z2 = 0, x1 = 0, x2 = 0;   // biquad state
            double lastF0 = double.NaN, b0 = 0, b1 = 0, b2 = 0, a0 = 1, a1 = 0, a2 = 0;   // coefficients, reused while the cutoff holds
            int stop = (int)Math.Ceiling(v.Stop * Rate);
            for (int i = 0; i < n && i < stop; i++)
            {
                double t = (double)i / Rate, s = 0;
                for (int k = 0; k < v.Oscs.Count; k++)
                {
                    s += WaveAt(v.Oscs[k].Wave, phase[k]);
                    phase[k] += v.Oscs[k].Freq.At(t) / Rate;
                }
                if (noise != null) s += i < noise.Length ? noise[i] : 0;
                if (v.FilterType != Filter.None)
                {
                    // RBJ cookbook; recomputed only when the cutoff moves (a sweep), same numbers either way
                    double f0 = Math.Min(v.FilterFreq.At(t), Rate * 0.45);
                    if (f0 != lastF0)
                    {
                        lastF0 = f0;
                        double w0 = 2 * Math.PI * f0 / Rate, cw = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * v.Q);
                        a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha;
                        if (v.FilterType == Filter.Lowpass) { b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2; }
                        else { b0 = alpha; b1 = 0; b2 = -alpha; }
                    }
                    double y = (b0 * s + b1 * x1 + b2 * x2 - a1 * z1 - a2 * z2) / a0;
                    x2 = x1; x1 = s; z2 = z1; z1 = y; s = y;
                }
                outp[i] = (float)(s * env.At(t));
            }
            return outp;
        }

        /// <summary>The same four numbers the JS offline render reports, defined the same way.</summary>
        public static (double peak, double rms, int peakAtMs, int zeroCrossHz) Analyse(float[] d)
        {
            double peak = 0, sum = 0; int at = 0;
            for (int i = 0; i < d.Length; i++) { double a = Math.Abs(d[i]); if (a > peak) { peak = a; at = i; } sum += d[i] * d[i]; }
            int win = Math.Min(d.Length, (int)Math.Floor(Rate * 0.2)), cross = 0;
            for (int i = 1; i < win; i++) if (d[i - 1] < 0 && d[i] >= 0) cross++;
            return (peak, Math.Sqrt(sum / d.Length), (int)Math.Round(at * 1000.0 / Rate), (int)Math.Round(cross / (win / (double)Rate)));
        }
    }
}
