using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Depths.Unity.Hud;

namespace Depths.Unity
{
    /// <summary>
    /// A built player started with <c>-depthsShot path.png [-depthsView menu|controls|audio|gameplay|game|hearts|boss|sandbox|summary|pause|banner|warning|lowhp|ledger]</c>
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

        /// <summary>Views that open the Game scene; the rest are menu pages.</summary>
        public static readonly string[] GameViews = { "game", "boss", "sandbox", "hearts", "summary", "pause", "banner", "warning", "lowhp", "ledger" };

        void Check(bool ok, string what)
        {
            if (!ok) _failed = true;
            Debug.Log("[Depths] check " + (ok ? "PASS " : "FAIL ") + what);
        }

        IEnumerator Start()
        {
            for (int i = 0; i < 10; i++) yield return null;
            bool inGame = Array.IndexOf(GameViews, _view) >= 0;
            if (inGame) SceneManager.LoadScene("Game");
            else
            {
                var m = FindAnyObjectByType<MainMenuController>();
                if (m != null && Array.IndexOf(MainMenuController.EvidenceViews, _view) >= 0) m.Evidence(_view);
                else if (m != null) m.ShowView(_view);
            }
            // the game views play for a while in real time (frame counts depend on the frame rate)
            if (_view == "game") yield return MeasureTickRate(5f);
            else if (_view == "boss") yield return new WaitForSecondsRealtime(4f);
            else if (_view == "sandbox") yield return new WaitForSecondsRealtime(1.5f);
            else if (_view == "hearts") { yield return CheckHearts(); yield break; }
            else if (_view == "summary") yield return SummaryView();
            else if (_view == "pause") yield return PauseView();
            else if (_view == "banner") yield return BannerView();
            else if (_view == "warning") yield return WarningView();
            else if (_view == "lowhp") yield return LowView();
            else if (_view == "ledger") yield return LedgerView();
            else if (Array.IndexOf(MainMenuController.EvidenceViews, _view) >= 0) yield return new WaitForSecondsRealtime(1f);   // the list opens, the pointer arrives
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
            float loudest = 0, longest = 0;   // the longest frame: a hitch past the clock's quarter-second cut loses time
            while (Time.realtimeSinceStartup - s0 < seconds - 1f)
            {
                AudioListener.GetOutputData(mix, 0);
                foreach (var x in mix) loudest = Mathf.Max(loudest, Mathf.Abs(x));
                longest = Mathf.Max(longest, Time.unscaledDeltaTime);
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
                  ((Time.frameCount - f0) / s).ToString("0") + " fps, longest frame " + (longest * 1000).ToString("0") + " ms (want " + Balance.TickHz + " +-2%)");
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
            // one row in Isaac order (red, the regen heart, armour): the i-th heart of the spec is the i-th slot
            for (int i = 0; i < Expected.Length; i++)
            {
                var (kind, fill) = Expected[i];
                var tl = HudLayout.HeartTopLeft(i);
                Color left = Sample(tex, HudLayout.LeftLobe(tl), scale), right = Sample(tex, HudLayout.RightLobe(tl), scale);
                string at = "heart " + (i + 1) + " " + kind + " " + fill;
                Lobe(at + ": left lobe ", left, fill != HeartFill.Empty, kind);
                Lobe(at + ": right lobe ", right, fill == HeartFill.Full, kind);
            }
            // nothing after the last heart: the row is exactly as long as the fixture
            var past = HudLayout.HeartTopLeft(Expected.Length);
            Color after = Sample(tex, HudLayout.LeftLobe(past), scale);
            bool anyHeart = Near(after, HeartRow.Red) || Near(after, HeartRow.Regen) || Near(after, HeartRow.Armour) ||
                            Near(after, HeartRow.RedEmpty) || Near(after, HeartRow.RegenEmpty);
            Check(!anyHeart, "no heart past the " + Expected.Length + "th: " + Hex(after));
            // exact halves: the half regen heart is lit up to the middle of its centre column and dark after it
            var rh = HudLayout.HeartTopLeft(4);
            float mid = 2 + 3.5f * HudLayout.HeartPx, rowY = 2 + 3 * HudLayout.HeartPx + HudLayout.HeartPx / 2f;
            Color justLeft = Sample(tex, rh + new Vector2(mid - 1.5f, rowY), scale), justRight = Sample(tex, rh + new Vector2(mid + 0.5f, rowY), scale);
            Check(Near(justLeft, HeartRow.Regen) && Near(justRight, HeartRow.RegenEmpty),
                  "half a heart is exactly half: " + Hex(justLeft) + " | " + Hex(justRight) + " across the middle of the centre column");
            Destroy(tex);
            for (int i = 0; i < 3; i++) yield return null;
            Application.Quit(_failed ? 1 : 0);
        }

