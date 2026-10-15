using System.Linq;
using UnityEngine;

namespace Depths.Unity
{
    /// <summary>Where settings live. An interface so the tests can use a dictionary instead of the registry.</summary>
    public interface ISettingsStore
    {
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        void Save();
    }

    public sealed class PlayerPrefsStore : ISettingsStore
    {
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>Settings that live only as long as the process (the screenshot mode's fixtures).</summary>
    public sealed class MemorySettingsStore : ISettingsStore
    {
        readonly System.Collections.Generic.Dictionary<string, string> _s = new System.Collections.Generic.Dictionary<string, string>();
        readonly System.Collections.Generic.Dictionary<string, float> _f = new System.Collections.Generic.Dictionary<string, float>();
        public string GetString(string key, string fallback) => _s.TryGetValue(key, out var v) ? v : fallback;
        public void SetString(string key, string value) => _s[key] = value;
        public float GetFloat(string key, float fallback) => _f.TryGetValue(key, out var v) ? v : fallback;
        public void SetFloat(string key, float value) => _f[key] = value;
        public void Save() { }
    }

    /// <summary>Gameplay options, saved like the volume. [h:Settings-1]</summary>
    public sealed class GameplaySettings
    {
        public const string CurveKey = "depths.gameplay.curve";
        readonly ISettingsStore _store;

        public GameplaySettings(ISettingsStore store) { _store = store; }

        /// <summary>Off until the player turns it on (the curve is not signed off yet; STATUS).</summary>
        public bool Curve
        {
            get => _store.GetString(CurveKey, "off") == "on";
            set { _store.SetString(CurveKey, value ? "on" : "off"); _store.Save(); }
        }

        public const string ShakeKey = "depths.gameplay.shake";

        /// <summary>Screen shake, Off to Insanity (ScreenShake). Medium until chosen; an old "on" reads as Medium.</summary>
        public ShakeLevel Shake
        {
            get => ScreenShake.Parse(_store.GetString(ShakeKey, "medium"));
            set { _store.SetString(ShakeKey, ScreenShake.Name(value)); _store.Save(); }
        }

        /// <summary>Call at the start of a run.</summary>
        public void Apply() => Depths.Curve.On = Curve;
    }

    /// <summary>The frame rate, apart from the game's speed: the simulation always steps at Balance.TickHz and the screen... [h:Settings-2]</summary>
    public sealed class DisplaySettings
    {
        public const string FrameKey = "depths.display.frames", CustomKey = "depths.display.custom";
        public const string CustomChoice = "Custom";
        public static readonly string[] Choices = { "VSync", "30", "60", "120", "144", "165", "240", CustomChoice, "Uncapped" };
        public const int CustomMin = 20, CustomMax = 500, CustomDefault = 90;
        readonly ISettingsStore _store;

        public DisplaySettings(ISettingsStore store) { _store = store; }

        /// <summary>One of <see cref="Choices"/>; anything else saved (an old or hand-edited value) reads as VSync.</summary>
        public string Frames
        {
            get { var v = _store.GetString(FrameKey, "VSync"); return System.Array.IndexOf(Choices, v) >= 0 ? v : "VSync"; }
            set { _store.SetString(FrameKey, System.Array.IndexOf(Choices, value) >= 0 ? value : "VSync"); _store.Save(); }
        }

        /// <summary>The player's own cap for the Custom choice, kept in CustomMin..CustomMax.</summary>
        public int Custom
        {
            get => ParseCustom(_store.GetString(CustomKey, CustomDefault.ToString())) ?? CustomDefault;
            set { _store.SetString(CustomKey, Mathf.Clamp(value, CustomMin, CustomMax).ToString()); _store.Save(); }
        }

        /// <summary>What a typed cap means: its digits as a number, clamped to the range; null if it has no digits.</summary>
        public static int? ParseCustom(string typed)
        {
            var digits = new string((typed ?? "").Where(c => c >= '0' && c <= '9').Take(6).ToArray());
            return digits.Length == 0 ? (int?)null : Mathf.Clamp(int.Parse(digits), CustomMin, CustomMax);
        }

        /// <summary>The (vSyncCount, targetFrameRate) a choice means; -1 is Unity's "as fast as the platform allows".</summary>
        public static (int vsync, int target) Mode(string choice, int custom = CustomDefault) =>
            choice == "VSync" ? (1, -1) : choice == "Uncapped" ? (0, -1) : choice == CustomChoice ? (0, Mathf.Clamp(custom, CustomMin, CustomMax))
            : int.TryParse(choice, out var n) ? (0, n) : (1, -1);

        /// <summary>Takes effect at once: called at startup and when the option changes.</summary>
        public void Apply()
        {
            var (vsync, target) = Mode(Frames, Custom);
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = target;
        }
    }
}
