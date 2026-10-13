using System;

namespace Depths.Unity.Audio
{
    /// <summary>
    /// The JS game's master compressor (src/45-sound.js: threshold -14 dB, knee 22, ratio 5, attack 4 ms,
    /// release 180 ms) as plain DSP: a soft-knee gain computer in dB, a peak detector smoothed by attack
    /// and release, channels linked, and Chrome's makeup gain so the set sits at the loudness it had in
    /// the browser. Pure, so it is tested without an audio device; MasterBus runs it on the final mix.
    /// </summary>
    public sealed class Compressor
    {
        public double Threshold = -14, Knee = 22, Ratio = 5, Attack = 0.004, Release = 0.18;
        readonly double _rate;
        double _reductionDb;   // current gain reduction, <= 0

        public Compressor(double sampleRate) { _rate = sampleRate; }

        /// <summary>The static curve: output level in dB for a steady input level in dB.</summary>
        public double Curve(double inDb)
        {
            double over = inDb - Threshold;
            if (2 * over < -Knee) return inDb;
            if (2 * Math.Abs(over) <= Knee)
            {
                double k = over + Knee / 2;
                return inDb + (1 / Ratio - 1) * k * k / (2 * Knee);
            }
            return Threshold + over / Ratio;
        }

        /// <summary>Chrome's makeup: (1 / the curve's gain at 0 dB)^0.6, linear.</summary>
        public double Makeup => Math.Pow(1 / Math.Pow(10, Curve(0) / 20), 0.6);

        public double ReductionDb => _reductionDb;

        /// <summary>Compresses interleaved samples in place.</summary>
        public void Process(float[] data, int channels)
        {
            double ca = Math.Exp(-1 / (Attack * _rate)), cr = Math.Exp(-1 / (Release * _rate)), makeup = Makeup;
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                double peak = 0;
                for (int c = 0; c < channels; c++) peak = Math.Max(peak, Math.Abs(data[i + c]));
                double inDb = peak > 1e-6 ? 20 * Math.Log10(peak) : -120;
                double want = Curve(inDb) - inDb;                                   // <= 0
                double coef = want < _reductionDb ? ca : cr;                        // more reduction: attack
                _reductionDb = want + (_reductionDb - want) * coef;
                float g = (float)(Math.Pow(10, _reductionDb / 20) * makeup);
                for (int c = 0; c < channels; c++)
                {
                    float y = data[i + c] * g;
                    data[i + c] = float.IsNaN(y) ? 0 : Math.Max(-1f, Math.Min(1f, y));
                }
            }
        }
    }
}