        GameView Game() => FindAnyObjectByType<GameView>();

        // Dies after the demo has fought a while, so the account has real numbers in it.
        IEnumerator SummaryView()
        {
            yield return new WaitForSecondsRealtime(4f);
            var v = Game();
            if (v == null || v.Hud == null) { Check(false, "summary: no running GameView"); yield break; }
            v.Run.player.hp = 0;
            v.Run.EndRun(false);
            for (int i = 0; i < 20; i++) yield return null;
            Check(v.Hud.Overlays.SummaryShown, "summary: the run's account opens on a death");
            Check(v.Hud.Overlays.SummaryValue(0) == v.Run.floor.ToString(), "summary: floor reached reads " + v.Hud.Overlays.SummaryValue(0));
            Check(v.Hud.Overlays.SummaryValue(4) == v.Run.kills.ToString(), "summary: enemies defeated reads " + v.Hud.Overlays.SummaryValue(4) + " for " + v.Run.kills);
        }

        IEnumerator PauseView()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            var v = Game();
            if (v == null || v.Hud == null) { Check(false, "pause: no running GameView"); yield break; }
            v.Paused = true;
            long t0 = v.Ticks;
            for (int i = 0; i < 20; i++) yield return null;
            Check(v.Hud.Overlays.PauseShown, "pause: the pause page is open");
            Check(v.Ticks == t0, "pause: no tick stepped under the pause page (" + (v.Ticks - t0) + ")");
        }

        // Four real descents, so the heading is the one a player sees on entering the second area.
        IEnumerator BannerView()
        {
            yield return new WaitForSecondsRealtime(1f);
            var v = Game();
            if (v == null || v.Hud == null) { Check(false, "banner: no running GameView"); yield break; }
            for (int i = 0; i < 4; i++) { v.Run.Descend(); yield return null; }
            yield return new WaitForSecondsRealtime(0.9f);
            Check(v.Run.floor == 5 && v.Hud.Overlays.BannerShown, "banner: FLOOR 5 is shown after the descent (floor " + v.Run.floor + ")");
        }

        IEnumerator WarningView()
        {
            yield return new WaitForSecondsRealtime(1f);
            var v = Game();
            if (v == null || v.Hud == null) { Check(false, "warning: no running GameView"); yield break; }
            v.Frozen = true;   // the fixture holds: the warning's timer stays at its middle
            v.Run.bossWarnT = Balance.BossWarnTime / 2;
            for (int i = 0; i < 10; i++) yield return null;
            Check(v.Hud.Overlays.WarningShown, "warning: the Warden's notice is up while bossWarnT > 0");
        }

        IEnumerator LowView()
        {
            yield return new WaitForSecondsRealtime(1f);
            var v = Game();
            if (v == null || v.Hud == null) { Check(false, "lowhp: no running GameView"); yield break; }
            v.Frozen = true;   // the fixture holds: one point of red, nothing else
            var p = v.Run.player;
            p.hp = 1; p.regenHeart = 0; p.armor = 0;
            yield return new WaitForSecondsRealtime(0.35f);
            Check(LedgerHud.IsLow(p), "lowhp: the ledger reads the body as on its last heart");
        }

        // A full page: a fixture with every line of both ledgers written (keys, mail, a held item and
        // passives, a part-spent blink, momentum, a charted floor). The harness sets these by hand.
        IEnumerator LedgerView()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            var v = Game();
            if (v == null || v.Hud == null) { Check(false, "ledger: no running GameView"); yield break; }
            v.Frozen = true;
            var run = v.Run;
            Items.Give(run, "tin_cup");
            Items.Give(run, "iron_ribs");
            Items.Give(run, "lucky_coin");
            var p = run.player;
            p.hp = Math.Min(p.maxHp, 7); p.armor = 3; p.hasSilver = true; p.hasGold = true;
            p.momentum = 0.62; p.weaponIdx = 1; p.cooldown = Weapons.All[1].Cooldown / Balance.TempoRate * 0.4;
            p.altCooldown = (int)(Balance.AltCooldown / Balance.TempoRate * 0.7);
            run.blinkCharges = 1; p.blinkRegen = Balance.BlinkRecharge * 0.45;
            run.kills = 23;
            // the chart: walk the floor breadth-first from the start, the path to the Warden included, and leave
            // a heart and the gold key lying in two walked rooms, so every mark the map can make is on it
            var d = run.dungeon;
            var start = run.CurrentRoom;
            var parent = new Dictionary<Room, Room>();
            var order = new List<Room> { start };
            parent[start] = null;
            for (int q = 0; q < order.Count; q++)
                foreach (Dir dir in new[] { Dir.N, Dir.E, Dir.S, Dir.W })
                {
                    if (!order[q].Doors.Contains(dir)) continue;
                    var n = d.Neighbour(order[q], dir);
                    if (n == null || parent.ContainsKey(n)) continue;
                    parent[n] = order[q];
                    order.Add(n);
                }
            Room boss = null;
            foreach (var r in order) if (r.Type == RoomKind.Boss) boss = r;
            for (var r = boss; r != null; r = parent[r]) r.Visited = true;
            if (boss != null) boss.Cleared = false;   // walked in on, not finished: the threat mark
            int walked = 0;
            foreach (var r in order)
            {
                if (walked >= 7 || r.Type == RoomKind.Item) continue;
                if (!r.Visited) { r.Visited = true; r.Cleared = true; }
                walked++;
            }
            var lying = new List<Room>();
            foreach (var r in order) if (r.Visited && r != start && r.Type != RoomKind.Boss) lying.Add(r);
            if (lying.Count > 0) lying[0].Pickups.Add(new Pickup { kind = "heart" });
            if (lying.Count > 1) lying[lying.Count - 1].Pickups.Add(new Pickup { kind = "goldkey" });
            int visited = 0;
            foreach (var r in d.AllRooms) if (r.Visited) visited++;
            Check(boss != null && boss.Visited && visited >= 6, "ledger: the chart has " + visited + " walked rooms and the Warden's");
            for (int i = 0; i < 10; i++) yield return null;
            Check(Items.ActiveItem(run) != null, "ledger: the fixture holds an active item");
        }

        // a lit lobe is the kind's colour; an unlit one is its dark slot
        void Lobe(string at, Color got, bool lit, HeartKind kind)
        {
            Color want = lit ? HeartRow.Lit(kind) : HeartRow.Unlit(kind);
            Check(Near(got, want), at + Hex(got) + " want " + (lit ? "" : "unlit ") + Hex(want));
        }

        // panel units (y down) to texture pixels (y up)
        static Color Sample(Texture2D t, Vector2 panel, float scale) =>
            t.GetPixel(Mathf.RoundToInt(panel.x * scale), t.height - 1 - Mathf.RoundToInt(panel.y * scale));

        static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) <= 10 / 255f && Mathf.Abs(a.g - b.g) <= 10 / 255f && Mathf.Abs(a.b - b.b) <= 10 / 255f;

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
