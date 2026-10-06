using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Depths.Unity
{
    /// <summary>
    /// The playable sandbox: one room driven by Depths.Core at the game's 210 Hz tick. The player
    /// moves (TickPlayer), shoots (Weapons.Fire), shells resolve (TickProjectiles), and loot is
    /// picked up (TickRoom), and lungers hunt and lunge and shooters and gunners hold their standoff and cast (TickBodies) - all ported with parity. Other
    /// kinds join as their movement is ported. This class only reads input and draws.
    /// Esc: menu. 1-4: guns. R: a fresh room.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameView : MonoBehaviour
    {
        public InputActionAsset controls;

        const double StepMs = 1000.0 / 210;
        static readonly Vector2 Offset = new Vector2(640 - 400, 360 - 355);   // room centre to screen centre
        static readonly BodyKind[] DummyKinds = { BodyKind.Lunger, BodyKind.Shooter, BodyKind.Lunger, BodyKind.Gunner };

        RunState _run;
        InputAction _move, _cast, _blast, _pause;
        RoomPainter _painter;
        Label _hud;
        double _acc;
        int _respawnT;
        bool _demo;
        int _demoT;

        void Start()
        {
            new Bindings(controls, new PlayerPrefsStore());   // applies the player's saved rebinds
            var map = controls.FindActionMap("Gameplay", true);
            _move = map.FindAction("Move", true);
            _cast = map.FindAction("Cast", true);
            _blast = map.FindAction("Blast", true);
            _pause = map.FindAction("Pause", true);
            map.Enable();
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-depthsView");
            _demo = i >= 0 && i + 1 < args.Length && args[i + 1] == "game";
            NewRun();
        }

        void OnDestroy() { controls?.FindActionMap("Gameplay")?.Disable(); }

        void NewRun()
        {
            uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _run = new RunState(new Rng(seed)) { dungeon = Dungeon.FromSeed(new Rng(seed)) };
            _run.state = "playing";
            _run.readyT = 0; _run.fadeT = 0;
            var p = _run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.MidY + 120;
            p.hp = p.maxHp = 8;
            _run.enemies.Clear();
            for (int k = 0; k < DummyKinds.Length; k++) SpawnDummy(k);
        }

        void SpawnDummy(int k)
        {
            double x = Balance.RoomLeft + 110 + k * 160, y = Balance.RoomTop + 90 + (k % 2) * 60;
            var e = Enemy.Of(DummyKinds[k % DummyKinds.Length], x, y);
            e.flank = k * 2.399963229728653;   // the game's golden-angle flank cursor
            e.noticeTimer = 60 + k * 25;        // a beat before they move, as a spawn has
            e.shootCd = e.cdMin;                 // and before a ranged body's first cast
            _run.enemies.Add(e);
        }

        void Update()
        {
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
                _hud.style.left = 290;
                _hud.style.width = 720;
                root.Add(_hud);
            }
            var kb = Keyboard.current;
            if (_pause.WasPressedThisFrame()) { SceneManager.LoadScene("MainMenu"); return; }
            if (kb != null)
            {
                for (int w = 0; w < 4; w++) if (kb[Key.Digit1 + w].wasPressedThisFrame) _run.player.weaponIdx = w;
                if (kb.rKey.wasPressedThisFrame) NewRun();
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
            if (_demo) { DemoInput(ref mv, ref fire, ref aim); alt = _demoT == 20; }

            _acc += Math.Min(Time.unscaledDeltaTime * 1000.0, 250.0);
            while (_acc >= StepMs)
            {
                // keys are -1/0/1 per axis in the game, and screen y points down
                var input = new Input(Math.Sign(mv.x), -Math.Sign(mv.y), fire: fire, alt: alt, aimX: aim.x, aimY: aim.y);
                if (_run.state == "playing")
                {
                    TickOrder.TickPlayer(_run, input);
                    TickOrder.TickProjectiles(_run);
                    TickOrder.TickBodies(_run);
                    TickOrder.TickRoom(_run);
                }
                if (_run.enemies.Count == 0 && ++_respawnT > 420) { _respawnT = 0; for (int k = 0; k < DummyKinds.Length; k++) SpawnDummy(k); }
                _acc -= StepMs;
            }
            _painter.MarkDirtyRepaint();
            var pl = _run.player;
            _hud.text = Weapons.All[pl.weaponIdx].Name + "   HP " + pl.hp.ToString("0.#") + "/" + pl.maxHp.ToString("0") +
                        "   armour " + pl.armor.ToString("0.#") + "   shots " + _run.shots + "  hits " + _run.hits + "  kills " + _run.kills +
                        (_run.state == "playing" ? "" : "\nYOU DIED - R for a new room") +
                        "\nSandbox on the ported core: movement, guns, shells, lungers, shooters, gunners, loot.\nBrunch packs and the Warden join as they are ported." +
                        "\n\n1-4 guns    R new room    Esc menu";
        }

        // Screenshot mode: strafe and shoot at the nearest target, so a still frame shows the sim running.
        void DemoInput(ref Vector2 mv, ref bool fire, ref Vector2 aim)
        {
            _demoT++;
            var p = _run.player;
            var t = _run.enemies.OrderBy(e => (e.x - p.x) * (e.x - p.x) + (e.y - p.y) * (e.y - p.y)).FirstOrDefault();
            if (t != null) aim = new Vector2((float)t.x, (float)t.y);
            mv = new Vector2((_demoT / 40) % 2 == 0 ? 1 : -1, 0);
            fire = true;
            if (_demoT == 30) p.weaponIdx = 1;
        }
    }

    /// <summary>Draws the room, the bodies, the shells, the loot and the player with Painter2D.</summary>
    sealed class RoomPainter : VisualElement
    {
        readonly Func<RunState> _run;
        static readonly Color Floor = new Color32(20, 23, 32, 255), Wall = new Color32(122, 86, 50, 255),
            PlayerC = new Color32(199, 155, 255, 255), Hostile = new Color32(255, 77, 77, 255);

        public RoomPainter(Func<RunState> run)
        {
            _run = run;
            generateVisualContent += Paint;
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

            foreach (var pk in run.pickups)
                Disc(g, W(pk.x, pk.y), (float)pk.r * 0.7f, pk.kind == "heart" ? (Color)new Color32(230, 70, 90, 255) : new Color32(120, 170, 230, 255));

            foreach (var e in run.enemies)
            {
                var c = W(e.x, e.y);
                Disc(g, c, (float)e.r, e.hitFlash > 0 ? Color.white : BodyColor(e.kind));
                float frac = (float)Math.Max(0, e.hp / Math.Max(1e-9, e.maxHp)), w = (float)e.r * 2, top = (float)e.r + 9;
                Rect(g, c + new Vector2(-w / 2, -top), c + new Vector2(w / 2, -top + 4), new Color(0, 0, 0, 0.6f));
                Rect(g, c + new Vector2(-w / 2, -top), c + new Vector2(-w / 2 + w * frac, -top + 4), new Color32(94, 226, 122, 255));
            }

            foreach (var p in run.projectiles)
            {
                Color col = Hostile;
                if (p.friendly && !ColorUtility.TryParseHtmlString(p.color, out col)) col = Color.white;
                Disc(g, W(p.x, p.y), (float)p.r, col);
            }

            var pl = run.player;
            bool flicker = pl.iframes > 0 && (pl.iframes / 14) % 2 == 0;
            Disc(g, W(pl.x, pl.y), (float)pl.r, flicker ? new Color(1, 1, 1, 0.5f) : PlayerC);
        }
    }
}
