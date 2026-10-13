using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace Depths.Unity.Audio
{
    /// <summary>
    /// Plays the Score: two looping sources started on the same DSP tick so the layers stay locked.
    /// The game calls <c>MusicEngine.SetArea(area)</c> and <c>MusicEngine.SetFight(live)</c> every frame;
    /// a new area renders on a worker thread and crossfades in, the fight layer fades with the room.
    /// </summary>
    public sealed class MusicEngine : MonoBehaviour
    {
        public const float CalmLevel = 0.8f, FightIn = 1 / 1.2f, FightOut = 1 / 3f, SwapFade = 1 / 0.7f;

        public AudioMixerGroup music;
        public static MusicEngine Instance { get; private set; }

        AudioSource _calm, _fight;
        readonly Dictionary<Area, (AudioClip calm, AudioClip fight)> _cache = new Dictionary<Area, (AudioClip, AudioClip)>();
        Area _want = Area.Area1;
        Area? _playing;
        Task<(float[] calm, float[] fight)> _job;
        Area _jobArea;
        float _fightLevel, _master;
        bool _fightOn, _swapping;

        void Awake() { Instance = this; }

        // Start, not Awake: AddComponent runs Awake before the owner can assign the mixer group
        void Start() { _calm = Source(); _fight = Source(); }

        AudioSource Source()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = true; s.volume = 0; s.outputAudioMixerGroup = music;
            return s;
        }

        /// <summary>For the player checks: the loop is running, and how far the fight layer is up (0..1).</summary>
        public bool IsPlaying => _calm != null && _calm.isPlaying;
        public float FightLevel => _fightLevel;
        public Area? Playing => _playing;

        public static void SetArea(Area a) { if (Instance != null) Instance._want = a; }
        public static void SetFight(bool live) { if (Instance != null) Instance._fightOn = live; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // the wanted area first, then the rest in the background, so a floor change never waits
            if (_job == null && NextToRender() is Area next) { _jobArea = next; _job = Task.Run(() => Score.Render(next)); }
            if (_job != null && _job.IsCompleted)
            {
                if (_job.Status == TaskStatus.RanToCompletion)
                    _cache[_jobArea] = (Clip("music-calm-" + _jobArea, _job.Result.calm), Clip("music-fight-" + _jobArea, _job.Result.fight));
                else Debug.LogWarning("[Depths] music render failed: " + _job.Exception?.GetBaseException().Message);
                _job = null;
            }

            if (_playing != _want && _cache.ContainsKey(_want)) _swapping = true;
            if (_swapping)
            {
                _master = Mathf.MoveTowards(_master, 0, SwapFade * dt);
                if (_master <= 0 || _playing == null)
                {
                    var (c, f) = _cache[_want];
                    _calm.clip = c; _fight.clip = f;
                    double at = AudioSettings.dspTime + 0.1;   // both on one DSP tick: the layers never drift
                    _calm.PlayScheduled(at); _fight.PlayScheduled(at);
                    _playing = _want; _swapping = false;
                }
            }
            else _master = Mathf.MoveTowards(_master, 1, SwapFade * dt);

            _fightLevel = Mathf.MoveTowards(_fightLevel, _fightOn ? 1 : 0, (_fightOn ? FightIn : FightOut) * dt);
            _calm.volume = CalmLevel * _master;
            _fight.volume = _fightLevel * _master;
        }

        Area? NextToRender()
        {
            if (!_cache.ContainsKey(_want)) return _want;
            foreach (Area a in System.Enum.GetValues(typeof(Area))) if (!_cache.ContainsKey(a)) return a;
            return null;
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
