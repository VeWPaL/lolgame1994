using System;
using UnityEngine;
using UnityEngine.UIElements;
using Depths.Unity.Art;

namespace Depths.Unity
{
    /// <summary>
    /// The room and everything in it, drawn the way src/70-view.js drawRoom draws it: the baked floor
    /// and masonry, doors and their locks, the bodies' pixel sprites and walk cycles, shells, rings and
    /// the player with the wand. Sprites go through one atlas in batched quads; only rings and dashes
    /// use Painter2D. Reads the run, never writes it.
    /// </summary>
    public sealed class WorldView : VisualElement
    {
        /// <summary>World to panel: the room's centre at the panel's centre (1280x720 reference).</summary>
        public static readonly Vector2 Origin = new Vector2(640 - Balance.MidX, 360 - Balance.MidY);
        public static Vector2 ToPanel(double x, double y) => new Vector2((float)x + Origin.x, (float)y + Origin.y);

        readonly Func<RunState> _run;
        readonly QuadBatch _q = new QuadBatch();
        readonly RoomArt _art = new RoomArt();
        public readonly FxTracker Fx = new FxTracker();

        /// <summary>Where the player aims, in world units (the wand points at it).</summary>
        public Vector2 Aim { get; set; }
        /// <summary>A cosmetic clock at 60 a second that stops with the game (the JS's frameCount / SPEEDUP).</summary>
        public float Fc { get; private set; }
        public int LastDrawCalls => _q.DrawCalls;
        public int LastQuads => _q.Quads;

        public WorldView(Func<RunState> run)
        {
            _run = run;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
            RegisterCallback<DetachFromPanelEvent>(_ => _art.Release());
        }

        /// <summary>Advances the cosmetic clock and the derived FX; dt is 0 while the game is stopped.</summary>
        public void Step(float dt)
        {
            Fc += dt * 60;
            var run = _run();
            if (run != null) Fx.Update(run, dt);
            MarkDirtyRepaint();
        }

        // ---- drawing helpers, world coordinates ----
        Atlas A => SpriteSet.Atlas;
        void Spr(string name, double cx, double cy, float alpha = 1) =>
            _q.Centred(A.Texture, A[name], (float)cx + Origin.x, (float)cy + Origin.y, QuadBatch.Alpha(alpha));
        void Rect(double x, double y, double w, double h, Color32 c) => _q.Rect((float)x + Origin.x, (float)y + Origin.y, (float)w, (float)h, c);
        void Glow(double cx, double cy, float r, Color32 c, float alpha)
        {
            var reg = A[SpriteSet.Glow];
            _q.Sprite(A.Texture, reg, (float)cx - r + Origin.x, (float)cy - r + Origin.y, r * 2, r * 2, QuadBatch.Tint(c, alpha));
        }
        void Dot(double cx, double cy, float rx, float ry, Color32 c)
        {
            var reg = A[SpriteSet.Disc];
            _q.Sprite(A.Texture, reg, (float)cx - rx + Origin.x, (float)cy - ry + Origin.y, rx * 2, ry * 2, c);
        }
        void Ring(Painter2D g, double cx, double cy, float r, float width, Color32 c)
        {
            if (c.a == 0 || r <= 0) return;
            _q.Flush();
            g.strokeColor = c; g.lineWidth = width;
            g.BeginPath(); g.Arc(ToPanel(cx, cy), r, 0, 360); g.Stroke();
        }

        static readonly Color32 Gold = Hex.C("#ffd23d"), Silver = Hex.C("#d8dee9"), Green = Hex.C("#5ee27a"), Shut = Hex.C("#c93b3b"),
            BarBack = new Color32(0, 0, 0, 255), Shadow = Hex.Rgba(0, 0, 0, 0.3f), Ink = Hex.C("#14151a"), Exit = Hex.C("#f4e6c8");

