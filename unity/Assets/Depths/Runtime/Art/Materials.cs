using System;
using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity.Art
{
    /// <summary>One area's colours (AREA_PAL in src/10-art.js).</summary>
    public sealed class AreaPalette
    {
        public string Stone, StoneLit, Mortar, Floor, FloorTint, Accent, AccentDim, MapWash;
        public Color32 FloorLight;   // the fleck colour, alpha set per fleck
        public float Wash;

        public static AreaPalette For(Area a) => a switch
        {
            Area.Area1 => new AreaPalette { Stone = "#2b2f3a", StoneLit = "#454c5c", Mortar = "#1d2028", Floor = "#25211c", FloorLight = new Color32(255, 240, 210, 255), FloorTint = "#25211c", Wash = 0, Accent = "#e8b06a", AccentDim = "#c8a878", MapWash = "#090b11" },
            Area.Area2 => new AreaPalette { Stone = "#3d2a20", StoneLit = "#63432e", Mortar = "#261811", Floor = "#30231a", FloorLight = new Color32(255, 224, 168, 255), FloorTint = "#5a3a12", Wash = 0.22f, Accent = "#e08a4a", AccentDim = "#b87545", MapWash = "#100a07" },
            Area.Area3 => new AreaPalette { Stone = "#16382e", StoneLit = "#3c5b52", Mortar = "#14241f", Floor = "#1b2a25", FloorLight = new Color32(190, 240, 214, 255), FloorTint = "#1e3c34", Wash = 0.20f, Accent = "#7fd4b0", AccentDim = "#63a68c", MapWash = "#070f10" },
            _ => new AreaPalette { Stone = "#2e0f1c", StoneLit = "#5e2c34", Mortar = "#210e13", Floor = "#26161a", FloorLight = new Color32(255, 196, 208, 255), FloorTint = "#3e0d20", Wash = 0.26f, Accent = "#ff9a8a", AccentDim = "#c97668", MapWash = "#140609" },
        };

        /// <summary>ROOM_BG: the type tint under the area wash. A secret room reads as a normal one.</summary>
        public static string RoomBg(RoomKind k) => k == RoomKind.Start ? "#1c2230" : k == RoomKind.Item ? "#2a2410" : k == RoomKind.Boss ? "#2a1414" : "#191b22";

        /// <summary>roomTone: the composited air of a room, used to fill a doorway (areaFloorTint).</summary>
        public Color32 RoomTone(RoomKind k) => Hex.Mix(Hex.C(RoomBg(k)), Hex.C(FloorTint), Wash);
    }

    /// <summary>
    /// The carved-wood and paper materials and the room's stone (src/10-art.js, src/70-view.js), as
    /// bakes. Noise comes from an art stream the view owns, never the run's: a draw must not move the
    /// simulation (CONVENTIONS, one-way data flow).
    /// </summary>
    public static class Materials
    {
        public static readonly Color32 WoodDark = Hex.C("#24140a"), WoodLit = Hex.C("#b98a55"), WoodShade = Hex.C("#3a2210"),
            Brass = Hex.C("#e0c07a"), InsetDark = Hex.C("#120a04"), InsetLit = Hex.C("#8a6238"), Paper = Hex.C("#efe7d1");

        static Img _wood;

        /// <summary>WOOD_TEX: 96px of vertical grain in four shades, dark knots, three larger knots.</summary>
        public static Img WoodTex => _wood ??= BakeWood(new Rng(0x0D1A5EEDu));

        static Img BakeWood(Rng rng)
        {
            var c = new Img(96, 96);
            c.Fill(0, 0, 96, 96, Hex.C("#6b4423"));
            var shades = new[] { Hex.C("#5b3a1c"), Hex.C("#7a5230"), Hex.C("#684222"), Hex.C("#73492a") };
            for (int x = 0; x < 96; x += 2)
            {
                int y = 0;
                while (y < 96) { int len = (8 + (int)(rng.Art() * 30)) & ~1; c.Fill(x, y, 2, len, shades[(int)(rng.Art() * 4)]); y += len; }
            }
            for (int i = 0; i < 26; i++) c.Fill(((int)(rng.Art() * 94)) & ~1, ((int)(rng.Art() * 90)) & ~1, 2, 2 + 2 * (int)(rng.Art() * 4), Hex.Rgba(28, 14, 5, 0.4f));
            for (int i = 0; i < 3; i++)
            {
                int x = 10 + (int)(rng.Art() * 76), y = 10 + (int)(rng.Art() * 76);
                c.Fill(x, y, 6, 4, Hex.C("#4a2c14")); c.Fill(x + 2, y + 1, 2, 2, Hex.C("#3a200e"));
            }
            return c;
        }

        /// <summary>woodPlate: grain, a 2px dark rim, a lit bevel top-left, a shaded one bottom-right,
        /// a brass rivet in each corner (rivets = false leaves them off, for a strip too small to carry them).</summary>
        public static Img WoodPlate(int w, int h, bool rivets = true)
        {
            var c = new Img(w, h);
            c.Pattern(WoodTex, 0, 0, w, h);
            c.Fill(0, 0, w, 2, WoodDark); c.Fill(0, h - 2, w, 2, WoodDark); c.Fill(0, 0, 2, h, WoodDark); c.Fill(w - 2, 0, 2, h, WoodDark);
            c.Fill(2, 2, w - 4, 2, WoodLit); c.Fill(2, 2, 2, h - 4, WoodLit);
            c.Fill(2, h - 4, w - 4, 2, WoodShade); c.Fill(w - 4, 2, 2, h - 4, WoodShade);
            if (rivets && w >= 16 && h >= 16)
                foreach (var (x, y) in new[] { (5, 5), (w - 7, 5), (5, h - 7), (w - 7, h - 7) })
                {
                    c.Fill(x - 1, y - 1, 4, 4, WoodDark);
                    c.Fill(x, y, 2, 2, Brass);
                }
            return c;
        }

        /// <summary>drawInset: a well cut into the plate, dark on the top-left, lit on the bottom-right.</summary>
        public static void Inset(Img g, int x, int y, int w, int h, Color32? fill)
        {
            g.Fill(x, y, w, 2, InsetDark); g.Fill(x, y, 2, h, InsetDark);
            g.Fill(x, y + h - 2, w, 2, InsetLit); g.Fill(x + w - 2, y, 2, h, InsetLit);
            if (fill.HasValue) g.Fill(x + 2, y + 2, w - 4, h - 4, fill.Value);
        }

        /// <summary>paperTex: cream stock, light and dark flecks, a faint edge.</summary>
        public static Img PaperTex(int w, int h, uint seed = 0x9A9E5u)
        {
            var rng = new Rng(seed);
            var c = new Img(w, h);
            c.Fill(0, 0, w, h, Paper);
            for (int i = 0; i < w * h / 40; i++)
                c.Fill((int)(rng.Art() * w), (int)(rng.Art() * h), 1 + (int)(rng.Art() * 2), 1,
                       rng.Art() < 0.5 ? Hex.Rgba(150, 125, 80, 0.10f) : Hex.Rgba(255, 255, 255, 0.35f));
            var edge = Hex.Rgba(120, 95, 55, 0.10f);
            c.Fill(0, 0, w, 2, edge); c.Fill(0, h - 2, w, 2, edge); c.Fill(0, 0, 2, h, edge); c.Fill(w - 2, 0, 2, h, edge);
            return c;
        }

        /// <summary>A plate with a paper sheet laid in it: wood, an inset at 8, paper at 10.</summary>
        public static Img PaperOnWood(int w, int h, int pad = 10)
        {
            var c = WoodPlate(w, h);
            Inset(c, pad - 2, pad - 2, w - (pad - 2) * 2, h - (pad - 2) * 2, null);
            c.Draw(PaperTex(w - pad * 2, h - pad * 2), pad, pad);
            return c;
        }

        // ---- the room ----

        /// <summary>caveTile: 128px of the area's floor with flecks of light and dark and wider stains.</summary>
        public static Img CaveTile(AreaPalette p, Rng rng)
        {
            var c = new Img(128, 128);
            c.Fill(0, 0, 128, 128, Hex.C(p.Floor));
            for (int i = 0; i < 220; i++)
            {
                double x = rng.Art() * 128, y = rng.Art() * 128, r = 1 + rng.Art() * 3;
                bool dark = rng.Art() < 0.5;
                float a = (float)(0.04 + rng.Art() * 0.08);
                var col = dark ? Hex.Rgba(0, 0, 0, a) : new Color32(p.FloorLight.r, p.FloorLight.g, p.FloorLight.b, (byte)Mathf.RoundToInt(a * 255));
                c.Disc((float)x, (float)y, (float)r, col);
            }
            for (int i = 0; i < 36; i++)
            {
                double x = rng.Art() * 128, y = rng.Art() * 128, r = 3 + rng.Art() * 7;
                c.Disc((float)x, (float)y, (float)r, Hex.Rgba(0, 0, 0, 0.10f));
            }
            return c;
        }

        public const int WallTileW = 64, WallTileH = 32, WallCourse = 16;

        /// <summary>wallTile: two courses of blocks, each block its own tone, a mortar line under each
        /// course and a lit edge on top of it.</summary>
        public static Img WallTile(AreaPalette p, Rng rng)
        {
            var c = new Img(WallTileW, WallTileH);
            c.Fill(0, 0, WallTileW, WallTileH, Hex.C(p.Stone));
            for (int row = 0; row < WallTileH / WallCourse; row++)
            {
                int y = row * WallCourse, off = row % 2 == 1 ? WallTileW / 4 : 0;
                c.Fill(0, y + WallCourse - 1, WallTileW, 1, Hex.C(p.Mortar));
                for (int bx = -1; bx < 2; bx++)
                {
                    int x = bx * WallTileW / 2 + off;
                    var tone = rng.Art() < 0.5 ? Hex.Rgba(0, 0, 0, (float)(0.03 + rng.Art() * 0.05)) : Hex.Rgba(255, 246, 224, (float)(0.03 + rng.Art() * 0.05));
                    c.Fill(x + 1, y, WallTileW / 2 - 2, WallCourse - 1, tone);
                }
                c.Fill(0, y, WallTileW, 1, Hex.C(p.StoneLit, 0.5f));
            }
            return c;
        }

        public const int WallThick = 16;

        /// <summary>drawFloor: the cave pattern, the room-type tint at 0.34, the area wash, a vignette.</summary>
        public static Img Floor(int w, int h, RoomKind type, AreaPalette p, Img cave)
        {
            var c = new Img(w, h);
            c.Pattern(cave, 0, 0, w, h);
            c.Fill(0, 0, w, h, Hex.C(AreaPalette.RoomBg(type), 0.34f));
            if (p.Wash > 0) c.Fill(0, 0, w, h, Hex.C(p.FloorTint, p.Wash));
            float rad = Mathf.Max(160, Mathf.Sqrt(w * w + h * h) * 0.47f);
            c.Radial(w / 2f, h / 2f, Mathf.Min(40, rad * 0.1f), new Color(1, 1, 1, 0.05f), rad, new Color(0, 0, 0, 0.32f));
            return c;
        }

        /// <summary>
        /// The floor's type-independent part, for <see cref="Floor(FloorLayers, RoomKind, AreaPalette)"/>. Every layer
        /// of <see cref="Floor(int, int, RoomKind, AreaPalette, Img)"/> composites over an opaque base, so a floor is
        /// A(x) + B(x) * C(type): A and B are made once per floor (one vignette pass), and each room type is then one
        /// multiply-add a pixel instead of four blend passes (the five types cost 0.6 s at a run's start, measured).
        /// </summary>
        public sealed class FloorLayers
        {
            public readonly int W, H;
            public readonly float[] A, B;   // A: 3 channels a pixel, 0-255; B: the vignette's pass-through
            public FloorLayers(int w, int h) { W = w; H = h; A = new float[w * h * 3]; B = new float[w * h]; }
        }

        const float TypeTint = 0.34f;

        public static FloorLayers FloorBase(int w, int h, AreaPalette p, Img cave)
        {
            var L = new FloorLayers(w, h);
            float keep = (1 - TypeTint) * (1 - p.Wash);
            float rad = Mathf.Max(160, Mathf.Sqrt(w * w + h * h) * 0.47f), r0 = Mathf.Min(40, rad * 0.1f);
            Color inner = new Color(1, 1, 1, 0.05f), outer = new Color(0, 0, 0, 0.32f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - w / 2f) * (x + 0.5f - w / 2f) + (y + 0.5f - h / 2f) * (y + 0.5f - h / 2f));
                    var v = Color.Lerp(inner, outer, Mathf.Clamp01((d - r0) / (rad - r0)));
                    var b = cave.Px[(y % cave.H) * cave.W + (x % cave.W)];
                    int i = y * w + x;
                    L.B[i] = 1 - v.a;
                    L.A[i * 3] = b.r * keep * L.B[i] + v.r * 255 * v.a;
                    L.A[i * 3 + 1] = b.g * keep * L.B[i] + v.g * 255 * v.a;
                    L.A[i * 3 + 2] = b.b * keep * L.B[i] + v.b * 255 * v.a;
                }
            return L;
        }

        /// <summary>The fast floor: equal to the four-pass bake within the passes' byte rounding (tested).</summary>
        public static Img Floor(FloorLayers L, RoomKind type, AreaPalette p)
        {
            Color32 t = Hex.C(AreaPalette.RoomBg(type)), wash = Hex.C(p.FloorTint);
            float cr = t.r * TypeTint * (1 - p.Wash) + wash.r * p.Wash, cg = t.g * TypeTint * (1 - p.Wash) + wash.g * p.Wash,
                  cb = t.b * TypeTint * (1 - p.Wash) + wash.b * p.Wash;
            var img = new Img(L.W, L.H);
            for (int i = 0; i < L.B.Length; i++)
                img.Px[i] = new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(L.A[i * 3] + L.B[i] * cr), 0, 255),
                                        (byte)Mathf.Clamp(Mathf.RoundToInt(L.A[i * 3 + 1] + L.B[i] * cg), 0, 255),
                                        (byte)Mathf.Clamp(Mathf.RoundToInt(L.A[i * 3 + 2] + L.B[i] * cb), 0, 255), 255);
            return img;
        }

        /// <summary>drawWall: four bands of the masonry pattern anchored to the room's outer corner;
        /// the image is the room plus the wall on every side, its middle left clear.</summary>
        public static Img Walls(int roomW, int roomH, Img tile)
        {
            int wt = WallThick, iw = roomW + wt * 2, ih = roomH + wt * 2;
            var c = new Img(iw, ih);
            c.Pattern(tile, 0, 0, iw, wt);
            c.Pattern(tile, 0, ih - wt, iw, wt);
            c.Pattern(tile, 0, wt, wt, ih - wt * 2);
            c.Pattern(tile, iw - wt, wt, wt, ih - wt * 2);
            return c;
        }

        /// <summary>The seed tag: a luggage tag cut from the same plank, a clipped corner, a punched
        /// hole, the seed stamped in and SEED under it (seedTagArt; the type is the pixel font).</summary>
        public static Img SeedTag(int w, int h, string text)
        {
            const int CH = 18;
            var c = new Img(w, h);
            // the outline: a rectangle with the top-left corner cut on the diagonal
            Func<int, int, bool> inside = (x, y) => x >= 1 && x < w - 1 && y >= 1 && y < h - 1 && x + y >= CH + 1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (inside(x, y)) c.Blend(x, y, WoodTex.Get(x % 96, y % 96));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!inside(x, y)) continue;
                    bool rim = !inside(x - 1, y) || !inside(x + 1, y) || !inside(x, y - 1) || !inside(x, y + 1);
                    bool rim2 = !inside(x - 2, y) || !inside(x, y - 2);
                    if (rim) c.Blend(x, y, Hex.Rgba(26, 14, 6, 0.85f));
                    else if (rim2) c.Blend(x, y, Hex.Rgba(255, 228, 186, 0.26f));
                }
            for (int i = 0; i < CH - 1; i++) c.Blend(1 + i, CH - i, Hex.Rgba(255, 214, 160, 0.32f));   // the cut edge catches the light
            float hx = CH * 0.72f, hy = h / 2f;
            c.Disc(hx, hy, 5.5f, Hex.C("#150c05"));
            for (int a = 0; a < 24; a++)   // the far wall of the hole, lit on the lower right
            {
                float t = Mathf.PI * (0.15f + 0.8f * a / 23f);
                c.Blend(Mathf.RoundToInt(hx + Mathf.Cos(t) * 5.5f), Mathf.RoundToInt(hy + Mathf.Sin(t) * 5.5f), Hex.Rgba(240, 205, 150, 0.38f));
            }
            int scale = Math.Max(2, Mathf.RoundToInt(h * 0.40f / PixelFont.H));
            int cx = w / 2 + 12, cy = Mathf.RoundToInt(h * 0.47f);
            string spaced = text ?? "";
            PixelFont.Stamp(c, spaced, cx, cy + 1, scale, Hex.Rgba(255, 214, 160, 0.32f));
            PixelFont.Stamp(c, spaced, cx, cy, scale, Hex.C("#241206"));
            PixelFont.Stamp(c, "SEED", cx, Mathf.RoundToInt(h * 0.82f), 1, Hex.Rgba(36, 18, 6, 0.72f));
            return c;
        }
    }

    /// <summary>The room's floor and walls for the run being drawn, baked once per (seed, area, type)
    /// and re-made when the run, the floor or the room type changes - as the JS caches are run-scoped.</summary>
    public sealed class RoomArt
    {
        uint _seed; int _floor; bool _have;
        Img _cave, _wallTile;
        Materials.FloorLayers _layers;   // this floor's type-independent part, made on its first floor texture
        readonly Dictionary<RoomKind, Texture2D> _floors = new Dictionary<RoomKind, Texture2D>();
        Texture2D _walls;
        public AreaPalette Palette { get; private set; }

        /// <summary>The floor's stone, re-made per floor as the JS clears its art caches on every descent.</summary>
        public void Sync(uint seed, int floor)
        {
            if (_have && seed == _seed && floor == _floor) return;
            Release();
            _have = true; _seed = seed; _floor = floor;
            Palette = AreaPalette.For(AreaRules.AreaForFloor(floor));
            var rng = new Rng(seed ^ 0xA27u ^ (uint)floor * 0x9E3779B1u);   // the view's own art stream, per run and floor
            _cave = Materials.CaveTile(Palette, rng);
            _layers = null;
            _wallTile = Materials.WallTile(Palette, rng);
        }

        /// <summary>Bakes every room type's floor and the walls now (behind a fade), so no room entry bakes mid-frame.</summary>
        public void Warm()
        {
            foreach (RoomKind k in System.Enum.GetValues(typeof(RoomKind))) Floor(k);
            _ = Walls;
        }

        public Texture2D Floor(RoomKind type)
        {
            if (type == RoomKind.Secret) type = RoomKind.Normal;   // the same tint (ROOM_BG has no secret entry): one texture
            if (!_floors.TryGetValue(type, out var t))
            {
                _layers ??= Materials.FloorBase(Balance.RoomRight - Balance.RoomLeft, Balance.RoomBottom - Balance.RoomTop, Palette, _cave);
                _floors[type] = t = Atlas.ToTexture(Materials.Floor(_layers, type, Palette), "floor");
            }
            return t;
        }

        public Texture2D Walls => _walls ??= Atlas.ToTexture(Materials.Walls(Balance.RoomRight - Balance.RoomLeft, Balance.RoomBottom - Balance.RoomTop, _wallTile), "walls");

        public void Release()
        {
            foreach (var t in _floors.Values) Atlas.Free(t);
            _floors.Clear();
            Atlas.Free(_walls);
            _walls = null;
        }
    }
}
