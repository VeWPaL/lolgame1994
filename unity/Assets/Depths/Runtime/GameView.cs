using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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
        RoomPainter _painter;
        WorldView _world;
        Label _hud;
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
        public bool Frozen { get; set; }

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
            _demo = i >= 0 && i + 1 < args.Length && (args[i + 1] == "game" || args[i + 1] == "boss");
            _bossRoom = i >= 0 && i + 1 < args.Length && args[i + 1] == "boss";
            _sandbox = i >= 0 && i + 1 < args.Length && args[i + 1] == "sandbox";
            bool labRow = i >= 0 && i + 1 < args.Length && args[i + 1] == "labrow";   // the row, frozen and unshot: every sprite
            _inLab = labRow || (i >= 0 && i + 1 < args.Length && args[i + 1] == "lab");
            _demo |= _sandbox || (_inLab && !labRow);
            NewRun();
            if (_inLab && !labRow) _lab.SetFrozen(false, _run);   // the screenshot wants the row at work
        }

        void OnDestroy()
        {
            controls?.FindActionMap("Gameplay")?.Disable();
            Depths.Unity.Audio.MusicEngine.SetFight(false);   // back to the menu: the calm layer only
        }

        bool _bossRoom, _sandbox, _inLab;
        readonly LabMode _lab = new LabMode();
        bool RealRun => !_sandbox && !_bossRoom && !_inLab;

        void LeaveLab() { if (_inLab) { _lab.Leave(); _inLab = false; } }

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
            if (_inLab) { _lab.Enter(_run); return; }
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

        // This frame's seconds, from the wall clock rather than Time.unscaledDeltaTime, so the deltas sum to
        // real time by construction. Once, with three other Unity editors running, the player measured
        // 61.65 Hz; the cause is not identified (CPU load alone matches the wall clock), and the check now
        // logs Unity's own delta sum next to the wall clock so a repeat says which side drifted.
        double _lastFrameAt = -1;
        float _frameS;

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            _frameS = _lastFrameAt < 0 ? 0 : (float)(now - _lastFrameAt);
            _lastFrameAt = now;
            var kb = Keyboard.current;
            if (_painter == null)
            {
                var root = GetComponent<UIDocument>().rootVisualElement?.Q("gameRoot");
                if (root == null || root.panel == null) return;   // the document attaches late; see the history note
                root.Clear();
                _world = new WorldView(() => _run);
                _world.style.position = Position.Absolute;
                _world.style.left = 0; _world.style.top = 0; _world.style.right = 0; _world.style.bottom = 0;
                root.Add(_world);
                _painter = new RoomPainter(() => _run);
                _painter.style.position = Position.Absolute;
                _painter.style.left = 0; _painter.style.top = 0; _painter.style.right = 0; _painter.style.bottom = 0;
                root.Add(_painter);
                _hud = new Label();
                _hud.AddToClassList("game-text");
                _hud.style.top = 600;   // below the room, which fills the middle of the screen
                _hud.style.left = 190;
                _hud.style.width = 900;
                root.Add(_hud);
                _lab.Attach(root);
            }
            // Esc pauses, as in the game; M from the pause (or after a death) goes back to the menu
            if (_pause.WasPressedThisFrame() && _run.state == "playing") _paused = !_paused;
            if ((_paused || _run.state != "playing") && kb != null && kb.mKey.wasPressedThisFrame) { SceneManager.LoadScene("MainMenu"); return; }
            if (kb != null)
            {
                for (int w = 0; w < 4; w++) if (kb[Key.Digit1 + w].wasPressedThisFrame) _run.player.weaponIdx = w;
                if (kb.rKey.wasPressedThisFrame) { NewRun(); _paused = false; _lastFloor = 1; }
                if (kb.f1Key.wasPressedThisFrame) { _sandbox = !_sandbox; _bossRoom = false; LeaveLab(); NewRun(); }
                if (kb.bKey.wasPressedThisFrame) { _bossRoom = !_bossRoom; _sandbox = false; LeaveLab(); NewRun(); }
                if (kb.f2Key.wasPressedThisFrame) { bool enter = !_inLab; _sandbox = _bossRoom = false; LeaveLab(); _inLab = enter; NewRun(); _paused = false; }
                if (kb.vKey.wasPressedThisFrame)   // cycle the Brunch guard rule under test
                    Balance.BrunchVariant = Balance.BrunchVariant == "A" ? "B" : Balance.BrunchVariant == "B" ? "A+" : "A";
            }

            // the aim: the pointer, from screen pixels to panel units to the room
            Vector2 aim = new Vector2((float)_run.player.x + 200, (float)_run.player.y);
            var mouse = Mouse.current;
            if (mouse != null && _painter.panel != null)
            {
                var sp = mouse.position.ReadValue();
                var pp = RuntimePanelUtils.ScreenToPanel(_painter.panel, new Vector2(sp.x, Screen.height - sp.y));
                aim = pp - WorldView.Origin;
            }
            Vector2 mv = _move.ReadValue<Vector2>();
            bool fire = _cast.IsPressed(), alt = _blast.IsPressed();
            if (_demo) DemoInput(ref mv, ref fire, ref alt, ref aim);
            if (_inLab) _lab.Keys(kb, _run, aim);

            // the blink is a key press, so it lands between ticks, as in the game
            if (_blink.WasPressedThisFrame() && TickOrder.TryBlink(_run, new Input(Math.Sign(mv.x), -Math.Sign(mv.y), aimX: aim.x, aimY: aim.y)))
                Depths.Unity.Audio.SoundEngine.Play("blink");

            // Q uses the active item, between ticks like the game's key handler
            if (_active.WasPressedThisFrame() && _run.state == "playing" && Items.UseActive(_run))
                Depths.Unity.Audio.SoundEngine.Play("pickup");

            for (int n = _clock.Advance(_frameS, _paused || Frozen); n > 0; n--)
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
                    if (!_inLab && _run.enemies.Count == 0 && ++_respawnT > Balance.Sec(2)) { _respawnT = 0; if (!_bossRoom) { for (int k = 0; k < DummyKinds.Length; k++) SpawnDummy(k); SpawnPack(); if (_run.CurrentRoom != null) _run.CurrentRoom.Cleared = false; } }   // each wave won pays as a real room does: regen refill + half blink
                }
            }
            _lab.AfterTicks(_run, _paused || Frozen ? 0 : _frameS);
            _lab.Draw();
            PlayEvents();
            Depths.Unity.Audio.MusicEngine.SetArea(AreaRules.AreaForFloor(_run.floor));
            Depths.Unity.Audio.MusicEngine.SetFight(_run.state == "playing" && !_paused && _run.enemies.Count > 0);
            if (_run.floor != _lastFloor) { _lastFloor = _run.floor; _bannerS = 3; Depths.Unity.Audio.SoundEngine.Play("door"); }
            if (_bannerS > 0) _bannerS -= _frameS;
            _painter.Overlay = _paused ? "PAUSED\n\nEsc resume     M menu"
                : _run.state != "playing" ? "THE DEPTHS TAKE YOU\n\nfloor " + _run.floor + "   kills " + _run.kills + "   " + (_run.ticks / Balance.TickHz / 60) + ":" + (_run.ticks / Balance.TickHz % 60).ToString("00") + "\nseed " + Rng.Encode(_run.rootSeed) + "\n\nR new run     M menu"
                : _bannerS > 0 ? "FLOOR " + _run.floor + "\n" + AreaRules.AreaForFloor(_run.floor) : null;
            _world.Aim = aim;
            _world.Step(_paused || Frozen || _run.state != "playing" ? 0 : _frameS);
            _painter.MarkDirtyRepaint();
            var pl = _run.player;
            var act = Items.ActiveItem(_run);
            string build = (act != null ? "   Q: " + Items.Def(act.Id).Name + (act.Charges == int.MaxValue ? "" : " x" + act.Charges) : "") +
                           (_run.loadout.Count > (act != null ? 1 : 0) ? "   carrying: " + string.Join(", ", _run.loadout.Where(s => s.Slot != Items.ActiveSlot).Select(s => Items.Def(s.Id).Name)) : "");
            string stats = Weapons.All[pl.weaponIdx].Name + "   HP " + pl.hp.ToString("0.#") + "/" + pl.maxHp.ToString("0") +
                           (pl.regenHeartMax > 0 ? " + regen " + pl.regenHeart.ToString("0") + "/" + pl.regenHeartMax.ToString("0") : "") +
                           "   armour " + pl.armor.ToString("0.#") + "   blinks " + _run.blinkCharges + "   kills " + _run.kills + build;
            if (RealRun)
            {
                var room = _run.CurrentRoom;
                _hud.text = stats + "   " + (pl.hasSilver ? "[silver key] " : "") + (pl.hasGold ? "[gold key] " : "") +
                            (_run.state == "playing" ? "" : "\nYOU DIED on floor " + _run.floor + " - R for a new run") +
                            "\nFloor " + _run.floor + " - " + AreaRules.AreaForFloor(_run.floor) + "   room: " + (room != null ? room.Type.ToString() : "?") +
                            "   seed " + Rng.Encode(_run.rootSeed) + "   curve " + (Curve.On ? "on" : "off") + "   Brunch rule " + Balance.BrunchVariant + " (V)" +
                            "\n\n1-4 guns   right-click blast   Shift blink   R new run   F1 sandbox   B Warden arena   Esc pause";
            }
            else
                _hud.text = stats + (_run.state == "playing" ? "" : "\nYOU DIED - R for a new room") +
                            "\n" + (_inLab ? "The lab: every body, the five middle ones too (not in runs yet)" : _bossRoom ? "Warden arena" : "Sandbox: every enemy, respawning") + " on the ported core.   Brunch rule " + Balance.BrunchVariant + " (V)" +
                            "\n\n1-4 guns   right-click blast   Shift blink   R new room   F1 real run   B Warden arena   Esc pause";
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

    /// <summary>Draws the room, the bodies, the shells, the loot and the player with Painter2D.</summary>
    sealed class RoomPainter : VisualElement
    {
        readonly Func<RunState> _run;
        readonly Label _overlay = new Label();
        public string Overlay
        {
            set
            {
                _overlay.text = value ?? "";
                _overlay.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
        public RoomPainter(Func<RunState> run)
        {
            _run = run;
            generateVisualContent += Paint;
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 290; _overlay.style.top = 250; _overlay.style.width = 700;
            _overlay.style.unityTextAlign = TextAnchor.MiddleCenter;
            _overlay.style.fontSize = 28;
            _overlay.style.color = new Color(0.91f, 0.91f, 0.93f);
            _overlay.style.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 0.82f);
            _overlay.style.paddingTop = 18; _overlay.style.paddingBottom = 18;
            _overlay.style.display = DisplayStyle.None;
            Add(_overlay);
        }

        static Vector2 W(double x, double y) => WorldView.ToPanel(x, y);

        // the floor so far: rooms walked, the current one bright, top-right of the screen
        static void DrawMinimap(Painter2D g, RunState run)
        {
            if (run.dungeon == null) return;
            const float cell = 14, x0 = 1060, y0 = 140;
            foreach (var r in run.dungeon.AllRooms)
            {
                if (!r.Visited) continue;
                bool here = r.X == run.curX && r.Y == run.curY;
                Color c = here ? new Color32(232, 232, 236, 255) : r.Type == RoomKind.Boss ? new Color32(179, 65, 47, 255)
                        : r.Type == RoomKind.Item ? new Color32(192, 138, 82, 255) : new Color32(72, 80, 95, 255);
                var p = new Vector2(x0 + r.X * cell, y0 + r.Y * cell);
                Rect(g, p, p + new Vector2(cell - 3, cell - 3), c);
            }
        }

        static void Disc(Painter2D g, Vector2 c, float r, Color col)
        {
            g.fillColor = col;
            g.BeginPath();
            g.Arc(c, r, 0, 360);
            g.Fill();
        }

        static void Rect(Painter2D g, Vector2 a, Vector2 b, Color col)
        {
            g.fillColor = col;
            g.BeginPath();
            g.MoveTo(a); g.LineTo(new Vector2(b.x, a.y)); g.LineTo(b); g.LineTo(new Vector2(a.x, b.y)); g.ClosePath();
            g.Fill();
        }

        void Paint(MeshGenerationContext ctx)
        {
            var run = _run();
            if (run == null) return;
            var g = ctx.painter2D;
            // interim: the minimap and the heart row, until the HUD replaces them (the room is WorldView)
            DrawMinimap(g, run);
            DrawHearts(g, run.player);
        }

        // pickups share the row's colours (HeartRow)
        static readonly Color HeartRed = HeartRow.Red, ArmourGray = HeartRow.Armour;

        // a heart of half-width s centred on c: two lobes and a point; half = the left half only
        static void Heart(Painter2D g, Vector2 c, float s, Color col, bool half = false)
        {
            float ly = c.y - s * 0.3f, r = s * 0.5f;
            Disc(g, new Vector2(c.x - r, ly), r, col);
            g.fillColor = col;
            g.BeginPath();
            g.MoveTo(new Vector2(c.x - s, ly)); g.LineTo(new Vector2(c.x, ly)); g.LineTo(new Vector2(c.x, c.y + s));
            g.ClosePath(); g.Fill();
            if (half) return;
            Disc(g, new Vector2(c.x + r, ly), r, col);
            g.BeginPath();
            g.MoveTo(new Vector2(c.x, ly)); g.LineTo(new Vector2(c.x + s, ly)); g.LineTo(new Vector2(c.x, c.y + s));
            g.ClosePath(); g.Fill();
        }

        /// <summary>The first heart's centre in panel units, just above the room's top-left corner.</summary>
        public static Vector2 FirstHeart => W(Balance.RoomLeft, Balance.RoomTop) + new Vector2(HeartRow.Size, -34);

        readonly System.Collections.Generic.List<HeartSlot> _row = new System.Collections.Generic.List<HeartSlot>();   // reused: no per-frame allocation

        // the health row above the room; the regenerating heart is rose-violet, the red's family a
        // step toward purple, so it reads as a different kind of heart without shouting
        void DrawHearts(Painter2D g, Player pl)
        {
            foreach (var h in HeartRow.Layout(pl, FirstHeart, _row))
            {
                var unlit = HeartRow.Unlit(h.Kind);
                if (unlit.HasValue) Heart(g, h.Centre, HeartRow.Size, unlit.Value);
                if (h.Fill != HeartFill.Empty) Heart(g, h.Centre, HeartRow.Size, HeartRow.Lit(h.Kind), h.Fill == HeartFill.Half);
            }
        }
    }
}
