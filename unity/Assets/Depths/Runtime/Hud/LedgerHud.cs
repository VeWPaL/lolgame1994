using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Depths.Unity.Art;

namespace Depths.Unity.Hud
{
    /// <summary>The in-game HUD: two ledger boards standing beside the room. [h:LedgerHud-1]</summary>
    public sealed class LedgerHud : VisualElement
    {
        public static readonly string[] AreaNames = { "The Shallows", "The Kiln Works", "The Flooded Galleries", "The Warden's Deep" };

        readonly InkLayer _ink;

        /// <summary>The boss whose bar the band shows (null: no bar), and the name on its plate.</summary>
        public static Enemy BossShown(RunState run) => InkLayer.LiveBoss(run);
        public static string BossName(BodyKind k) => InkLayer.BossName(k);
        readonly VisualElement _bossPlate, _seedTag;
        readonly Label _dev;
        readonly List<Texture2D> _owned = new List<Texture2D>();
        Texture2D _seedTex;
        uint _seedOf; bool _seeded;
        ulong _sig;
        bool _low;

        public readonly HudOverlays Overlays;
        /// <summary>How many times the ink layer has been asked to repaint (the retained-mode tests count these).</summary>
        public int Repaints { get; private set; }
        public bool DevVisible => _dev.style.display == DisplayStyle.Flex;
        /// <summary>The seed the tag on the page was stamped with (tests read it).</summary>
        public uint SeedShown => _seedOf;

        /// <summary>Bakes everything the HUD needs that does not depend on the run: called from the menu, so
        /// pressing Start Game does not freeze on it (it was about a second, measured on the first game frame).</summary>
        public static void Prebake()
        {
            _ = SpriteSet.WhiteTexel;   // the sprite atlas, every region resolved
            LedgerArt.Kept("board-left", () => LedgerArt.Board(true));
            LedgerArt.Kept("board-right", () => LedgerArt.Board(false));
            LedgerArt.Kept("boss-plate", LedgerArt.BossPlate);
            HudOverlays.Prebake();
        }

        public LedgerHud()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;

            Add(Baked(LedgerArt.Kept("board-left", () => LedgerArt.Board(true)), HudLayout.Board(true), "board-left"));
            Add(Baked(LedgerArt.Kept("board-right", () => LedgerArt.Board(false)), HudLayout.Board(false), "board-right"));
            _bossPlate = Baked(LedgerArt.Kept("boss-plate", LedgerArt.BossPlate), HudLayout.BossPlate, "boss-plate");
            _bossPlate.style.display = DisplayStyle.None;
            Add(_bossPlate);
            _seedTag = new VisualElement { name = "seed-tag", pickingMode = PickingMode.Ignore };
            var st = HudLayout.SeedTag;
            Place(_seedTag, (int)st.x, (int)st.y, (int)st.width, (int)st.height);
            Add(_seedTag);

            _ink = new InkLayer(this);
            Add(_ink);

            _dev = new Label { pickingMode = PickingMode.Ignore };
            var dr = HudLayout.DevLine;
            Place(_dev, (int)dr.x, (int)dr.y, (int)dr.width, (int)dr.height);
            _dev.style.fontSize = 11;
            _dev.style.color = new Color(0.62f, 0.66f, 0.74f);
            _dev.style.whiteSpace = WhiteSpace.Normal;
            _dev.style.unityTextAlign = TextAnchor.UpperCenter;
            _dev.style.display = DisplayStyle.None;
            Add(_dev);

            Overlays = new HudOverlays();
            Add(Overlays);
            RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        static void Place(VisualElement e, int x, int y, int w, int h)
        {
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
        }

        VisualElement Baked(Img img, Rect at, string name)
        {
            var tex = Atlas.ToTexture(img, name);
            _owned.Add(tex);
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Place(e, (int)at.x, (int)at.y, (int)at.width, (int)at.height);
            e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }

        void Release()
        {
            foreach (var t in _owned) Atlas.Free(t);
            _owned.Clear();
            Atlas.Free(_seedTex);
            _seedTex = null;
            Overlays.Release();
        }

        /// <summary>The developer line (seed, curve, pools, keys): F10. The caller rebuilds the text only when it changes.</summary>
        public void SetDev(bool visible, string text)
        {
            var d = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_dev.style.display != d) _dev.style.display = d;
            if (visible && text != null && _dev.text != text) _dev.text = text;
        }

        internal RunState Run { get; private set; }
        internal float Time { get; private set; }
        internal bool Low => _low;

        /// <summary>Once a frame: reads the run, updates what changed, and asks for ink only if a drawn value moved.</summary>
        public void Refresh(RunState run, float dt)
        {
            Run = run;
            Time += dt;
            if (run == null || run.player == null) return;
            _low = IsLow(run.player);

            // the seed tag is the JS's luggage tag, stamped again only for a new seed
            if (!_seeded || run.rootSeed != _seedOf)
            {
                _seeded = true; _seedOf = run.rootSeed;
                Atlas.Free(_seedTex);
                var st = HudLayout.SeedTag;
                _seedTex = Atlas.ToTexture(LedgerArt.SeedTag((int)st.width, (int)st.height, Rng.Encode(run.rootSeed)), "seed-tag");
                _seedTag.style.backgroundImage = new StyleBackground(_seedTex);
            }

            bool boss = InkLayer.LiveBoss(run) != null;
            var bd = boss ? DisplayStyle.Flex : DisplayStyle.None;
            if (_bossPlate.style.display != bd) _bossPlate.style.display = bd;

            ulong sig = Signature(run, _low);
            if (sig != _sig)
            {
                _sig = sig;
                Repaints++;
                _ink.MarkDirtyRepaint();
            }
        }

