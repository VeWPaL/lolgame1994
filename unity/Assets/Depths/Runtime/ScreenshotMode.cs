using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Depths.Unity.Hud;

namespace Depths.Unity
{
    /// <summary>A built player started with -depthsShot path.png [-depthsView... [h:ScreenshotMode-1]</summary>
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
        public static readonly string[] GameViews = { "game", "boss", "sandbox", "hearts", "summary", "pause", "banner", "warning", "lowhp", "ledger", "lab", "labrow", "secret", "rail", "leap", "thud", "rush", "spike", "won", "floorboss", "final1", "final2", "final3" };

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
            else if (_view == "lab") yield return new WaitForSecondsRealtime(3.2f);   // long enough for a dive, a lob and a ring
            else if (_view == "labrow" || _view == "rail") yield return new WaitForSecondsRealtime(1f);
            else if (_view == "hearts") { yield return CheckHearts(); yield break; }
            else if (_view == "secret") { yield return SecretView(); yield break; }
            else if (_view == "leap") yield return LeapView(false);
            else if (_view == "thud") yield return LeapView(true);
            else if (_view == "rush") yield return RushView();
            else if (_view == "floorboss") yield return FloorBossView();
            else if (_view == "final1" || _view == "final2" || _view == "final3") yield return FinalView(_view[5] - '0');
            else if (_view == "spike") yield return SpikeView();
            else if (_view == "summary") yield return SummaryView();
            else if (_view == "won") yield return WonView();
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
            double s0 = Time.realtimeSinceStartupAsDouble, dropped0 = view.DroppedSeconds, fed0 = view.FedSeconds;
            long frames0 = view.Frames;
            int gc0 = GC.CollectionCount(0);
            // GC.GetAllocatedBytesForCurrentThread reads 0 under Unity's Mono; the profiler's own counter is the real one
            using var allocRec = global::Unity.Profiling.ProfilerRecorder.StartNew(global::Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 1024);
            // the final mix, sampled while the demo shoots: a missing listener or a dead bus is silence
            var mix = new float[1024];
            float loudest = 0, longest = 0;   // the longest frame: a hitch past the clock's quarter-second cut loses time
            double unityDt = 0;   // Unity's own frame deltas over the window, logged against the wall clock
            while (Time.realtimeSinceStartupAsDouble - s0 < seconds - 1f)
            {
                unityDt += Time.unscaledDeltaTime;
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
            // the window is the seconds the clock was fed, not the wall clock between two coroutine resumes: a stall late in the
            // frame the window opens on is fed on the next frame, so wall time would miss it (measured: 79 Hz from a 1.07 s stall)
            double s = Time.realtimeSinceStartupAsDouble - s0, dropped = view.DroppedSeconds - dropped0, fed = view.FedSeconds - fed0, played = fed - dropped;
            Debug.Log("[Depths] note Time.unscaledDeltaTime summed " + unityDt.ToString("0.000") + " s over " + s.ToString("0.000") + " s of wall clock");
            long clock = view.Ticks - t0, sim = view.Run.ticks - sim0;   // the run's own count: the loop must really step it
            // A frame over TickClock.MaxFrameSeconds is cut on purpose (no fast-forward after a hitch), so the rate
            // is measured over the seconds the game played; how much a loaded machine made it drop is reported.
            Debug.Log("[Depths] note " + dropped.ToString("0.000") + " s of " + s.ToString("0.000") + " s dropped by the hitch cap");
            long allocSum = 0, allocMax = 0;
            for (int k = 0; k < allocRec.Count; k++) { long v = allocRec.GetSample(k).Value; allocSum += v; allocMax = Math.Max(allocMax, v); }
            Debug.Log("[Depths] note managed garbage: " + (allocRec.Valid ? (allocSum / 1024.0 / s).ToString("0.0") + " KB/s over " + allocRec.Count +
                      " frames (worst frame " + allocMax + " B)" : "no GC counter in this player") + ", " + (GC.CollectionCount(0) - gc0) + " collections in the window");
            // the clock's own input over the window: fed seconds should equal the window, ticks 60 x (fed - dropped)
            Debug.Log("[Depths] note the clock was fed " + (view.FedSeconds - fed0).ToString("0.000") + " s in " + (view.Frames - frames0) +
                      " frames (longest " + view.LongestFrame.ToString("0.000") + " s since start) and stepped " + (view.Ticks - t0) + " ticks");
            Check(played >= 0.5 * fed, "the game played at least half of what it was fed (" + played.ToString("0.00") + " of " + fed.ToString("0.00") + " s)");
            double hz = sim / Math.Max(1e-9, played);
            Check(Balance.TickHz == Balance.GameHz, "Balance.TickHz is " + Balance.TickHz + " in the player (want " + Balance.GameHz + ")");
            Check(view.Run.state == "playing", "the demo run is still playing (state " + view.Run.state + "), so every clock tick reached the sim");
            Check(sim == clock, "the sim stepped " + sim + " ticks for the clock's " + clock);
            Check(Math.Abs(hz - Balance.TickHz) <= 0.02 * Balance.TickHz,
                  "tick rate " + hz.ToString("0.00") + " Hz over " + played.ToString("0.00") + " s played at " +
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

        // the whole descent at once: down to floor 14, into the arena, the final boss down, the run's account as a win
        IEnumerator WonView()
        {
            GameView v = null;
            while (v == null || v.Run == null || v.Hud == null) { yield return null; v = Game(); }
            var run = v.Run;
            while (run.floor < Balance.FinalFloor) run.Descend();
            Check(run.dungeon.AllRooms.Count == 2, "floor 14 is the antechamber and the arena");
            Room arena = null;
            foreach (var r in run.dungeon.AllRooms) if (r.Type == RoomKind.Boss) arena = r;
            Rooms.EnterRoom(run, arena.X, arena.Y, Dir.S);
            run.readyT = 0; run.trans = null;
            run.enemies.Clear();
            for (int i = 0; i < 30; i++) yield return null;
            Check(run.state == "won", "the final boss's death wins the run (state " + run.state + ")");
            Check(v.Hud.Overlays.SummaryShown, "won: the run's account opens");
            Check(v.Hud.Overlays.SummaryValue(0) == "14", "won: floor reached reads " + v.Hud.Overlays.SummaryValue(0));
            Check(v.Hud.Overlays.RecordValue(2) != "none yet", "won: the escape is recorded (" + v.Hud.Overlays.RecordValue(2) + ")");
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

        // A full page: a fixture with every line of both ledgers written (keys, mail, a held item and passives, a... [h:ScreenshotMode-2]
        IEnumerator SecretView()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            var view = FindAnyObjectByType<GameView>();
            var run = view != null ? view.Run : null;
            var room = run?.CurrentRoom;
            if (room == null || !room.Secret.HasValue) { Check(false, "secret: the run did not open in a room with a fake wall"); Application.Quit(1); yield break; }
            var d = room.Secret.Value;
            Debug.Log("[Depths] note secret wall " + d + " in room (" + room.X + "," + room.Y + ")");
            ScreenCapture.CaptureScreenshot(_path);
            for (int i = 0; i < 10; i++) yield return null;
            view.Frozen = true;
            double px = d == Dir.E ? Balance.RoomRight : d == Dir.W ? Balance.RoomLeft : Balance.MidX;
            double py = d == Dir.S ? Balance.RoomBottom : d == Dir.N ? Balance.RoomTop : Balance.MidY;
            Check(!Blast.TryBreakSecret(run, Balance.MidX, Balance.MidY), "a blast in the middle of the room leaves the wall shut");
            Check(Blast.TryBreakSecret(run, px, py), "a blast at the crack opens the wall");
            Check(room.Doors.Contains(d) && !room.Secret.HasValue && run.secret, "the wall is now a door and the find is counted");
            var behind = run.dungeon.Neighbour(room, d);
            Check(behind != null && behind.Type == RoomKind.Secret, "the room behind is the secret room");
            if (behind != null)
            {
                Rooms.EnterRoom(run, behind.X, behind.Y, d == Dir.N ? Dir.S : d == Dir.S ? Dir.N : d == Dir.E ? Dir.W : Dir.E);
                bool hook = false;
                foreach (var pk in run.pickups) hook |= pk.kind == "hook";
                Check(hook, "the secret room holds the hook");
            }
            for (int i = 0; i < 3; i++) yield return null;
            Application.Quit(_failed ? 1 : 0);
        }

        // the Burrower's leap, frozen mid-flight: the body in the air, its shadow, the landing marked on the floor
        IEnumerator LeapView(bool landed)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            var view = FindAnyObjectByType<GameView>();
            Enemy b = null;
            foreach (var e in view.Run.enemies) if (e.kind == BodyKind.Burrower) b = e;
            for (int i = 0; i < 600 && b != null && !(landed ? b.dazed && b.stun < Middle.BurrowDaze * 0.75 : b.move == "leap" && b.moveT <= Middle.BurrowLeap / 2); i++) yield return null;
            view.Frozen = true;
            if (landed) Check(b != null && b.dazed, "the Burrower is down, dazed, tumbling");
            else Check(b != null && b.move == "leap", "the Burrower is in the air (" + (b != null ? b.move : "none") + ")");
            for (int i = 0; i < 10; i++) yield return null;
        }

        // a Brunch winding up its rush, frozen two thirds in: trembling, its line grown toward the player
        IEnumerator RushView()
        {
            GameView view = null;
            while (view == null || view.Run == null) { yield return null; view = FindAnyObjectByType<GameView>(); }
            Enemy w = null;
            for (int i = 0; i < 600 && w == null; i++)
            {
                foreach (var e in view.Run.enemies) if (e.rushWind > 0 && e.rushWind <= Balance.BrunchRushWind / 3) w = e;
                if (w == null) yield return null;
            }
            view.Frozen = true;
            Check(w != null, "a Brunch is winding up its rush");
            for (int i = 0; i < 10; i++) yield return null;
        }

        // the Gatekeeper mid-tell: its lobs' marks on the floor, then a charge line locked at the player, frozen two thirds in
        IEnumerator FloorBossView()
        {
            GameView view = null;
            while (view == null || view.Run == null) { yield return null; view = FindAnyObjectByType<GameView>(); }
            var run = view.Run;
            Enemy g = null;
            for (int i = 0; i < 600 && g == null; i++)
            {
                foreach (var e in run.enemies) if (e.kind == BodyKind.Gatekeeper && e.noticeTimer <= 0) g = e;
                if (g == null) yield return null;
            }
            Check(g != null, "floorboss: the Gatekeeper is in the arena");
            if (g == null) yield break;
            g.bossCd = 1e9;   // the view picks its moves: the lobs, then the charge
            // the view steps the game itself, tick by tick: a frame clock starved by focus changes once left it frozen mid-cast
            view.Frozen = true;
            void Step() { TickOrder.TickPlayer(run, Depths.Input.None); TickOrder.TickProjectiles(run); TickOrder.TickBodies(run); }
            FloorBoss.Begin(run, g, FloorBoss.Attack.Slam);
            for (int i = 0; i < 600 && !run.projectiles.Exists(p => p.lob && p.flight > p.flightMax / 3); i++) Step();
            FloorBoss.Begin(run, g, FloorBoss.Attack.Charge);
            Check(FloorBoss.ChargeTell >= Balance.Sec(FloorBoss.TellMin) && FloorBoss.SlamFlight >= Balance.Sec(FloorBoss.TellMin), "floorboss: the charge's tell and the lobs' marks last at least 0.3 s");
            for (int i = 0; i < 600 && !(g.move == "tell" && g.moveT <= g.fbTellMax / 3); i++) Step();
            Check(g.move == "tell", "floorboss: frozen mid-tell (" + g.move + ")");
            Check(run.projectiles.Exists(p => p.lob), "floorboss: the slam's landing marks are on the floor");
            var shown = LedgerHud.BossShown(run);
            Check(shown == g && LedgerHud.BossName(shown.kind) == FloorBoss.Name, "floorboss: the bar above the room is the Gatekeeper's, named " + FloorBoss.Name);
            for (int i = 0; i < 10; i++) yield return null;
        }

        // the Sunken King, one view a phase, each frozen mid-tell over its floor: seated over the fault lines with a... [h:ScreenshotMode-3]
        IEnumerator FinalView(int phase)
        {
            GameView view = null;
            while (view == null || view.Run == null) { yield return null; view = FindAnyObjectByType<GameView>(); }
            var run = view.Run;
            Enemy k = null;
            for (int i = 0; i < 600 && k == null; i++)
            {
                foreach (var e in run.enemies) if (e.kind == BodyKind.SunkenKing) k = e;
                if (k == null) yield return null;
            }
            string tag = "final" + phase + ": ";
            Check(k != null, tag + "the Sunken King is in the arena");
            if (k == null) yield break;
            view.Frozen = true;   // the view steps the game itself, tick by tick
            void Step() { TickOrder.TickPlayer(run, Depths.Input.None); TickOrder.TickProjectiles(run); TickOrder.TickBodies(run); }
            k.noticeTimer = 0; k.bossCd = 1e9;
            FinalBoss.Settle(k, phase);
            if (phase >= 2) { k.x = Balance.MidX - 170; k.y = Balance.MidY - 60; }   // off the throne, beside the fallen ground
            var g = k.ground;
            int min = Balance.Sec(FinalBoss.TellMin);
            Step();
            if (phase == 1)
            {
                FinalBoss.Begin(run, k, FinalBoss.Attack.Fan);
                for (int i = 0; i < 600 && (k.move == "cast" || run.projectiles.Count == 0); i++) Step();
                for (int i = 0; i < 8; i++) Step();
                FinalBoss.Begin(run, k, FinalBoss.Attack.Quake);
                for (int i = 0; i < 600 && FirstWarn(g) > FinalBoss.CrackTell * 0.4f; i++) Step();
                Check(g.Cracking && Lit(g, FinalBoss.Source.Fault) == 30, tag + "the fault lines (every other row) are cracking");
                Check(run.projectiles.Exists(p => !p.friendly), tag + "a fan's shells are in the air");
                Check(FinalBoss.CrackTell >= min && FinalBoss.FanTell >= min, tag + "the crack and the fan's flash last at least 0.35 s");
                Check(FinalBoss.PostureOf(k) == FinalBoss.Posture.Seated && k.x == FinalBoss.ThroneX, tag + "seated on its throne");
            }
            else if (phase == 2)
            {
                FinalBoss.Begin(run, k, FinalBoss.Attack.Quake);
                for (int i = 0; i < 600 && g.Cracking; i++) Step();
                for (int i = 0; i < 6; i++) Step();
                Check(Burning(g) > 10, tag + "the checker is burning");
                for (int i = 0; i < 600 && (g.Cracking || g.Burning); i++) Step();   // the game lays no rift over a burning floor
                FinalBoss.Begin(run, k, FinalBoss.Attack.Rift);
                for (int i = 0; i < 600 && k.move == "rift" && k.moveT > k.fbTellMax * 0.4f; i++) Step();
                Check(k.move == "rift", tag + "frozen mid-rift (" + k.move + ")");
                Check(!g.Burning && !g.Cracking, tag + "the rift is laid on quiet ground, the checker burned out");
                Check(Pits(g) == 8, tag + "the heart of the room has fallen");
                Check(FinalBoss.RiftTell >= min, tag + "the rift's strip shows at least 0.35 s");
                Check(FinalBoss.PostureOf(k) == FinalBoss.Posture.Standing, tag + "standing");
            }
            else
            {
                FinalBoss.Begin(run, k, FinalBoss.Attack.Quake);
                for (int i = 0; i < 600 && k.move == "quake"; i++) Step();
                for (int i = 0; i < 600 && g.Cracking && FirstWarn(g) > FinalBoss.QuadTell * 0.4f; i++) Step();
                Check(g.Cracking && k.castT <= 0 && k.move != "tell" && k.move != "rift" && k.move != "leap", tag + "the quadrants crack and the King waits on them (the floor is the attack)");
                Check(Lit(g, FinalBoss.Source.Quadrant) == 24 && Pits(g) == 28, tag + "three quadrants crack inside the fallen edge");
                Check(FinalBoss.QuadTell >= min, tag + "the quadrants' crack lasts at least 0.35 s");
                Check(FinalBoss.PostureOf(k) == FinalBoss.Posture.Risen, tag + "risen");
            }
            var shown = LedgerHud.BossShown(run);
            Check(shown == k && LedgerHud.BossName(shown.kind) == FinalBoss.Name && shown.phase == phase, tag + "the bar above the room is the King's, named " + FinalBoss.Name + ", phase " + phase);
            for (int i = 0; i < 10; i++) yield return null;
        }

        static int FirstWarn(FinalBoss.Ground g) { int m = int.MaxValue; for (int i = 0; i < FinalBoss.Ground.N; i++) if (g.warn[i] > 0) m = Math.Min(m, g.warn[i]); return m; }
        static int Lit(FinalBoss.Ground g, FinalBoss.Source s) { int n = 0; for (int i = 0; i < FinalBoss.Ground.N; i++) if (g.warn[i] > 0 && g.src[i] == s) n++; return n; }
        static int Burning(FinalBoss.Ground g) { int n = 0; for (int i = 0; i < FinalBoss.Ground.N; i++) if (g.burn[i] > 0) n++; return n; }
        static int Pits(FinalBoss.Ground g) { int n = 0; for (int i = 0; i < FinalBoss.Ground.N; i++) if (g.pit[i]) n++; return n; }

        // down to a floor with a spike room, walked in beside it, into it: its elite's glow, and the skull on the map
        IEnumerator SpikeView()
        {
            GameView view = null;
            while (view == null || view.Run == null) { yield return null; view = FindAnyObjectByType<GameView>(); }
            var run = view.Run;
            Room spike = null;
            for (int f = 0; f < 12 && spike == null; f++)
            {
                if (run.floor < Spikes.FirstFloor) { run.Descend(); continue; }
                foreach (var r in run.dungeon.AllRooms) if (r.Spike == SpikeKind.Elite && spike == null) spike = r;
                if (spike == null) run.Descend();
            }
            Check(spike != null, "a floor with an elite spike room");
            if (spike == null) yield break;
            foreach (var r in run.dungeon.AllRooms)   // its neighbours walked, so the map knows it
                if (System.Math.Abs(r.X - spike.X) + System.Math.Abs(r.Y - spike.Y) == 1) { r.Visited = true; r.Spawned = true; r.Cleared = true; }
            Rooms.EnterRoom(run, spike.X, spike.Y, Dir.S);
            run.readyT = 0; run.trans = null;
            bool elite = false;
            foreach (var e in run.enemies) elite |= e.elite;
            Check(elite, "the spike room's elite is in it");
            for (int i = 0; i < 30; i++) yield return null;
            view.Frozen = true;
            for (int i = 0; i < 10; i++) yield return null;
        }

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
