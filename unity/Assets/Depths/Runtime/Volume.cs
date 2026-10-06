using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Depths.Unity
{
    public enum Channel { Master, Music, Sfx }

    /// <summary>
    /// The three volume sliders. A slider is linear 0..1 because that is what a player expects to
    /// drag; the mixer works in decibels, so the conversion lives here and is the only one.
    /// </summary>
    public sealed class VolumeSettings
    {
        public const float Default = 0.8f;
        public const float SilentDb = -80f;   // the mixer's floor; log10(0) has no answer

        readonly ISettingsStore _store;
        public event Action<Channel, float> Changed;

        public VolumeSettings(ISettingsStore store) { _store = store; }

        public static string Key(Channel c) => "depths.volume." + c.ToString().ToLowerInvariant();
        /// <summary>The exposed parameter name on the mixer group, e.g. "MasterVolume".</summary>
        public static string Param(Channel c) => c + "Volume";

        public float Get(Channel c) => Mathf.Clamp01(_store.GetFloat(Key(c), Default));

        public void Set(Channel c, float linear)
        {
            linear = Mathf.Clamp01(linear);
            _store.SetFloat(Key(c), linear);
            _store.Save();
            Changed?.Invoke(c, linear);
        }

        /// <summary>20*log10(v), floored: 1 -> 0 dB, 0.5 -> -6 dB, 0 -> silent.</summary>
        public static float ToDecibels(float linear) =>
            linear <= 0.0001f ? SilentDb : Mathf.Max(SilentDb, 20f * Mathf.Log10(linear));

        public void ApplyAll(AudioMixer mixer)
        {
            if (mixer == null) return;
            foreach (Channel c in Enum.GetValues(typeof(Channel)))
                mixer.SetFloat(Param(c), ToDecibels(Get(c)));
        }
    }
}
