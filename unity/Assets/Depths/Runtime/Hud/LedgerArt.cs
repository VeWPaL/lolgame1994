using System;
using UnityEngine;
using Depths.Unity.Art;

namespace Depths.Unity.Hud
{
    /// <summary>
    /// The ledger's bakes, built from the JS materials (woodPlate, drawInset, paperTex): the two
    /// lecterns with their pages ruled and headed, the overlay sheets, the menu's desk and open book,
    /// key caps and knobs. Everything is an <see cref="Img"/>, baked once and turned into a texture by
    /// the caller; nothing here runs per frame.
    /// </summary>
    public static class LedgerArt
    {
        public static readonly Color32 Ink = Hex.C("#2b1d12"), InkSoft = Hex.C("#5a4630"), InkFaint = Hex.Rgba(70, 50, 30, 0.42f),
            Rule = Hex.Rgba(64, 96, 140, 0.30f), Margin = Hex.Rgba(176, 52, 40, 0.50f), RedInk = Hex.C("#a3281f"),
            GoldInk = Hex.C("#a8740e"), SilverInk = Hex.C("#5d6670"), Well = Hex.C("#1a1210"), BrassDark = Hex.C("#6e5020");

        /// <summary>paperTex, aged: a warm tone over it and the edges browned, so a page sits beside a dark room without glaring.</summary>
        static readonly System.Collections.Generic.Dictionary<string, Img> Memo = new System.Collections.Generic.Dictionary<string, Img>();

        /// <summary>A bake that does not depend on the run, made once per session and handed out as a copy
        /// (callers draw on some of them). The menu warms these, so entering a run does not stall on them.</summary>
        public static Img Kept(string key, System.Func<Img> bake)
        {
            if (!Memo.TryGetValue(key, out var img)) Memo[key] = img = bake();
            return img.Clone();
        }

        public static Img AgedPaper(int w, int h, uint seed = 0x9A9E5u, float tone = 0.17f)
        {
            var p = Materials.PaperTex(w, h, seed);
            p.Fill(0, 0, w, h, Hex.Rgba(80, 52, 22, tone));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int d = Math.Min(Math.Min(x, y), Math.Min(w - 1 - x, h - 1 - y));
                    if (d >= 16) continue;
                    float t = 1 - d / 16f;
                    p.Blend(x, y, Hex.Rgba(96, 60, 24, 0.20f * t * t));
                }
            return p;
        }

        /// <summary>Struck type: a light copy one pixel down-right, the ink on top, so it reads as pressed in.</summary>
        public static void Struck(Img g, string s, int cx, int cy, int scale, Color32 ink, Color32? lift = null)
        {
            PixelFont.Stamp(g, s, cx + 1, cy + 1, scale, lift ?? Hex.Rgba(255, 246, 224, 0.45f));
            PixelFont.Stamp(g, s, cx, cy, scale, ink);
        }

        /// <summary>A brass clip across a page's top edge: rim, face, a lit top row and two rivets.</summary>
        public static void Clip(Img g, int cx, int y, int w = 64, int h = 18)
        {
            int x = cx - w / 2;
            g.Fill(x + 2, y + h, w - 2, 3, Hex.Rgba(0, 0, 0, 0.30f));   // its shadow on the page
            g.Fill(x, y, w, h, BrassDark);
            g.Fill(x + 1, y + 1, w - 2, h - 2, Materials.Brass);
            g.Fill(x + 1, y + 1, w - 2, 2, Hex.C("#f6e2a8"));
            g.Fill(x + 1, y + h - 4, w - 2, 3, Hex.C("#b8954c"));
            foreach (int rx in new[] { x + 7, x + w - 10 })
            {
                g.Fill(rx - 1, y + 6, 5, 5, BrassDark);
                g.Fill(rx, y + 7, 3, 3, Hex.C("#fff1c4"));
            }
        }

