using UnityEngine;

namespace Depths.Unity
{
    /// <summary>How much the screen shakes (Options > Gameplay): Medium is Isaac (only the big moments), High is Gungeon (most
    /// weapons and hits, never silly), Insanity is Nuclear Throne turned up for laughs (everything, the HUD too).</summary>
    public enum ShakeLevel { Off, Medium, High, Insanity }

    /// <summary>Trauma shake: events add trauma (0..1), the offset is the level's reach x trauma^1.5 on smooth noise, and
    /// trauma drains. Its own clock runs only while stepped, so a paused game holds still. Presentation only.</summary>
    public sealed class ScreenShake
    {
        public enum Kick { Thud, Boom, Hurt, Kill, Shot, Hit }

        // trauma per event, by level (rows: Off, Medium, High, Insanity; columns: the Kick order)
        static readonly float[,] Trauma =
        {
            { 0, 0, 0, 0, 0, 0 },
            { 0.75f, 0.6f, 0.45f, 0, 0, 0 },
            { 0.8f, 0.65f, 0.5f, 0.3f, 0.2f, 0.12f },
            { 1f, 1f, 0.9f, 0.6f, 0.45f, 0.35f },
        };
        static readonly float[] ReachPx = { 0, 10, 12, 28 }, TurnDeg = { 0, 0, 0, 4f }, Drain = { 1, 1.6f, 3f, 1.4f };
        public const int MostAFrame = 4;   // kicks of one kind a frame: a Scatter volley is a volley, not eight

        public ShakeLevel Level = ShakeLevel.Medium;
        public float Trauma01 { get; private set; }
        float _clock;

        /// <summary>Insanity shakes the ledger too.</summary>
        public bool HudToo => Level == ShakeLevel.Insanity;

        /// <summary>`times` events of a kind (several in one frame add up, to MostAFrame), each scaled by `weight` (a gun's
        /// shot weighs its cadence: a slow, heavy shot kicks, a beam's stream buzzes).</summary>
        public void Add(Kick k, int times = 1, float weight = 1)
        {
            if (Level == ShakeLevel.Insanity) weight = Mathf.Max(weight, 0.7f);   // Nuclear Throne: every gun is loud
            for (int i = 0; i < Mathf.Min(times, MostAFrame); i++) Trauma01 = Mathf.Min(1, Trauma01 + Trauma[(int)Level, (int)k] * weight);
        }

        public void Step(float seconds)
        {
            Trauma01 = Mathf.Max(0, Trauma01 - Drain[(int)Level] * seconds);
            _clock += seconds;
        }

        public void Clear() => Trauma01 = 0;

        /// <summary>The offset in px and the tilt in degrees now.</summary>
        public (float x, float y, float deg) Offset()
        {
            float a = Trauma01 * Mathf.Sqrt(Trauma01);
            if (a <= 0) return (0, 0, 0);
            float f = _clock * 28;
            float n(float seed) => Mathf.PerlinNoise(seed, f) * 2 - 1;
            return (ReachPx[(int)Level] * a * n(1.3f), ReachPx[(int)Level] * a * n(7.9f), TurnDeg[(int)Level] * a * n(4.2f));
        }

        /// <summary>A shot's weight from its gun's cooldown in seconds: 0.4 s is 1, a beam's stream far less, a cannon more.</summary>
        public static float ShotWeight(double cooldownS) => Mathf.Clamp((float)(cooldownS / 0.4), 0.2f, 1.6f);

        public static ShakeLevel Parse(string s) => s switch
        {
            "off" => ShakeLevel.Off, "high" => ShakeLevel.High, "insanity" => ShakeLevel.Insanity, _ => ShakeLevel.Medium,
        };

        public static string Name(ShakeLevel l) => l.ToString().ToLowerInvariant();
    }
}
