using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Depths.Unity.Art;

namespace Depths.Unity.Hud
{
    /// <summary>
    /// The in-game HUD: two lecterns in the side margins, each holding a ledger page. The left page is
    /// the body (blood, mending, mail, keys, blink, momentum, depth, slain, seed), the right the map and
    /// the kit; the Warden's bar sits under the room. Retained: the stands are baked once, labels change
    /// only when their value does, and the ink layer repaints only when a drawn value moves (one 64-bit
    /// signature per frame, compared). <see cref="Refresh"/> allocates nothing once warm.
    /// </summary>
    public sealed class LedgerHud : VisualElement
    {
        public static readonly string[] AreaNames = { "The Shallows", "The Kiln Works", "The Flooded Galleries", "The Warden's Deep" };

        readonly InkLayer _ink;
        readonly VisualElement _bossPlate;
        readonly Label[] _rowLabels = new Label[HudLayout.LeftLabels.Length];
        readonly Label _area, _weapon, _alt, _itemName, _carry, _dev, _slain, _seed;
        int _kills = -1; uint _seedOf; bool _seeded;
        readonly List<Texture2D> _owned = new List<Texture2D>();
        ulong _sig;
        bool _low;
        int _momStep = -1;

        public readonly HudOverlays Overlays;
        /// <summary>How many times the ink layer has been asked to repaint (the retained-mode tests count these).</summary>
        public int Repaints { get; private set; }
        public bool DevVisible => _dev.style.display == DisplayStyle.Flex;

        public LedgerHud()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;

            Add(Baked(LedgerArt.Stand(true), HudLayout.Stand(true), "stand-left"));
            Add(Baked(LedgerArt.Stand(false), HudLayout.Stand(false), "stand-right"));
            _bossPlate = Baked(LedgerArt.BossPlate(), HudLayout.BossPlate, "boss-plate");
            _bossPlate.style.display = DisplayStyle.None;
            Add(_bossPlate);

            var ink = LedgerArt.Ink;
            for (int i = 0; i < _rowLabels.Length; i++)
            {
                var l = LedgerFonts.Ink(HudLayout.LeftLabels[i], 14, (Color)LedgerArt.InkSoft, LedgerFonts.Italic);
                Place(l, HudLayout.LabelX - 2, HudLayout.LeftRows[i], HudLayout.MarginX - HudLayout.LabelX, 24);
                _rowLabels[i] = l;
                Add(l);
            }
            _area = LedgerFonts.Ink("", 14, (Color)ink, LedgerFonts.Italic);
            Place(_area, HudLayout.ValueX, (int)HudLayout.DepthSeal.yMax + 6, 148, 20);
            Add(_area);
            _slain = LedgerFonts.Ink("", 17, (Color)ink, LedgerFonts.Regular);
            Place(_slain, HudLayout.ValueX, HudLayout.SlainY, 140, 26);
            Add(_slain);
            _seed = LedgerFonts.Ink("", 17, (Color)ink, LedgerFonts.Regular);
            Place(_seed, HudLayout.ValueX, HudLayout.SeedY, 140, 26);
            _seed.style.letterSpacing = 2;
            Add(_seed);
            _weapon = Caption(0); _alt = Caption(2);
            Caption(1);   // the item's own line below names it
            _itemName = LedgerFonts.Ink("", 15, (Color)ink, LedgerFonts.Italic, TextAnchor.MiddleCenter);
            Place(_itemName, HudLayout.RightX + 14, HudLayout.ItemNameY, HudLayout.LecternW - 28, 22);
            Add(_itemName);
            _carry = LedgerFonts.Ink("carrying", 12, (Color)LedgerArt.InkSoft, LedgerFonts.Italic);
            Place(_carry, HudLayout.RightX + 18, HudLayout.CarryY - 26, 120, 16);
            Add(_carry);

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

        Label Caption(int slot)
        {
            var s = HudLayout.Slot(slot);
            var l = LedgerFonts.Ink("", 13, (Color)LedgerArt.InkSoft, LedgerFonts.Italic, TextAnchor.MiddleCenter);
            Place(l, (int)s.x - 8, HudLayout.CaptionY, (int)s.width + 16, 18);
            Add(l);
            return l;
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
            Overlays.Release();
        }