        /// <summary>A cloth ribbon hanging from (x, y) down h pixels, its end cut in a V.</summary>
        public static void Ribbon(Img g, int x, int y, int w, int h, Color32 c)
        {
            var dark = Hex.Mix(c, Hex.C("#000000"), 0.35f);
            for (int yy = 0; yy < h; yy++)
            {
                int notch = Math.Max(0, yy - (h - w / 2));   // the V eats the last w/2 rows from the middle out
                for (int xx = 0; xx < w; xx++)
                {
                    if (Math.Abs(xx - (w - 1) / 2f) < notch) continue;
                    g.Blend(x + xx, y + yy, xx == 0 || xx == w - 1 ? dark : xx == 1 ? Hex.Mix(c, Hex.C("#ffffff"), 0.25f) : c);
                }
            }
        }

        /// <summary>A small wood plate with a dark well in it: a slot, the depth seal, a key cap.</summary>
        public static Img WellPlate(int w, int h, int frame = 6, bool rivets = true)
        {
            var g = Materials.WoodPlate(w, h, rivets);
            Materials.Inset(g, frame, frame, w - frame * 2, h - frame * 2, Well);
            return g;
        }

        // ---- the boards ----

        public static readonly Color32 Lift = Hex.Rgba(255, 246, 224, 0.55f);

        /// <summary>A dark well cut into wood or paper: dark top-left, a 1px lit bevel bottom-right, a soft inner shadow.</summary>
        public static void CutWell(Img g, int x, int y, int w, int h, Color32? fill = null)
        {
            g.Fill(x, y, w, h, fill ?? Hex.C("#140c06"));
            g.Fill(x, y, w, 2, Materials.InsetDark); g.Fill(x, y, 2, h, Materials.InsetDark);
            g.Fill(x + 2, y + 2, w - 4, 2, Hex.Rgba(0, 0, 0, 0.35f));
            g.Fill(x, y + h - 1, w, 1, Hex.C("#c8965a")); g.Fill(x + w - 1, y, 1, h, Hex.C("#c8965a"));   // the lit bevel
            g.Fill(x, y + h, w + 1, 1, Hex.Rgba(255, 246, 224, 0.5f));
        }

        /// <summary>A label in the ledger's hand: struck into the paper in soft ink.</summary>
        public static void Label(Img g, string text, int x, int y, Color32? ink = null)
        {
            LedgerType.Stamp(g, text, x + 1, y + 1, HudLayout.TextScale, Lift);
            LedgerType.Stamp(g, text, x, y, HudLayout.TextScale, ink ?? InkSoft);
        }

        static void RuleAt(Img g, int x, int y, int w) { g.Fill(x, y, w, 1, InkFaint); g.Fill(x, y + 3, w, 2, InkFaint); }

        static void Header(Img g, string text, int cx, int y)
        {
            LedgerType.Struck(g, text, cx, y, HudLayout.TextScale, Ink, Lift);
            int w = LedgerType.Width(text, HudLayout.TextScale) + 48;
            RuleAt(g, cx - w / 2, y + 20, w);
        }

        /// <summary>
        /// One board: a plank as tall as the screen allows, its inset and aged page, a clip at the top,
        /// and the page's fixed parts (headers, labels, rules, wells, the seal, the slots and their name
        /// plates). Everything that changes is drawn over it by the ink layer. Origin: HudLayout.Board.
        /// </summary>
        public static Img Board(bool left)
        {
            var at = HudLayout.Board(left);
            int W = (int)at.width, H = (int)at.height, ox = -(int)at.x, oy = -(int)at.y, pad = HudLayout.PaperPad;
            var g = Materials.WoodPlate(W, H);
            Materials.Inset(g, pad - 2, pad - 2, W - (pad - 2) * 2, H - (pad - 2) * 2, null);
            g.Draw(AgedPaper(W - pad * 2, H - pad * 2, left ? 0x1ED6Eu : 0x3A9u), pad, pad);
            int x0 = HudLayout.Ink0(left) + ox, cx = W / 2;
            if (left) Body(g, ox, oy, x0, cx); else MapAndKit(g, ox, oy, x0, cx);
            Clip(g, cx, 1);
            return g;
        }

