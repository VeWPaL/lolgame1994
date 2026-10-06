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
}
