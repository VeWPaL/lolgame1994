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

    /// <summary>
    /// Gameplay options, saved like the volume. The difficulty curve (Depths.Core <c>Curve.On</c>) is read
    /// per floor, so it is applied when a run starts, never mid-run.
    /// </summary>
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

        /// <summary>Call at the start of a run.</summary>
        public void Apply() => Depths.Curve.On = Curve;
    }
}