        public static readonly string[] BandNames = { "SHALLOWS", "KILN WORKS", "FLOODED GALLERIES", "WARDEN'S DEEP" };
        static readonly string[] Blink = { "#...#...", ".#...#..", "..#...#.", ".#...#..", "#...#..." };

        /// <summary>A pictogram in ink: the row's mark in the margin, in place of a written label.</summary>
        static void Picto(Img g, string[] rows, int x, int y, int px, Color32 c)
        {
            // any lit cell of a palette sprite (the key's 'g') is ink in a pictogram
            var ink = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var ch = rows[i].ToCharArray();
                for (int j = 0; j < ch.Length; j++) if (ch[j] != '.') ch[j] = '#';
                ink[i] = new string(ch);
            }
            rows = ink;
            g.Draw(Sprites.Glyph(rows, Lift, px), x + 1, y + 1);
            g.Draw(Sprites.Glyph(rows, c, px), x, y);
        }

        static void Body(Img g, int ox, int oy, int x0, int cx)
        {
            int w = HudLayout.InkW;
            Header(g, "THE BODY", cx, HudLayout.HeaderY + oy);
            var m = HudLayout.MomentumWell; CutWell(g, (int)m.x + ox, (int)m.y + oy, (int)m.width, (int)m.height);
            // keys: a key in the margin, then the two wells; blink: a dash mark, then its two pips
            Picto(g, Sprites.KeyRows, HudLayout.IconX + ox - 2, HudLayout.KeysY + 11 + oy, 2, InkSoft);
            for (int i = 0; i < 2; i++) { var k = HudLayout.KeyWell(i); CutWell(g, (int)k.x + ox, (int)k.y + oy, (int)k.width, (int)k.height); }
            Picto(g, Blink, HudLayout.IconX + ox, HudLayout.BlinkY + 9 + oy, 2, InkSoft);
            for (int i = 0; i < 2; i++) { var b = HudLayout.BlinkWell(i); CutWell(g, (int)b.x + ox, (int)b.y + oy, (int)b.width, (int)b.height); }
            RuleAt(g, x0, HudLayout.Rule2 + oy, w);
            var s = HudLayout.DepthSeal;
            g.Fill((int)s.x + ox + 2, (int)s.yMax + oy, (int)s.width - 2, 3, Hex.Rgba(40, 24, 10, 0.35f));
            g.Draw(WellPlate((int)s.width, (int)s.height, 7, true), (int)s.x + ox, (int)s.y + oy);
            // the descent: four bands of the climb, each a row of floors to strike off
            for (int band = 0; band < 4; band++)
            {
                Label(g, BandNames[band], x0, HudLayout.BandLabelY(band) + oy, InkSoft);
                int rungs = band == 3 ? 1 : 4;
                for (int i = 0; i < rungs; i++)
                {
                    var r = HudLayout.Rung(band, i);
                    int rx = (int)r.x + ox, ry = (int)r.y + oy, rw = (int)r.width, rh = (int)r.height;
                    g.Fill(rx + 1, ry + rh, rw - 1, 2, Hex.Rgba(40, 24, 10, 0.25f));
                    g.Fill(rx, ry, rw, rh, Hex.Rgba(255, 250, 235, 0.35f));
                    g.Fill(rx, ry, rw, 1, InkSoft); g.Fill(rx, ry + rh - 1, rw, 1, InkSoft); g.Fill(rx, ry, 1, rh, InkSoft); g.Fill(rx + rw - 1, ry, 1, rh, InkSoft);
                    string n = band == 3 ? "13 AND DOWN" : (band * 4 + i + 1).ToString();
                    LedgerType.Stamp(g, n, rx + rw / 2 - LedgerType.Width(n, 2) / 2, ry + 5, 2, InkFaint);
                }
            }
            RuleAt(g, x0, HudLayout.Rule3 + oy, w);
            Picto(g, Sprites.MiniGlyph["boss"], HudLayout.IconX + ox, HudLayout.SlainY + 2 + oy, 3, InkSoft);
        }

        static void MapAndKit(Img g, int ox, int oy, int x0, int cx)
        {
            Header(g, "THE MAP", cx, HudLayout.HeaderY + oy);
            Header(g, "THE KIT", cx, HudLayout.KitHeaderY + oy);
            for (int i = 0; i < 3; i++)
            {
                var s = HudLayout.Slot(i);
                g.Fill((int)s.x + ox + 3, (int)s.yMax + oy, (int)s.width - 3, 3, Hex.Rgba(40, 24, 10, 0.35f));
                g.Draw(WellPlate((int)s.width, (int)s.height), (int)s.x + ox, (int)s.y + oy);
                var p = HudLayout.Plate(i);
                g.Fill((int)p.x + ox + 3, (int)p.yMax + oy, (int)p.width - 3, 3, Hex.Rgba(40, 24, 10, 0.30f));
                g.Draw(NamePlate((int)p.width, (int)p.height), (int)p.x + ox, (int)p.y + oy);
            }
            Label(g, "CARRYING", x0, HudLayout.CarryLabelY + oy);
        }

        /// <summary>
        /// The seed tag (Materials.SeedTag's luggage tag: the clipped corner, the punched hole), with the seed
        /// inlaid in pale wood in the HUD's hand so it reads at a glance; the tag is the label, so no word on it.
        /// </summary>
        public static Img SeedTag(int w, int h, string text)
        {
            const int CH = 18;
            var c = new Img(w, h);
            bool Inside(int x, int y) => x >= 1 && x < w - 1 && y >= 1 && y < h - 1 && x + y >= CH + 1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Inside(x, y)) continue;
                    c.Blend(x, y, Materials.WoodTex.Get(x % 96, y % 96));
                    bool rim = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1);
                    bool rim2 = !Inside(x - 2, y) || !Inside(x, y - 2);
                    if (rim) c.Blend(x, y, Hex.Rgba(26, 14, 6, 0.85f));
                    else if (rim2) c.Blend(x, y, Hex.Rgba(255, 228, 186, 0.26f));
                }
            for (int i = 0; i < CH - 1; i++) c.Blend(1 + i, CH - i, Hex.Rgba(255, 214, 160, 0.32f));
            float hx = CH * 0.72f, hy = h / 2f;
            c.Disc(hx, hy, 5.5f, Hex.C("#150c05"));
            // the inlay: a dark channel cut first, the pale seed set into it
            string s = text ?? "";
            int scale = 3, tw = LedgerType.Width(s, scale), tx = (w + CH) / 2 - tw / 2, ty = h / 2 - LedgerType.H * scale / 2;
            LedgerType.Stamp(c, s, tx + 1, ty + 1, scale, Hex.Rgba(20, 10, 4, 0.9f));
            LedgerType.Stamp(c, s, tx, ty, scale, Hex.C("#f3dfb2"));
            return c;
        }

        /// <summary>A name plate: a strip of the plank with a paper slip let into it, where a name is written.</summary>
        public static Img NamePlate(int w, int h)
        {
            var g = Materials.WoodPlate(w, h, false);
            Materials.Inset(g, 4, 4, w - 8, h - 8, null);
            g.Draw(AgedPaper(w - 12, h - 12, 0x9E7u, 0.06f), 6, 6);
            return g;
        }

        /// <summary>A small wooden key cap with its letter struck in cream: the Q beside the held item.</summary>
        public static Img Cap(string letter, int s)
        {
            var g = KeyCap(s, s, false);
            LedgerType.Struck(g, letter, s / 2, (s - 4) / 2 - 7, 2, Hex.C("#f0e2c4"), Hex.Rgba(0, 0, 0, 0.6f));
            return g;
        }

        /// <summary>A paper tag pinned in the empty hand: "NOTHING HELD" in ink, a brass pin, a shadow.</summary>
        public static Img NothingTag(int w, int h)
        {
            var g = new Img(w + 3, h + 4);
            g.Fill(3, 4, w, h, Hex.Rgba(0, 0, 0, 0.40f));
            g.Draw(AgedPaper(w, h, 0x7A6u, 0.04f), 0, 0);
            LedgerType.Stamp(g, "NOTHING", w / 2 - LedgerType.Width(7, 1) / 2, h / 2 - 9, 1, Ink);
            LedgerType.Stamp(g, "HELD", w / 2 - LedgerType.Width(4, 1) / 2, h / 2 + 2, 1, Ink);
            g.Disc(w / 2f, 4, 3, BrassDark); g.Disc(w / 2f - 0.5f, 3.5f, 2, Materials.Brass);
            return g;
        }

        /// <summary>A heart at the HUD's size, filled by exact halves: the split runs down the middle of
        /// the centre column, so half a heart is half the pixels (1 HP is half a heart).</summary>
        public static Img Heart(Sprites.HeartScheme scheme, int fill, int px)
        {
            var pal = Sprites.HeartPal(scheme);
            // an empty regen container keeps its own dark rose, so it never reads as an empty red one
            if (scheme == Sprites.HeartScheme.Regen) { pal['E'] = Hex.C("#6b3459"); pal['G'] = Hex.C("#86476f"); }
            var lit = Sprites.Bake(Sprites.HeartRowsFor(1), pal, px);
            if (fill == 2) return lit;
            var dark = Sprites.Bake(Sprites.HeartRowsFor(0), pal, px);
            if (fill == 0) return dark;
            int split = 2 + (7 * px) / 2;   // pad + half the heart's width
            for (int y = 0; y < lit.H; y++)
                for (int x = split; x < lit.W; x++) lit.Px[y * lit.W + x] = dark.Px[y * lit.W + x];
            return lit;
        }

        // ---- the overlays ----

        /// <summary>A document: paper on wood, the JS run-summary sheet.</summary>
        public static Img Sheet(int w, int h) => PaperOnWoodAged(w, h, 10);

        public static Img PaperOnWoodAged(int w, int h, int pad)
        {
            var c = Materials.WoodPlate(w, h);
            Materials.Inset(c, pad - 2, pad - 2, w - (pad - 2) * 2, h - (pad - 2) * 2, null);
            c.Draw(AgedPaper(w - pad * 2, h - pad * 2, 0x5EE7u, 0.10f), pad, pad);
            return c;
        }

        /// <summary>The Warden's bar: a long plate, its dark well cut where the fill runs.</summary>
        public static Img BossPlate()
        {
            var p = HudLayout.BossPlate; var w = HudLayout.BossWell;
            var g = Materials.WoodPlate((int)p.width, (int)p.height);
            Materials.Inset(g, (int)(w.x - p.x) - 2, (int)(w.y - p.y) - 2, (int)w.width + 4, (int)w.height + 4, Hex.C("#0a0806"));
            return g;
        }

        /// <summary>A notice pinned up: aged paper, a red ink frame, two brass pins.</summary>
        public static Img Notice(int w, int h)
        {
            var g = new Img(w, h);
            g.Fill(3, 4, w - 3, h - 4, Hex.Rgba(0, 0, 0, 0.45f));
            g.Draw(AgedPaper(w - 4, h - 4, 0x77u, 0.12f), 0, 0);
            var red = Hex.Rgba(163, 40, 31, 0.85f);
            g.Fill(6, 6, w - 16, 2, red); g.Fill(6, h - 14, w - 16, 2, red); g.Fill(6, 6, 2, h - 18, red); g.Fill(w - 12, 6, 2, h - 18, red);
            foreach (int x in new[] { 14, w - 20 }) { g.Disc(x, 12, 4, BrassDark); g.Disc(x - 0.5f, 11.5f, 3, Materials.Brass); g.Fill(x - 2, 10, 2, 1, Hex.C("#fff1c4")); }
            return g;
        }

        // ---- the menu: a desk, an open ledger, its fittings ----

        /// <summary>The desk: the plank pattern, darkened, lamp-lit in the middle and black at the edges.</summary>
        public static Img Desk(int w, int h)
        {
            var g = new Img(w, h);
            // planks run across: the 96px grain tile turned on its side reads as boards
            var tile = Materials.WoodTex;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) g.Px[y * w + x] = tile.Get(y % 96, x % 96);
            for (int y = 0; y < h; y += 96) g.Fill(0, y, w, 2, Materials.WoodDark);
            g.Fill(0, 0, w, h, Hex.Rgba(14, 8, 4, 0.52f));
            g.Radial(w / 2f, h / 2f, Math.Min(w, h) * 0.30f, new Color(0, 0, 0, 0), Math.Max(w, h) * 0.66f, new Color(0, 0, 0, 0.82f));
            return g;
        }

        /// <summary>
        /// An open ledger seen from above: wooden boards with brass corners, two pages with their stacked
        /// edges, the gutter's shadow and a red margin on each page. The left page is <paramref name="w"/>/2.
        /// </summary>
        public static Img Book(int w, int h)
        {
            var g = new Img(w, h + 26);
            g.Fill(10, 14, w - 6, h - 4, Hex.Rgba(0, 0, 0, 0.55f));   // its shadow on the desk
            g.Draw(Materials.WoodPlate(w, h), 0, 0);
            foreach (var (cx, cy, sx, sy) in new[] { (0, 0, 1, 1), (w - 1, 0, -1, 1), (0, h - 1, 1, -1), (w - 1, h - 1, -1, -1) })
                for (int i = 0; i < 26; i++)
                    for (int j = 0; j < 26 - i; j++)
                        g.Blend(cx + sx * i, cy + sy * j, i + j >= 24 ? BrassDark : i + j < 3 ? Hex.C("#fff1c4") : Materials.Brass);
            int m = 22, gut = w / 2;
            // the page blocks: three edge lines on the outer sides and the foot read as a thick book
            for (int k = 3; k >= 1; k--)
            {
                var edge = Hex.Mix(Materials.Paper, Hex.C("#8a6a40"), 0.25f + 0.12f * k);
                g.Fill(m - k * 2, m - 2 + k, gut - m + k * 2, h - m * 2 + k * 2, edge);
                g.Fill(gut, m - 2 + k, gut - m + k * 2, h - m * 2 + k * 2, edge);
            }
            var left = AgedPaper(gut - m, h - m * 2, 0xB00Cu, 0.10f);
            var right = AgedPaper(gut - m, h - m * 2, 0xB00Du, 0.10f);
            g.Draw(left, m, m); g.Draw(right, gut, m);
            // the gutter: the pages curve down into the spine
            for (int x = -46; x < 46; x++)
            {
                float t = 1 - Math.Abs(x + 0.5f) / 46f;
                g.Fill(gut + x, m, 1, h - m * 2, Hex.Rgba(70, 40, 16, 0.42f * t * t * t));
            }
            g.Fill(gut - 1, m, 2, h - m * 2, Hex.Rgba(40, 22, 8, 0.55f));
            // red margins and a header rule on each page
            foreach (int px in new[] { m, gut })
            {
                g.Fill(px + 54, m + 12, 1, h - m * 2 - 24, Margin);
                g.Fill(px + 57, m + 12, 1, h - m * 2 - 24, Margin);
                g.Fill(px + 24, m + 96, gut - m - 48, 1, InkFaint);
                g.Fill(px + 24, m + 99, gut - m - 48, 2, InkFaint);
            }
            // two ribbons from the head of the spine, out past the foot
            Ribbon(g, gut + 14, m - 4, 10, h - m + 22, Hex.C("#8c2a2a"));
            Ribbon(g, gut + 28, m - 4, 8, h - m + 10, Hex.C("#2a5a7a"));
            return g;
        }

        /// <summary>A key cap: a little plank with a lit top and a thick dark foot, as if it stands up.</summary>
        public static Img KeyCap(int w, int h, bool lit)
        {
            var g = new Img(w, h);
            g.Fill(0, 2, w, h - 2, Materials.WoodDark);
            var face = Materials.WoodPlate(w, h - 4, false);
            g.Draw(face, 0, 0);
            if (lit) { g.Fill(2, 2, w - 4, h - 8, Hex.Rgba(255, 210, 120, 0.22f)); g.Fill(0, 0, w, 1, Materials.Brass); }
            return g;
        }

        /// <summary>A brass slider knob: a rounded block, lit on top, shaded below.</summary>
        public static Img Knob(int w, int h)
        {
            var g = new Img(w, h);
            g.Fill(1, 0, w - 2, h, BrassDark); g.Fill(0, 1, w, h - 2, BrassDark);
            g.Fill(2, 2, w - 4, h - 4, Materials.Brass);
            g.Fill(2, 2, w - 4, 2, Hex.C("#fff1c4"));
            g.Fill(2, h - 5, w - 4, 3, Hex.C("#b8954c"));
            for (int i = 0; i < 3; i++) g.Fill(w / 2 - 3, 7 + i * 3, 6, 1, BrassDark);   // a grip
            return g;
        }

        /// <summary>A ledger box to tick: an inked square, and the tick when it is set.</summary>
        public static Img TickBox(int s, bool ticked)
        {
            var g = new Img(s, s);
            // a little wooden frame with a paper square let into it; the tick is inked in red across it
            g.Draw(Materials.WoodPlate(s, s, false), 0, 0);
            Materials.Inset(g, 4, 4, s - 8, s - 8, null);
            g.Draw(AgedPaper(s - 12, s - 12, 0x71Cu, 0.04f), 6, 6);
            if (ticked)
                for (int i = 0; i <= 16; i++)
                {
                    // a pen tick: down-right for a third, then a long stroke up-right past the frame
                    float t = i / 16f;
                    float x = 6 + t * (s - 8), y = t < 0.33f ? s * 0.5f + t * s * 0.6f : s * 0.70f - (t - 0.33f) * s * 0.95f;
                    g.Fill(Mathf.RoundToInt(x) - 1, Mathf.RoundToInt(y) - 1, 3, 3, RedInk);
                }
            return g;
        }

        /// <summary>A field cut for writing in: a wooden frame around a paper slip (9-sliced at 6 by the menu).</summary>
        public static Img FieldFrame(int w, int h)
        {
            var g = Materials.WoodPlate(w, h, false);
            Materials.Inset(g, 3, 3, w - 6, h - 6, null);
            g.Draw(AgedPaper(w - 10, h - 10, 0xF1Eu, 0.02f), 5, 5);
            return g;
        }

        /// <summary>A brass arrowhead pointing down, lit along its top: the dropdown's opener.</summary>
        public static Img BrassArrow(int w, int h)
        {
            var g = new Img(w, h);
            for (int y = 0; y < h; y++)
            {
                int half = (w / 2) * (h - y) / h;
                for (int x = w / 2 - half; x <= w / 2 + half && x < w; x++)
                {
                    bool edge = x == w / 2 - half || x == w / 2 + half || y == 0;
                    g.Blend(x, y, edge ? BrassDark : y < 2 ? Hex.C("#fff1c4") : Materials.Brass);
                }
            }
            return g;
        }

        /// <summary>A groove cut in a plank for a slider to ride in: wood rims, a dark channel (9-sliced at 5).</summary>
        public static Img Groove(int w, int h)
        {
            var g = Materials.WoodPlate(w, h, false);
            CutWell(g, 3, 3, w - 6, h - 6, Hex.C("#1a1008"));
            return g;
        }

        /// <summary>A word struck in the pixel font, on a clear ground, for titles laid on paper.</summary>
        public static Img Stamp(string text, int scale, Color32 ink)
        {
            int w = PixelFont.Width(text, scale) + 4, h = PixelFont.H * scale + 4;
            var g = new Img(w, h);
            Struck(g, text, w / 2 - 1, h / 2 - 1, scale, ink, Hex.Rgba(255, 246, 224, 0.55f));
            return g;
        }

        /// <summary>The cast list for the left page of the menu: the player drawn large, with his shadow.</summary>
        public static Img Portrait(int px)
        {
            var body = Sprites.Bake(Sprites.Frame(Sprites.PlayerUpper, 0), Sprites.PlayerPal, px);
            var g = new Img(body.W + 20, body.H + 14);
            g.Disc(g.W / 2f, body.H + 2, body.W * 0.36f, Hex.Rgba(40, 24, 10, 0.25f));
            g.Draw(body, 10, 0);
            return g;
        }
    }
}