        /// <summary>The developer line (seed, curve, Brunch rule, keys): F3. The caller rebuilds the text only when it changes.</summary>
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
            var p = run.player;

            bool low = IsLow(p);
            if (low != _low)
            {
                _low = low;
                _rowLabels[0].style.color = low ? (Color)LedgerArt.RedInk : (Color)LedgerArt.InkSoft;
            }
            // the momentum label warms with the meter, in five steps (the blink trail's ramp)
            int ms = Mathf.RoundToInt(Mathf.Clamp01((float)p.momentum) * 4);
            if (ms != _momStep)
            {
                _momStep = ms;
                float t = ms / 4f;
                _rowLabels[5].style.color = ms == 0 ? (Color)LedgerArt.InkSoft : new Color((70 - 40 * t) / 255f, (90 + 50 * t) / 255f, (60 + 10 * t) / 255f);
            }

            int area = (int)AreaRules.AreaForFloor(run.floor);
            SetText(_area, AreaNames[Mathf.Clamp(area, 0, AreaNames.Length - 1)]);
            // the count and the seed are written in the hand, rewritten only when they change
            if (run.kills != _kills) { _kills = run.kills; _slain.text = run.kills.ToString(); }
            if (!_seeded || run.rootSeed != _seedOf) { _seeded = true; _seedOf = run.rootSeed; _seed.text = Rng.Encode(run.rootSeed); }
            int wi = Mathf.Clamp(p.weaponIdx, 0, Weapons.All.Length - 1);
            SetText(_weapon, Weapons.All[wi].Name);
            SetText(_alt, p.altMode == "hook" ? "Hook" : "Blast");
            var held = Items.ActiveItem(run);
            SetText(_itemName, held == null ? "nothing in hand" : Items.Def(held.Id).Name);
            var carryShow = PassiveCount(run) > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_carry.style.display != carryShow) _carry.style.display = carryShow;

            bool boss = InkLayer.LiveBoss(run) != null;
            var bd = boss ? DisplayStyle.Flex : DisplayStyle.None;
            if (_bossPlate.style.display != bd) _bossPlate.style.display = bd;