        /// <summary>Low health: the last heart's worth of everything that stands between the player and death. [h:LedgerHud-2]</summary>
        public static bool IsLow(Player p) => p.hp > 0 && Math.Round(p.hp) + Math.Max(0, Math.Round(p.regenHeart)) + Math.Max(0, Math.Round(p.armor)) <= 2;

        // ---- the repaint signature: every value the ink layer draws, quantised to what a pixel can show ----
        static ulong Mix(ulong h, long v) { h ^= (ulong)v + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2); return h * 0x100000001B3UL; }

        ulong Signature(RunState run, bool low)
        {
            var p = run.player;
            ulong h = 1469598103934665603UL;
            h = Mix(h, (long)Math.Round(p.hp)); h = Mix(h, (long)p.maxHp); h = Mix(h, (long)Math.Round(p.regenHeart)); h = Mix(h, (long)p.regenHeartMax);
            h = Mix(h, (long)Math.Round(p.armor)); h = Mix(h, (p.hasSilver ? 1 : 0) | (p.hasGold ? 2 : 0) | (p.altMode == "hook" ? 4 : 0));
            h = Mix(h, run.blinkCharges); h = Mix(h, InkLayer.FillPx(InkLayer.BlinkFill(run), HudLayout.BlinkWell(0).width));
            h = Mix(h, InkLayer.FillPx(Mathf.Clamp01((float)p.momentum), HudLayout.MomentumWell.width));
            h = Mix(h, run.floor); h = Mix(h, run.kills);
            h = Mix(h, p.weaponIdx); h = Mix(h, InkLayer.ShadePx(InkLayer.WeaponReady(p))); h = Mix(h, InkLayer.ShadePx(InkLayer.AltReady(p)));
            var held = Items.ActiveItem(run);
            h = Mix(h, held == null ? 0 : held.Id.GetHashCode()); h = Mix(h, held?.Charges ?? -1);
            for (int i = 0; i < run.loadout.Count; i++) h = Mix(h, run.loadout[i].Id.GetHashCode());
            h = Mix(h, run.curX * 16 + run.curY); h = Mix(h, (run.bossUnlocked ? 1 : 0) | (run.itemUnlocked ? 2 : 0));
            bool bossOpen = false;
            if (run.dungeon != null)
            {
                var rooms = run.dungeon.AllRooms;
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    h = Mix(h, (r.Visited ? 1 : 0) | (r.Cleared ? 2 : 0) | (r.Spike != SpikeKind.None ? 4 : 0));
                    if (r.Visited) h = Mix(h, InkLayer.MapPickup(run, r));
                    bossOpen |= r.Visited && r.Type == RoomKind.Boss && !r.Cleared;
                }
            }
            var boss = InkLayer.LiveBoss(run);
            if (boss != null) { h = Mix(h, boss.phase); h = Mix(h, InkLayer.FillPx(Mathf.Clamp01((float)(boss.hp / boss.maxHp)), HudLayout.BossWell.width)); }
            // the two pulses, stepped finely enough to look smooth and coarsely enough to skip most frames
            h = Mix(h, low ? (long)(InkLayer.Pulse(Time, 9.6f) * 16) : -1);
            h = Mix(h, bossOpen ? (long)(InkLayer.Pulse(Time, 5.4f) * 10) : -1);
            return h;
        }

        // ================================================================================================

        /// <summary>The ink: everything on the pages that moves, as atlas quads in two textures.</summary>
        sealed class InkLayer : VisualElement
        {
            readonly LedgerHud _hud;
            readonly QuadBatch _q = new QuadBatch();
            readonly List<HeartSlot> _row = new List<HeartSlot>();
            readonly char[] _digits = new char[16];
            readonly Region[,] _hearts = new Region[3, 3];   // kind, fill (empty, half, full)
            readonly Region[] _keys = new Region[2];
            readonly Region[] _icons = new Region[6];
            readonly Region[] _mouseGun = new Region[4];
            readonly Region _mouseBlast, _mouseHook, _glow, _white, _disc;
            readonly Dictionary<string, Region> _item26 = new Dictionary<string, Region>(), _icon2 = new Dictionary<string, Region>(), _icon4 = new Dictionary<string, Region>();
            readonly Region[] _pick = new Region[5];
            readonly Dictionary<string, Color32> _itemInk = new Dictionary<string, Color32>();
            readonly Atlas _hudArt;
            readonly Region _gItem, _gBoss, _gSpike, _gLockGold, _gLockSilver, _qCap;

            public InkLayer(LedgerHud hud)
            {
                _hud = hud;
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
                generateVisualContent += Paint;
                var a = SpriteSet.Atlas;
                _keys[0] = a[SpriteSet.Key(false, 3, false)]; _keys[1] = a[SpriteSet.Key(true, 3, false)];
                for (int i = 0; i < 4; i++) _icons[i] = a[SpriteSet.Icon(i.ToString())];
                _icons[4] = a[SpriteSet.Icon("blast")]; _icons[5] = a[SpriteSet.Icon("hook")];
                for (int i = 0; i < 4; i++) _mouseGun[i] = a[SpriteSet.Mouse(true, Weapons.All[i].Color)];
                _mouseBlast = a[SpriteSet.Mouse(false, Sprites.BlastColor)]; _mouseHook = a[SpriteSet.Mouse(false, Sprites.HookColor)];
                foreach (var id in ItemArt.Ids) { _item26[id] = a[SpriteSet.Item(id, 26)]; _itemInk[id] = Hex.C(ItemArt.Color(id)); }
                foreach (var id in ItemIcons.Ids) { _icon2[id] = a[SpriteSet.ItemIcon(id, 2)]; _icon4[id] = a[SpriteSet.ItemIcon(id, 4)]; }
                _item26["?"] = a[SpriteSet.Item("?", 26)];
                _glow = a[SpriteSet.Glow]; _white = a[SpriteSet.White]; _disc = a[SpriteSet.Disc];

                // the HUD's own pieces: hearts by exact halves, the map's marks in ink, the Q cap, the empty-hand tag
                var list = new List<(string, Img)>
                {
                    ("item", Sprites.Glyph(Sprites.MiniGlyph["item"], LedgerArt.GoldInk, 2)),
                    ("boss", Sprites.Glyph(HornedSkull, LedgerArt.RedInk, 2)),
                    ("spike", Sprites.Glyph(SpikeSkull, LedgerArt.RedInk, 2)),
                    ("lockgold", Sprites.Bake(Lock, new Dictionary<char, Color32> { ['#'] = Hex.C("#ffd23d") }, 1)),
                    ("locksilver", Sprites.Bake(Lock, new Dictionary<char, Color32> { ['#'] = Hex.C("#d8dee9") }, 1)),
                    ("pick1", Sprites.Bake(PickHeart, new Dictionary<char, Color32> { ['#'] = Hex.C("#e8395a") }, 1)),
                    ("pick2", Sprites.Bake(PickHeart, new Dictionary<char, Color32> { ['#'] = Hex.C("#9aa0ab") }, 1)),
                    ("pick3", Sprites.Bake(PickKey, new Dictionary<char, Color32> { ['#'] = Hex.C("#d8dee9") }, 1)),
                    ("pick4", Sprites.Bake(PickKey, new Dictionary<char, Color32> { ['#'] = Hex.C("#ffd23d") }, 1)),
                    ("qcap", LedgerArt.Cap("Q", 24)),
                                    };
                var schemes = new[] { Sprites.HeartScheme.Red, Sprites.HeartScheme.Regen, Sprites.HeartScheme.Gray };
                for (int k = 0; k < 3; k++)
                    for (int f = 0; f < 3; f++) list.Add(("heart" + k + f, LedgerArt.Heart(schemes[k], f, HudLayout.HeartPx)));
                _hudArt = Atlas.Build(list);
                _gItem = _hudArt["item"]; _gBoss = _hudArt["boss"]; _gSpike = _hudArt["spike"]; _gLockGold = _hudArt["lockgold"]; _gLockSilver = _hudArt["locksilver"];
                _qCap = _hudArt["qcap"];
                for (int k = 1; k <= 4; k++) _pick[k] = _hudArt["pick" + k];
                for (int k = 0; k < 3; k++) for (int f = 0; f < 3; f++) _hearts[k, f] = _hudArt["heart" + k + f];
                RegisterCallback<DetachFromPanelEvent>(_ => Atlas.Free(_hudArt.Texture));
            }

            // the map's own marks: the Warden's horned skull, a padlock and the pickups as 7px pictures
            static readonly string[] SpikeSkull = { ".###.", "#####", "#.#.#", "#####", ".#.#." };   // a spike room: no horns
            static readonly string[] HornedSkull = { "#.....#", "##...##", ".#####.", "#######", "#..#..#", "#######", ".#.#.#." };
            static readonly string[] Lock = { ".###.", "#...#", "#...#", "#####", "##.##", "#####" };
            static readonly string[] PickHeart = { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#..." };
            static readonly string[] PickKey = { ".##....", "#..####", ".##.#.#" };

            public static float Pulse(float t, float rate) => 0.5f + 0.5f * Mathf.Sin(t * rate);

            /// <summary>How many pixels of a slot's well a cooldown darkens (0 when ready).</summary>
            public static int ShadePx(float ready) => Mathf.RoundToInt((1 - ready) * (HudLayout.SlotSize - 14));
            public static int FillPx(float f, float width) => Mathf.RoundToInt(Mathf.Clamp01(f) * (width - 3));

            public static float BlinkFill(RunState run) =>
                run.blinkCharges >= 2 ? 0 : Mathf.Clamp01((float)(run.player.blinkRegen / Balance.BlinkRecharge));

            /// <summary>1 when the gun is ready; the cooldown's max is the weapon's cadence at the current tempo (Weapons.Fire).</summary>
            public static float WeaponReady(Player p)
            {
                var w = Weapons.All[Mathf.Clamp(p.weaponIdx, 0, Weapons.All.Length - 1)];
                double max = w.Cooldown / Balance.TempoRate;
                return max <= 0 ? 1 : Mathf.Clamp01((float)(1 - p.cooldown / max));
            }

            public static float AltReady(Player p)
            {
                double max = (int)(Balance.AltCooldown / Balance.TempoRate);
                return max <= 0 ? 1 : Mathf.Clamp01((float)(1 - p.altCooldown / max));
            }

            /// <summary>The name on the boss plate.</summary>
            public static string BossName(BodyKind k) => k == BodyKind.SunkenKing ? FinalBoss.Name : k == BodyKind.Gatekeeper ? FloorBoss.Name : "THE WARDEN";

            public static Enemy LiveBoss(RunState run)
            {
                for (int i = 0; i < run.enemies.Count; i++)
                    if (Bodies.IsBoss(run.enemies[i].kind) && run.enemies[i].hp > 0) return run.enemies[i];
                return null;
            }

            /// <summary>The pickup a room's map cell shows (heart, armour, keys), as a small code; 0 for none.</summary>
            public static int MapPickup(RunState run, Room r)
            {
                var list = r.X == run.curX && r.Y == run.curY ? run.pickups : r.Pickups;
                for (int i = 0; i < list.Count; i++)
                {
                    var k = list[i].kind;
                    if (k == "heart") return 1;
                    if (k == "armor") return 2;
                    if (k == "key") return 3;
                    if (k == "goldkey") return 4;
                }
                return 0;
            }

            // ---- quads ----
            Texture T => SpriteSet.Atlas.Texture;
            void Spr(in Region r, float x, float y, Color32 tint) => _q.Sprite(T, r, x, y, r.W, r.H, tint);
            void Art(in Region r, float x, float y, float scale = 1, float alpha = 1) => _q.Sprite(_hudArt.Texture, r, x, y, r.W * scale, r.H * scale, QuadBatch.Alpha(alpha));
            void Rect(float x, float y, float w, float h, Color32 c) => _q.Sprite(T, _white, x, y, w, h, c);
            void Frame(float x, float y, float w, float h, int t, Color32 c)
            {
                Rect(x, y, w, t, c); Rect(x, y + h - t, w, t, c); Rect(x, y + t, t, h - t * 2, c); Rect(x + w - t, y + t, t, h - t * 2, c);
            }

            /// <summary>Text in the ledger's 5x7 hand from (x, y), a range of a string or the digit buffer; no strings made.</summary>
            void Text(string s, int from, int to, float x, float y, int scale, Color32 c)
            {
                for (int i = from; i < to; i++) Glyph(s[i], x + (i - from) * LedgerType.Advance * scale, y, scale, c);
            }
            void Text(string s, float x, float y, int scale, Color32 c) => Text(s, 0, s.Length, x, y, scale, c);
            void Digits(int n, float x, float y, int scale, Color32 c) { for (int i = 0; i < n; i++) Glyph(_digits[i], x + i * LedgerType.Advance * scale, y, scale, c); }
            void Glyph(char ch, float x, float y, int scale, Color32 c)
            {
                if (ch == ' ') return;
                var rows = LedgerType.Rows(ch);
                for (int ry = 0; ry < LedgerType.H; ry++)
                {
                    var row = rows[ry];
                    for (int rx = 0; rx < LedgerType.W; rx++)
                    {
                        if (row[rx] != '#') continue;
                        int run = 1;   // a horizontal run of lit cells is one quad
                        while (rx + run < LedgerType.W && row[rx + run] == '#') run++;
                        Rect(x + rx * scale, y + ry * scale, run * scale, scale, c);
                        rx += run - 1;
                    }
                }
            }

            /// <summary>Wrapped text: up to maxLines lines of s in maxW pixels; returns the lines used.</summary>
            int Wrapped(string s, float x, float y, int maxW, int scale, Color32 c, int maxLines, bool centre = false)
            {
                int from = 0, line = 0;
                while (from < s.Length && line < maxLines)
                {
                    var (end, next) = LedgerType.Wrap(s, from, maxW, scale);
                    float lx = centre ? x + (maxW - LedgerType.Width(end - from, scale)) / 2 : x;
                    Text(s, from, end, lx, y + line * (LedgerType.H + LedgerType.LineGap) * scale, scale, c);
                    from = next; line++;
                }
                return line;
            }

            /// <summary>A name on its plate, one or two lines, centred on the plate's height.</summary>
            void PlateText(string s, Rect pl, int maxW)
            {
                int lines = 0;
                for (int from = 0; from < s.Length && lines < 2; lines++) from = LedgerType.Wrap(s, from, maxW, 2).next;
                float h = lines * LedgerType.H * 2 + (lines - 1) * LedgerType.LineGap * 2;
                Wrapped(s, pl.x + 10, Mathf.Round(pl.center.y - h / 2), maxW, 2, LedgerArt.Ink, 2);
            }

            int Num(int v, string prefix = null)
            {
                int n = 0;
                if (prefix != null) foreach (var ch in prefix) _digits[n++] = ch;
                if (v < 0) { _digits[n++] = '-'; v = -v; }
                int start = n;
                do { _digits[n++] = (char)('0' + v % 10); v /= 10; } while (v > 0 && n < _digits.Length);
                Array.Reverse(_digits, start, n - start);
                return n;
            }

            void Paint(MeshGenerationContext ctx)
            {
                var run = _hud.Run;
                if (run == null || run.player == null) return;
                _q.Begin(ctx);
                int area = (int)AreaRules.AreaForFloor(run.floor);
                DrawBody(run, area);
                DrawMap(run, area);
                DrawKit(run);
                DrawBoss(run);
                if (_hud.Low) DrawBleed();
                _q.Flush();
            }

            static readonly Color32 Soot = Hex.C("#0d0a08"), Cream = Hex.C("#e8dcc0"), Dull = Hex.C("#6b5a44"), Spent = Hex.C("#c85a5a"),
                RobeLit = Hex.C("#7a55c4"), BossGreen = Hex.C("#4fd06c"), PhaseGray = Hex.C("#d8c4a0"), BlinkFull = Sprites.BlinkBlue,
                BlinkPart = Sprites.BlinkDeep, Shine = Hex.Rgba(255, 255, 255, 0.45f), Under = Hex.Rgba(0, 0, 0, 0.30f), NotchInk = Hex.C("#3a2a18");
            static readonly Color32[] Accent = AreaInk(false), AccentDim = AreaInk(true);
            static readonly Color32 DoorCut = Hex.C("#f4ead2");
            static readonly Color32 Visited = Hex.C("#d9c9a0"), StartFill = Hex.C("#cbb98c"), Shell = Hex.Rgba(70, 50, 30, 0.10f);
            static readonly Color32[] PhaseInk = { Hex.C("#8fd89a"), Hex.C("#8fd89a"), Hex.C("#e8c46a"), Hex.C("#e8804a") };
            static Color32[] AreaInk(bool dim)
            {
                var a = new Color32[4];
                for (int i = 0; i < 4; i++) { var p = AreaPalette.For((Area)i); a[i] = Hex.C(dim ? p.AccentDim : p.Accent); }
                return a;
            }

            /// <summary>A bar inside a well: the colour, a lit top line, a shaded foot, so it reads as a surface.</summary>
            void Bar(Rect well, int px, Color32 c)
            {
                if (px <= 0) return;
                float x = well.x + 2, y = well.y + 2, h = well.height - 3;
                Rect(x, y, px, h, c);
                Rect(x, y, px, 1, Shine);
                Rect(x, y + h - 2, px, 2, Under);
            }

            // ---- the left page ----
            void DrawBody(RunState run, int area)
            {
                var p = run.player;
                HeartRow.Layout(p, _row);
                if (_hud.Low)
                {
                    // the last red heart that still has blood in it throbs
                    float pu = Pulse(_hud.Time, 9.6f);
                    int last = -1;
                    for (int i = 0; i < _row.Count && i < HudLayout.HeartCapacity; i++) if (_row[i].Kind == HeartKind.Red && _row[i].Fill != HeartFill.Empty) last = i;
                    if (last >= 0)
                    {
                        var at = HudLayout.HeartTopLeft(last, Mathf.Min(_row.Count, HudLayout.HeartCapacity)) + new Vector2(HudLayout.HeartW / 2f, HudLayout.HeartH / 2f);
                        float r = 26 + 7 * pu;
                        _q.Sprite(T, _glow, at.x - r, at.y - r, r * 2, r * 2, Hex.Rgba(232, 57, 90, 0.40f + 0.55f * pu));
                    }
                }
                int shown = Mathf.Min(_row.Count, HudLayout.HeartCapacity);
                for (int i = shown - 1; i >= 0; i--)   // drawn right to left, so a closed-up row overlaps in reading order
                {
                    var h = _row[i];
                    int k = h.Kind == HeartKind.Red ? 0 : h.Kind == HeartKind.Regen ? 1 : 2;
                    int f = h.Fill == HeartFill.Full ? 2 : h.Fill == HeartFill.Half ? 1 : 0;
                    var tl = HudLayout.HeartTopLeft(i, shown);
                    Art(_hearts[k, f], tl.x, tl.y);
                }
                if (_row.Count > HudLayout.HeartCapacity)
                {
                    var tl = HudLayout.HeartTopLeft(HudLayout.HeartCapacity - 1, HudLayout.HeartCapacity);
                    int n = Num(_row.Count - HudLayout.HeartCapacity, "+");
                    Digits(n, tl.x + 4, tl.y - 1, 2, LedgerArt.Ink);   // over the last heart, never under the row
                }

                // momentum, straight under the hearts, in the blink's blue (owner 2026-10-14). Not green: green is every
                // health bar in the room, and a green bar under the hearts read as health (critic)
                var mw = HudLayout.MomentumWell;
                float m = Mathf.Clamp01((float)p.momentum);
                Bar(mw, FillPx(m, mw.width), Color32.Lerp(Sprites.BlinkDeep, Sprites.BlinkBlue, m));   // the blink's blue, brightening as it fills
                for (int i = 1; i < 4; i++) Rect(mw.x + Mathf.Round(mw.width * i / 4f), mw.y + 2, 1, mw.height - 3, NotchInk);

                // keys: held ones in their metal, missing ones a quarter-strength ghost in the dark well
                for (int i = 0; i < 2; i++)
                {
                    var w = HudLayout.KeyWell(i);
                    bool held = i == 0 ? p.hasSilver : p.hasGold;
                    var k = _keys[i];
                    Spr(k, Mathf.Round(w.center.x - k.W / 2f), Mathf.Round(w.center.y - k.H / 2f), QuadBatch.Alpha(held ? 1 : 0.25f));
                }

                // blink: two pips; a held charge is a lit pale blue with a glow, the recharging one fills in a deeper blue
                float fill = BlinkFill(run);
                for (int i = 0; i < 2; i++)
                {
                    var w = HudLayout.BlinkWell(i);
                    bool whole = i < run.blinkCharges;
                    float f = whole ? 1 : i == run.blinkCharges ? fill : 0;
                    if (whole) _q.Sprite(T, _glow, w.x - 6, w.y - 10, w.width + 12, w.height + 20, Hex.Rgba(120, 180, 255, 0.35f));
                    Bar(w, FillPx(f, w.width), whole ? BlinkFull : BlinkPart);
                }

                // depth: the floor struck into the seal in the area's accent, the area named beside it
                var seal = HudLayout.DepthSeal;
                int nd = Num(run.floor);
                int sc = nd >= 3 ? 3 : 4;
                float tw = LedgerType.Width(nd, sc);
                float dx = Mathf.Round(seal.center.x - tw / 2), dy = Mathf.Round(seal.center.y - LedgerType.H * sc / 2f);
                Digits(nd, dx + 1, dy + 1, sc, Soot);
                Digits(nd, dx, dy, sc, run.floor > 1 ? Accent[area] : AccentDim[area]);
                Wrapped(AreaNames[Mathf.Clamp(area, 0, AreaNames.Length - 1)], HudLayout.AreaX, HudLayout.AreaY, HudLayout.InkW - 88, 2, LedgerArt.Ink, 3);
                Ladder(run.floor);

                // the footer: the slain, by the skull in the margin (the seed tag is its own element)
                int nk = Num(run.kills);
                Digits(nk, HudLayout.RowX + 1, HudLayout.SlainY + 1, 3, LedgerArt.Lift);
                Digits(nk, HudLayout.RowX, HudLayout.SlainY, 3, LedgerArt.Ink);
            }

            // the descent: floors passed are struck through in red, this floor is inked in its accent
            void Ladder(int floor)
            {
                for (int band = 0; band < 4; band++)
                {
                    for (int i = 0; i < HudLayout.Rungs(band); i++)
                    {
                        var r = HudLayout.Rung(band, i);
                        int f = band * 4 + i + 1;
                        bool here = floor == f || (band == 3 && i == 1 && floor > f), passed = floor > f && !here;
                        if (here)
                        {
                            Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2, Accent[band]);
                            Frame(r.x, r.y, r.width, r.height, 2, LedgerArt.Ink);
                            int n = Num(floor, null);
                            Digits(n, Mathf.Round(r.center.x - LedgerType.Width(n, 2) / 2f), r.y + 5, 2, LedgerArt.Ink);
                        }
                        else if (passed)
                            for (int k = 0; k <= 24; k++)   // a pen stroke through the box, corner to corner
                                Rect(r.x + 3 + k * (r.width - 8) / 24f, r.yMax - 5 - k * (r.height - 8) / 24f, 2, 2, LedgerArt.RedInk);
                    }
                }
            }

            // ---- the right page: the map, inked on the chart, fitted to the rooms it knows ----
            void DrawMap(RunState run, int area)
            {
                if (run.dungeon == null) return;
                var m = HudLayout.Map;
                var rooms = run.dungeon.AllRooms;
                // the known rooms' bounds: a small floor is drawn larger and centred, never adrift in the paper
                int x0 = 99, y0 = 99, x1 = -1, y1 = -1;
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    if (!r.Visited && !Known(run, r)) continue;
                    x0 = Mathf.Min(x0, r.X); y0 = Mathf.Min(y0, r.Y); x1 = Mathf.Max(x1, r.X); y1 = Mathf.Max(y1, r.Y);
                }
                if (x1 < 0) return;
                int span = Mathf.Max(x1 - x0 + 1, y1 - y0 + 1, 3);
                int cell = Mathf.Clamp((int)m.width / span, HudLayout.MapCell, HudLayout.MapMaxCell);
                cell = Mathf.Min(cell, (int)m.width / Mathf.Max(x1 - x0 + 1, y1 - y0 + 1));
                int box = cell * 27 / 32, off = (cell - box) / 2;
                float ox = Mathf.Round(m.center.x - (x1 - x0 + 1) * cell / 2f) - x0 * cell, oy = Mathf.Round(m.center.y - (y1 - y0 + 1) * cell / 2f) - y0 * cell;
                float bossPulse = Pulse(_hud.Time, 5.4f);
                var ink = LedgerArt.Ink;
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    if (r.Visited || !Known(run, r)) continue;
                    // a known room not yet walked: a dashed pencil shell, a padlock if a key opens it
                    float x = ox + r.X * cell + off, y = oy + r.Y * cell + off;
                    Rect(x, y, box, box, Shell);
                    for (int k = 0; k < box; k += 4)
                    {
                        Rect(x + k, y, 2, 1, LedgerArt.InkFaint); Rect(x + k, y + box - 1, 2, 1, LedgerArt.InkFaint);
                        Rect(x, y + k, 1, 2, LedgerArt.InkFaint); Rect(x + box - 1, y + k, 1, 2, LedgerArt.InkFaint);
                    }
                    RoomMark(run, r, x, y, box, false);
                }
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    if (!r.Visited) continue;
                    float x = ox + r.X * cell + off, y = oy + r.Y * cell + off;
                    bool here = r.X == run.curX && r.Y == run.curY;
                    bool dn = r.Doors.Contains(Dir.N), ds = r.Doors.Contains(Dir.S), dw = r.Doors.Contains(Dir.W), de = r.Doors.Contains(Dir.E);
                    // doors: a thin ink thread across the gap
                    if (dn) Rect(x + box / 2f - 1, y - off - 1, 2, off + 1, LedgerArt.InkSoft);
                    if (ds) Rect(x + box / 2f - 1, y + box, 2, off + 1, LedgerArt.InkSoft);
                    if (dw) Rect(x - off - 1, y + box / 2f - 1, off + 1, 2, LedgerArt.InkSoft);
                    if (de) Rect(x + box, y + box / 2f - 1, off + 1, 2, LedgerArt.InkSoft);
                    var fill = here ? Accent[area] : r.Type == RoomKind.Start ? StartFill : Visited;
                    Rect(x, y, box, box, fill);
                    Rect(x + 1, y + 1, box - 2, 1, Hex.Rgba(255, 250, 235, 0.45f));
                    if (r.Type == RoomKind.Boss) Rect(x, y, box, box, Hex.Rgba(163, 40, 31, 0.22f));
                    Frame(x, y, box, box, here ? 2 : 1, ink);
                    // and each door is a 2px notch cut in the room's own edge (the JS's door ticks)
                    int t = here ? 2 : 1;
                    if (dn) Rect(x + box / 2f - 3, y, 6, t, DoorCut);
                    if (ds) Rect(x + box / 2f - 3, y + box - t, 6, t, DoorCut);
                    if (dw) Rect(x, y + box / 2f - 3, t, 6, DoorCut);
                    if (de) Rect(x + box - t, y + box / 2f - 3, t, 6, DoorCut);
                    RoomMark(run, r, x, y, box, true);
                    if (r.Type == RoomKind.Boss && !r.Cleared)
                    {
                        // a Warden walked in on and not finished: a red ring that breathes
                        var red = Hex.Rgba(200, 40, 30, 0.50f + 0.45f * bossPulse);
                        Frame(x - 3, y - 3, box + 6, box + 6, 2, red);
                    }
                    if (here)
                    {
                        float cx = x + box / 2f, cy = y + box / 2f;
                        _q.Sprite(T, _disc, cx - 6, cy - 6, 12, 12, LedgerArt.Ink); _q.Sprite(T, _disc, cx - 4, cy - 4, 8, 8, RobeLit);
                    }
                    int pk = MapPickup(run, r);
                    if (pk > 0) { var pr = _pick[pk]; Art(pr, x + box - pr.W - 1, y + box - pr.H - 1); }
                }
            }

            // what a room is, on the chart: the Warden's horned skull, an item's cross, and the padlock of the key
            // that opens it (silver for an item room, gold for the Warden's) beside the mark until the door is open
            void RoomMark(RunState run, Room r, float x, float y, int box, bool walked)
            {
                if (r.Spike != SpikeKind.None) Art(_gSpike, x + 2, y + 2);   // a spike room, once its door is seen
                if (r.Type != RoomKind.Boss && r.Type != RoomKind.Item) return;
                bool boss = r.Type == RoomKind.Boss;
                bool sealedRoom = boss ? !run.bossUnlocked : !run.itemUnlocked;
                var mark = boss ? _gBoss : _gItem;
                var lockR = boss ? _gLockGold : _gLockSilver;
                if (!sealedRoom) { Centre(mark, x + box / 2f, y + box / 2f, walked ? 1 : 0.7f); return; }
                Centre(mark, x + box / 2f - 5, y + box / 2f, walked ? 1 : 0.7f);
                Art(lockR, x + box - lockR.W - 1, y + 1);
            }

            void Centre(in Region g, float cx, float cy, float alpha = 1) =>
                _q.Sprite(_hudArt.Texture, g, Mathf.Round(cx - g.W / 2f), Mathf.Round(cy - g.H / 2f), g.W, g.H, QuadBatch.Alpha(alpha));

            static bool Known(RunState run, Room r)
            {
                // a room is on the chart once a walked room has a door to it
                for (int d = 0; d < 4; d++)
                {
                    int nx = r.X + (d == 1 ? 1 : d == 3 ? -1 : 0), ny = r.Y + (d == 2 ? 1 : d == 0 ? -1 : 0);
                    var n = run.dungeon.RoomAt(nx, ny);
                    if (n != null && n.Visited && n.Doors.Contains((Dir)((d + 2) % 4))) return true;
                }
                return false;
            }

            // ---- the kit: a slot and its name plate per row ----
            void DrawKit(RunState run)
            {
                var p = run.player;
                int wi = Mathf.Clamp(p.weaponIdx, 0, Weapons.All.Length - 1);
                Arm(0, _icons[wi], WeaponReady(p), Weapons.All[wi].Name, _mouseGun[wi]);
                bool hook = p.altMode == "hook";
                Arm(2, _icons[hook ? 5 : 4], AltReady(p), hook ? "Hook" : "Blast", hook ? _mouseHook : _mouseBlast);

                var s = HudLayout.Slot(1);
                var pl = HudLayout.Plate(1);
                var held = Items.ActiveItem(run);
                float capX = pl.xMax - 8 - _qCap.W, capY = Mathf.Round(pl.center.y - _qCap.H / 2f);
                if (held == null)
                {
                    // the empty hand: a dark well with the ghost of its key, EMPTY on the plate, the cap waiting
                    Text("Q", Mathf.Round(s.center.x - LedgerType.Width(1, 5) / 2f), Mathf.Round(s.center.y - LedgerType.H * 5 / 2f), 5, Hex.Rgba(232, 220, 192, 0.10f));
                    Text("Empty", pl.x + 10, pl.center.y - 7, 2, LedgerArt.InkSoft);
                    Art(_qCap, capX, capY, 1, 0.45f);
                }
                else
                {
                    var pic = _icon4.TryGetValue(held.Id, out var ir) ? ir : _item26.TryGetValue(held.Id, out var tr) ? tr : _item26["?"];
                    if (_icon4.ContainsKey(held.Id)) Spr(pic, Mathf.Round(s.center.x - pic.W / 2f), Mathf.Round(s.center.y - pic.H / 2f), QuadBatch.Alpha(1));
                    else _q.Sprite(T, pic, s.x + 7, s.y + 7, s.width - 14, s.height - 14, QuadBatch.Alpha(1));   // a lettered tile at 2x
                    int charges = held.Charges ?? 0, recharge = Items.Def(held.Id).Recharge;
                    if (recharge > 0)
                    {
                        // a recharging active: a pip a won fight, inked as they fill
                        var ink = _itemInk.TryGetValue(held.Id, out var pc) ? pc : Cream;
                        for (int k = 0; k < recharge; k++)
                        {
                            float px = s.xMax - 9 - (recharge - k) * 10, py = s.yMax - 17;
                            Rect(px - 1, py - 1, 9, 9, Hex.Rgba(8, 4, 2, 0.80f));
                            Rect(px, py, 7, 7, k < charges ? ink : Spent);
                        }
                    }
                    else if (held.Charges != int.MaxValue)
                    {
                        int n = Num(charges, "x");
                        float bw = LedgerType.Width(n, 2) + 6;
                        Rect(s.xMax - 7 - bw, s.yMax - 7 - 18, bw, 18, Hex.Rgba(8, 4, 2, 0.80f));
                        Digits(n, s.xMax - 4 - bw, s.yMax - 7 - 16, 2, charges > 0 ? (_itemInk.TryGetValue(held.Id, out var ic) ? ic : Cream) : Spent);
                    }
                    PlateText(Items.Def(held.Id).Name, pl, (int)(capX - pl.x - 14));
                    Art(_qCap, capX, capY, 1, Items.Ready(run) ? 1 : 0.45f);
                }

                // the passive items, as their pictures in a row
                int shown = 0;
                for (int i = 0; i < run.loadout.Count; i++)
                {
                    var it = run.loadout[i];
                    if (it.Slot == Items.ActiveSlot) continue;
                    if (shown >= HudLayout.CarryPerLine * 2) break;
                    float x = HudLayout.Ink0(false) + (shown % HudLayout.CarryPerLine) * HudLayout.CarryStep, y = HudLayout.CarryY + (shown / HudLayout.CarryPerLine) * HudLayout.CarryStep;
                    if (_icon2.TryGetValue(it.Id, out var ir)) Spr(ir, x, y, QuadBatch.Alpha(1));
                    else { var tile = _item26.TryGetValue(it.Id, out var tr) ? tr : _item26["?"]; Spr(tile, x + 1, y + 1, QuadBatch.Alpha(1)); }
                    shown++;
                }
                if (shown == 0) Text("nothing yet", HudLayout.Ink0(false), HudLayout.CarryY + 6, 2, LedgerArt.InkSoft);
            }

            void Arm(int row, in Region icon, float ready, string name, in Region mouse)
            {
                var s = HudLayout.Slot(row);
                var pl = HudLayout.Plate(row);
                Spr(icon, Mathf.Round(s.center.x - icon.W / 2f), Mathf.Round(s.center.y - icon.H / 2f), QuadBatch.Alpha(1));
                int shade = ShadePx(ready);   // the cooldown, drawn down from the top of the well in whole pixels
                if (shade > 0) Rect(s.x + 7, s.y + 7, s.width - 14, shade, Hex.Rgba(8, 4, 2, 0.66f));
                float mx = pl.xMax - 8 - mouse.W, my = Mathf.Round(pl.center.y - mouse.H / 2f);
                Spr(mouse, mx, my, QuadBatch.Alpha(1));
                PlateText(name, pl, (int)(mx - pl.x - 14));
            }

            // ---- the Warden ----
            void DrawBoss(RunState run)
            {
                var e = LiveBoss(run);
                if (e == null) return;
                var w = HudLayout.BossWell;
                int fw = FillPx(Mathf.Clamp01((float)(e.hp / e.maxHp)), w.width);
                if (fw > 0)
                {
                    float x = w.x + 2, y = w.y + 2, h = w.height - 3;
                    Rect(x, y, fw, h, BossGreen);
                    Rect(x, y, fw, 3, Hex.Rgba(200, 255, 210, 0.45f));
                    Rect(x, y + h - 5, fw, 5, Hex.Rgba(0, 50, 20, 0.35f));
                    for (float sx = x + 6; sx < x + fw; sx += 8) Rect(sx, y + 3, 1, h - 8, Hex.Rgba(0, 60, 20, 0.14f));   // grain
                }
                Notch(w, Balance.BossPhase1); Notch(w, Balance.BossPhase2);
                var plate = HudLayout.BossPlate;
                int ph = Mathf.Clamp(e.phase, 1, 3);
                string name = BossName(e.kind);
                Text(name, plate.x + 15, plate.y + 13, 2, Soot);
                Text(name, plate.x + 14, plate.y + 12, 2, PhaseInk[ph]);
                int n = Num(ph, "PHASE ");
                float px = plate.xMax - 14 - LedgerType.Width(n, 2);
                Digits(n, px + 1, plate.y + 13, 2, Soot);
                Digits(n, px, plate.y + 12, 2, PhaseGray);
            }

            void Notch(Rect w, double th)
            {
                float nx = w.x + Mathf.Round(w.width * (float)th);
                Rect(nx - 1, w.y - 5, 3, w.height + 10, Cream);
                Rect(nx - 1, w.y - 5, 3, 1, Soot);
            }

            // ---- the last heart: red light around the room's rim and down the pages' inner edges ----
            void DrawBleed()
            {
                float pu = Pulse(_hud.Time, 9.6f);
                var room = HudLayout.Room;
                for (int k = 0; k < HudLayout.RimGlow; k++)
                {
                    float t = 1 - k / (float)HudLayout.RimGlow;
                    var c = Hex.Rgba(235, 30, 40, (0.30f + 0.60f * pu) * t * t);
                    Frame(room.x - k - 1, room.y - k - 1, room.width + k * 2 + 2, room.height + k * 2 + 2, 1, c);
                }
                for (int k = 0; k < 22; k++)
                {
                    float t = 1 - k / 22f;
                    var c = Hex.Rgba(220, 30, 40, (0.16f + 0.40f * pu) * t * t);
                    float top = HudLayout.BoardTop + HudLayout.PaperPad, h = HudLayout.BoardH - HudLayout.PaperPad * 2;
                    Rect(HudLayout.LeftX + HudLayout.BoardW - HudLayout.PaperPad - 1 - k, top, 1, h, c);
                    Rect(HudLayout.RightX + HudLayout.PaperPad + k, top, 1, h, c);
                }
            }
        }
    }
}
