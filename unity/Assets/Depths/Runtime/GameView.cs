using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Depths.Unity.Hud;

namespace Depths.Unity
{
    /// <summary>The game, on Depths.Core at its tick rate (Balance.TickHz, 60 Hz): a seeded run of rooms, doors, keys, waves... [h:GameView-1]</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameView : MonoBehaviour
    {
        public InputActionAsset controls;

        static readonly BodyKind[] DummyKinds = { BodyKind.Lunger, BodyKind.Shooter, BodyKind.Lunger, BodyKind.Gunner };

        RunState _run;
        InputAction _move, _cast, _blast, _blink, _active, _pause;
        int _lastShots, _lastHits, _lastKills;
        double _lastHp, _lastDmg;
        WorldView _world;
        LedgerHud _hud;
        bool _devOn;
        int _lastPicked, _lastWinding, _lastThuds, _lastBooms;
        readonly ScreenShake _shake = new ScreenShake();
        bool _lastWarned;
        Bindings _binds;
        long _devKey = long.MinValue;
        bool _recorded;   // the records take a run once, on the frame it ends
        RunRecords.Result _rec;
        readonly TickClock _clock = new TickClock();
        int _respawnT;
        bool _demo;
        float _demoS;   // seconds of demo input so far: the demo is timed, not counted in frames
        bool _demoBlast;   // held until a tick takes it: at 150 fps most frames step no tick

        /// <summary>For the screenshot checks: the run, the clock's ticks, and a freeze that stops ticking without an
        /// overlay (input between ticks - blink, Q, keys - still reaches the run).</summary>
        public RunState Run => _run;
        public long Ticks => _clock.Ticks;
        public double DroppedSeconds => _clock.DroppedSeconds;
        /// <summary>The clock's own input, for the rate check's diagnosis: seconds fed, the longest frame, frames.</summary>
        public double FedSeconds { get; private set; }
        public double LongestFrame { get; private set; }
        public long Frames { get; private set; }
        public bool Frozen { get; set; }
        /// <summary>Esc's pause, settable so the screenshot mode can show the pause page.</summary>
        public bool Paused { get => _paused; set => _paused = value; }
        public LedgerHud Hud => _hud;

        void Start()
        {
            _binds = new Bindings(controls, new PlayerPrefsStore());   // applies the player's saved rebinds
            new DisplaySettings(new PlayerPrefsStore()).Apply();   // the frame rate, also when Play starts in this scene
            var map = controls.FindActionMap("Gameplay", true);
            _move = map.FindAction("Move", true);
            _cast = map.FindAction("Cast", true);
            _blast = map.FindAction("Blast", true);
            _blink = map.FindAction("Blink", true);
            _active = map.FindAction("Active", true);
            _pause = map.FindAction("Pause", true);
            map.Enable();
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-depthsView");
            _demo = i >= 0 && i + 1 < args.Length && (args[i + 1] == "game" || args[i + 1] == "boss" || args[i + 1] == "summary" || args[i + 1] == "pause" || args[i + 1] == "won");
            _floorBossView = i >= 0 && i + 1 < args.Length && args[i + 1] == "floorboss";   // the Gatekeeper's arena, the player still
            _finalView = i >= 0 && i + 1 < args.Length && args[i + 1].StartsWith("final", StringComparison.Ordinal);   // the King's arena, a view a phase
            _bossRoom = _floorBossView || _finalView || (i >= 0 && i + 1 < args.Length && args[i + 1] == "boss");
            _sandbox = i >= 0 && i + 1 < args.Length && args[i + 1] == "sandbox";
            bool labRow = i >= 0 && i + 1 < args.Length && args[i + 1] == "labrow";   // the row, frozen and unshot: every sprite
            _inLab = labRow || (i >= 0 && i + 1 < args.Length && args[i + 1] == "lab");
            _demo |= _sandbox || (_inLab && !labRow);
            _secretView = i >= 0 && i + 1 < args.Length && args[i + 1] == "secret";
            _railView = i >= 0 && i + 1 < args.Length && args[i + 1] == "rail";   // one egg track (-depthsSeed n picks it), the lab frozen
            _leapView = i >= 0 && i + 1 < args.Length && (args[i + 1] == "leap" || args[i + 1] == "thud");   // a Burrower about to jump at the player
            _rushView = i >= 0 && i + 1 < args.Length && args[i + 1] == "rush";   // a Brunch pack beside the player, about to rush
            _inLab |= _railView || _leapView || _rushView;
            int si = Array.IndexOf(args, "-depthsSeed");
            if (si >= 0 && si + 1 < args.Length && uint.TryParse(args[si + 1], out var fixedSeed)) _fixedSeed = fixedSeed;
            if (_secretView && _fixedSeed == null) _fixedSeed = 1;   // seed 1 has a fake wall on floor 1 (the JS reference frame uses it too)
            NewRun();
            if (_inLab && !labRow) _lab.SetFrozen(false, _run);   // the screenshot wants the row at work
        }

        // the room jolts (the ledger too, at Insanity) and settles as the trauma drains; a paused or frozen game holds still
        void Shake()
        {
            bool still = _paused || Frozen;
            if (!still) _shake.Step(_frameS);
            var (x, y, deg) = still ? (0f, 0f, 0f) : _shake.Offset();
            if (x == 0 && y == 0 && deg == 0 && !_shook) return;
            _world.style.translate = new Translate(x, y);
            _world.style.rotate = new Rotate(deg);
            _hud.style.translate = _shake.HudToo ? new Translate(x * 0.5f, y * 0.5f) : new Translate(0, 0);
            _shook = x != 0 || y != 0 || deg != 0;
        }
        bool _shook;

        // alt-tab pauses a live run, and the time away is not fed to the clock
        void OnApplicationFocus(bool focused)
        {
            if (focused) { _lastFrameAt = -1; return; }
            if (_run != null && _run.state == "playing" && RealRun && !_demo) _paused = true;
        }

        void OnDestroy()
        {
            controls?.FindActionMap("Gameplay")?.Disable();
            Depths.Unity.Audio.MusicEngine.SetFight(false);   // back to the menu: the calm layer only
        }

        bool _floorBossView, _finalView;
        bool _bossRoom, _sandbox, _inLab, _secretView, _railView, _leapView, _rushView, _wonSounded;
        uint? _fixedSeed;   // -depthsSeed: the screenshot checks pick their dungeon
        readonly LabMode _lab = new LabMode();
        bool RealRun => !_sandbox && !_bossRoom && !_inLab;

        void LeaveLab() { if (_inLab) { _lab.Leave(); _inLab = false; } }

        void NewRun()
        {
            new GameplaySettings(new PlayerPrefsStore()).Apply();   // the curve is chosen per run, in Options
            _recorded = false;
            _paused = false; _bannerS = 0; _lastFloor = 1; _respawnT = 0;   // the view starts with the run
            _lastPicked = 0; _lastWarned = false; _wonSounded = false; _lastWinding = 0; _lastThuds = 0; _lastBooms = 0;
            _shake.Clear(); _shake.Level = new GameplaySettings(new PlayerPrefsStore()).Shake;
            uint seed = _fixedSeed ?? (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            if (RealRun)
            {
                _run = new RunState(new Rng(seed));
                _run.Start(seed);
                if (_secretView) EnterSecretWall();
                _lastShots = _lastHits = _lastKills = 0; _lastHp = _run.player.hp; _lastDmg = 0;
                return;
            }
            _run = new RunState(new Rng(seed)) { dungeon = Dungeon.FromSeed(new Rng(seed)) };
            _run.state = "playing";
            _run.readyT = 0; _run.fadeT = 0;
            var p = _run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.MidY + 120;
            p.regenHeart = p.regenHeartMax = Balance.RegenHp;   // the same body a real run starts with
            p.hp = p.maxHp = 8 - Balance.RegenHp;
            _lastShots = _lastHits = _lastKills = 0; _lastHp = _run.player.hp; _lastDmg = 0;
            _run.enemies.Clear();
            var arena = _run.CurrentRoom;
            if (arena != null) arena.Fought = true;   // hand-placed waves are fights, so winning one refills the regen heart
            if (_bossRoom)
            {
                // the Warden (or the Gatekeeper, for its view), spawned the way the game spawns it (its own draws, its kit)
                var w = Spawn.Body(_run, _finalView ? BodyKind.SunkenKing : _floorBossView ? BodyKind.Gatekeeper : BodyKind.Boss, Balance.MidX, Balance.RoomTop + 120);
                w.noticeTimer = Balance.Sec(0.43);
                _run.enemies.Add(w);
                return;
            }
            if (_inLab)
            {
                _lab.Enter(_run);
                if (_leapView)
                {
                    _run.enemies.Clear();
                    var b = Spawn.Body(_run, BodyKind.Burrower, _run.player.x - 150, _run.player.y - 90);
                    b.noticeTimer = 0; b.alerted = true; b.buried = true; b.move = "crack"; b.moveT = Middle.BurrowCrack;
                    _run.enemies.Add(b);
                    _lab.SetFrozen(false, _run);
                }
                if (_rushView)
                {
                    _run.enemies.Clear();
                    var s = Spawn.Body(_run, BodyKind.Shooter, Balance.MidX + 220, Balance.RoomTop + 50);
                    s.noticeTimer = int.MaxValue / 2;
                    _run.enemies.Add(s);
                    for (int k = 0; k < 4; k++)
                    {
                        var b = Spawn.Body(_run, BodyKind.Brunch, _run.player.x - 150 + (k % 2) * 20, _run.player.y - 70 + (k / 2) * 20);
                        b.packId = 91; b.packSlot = k; b.noticeTimer = 0; b.alerted = true;
                        _run.enemies.Add(b);
                    }
                    _lab.SetFrozen(false, _run);
                }
                if (_railView)
                {
                    _run.enemies.Clear();
                    var egg = Spawn.Body(_run, BodyKind.Egg, Balance.MidX, Balance.MidY);
                    egg.noticeTimer = int.MaxValue / 2;
                    Eggs.OnRail(_run, egg, (int)((_fixedSeed ?? 0) % (uint)Eggs.Patterns));
                    _run.enemies.Add(egg);
                }
                return;
            }
            for (int k = 0; k < DummyKinds.Length; k++) SpawnDummy(k);
            SpawnPack();
        }

        // The "secret" screenshot view: straight into the room that holds the fake wall, emptied, the ready freeze skipped.
        void EnterSecretWall()
        {
            foreach (var r in _run.dungeon.AllRooms)
            {
                if (!r.Secret.HasValue) continue;
                Rooms.EnterRoom(_run, r.X, r.Y, r.Secret.Value == Dir.S ? Dir.N : Dir.S);
                _run.enemies.Clear();
                _run.readyT = 0; _run.fadeT = 0;
                return;
            }
        }

        // A Brunch pack of four: it walls the nearest shooter or gunner, or hunts you if there is none.
        void SpawnPack()
        {
            for (int i = 0; i < 4; i++)
            {
                var e = Enemy.Of(BodyKind.Brunch, Balance.RoomLeft + 300 + (i % 2) * 18, Balance.RoomTop + 200 + (i / 2) * 18);
                e.packId = 1; e.packSlot = i; e.noticeTimer = Balance.Sec(0.43);
                _run.enemies.Add(e);
            }
        }

        void SpawnDummy(int k)
        {
            double x = Balance.RoomLeft + 110 + k * 160, y = Balance.RoomTop + 90 + (k % 2) * 60;
            var e = Enemy.Of(DummyKinds[k % DummyKinds.Length], x, y);
            e.flank = k * 2.399963229728653;   // the game's golden-angle flank cursor
            e.noticeTimer = Balance.Sec((60 + 25.0 * k) / Balance.JsHz);   // a beat before they move: 60 + 25k ticks at the JS rate
            e.shootCd = e.cdMin;                 // and before a ranged body's first cast
            _run.enemies.Add(e);
        }

        bool _paused;
        float _bannerS;   // seconds the floor banner has left
        int _lastFloor = 1;

        // This frame's seconds, from the wall clock rather than Time.unscaledDeltaTime, so the deltas sum to real time... [h:GameView-2]
        double _lastFrameAt = -1;
        float _frameS;

        // A frame of Update that runs long names its slow part in the log (a stall over a second was measured
        // once a run had just started). Costs a stopwatch read per section; logs only past the threshold.
        static readonly System.Diagnostics.Stopwatch FrameWatch = new System.Diagnostics.Stopwatch();
        static readonly string[] SectionNames = { "input", "ticks", "lab", "sounds", "music+banner", "world", "hud" };
        readonly double[] _sectionMs = new double[7];
        double _sectionAt;
        const double LongFrameMs = 200;

        void Section(int i)
        {
            double now = FrameWatch.Elapsed.TotalMilliseconds;
            _sectionMs[i] = now - _sectionAt;
            _sectionAt = now;
        }

        void ReportLongFrame()
        {
            if (_sectionAt < LongFrameMs) return;
            var sb = new System.Text.StringBuilder("[Depths] note long Update ").Append(_sectionAt.ToString("0")).Append(" ms:");
            for (int i = 0; i < _sectionMs.Length; i++) sb.Append(' ').Append(SectionNames[i]).Append(' ').Append(_sectionMs[i].ToString("0"));
            Debug.Log(sb.Append(" (frame ").Append(Time.frameCount).Append(", run tick ").Append(_run.ticks).Append(')').ToString());
        }

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            _frameS = _lastFrameAt < 0 ? 0 : (float)(now - _lastFrameAt);
            _lastFrameAt = now;
            if (!_paused && !Frozen) { FedSeconds += _frameS; LongestFrame = Math.Max(LongestFrame, _frameS); Frames++; }
            FrameWatch.Restart(); _sectionAt = 0;
            var kb = Keyboard.current;
            if (_hud == null)
            {
                var root = GetComponent<UIDocument>().rootVisualElement?.Q("gameRoot");
                if (root == null || root.panel == null) return;   // the document attaches late; see the history note
                root.Clear();
                _world = new WorldView(() => _run);
                _world.style.position = Position.Absolute;
                _world.style.left = 0; _world.style.top = 0; _world.style.right = 0; _world.style.bottom = 0;
                root.Add(_world);
                _hud = new LedgerHud();
                root.Add(_hud);
                _lab.Attach(root);
            }
            // Esc pauses, as in the game; M from the pause (or after a death) goes back to the menu
            if (_pause.WasPressedThisFrame() && _run.state == "playing") _paused = !_paused;
            if ((_paused || _run.state != "playing") && kb != null && kb.mKey.wasPressedThisFrame) { SceneManager.LoadScene("MainMenu"); return; }
            if (kb != null)
            {
                for (int w = 0; w < 4; w++) if (kb[Key.Digit1 + w].wasPressedThisFrame) _run.player.weaponIdx = w;
                if (kb.rKey.wasPressedThisFrame) NewRun();
                if (kb.f10Key.wasPressedThisFrame) _devOn = !_devOn;   // not F3: in the lab F3 freezes the row, as in the JS lab
                // the sandbox and the Warden arena are developer views: only with the dev line up (F10)
                if (_devOn && kb.f1Key.wasPressedThisFrame) { _sandbox = !_sandbox; _bossRoom = false; LeaveLab(); NewRun(); }
                if (_devOn && kb.bKey.wasPressedThisFrame) { _bossRoom = !_bossRoom; _sandbox = false; LeaveLab(); NewRun(); }
                // F3 skips a floor in a real run (in the lab it freezes the row); never past the last floor
                if (_devOn && RealRun && !_paused && _run.state == "playing" && kb.f3Key.wasPressedThisFrame
                    && !(Balance.RunEnds && _run.floor >= Balance.FinalFloor)) _run.Descend();
                if (kb.f2Key.wasPressedThisFrame) { bool enter = !_inLab; _sandbox = _bossRoom = false; LeaveLab(); _inLab = enter; NewRun(); }
            }

            // the aim: the pointer, from screen pixels to panel units to the room
            Vector2 aim = new Vector2((float)_run.player.x + 200, (float)_run.player.y);
            var mouse = Mouse.current;
            if (mouse != null && _world.panel != null)
            {
                var sp = mouse.position.ReadValue();
                var pp = RuntimePanelUtils.ScreenToPanel(_world.panel, new Vector2(sp.x, Screen.height - sp.y));
                aim = pp - WorldView.Origin;
            }
            Vector2 mv = _move.ReadValue<Vector2>();
            bool fire = _cast.IsPressed(), alt = _blast.IsPressed();
            if (_demo) DemoInput(ref mv, ref fire, ref alt, ref aim);
            if (_inLab) _lab.Keys(kb, _run, aim);

            // the blink is a key press, so it lands between ticks, as in the game
            if (!_paused && _blink.WasPressedThisFrame() && TickOrder.TryBlink(_run, new Input(Math.Sign(mv.x), -Math.Sign(mv.y), aimX: aim.x, aimY: aim.y)))
                Depths.Unity.Audio.SoundEngine.Play("blink");

            // Q uses the active item, between ticks like the game's key handler
            if (!_paused && _active.WasPressedThisFrame() && _run.state == "playing" && Items.UseActive(_run))
                Depths.Unity.Audio.SoundEngine.Play("pickup");

            Section(0);
            for (int n = _clock.Advance(_frameS, _paused || Frozen); n > 0; n--)
            {
                // keys are -1/0/1 per axis in the game, and screen y points down
                var input = new Input(Math.Sign(mv.x), -Math.Sign(mv.y), fire: fire, alt: alt, aimX: aim.x, aimY: aim.y);
                _demoBlast = false;
                Interp.Snap(_run);   // where everything was, so the frame can draw between this tick and the next
                if (RealRun) TickOrder.Update(_run, input);   // gates, transitions, then player, shells, bodies, room
                else if (_run.state == "playing")
                {
                    TickOrder.TickPlayer(_run, input);
                    _run.trans = null;   // the sandbox is one room: a door leads nowhere
                    TickOrder.TickProjectiles(_run);
                    TickOrder.TickBodies(_run);
                    TickOrder.TickRoom(_run);
                    if (!_inLab && _run.enemies.Count == 0 && ++_respawnT > Balance.Sec(2)) { _respawnT = 0; if (!_bossRoom) { for (int k = 0; k < DummyKinds.Length; k++) SpawnDummy(k); SpawnPack(); if (_run.CurrentRoom != null) _run.CurrentRoom.Cleared = false; } }   // each wave won pays as a real room does: regen refill + half blink
                }
            }
            Section(1);
            _lab.AfterTicks(_run, _paused || Frozen ? 0 : _frameS);
            _lab.Draw();
            Section(2);
            PlayEvents();
            Section(3);
            Depths.Unity.Audio.MusicEngine.SetArea(AreaRules.AreaForFloor(_run.floor));
            Depths.Unity.Audio.MusicEngine.SetFight(_run.state == "playing" && !_paused && _run.enemies.Count > 0);
            if (_run.floor != _lastFloor) { _lastFloor = _run.floor; _bannerS = BannerS; Depths.Unity.Audio.SoundEngine.Play("door"); }
            if (_bannerS > 0 && !_paused && !Frozen) _bannerS -= _frameS;
            Section(4);
            _world.Aim = aim;
            _world.Alpha = (float)_clock.Alpha;
            _world.Step(_paused || Frozen || _run.state != "playing" ? 0 : _frameS);
            Shake();
            Section(5);
            _hud.Refresh(_run, _frameS);
            _hud.Overlays.SetPause(_paused && _run.state == "playing", _run);
            if (_paused) _hud.Overlays.SetPauseKey(_binds.Display("Pause"));
            if (_run.state != "playing" && !_recorded) { _recorded = true; _rec = RecordRun(); }
            _hud.Overlays.SetSummary(_run.state != "playing", _run, _rec);
            _hud.Overlays.SetBanner(_run.floor, BannerAlpha(_bannerS));
            _hud.Overlays.SetWarning(HudOverlays.WarningAlpha(_run));
            UpdateDev();
            Section(6);
            ReportLongFrame();
        }

        // A real run's end goes into the records; the sandbox and the arena only read them.
        RunRecords.Result RecordRun()
        {
            // the screenshot mode plays fixtures: it must never write into the player's real records
            bool shot = Array.IndexOf(Environment.GetCommandLineArgs(), "-depthsShot") >= 0;
            var records = new RunRecords(shot ? (ISettingsStore)new MemorySettingsStore() : new PlayerPrefsStore());
            if (!RealRun) return new RunRecords.Result(records.DeepestFloor, records.BestRooms, false, false, records.Wins, records.BestWin, false);
            int visited = 0;
            for (int i = 0; i < _run.dungeon.AllRooms.Count; i++) if (_run.dungeon.AllRooms[i].Visited) visited++;
            return records.Record(_run.floor, visited, _run.state == "won", (float)(_run.ticks / (double)Balance.TickHz));
        }

        const float BannerS = 3;
        /// <summary>The floor heading's envelope: in over a quarter second, held, out over the last 0.6 s.</summary>
        public static float BannerAlpha(float left) => left <= 0 ? 0 : Mathf.Clamp01(Mathf.Min((BannerS - left) / 0.25f, left / 0.6f));

        // The developer line (F10): its text is rebuilt only when what it says changes.
        void UpdateDev()
        {
            if (!_devOn) { _hud.SetDev(false, null); return; }
            var room = _run.CurrentRoom;
            long key = ((long)_run.rootSeed << 24) ^ ((Curve.On ? 1L : 0) << 1) ^ ((_inLab ? 1L : 0) << 4) ^ ((_sandbox ? 1L : 0) << 9) ^ ((_bossRoom ? 1L : 0) << 10) ^ ((long)(room != null ? (int)room.Type : 7) << 11) ^ ((long)_run.floor << 14);
            string text = null;
            if (key != _devKey)
            {
                _devKey = key;
                string mode = RealRun ? "floor " + _run.floor + " - " + AreaRules.AreaForFloor(_run.floor) + "   room " + (room != null ? room.Type.ToString() : "?")
                                      : _inLab ? "The lab: every body and every egg form" : _bossRoom ? "Warden arena" : "Sandbox: every enemy, respawning";
                string pools = RealRun && _run.pools != null ? "\n" + Pools.Describe(_run.pools) : "";
                text = mode + "   seed " + Rng.Encode(_run.rootSeed) + "   curve " + (Curve.On ? "on" : "off") + pools +
                       "\n1-4 guns   right-click blast   " + _binds.Display("Blink") + " blink   " + _binds.Display("Active") + " item   R new run   F3 skip floor   F1 sandbox   F2 lab   B Warden arena   " + _binds.Display("Pause") + " pause   F10 this line";
            }
            _hud.SetDev(true, text);
        }

        // The simulation makes no sound; the view hears what changed since the last frame.
        void PlayEvents()
        {
            var p = _run.player;
            if (_run.shots > _lastShots) Depths.Unity.Audio.SoundEngine.Play("shot");
            if (_run.hits > _lastHits) Depths.Unity.Audio.SoundEngine.Play("hit");
            if (_run.kills > _lastKills) Depths.Unity.Audio.SoundEngine.Play("kill");
            // dmgTaken grows on every landed hit, so a hit is heard even on the tick the regen heart refills
            if (_run.dmgTaken > _lastDmg) Depths.Unity.Audio.SoundEngine.Play("hurt");
            if (_run.state != "playing" && _lastHp > 0 && p.hp <= 0) Depths.Unity.Audio.SoundEngine.Play("over");
            if (_run.state == "won" && !_wonSounded) { _wonSounded = true; Depths.Unity.Audio.SoundEngine.Play("door"); Depths.Unity.Audio.SoundEngine.Play("pickup", 0, 0.8f); }
            if (_run.picked > _lastPicked) Depths.Unity.Audio.SoundEngine.Play("pickup");
            if (_run.thuds > _lastThuds) { Depths.Unity.Audio.SoundEngine.Play("door", 0, 0.7f); _shake.Add(ScreenShake.Kick.Thud, _run.thuds - _lastThuds); }
            if (_run.booms > _lastBooms) _shake.Add(ScreenShake.Kick.Boom, _run.booms - _lastBooms);
            if (_run.dmgTaken > _lastDmg) _shake.Add(ScreenShake.Kick.Hurt);
            if (_run.kills > _lastKills) _shake.Add(ScreenShake.Kick.Kill, _run.kills - _lastKills);
            if (_run.shots > _lastShots) _shake.Add(ScreenShake.Kick.Shot, 1, ScreenShake.ShotWeight(Weapons.All[p.weaponIdx].CooldownS));
            if (_run.hits > _lastHits) _shake.Add(ScreenShake.Kick.Hit, _run.hits - _lastHits);
            if (_run.bossWarned && !_lastWarned) Depths.Unity.Audio.SoundEngine.Play("boss");
            // a long wind-up (the Sprayer, the Mortar, the egg) is heard as it starts: the tell is the warning
            int winding = 0;
            foreach (var e in _run.enemies)
                if (e.castT > 0 && (e.kind == BodyKind.Sprayer || e.kind == BodyKind.Mortar || e.kind == BodyKind.Egg)) winding++;
            if (winding > _lastWinding) Depths.Unity.Audio.SoundEngine.Play("tell");
            _lastShots = _run.shots; _lastHits = _run.hits; _lastKills = _run.kills; _lastHp = p.hp; _lastDmg = _run.dmgTaken;
            _lastPicked = _run.picked; _lastWarned = _run.bossWarned; _lastWinding = winding; _lastThuds = _run.thuds; _lastBooms = _run.booms;
        }

        // Screenshot mode: strafe and shoot at the nearest target, so a still frame shows the sim running.
        void DemoInput(ref Vector2 mv, ref bool fire, ref bool alt, ref Vector2 aim)
        {
            float before = _demoS;
            _demoS += _frameS;
            var p = _run.player;
            Enemy t = null;   // the nearest body that can be shot; a plain loop, so the measured demo allocates nothing
            double best = double.PositiveInfinity;
            foreach (var e in _run.enemies)
            {
                if (e.buried) continue;
                double d = (e.x - p.x) * (e.x - p.x) + (e.y - p.y) * (e.y - p.y);
                if (d < best) { best = d; t = e; }
            }
            if (t != null) aim = new Vector2((float)t.x, (float)t.y);
            mv = new Vector2((int)(_demoS / 0.667f) % 2 == 0 ? 1 : -1, 0);   // turn every 2/3 s
            var room = _run.CurrentRoom;
            if (RealRun && t == null && room != null)
            {
                // a quiet room: walk out through the first open door (screen y is up for the move action)
                foreach (var d in room.Doors)
                {
                    if (!Rooms.DoorPassable(_run, room, d)) continue;
                    var (dx, dy) = Rooms.DoorPoint(d);
                    double tx = dx + Math.Sign(dx - Balance.MidX) * 40, ty = dy + Math.Sign(dy - Balance.MidY) * 40;
                    mv = new Vector2(Math.Sign(Math.Round(tx - p.x)), -Math.Sign(Math.Round(ty - p.y)));
                    break;
                }
            }
            fire = t != null;
            _demoBlast |= before < 0.333f && _demoS >= 0.333f;   // one blast, a third of a second in
            alt = _demoBlast;
            if (before < 0.5f && _demoS >= 0.5f) p.weaponIdx = 1;
        }
    }
}