        void Paint(MeshGenerationContext ctx)
        {
            var run = _run();
            if (run == null || run.dungeon == null) return;
            var g = ctx.painter2D;
            _q.Begin(ctx);
            var room = run.CurrentRoom;
            var area = AreaRules.AreaForFloor(run.floor);
            _art.Sync(run.rootSeed, area);
            var pal = _art.Palette;
            var type = room?.Type ?? RoomKind.Normal;

            // floor, the hook's ground spell, the walls
            var fl = _art.Floor(type);
            _q.Sprite(fl, new Region(fl.width, fl.height, 0, 0, 1, 1), Balance.RoomLeft + Origin.x, Balance.RoomTop + Origin.y, fl.width, fl.height, QuadBatch.Alpha(1));
            DrawHookFields(g, run);
            var wl = _art.Walls;
            int wt = Materials.WallThick;
            _q.Sprite(wl, new Region(wl.width, wl.height, 0, 0, 1, 1), Balance.RoomLeft - wt + Origin.x, Balance.RoomTop - wt + Origin.y, wl.width, wl.height, QuadBatch.Alpha(1));
            if (room != null) DrawDoors(run, room, pal);

            // dash and charge puffs, under the bodies
            for (int i = 0; i < Fx.Count; i++)
            {
                ref var f = ref Fx[i];
                if (f.Ring) continue;
                if (f.Charge)
                {
                    float grow = 1 - f.Life / f.Max, rr = 6 + 7 * grow;
                    Glow(f.X, f.Y, rr, new Color32(255, 255, 255, 255), 0.16f + 0.34f * grow);
                    continue;
                }
                Color32 c = f.Mine ? MomentumTint(f.Mom) : new Color32(255, 255, 255, 255);
                Glow(f.X, f.Y, 14, c, 0.35f * f.Life / f.Max);
            }

            DrawLungeLines(g, run);
            DrawPickups(g, run);
            DrawBodies(g, run);
            DrawShells(g, run);
            for (int i = 0; i < Fx.Count; i++)
            {
                ref var f = ref Fx[i];
                if (!f.Ring) continue;
                float t = 1 - f.Life / f.Max, rad = f.R * (f.Dir < 0 ? 1 - t : t);
                Ring(g, f.X, f.Y, Mathf.Max(1, rad), 3, QuadBatch.Tint(f.Color, 0.7f * (1 - t * 0.55f)));
            }
            DrawPlayer(g, run);
            if (run.roomFade > 0.01)
                Rect(Balance.RoomLeft - wt, Balance.RoomTop - wt, Balance.RoomRight - Balance.RoomLeft + wt * 2, Balance.RoomBottom - Balance.RoomTop + wt * 2,
                     Hex.Rgba(0, 0, 0, Mathf.Clamp01((float)run.roomFade)));
            _q.Flush();
        }

        static Color32 MomentumTint(float m)
        {
            int i = Mathf.RoundToInt(Mathf.Clamp01(m) * 4);   // MOMENTUM_STEPS 5: white to green
            float t = i / 4f;
            return new Color32((byte)Mathf.RoundToInt(255 + (120 - 255) * t), 255, (byte)Mathf.RoundToInt(255 + (160 - 255) * t), 255);
        }

        // ---- the room's edge ----

        /// <summary>doorRect: the gap in the wall for a side, as x, y, w, h in world units.</summary>
        public static (float x, float y, float w, float h) DoorRect(Dir d, int wt)
        {
            float hw = Balance.DoorWidth / 2f;
            return d switch
            {
                Dir.N => (Balance.MidX - hw, Balance.RoomTop - wt, Balance.DoorWidth, wt),
                Dir.S => (Balance.MidX - hw, Balance.RoomBottom, Balance.DoorWidth, wt),
                Dir.W => (Balance.RoomLeft - wt, Balance.MidY - hw, wt, Balance.DoorWidth),
                _ => (Balance.RoomRight, Balance.MidY - hw, wt, Balance.DoorWidth),
            };
        }

        static (float x, float y) LockPoint(Dir d) => d switch
        {
            Dir.N => (Balance.MidX, Balance.RoomTop - 8), Dir.S => (Balance.MidX, Balance.RoomBottom + 8),
            Dir.W => (Balance.RoomLeft - 8, Balance.MidY), _ => (Balance.RoomRight + 8, Balance.MidY),
        };

        static readonly Dir[] Sides = { Dir.N, Dir.S, Dir.W, Dir.E };

