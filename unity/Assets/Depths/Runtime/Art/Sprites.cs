using System;
using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity.Art
{
    /// <summary>The JS pixel art (src/10-art.js at js-final), rows and palettes verbatim, and its bakers. [h:Sprites-1]</summary>
    public static class Sprites
    {
        static string Mirror(string h) { var a = h.ToCharArray(); Array.Reverse(a); return h + new string(a); }
        static string[] Mirror(params string[] rows) { var r = new string[rows.Length]; for (int i = 0; i < rows.Length; i++) r[i] = Mirror(rows[i]); return r; }
        static Dictionary<char, Color32> Pal(params (char k, string hex)[] e)
        {
            var d = new Dictionary<char, Color32>();
            foreach (var (k, hex) in e) d[k] = Hex.C(hex);
            return d;
        }

        public static readonly string[][] LegFrames =
        {
            new[] { "..bb....bb..", "..bbb...bb.." },
            new[] { "...bb..bb...", "...bb..bb..." },
            new[] { "..bb....bb..", "..bb...bbb.." },
            new[] { "...bb..bb...", "...bb..bb..." },
        };

        public static readonly string[] PlayerUpper = Mirror(".....h", "....hh", "...hhh", "..hhhh", ".hhhhh", ".yyyyy", "hhhhhh", "..ffff", "..ffef", "..fwww", "...www", ".mmtww", "mmtttw", "smyyyy", ".mtttt", "mmmmmm");
        public static readonly Dictionary<char, Color32> PlayerPal = Pal(('h', "#4b2f86"), ('y', "#e8c04a"), ('f', "#f3cfa8"), ('e', "#1a1220"), ('w', "#ece8f5"), ('m', "#7a55c4"), ('t', "#5b3a9e"), ('s', "#f3cfa8"), ('b', "#2a1d38"));
        public static readonly string[] LungerUpper = Mirror("h.....", "hh....", ".hdddd", "..dddd", "..dyyd", "..dkkk", ".mmttt", "mmtttt", "dmtttt", "dmtttt", ".bbbbb", ".bpppp", "..pppp");
        public static readonly Dictionary<char, Color32> LungerPal = Pal(('h', "#e6d9b8"), ('d', "#d1495b"), ('y', "#ffe066"), ('k', "#3a141c"), ('m', "#b03a4a"), ('t', "#8f2b3a"), ('b', "#3a141c"), ('p', "#5c1f28"));
        public static readonly Dictionary<char, Color32> BossPal = Pal(('h', "#f2e2b0"), ('d', "#ff8a3d"), ('y', "#fff1a0"), ('k', "#3d1c08"), ('m', "#d9662a"), ('t', "#b5501f"), ('b', "#3d1c08"), ('p', "#5c2c10"));
        public static readonly string[] ShooterUpper = Mirror("...kkk", "..kkkk", ".kkkkk", ".kkeed", ".kkddd", ".kkmtt", "mmtttt", "mstttt", ".ttttt", ".ttttt", "tttttt", "tttttt");
        public static readonly Dictionary<char, Color32> ShooterPal = Pal(('k', "#4a2f7a"), ('d', "#160d24"), ('e', "#7ff5ff"), ('m', "#6a3fae"), ('t', "#5b3a9e"), ('s', "#c9a6ff"), ('b', "#241a3d"));
        public static readonly Dictionary<char, Color32> GunnerPal = Pal(('k', "#5a2436"), ('d', "#1a0c12"), ('e', "#ffe08a"), ('m', "#8a2f4a"), ('t', "#6d2238"), ('s', "#ff9db0"), ('b', "#2a1018"));
        public static readonly Dictionary<char, Color32> BrunchPal = Pal(('h', "#eef7a4"), ('d', "#5f6d1c"), ('y', "#f6ff3d"), ('k', "#171c08"), ('m', "#bfe04a"), ('t', "#9cc033"), ('b', "#3b4712"), ('p', "#5d6d1a"));

        // ---- the middle enemies (C# only, after js-final): new placeholder art in the same grammar,
        // a silhouette each so they read apart at a glance: prongs, a tube, claws, nozzles, a shell ----
        public static readonly string[] SplitterUpper = Mirror(".k...k", ".kk..k", "..kkkk", "..keed", "..kddd", ".mmttt", "mmtttt", "mstttt", ".ttttt", "..tttt", "..tttt", ".ttttt");
        public static readonly Dictionary<char, Color32> SplitterPal = Pal(('k', "#7a2a6a"), ('e', "#ffd1f4"), ('d', "#1f0a1a"), ('m', "#c2449c"), ('t', "#9b2f7d"), ('s', "#ff9de0"), ('b', "#2a0f24"));
        public static readonly string[] MortarUpper = Mirror("....pp", "....pp", ".kkkpp", "kkkkkk", "kkkeek", ".kkddd", "mmtttt", "mttttt", "tttttt", "tttttt", ".ttttt");
        public static readonly Dictionary<char, Color32> MortarPal = Pal(('p', "#3b2a1a"), ('k', "#6b4a2a"), ('e', "#ffe08a"), ('d', "#1a1008"), ('m', "#b8742e"), ('t', "#8a5420"), ('b', "#2a1a0c"));
        public static readonly string[] BurrowerUpper = Mirror("...hhh", "..hhhh", ".hhhhh", ".hheyh", ".hhhyy", "chhhhh", "cchhhh", ".chhhh", "..hhhh", "...hhh");
        public static readonly Dictionary<char, Color32> BurrowerPal = Pal(('h', "#5a4636"), ('e', "#0d0907"), ('y', "#e8a0a0"), ('c', "#e8d8b0"), ('b', "#2a1f18"));
        public static readonly string[] SprayerUpper = Mirror("...nn.", "...nn.", "..kkkk", ".kkeek", ".kkkkk", ".mmttt", "mmtttt", "mstttt", ".ttttt", ".ttttt", "tttttt");
        public static readonly Dictionary<char, Color32> SprayerPal = Pal(('n', "#c9ffd0"), ('k', "#2f6b3a"), ('e', "#ff4d4d"), ('m', "#4c9a5a"), ('t', "#357a45"), ('s', "#9cf0a8"), ('b', "#183a20"));
        // the Gatekeeper, the floor boss of 1-13: horns, a visor lit in its shells' blue, a brass-barred gate for a chest
        public static readonly string[] GatekeeperUpper = Mirror("h.....", "hh....", ".hkkkk", ".kkkkk", ".kteee", ".kkttt", "mmkkkk", "mgmgmg", "mgmgmg", "mgmgmg", ".ttttt", "..tttt");
        public static readonly Dictionary<char, Color32> GatekeeperPal = Pal(('h', "#e8dcc0"), ('k', "#4a5566"), ('t', "#2e3644"), ('e', "#7fe0ff"), ('m', "#7a8aa0"), ('g', "#c9a24a"), ('b', "#1c2028"));
        /// <summary>The egg has no legs: every frame is the same shell on its base.</summary>
        /// <summary>The blink's colour (the ledger's blink well, the blink puffs); momentum wears it too.</summary>
        public static readonly Color32 BlinkBlue = new Color32(0x7f, 0xc4, 0xff, 255), BlinkDeep = new Color32(0x2f, 0x5c, 0x9e, 255);

        public static readonly string[] EggRows = Mirror("...ss", "..sss", ".sscc", ".scce", "scccc", "scccc", "scccc", ".sccc", ".ssss", "..bbb");
        public static readonly Dictionary<char, Color32> EggPal = Pal(('s', "#e8f5c0"), ('c', "#9ad84a"), ('e', "#2a0a0a"), ('b', "#4a5a2a"));

        public static string[] Frame(string[] upper, int f)
        {
            var r = new string[upper.Length + 2];
            Array.Copy(upper, r, upper.Length);
            r[upper.Length] = LegFrames[f][0]; r[upper.Length + 1] = LegFrames[f][1];
            return r;
        }

        public static readonly string[] KeyRows = { ".ggg.....", "g...g....", "g...ggggg", "g...g.g.g", ".ggg....." };
        public static readonly Dictionary<char, Color32> KeyGold = Pal(('g', "#ffd23d")), KeySilver = Pal(('g', "#d8dee9")),
            KeyGoldDim = Pal(('g', "#6e5f22")), KeySilverDim = Pal(('g', "#5b6068"));
        public static readonly string[] MouseRows = { "..mmm..", ".mmmmm.", "mmmdmmm", "mLmdmRm", "mLmdmRm", "mLmdmRm", "mmmdmmm", ".mmmmm.", "..mmm.." };

        public static readonly Color32 Outline = Hex.C("#14151a");

        /// <summary>bakeSprite: a 2px pad, a dark outline one pixel proud of every lit cell, then the
        /// palette; with <paramref name="flash"/> the silhouette alone, in that colour.</summary>
        public static Img Bake(string[] rows, Dictionary<char, Color32> pal, int px, Color32? flash = null)
        {
            int w = rows[0].Length, h = rows.Length, pad = 2;
            var img = new Img(w * px + pad * 2, h * px + pad * 2);
            var edge = flash ?? Outline;
            for (int ry = 0; ry < h; ry++)
                for (int rx = 0; rx < w; rx++)
                    if (rows[ry][rx] != '.') img.Fill(pad + rx * px - 1, pad + ry * px - 1, px + 2, px + 2, edge);
            if (flash == null)
                for (int ry = 0; ry < h; ry++)
                    for (int rx = 0; rx < w; rx++)
                    {
                        char ch = rows[ry][rx];
                        if (ch == '.') continue;
                        img.Fill(pad + rx * px, pad + ry * px, px, px, pal[ch]);
                    }
            return img;
        }

        // ---- hearts: filled by fraction, quantised (heartRows / drawHeart) ----
        public static readonly string[] HeartRows = { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#..." };
        public const int HeartSteps = 16;
        public enum HeartScheme { Red, Gray, DimGray, Regen }

        /// <summary>R lit, H the lit highlight, E unlit, G the unlit highlight. Regen is the C#-only
        /// rose-violet heart (HeartRow.Regen); the other three are the JS's.</summary>
        public static Dictionary<char, Color32> HeartPal(HeartScheme s) => s switch
        {
            HeartScheme.Red => Pal(('R', "#e8395a"), ('H', "#ff9db0"), ('E', "#3a2530"), ('G', "#55384a")),
            HeartScheme.Gray => Pal(('R', "#9aa0ab"), ('H', "#dfe3ea"), ('E', "#3a2530"), ('G', "#55384a")),
            HeartScheme.DimGray => Pal(('R', "#4b4f58"), ('H', "#686e78"), ('E', "#241a20"), ('G', "#2b2432")),
            _ => Pal(('R', "#cd4e96"), ('H', "#f2a8d4"), ('E', "#462640"), ('G', "#5c3656")),
        };

        public static string[] HeartRowsFor(float fill)
        {
            float on = Mathf.Clamp01(fill) * 7;
            var r = new string[HeartRows.Length];
            for (int ry = 0; ry < HeartRows.Length; ry++)
            {
                var c = new char[7];
                for (int rx = 0; rx < 7; rx++)
                {
                    if (HeartRows[ry][rx] != '#') { c[rx] = '.'; continue; }
                    bool lit = rx < on;
                    c[rx] = ry == 0 && rx == 1 ? (lit ? 'H' : 'G') : (lit ? 'R' : 'E');
                }
                r[ry] = new string(c);
            }
            return r;
        }

        public static int HeartStep(float fill) => Mathf.RoundToInt(Mathf.Clamp01(fill) * HeartSteps);

        // ---- glows (makeGlow) ----
        public static Img Glow(int r, Color inner, Color outer)
        {
            var g = new Img(r * 2, r * 2);
            g.Radial(r, r, 0, inner, r, outer);
            return g;
        }

        // ---- spell icons (bakeIcon), 34px ----
        static int JsRound(double v) => (int)Math.Floor(v + 0.5);

        static void PixDisc(Img g, int cx, int cy, int r, int px, Color32 c)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r + r * 0.6) g.Fill(cx + dx * px, cy + dy * px, px, px, c);
        }

        /// <summary>0-3 the four guns, "blast", "hook". The colour is the colour of the thing.</summary>
        public static Img Icon(string idx, string color)
        {
            var col = Hex.C(color);
            var g = new Img(34, 34);
            Color c = col;
            g.Draw(Glow(16, c, new Color(c.r, c.g, c.b, 0)), 1, 1, 0.55f);
            const int P = 2;
            switch (idx)
            {
                case "0":
                    g.Fill(2, 15, 4, 2, col); g.Fill(5, 19, 3, 2, col); g.Fill(4, 11, 3, 2, col);
                    PixDisc(g, 19, 17, 5, P, col); PixDisc(g, 19, 17, 3, P, Hex.C("#e9dcff")); PixDisc(g, 19, 17, 1, P, Hex.C("#ffffff"));
                    break;
                case "1":
                    foreach (var (x, y) in new[] { (9, 21), (25, 21), (17, 11), (17, 27) }) { PixDisc(g, x, y, 2, P, col); PixDisc(g, x, y, 1, P, Hex.C("#ffd9c4")); }
                    break;
                case "2":
                    for (int i = 0; i < 6; i++)
                    {
                        double t = i / 5.0;
                        int x = JsRound(6 + 22 * t), y = JsRound(28 - 22 * t);
                        PixDisc(g, x, y, i == 5 ? 2 : 1, P, col);
                        g.Fill(x, y, 2, 2, Hex.C("#dff0ff"));
                    }
                    break;
                case "3":
                    PixDisc(g, 17, 17, 6, P, Hex.C("#6fbf7a")); PixDisc(g, 17, 17, 5, P, Hex.C("#2a6b34")); PixDisc(g, 17, 17, 3, P, Hex.C("#0d1a10"));
                    foreach (var (x, y) in new[] { (9, 7), (24, 10), (24, 25), (8, 23) }) g.Fill(x, y, 2, 2, Hex.C("#a8e6b0"));
                    break;
                case "hook":
                    foreach (var (x, y) in new[] { (7, 7), (27, 9), (9, 26) }) PixDisc(g, x, y, 2, P, col);
                    for (int i = 0; i < 5; i++)
                    {
                        double t = i / 4.0, a = 2.2 - t * 1.5;
                        g.Fill(JsRound(17 + Math.Cos(a) * 13) - 1, JsRound(17 + Math.Sin(a) * 13) - 1, 2, 2, col);
                    }
                    PixDisc(g, 17, 17, 4, P, col); PixDisc(g, 17, 17, 2, P, Hex.C("#cfe6ff"));
                    break;
                default:   // the blast: rays pulled in 2px so the icon sits inside its plate
                    foreach (var (x, y, w, h) in new[] { (16, 4, 2, 4), (16, 28, 2, 4), (4, 16, 4, 2), (26, 16, 4, 2), (8, 8, 2, 2), (24, 8, 2, 2), (8, 24, 2, 2), (24, 24, 2, 2) }) g.Fill(x, y, w, h, col);
                    PixDisc(g, 17, 17, 4, P, col); PixDisc(g, 17, 17, 2, P, Hex.C("#ffd9a0")); PixDisc(g, 17, 17, 1, P, Hex.C("#ffffff"));
                    break;
            }
            return g;
        }

        public const string BlastColor = "#ff8a3d", HookColor = "#2a6fc4";

        // ---- the fake wall's tell: jagged fissures, dark with a lit lip, across a horizontal wall (rotated for a... [h:Sprites-2]
        public static readonly string[] CrackRows =
        {
            "..........:::.....#.....:::.............",
            "........:::::.....#+...:::::............",
            ".......::::::....#+...::::::::..........",
            "......:::::::....#+..:::::#::::.........",
            ".....::::::::...##+.::::::#+::::........",
            "....::::##::::..#+..:::::#+::::::.......",
            "...:::::::##+::#+..:::::#+:::::::::.....",
            "...::::::::#####+:::::##+::::::::::.....",
            "...:::::::::::#+#####+:::::::::::::.....",
            "....::::::::::#+::::::::##:::::::.......",
            ".....:::::::::#+::::::::::##+::::.......",
            "......::::::::##+:::::::::::#+::........",
            ".......::::::::#+::::::::::::#+.........",
            "........::::::::#+:::::::::.............",
            ".........:::::::#+:::::.................",
            "................#.......................",
        };

        public static Img Crack(bool vertical, Color32 dark, Color32 lit, Color32 scorch)
        {
            int w = CrackRows[0].Length, h = CrackRows.Length;
            var g = vertical ? new Img(h, w) : new Img(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = CrackRows[y][x];
                    if (c == '.') continue;
                    int px = vertical ? y : x, py = vertical ? x : y;
                    g.Fill(px, py, 1, 1, c == '#' ? dark : c == '+' ? lit : scorch);
                }
            return g;
        }

        // ---- minimap glyphs ----
        public static readonly Dictionary<string, string[]> MiniGlyph = new Dictionary<string, string[]>
        {
            ["item"] = new[] { "..#..", "..#..", "#####", "..#..", "..#.." },
            ["boss"] = new[] { ".###.", "#####", "#.#.#", "#####", ".#.#." },
            ["lock"] = new[] { ".###.", "#...#", "#####", "#####", "#.#.#", "#####" },
        };

        public static Img Glyph(string[] rows, Color32 c, int px)
        {
            var g = new Img(rows[0].Length * px, rows.Length * px);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == '#') g.Fill(x * px, y * px, px, px, c);
            return g;
        }

        // ---- item tiles (bakeItemIcon); the stamped mark is the pixel font, not a system font ----
        public static Img ItemTile(string id, int px)
        {
            var (color, glyph) = ItemArt.Of(id);
            var g = new Img(px, px);
            int m = Math.Max(1, JsRound(px * 0.09));
            g.Fill(0, 0, px, px, Hex.C("#0b0e14"));
            g.Fill(m, m, px - m * 2, px - m * 2, Hex.C(color));
            g.Fill(m, m, px - m * 2, m, Hex.Rgba(255, 240, 210, 0.30f));
            g.Fill(m, px - m * 2, px - m * 2, m, Hex.Rgba(0, 0, 0, 0.42f));
            g.Fill(0, 0, px, m, Hex.Rgba(0, 0, 0, 0.55f)); g.Fill(0, 0, m, px, Hex.Rgba(0, 0, 0, 0.55f));
            int scale = Math.Max(1, px / 13);
            PixelFont.Stamp(g, glyph, px / 2, px / 2, scale, Hex.Rgba(20, 12, 5, 0.72f));
            return g;
        }
    }

    /// <summary>Each item's tile colour and two-letter mark, from src/25b-items-roster.js (view data
    /// only, so it lives with the art rather than in Depths.Core).</summary>
    public static class ItemArt
    {
        static readonly Dictionary<string, (string color, string glyph)> Table = new Dictionary<string, (string, string)>
        {
            ["heavy_hands"] = ("#b98a55", "HH"), ["weighted_rod"] = ("#8a6238", "WR"), ["tin_cup"] = ("#c9d3e0", "TC"),
            ["bone_whistle"] = ("#d8cfae", "BW"), ["iron_ribs"] = ("#7f8a99", "IR"), ["swift_boots"] = ("#6fbf9a", "SB"),
            ["lucky_coin"] = ("#e0c07a", "LC"), ["steady_hand"] = ("#b8c4d4", "SH"), ["weighted_grip"] = ("#8fa6c4", "WG"),
            ["hunters_mark"] = ("#c79bff", "HM"), ["glass_wands"] = ("#9fe8ff", "GW"), ["brass_compass"] = ("#d6a44a", "BC"),
            ["lantern_friend"] = ("#f0c86a", "LF"),
            ["hush_bell"] = ("#d9b44a", "HB"), ["time_glass"] = ("#9fd8ff", "TG"), ["ricochet_shells"] = ("#b8c4d4", "RS"), ["ember_rounds"] = ("#ff8a4a", "ER"), ["chain_spark"] = ("#c79bff", "CS"), ["glass_heart"] = ("#ff9fbf", "GH"), ["phoenix_feather"] = ("#ff7a3a", "PF"),
        };

        /// <summary>An item the table does not know still gets a tile: the plate's wood and its first two letters.</summary>
        public static (string color, string glyph) Of(string id)
        {
            if (id != null && Table.TryGetValue(id, out var v)) return v;
            string s = (id ?? "?").Replace("_", "").ToUpperInvariant();
            return ("#8a6238", s.Length >= 2 ? s.Substring(0, 2) : s.Length == 1 ? s : "?");
        }

        public static string Color(string id) => Of(id).color;
        public static IEnumerable<string> Ids => Table.Keys;
    }

    /// <summary>A 3x5 pixel font for anything stamped into the art: item marks, numerals, labels
    /// that should read as part of the wood rather than as UI text.</summary>
    public static class PixelFont
    {
        public const int W = 3, H = 5;
        static readonly Dictionary<char, string[]> G = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".#.", "#.#", "###", "#.#", "#.#" }, ['B'] = new[] { "##.", "#.#", "##.", "#.#", "##." },
            ['C'] = new[] { ".##", "#..", "#..", "#..", ".##" }, ['D'] = new[] { "##.", "#.#", "#.#", "#.#", "##." },
            ['E'] = new[] { "###", "#..", "##.", "#..", "###" }, ['F'] = new[] { "###", "#..", "##.", "#..", "#.." },
            ['G'] = new[] { ".##", "#..", "#.#", "#.#", ".##" }, ['H'] = new[] { "#.#", "#.#", "###", "#.#", "#.#" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", "###" }, ['J'] = new[] { "..#", "..#", "..#", "#.#", ".#." },
            ['K'] = new[] { "#.#", "#.#", "##.", "#.#", "#.#" }, ['L'] = new[] { "#..", "#..", "#..", "#..", "###" },
            ['M'] = new[] { "#.#", "###", "###", "#.#", "#.#" }, ['N'] = new[] { "##.", "#.#", "#.#", "#.#", "#.#" },
            ['O'] = new[] { ".#.", "#.#", "#.#", "#.#", ".#." }, ['P'] = new[] { "##.", "#.#", "##.", "#..", "#.." },
            ['Q'] = new[] { ".#.", "#.#", "#.#", "##.", ".##" }, ['R'] = new[] { "##.", "#.#", "##.", "#.#", "#.#" },
            ['S'] = new[] { ".##", "#..", ".#.", "..#", "##." }, ['T'] = new[] { "###", ".#.", ".#.", ".#.", ".#." },
            ['U'] = new[] { "#.#", "#.#", "#.#", "#.#", "###" }, ['V'] = new[] { "#.#", "#.#", "#.#", "#.#", ".#." },
            ['W'] = new[] { "#.#", "#.#", "###", "###", "#.#" }, ['X'] = new[] { "#.#", "#.#", ".#.", "#.#", "#.#" },
            ['Y'] = new[] { "#.#", "#.#", ".#.", ".#.", ".#." }, ['Z'] = new[] { "###", "..#", ".#.", "#..", "###" },
            ['0'] = new[] { "###", "#.#", "#.#", "#.#", "###" }, ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
            ['2'] = new[] { "##.", "..#", ".#.", "#..", "###" }, ['3'] = new[] { "##.", "..#", ".#.", "..#", "##." },
            ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" }, ['5'] = new[] { "###", "#..", "##.", "..#", "##." },
            ['6'] = new[] { ".##", "#..", "###", "#.#", "###" }, ['7'] = new[] { "###", "..#", ".#.", ".#.", ".#." },
            ['8'] = new[] { "###", "#.#", "###", "#.#", "###" }, ['9'] = new[] { "###", "#.#", "###", "..#", "##." },
            ['+'] = new[] { "...", ".#.", "###", ".#.", "..." }, ['-'] = new[] { "...", "...", "###", "...", "..." },
            ['x'] = new[] { "...", "#.#", ".#.", "#.#", "..." }, ['/'] = new[] { "..#", "..#", ".#.", "#..", "#.." },
            [':'] = new[] { "...", ".#.", "...", ".#.", "..." }, ['.'] = new[] { "...", "...", "...", "...", ".#." },
            ['?'] = new[] { "##.", "..#", ".#.", "...", ".#." }, [' '] = new[] { "...", "...", "...", "...", "..." },
        };

        public static bool Has(char c) => G.ContainsKey(char.ToUpperInvariant(c)) || G.ContainsKey(c);
        public static string[] Rows(char c) => G.TryGetValue(c, out var r) ? r : G.TryGetValue(char.ToUpperInvariant(c), out r) ? r : G['?'];

        /// <summary>Width of a run of text at a scale: 3 px a glyph, 1 px between, all times scale.</summary>
        public static int Width(string s, int scale) => s.Length == 0 ? 0 : (s.Length * (W + 1) - 1) * scale;

        /// <summary>Stamps text centred on (cx, cy).</summary>
        public static void Stamp(Img g, string s, int cx, int cy, int scale, Color32 c)
        {
            int x = cx - Width(s, scale) / 2, y = cy - H * scale / 2;
            foreach (char ch in s)
            {
                var rows = Rows(ch);
                for (int ry = 0; ry < H; ry++)
                    for (int rx = 0; rx < W; rx++)
                        if (rows[ry][rx] == '#') g.Fill(x + rx * scale, y + ry * scale, scale, scale, c);
                x += (W + 1) * scale;
            }
        }
    }
}
