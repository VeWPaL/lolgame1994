using System.Collections.Generic;
using static Depths.Unity.Audio.Synth;

namespace Depths.Unity.Audio
{
    /// <summary>
    /// The JavaScript game's twelve voices (src/45-sound.js VOICES), as data. Same frequencies,
    /// sweeps, filters and envelopes; the tests compare each render with the JS offline render.
    /// WebAudio's lowpass Q is in dB (default 1 dB = 1.122 linear); its bandpass Q is linear.
    /// </summary>
    public static class Voices
    {
        const double LowpassQ = 1.122;

        static Osc O(Wave w, double f0, double f1, double at) =>
            new Osc { Wave = w, Freq = new Param(f0).Set(0, f0).Exp(at, f1) };

        static Voice V(string name, double gain, double jitter, double len, double a, double d, double s, double r)
            => new Voice { Name = name, Gain = gain, Jitter = jitter, Len = len, Stop = len, A = a, D = d, Sustain = s, Release = r, Peak = gain };

        public static readonly IReadOnlyDictionary<string, Voice> All = Build();

        static Dictionary<string, Voice> Build()
        {
            var m = new Dictionary<string, Voice>();
            void Add(Voice v) => m[v.Name] = v;

            var shot = V("shot", 0.5, 35, 0.09, 0.004, 0.02, 0.35, 0.05);
            shot.Peak = 0.25;   // env(..., o.gain*0.5)
            shot.Oscs.Add(O(Wave.Triangle, 880, 220, 0.05));
            shot.FilterType = Filter.Lowpass; shot.FilterFreq = new Param(2600); shot.Q = LowpassQ;
            Add(shot);

            var hit = V("hit", 0.7, 120, 0.15, 0.003, 0.03, 0.25, 0.08);
            hit.Noise = true; hit.FilterType = Filter.Bandpass; hit.FilterFreq = new Param(760); hit.Q = 1.1;
            Add(hit);

            var hurt = V("hurt", 0.5, 0, 0.3, 0.005, 0.06, 0.5, 0.2);
            hurt.Oscs.Add(O(Wave.Sawtooth, 180, 70, 0.22));
            Add(hurt);

            var ph = V("playerHit", 0.75, 60, 0.1, 0.002, 0.02, 0.3, 0.06);
            ph.Noise = true; ph.FilterType = Filter.Bandpass; ph.FilterFreq = new Param(1800); ph.Q = 0.8;
            Add(ph);

            var blink = V("blink", 0.55, 20, 0.18, 0.006, 0.04, 0.5, 0.1);
            blink.Oscs.Add(O(Wave.Sine, 420, 1250, 0.12));
            Add(blink);

            var kill = V("kill", 0.42, 80, 0.25, 0.004, 0.05, 0.4, 0.14);
            kill.Oscs.Add(O(Wave.Triangle, 320, 90, 0.18));
            Add(kill);

            var touch = V("touch", 0.46, 40, 0.3, 0.004, 0.05, 0.5, 0.18);
            touch.Oscs.Add(O(Wave.Square, 130, 55, 0.2));
            touch.FilterType = Filter.Lowpass; touch.FilterFreq = new Param(700); touch.Q = LowpassQ;
            Add(touch);

            var tell = V("tell", 0.16, 15, 0.55, 0.05, 0.3, 0.8, 0.12);
            tell.Oscs.Add(O(Wave.Sine, 660, 990, 0.45));
            Add(tell);

            var boss = V("boss", 0.34, 0, 1.3, 0.12, 0.4, 0.7, 0.6);
            boss.Oscs.Add(O(Wave.Sawtooth, 70, 42, 1.1));
            boss.Oscs.Add(O(Wave.Sine, 35, 28, 1.1));
            boss.FilterType = Filter.Lowpass; boss.FilterFreq = new Param(420); boss.Q = LowpassQ;
            Add(boss);

            var door = V("door", 0.5, 0, 0.4, 0.03, 0.12, 0.5, 0.16);
            door.Noise = true; door.FilterType = Filter.Lowpass; door.Q = LowpassQ;
            door.FilterFreq = new Param(400).Set(0, 400).Exp(0.3, 1800);
            Add(door);

            var pickup = V("pickup", 0.55, 70, 0.14, 0.004, 0.03, 0.5, 0.08);
            pickup.Oscs.Add(O(Wave.Triangle, 740, 1180, 0.09));
            Add(pickup);

            var over = V("over", 0.3, 0, 1.1, 0.05, 0.4, 0.6, 0.45);
            over.Oscs.Add(O(Wave.Sine, 300, 150, 0.9));
            Add(over);
            return m;
        }
    }
}
