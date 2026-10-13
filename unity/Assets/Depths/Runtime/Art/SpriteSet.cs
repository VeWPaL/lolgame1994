using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity.Art
{
    /// <summary>
    /// Every fixed sprite in the game, baked once into one atlas and found by name. The names are
    /// built by the static helpers below, so a caller cannot spell one that was never baked.
    /// </summary>
    public static class SpriteSet
    {
        static Atlas _atlas;
        public static Atlas Atlas => _atlas ??= Atlas.Build(All());

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
        public static string Glyph(string kind, string color) => "glyph:" + kind + ":" + color;
        public const string Glow = "glow", Muzzle = "muzzle", White = "white", Disc = "disc";

        static string BodyName(BodyKind k) => k switch
        {
            BodyKind.Brunch => "brunch", BodyKind.Shooter => "shooter", BodyKind.Gunner => "gunner", BodyKind.Boss => "boss", _ => "lunger",
        };

        /// <summary>The pixel size each body is drawn at (ENEMY[...].art).</summary>
        public static int Art(BodyKind k) => k switch { BodyKind.Brunch => 1, BodyKind.Gunner => 3, BodyKind.Boss => 4, _ => 2 };

        static bool ItemKnown(string id) { foreach (var i in ItemArt.Ids) if (i == id) return true; return false; }

        public static IEnumerable<string> MouseColors()
        {
            foreach (var w in Weapons.All) yield return w.Color;
            yield return Sprites.BlastColor; yield return Sprites.HookColor;
        }

        public static IEnumerable<string> GlyphColors => new[] { "#ffd23d", "#d8dee9", "#2a1a10" };

        static List<(string, Img)> All()
        {
            var list = new List<(string, Img)>();
            for (int f = 0; f < 4; f++)
            {
                var pr = Sprites.Frame(Sprites.PlayerUpper, f);
                list.Add((Player(f, false), Sprites.Bake(pr, Sprites.PlayerPal, 2)));
                list.Add((Player(f, true), Sprites.Bake(pr, Sprites.PlayerPal, 2, Flash)));
                foreach (BodyKind k in new[] { BodyKind.Lunger, BodyKind.Brunch, BodyKind.Boss, BodyKind.Shooter, BodyKind.Gunner })
                {
                    bool ranged = k == BodyKind.Shooter || k == BodyKind.Gunner;
                    var rows = Sprites.Frame(ranged ? Sprites.ShooterUpper : Sprites.LungerUpper, f);
                    var pal = k == BodyKind.Gunner ? Sprites.GunnerPal : k == BodyKind.Brunch ? Sprites.BrunchPal
                            : k == BodyKind.Shooter ? Sprites.ShooterPal : k == BodyKind.Boss ? Sprites.BossPal : Sprites.LungerPal;
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
            foreach (var kv in Sprites.MiniGlyph)
                foreach (var c in GlyphColors) list.Add((Glyph(kv.Key, c), Sprites.Glyph(kv.Value, Hex.C(c), 2)));
            list.Add((Glow, Sprites.Glow(32, Color.white, new Color(1, 1, 1, 0))));
            list.Add((Muzzle, Sprites.Glow(11, new Color(230 / 255f, 200 / 255f, 1, 1), new Color(180 / 255f, 110 / 255f, 1, 0))));
            var disc = new Img(64, 64); disc.Disc(32, 32, 32, Flash);   // shells, shadows and dots: tinted and scaled
            list.Add((Disc, disc));
            var white = new Img(4, 4); white.Fill(0, 0, 4, 4, Flash);
            list.Add((White, white));
            return list;
        }
    }
}