        void DrawDoors(RunState run, Room room, AreaPalette pal)
        {
            int wt = Materials.WallThick;
            var tone = pal.RoomTone(room.Type);
            foreach (var d in Sides)
            {
                if (!room.Doors.Contains(d)) continue;
                var (x, y, w, h) = DoorRect(d, wt);
                Rect(x, y, w, h, tone);
                var next = run.dungeon.Neighbour(room, d)?.Type;
                var lip = run.enemies.Count > 0 ? Shut : next == RoomKind.Boss && !run.bossUnlocked ? Gold : next == RoomKind.Item && !run.itemUnlocked ? Silver : Green;
                if (d == Dir.N || d == Dir.S) Rect(x, d == Dir.N ? y : y + h - 4, w, 4, lip);
                else Rect(d == Dir.W ? x : x + w - 4, y, 4, h, lip);
            }
            float breathe = 0.5f + 0.5f * Mathf.Sin(Fc * 0.07f);
            foreach (var d in Sides)   // the boss gate: a portcullis that breathes, and lifts as the lock works
            {
                if (!room.Doors.Contains(d) || run.dungeon.Neighbour(room, d)?.Type != RoomKind.Boss || run.bossUnlocked) continue;
                var (x, y, w, h) = DoorRect(d, wt);
                float prog = Unlocking(run, room, d) ? run.unlockT / (float)Balance.UnlockTime : 0, lift = prog * 0.5f;
                Rect(x, y, w, h, Hex.Rgba(20, 16, 8, 0.85f - 0.25f * breathe));
                var slat = Hex.Rgba(255, 210, 61, 0.55f + 0.35f * breathe);
                for (int i = 0; i < 5; i++)
                {
                    float f = i / 4f, gap = lift * Balance.DoorWidth * 0.5f;
                    if (d == Dir.N || d == Dir.S) Rect(x + 4 + f * (w - 8) + gap, y + 3, 3, h - 6, slat);
                    else Rect(x + 3, y + 4 + f * (h - 8) + gap, w - 6, 3, slat);
                }
                Rect(x, y, w, h, QuadBatch.Tint(Gold, Mathf.Clamp01(0.20f + 0.14f * breathe - prog * 0.3f)));
            }
            if (room.Secret.HasValue)   // a fake wall: three hairline cracks are the only tell
            {
                var s = room.Secret.Value;
                var (px, py) = Rooms.DoorPoint(s);
                var crack = Hex.Rgba(164, 172, 188, 0.24f);
                for (int i = 0; i < 3; i++)
                    if (s == Dir.N || s == Dir.S) Rect(px - 8 + i * 6, py - (s == Dir.N ? 8 : -4), 1, 6, crack);
                    else Rect(px - (s == Dir.W ? 8 : -4), py - 8 + i * 6, 6, 1, crack);
            }
            foreach (var d in Sides)   // padlocks, tinted to their key; the shackle lifts as the lock throws
            {
                if (!room.Doors.Contains(d) || !Rooms.DoorSealed(run, room, d)) continue;
                var col = run.dungeon.Neighbour(room, d)?.Type == RoomKind.Boss ? Gold : Silver;
                float prog = Unlocking(run, room, d) ? run.unlockT / (float)Balance.UnlockTime : 0;
                if (prog > 0)
                {
                    var (x, y, w, h) = DoorRect(d, wt);
                    var band = QuadBatch.Tint(col, 0.3f + 0.4f * prog);
                    if (w > h) Rect(x, y, w, 8, band); else Rect(x, y, 8, h, band);
                }
                float lift = prog * 14, a = Rooms.HasKeyFor(run, room, d) ? 0.55f + 0.45f * prog : 1;
                var (cx, cy) = LockPoint(d);
                var c = QuadBatch.Tint(col, a);
                Rect(cx - 6, cy - 1 - lift, 12, 8, c);
                Rect(cx - 4, cy - 7 - lift, 8, 2, c); Rect(cx - 4, cy - 7 - lift, 2, 6, c); Rect(cx + 2, cy - 7 - lift, 2, 6, c);   // the shackle
                Rect(cx - 1, cy + 2 - lift, 2, 3, QuadBatch.Tint(Ink, a));
            }
        }

        static bool Unlocking(RunState run, Room r, Dir d) => run.unlockRoom == r && run.unlockDir == d;

