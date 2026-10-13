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
            if (_painter == null)
            {
                var root = GetComponent<UIDocument>().rootVisualElement?.Q("gameRoot");
                if (root == null || root.panel == null) return;   // the document attaches late; see the history note
                root.Clear();
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
            }
            // Esc pauses, as in the game; M from the pause (or after a death) goes back to the menu
            if (_pause.WasPressedThisFrame() && _run.state == "playing") _paused = !_paused;
            if ((_paused || _run.state != "playing") && kb != null && kb.mKey.wasPressedThisFrame) { SceneManager.LoadScene("MainMenu"); return; }
            if (kb != null)
            {
                for (int w = 0; w < 4; w++) if (kb[Key.Digit1 + w].wasPressedThisFrame) _run.player.weaponIdx = w;
                if (kb.rKey.wasPressedThisFrame) { NewRun(); _paused = false; _lastFloor = 1; }
                if (kb.f1Key.wasPressedThisFrame) { _sandbox = !_sandbox; _bossRoom = false; NewRun(); }
                if (kb.bKey.wasPressedThisFrame) { _bossRoom = !_bossRoom; _sandbox = false; NewRun(); }
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
                aim = pp - Offset;
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
            if (_run.floor != _lastFloor) { _lastFloor = _run.floor; _bannerS = 3; Depths.Unity.Audio.SoundEngine.Play("door"); }
            if (_bannerS > 0) _bannerS -= Time.unscaledDeltaTime;
            _painter.Overlay = _paused ? "PAUSED\n\nEsc resume     M menu"
                : _run.state != "playing" ? "THE DEPTHS TAKE YOU\n\nfloor " + _run.floor + "   kills " + _run.kills + "   " + (_run.ticks / Balance.TickHz / 60) + ":" + (_run.ticks / Balance.TickHz % 60).ToString("00") + "\nseed " + Rng.Encode(_run.rootSeed) + "\n\nR new run     M menu"
                : _bannerS > 0 ? "FLOOR " + _run.floor + "\n" + AreaRules.AreaForFloor(_run.floor) : null;
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
                            "\n" + (_bossRoom ? "Warden arena" : "Sandbox: every enemy, respawning") + " on the ported core.   Brunch rule " + Balance.BrunchVariant + " (V)" +
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
        static readonly Color Floor = new Color32(20, 23, 32, 255), Wall = new Color32(122, 86, 50, 255),
            PlayerC = new Color32(199, 155, 255, 255), Hostile = new Color32(255, 77, 77, 255);

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

        static Vector2 W(double x, double y) => new Vector2((float)x + 240, (float)y + 5);

        static Color BodyColor(BodyKind k)
        {
            switch (k)
            {
                case BodyKind.Lunger: return new Color32(214, 120, 80, 255);
                case BodyKind.Brunch: return new Color32(230, 200, 90, 255);
                case BodyKind.Shooter: return new Color32(220, 80, 90, 255);
                case BodyKind.Gunner: return new Color32(240, 160, 60, 255);
                default: return new Color32(180, 60, 60, 255);
            }
        }

        static Color PickupColor(string kind)
        {
            switch (kind)
            {
                case "heart": return new Color32(230, 70, 90, 255);
                case "armor": return new Color32(120, 170, 230, 255);
                case "key": return new Color32(205, 210, 220, 255);
                case "goldkey": return new Color32(235, 195, 70, 255);
                case "exit": return new Color32(160, 110, 255, 255);
                case "weapon": return new Color32(90, 200, 255, 255);
                case "item": return new Color32(240, 200, 120, 255);
                default: return new Color32(80, 220, 190, 255);
            }
        }

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
            var a = W(Balance.RoomLeft, Balance.RoomTop);
            var b = W(Balance.RoomRight, Balance.RoomBottom);
            Rect(g, a - new Vector2(6, 6), b + new Vector2(6, 6), Wall);
            Rect(g, a, b, Floor);
            var room = run.CurrentRoom;
            if (room != null)
                foreach (var d in room.Doors)
                {
                    // open: floor; sealed: the colour of what is behind it; shut by a fight: bars
                    Color c = Rooms.DoorPassable(run, room, d) ? Floor
                        : Rooms.DoorSealed(run, room, d) ? (run.dungeon.Neighbour(room, d)?.Type == RoomKind.Boss ? new Color32(179, 65, 47, 255) : new Color32(192, 138, 82, 255))
                        : new Color32(72, 80, 95, 255);
                    float h = Balance.DoorWidth / 2f;
                    var (dx, dy) = Rooms.DoorPoint(d);
                    var cp = W(dx, dy);
                    bool vertical = d == Dir.E || d == Dir.W;
                    Rect(g, cp - (vertical ? new Vector2(7, h) : new Vector2(h, 7)), cp + (vertical ? new Vector2(7, h) : new Vector2(h, 7)), c);
                }
            DrawMinimap(g, run);

            foreach (var pk in run.pickups)
            {
                // placeholder sprites: hearts red, armour gray, a half pickup is the left half
                bool half = pk.kind == "halfheart" || pk.kind == "halfarmor";
                if (pk.kind == "heart" || pk.kind == "halfheart") Heart(g, W(pk.x, pk.y), (float)pk.r, HeartRed, half);
                else if (pk.kind == "armor" || pk.kind == "halfarmor") Heart(g, W(pk.x, pk.y), (float)pk.r, ArmourGray, half);
                else Disc(g, W(pk.x, pk.y), (float)pk.r * 0.7f, PickupColor(pk.kind));
            }

            foreach (var e in run.enemies)
            {
                var c = W(e.x, e.y);
                Disc(g, c, (float)e.r, e.hitFlash > 0 ? Color.white : BodyColor(e.kind));
                if (e.castT > 0)
                {
                    // the tell: a ring that closes on the body as the shot comes
                    float k = (float)e.castT / Balance.CastTime;
                    g.strokeColor = new Color(1f, 0.6f, 0.3f, 0.9f);
                    g.lineWidth = 2;
                    g.BeginPath();
                    g.Arc(c, (float)e.r + 4 + 18 * k, 0, 360);
                    g.Stroke();
                }
                float frac = (float)Math.Max(0, e.hp / Math.Max(1e-9, e.maxHp)), w = (float)e.r * 2, top = (float)e.r + 9;
                Rect(g, c + new Vector2(-w / 2, -top), c + new Vector2(w / 2, -top + 4), new Color(0, 0, 0, 0.6f));
                Rect(g, c + new Vector2(-w / 2, -top), c + new Vector2(-w / 2 + w * frac, -top + 4), new Color32(94, 226, 122, 255));
            }

            foreach (var hf in run.hookFields)
            {
                // the hook's ground spell, fading as it runs out
                g.strokeColor = new Color(0.16f, 0.44f, 0.77f, 0.25f + 0.6f * hf.life / hf.max);
                g.lineWidth = 2;
                g.BeginPath();
                g.Arc(W(hf.x, hf.y), (float)hf.r, 0, 360);
                g.Stroke();
            }

            foreach (var p in run.projectiles)
            {
                Color col = Hostile;
                if (p.friendly && !ColorUtility.TryParseHtmlString(p.color, out col)) col = Color.white;
                Disc(g, W(p.x, p.y), (float)p.r, col);
            }

            var pl = run.player;
            if (run.roomFade > 0.01) Rect(g, a - new Vector2(6, 6), b + new Vector2(6, 6), new Color(0.04f, 0.05f, 0.07f, (float)run.roomFade));
            bool flicker = pl.iframes > 0 && (pl.iframes / Math.Max(1, Balance.Sec(0.067))) % 2 == 0;   // IFRAME_FLICKER
            Disc(g, W(pl.x, pl.y), (float)pl.r, flicker ? new Color(1, 1, 1, 0.5f) : PlayerC);
            DrawHearts(g, pl);
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
