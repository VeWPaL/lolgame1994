using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity.Art
{
    /// <summary>Every fixed sprite in the game, baked once into one atlas and found by name. [h:SpriteSet-1]</summary>
    public static class SpriteSet
    {
        static Atlas _atlas;
        public static Atlas Atlas => _atlas ??= Atlas.Build(All());

        // ---- regions resolved once, so a frame looks sprites up by index and builds no names ----
        static Region[] _bodies, _players, _hearts, _keys, _guns;
        static Dictionary<string, Region>[] _items;
        static Region _blast, _hook, _white;
        static readonly int Kinds = System.Enum.GetValues(typeof(BodyKind)).Length, Schemes = System.Enum.GetValues(typeof(Sprites.HeartScheme)).Length;

        static void Resolve()
        {
            if (_bodies != null) return;
            var a = Atlas;
            _bodies = new Region[Kinds * 8];
            foreach (BodyKind k in System.Enum.GetValues(typeof(BodyKind)))
                for (int f = 0; f < 4; f++) { _bodies[((int)k * 4 + f) * 2] = a[Body(k, f, false)]; _bodies[((int)k * 4 + f) * 2 + 1] = a[Body(k, f, true)]; }
            _players = new Region[8];
            for (int f = 0; f < 4; f++) { _players[f * 2] = a[Player(f, false)]; _players[f * 2 + 1] = a[Player(f, true)]; }
            _hearts = new Region[Schemes * HeartPx.Length * (Sprites.HeartSteps + 1)];
            for (int s = 0; s < Schemes; s++)
                for (int p = 0; p < HeartPx.Length; p++)
                    for (int q = 0; q <= Sprites.HeartSteps; q++)
                        _hearts[(s * HeartPx.Length + p) * (Sprites.HeartSteps + 1) + q] = a["heart:" + (Sprites.HeartScheme)s + ":" + HeartPx[p] + ":" + q];
            _keys = new Region[2 * KeyPx.Length * 2];
            for (int g = 0; g < 2; g++) for (int p = 0; p < KeyPx.Length; p++) for (int d = 0; d < 2; d++) _keys[(g * KeyPx.Length + p) * 2 + d] = a[Key(g == 1, KeyPx[p], d == 1)];
            _guns = new Region[Weapons.All.Length];
            for (int i = 0; i < _guns.Length; i++) _guns[i] = a[Icon(i.ToString())];
            _blast = a[Icon("blast")]; _hook = a[Icon("hook")];
            _items = new Dictionary<string, Region>[ItemPx.Length];
            for (int p = 0; p < ItemPx.Length; p++)
            {
                _items[p] = new Dictionary<string, Region>();
                foreach (var id in ItemArt.Ids) _items[p][id] = a[Item(id, ItemPx[p])];
                _items[p]["?"] = a["item:?:" + ItemPx[p]];
            }
            var w = a[White];
            float u = (w.U0 + w.U1) / 2, v = (w.V0 + w.V1) / 2;
            _white = new Region(1, 1, u, v, u, v);
        }

        public static Region BodyR(BodyKind k, int frame, bool flash) { Resolve(); return _bodies[((int)k * 4 + frame) * 2 + (flash ? 1 : 0)]; }
        public static Region PlayerR(int frame, bool flash) { Resolve(); return _players[frame * 2 + (flash ? 1 : 0)]; }
        public static Region HeartR(Sprites.HeartScheme s, int px, float fill)
        {
            Resolve();
            int p = System.Array.IndexOf(HeartPx, px);
            if (p < 0) throw new System.ArgumentException("no hearts baked at px " + px);
            return _hearts[((int)s * HeartPx.Length + p) * (Sprites.HeartSteps + 1) + Sprites.HeartStep(fill)];
        }
        public static Region KeyR(bool gold, int px, bool dim) { Resolve(); return _keys[((gold ? 1 : 0) * KeyPx.Length + System.Array.IndexOf(KeyPx, px)) * 2 + (dim ? 1 : 0)]; }
        public static Region GunR(int i) { Resolve(); return _guns[i]; }
        public static Region AltR(bool hook) { Resolve(); return hook ? _hook : _blast; }
        public static Region ItemR(string id, int px)
        {
            Resolve();
            var t = _items[System.Array.IndexOf(ItemPx, px)];
            return id != null && t.TryGetValue(id, out var r) ? r : t["?"];
        }

        static Dictionary<string, Region>[] _itemPictures;

        /// <summary>An item's picture (ItemIcons) at px 2 or 4, by index rather than by name; false for an item that only has its lettered tile.</summary>
        public static bool ItemIconR(string id, int px, out Region r)
        {
            Resolve();
            if (_itemPictures == null)
            {
                _itemPictures = new Dictionary<string, Region>[ItemIconPx.Length];
                for (int p = 0; p < ItemIconPx.Length; p++)
                {
                    _itemPictures[p] = new Dictionary<string, Region>();
                    foreach (var i in ItemIcons.Ids) _itemPictures[p][i] = Atlas[ItemIcon(i, ItemIconPx[p])];
                }
            }
            r = default;
            int at = System.Array.IndexOf(ItemIconPx, px);
            return at >= 0 && id != null && _itemPictures[at].TryGetValue(id, out r);
        }

        /// <summary>The middle of the white sprite's texel: a solid-colour quad that stays in the atlas batch.</summary>
        public static Region WhiteTexel { get { Resolve(); return _white; } }

        public static readonly Color32 Flash = new Color32(255, 255, 255, 255);
        public static readonly int[] HeartPx = { 2, 3, 4 }, KeyPx = { 1, 2, 3 }, ItemPx = { 18, 22, 26 };

        // ---- names ----
        public static string Body(BodyKind k, int frame, bool flash) => BodyName(k) + frame + (flash ? "!" : "");
        public static string Player(int frame, bool flash) => "player" + frame + (flash ? "!" : "");
        public static string Heart(Sprites.HeartScheme s, int px, float fill) => "heart:" + s + ":" + px + ":" + Sprites.HeartStep(fill);
        public static string Key(bool gold, int px, bool dim) => (gold ? "goldkey:" : "key:") + px + (dim ? ":dim" : "");
        public static string Icon(string idx) => "icon:" + idx;
        public static string Mouse(bool left, string color) => "mouse:" + (left ? "L:" : "R:") + color;
        public static string Item(string id, int px) => "item:" + (ItemKnown(id) ? id : "?") + ":" + px;
        /// <summary>An item's picture (ItemIcons) at px 2 or 4; null for an item that only has its lettered tile.</summary>
        public static string ItemIcon(string id, int px) => ItemIcons.Has(id) ? "itemicon:" + id + ":" + px : null;
        public static readonly int[] ItemIconPx = { 2, 4 };
        public const string Glow = "glow", Muzzle = "muzzle", White = "white", Disc = "disc", CrackH = "crack:h", CrackV = "crack:v";

        static string BodyName(BodyKind k) => k switch
        {
            BodyKind.Brunch => "brunch", BodyKind.Shooter => "shooter", BodyKind.Gunner => "gunner", BodyKind.Boss => "boss",
            BodyKind.Splitter => "splitter", BodyKind.Mortar => "mortar", BodyKind.Burrower => "burrower", BodyKind.Sprayer => "sprayer", BodyKind.Egg => "egg", BodyKind.Gatekeeper => "gatekeeper",
            _ => "lunger",
        };

        /// <summary>Every body kind's rows for a walk frame and its palette.</summary>
        public static (string[] rows, Dictionary<char, Color32> pal) BodyArt(BodyKind k, int f) => k switch
        {
            BodyKind.Shooter => (Sprites.Frame(Sprites.ShooterUpper, f), Sprites.ShooterPal),
            BodyKind.Gunner => (Sprites.Frame(Sprites.ShooterUpper, f), Sprites.GunnerPal),
            BodyKind.Brunch => (Sprites.Frame(Sprites.LungerUpper, f), Sprites.BrunchPal),
            BodyKind.Boss => (Sprites.Frame(Sprites.LungerUpper, f), Sprites.BossPal),
            BodyKind.Splitter => (Sprites.Frame(Sprites.SplitterUpper, f), Sprites.SplitterPal),
            BodyKind.Mortar => (Sprites.Frame(Sprites.MortarUpper, f), Sprites.MortarPal),
            BodyKind.Burrower => (Sprites.Frame(Sprites.BurrowerUpper, f), Sprites.BurrowerPal),
            BodyKind.Sprayer => (Sprites.Frame(Sprites.SprayerUpper, f), Sprites.SprayerPal),
            BodyKind.Egg => (Sprites.EggRows, Sprites.EggPal),
            BodyKind.Gatekeeper => (Sprites.Frame(Sprites.GatekeeperUpper, f), Sprites.GatekeeperPal),
            _ => (Sprites.Frame(Sprites.LungerUpper, f), Sprites.LungerPal),
        };

        /// <summary>The pixel size each body is drawn at (ENEMY[...].art).</summary>
        public static int Art(BodyKind k) => k switch { BodyKind.Brunch => 1, BodyKind.Gunner => 3, BodyKind.Boss => 4, BodyKind.Gatekeeper => 4, _ => 2 };

        static bool ItemKnown(string id) { foreach (var i in ItemArt.Ids) if (i == id) return true; return false; }

        public static IEnumerable<string> MouseColors()
        {
            foreach (var w in Weapons.All) yield return w.Color;
            yield return Sprites.BlastColor; yield return Sprites.HookColor;
        }

        static List<(string, Img)> All()
        {
            var list = new List<(string, Img)>();
            for (int f = 0; f < 4; f++)
            {
                var pr = Sprites.Frame(Sprites.PlayerUpper, f);
                list.Add((Player(f, false), Sprites.Bake(pr, Sprites.PlayerPal, 2)));
                list.Add((Player(f, true), Sprites.Bake(pr, Sprites.PlayerPal, 2, Flash)));
                foreach (BodyKind k in System.Enum.GetValues(typeof(BodyKind)))
                {
                    var (rows, pal) = BodyArt(k, f);
                    list.Add((Body(k, f, false), Sprites.Bake(rows, pal, Art(k))));
                    list.Add((Body(k, f, true), Sprites.Bake(rows, pal, Art(k), Flash)));
                }
            }
            foreach (int px in KeyPx)
                foreach (bool gold in new[] { false, true })
                {
                    list.Add((Key(gold, px, false), Sprites.Bake(Sprites.KeyRows, gold ? Sprites.KeyGold : Sprites.KeySilver, px)));
                    list.Add((Key(gold, px, true), Sprites.Bake(Sprites.KeyRows, gold ? Sprites.KeyGoldDim : Sprites.KeySilverDim, px)));
                }
            foreach (Sprites.HeartScheme s in System.Enum.GetValues(typeof(Sprites.HeartScheme)))
                foreach (int px in HeartPx)
                    for (int q = 0; q <= Sprites.HeartSteps; q++)
                        list.Add(("heart:" + s + ":" + px + ":" + q, Sprites.Bake(Sprites.HeartRowsFor(q / (float)Sprites.HeartSteps), Sprites.HeartPal(s), px)));
            for (int i = 0; i < Weapons.All.Length; i++) list.Add((Icon(i.ToString()), Sprites.Icon(i.ToString(), Weapons.All[i].Color)));
            list.Add((Icon("blast"), Sprites.Icon("blast", Sprites.BlastColor)));
            list.Add((Icon("hook"), Sprites.Icon("hook", Sprites.HookColor)));
            foreach (var c in MouseColors())
                foreach (bool left in new[] { true, false })
                {
                    if (list.Exists(e => e.Item1 == Mouse(left, c))) continue;
                    var pal = new Dictionary<char, Color32> { ['m'] = Hex.C("#7d838f"), ['d'] = Hex.C("#20222b"),
                        ['L'] = left ? Hex.C(c) : Hex.C("#4a4f5a"), ['R'] = left ? Hex.C("#4a4f5a") : Hex.C(c) };
                    list.Add((Mouse(left, c), Sprites.Bake(Sprites.MouseRows, pal, 2)));
                }
            foreach (int px in ItemPx)
            {
                foreach (var id in ItemArt.Ids) list.Add((Item(id, px), Sprites.ItemTile(id, px)));
                list.Add(("item:?:" + px, Sprites.ItemTile("?", px)));
            }
            foreach (int px in ItemIconPx)
                foreach (var id in ItemIcons.Ids) list.Add(("itemicon:" + id + ":" + px, ItemIcons.Bake(id, px)));
            list.Add((Glow, Sprites.Glow(32, Color.white, new Color(1, 1, 1, 0))));
            list.Add((Muzzle, Sprites.Glow(11, new Color(230 / 255f, 200 / 255f, 1, 1), new Color(180 / 255f, 110 / 255f, 1, 0))));
            list.Add((CrackH, Sprites.Crack(false, Hex.C("#07080b", 0.95f), Hex.C("#b4bccb", 0.7f), Hex.C("#000000", 0.28f))));
            list.Add((CrackV, Sprites.Crack(true, Hex.C("#07080b", 0.95f), Hex.C("#b4bccb", 0.7f), Hex.C("#000000", 0.28f))));
            var disc = new Img(64, 64); disc.Disc(32, 32, 32, Flash);   // shells, shadows and dots: tinted and scaled
            list.Add((Disc, disc));
            var white = new Img(4, 4); white.Fill(0, 0, 4, 4, Flash);
            list.Add((White, white));
            return list;
        }
    }
}