        void DrawHookFields(Painter2D g, RunState run)
        {
            var col = Hex.C(Sprites.HookColor);
            foreach (var f in run.hookFields)
            {
                float left = f.life / (float)Math.Max(1, f.max), fade = left < 0.34f ? left / 0.34f : 1;
                Dot(f.x, f.y, (float)f.r, (float)f.r, QuadBatch.Tint(col, 0.13f * fade));
                Ring(g, f.x, f.y, (float)f.r * (0.55f + 0.45f * (1 - left)), 2, QuadBatch.Tint(col, 0.5f * fade));
                Ring(g, f.x, f.y, (float)f.r * 0.55f, 1, QuadBatch.Tint(col, 0.3f * fade));
                float t = Fc * 0.05f;
                for (int i = 0; i < 7; i++)   // the inward spiral: each mote on its own radius and phase
                {
                    float spin = t * (1.5f + (i % 3) * 0.35f) + i * 0.9f;
                    float rad = (float)f.r * (0.5f + 0.48f * (1 - Frac(t * 0.22f + i * 0.14f)));
                    float mx = (float)f.x + Mathf.Cos(spin) * rad, my = (float)f.y + Mathf.Sin(spin) * rad * 0.86f;
                    Rect(Mathf.Round(mx) - 1, Mathf.Round(my) - 1, 2, 2, Hex.C("#e8fffb", (0.75f - 0.35f * ((i * 7) % 5) / 5f) * fade));
                }
            }
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        // ---- the committed line: a lunger's windup and its lunge ----
        void DrawLungeLines(Painter2D g, RunState run)
        {
            var col = Hex.C("#ff9a6b");
            foreach (var e in run.enemies)
            {
                if (e.lungeState != "wind" && e.lungeState != "lunge") continue;
                bool wind = e.lungeState == "wind";
                float grow = wind ? 1 - e.lungeT / (float)Balance.LungeWindup : 1;
                double len = e.lungeLen > 0 ? e.lungeLen : Balance.LungeFloor;
                double ex = e.x, ey = e.y, tx = e.x + e.lungeDx * len, ty = e.y + e.lungeDy * len;
                if (!wind)
                {
                    if (!Fx.LungeFrom(e, out float fx, out float fy)) continue;
                    ex = fx; ey = fy; tx = e.x; ty = e.y;
                }
                Dashes(ex, ey, tx, ty, 1 + 2 * grow, QuadBatch.Tint(col, 0.20f + 0.55f * grow));
                Ring(g, tx, ty, 5 + 3 * grow, 1 + 2 * grow, QuadBatch.Tint(col, 0.30f + 0.50f * grow));
            }
        }

        void Dashes(double x0, double y0, double x1, double y1, float width, Color32 c)
        {
            float dx = (float)(x1 - x0), dy = (float)(y1 - y0), len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            float ang = Mathf.Atan2(dy, dx);
            var r = A[SpriteSet.White];
            var white = new Region(1, 1, (r.U0 + r.U1) / 2, (r.V0 + r.V1) / 2, (r.U0 + r.U1) / 2, (r.V0 + r.V1) / 2);
            for (float s = 0; s < len; s += 10)   // setLineDash([5, 5])
                _q.Rotated(A.Texture, white, ToPanel(x0, y0), s, -width / 2, Mathf.Min(5, len - s), width, ang, c);
        }

        // ---- loot ----
        void DrawPickups(Painter2D g, RunState run)
        {
            foreach (var pk in run.pickups)
            {
                float bob = Mathf.Sin(Fc * 0.08f + (float)pk.x) * 2;
                switch (pk.kind)
                {
                    case "exit": DrawExit(g, pk.x, pk.y); break;
                    case "weapon": Spr(SpriteSet.Icon(pk.w.ToString()), pk.x, pk.y + bob); break;
                    case "hook": case "blast": Spr(SpriteSet.Icon(pk.kind), pk.x, pk.y + bob); break;
                    case "item": Spr(SpriteSet.ItemIcon(pk.id, 2) ?? SpriteSet.Item(pk.id, 26), pk.x, pk.y + Mathf.Sin(Fc * 0.09f + (float)pk.x) * 2); break;   // its picture, as in the kit
                    case "key": case "goldkey": Spr(SpriteSet.Key(pk.kind == "goldkey", 3, false), pk.x, pk.y + Mathf.Sin(Fc * 0.1f) * 2); break;
                    case "heart": Spr(SpriteSet.Heart(Sprites.HeartScheme.Red, 2, 1), pk.x, pk.y); break;
                    case "halfheart": Spr(SpriteSet.Heart(Sprites.HeartScheme.Red, 2, 0.5f), pk.x, pk.y); break;
                    case "armor": Spr(SpriteSet.Heart(Sprites.HeartScheme.Gray, 2, 1), pk.x, pk.y); break;
                    case "halfarmor": Spr(SpriteSet.Heart(Sprites.HeartScheme.Gray, 2, 0.5f), pk.x, pk.y); break;
                    default: Dot(pk.x, pk.y, 6, 6, Hex.C("#50dcbe")); break;
                }
            }
        }

        /// <summary>drawExitPortal: three rings breathing outward, a soft disc, a floor shadow, an up-arrow.</summary>
        void DrawExit(Painter2D g, double cx, double cy)
        {
            float t = Fc * 0.05f;
            for (int i = 0; i < 3; i++)
            {
                float p = Frac(t * 0.5f + i / 3f);
                Ring(g, cx, cy, 8 + 30 * p, 2, QuadBatch.Tint(Exit, 0.5f * (1 - p)));
            }
            Dot(cx, cy, 15, 15, QuadBatch.Tint(Exit, 0.20f + 0.10f * Mathf.Sin(t * 2)));
            Dot(cx, cy + 16, 14, 5, Hex.Rgba(0, 0, 0, 0.28f));
            var arrow = QuadBatch.Tint(Exit, 0.85f);
            Rect(cx - 2, cy - 2, 4, 14, arrow);
            for (int row = 0; row < 10; row++)   // the head, as rows: 10 tall, 16 wide at its base
            {
                float half = 8 * (row + 1) / 10f;
                Rect(cx - half, cy - 11 + row, half * 2, 1, arrow);
            }
        }

        // ---- the bodies ----
        static float BarHeight(BodyKind k) => k == BodyKind.Brunch ? 11 : k == BodyKind.Gunner ? 32 : k == BodyKind.Boss ? 40 : 26;
        static string CastColor(BodyKind k) => k == BodyKind.Gunner ? "#ffb03a" : "#ff4d4d";

        void DrawBodies(Painter2D g, RunState run)
        {
            float readyProg = run.readyT > 0 ? 1 - run.readyT / (float)Balance.Ready : 1;
            foreach (var e in run.enemies)
            {
                int fr = e.anim > 0 ? (int)Math.Floor(e.anim) % 4 : -1, frame = fr < 0 ? 1 : fr;
                bool boss = e.kind == BodyKind.Boss;
                float bob = fr == 1 || fr == 3 ? -2 : 0;
                float hop = run.readyT > 0 ? -Mathf.Abs(Mathf.Sin(readyProg * Mathf.PI * 2)) * 6 : 0;
                Dot(e.x, e.y + e.r * 0.8, (float)e.r * 0.9f, (float)e.r * 0.3f, Shadow);
                double ey = e.y - (boss ? 6 : 2) + bob * (boss ? 2 : 1) + hop;
                Spr(SpriteSet.Body(e.kind, frame, false), e.x, ey);
                if (e.hitFlash > 0)   // a translucent silhouette over the body, so sustained fire still shows a creature
                {
                    float k = 0.38f + (0.62f - 0.38f) * Mathf.Min(1, (float)e.hitFlash / Balance.HitFlash);
                    Spr(SpriteSet.Body(e.kind, frame, true), e.x, ey, k);
                }
                if (!boss)
                {
                    float barY = (float)(e.y - BarHeight(e.kind)), frac = (float)Math.Max(0, Math.Min(1, e.hp / Math.Max(1e-9, e.maxHp)));
                    Rect(e.x - e.r, barY, e.r * 2, 4, BarBack);
                    Rect(e.x - e.r, barY, e.r * 2 * frac, 4, Green);
                }
                if (e.stun > 0) DrawStunStars(e.x, ey - e.r - 6, (float)e.stun);
                if (e.castT > 0) DrawCastFlash(g, e.x, e.y, e.castT, Balance.CastTime, Hex.C(CastColor(e.kind)));
            }
        }

        void DrawStunStars(double x, double y, float stun)
        {
            float t = Fc * 0.09f, left = stun / Balance.HookHold, a = Mathf.Min(1, left * 3) * 0.95f;
            var c = Hex.C("#ffe9a8", a);
            for (int i = 0; i < 3; i++)
            {
                float ang = t + i * 2.094f, r = 8 + 2 * Mathf.Sin(t * 1.7f + i);
                float sx = Mathf.Round((float)x + Mathf.Cos(ang) * r), sy = Mathf.Round((float)y + Mathf.Sin(ang) * r * 0.6f);
                const float s = 2.6f;
                Rect(sx - s, sy - 1, 2 * s, 2, c);
                Rect(sx - 1, sy - s, 2, 2 * s, c);
            }
        }

        /// <summary>drawCastFlash: a ring that tightens and brightens, and a glow, as the shot comes.</summary>
        void DrawCastFlash(Painter2D g, double x, double y, int t, int total, Color32 col)
        {
            float k = 1 - t / (float)Math.Max(1, total);
            Ring(g, x, y, 5 + 7 * k, 1 + 1.5f * k, QuadBatch.Tint(col, 0.35f + 0.6f * k));
            Glow(x, y, 8 + 5 * k, col, 0.5f * k);
        }

        // ---- shells ----
        static readonly Color32 HostileDefault = Hex.C("#ff4d4d");
        const float BoltDrawR = 6, BoltSizeMax = 1.05f, BoltSizeMin = 0.65f, BoltFadeMin = 0.55f;

        void DrawShells(Painter2D g, RunState run)
        {
            string bolt = Weapons.All[0].Color;
            foreach (var p in run.projectiles)
            {
                var col = string.IsNullOrEmpty(p.color) ? HostileDefault : Hex.C(p.color);
                float alpha = 1;
                if (p.alt)
                {
                    bool hook = p.mode == "hook";
                    Ring(g, p.tx, p.ty, (float)(hook ? Balance.HookAoe : Balance.AltAoe), 2, QuadBatch.Tint(col, 0x48 / 255f));
                    alpha = 0.6f;
                }
                if (p.friendly)
                {
                    // the Bolt shrinks and dims along its falloff: the gun saying what the hit is worth
                    bool shrink = !p.alt && p.color == bolt && p.fNear.HasValue;
                    float st = shrink ? Mathf.Clamp01((float)((Math.Sqrt((p.x - p.ox) * (p.x - p.ox) + (p.y - p.oy) * (p.y - p.oy)) - p.fNear.Value) / Math.Max(1, p.fFar - p.fNear.Value))) : 0;
                    float dr = shrink ? BoltDrawR * (BoltSizeMax + (BoltSizeMin - BoltSizeMax) * st) : (float)p.r;
                    float fade = (shrink ? 1 - (1 - BoltFadeMin) * st : 1) * alpha;
                    float R = Mathf.Max(2, Mathf.Round(dr * 3.2f));
                    Glow(p.x, p.y, R, col, fade);
                    Dot(p.x, p.y, dr, dr, QuadBatch.Tint(col, fade));
                    if (shrink) Ring(g, p.x, p.y, dr + 2.5f, 1, QuadBatch.Tint(col, 0.5f * fade));
                }
                else
                {
                    if (p.heavy)   // the gunner's shell: a glow and a hot core, readable without its colour
                    {
                        Glow(p.x, p.y, Mathf.Round((float)p.r * 4), col, 0.8f);
                        Dot(p.x, p.y, (float)p.r * 0.55f, (float)p.r * 0.55f, Hex.C("#fff4e0"));
                    }
                    Dot(p.x, p.y, (float)p.r, (float)p.r, QuadBatch.Tint(col, alpha));
                }
            }
        }

        // ---- the player ----
        void DrawPlayer(Painter2D g, RunState run)
        {
            var pl = run.player;
            float readyProg = run.readyT > 0 ? 1 - run.readyT / (float)Balance.Ready : 1;
            float a = Mathf.Atan2(Aim.y - (float)pl.y, Aim.x - (float)pl.x);
            int fr = pl.anim > 0 ? (int)Math.Floor(pl.anim) % 4 : -1, frame = fr < 0 ? 1 : fr;
            float bob = fr < 0 ? Mathf.Round(Mathf.Sin(Fc * 0.07f)) : ((fr & 1) == 1 ? -2 : 0);
            float hop = run.readyT > 0 ? -Mathf.Sin(readyProg * Mathf.PI) * 7 : 0;
            bool flick = pl.iframes > 0 && (pl.iframes / Math.Max(1, Balance.Sec(0.067))) % 2 == 0;
            double oy = pl.y + bob + hop;
            var wandCol = Hex.C(Weapons.All[pl.weaponIdx].Color);
            bool behind = Mathf.Sin(a) < -0.35f;

            double lagD = Math.Sqrt((pl.lagX - pl.x) * (pl.lagX - pl.x) + (pl.lagY - pl.y) * (pl.lagY - pl.y));
            if (lagD > 3)   // the trailing hitbox after a blink, drawn faintly where it really is
            {
                float fade = Mathf.Min(1, (float)lagD / Balance.BlinkDist);
                Ring(g, pl.lagX, pl.lagY + Balance.PlayerHitDy, Balance.PlayerHitR, 1, Hex.C("#66d9ff", 0.5f * fade));
                Spr(SpriteSet.Player(frame, false), pl.lagX, pl.lagY + bob + hop - 4, 0.18f * fade);
            }
            Dot(pl.x, pl.y + 13, 11, 4, Shadow);
            float alpha = flick ? 0.35f : 1;
            float glowExtra = run.readyT > 0 ? 0.5f * readyProg : 0;
            Vector2 tip = default;
            if (behind) tip = DrawWand(pl.x, oy, a, wandCol, glowExtra, alpha);
            Spr(SpriteSet.Player(frame, false), pl.x, oy - 4, alpha);
            if (!behind) tip = DrawWand(pl.x, oy, a, wandCol, glowExtra, alpha);
            if (pl.muzzleTimer > 0)
            {
                var m = A[SpriteSet.Muzzle];
                _q.Sprite(A.Texture, m, tip.x - 11 + Origin.x, tip.y - 11 + Origin.y, m.W, m.H, QuadBatch.Alpha(0.85f * pl.muzzleTimer / Weapons.MuzzleTicks));
            }
        }

        static readonly (int x, int y, int w, int h, string c)[] WandParts =
        {
            (-7, -3, 36, 6, "#1a1008"), (-5, -2, 27, 4, "#8a5a2b"), (-5, -2, 27, 1, "#b98040"), (-5, 1, 27, 1, "#5a3618"),
            (-5, -2, 9, 4, "#3b2a55"), (-3, -2, 1, 4, "#6a4aa0"), (1, -2, 1, 4, "#6a4aa0"), (21, -3, 3, 6, "#e8c04a"), (24, -4, 8, 8, "#1a1008"),
        };
        static Color32[] _wandCols;

        /// <summary>drawWand: the staff turned to the aim, its gem in the gun's colour, a glow and two motes at the tip.</summary>
        Vector2 DrawWand(double px, double py, float a, Color32 color, float extra, float alpha)
        {
            if (_wandCols == null) { _wandCols = new Color32[WandParts.Length]; for (int i = 0; i < WandParts.Length; i++) _wandCols[i] = Hex.C(WandParts[i].c); }
            int side = Mathf.Cos(a) >= 0 ? 1 : -1;
            float hx = (float)px + side * 11, hy = (float)py + 5;
            var pivot = ToPanel(hx, hy);
            var r = A[SpriteSet.White];
            var white = new Region(1, 1, (r.U0 + r.U1) / 2, (r.V0 + r.V1) / 2, (r.U0 + r.U1) / 2, (r.V0 + r.V1) / 2);
            for (int i = 0; i < WandParts.Length; i++)
            {
                var (x, y, w, h, _) = WandParts[i];
                _q.Rotated(A.Texture, white, pivot, x, y, w, h, a, QuadBatch.Tint(_wandCols[i], alpha));
            }
            _q.Rotated(A.Texture, white, pivot, 25, -3, 6, 6, a, QuadBatch.Tint(color, alpha));
            _q.Rotated(A.Texture, white, pivot, 26, -2, 2, 2, a, QuadBatch.Alpha(alpha));
            float tx = hx + Mathf.Cos(a) * 29, ty = hy + Mathf.Sin(a) * 29;
            Glow(tx, ty, 14, color, Mathf.Min(1, 0.35f + 0.12f * Mathf.Sin(Fc * 0.15f) + extra));
            float t = Fc * 0.08f;
            Rect(Mathf.Round(tx + Mathf.Cos(t) * 9) - 1, Mathf.Round(ty + Mathf.Sin(t * 1.3f) * 9) - 1, 2, 2, new Color32(255, 255, 255, 255));
            Rect(Mathf.Round(tx + Mathf.Cos(t + 3) * 7) - 1, Mathf.Round(ty + Mathf.Sin(t * 0.9f + 2) * 7) - 1, 2, 2, new Color32(255, 255, 255, 255));
            return new Vector2(tx, ty);
        }
    }
}
