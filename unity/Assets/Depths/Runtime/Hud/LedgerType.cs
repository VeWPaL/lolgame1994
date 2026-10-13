using System.Collections.Generic;
using UnityEngine;
using Depths.Unity.Art;

namespace Depths.Unity.Hud
{
    /// <summary>
    /// The HUD's one hand: a 5x7 pixel face, so every word on the lecterns is drawn in the same pixels
    /// as the room beside it (the 3x5 PixelFont stays for item marks). Lower case is drawn as upper case.
    /// Text is laid out without building strings: a word wrap walks the source string in place.
    /// </summary>
    public static class LedgerType
    {
        public const int W = 5, H = 7, Advance = 6, LineGap = 3;

        static readonly Dictionary<char, string[]> G = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
            [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." },
            [','] = new[] { ".....", ".....", ".....", ".....", ".##..", "..#..", ".#..." },
            ['\''] = new[] { "..#..", "..#..", ".#...", ".....", ".....", ".....", "....." },
            [':'] = new[] { ".....", ".##..", ".##..", ".....", ".##..", ".##..", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            ['x'] = new[] { ".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#" },
            ['('] = new[] { "...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#." },
            [')'] = new[] { ".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..." },
            ['%'] = new[] { "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##" },
            ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            ['<'] = new[] { "...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#." },
        };

        public static string[] Rows(char c)
        {
            if (G.TryGetValue(c, out var r)) return r;
            if (G.TryGetValue(char.ToUpperInvariant(c), out r)) return r;
            return G['?'];
        }

        public static bool Has(char c) => G.ContainsKey(c) || G.ContainsKey(char.ToUpperInvariant(c));

        /// <summary>Width of n glyphs at a scale: 5 px each, 1 px between.</summary>
        public static int Width(int n, int scale) => n <= 0 ? 0 : (n * Advance - 1) * scale;
        public static int Width(string s, int scale) => Width(s?.Length ?? 0, scale);

        /// <summary>Stamps text with its top-left at (x, y) into an image.</summary>
        public static void Stamp(Img g, string s, int x, int y, int scale, Color32 c)
        {
            for (int i = 0; i < s.Length; i++)
            {
                var rows = Rows(s[i]);
                for (int ry = 0; ry < H; ry++)
                    for (int rx = 0; rx < W; rx++)
                        if (rows[ry][rx] == '#') g.Fill(x + (i * Advance + rx) * scale, y + ry * scale, scale, scale, c);
            }
        }

        /// <summary>Struck: a light copy down-right, then the ink, centred on cx.</summary>
        public static void Struck(Img g, string s, int cx, int y, int scale, Color32 ink, Color32 lift)
        {
            int x = cx - Width(s, scale) / 2;
            Stamp(g, s, x + 1, y + 1, scale, lift);
            Stamp(g, s, x, y, scale, ink);
        }

        /// <summary>
        /// Greedy word wrap of s[from..] into maxW pixels: the end (exclusive) of the first line and the
        /// start of the next. No strings are made, so the ink layer can wrap every frame for free.
        /// </summary>
        public static (int end, int next) Wrap(string s, int from, int maxW, int scale)
        {
            int maxChars = Mathf.Max(1, (maxW / scale + 1) / Advance);
            if (s.Length - from <= maxChars) return (s.Length, s.Length);
            int cut = -1;
            for (int i = from; i <= from + maxChars && i < s.Length; i++) if (s[i] == ' ') cut = i;
            if (cut <= from) return (from + maxChars, from + maxChars);
            return (cut, cut + 1);
        }
    }
}
