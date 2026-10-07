using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Depths.Unity
{
    /// <summary>
    /// A built player started with <c>-depthsShot path.png [-depthsView menu|controls|audio|game|hearts]</c>
    /// opens that view, captures one frame and quits. It is how the game is checked by eye from a script.
    /// <c>game</c> also measures the tick rate in real time and <c>hearts</c> samples the health row's
    /// pixels; each logs a "[Depths] check" line, and the player exits 1 if a check fails.
    /// </summary>
    public sealed class ScreenshotMode : MonoBehaviour
    {
        string _path, _view;
        bool _failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            string path = Arg(args, "-depthsShot");
            if (path == null) return;
            var go = new GameObject("ScreenshotMode");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<ScreenshotMode>();
            s._path = path;
            s._view = Arg(args, "-depthsView") ?? "menu";
        }

        static string Arg(string[] a, string name)
        {
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        void Check(bool ok, string what)
        {
            if (!ok) _failed = true;
            Debug.Log("[Depths] check " + (ok ? "PASS " : "FAIL ") + what);
        }

        IEnumerator Start()
        {
            for (int i = 0; i < 10; i++) yield return null;
            bool inGame = _view == "game" || _view == "boss" || _view == "sandbox" || _view == "hearts";
            if (inGame) SceneManager.LoadScene("Game");
            else
            {
                var m = FindAnyObjectByType<MainMenuController>();
                if (m != null) m.ShowView(_view);
            }
            // the game views play for a while in real time (frame counts depend on the frame rate)
            if (_view == "game") yield return MeasureTickRate(5f);
            else if (_view == "boss") yield return new WaitForSecondsRealtime(4f);
            else if (_view == "sandbox") yield return new WaitForSecondsRealtime(1.5f);
            else if (_view == "hearts") { yield return CheckHearts(); yield break; }
            else for (int i = 0; i < 20; i++) yield return null;
            ScreenCapture.CaptureScreenshot(_path);
            for (int i = 0; i < 10; i++) yield return null;
            Application.Quit(_failed ? 1 : 0);
        }

        // Ticks stepped against wall-clock time, after a second to settle (the scene load is a long frame).
        IEnumerator MeasureTickRate(float seconds)
        {
            yield return new WaitForSecondsRealtime(1f);
            var view = FindAnyObjectByType<GameView>();
            if (view == null) { Check(false, "tick rate: no GameView in the Game scene"); yield break; }
            long t0 = view.Ticks, sim0 = view.Run.ticks;
            int f0 = Time.frameCount;
            float s0 = Time.realtimeSinceStartup;
            // the final mix, sampled while the demo shoots: a missing listener or a dead bus is silence
            var mix = new float[1024];
            float loudest = 0;
            while (Time.realtimeSinceStartup - s0 < seconds - 1f)
            {
                AudioListener.GetOutputData(mix, 0);
                foreach (var x in mix) loudest = Mathf.Max(loudest, Mathf.Abs(x));
                yield return null;
            }
            Check(FindAnyObjectByType<AudioListener>() != null, "the scene has an AudioListener");
            Check(loudest > 0.01f, "the game is audible: loudest sample of the mix " + loudest.ToString("0.000") + " while the demo fights");
            var music = Depths.Unity.Audio.MusicEngine.Instance;
            Check(music != null && music.IsPlaying && music.Playing == AreaRules.AreaForFloor(view.Run.floor),
                  "the music plays the floor's area (" + (music != null ? music.Playing?.ToString() ?? "nothing" : "no engine") + ")");
            Check(music != null && (view.Run.enemies.Count == 0 || music.FightLevel > 0.5f),
                  "the fight layer is up while bodies live: " + (music != null ? music.FightLevel.ToString("0.00") : "-") + " with " + view.Run.enemies.Count + " bodies");
            double s = Time.realtimeSinceStartup - s0;
            long clock = view.Ticks - t0, sim = view.Run.ticks - sim0;   // the run's own count: the loop must really step it
            double hz = sim / s;
            Check(Balance.TickHz == Balance.GameHz, "Balance.TickHz is " + Balance.TickHz + " in the player (want " + Balance.GameHz + ")");
            Check(view.Run.state == "playing", "the demo run is still playing (state " + view.Run.state + "), so every clock tick reached the sim");
            Check(sim == clock, "the sim stepped " + sim + " ticks for the clock's " + clock);
            Check(Math.Abs(hz - Balance.TickHz) <= 0.02 * Balance.TickHz,
                  "tick rate " + hz.ToString("0.00") + " Hz over " + s.ToString("0.00") + " s at " +
                  ((Time.frameCount - f0) / s).ToString("0") + " fps (want " + Balance.TickHz + " +-2%)");
        }

        // The specification, not the layout code: 5 of 8 HP, regen 1 of 4, armour 3 draws red full,
        // full, half, empty; rose half, empty; gray full, half - left to right from the first heart.
        static readonly (HeartKind kind, HeartFill fill)[] Expected =
        {
            (HeartKind.Red, HeartFill.Full), (HeartKind.Red, HeartFill.Full), (HeartKind.Red, HeartFill.Half), (HeartKind.Red, HeartFill.Empty),
            (HeartKind.Regen, HeartFill.Half), (HeartKind.Regen, HeartFill.Empty), (HeartKind.Armour, HeartFill.Full), (HeartKind.Armour, HeartFill.Half),
        };

        IEnumerator CheckHearts()
        {
            for (int i = 0; i < 10; i++) yield return null;
            var view = FindAnyObjectByType<GameView>();
            if (view == null || view.Run == null) { Check(false, "hearts: no running GameView"); Application.Quit(1); yield break; }
            view.Frozen = true;   // the fixture holds: no tick can heal, hurt or refill it
            var p = view.Run.player;
            p.maxHp = 8; p.hp = 5; p.regenHeartMax = 4; p.regenHeart = 1; p.armor = 3;
            for (int i = 0; i < 5; i++) yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(_path, tex.EncodeToPNG());

            float scale = tex.width / 1280f;   // the panel's reference width; the player opens at 16:9
            Check(Math.Abs(tex.height - 720f * scale) < 1.5f, "hearts: screen is 16:9 (" + tex.width + "x" + tex.height + ")");
            for (int i = 0; i < Expected.Length; i++)
            {
                var (kind, fill) = Expected[i];
                var slot = new HeartSlot(RoomPainter.FirstHeart + new Vector2(i * HeartRow.Step, 0), kind, fill);
                Color lit = HeartRow.Lit(kind);
                Color left = Sample(tex, HeartRow.LeftLobe(slot), scale), right = Sample(tex, HeartRow.RightLobe(slot), scale);
                string at = "heart " + (i + 1) + " " + kind + " " + fill;
                Lobe(at + ": left lobe ", left, fill != HeartFill.Empty, kind);
                Lobe(at + ": right lobe ", right, fill == HeartFill.Full, kind);
            }
            // nothing after the last heart: the row is exactly as long as the fixture
            var past = new HeartSlot(RoomPainter.FirstHeart + new Vector2(Expected.Length * HeartRow.Step, 0), HeartKind.Armour, HeartFill.Full);
            Color after = Sample(tex, HeartRow.LeftLobe(past), scale);
            bool anyHeart = Near(after, HeartRow.Red) || Near(after, HeartRow.Regen) || Near(after, HeartRow.Armour) ||
                            Near(after, HeartRow.RedEmpty) || Near(after, HeartRow.RegenEmpty);
            Check(!anyHeart, "no heart past the " + Expected.Length + "th: " + Hex(after));
            Destroy(tex);
            for (int i = 0; i < 3; i++) yield return null;
            Application.Quit(_failed ? 1 : 0);
        }

        // a lit lobe is the kind's colour; an unlit one is its dark slot, or for armour anything but gray
        void Lobe(string at, Color got, bool lit, HeartKind kind)
        {
            Color on = HeartRow.Lit(kind);
            if (lit) Check(Near(got, on), at + Hex(got) + " want " + Hex(on));
            else if (HeartRow.Unlit(kind) is Color off) Check(Near(got, off), at + Hex(got) + " want unlit " + Hex(off));
            else Check(!Near(got, on), at + Hex(got) + " must not be " + Hex(on));
        }

        // panel units (y down) to texture pixels (y up)
        static Color Sample(Texture2D t, Vector2 panel, float scale) =>
            t.GetPixel(Mathf.RoundToInt(panel.x * scale), t.height - 1 - Mathf.RoundToInt(panel.y * scale));

        static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) <= 10 / 255f && Mathf.Abs(a.g - b.g) <= 10 / 255f && Mathf.Abs(a.b - b.b) <= 10 / 255f;

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
