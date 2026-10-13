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

        // ---- the lecterns ----

        /// <summary>
        /// One stand: the ledger plate (wood, inset, aged paper) as tall as the room, a ledge it rests on,
        /// a post down to the floor, a clip at the top and the page ruled for its side. Positions are the
        /// HudLayout ones, shifted into the image (whose origin is <see cref="HudLayout.Stand"/>).
        /// </summary>
        public static Img Stand(bool left)
        {
            var at = HudLayout.Stand(left);
            int W = (int)at.width, H = (int)at.height;
            int ox = -(int)at.x, oy = -(int)at.y;   // panel to image
            var g = new Img(W, H);
            int px = (left ? HudLayout.LeftX : HudLayout.RightX) + ox, py = HudLayout.LecternTop + oy;
            int pw = HudLayout.LecternW, ph = HudLayout.LecternH, pad = HudLayout.PaperPad;

            // the post first, behind the ledge, darker for being further back
            int postX = W / 2 - HudLayout.PostW / 2, postY = py + ph + HudLayout.LedgeH - 4;
            g.Pattern(Materials.WoodTex, postX, postY, HudLayout.PostW, H - postY);
            g.Fill(postX, postY, HudLayout.PostW, H - postY, Hex.Rgba(10, 5, 2, 0.38f));
            g.Fill(postX, postY, 2, H - postY, Materials.WoodDark); g.Fill(postX + HudLayout.PostW - 2, postY, 2, H - postY, Materials.WoodDark);
            g.Fill(postX + 2, postY, 2, H - postY, Hex.Rgba(185, 138, 85, 0.45f));
            g.Fill(postX, postY, HudLayout.PostW, 6, Hex.Rgba(0, 0, 0, 0.45f));   // in the ledge's shadow
            for (int i = 0; i < 3; i++) g.Fill(postX + 8, postY + 30 + i * 26, HudLayout.PostW - 16, 2, Hex.Rgba(36, 20, 10, 0.55f));   // turned rings

            // the plate and its page
            g.Draw(Materials.WoodPlate(pw, ph), px, py);
            Materials.Inset(g, px + pad - 2, py + pad - 2, pw - (pad - 2) * 2, ph - (pad - 2) * 2, null);
            g.Draw(AgedPaper(pw - pad * 2, ph - pad * 2, left ? 0x1ED6Eu : 0x3A9u), px + pad, py + pad);

            // the ledge the book rests on, proud of the plate on both sides
            var ledge = Materials.WoodPlate(W, HudLayout.LedgeH, false);
            g.Draw(ledge, 0, py + ph - 4);
            g.Fill(2, py + ph - 4 + HudLayout.LedgeH, W - 4, 3, Hex.Rgba(0, 0, 0, 0.40f));

            if (left) RuleBody(g, ox, oy); else RuleMap(g, ox, oy);
            Clip(g, px + pw / 2, py - 6);
            // a bookmark ribbon out of the bottom of the page, over the ledge
            Ribbon(g, px + pw - 46, py + ph - pad - 6, 9, 46, left ? Hex.C("#8c2a2a") : Hex.C("#2a5a7a"));
            return g;
        }

        static void Header(Img g, string text, int cx, int y)
        {
            Struck(g, text, cx, y + 7, 3, Ink);
            int w = PixelFont.Width(text, 3) + 40;
            g.Fill(cx - w / 2, y + 20, w, 1, InkFaint);
            g.Fill(cx - w / 2, y + 23, w, 2, InkFaint);
        }

        static void RuleBody(Img g, int ox, int oy)
        {
            int x0 = HudLayout.LeftX + HudLayout.PaperPad + ox, x1 = HudLayout.LeftX + HudLayout.LecternW - HudLayout.PaperPad + ox;
            Header(g, "THE BODY", (x0 + x1) / 2, HudLayout.HeaderY + oy);
            int top = HudLayout.BloodY - 6 + oy, bottom = HudLayout.LecternTop + HudLayout.LecternH - HudLayout.PaperPad - 4 + oy;
            g.Fill(HudLayout.MarginX + ox, top, 1, bottom - top, Margin);   // the red double margin
            g.Fill(HudLayout.MarginX + 3 + ox, top, 1, bottom - top, Margin);
            for (int i = 0; i < HudLayout.LeftRows.Length; i++)
            {
                g.Fill(x0 + 4, HudLayout.RowBottom(i) + oy, x1 - x0 - 8, 1, Rule);
                if (i == 0) g.Fill(HudLayout.ValueX + ox, HudLayout.BloodY + HudLayout.BloodLineStep - 1 + oy, x1 - HudLayout.ValueX - ox - 4, 1, Hex.Rgba(64, 96, 140, 0.14f));
            }
            // the depth seal: a block of the plank laid on the page, the floor struck into it each frame
            var s = HudLayout.DepthSeal;
            g.Fill((int)s.x + ox + 2, (int)s.yMax + oy, (int)s.width - 2, 3, Hex.Rgba(40, 24, 10, 0.35f));
            g.Draw(WellPlate((int)s.width, (int)s.height, 6, true), (int)s.x + ox, (int)s.y + oy);
        }

        static void RuleMap(Img g, int ox, int oy)
        {
            int x0 = HudLayout.RightX + HudLayout.PaperPad + ox, x1 = HudLayout.RightX + HudLayout.LecternW - HudLayout.PaperPad + ox;
            Header(g, "THE MAP", (x0 + x1) / 2, HudLayout.HeaderY + oy);
            // the chart's grid: faint pencil crosses where the cells meet
            var m = HudLayout.Map;
            for (int i = 0; i <= 7; i++)
                for (int j = 0; j <= 7; j++)
                {
                    int cx = (int)m.x + ox + i * HudLayout.MapCell - 2, cy = (int)m.y + oy + j * HudLayout.MapCell - 2;
                    g.Fill(cx - 2, cy, 5, 1, Hex.Rgba(64, 96, 140, 0.30f)); g.Fill(cx, cy - 2, 1, 5, Hex.Rgba(64, 96, 140, 0.30f));
                }
            Header(g, "THE KIT", (x0 + x1) / 2, HudLayout.KitHeaderY + oy);
            for (int i = 0; i < 3; i++)
            {
                var s = HudLayout.Slot(i);
                g.Fill((int)s.x + ox + 3, (int)s.yMax + oy, (int)s.width - 3, 3, Hex.Rgba(40, 24, 10, 0.35f));   // resting on the page
                g.Draw(WellPlate((int)s.width, (int)s.height), (int)s.x + ox, (int)s.y + oy);
            }
            g.Fill(x0 + 4, HudLayout.CarryY - 8 + oy, x1 - x0 - 8, 1, Rule);
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
            g.Fill(0, 0, s, s, Hex.Rgba(255, 250, 235, 0.35f));
            g.Fill(0, 0, s, 2, Ink); g.Fill(0, s - 2, s, 2, Ink); g.Fill(0, 0, 2, s, Ink); g.Fill(s - 2, 0, 2, s, Ink);
            if (ticked)
                for (int i = 0; i < s; i++)
                {
                    // a quick pen tick: down-right for a third, then up-right past the box
                    int x = 3 + i * (s - 3) / s, y = i < s / 3 ? s / 2 + i : s / 2 + s / 3 - (i - s / 3) * 3 / 2;
                    g.Fill(x, y - 1, 3, 3, RedInk);
                }
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
