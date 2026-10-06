using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Depths.Unity.Audio
{
    /// <summary>
    /// Which source a new sound gets: a free one, else the one that will finish soonest (the JS pool
    /// trims oldest-first for the same reason - a cap that refuses new sounds drops the important ones).
    /// Pure, so the bound is tested without an audio device.
    /// </summary>
    public sealed class VoicePool
    {
        readonly double[] _endsAt;
        public VoicePool(int size) { _endsAt = new double[size]; }
        public int Size => _endsAt.Length;

        public int Take(double now, double length)
        {
            int best = 0;
            for (int i = 0; i < _endsAt.Length; i++)
            {
                if (_endsAt[i] <= now) { best = i; break; }
                if (_endsAt[i] < _endsAt[best]) best = i;
            }
            _endsAt[best] = now + length;
            return best;
        }

        public int Busy(double now)
        {
            int n = 0;
            foreach (var e in _endsAt) if (e > now) n++;
            return n;
        }
    }

    /// <summary>
    /// The one call the game makes: <c>SoundEngine.Play("hit", pan)</c>. Voices are rendered once at
    /// start by <see cref="Synth"/> and played through a fixed pool of AudioSources on the mixer's Sfx
    /// group. Pitch jitter is per play, from a local stream, so sound never touches the game's RNG.
    /// </summary>
    public sealed class SoundEngine : MonoBehaviour
    {
        public const int MaxVoices = 12;   // the JS cap is 48 nodes, about 8-16 sounds
        public AudioMixerGroup sfx;
        public static SoundEngine Instance { get; private set; }

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        AudioSource[] _src;
        VoicePool _pool;
        uint _rs = 0x2545f491;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            foreach (var v in Voices.All.Values)
            {
                var data = Synth.Render(v, v.Len + 0.02);
                var clip = AudioClip.Create(v.Name, data.Length, 1, Synth.Rate, false);
                clip.SetData(data, 0);
                _clips[v.Name] = clip;
            }
            _src = new AudioSource[MaxVoices];
            for (int i = 0; i < MaxVoices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.outputAudioMixerGroup = sfx;
                _src[i] = s;
            }
            _pool = new VoicePool(MaxVoices);
        }

        float Rand() { _rs ^= _rs << 13; _rs ^= _rs >> 17; _rs ^= _rs << 5; return _rs / 4294967296f; }

        /// <summary>Plays a voice. pan -1..1, gain multiplies the voice's own. False if there is no such voice.</summary>
        public static bool Play(string name, float pan = 0, float gain = 1)
        {
            var e = Instance;
            if (e == null || !e._clips.TryGetValue(name, out var clip)) return false;
            var v = Voices.All[name];
            float cents = (float)v.Jitter * (e.Rand() * 2 - 1);
            var s = e._src[e._pool.Take(Time.unscaledTimeAsDouble, clip.length)];
            s.clip = clip;
            s.pitch = Mathf.Pow(2f, cents / 1200f);
            s.panStereo = Mathf.Clamp(pan, -1f, 1f);
            s.volume = Mathf.Clamp01(gain);
            s.Play();
            return true;
        }
    }
}
