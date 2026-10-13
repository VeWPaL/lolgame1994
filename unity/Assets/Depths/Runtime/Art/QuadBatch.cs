using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Depths.Unity.Art
{
    /// <summary>
    /// Sprite quads for a UI Toolkit element, collected per texture and written as one temp mesh per
    /// run of the same texture, in draw order. Buffers are reused: drawing a frame allocates nothing
    /// on the managed heap. Call <see cref="Flush"/> before any Painter2D stroke so the order holds.
    /// </summary>
    public sealed class QuadBatch
    {
        Vertex[] _v = new Vertex[4 * 1024];
        int _quads;
        Texture _tex;
        MeshGenerationContext _mgc;

        public int DrawCalls { get; private set; }   // meshes written since Begin, for the perf tests
        public int Quads { get; private set; }

        public void Begin(MeshGenerationContext mgc) { _mgc = mgc; _quads = 0; _tex = null; DrawCalls = 0; Quads = 0; }

        /// <summary>A sprite with its top-left at (x, y), drawn w x h, tinted (alpha = globalAlpha).</summary>
        public void Sprite(Texture tex, in Region r, float x, float y, float w, float h, Color32 tint)
        {
            if (tint.a == 0) return;
            Use(tex);
            int i = _quads * 4;
            _v[i + 0] = V(x, y, r.U0, r.V1, tint);
            _v[i + 1] = V(x + w, y, r.U1, r.V1, tint);
            _v[i + 2] = V(x + w, y + h, r.U1, r.V0, tint);
            _v[i + 3] = V(x, y + h, r.U0, r.V0, tint);
            _quads++; Quads++;
        }

        /// <summary>The sprite at its own pixel size, centred on (cx, cy) and snapped to whole pixels like drawSprite.</summary>
        public void Centred(Texture tex, in Region r, float cx, float cy, Color32 tint, float scale = 1)
        {
            float w = r.W * scale, h = r.H * scale;
            Sprite(tex, r, Mathf.Round(cx - w / 2), Mathf.Round(cy - h / 2), w, h, tint);
        }

        /// <summary>A sprite rotated by <paramref name="angle"/> (radians) about a pivot; (lx, ly) is the
        /// sprite's top-left in the rotated frame, relative to the pivot.</summary>
        public void Rotated(Texture tex, in Region r, Vector2 pivot, float lx, float ly, float w, float h, float angle, Color32 tint)
        {
            if (tint.a == 0) return;
            Use(tex);
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            Vector2 P(float x, float y) => new Vector2(pivot.x + x * c - y * s, pivot.y + x * s + y * c);
            int i = _quads * 4;
            var a = P(lx, ly); var b = P(lx + w, ly); var d = P(lx + w, ly + h); var e = P(lx, ly + h);
            _v[i + 0] = V(a.x, a.y, r.U0, r.V1, tint);
            _v[i + 1] = V(b.x, b.y, r.U1, r.V1, tint);
            _v[i + 2] = V(d.x, d.y, r.U1, r.V0, tint);
            _v[i + 3] = V(e.x, e.y, r.U0, r.V0, tint);
            _quads++; Quads++;
        }

        /// <summary>A solid rectangle, from the atlas's white texel so it stays in the same batch.</summary>
        public void Rect(float x, float y, float w, float h, Color32 c) => Sprite(SpriteSet.Atlas.Texture, SpriteSet.WhiteTexel, x, y, w, h, c);

        /// <summary>A stroked circle as a polygon of thin quads, so rings stay in the atlas batch
        /// (a Painter2D stroke between sprites would split it into another mesh each time).</summary>
        public void Ring(float cx, float cy, float r, float width, Color32 c)
        {
            if (c.a == 0 || r <= 0) return;
            int n = Mathf.Clamp(Mathf.CeilToInt(r * 0.8f), 10, 72);
            float step = 2 * Mathf.PI / n, chord = 2 * r * Mathf.Sin(step / 2) + 0.6f;   // a hair long, so segments meet
            var tex = SpriteSet.Atlas.Texture; var white = SpriteSet.WhiteTexel;
            for (int i = 0; i < n; i++)
            {
                float a = i * step, px = cx + Mathf.Cos(a) * r, py = cy + Mathf.Sin(a) * r;
                Rotated(tex, white, new Vector2(px, py), -chord / 2, -width / 2, chord, width, a + Mathf.PI / 2 + step / 2, c);
            }
        }

        static Vertex V(float x, float y, float u, float v, Color32 c) =>
            new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = c, uv = new Vector2(u, v) };

        void Use(Texture tex)
        {
            if (_tex != tex) { Flush(); _tex = tex; }
            if ((_quads + 1) * 4 > _v.Length) { if (_v.Length < 4 * 16384) System.Array.Resize(ref _v, _v.Length * 2); else Flush(); }
        }

        /// <summary>Writes what is collected. Quads past 16k (the 16-bit index limit) go in a second mesh.</summary>
        public void Flush()
        {
            if (_quads == 0 || _mgc == null) { _quads = 0; return; }
            int done = 0;
            while (done < _quads)
            {
                int n = Mathf.Min(_quads - done, 16383);
                _mgc.AllocateTempMesh(n * 4, n * 6, out NativeSlice<Vertex> verts, out NativeSlice<ushort> idx);
                for (int q = 0; q < n; q++)
                {
                    int s = (done + q) * 4, d = q * 4;
                    verts[d] = _v[s]; verts[d + 1] = _v[s + 1]; verts[d + 2] = _v[s + 2]; verts[d + 3] = _v[s + 3];
                    int k = q * 6;
                    idx[k] = (ushort)d; idx[k + 1] = (ushort)(d + 1); idx[k + 2] = (ushort)(d + 2);
                    idx[k + 3] = (ushort)d; idx[k + 4] = (ushort)(d + 2); idx[k + 5] = (ushort)(d + 3);
                }
                _mgc.DrawMesh(verts, idx, _tex, TextureOptions.SkipDynamicAtlas);
                DrawCalls++;
                done += n;
            }
            _quads = 0;
        }

        public static Color32 Alpha(float a) => new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255), 0, 255));
        public static Color32 Tint(Color32 c, float a) => new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(Mathf.RoundToInt(a * c.a), 0, 255));
    }
}
