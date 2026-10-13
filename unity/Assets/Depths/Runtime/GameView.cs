using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Depths.Unity.Hud;

namespace Depths.Unity
{
    /// <summary>
    /// The game, on Depths.Core at its tick rate (Balance.TickHz, 60 Hz): a seeded run of rooms, doors, keys, waves,
    /// the Warden and the way down (TickOrder.Update), all ported with parity. F1 swaps to the sandbox
    /// (one room of every enemy), B to a Warden arena. This class only reads input and draws.
    /// Esc: menu. 1-4: guns. R: a new run (or room).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameView : MonoBehaviour
    {
        public InputActionAsset controls;

        static readonly Vector2 Offset = new Vector2(640 - 400, 360 - 355);   // room centre to screen centre
        static readonly BodyKind[] DummyKinds = { BodyKind.Lunger, BodyKind.Shooter, BodyKind.Lunger, BodyKind.Gunner };

        RunState _run;
        InputAction _move, _cast, _blast, _blink, _active, _pause;
        int _lastShots, _lastHits, _lastKills;
        double _lastHp, _lastDmg;
        WorldView _world;
        LedgerHud _hud;
        bool _devOn;
        long _devKey = long.MinValue;
        readonly TickClock _clock = new TickClock();
        int _respawnT;
        bool _demo;
        float _demoS;   // seconds of demo input so far: the demo is timed, not counted in frames
        bool _demoBlast;   // held until a tick takes it: at 150 fps most frames step no tick

        /// <summary>For the screenshot checks: the run, the clock's ticks, and a freeze that stops ticking without an
        /// overlay (input between ticks - blink, Q, keys - still reaches the run).</summary>
        public RunState Run => _run;
        public long Ticks => _clock.Ticks;
        public bool Frozen { get; set; }
        /// <summary>Esc's pause, settable so the screenshot mode can show the pause page.</summary>
        public bool Paused { get => _paused; set => _paused = value; }
        public LedgerHud Hud => _hud;

        void Start()
        {
            new Bindings(controls, new PlayerPrefsStore());   // applies the player's saved rebinds
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
            _demo = i >= 0 && i + 1 < args.Length && (args[i + 1] == "game" || args[i + 1] == "boss" || args[i + 1] == "summary" || args[i + 1] == "pause");
            _bossRoom = i >= 0 && i + 1 < args.Length && args[i + 1] == "boss";
            _sandbox = i >= 0 && i + 1 < args.Length && args[i + 1] == "sandbox";
            _demo |= _sandbox;
            NewRun();
        }

        void OnDestroy()
        {
            controls?.FindActionMap("Gameplay")?.Disable();
            Depths.Unity.Audio.MusicEngine.SetFight(false);   // back to the menu: the calm layer only
        }

        bool _bossRoom, _sandbox;
        bool RealRun => !_sandbox && !_bossRoom;

        void NewRun()
        {
            new GameplaySettings(new PlayerPrefsStore()).Apply();   // the curve is chosen per run, in Options
            uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            if (RealRun)
            {
                _run = new RunState(new Rng(seed));
                _run.Start(seed);
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
                // the Warden, spawned the way the game spawns it (its own draws, its kit)
                var w = Spawn.Body(_run, BodyKind.Boss, Balance.MidX, Balance.RoomTop + 120);
                w.noticeTimer = Balance.Sec(0.43);
                _run.enemies.Add(w);
                return;
            }
            for (int k = 0; k < DummyKinds.Length; k++) SpawnDummy(k);
            SpawnPack();
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

        void Update()
        {
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
            }
            // Esc pauses, as in the game; M from the pause (or after a death) goes back to the menu
            if (_pause.WasPressedThisFrame() && _run.state == "playing") _paused = !_paused;
            if ((_paused || _run.state != "playing") && kb != null && kb.mKey.wasPressedThisFrame) { SceneManager.LoadScene("MainMenu"); return; }
            if (kb != null)
            {
                for (int w = 0; w < 4; w++) if (kb[Key.Digit1 + w].wasPressedThisFrame) _run.player.weaponIdx = w;
                if (kb.rKey.wasPressedThisFrame) { NewRun(); _paused = false; _lastFloor = 1; }
                if (kb.f3Key.wasPressedThisFrame) _devOn = !_devOn;
                if (kb.f1Key.wasPressedThisFrame) { _sandbox = !_sandbox; _bossRoom = false; NewRun(); }
                if (kb.bKey.wasPressedThisFrame) { _bossRoom = !_bossRoom; _sandbox = false; NewRun(); }
                if (kb.vKey.wasPressedThisFrame)   // cycle the Brunch guard rule under test
                    Balance.BrunchVariant = Balance.BrunchVariant == "A" ? "B" : Balance.BrunchVariant == "B" ? "A+" : "A";
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

            // the blink is a key press, so it lands between ticks, as in the game
            if (_blink.WasPressedThisFrame() && TickOrder.TryBlink(_run, new Input(Math.Sign(mv.x), -Math.Sign(mv.y), aimX: aim.x, aimY: aim.y)))
                Depths.Unity.Audio.SoundEngine.Play("blink");

            // Q uses the active item, between ticks like the game's key handler
            if (_active.WasPressedThisFrame() && _run.state == "playing" && Items.UseActive(_run))
                Depths.Unity.Audio.SoundEngine.Play("pickup");

            for (int n = _clock.Advance(Time.unscaledDeltaTime, _paused || Frozen); n > 0; n--)
            {
                // keys are -1/0/1 per axis in the game, and screen y points down
                var input = new Input(Math.Sign(mv.x), -Math.Sign(mv.y), fire: fire, alt: alt, aimX: aim.x, aimY: aim.y);
                _demoBlast = false;
                if (RealRun) TickOrder.Update(_run, input);   // gates, transitions, then player, shells, bodies, room
                else if (_run.state == "playing")
                {
                    TickOrder.TickPlayer(_run, input);
                    _run.trans = null;   // the sandbox is one room: a door leads nowhere
                    TickOrder.TickProjectiles(_run);
                    TickOrder.TickBodies(_run);
                    TickOrder.TickRoom(_run);
                    if (_run.enemies.Count == 0 && ++_respawnT > Balance.Sec(2)) { _respawnT = 0; if (!_bossRoom) { for (int k = 0; k < DummyKinds.Length; k++) SpawnDummy(k); SpawnPack(); if (_run.CurrentRoom != null) _run.CurrentRoom.Cleared = false; } }   // each wave won pays as a real room does: regen refill + half blink
                }
            }
            PlayEvents();
            Depths.Unity.Audio.MusicEngine.SetArea(AreaRules.AreaForFloor(_run.floor));
            Depths.Unity.Audio.MusicEngine.SetFight(_run.state == "playing" && !_paused && _run.enemies.Count > 0);
            if (_run.floor != _lastFloor) { _lastFloor = _run.floor; _bannerS = BannerS; Depths.Unity.Audio.SoundEngine.Play("door"); }
            if (_bannerS > 0) _bannerS -= Time.unscaledDeltaTime;
            _world.Aim = aim;
            _world.Step(_paused || Frozen || _run.state != "playing" ? 0 : Time.unscaledDeltaTime);
            _hud.Refresh(_run, Time.unscaledDeltaTime);
            _hud.Overlays.SetPause(_paused && _run.state == "playing", _run);
            _hud.Overlays.SetSummary(_run.state != "playing", _run);
            _hud.Overlays.SetBanner(_run.floor, BannerAlpha(_bannerS));
            _hud.Overlays.SetWarning(HudOverlays.WarningAlpha(_run));
            UpdateDev();
        }

        const float BannerS = 3;
        /// <summary>The floor heading's envelope: in over a quarter second, held, out over the last 0.6 s.</summary>
        public static float BannerAlpha(float left) => left <= 0 ? 0 : Mathf.Clamp01(Mathf.Min((BannerS - left) / 0.25f, left / 0.6f));

        // The developer line (F3): its text is rebuilt only when what it says changes.
        void UpdateDev()
        {
            if (!_devOn) { _hud.SetDev(false, null); return; }
            var room = _run.CurrentRoom;
            long key = ((long)_run.rootSeed << 24) ^ ((Curve.On ? 1L : 0) << 1) ^ ((long)Balance.BrunchVariant.GetHashCode() << 4) ^ ((_sandbox ? 1L : 0) << 9) ^ ((_bossRoom ? 1L : 0) << 10) ^ ((long)(room != null ? (int)room.Type : 7) << 11) ^ ((long)_run.floor << 14);
            string text = null;
            if (key != _devKey)
            {
                _devKey = key;
                string mode = RealRun ? "floor " + _run.floor + " - " + AreaRules.AreaForFloor(_run.floor) + "   room " + (room != null ? room.Type.ToString() : "?")
                                      : _bossRoom ? "Warden arena" : "Sandbox: every enemy, respawning";
                text = mode + "   seed " + Rng.Encode(_run.rootSeed) + "   curve " + (Curve.On ? "on" : "off") + "   Brunch rule " + Balance.BrunchVariant + " (V)" +
                       "\n1-4 guns   right-click blast   Shift blink   Q item   R new run   F1 sandbox   B Warden arena   Esc pause   F3 this line";
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
            _lastShots = _run.shots; _lastHits = _run.hits; _lastKills = _run.kills; _lastHp = p.hp; _lastDmg = _run.dmgTaken;
        }

        // Screenshot mode: strafe and shoot at the nearest target, so a still frame shows the sim running.
        void DemoInput(ref Vector2 mv, ref bool fire, ref bool alt, ref Vector2 aim)
        {
            float before = _demoS;
            _demoS += Time.unscaledDeltaTime;
            var p = _run.player;
            var t = _run.enemies.OrderBy(e => (e.x - p.x) * (e.x - p.x) + (e.y - p.y) * (e.y - p.y)).FirstOrDefault();
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
