using System;
using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity.Art
{
    /// <summary>One sprite's place in an atlas: its size in pixels and its UV rectangle (y up).</summary>
    public readonly struct Region
    {
        public readonly int W, H;
        public readonly float U0, V0, U1, V1;
        public Region(int w, int h, float u0, float v0, float u1, float v1) { W = w; H = h; U0 = u0; V0 = v0; U1 = u1; V1 = v1; }
    }

    /// <summary>Packs baked images into one point-filtered texture (shelf packing, tallest first, 2px gutters), so the whole... [h:Atlas-1]</summary>
    public sealed class Atlas
    {
        public Texture2D Texture { get; }
        readonly Dictionary<string, Region> _regions;

        Atlas(Texture2D tex, Dictionary<string, Region> regions) { Texture = tex; _regions = regions; }

        public bool Has(string key) => _regions.ContainsKey(key);
        public int Count => _regions.Count;

        public Region this[string key] => _regions.TryGetValue(key, out var r) ? r
            : throw new KeyNotFoundException("atlas has no sprite \"" + key + "\"");

        /// <summary>Lays the images out and returns where each went, without making a texture (tested alone).</summary>
        public static Dictionary<string, RectInt> Pack(IList<(string key, Img img)> items, int size, int gutter = 2)
        {
            var order = new List<(string key, Img img)>(items);
            order.Sort((a, b) => b.img.H != a.img.H ? b.img.H.CompareTo(a.img.H) : string.CompareOrdinal(a.key, b.key));
            var at = new Dictionary<string, RectInt>();
            int x = gutter, y = gutter, shelf = 0;
            foreach (var (key, img) in order)
            {
                if (at.ContainsKey(key)) throw new ArgumentException("atlas key twice: " + key);
                if (x + img.W + gutter > size) { x = gutter; y += shelf + gutter; shelf = 0; }
                if (img.W + gutter * 2 > size || y + img.H + gutter > size) return null;   // does not fit at this size
                at[key] = new RectInt(x, y, img.W, img.H);
                x += img.W + gutter;
                shelf = Math.Max(shelf, img.H);
            }
            return at;
        }

        public static Atlas Build(IList<(string key, Img img)> items)
        {
            for (int size = 256; size <= 4096; size *= 2)
            {
                var at = Pack(items, size);
                if (at == null) continue;
                var px = new Color32[size * size];
                foreach (var (key, img) in items)
                {
                    var r = at[key];
                    for (int y = 0; y < img.H; y++)   // the image is top-down, the texture bottom-up
                        Array.Copy(img.Px, y * img.W, px, (size - 1 - (r.y + y)) * size + r.x, img.W);
                }
                var tex = NewTexture(size, size, "DepthsAtlas");
                tex.SetPixels32(px);
                tex.Apply(false, false);   // kept readable: the pixel checks read it back (about 1 MB)
                var regions = new Dictionary<string, Region>();
                foreach (var (key, img) in items)
                {
                    var r = at[key];
                    regions[key] = new Region(img.W, img.H, (float)r.x / size, (float)(size - r.y - img.H) / size,
                                              (float)(r.x + img.W) / size, (float)(size - r.y) / size);
                }
                return new Atlas(tex, regions);
            }
            throw new InvalidOperationException("the atlas does not fit in 4096x4096");
        }

        static Texture2D NewTexture(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name, hideFlags = HideFlags.DontSave };

        /// <summary>A single image as its own texture (the room's floor and walls, a plate for a panel).</summary>
        public static Texture2D ToTexture(Img img, string name)
        {
            var px = new Color32[img.W * img.H];
            for (int y = 0; y < img.H; y++) Array.Copy(img.Px, y * img.W, px, (img.H - 1 - y) * img.W, img.W);
            var tex = NewTexture(img.W, img.H, name);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Destroy that also works in edit mode (the EditMode tests make textures too).</summary>
        public static void Free(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