            ulong sig = Signature(run, low);
            if (sig != _sig)
            {
                _sig = sig;
                Repaints++;
                _ink.MarkDirtyRepaint();
            }
        }

        static void SetText(Label l, string s) { if (!ReferenceEquals(l.text, s) && l.text != s) l.text = s; }

        /// <summary>
        /// Low health: the last heart's worth of everything that stands between the player and death.
        /// The regen heart and armour drain first, so they count; at 2 points or fewer the page bleeds.
        /// </summary>
        public static bool IsLow(Player p) => p.hp > 0 && Math.Round(p.hp) + Math.Max(0, Math.Round(p.regenHeart)) + Math.Max(0, Math.Round(p.armor)) <= 2;

        internal static int PassiveCount(RunState run)
        {
            int n = 0;
            for (int i = 0; i < run.loadout.Count; i++) if (run.loadout[i].Slot != Items.ActiveSlot) n++;
            return n;
        }

        // ---- the repaint signature: every value the ink layer draws, quantised to what a pixel can show ----
        static ulong Mix(ulong h, long v) { h ^= (ulong)v + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2); return h * 0x100000001B3UL; }

        ulong Signature(RunState run, bool low)
        {
            var p = run.player;
            ulong h = 1469598103934665603UL;
            h = Mix(h, (long)Math.Round(p.hp)); h = Mix(h, (long)p.maxHp); h = Mix(h, (long)Math.Round(p.regenHeart)); h = Mix(h, (long)p.regenHeartMax);
            h = Mix(h, (long)Math.Round(p.armor)); h = Mix(h, (p.hasSilver ? 1 : 0) | (p.hasGold ? 2 : 0) | (p.altMode == "hook" ? 4 : 0));
            h = Mix(h, run.blinkCharges); h = Mix(h, (long)(InkLayer.BlinkFill(run) * HudLayout.BlinkW));
            h = Mix(h, (long)(Mathf.Clamp01((float)p.momentum) * HudLayout.MomentumW));
            h = Mix(h, run.floor);
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
                    h = Mix(h, (r.Visited ? 1 : 0) | (r.Cleared ? 2 : 0));
                    if (r.Visited) h = Mix(h, InkLayer.MapPickup(run, r));
                    bossOpen |= r.Visited && r.Type == RoomKind.Boss && !r.Cleared;
                }
            }
            var boss = InkLayer.LiveBoss(run);
            if (boss != null) { h = Mix(h, boss.phase); h = Mix(h, (long)(Mathf.Clamp01((float)(boss.hp / boss.maxHp)) * HudLayout.BossWell.width)); }
            // the two pulses, stepped finely enough to look smooth and coarsely enough to skip most frames
            h = Mix(h, low ? (long)(InkLayer.Pulse(Time, 9.6f) * 16) : -1);
            h = Mix(h, bossOpen ? (long)(InkLayer.Pulse(Time, 5.4f) * 10) : -1);
            return h;
        }

        // ================================================================================================

        /// <summary>The ink: everything on the pages that moves, as atlas quads in one batch.</summary>
        sealed class InkLayer : VisualElement
        {
            readonly LedgerHud _hud;
            readonly QuadBatch _q = new QuadBatch();
            readonly List<HeartSlot> _row = new List<HeartSlot>();
            readonly char[] _digits = new char[12];
            readonly Region[][] _hearts = new Region[4][];
            readonly Region[] _keys = new Region[4];
            readonly Region[] _icons = new Region[6];
            readonly Region[] _mouseGun = new Region[4];
            readonly Region _mouseBlast, _mouseHook;
            readonly Dictionary<string, Color32> _itemInk = new Dictionary<string, Color32>();
            readonly Dictionary<string, Region> _item26 = new Dictionary<string, Region>(), _item18 = new Dictionary<string, Region>();
            readonly Region _glow, _white, _disc;
            readonly Atlas _glyphs;
            readonly Region _gItem, _gBoss, _gLockGold, _gLockSilver, _gBossRed;

            public InkLayer(LedgerHud hud)
            {
                _hud = hud;
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
                generateVisualContent += Paint;
                var a = SpriteSet.Atlas;
                foreach (Sprites.HeartScheme s in Enum.GetValues(typeof(Sprites.HeartScheme)))
                {
                    var arr = new Region[Sprites.HeartSteps + 1];
                    for (int q = 0; q <= Sprites.HeartSteps; q++) arr[q] = a["heart:" + s + ":" + HudLayout.HeartPx + ":" + q];
                    _hearts[(int)s] = arr;
                }
                for (int k = 0; k < 4; k++) _keys[k] = a[SpriteSet.Key(k >= 2, 2, k % 2 == 1)];
                for (int i = 0; i < 4; i++) _icons[i] = a[SpriteSet.Icon(i.ToString())];
                _icons[4] = a[SpriteSet.Icon("blast")]; _icons[5] = a[SpriteSet.Icon("hook")];
                for (int i = 0; i < 4; i++) _mouseGun[i] = a[SpriteSet.Mouse(true, Weapons.All[i].Color)];
                _mouseBlast = a[SpriteSet.Mouse(false, Sprites.BlastColor)]; _mouseHook = a[SpriteSet.Mouse(false, Sprites.HookColor)];
                foreach (var id in ItemArt.Ids) { _item26[id] = a[SpriteSet.Item(id, 26)]; _item18[id] = a[SpriteSet.Item(id, 18)]; _itemInk[id] = Hex.C(ItemArt.Color(id)); }
                _item26["?"] = a[SpriteSet.Item("?", 26)]; _item18["?"] = a[SpriteSet.Item("?", 18)];
                _glow = a[SpriteSet.Glow]; _white = a[SpriteSet.White]; _disc = a[SpriteSet.Disc];

                // the map's marks in ink colours, so they read on paper (the JS ones are lit for a dark board)
                var list = new List<(string, Img)>
                {
                    ("item", Sprites.Glyph(Sprites.MiniGlyph["item"], LedgerArt.GoldInk, 2)),
                    ("boss", Sprites.Glyph(Sprites.MiniGlyph["boss"], LedgerArt.Ink, 2)),
                    ("bossred", Sprites.Glyph(Sprites.MiniGlyph["boss"], LedgerArt.RedInk, 2)),
                    ("lockgold", Sprites.Glyph(Sprites.MiniGlyph["lock"], LedgerArt.GoldInk, 2)),
                    ("locksilver", Sprites.Glyph(Sprites.MiniGlyph["lock"], LedgerArt.SilverInk, 2)),
                };
                _glyphs = Atlas.Build(list);
                _gItem = _glyphs["item"]; _gBoss = _glyphs["boss"]; _gBossRed = _glyphs["bossred"]; _gLockGold = _glyphs["lockgold"]; _gLockSilver = _glyphs["locksilver"];
                RegisterCallback<DetachFromPanelEvent>(_ => Atlas.Free(_glyphs.Texture));
            }

            public static float Pulse(float t, float rate) => 0.5f + 0.5f * Mathf.Sin(t * rate);

            /// <summary>How many pixels of the slot's well a cooldown darkens (0 when ready).</summary>
            public static int ShadePx(float ready) => Mathf.RoundToInt((1 - ready) * (HudLayout.SlotSize - 16));

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

            public static Enemy LiveBoss(RunState run)
            {
                for (int i = 0; i < run.enemies.Count; i++)
                    if (run.enemies[i].kind == BodyKind.Boss && run.enemies[i].hp > 0) return run.enemies[i];
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
            void Rect(float x, float y, float w, float h, Color32 c) => _q.Sprite(T, _white, x + 0, y, w, h, c);
            void Frame(float x, float y, float w, float h, int t, Color32 c)
            {
                Rect(x, y, w, t, c); Rect(x, y + h - t, w, t, c); Rect(x, y + t, t, h - t * 2, c); Rect(x + w - t, y + t, t, h - t * 2, c);
            }

            /// <summary>Pixel-font text from its left edge, as quads (no strings are built).</summary>
            void Text(string s, float x, float y, int scale, Color32 c) { for (int i = 0; i < s.Length; i++) Glyph(s[i], x + i * 4 * scale, y, scale, c); }
            void Text(char[] s, int n, float x, float y, int scale, Color32 c) { for (int i = 0; i < n; i++) Glyph(s[i], x + i * 4 * scale, y, scale, c); }
            void Glyph(char ch, float x, float y, int scale, Color32 c)
            {
                var rows = PixelFont.Rows(ch);
                for (int ry = 0; ry < PixelFont.H; ry++)
                    for (int rx = 0; rx < PixelFont.W; rx++)
                        if (rows[ry][rx] == '#') Rect(x + rx * scale, y + ry * scale, scale, scale, c);
            }
            static float TextW(int n, int scale) => n == 0 ? 0 : (n * 4 - 1) * scale;

            int Digits(int v, string prefix = null)
            {
                int n = 0;
                if (prefix != null) foreach (var ch in prefix) _digits[n++] = ch;
                if (v < 0) { _digits[n++] = '-'; v = -v; }
                int start = n;
                do { _digits[n++] = (char)('0' + v % 10); v /= 10; } while (v > 0 && n < _digits.Length);
                Array.Reverse(_digits, start, n - start);
                return n;
            }

            /// <summary>Struck into wood: a dark copy down-right, then the colour.</summary>
            void Struck(int n, float x, float y, int scale, Color32 c)
            {
                Text(_digits, n, x + 1, y + 1, scale, Soot);
                Text(_digits, n, x, y, scale, c);
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
                _q.Flush();
            }

            // ---- the left page ----
            void DrawBody(RunState run, int area)
            {
                var p = run.player;
                HeartRow.Layout(p, _row);
                float t = _hud.Time;
                if (_hud.Low)
                {
                    // the page bleeds: the Blood line washes red, and the last lit heart throbs
                    float pu = Pulse(t, 9.6f);
                    Rect(HudLayout.LeftX + HudLayout.PaperPad + 2, HudLayout.BloodY - 4, HudLayout.LecternW - HudLayout.PaperPad * 2 - 4, HudLayout.BloodLineStep * HudLayout.BloodLines, Hex.Rgba(200, 30, 30, 0.08f + 0.16f * pu));
                    int last = -1, red = 0;
                    for (int i = 0; i < _row.Count; i++) if (_row[i].Kind == HeartKind.Red) { if (_row[i].Fill != HeartFill.Empty) last = red; red++; }
                    if (last >= 0)
                    {
                        var at = HudLayout.HeartTopLeft(HeartKind.Red, last) + new Vector2(HudLayout.HeartW / 2f, HudLayout.HeartH / 2f);
                        float r = 22 + 6 * pu;
                        _q.Sprite(T, _glow, at.x - r, at.y - r, r * 2, r * 2, Hex.Rgba(232, 57, 90, 0.35f + 0.55f * pu));
                    }
                }
                int nRed = 0, nRegen = 0, nMail = 0;
                for (int i = 0; i < _row.Count; i++)
                {
                    var h = _row[i];
                    int idx = h.Kind == HeartKind.Red ? nRed++ : h.Kind == HeartKind.Regen ? nRegen++ : nMail++;
                    if (idx >= HudLayout.HeartCapacity(h.Kind)) continue;
                    var scheme = h.Kind == HeartKind.Red ? Sprites.HeartScheme.Red : h.Kind == HeartKind.Regen ? Sprites.HeartScheme.Regen : Sprites.HeartScheme.Gray;
                    int step = h.Fill == HeartFill.Full ? Sprites.HeartSteps : h.Fill == HeartFill.Half ? Sprites.HeartSteps / 2 : 0;
                    var tl = HudLayout.HeartTopLeft(h.Kind, idx);
                    Spr(_hearts[(int)scheme][step], tl.x, tl.y, QuadBatch.Alpha(1));
                }
                Overflow(HeartKind.Red, nRed); Overflow(HeartKind.Regen, nRegen); Overflow(HeartKind.Armour, nMail);
                if (nRegen == 0) Dash(HudLayout.MendingY);
                if (nMail == 0) Dash(HudLayout.MailY);

                // keys: held ones bright, missing ones a dim outline of the same key
                int ky = HudLayout.KeysY + 6;
                Spr(_keys[p.hasSilver ? 0 : 1], HudLayout.ValueX, ky, QuadBatch.Alpha(1));
                Spr(_keys[p.hasGold ? 2 : 3], HudLayout.ValueX + HudLayout.KeyGap, ky, QuadBatch.Alpha(1));

                // blink: two inked boxes; a charge is a filled box, the recharging one fills from the left
                float fill = BlinkFill(run);
                for (int i = 0; i < 2; i++)
                {
                    float x = HudLayout.ValueX + i * (HudLayout.BlinkW + HudLayout.BlinkGap), y = HudLayout.BlinkY + 7;
                    Rect(x, y, HudLayout.BlinkW, HudLayout.BlinkH, Hex.Rgba(90, 60, 30, 0.12f));
                    float f = i < run.blinkCharges ? 1 : i == run.blinkCharges ? fill : 0;
                    if (f > 0)
                    {
                        bool whole = f >= 1;
                        Rect(x + 1, y + 1, (HudLayout.BlinkW - 2) * f, HudLayout.BlinkH - 2, whole ? BlinkInk : Hex.Rgba(70, 96, 150, 0.55f));
                        if (whole) Rect(x + 2, y + 2, HudLayout.BlinkW - 4, 1, Hex.Rgba(200, 220, 255, 0.45f));
                    }
                    Frame(x, y, HudLayout.BlinkW, HudLayout.BlinkH, 1, LedgerArt.Ink);
                }

                // momentum: a ruled bar in quarters, inked green as it fills (the blink trail's ramp)
                float m = Mathf.Clamp01((float)p.momentum);
                float mx = HudLayout.ValueX, my = HudLayout.MomentumY + 7;
                Rect(mx, my, HudLayout.MomentumW, HudLayout.MomentumH, Hex.Rgba(90, 60, 30, 0.12f));
                int mw = (int)(m * (HudLayout.MomentumW - 2));
                if (mw > 0) Rect(mx + 1, my + 1, mw, HudLayout.MomentumH - 2, new Color32((byte)(50 + 30 * m), (byte)(120 + 70 * m), (byte)(70 + 20 * m), 255));
                for (int i = 1; i < 4; i++) Rect(mx + Mathf.Round(HudLayout.MomentumW * i / 4f), my, 1, HudLayout.MomentumH, LedgerArt.InkSoft);
                Frame(mx, my, HudLayout.MomentumW, HudLayout.MomentumH, 1, LedgerArt.Ink);

                // depth: the floor struck into the seal in the area's accent, and a tally of floors in ink
                var seal = HudLayout.DepthSeal;
                int n = Digits(run.floor);
                int sc = n >= 3 ? 3 : 5;
                float tw = TextW(n, sc);
                Struck(n, Mathf.Round(seal.center.x - tw / 2), Mathf.Round(seal.center.y - PixelFont.H * sc / 2f), sc, run.floor > 1 ? Accent[area] : AccentDim[area]);
                Tally(run.floor);

            }

            void Dash(int rowY) => Rect(HudLayout.ValueX + 2, rowY + 12, 14, 2, LedgerArt.InkFaint);

            void Overflow(HeartKind kind, int count)
            {
                int cap = HudLayout.HeartCapacity(kind);
                if (count <= cap) return;
                var tl = HudLayout.HeartTopLeft(kind, cap - 1);
                int n = Digits(count - cap, "+");
                Text(_digits, n, tl.x + HudLayout.HeartW + 2, tl.y + 6, 2, LedgerArt.Ink);
            }

            // floors as tally marks: four strokes and a fifth struck through them, two lines of ten
            void Tally(int floors)
            {
                int shown = Mathf.Min(floors, 20);
                for (int i = 0; i < shown; i++)
                {
                    int line = i / 10, inGroup = i % 5, group = (i % 10) / 5;
                    float x = HudLayout.TallyX + group * 30 + inGroup * 5, y = HudLayout.DepthY + 6 + line * 24;
                    if (inGroup < 4) { Rect(x, y, 2, HudLayout.TallyH, LedgerArt.Ink); continue; }
                    float x0 = HudLayout.TallyX + group * 30 - 3;
                    for (int k = 0; k <= 24; k++) Rect(x0 + k, y + HudLayout.TallyH - 3 - k * (HudLayout.TallyH - 4) / 24f, 2, 2, LedgerArt.RedInk);
                }
                if (floors > 20)
                {
                    int n = Digits(floors - 20, "+");
                    Text(_digits, n, HudLayout.TallyX, HudLayout.DepthY + 56, 2, LedgerArt.InkSoft);
                }
            }

            // ---- the right page: the map, inked on the chart ----
            static readonly Color32[] Accent = AreaInk(false), AccentDim = AreaInk(true);
            static Color32[] AreaInk(bool dim)
            {
                var a = new Color32[4];
                for (int i = 0; i < 4; i++) { var p = AreaPalette.For((Area)i); a[i] = Hex.C(dim ? p.AccentDim : p.Accent); }
                return a;
            }
            static readonly Color32 Soot = Hex.C("#0d0a08"), Cream = Hex.C("#e8dcc0"), Dull = Hex.C("#6b5a44"), Spent = Hex.C("#c85a5a"),
                RobeLit = Hex.C("#7a55c4"), BlinkInk = Hex.C("#2c3a5c"), BossGreen = Hex.C("#5ee27a"), PhaseGray = Hex.C("#c8b08a");
            static readonly Color32[] PickupInk = { default, Hex.C("#e8395a"), Hex.C("#9aa0ab"), Hex.C("#d8dee9"), Hex.C("#ffd23d") };
            static readonly Color32 Visited = Hex.C("#d9c9a0"), StartFill = Hex.C("#cbb98c"), Shell = Hex.Rgba(70, 50, 30, 0.10f);

            void DrawMap(RunState run, int area)
            {
                if (run.dungeon == null) return;
                var m = HudLayout.Map;
                int cell = HudLayout.MapCell, box = HudLayout.MapBox, off = (cell - box) / 2;
                var rooms = run.dungeon.AllRooms;
                float bossPulse = Pulse(_hud.Time, 5.4f);
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    if (r.Visited || !Known(run, r)) continue;
                    // a known room not yet walked: a dashed pencil outline, and a padlock if a key opens it
                    float x = m.x + r.X * cell + off, y = m.y + r.Y * cell + off;
                    Rect(x, y, box, box, Shell);
                    for (int k = 0; k < box; k += 4)
                    {
                        Rect(x + k, y, 2, 1, LedgerArt.InkFaint); Rect(x + k, y + box - 1, 2, 1, LedgerArt.InkFaint);
                        Rect(x, y + k, 1, 2, LedgerArt.InkFaint); Rect(x + box - 1, y + k, 1, 2, LedgerArt.InkFaint);
                    }
                    bool locked = r.Type == RoomKind.Boss ? !run.bossUnlocked && !run.player.hasGold : r.Type == RoomKind.Item && !run.itemUnlocked && !run.player.hasSilver;
                    if (locked) Centre(r.Type == RoomKind.Boss ? _gLockGold : _gLockSilver, x + box / 2f, y + box / 2f);
                }
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    if (!r.Visited) continue;
                    float x = m.x + r.X * cell + off, y = m.y + r.Y * cell + off;
                    bool here = r.X == run.curX && r.Y == run.curY;
                    // doors first, as ink bridges across the gap to the neighbour
                    var ink = LedgerArt.Ink;
                    if (r.Doors.Contains(Dir.N)) Rect(x + box / 2f - 3, y - off - 1, 6, off + 1, ink);
                    if (r.Doors.Contains(Dir.S)) Rect(x + box / 2f - 3, y + box, 6, off + 1, ink);
                    if (r.Doors.Contains(Dir.W)) Rect(x - off - 1, y + box / 2f - 3, off + 1, 6, ink);
                    if (r.Doors.Contains(Dir.E)) Rect(x + box, y + box / 2f - 3, off + 1, 6, ink);
                    var fill = here ? Accent[area] : r.Type == RoomKind.Start ? StartFill : Visited;
                    Rect(x, y, box, box, fill);
                    if (r.Type == RoomKind.Boss) Rect(x, y, box, box, Hex.Rgba(163, 40, 31, 0.22f));
                    Frame(x, y, box, box, here ? 2 : 1, ink);
                    if (r.Type == RoomKind.Item || r.Type == RoomKind.Boss)
                    {
                        bool sealedRoom = r.Type == RoomKind.Boss ? !run.bossUnlocked : !run.itemUnlocked;
                        var g = sealedRoom ? (r.Type == RoomKind.Boss ? _gLockGold : _gLockSilver) : r.Type == RoomKind.Boss ? _gBossRed : _gItem;
                        Centre(g, x + box / 2f, y + box / 2f);
                    }
                    if (r.Type == RoomKind.Boss && !r.Cleared)
                    {
                        // a Warden walked in on and not finished: a red ring that breathes, ticks at the corners
                        var red = Hex.Rgba(200, 40, 30, 0.50f + 0.45f * bossPulse);
                        Frame(x - 3, y - 3, box + 6, box + 6, 2, red);
                        for (int k = 0; k < 4; k++) Rect(x - 5 + (k % 2) * (box + 6), y - 5 + (k / 2) * (box + 6), 4, 4, red);
                    }
                    if (here)
                    {
                        // you are here: an ink dot with the player's robe colour in it
                        float cx = x + box / 2f, cy = y + box / 2f;
                        _q.Sprite(T, _disc, cx - 5, cy - 5, 10, 10, LedgerArt.Ink); _q.Sprite(T, _disc, cx - 3, cy - 3, 6, 6, RobeLit);
                    }
                    int pk = MapPickup(run, r);
                    if (pk > 0)
                    {
                        var pc = PickupInk[pk];
                        Rect(x + box - 9, y + box - 9, 6, 6, ink); Rect(x + box - 8, y + box - 8, 4, 4, pc);
                    }
                }
            }

            void Centre(in Region g, float cx, float cy) => _q.Sprite(_glyphs.Texture, g, Mathf.Round(cx - g.W / 2f), Mathf.Round(cy - g.H / 2f), g.W, g.H, QuadBatch.Alpha(1));

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

            // ---- the kit ----
            void DrawKit(RunState run)
            {
                var p = run.player;
                int wi = Mathf.Clamp(p.weaponIdx, 0, Weapons.All.Length - 1);
                Arm(HudLayout.Slot(0), _icons[wi], WeaponReady(p), _mouseGun[wi]);
                bool hook = p.altMode == "hook";
                Arm(HudLayout.Slot(2), _icons[hook ? 5 : 4], AltReady(p), hook ? _mouseHook : _mouseBlast);

                var s = HudLayout.Slot(1);
                var held = Items.ActiveItem(run);
                if (held == null)
                {
                    // the empty hand: a dotted square, "there is a thing here and you do not have it"
                    float q = s.width / 2 - 14, cx = s.center.x, cy = s.center.y;
                    for (float k = -q; k < q; k += 7)
                    {
                        var c = Hex.Rgba(107, 90, 68, 0.6f);
                        Rect(cx + k, cy - q, 3, 1, c); Rect(cx + k, cy + q, 3, 1, c); Rect(cx - q, cy + k, 1, 3, c); Rect(cx + q, cy + k, 1, 3, c);
                    }
                }
                else
                {
                    var tile = _item26.TryGetValue(held.Id, out var tr) ? tr : _item26["?"];
                    Spr(tile, Mathf.Round(s.center.x - tile.W / 2f), s.y + 9, QuadBatch.Alpha(1));
                    Rect(s.x + 7, s.yMax - 20, s.width - 14, 13, Hex.Rgba(8, 4, 2, 0.74f));
                    int charges = held.Charges ?? 0;
                    bool any = charges > 0;
                    Text("Q", s.x + 10, s.yMax - 18, 2, any ? Cream : Dull);
                    if (held.Charges != int.MaxValue)
                    {
                        int n = Digits(charges, "x");
                        Text(_digits, n, s.xMax - 9 - TextW(n, 2), s.yMax - 18, 2, any ? (_itemInk.TryGetValue(held.Id, out var ic) ? ic : Cream) : Spent);
                    }
                }

                // the passive items, as small tiles in a row
                int shown = 0;
                for (int i = 0; i < run.loadout.Count; i++)
                {
                    var it = run.loadout[i];
                    if (it.Slot == Items.ActiveSlot) continue;
                    var tile = _item18.TryGetValue(it.Id, out var tr) ? tr : _item18["?"];
                    float x = HudLayout.RightX + 20 + (shown % HudLayout.CarryPerLine) * HudLayout.CarryStep, y = HudLayout.CarryY + (shown / HudLayout.CarryPerLine) * HudLayout.CarryStep;
                    if (++shown > HudLayout.CarryPerLine * 2) break;
                    Rect(x + 1, y + 1, tile.W, tile.H, Hex.Rgba(40, 24, 10, 0.35f));
                    Spr(tile, x, y, QuadBatch.Alpha(1));
                }
            }

            void Arm(Rect s, in Region icon, float ready, in Region mouse)
            {
                Spr(icon, Mathf.Round(s.center.x - icon.W / 2f), Mathf.Round(s.center.y - icon.H / 2f), QuadBatch.Alpha(1));
                int shade = ShadePx(ready);   // the cooldown, drawn down from the top of the well in whole pixels
                if (shade > 0) Rect(s.x + 8, s.y + 8, s.width - 16, shade, Hex.Rgba(8, 4, 2, 0.62f));
                _q.Centred(T, mouse, s.xMax - 7, s.yMax - 8, QuadBatch.Alpha(1));
            }

            // ---- the Warden ----
            static readonly Color32[] PhaseInk = { Hex.C("#8fd89a"), Hex.C("#8fd89a"), Hex.C("#e8c46a"), Hex.C("#e8804a") };

            void DrawBoss(RunState run)
            {
                var e = LiveBoss(run);
                if (e == null) return;
                var w = HudLayout.BossWell;
                float frac = Mathf.Clamp01((float)(e.hp / e.maxHp));
                float fw = Mathf.Round(w.width * frac);
                Rect(w.x, w.y, fw, w.height, BossGreen);
                if (fw > 0) { Rect(w.x, w.y, fw, 2, Hex.Rgba(190, 255, 200, 0.45f)); Rect(w.x, w.yMax - 3, fw, 3, Hex.Rgba(0, 60, 20, 0.35f)); }
                Notch(w, Balance.BossPhase1); Notch(w, Balance.BossPhase2);
                var plate = HudLayout.BossPlate;
                int ph = Mathf.Clamp(e.phase, 1, 3);
                Text("THE WARDEN", plate.x + 13, plate.y + 10, 2, Soot);
                Text("THE WARDEN", plate.x + 12, plate.y + 9, 2, PhaseInk[ph]);
                int n = Digits(ph, "PHASE ");
                Text(_digits, n, plate.xMax - 12 - TextW(n, 2) + 1, plate.y + 10, 2, Soot);
                Text(_digits, n, plate.xMax - 12 - TextW(n, 2), plate.y + 9, 2, PhaseGray);
            }

            void Notch(Rect w, double th)
            {
                float nx = w.x + Mathf.Round(w.width * (float)th);
                Rect(nx - 1, w.y - 4, 3, w.height + 8, Cream);
                Rect(nx - 1, w.y - 4, 3, 1, Soot);
            }
        }
    }
}
