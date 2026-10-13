using System;
using UnityEngine;

namespace Depths.Unity.Art
{
    /// <summary>
    /// A CPU image with the few canvas operations the JS art uses (fillRect, arc fill, drawImage,
    /// pattern fill, radial gradient), composited source-over. Pure C#, so every bake is testable
    /// without a GPU; textures are made from it once (<see cref="Atlas"/>). y points down, as in canvas.
    /// </summary>
    public sealed class Img
    {
        public readonly int W, H;
        public readonly Color32[] Px;   // row-major from the TOP row

        public Img(int w, int h) { W = Math.Max(1, w); H = Math.Max(1, h); Px = new Color32[W * H]; }

        public Color32 Get(int x, int y) => Px[y * W + x];

        /// <summary>Source-over at coverage <paramref name="cover"/> (0-1) times the colour's own alpha.</summary>
        public void Blend(int x, int y, Color32 c, float cover = 1)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            float sa = c.a / 255f * cover;
            if (sa <= 0) return;
            int i = y * W + x;
            var d = Px[i];
            float da = d.a / 255f, oa = sa + da * (1 - sa);
            if (oa <= 0) { Px[i] = default; return; }
            float k = da * (1 - sa);
            Px[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((c.r * sa + d.r * k) / oa), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((c.g * sa + d.g * k) / oa), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((c.b * sa + d.b * k) / oa), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(oa * 255), 0, 255));
        }

        /// <summary>fillRect on whole pixels (every JS sprite bake uses whole-pixel rects).</summary>
        public void Fill(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
                for (int xx = Math.Max(0, x); xx < Math.Min(W, x + w); xx++) Blend(xx, yy, c);
        }

        /// <summary>An anti-aliased filled circle, by 4x4 supersampling: the canvas arc fill.</summary>
        public void Disc(float cx, float cy, float r, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r), y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
            float rr = r * r;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int n = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float dx = x + (sx + 0.5f) / 4 - cx, dy = y + (sy + 0.5f) / 4 - cy;
                            if (dx * dx + dy * dy <= rr) n++;
                        }
                    if (n > 0) Blend(x, y, c, n / 16f);
                }
        }

        /// <summary>drawImage at whole-pixel offset, with globalAlpha.</summary>
        public void Draw(Img src, int x, int y, float alpha = 1)
        {
            for (int sy = 0; sy < src.H; sy++)
                for (int sx = 0; sx < src.W; sx++) Blend(x + sx, y + sy, src.Px[sy * src.W + sx], alpha);
        }

        /// <summary>A repeating pattern anchored at the image origin, as createPattern + fillRect.</summary>
        public void Pattern(Img tile, int x, int y, int w, int h, float alpha = 1)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
                for (int xx = Math.Max(0, x); xx < Math.Min(W, x + w); xx++)
                    Blend(xx, yy, tile.Px[(yy % tile.H) * tile.W + (xx % tile.W)], alpha);
        }

        /// <summary>A radial gradient from (inner, r0) to (outer, r1) over the whole image, colours mixed straight.</summary>
        public void Radial(float cx, float cy, float r0, Color inner, float r1, Color outer)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float t = Mathf.Clamp01((d - r0) / Math.Max(1e-4f, r1 - r0));
                    Blend(x, y, Color.Lerp(inner, outer, t));
                }
        }

        public Img Clone()
        {
            var c = new Img(W, H);
            Array.Copy(Px, c.Px, Px.Length);
            return c;
        }
    }

    /// <summary>Colours written the way the JS writes them.</summary>
    public static class Hex
    {
        public static Color32 C(string h, float a = 1)
        {
            string s = h.TrimStart('#');
            if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            if (s.Length != 6) throw new ArgumentException("not a colour: \"" + h + "\" - expected #abc or #aabbcc");
            return new Color32(Convert.ToByte(s.Substring(0, 2), 16), Convert.ToByte(s.Substring(2, 2), 16),
                               Convert.ToByte(s.Substring(4, 2), 16), (byte)Mathf.RoundToInt(a * 255));
        }

        public static Color32 Rgba(int r, int g, int b, float a) => new Color32((byte)r, (byte)g, (byte)b, (byte)Mathf.RoundToInt(a * 255));

        /// <summary>mixHex: t is how much of b lands in the result; both ends exact.</summary>
        public static Color32 Mix(Color32 a, Color32 b, float t) => new Color32(
            (byte)Mathf.RoundToInt(a.r + (b.r - a.r) * t), (byte)Mathf.RoundToInt(a.g + (b.g - a.g) * t),
            (byte)Mathf.RoundToInt(a.b + (b.b - a.b) * t), 255);
    }
}
