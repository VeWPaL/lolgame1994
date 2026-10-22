using System;
using UnityEngine;
using UnityEngine.UIElements;
using Depths.Unity.Art;

namespace Depths.Unity
{
    /// <summary>The room and everything in it, drawn the way src/70-view.js drawRoom draws it: the baked floor and masonry... [h:WorldView-1]</summary>
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
        /// <summary>The clock's leftover fraction of a tick: bodies, shells and the player draw that far from the last
        /// tick's place to this one's (<see cref="Interp"/>). 1 draws the latest tick.</summary>
        public float Alpha { get; set; } = 1;
        /// <summary>A cosmetic clock at 60 a second that stops with the game (the JS's frameCount / SPEEDUP).</summary>
        public float Fc { get; private set; }

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
            if (run != null)
            {
                if (!ReferenceEquals(run, _warmRun) || run.floor != _warmFloor) Warm(run);
                Fx.Update(run, dt);
            }
            MarkDirtyRepaint();
        }

        RunState _warmRun;
        int _warmFloor;

        /// <summary>Bakes the atlas and this floor's room textures now: on a new run or a descent, which happen
        /// behind a fade, rather than inside the first paint of a room type.</summary>
        public void Warm(RunState run)
        {
            _ = SpriteSet.WhiteTexel;   // builds the atlas and resolves every region
            _art.Sync(run.rootSeed, run.floor);
            _art.Warm();
            _warmRun = run; _warmFloor = run.floor;
        }

        // ---- drawing helpers, world coordinates ----
        Atlas A => SpriteSet.Atlas;
        void Spr(in Region r, double cx, double cy, float alpha = 1) =>
            _q.Centred(A.Texture, r, (float)cx + Origin.x, (float)cy + Origin.y, QuadBatch.Alpha(alpha));
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
        void Ring(double cx, double cy, float r, float width, Color32 c) => _q.Ring((float)cx + Origin.x, (float)cy + Origin.y, r, width, c);

        static readonly Color32 Gold = Hex.C("#ffd23d"), Silver = Hex.C("#d8dee9"), Green = Hex.C("#5ee27a"), Shut = Hex.C("#c93b3b"),
            BarBack = new Color32(0, 0, 0, 255), Shadow = Hex.Rgba(0, 0, 0, 0.3f), Ink = Hex.C("#14151a"), Exit = Hex.C("#f4e6c8");

        void Paint(MeshGenerationContext ctx)
        {
            var run = _run();
            if (run == null || run.dungeon == null) return;
            _q.Begin(ctx);
            var room = run.CurrentRoom;
            _art.Sync(run.rootSeed, run.floor);   // a no-op once Step has warmed this floor
            var pal = _art.Palette;
            var type = room?.Type ?? RoomKind.Normal;

            // floor, the hook's ground spell, the walls
            var fl = _art.Floor(type);
            _q.Sprite(fl, new Region(fl.width, fl.height, 0, 0, 1, 1), Balance.RoomLeft + Origin.x, Balance.RoomTop + Origin.y, fl.width, fl.height, QuadBatch.Alpha(1));
            DrawHookFields(run);
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
                Color32 c = f.Mine ? Sprites.BlinkBlue : new Color32(255, 255, 255, 255);
                Glow(f.X, f.Y, 14, c, 0.35f * f.Life / f.Max);
            }

            DrawRides(run);
            DrawLungeLines(run);
            DrawPickups(run);
            DrawBodies(run);
            DrawShells(run);
            for (int i = 0; i < Fx.Count; i++)
            {
                ref var f = ref Fx[i];
                if (!f.Ring) continue;
                float t = 1 - f.Life / f.Max, rad = f.R * (f.Dir < 0 ? 1 - t : t);
                Ring(f.X, f.Y, Mathf.Max(1, rad), 3, QuadBatch.Tint(f.Color, 0.7f * (1 - t * 0.55f)));
            }
            DrawPlayer(run);
            if (run.roomFade > 0.01)
                Rect(Balance.RoomLeft - wt, Balance.RoomTop - wt, Balance.RoomRight - Balance.RoomLeft + wt * 2, Balance.RoomBottom - Balance.RoomTop + wt * 2,
                     Hex.Rgba(0, 0, 0, Mathf.Clamp01((float)run.roomFade)));
            _q.Flush();
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
            if (room.Secret.HasValue)   // a fake wall: a crack in the stone is the only tell, and the blast opens it
            {
                var s = room.Secret.Value;
                var (cx, cy) = LockPoint(s);   // the middle of the wall band on that side
                Spr(A[s == Dir.N || s == Dir.S ? SpriteSet.CrackH : SpriteSet.CrackV], cx, cy);
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

        void DrawHookFields(RunState run)
        {
            var col = Hex.Of(Sprites.HookColor);
            foreach (var f in run.hookFields)
            {
                float left = f.life / (float)Math.Max(1, f.max), fade = left < 0.34f ? left / 0.34f : 1;
                Dot(f.x, f.y, (float)f.r, (float)f.r, QuadBatch.Tint(col, 0.13f * fade));
                Ring(f.x, f.y, (float)f.r * (0.55f + 0.45f * (1 - left)), 2, QuadBatch.Tint(col, 0.5f * fade));
                Ring(f.x, f.y, (float)f.r * 0.55f, 1, QuadBatch.Tint(col, 0.3f * fade));
                float t = Fc * 0.05f;
                for (int i = 0; i < 7; i++)   // the inward spiral: each mote on its own radius and phase
                {
                    float spin = t * (1.5f + (i % 3) * 0.35f) + i * 0.9f;
                    float rad = (float)f.r * (0.5f + 0.48f * (1 - Frac(t * 0.22f + i * 0.14f)));
                    float mx = (float)f.x + Mathf.Cos(spin) * rad, my = (float)f.y + Mathf.Sin(spin) * rad * 0.86f;
                    Rect(Mathf.Round(mx) - 1, Mathf.Round(my) - 1, 2, 2, QuadBatch.Tint(Hex.Of("#e8fffb"), (0.75f - 0.35f * ((i * 7) % 5) / 5f) * fade));
                }
            }
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        // ---- the committed line: a lunger's windup and its lunge ----
        void DrawLungeLines(RunState run)
        {
            var col = Hex.Of("#ff9a6b");
            foreach (var e in run.enemies)
            {
                if (e.lungeState != "wind" && e.lungeState != "lunge") continue;
                bool wind = e.lungeState == "wind";
                float grow = wind ? 1 - e.lungeT / (float)Balance.LungeWindup : 1;
                double len = e.lungeLen > 0 ? e.lungeLen : Balance.LungeFloor;
                Interp.At(run, e, Alpha, out var bx, out var by);
                double ex = bx, ey = by, tx = bx + e.lungeDx * len, ty = by + e.lungeDy * len;
                if (!wind)
                {
                    if (!Fx.LungeFrom(e, out float fx, out float fy)) continue;
                    ex = fx; ey = fy; tx = bx; ty = by;
                }
                Dashes(ex, ey, tx, ty, 1 + 2 * grow, QuadBatch.Tint(col, 0.20f + 0.55f * grow));
                Ring(tx, ty, 5 + 3 * grow, 1 + 2 * grow, QuadBatch.Tint(col, 0.30f + 0.50f * grow));
            }
            // the Gatekeeper's charge: the whole locked line from the tell's first tick, the Lunger's language, wider for its body
            foreach (var e in run.enemies)
            {
                if (e.kind != BodyKind.Gatekeeper || (e.move != "tell" && e.move != "charge")) continue;
                bool wind = e.move == "tell";
                float grow = wind ? 1 - e.moveT / (float)System.Math.Max(1, e.fbTellMax) : 1;
                Interp.At(run, e, Alpha, out var bx, out var by);
                double left = e.fbLen - (wind ? 0 : e.fbGone), tx = bx + e.fbDx * left, ty = by + e.fbDy * left;
                Dashes(bx, by, tx, ty, 2 + 3 * grow, QuadBatch.Tint(col, 0.35f + 0.55f * grow));
                Ring(tx, ty, 8 + 5 * grow, 2 + 2 * grow, QuadBatch.Tint(col, 0.40f + 0.50f * grow));
            }
            // a Brunch winding up to rush: the same committed line, growing from it toward the player
            var pl = run.player;
            foreach (var e in run.enemies)
            {
                if (e.rushWind <= 0) continue;
                float grow = 1 - e.rushWind / (float)Balance.BrunchRushWind;
                Interp.At(run, e, Alpha, out var bx, out var by);
                double dx = pl.x - bx, dy = pl.y - by, d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1) continue;
                double len = Math.Min(d, RushLine) * (0.35 + 0.65 * grow);
                Dashes(bx, by, bx + dx / d * len, by + dy / d * len, 1 + 2 * grow, QuadBatch.Tint(col, 0.25f + 0.6f * grow));
            }
        }

        const float RushLine = 70;   // px of the Brunch's wind-up line at full growth
        static float RushShiver(Enemy e) => 0.5f + 1.5f * (1 - e.rushWind / (float)Balance.BrunchRushWind);

        void Dashes(double x0, double y0, double x1, double y1, float width, Color32 c)
        {
            float dx = (float)(x1 - x0), dy = (float)(y1 - y0), len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            float ang = Mathf.Atan2(dy, dx);
            var white = SpriteSet.WhiteTexel;
            for (float s = 0; s < len; s += 10)   // setLineDash([5, 5])
                _q.Rotated(A.Texture, white, ToPanel(x0, y0), s, -width / 2, Mathf.Min(5, len - s), width, ang, c);
        }

        // ---- loot ----
        void DrawPickups(RunState run)
        {
            foreach (var pk in run.pickups)
            {
                float bob = Mathf.Sin(Fc * 0.08f + (float)pk.x) * 2;
                switch (pk.kind)
                {
                    case "exit": DrawExit(pk.x, pk.y); break;
                    case "weapon": Spr(SpriteSet.GunR(pk.w), pk.x, pk.y + bob); break;
                    case "hook": case "blast": Spr(SpriteSet.AltR(pk.kind == "hook"), pk.x, pk.y + bob); break;
                    case "item":   // its picture, as in the kit; the lettered tile for an item without one
                        Spr(SpriteSet.ItemIconR(pk.id, 2, out var pic) ? pic : SpriteSet.ItemR(pk.id, 26), pk.x, pk.y + Mathf.Sin(Fc * 0.09f + (float)pk.x) * 2); break;
                    case "key": case "goldkey": Spr(SpriteSet.KeyR(pk.kind == "goldkey", 3, false), pk.x, pk.y + Mathf.Sin(Fc * 0.1f) * 2); break;
                    case "heart": Spr(SpriteSet.HeartR(Sprites.HeartScheme.Red, 2, 1), pk.x, pk.y); break;
                    case "halfheart": Spr(SpriteSet.HeartR(Sprites.HeartScheme.Red, 2, 0.5f), pk.x, pk.y); break;
                    case "armor": Spr(SpriteSet.HeartR(Sprites.HeartScheme.Gray, 2, 1), pk.x, pk.y); break;
                    case "halfarmor": Spr(SpriteSet.HeartR(Sprites.HeartScheme.Gray, 2, 0.5f), pk.x, pk.y); break;
                    default: Dot(pk.x, pk.y, 6, 6, Hex.Of("#50dcbe")); break;
                }
            }
        }

        /// <summary>drawExitPortal: three rings breathing outward, a soft disc, a floor shadow, an up-arrow.</summary>
        void DrawExit(double cx, double cy)
        {
            float t = Fc * 0.05f;
            for (int i = 0; i < 3; i++)
            {
                float p = Frac(t * 0.5f + i / 3f);
                Ring(cx, cy, 8 + 30 * p, 2, QuadBatch.Tint(Exit, 0.5f * (1 - p)));
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
        static float BarHeight(BodyKind k) => (float)Bodies.Of(k).Bar;
        static string CastColor(BodyKind k) => k switch
        {
            BodyKind.Gunner => "#ffb03a", BodyKind.Splitter => Middle.SplitColor, BodyKind.Mortar => Middle.LobColor,
            BodyKind.Egg => Middle.EggColor, BodyKind.Gatekeeper => FloorBoss.ShellColor, _ => "#ff4d4d",
        };
        static readonly Color32 Dirt = Hex.C("#3a2a1c"), DirtLit = Hex.C("#7a5a3a"), Crack = Hex.C("#ff8a5a");

        /// <summary>The Burrower when it is not a body you can hit: sinking, a mound running under the
        /// floor, then the crack at its mark that is the whole warning.</summary>
        bool DrawBurrow(Enemy e, int frame, double ex, double ey)
        {
            if (!e.buried) return false;   // walking or digging it is a body you can hit: drawn as one (DrawBodies)
            if (e.move == "leap")   // in the air: its landing marked on the floor (filling as it comes), the body arcing over
            {
                float t = 1 - Mathf.Clamp01(e.moveT / (float)Middle.BurrowLeap);
                Dot(e.markX, e.markY, (float)Middle.BurrowHitR, (float)Middle.BurrowHitR * 0.55f, QuadBatch.Tint(Crack, 0.12f + 0.25f * t));
                Ring(e.markX, e.markY, (float)Middle.BurrowHitR * (1.3f - 0.3f * t), 2, QuadBatch.Tint(Crack, 0.45f + 0.5f * t));
                Dot(ex, ey + e.r * 0.8, (float)e.r * 0.9f, (float)e.r * 0.3f, Shadow);
                double arc = ey - 2 - Mathf.Sin(t * Mathf.PI) * 56;   // -2: where DrawBodies stands a body, so no pop at either end
                Spr(SpriteSet.BodyR(e.kind, frame, false), ex, arc);
                return true;
            }
            if (e.move == "rise" || e.move == "crack")   // breaking the floor: cracks reaching out as it comes
            {
                float full = e.move == "crack" ? Middle.BurrowCrack : Middle.BurrowRise;
                float p = 1 - Mathf.Clamp01(e.moveT / full), pulse = 0.5f + 0.5f * Mathf.Sin(Fc * 0.5f);
                Dot(ex, ey, (float)Middle.BurrowHitR, (float)Middle.BurrowHitR * 0.5f, QuadBatch.Tint(Dirt, 0.35f + 0.4f * p));
                Ring(ex, ey, (float)Middle.BurrowHitR, 2, QuadBatch.Tint(Crack, 0.35f + 0.5f * p * pulse + 0.15f));
                for (int i = 0; i < 6; i++)   // cracks reach out further as it comes
                {
                    float a = i * 1.047f + 0.3f, len = 6 + 18 * p;
                    for (float s = 4; s < len; s += 2) Rect(ex + Mathf.Cos(a) * s - 1, ey + Mathf.Sin(a) * s * 0.55f - 1, 2, 2, QuadBatch.Tint(Crack, 0.5f + 0.4f * p));
                }
                return true;
            }
            // under: a low mound of earth heaving along the floor
            Dot(ex, ey + 4, 13, 6, QuadBatch.Tint(Dirt, 0.85f));
            for (int i = 0; i < 4; i++)
                Rect(ex - 8 + i * 5, ey + 1 + Mathf.Sin(Fc * 0.4f + i) * 2, 3, 2, QuadBatch.Tint(DirtLit, 0.9f));
            return true;
        }

        // ---- the eggs' rides: a rail's track under everything, an orbit's path faintly round its host ----
        static readonly Color32 RailIron = Hex.C("#c9b48e"), RailSleeper = Hex.C("#6e5539"), RailShade = Hex.C("#000000", 0.35f), OrbitPath = Hex.C("#b6ff5c");

        void DrawRides(RunState run)
        {
            var white = SpriteSet.WhiteTexel;
            foreach (var e in run.enemies)
            {
                if (e.kind != BodyKind.Egg) continue;
                if (e.eggForm == EggForm.Rail && e.rail != null)
                {
                    var pts = e.rail;
                    int n = pts.Length / 2, segs = e.railLoop ? n : n - 1;
                    float carry = 0;   // sleepers every 14 px along the whole track, not restarting at each joint
                    for (int i = 0; i < segs; i++)
                    {
                        int j = (i + 1) % n;
                        float x0 = (float)pts[2 * i], y0 = (float)pts[2 * i + 1], dx = (float)pts[2 * j] - x0, dy = (float)pts[2 * j + 1] - y0;
                        float len = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                        if (len < 0.01f) continue;
                        var at = ToPanel(x0, y0);
                        _q.Rotated(A.Texture, white, at, 0, -8, len + 1, 16, ang, RailShade);   // the bed, so the track reads on any floor
                        for (float s = 14 - carry; s < len; s += 14) _q.Rotated(A.Texture, white, at, s - 2, -8, 4, 16, ang, RailSleeper);
                        carry = (carry + len) % 14;
                        _q.Rotated(A.Texture, white, at, 0, -5, len + 1, 2, ang, RailIron);
                        _q.Rotated(A.Texture, white, at, 0, 3, len + 1, 2, ang, RailIron);
                    }
                }
                else if (e.eggForm == EggForm.Orbit && e.eggHost != null)
                {
                    Interp.At(run, e.eggHost, Alpha, out var hx, out var hy);
                    for (int i = 0; i < 24; i += 2)   // a dotted circle: where the egg will be
                    {
                        float a = i * Mathf.PI * 2 / 24 + Fc * 0.01f;
                        Rect(hx + Mathf.Cos(a) * e.eggR - 1, hy + Mathf.Sin(a) * e.eggR - 1, 2, 2, QuadBatch.Tint(OrbitPath, 0.22f));
                    }
                }
            }
        }

        void DrawBodies(RunState run)
        {
            float readyProg = run.readyT > 0 ? 1 - run.readyT / (float)Balance.Ready : 1;
            foreach (var e in run.enemies)
            {
                int fr = e.anim > 0 ? (int)Math.Floor(e.anim) % 4 : -1, frame = fr < 0 ? 1 : fr;
                bool boss = Bodies.IsBoss(e.kind);
                float bob = fr == 1 || fr == 3 ? -2 : 0;
                float hop = run.readyT > 0 ? -Mathf.Abs(Mathf.Sin(readyProg * Mathf.PI * 2)) * 6 : 0;
                Interp.At(run, e, Alpha, out var bx, out var by);   // between the last two ticks: smooth at any frame rate
                if (e.rushWind > 0) bx += RushShiver(e) * Mathf.Sin(Fc * 2.3f);   // a Brunch about to rush trembles where it stands
                if (e.kind == BodyKind.Burrower && DrawBurrow(e, frame, bx, by)) continue;
                // a digging Burrower sinks and fades into the floor, still hittable, so still flashing
                float sink = e.kind == BodyKind.Burrower && e.move == "dig" ? 1 - Mathf.Clamp01(e.moveT / (float)Middle.BurrowDig) : 0;
                if (e.elite)   // a spike room's elite: a slow gold glow, and a gold ring at its feet
                {
                    float beat = 0.5f + 0.5f * Mathf.Sin(Fc * 0.08f);
                    Glow(bx, by, (float)e.r + 18 + 4 * beat, Gold, 0.55f + 0.25f * beat);
                    Ring(bx, by - 2, (float)e.r + 5 + beat, 1.5f, QuadBatch.Tint(Gold, 0.45f + 0.35f * beat));
                    Ring(bx, by + e.r * 0.8, (float)e.r + 2, 2, QuadBatch.Tint(Gold, 0.9f));
                }
                Dot(bx, by + e.r * 0.8, (float)e.r * 0.9f, (float)e.r * 0.3f, Shadow);
                double ey = by - (boss ? 6 : 2) + bob * (boss ? 2 : 1) + hop + sink * 8;
                if (e.dazed && e.stun > 0) DrawTumble(e, frame, bx, ey);   // jarred by its own landing: tipped over, rocking back up
                else Spr(SpriteSet.BodyR(e.kind, frame, false), bx, ey, 1 - 0.75f * sink);
                if (e.hitFlash > 0)   // a translucent silhouette over the body, so sustained fire still shows a creature
                {
                    float k = 0.38f + (0.62f - 0.38f) * Mathf.Min(1, (float)e.hitFlash / Balance.HitFlash);
                    Spr(SpriteSet.BodyR(e.kind, frame, true), bx, ey, k);
                }
                if (sink > 0)
                    for (int i = 0; i < 5; i++)   // dust thrown up as it digs
                    {
                        float a = Fc * 0.2f + i * 1.26f, r = (float)e.r + 2 + 4 * sink;
                        Rect(bx + Mathf.Cos(a) * r - 1, by + 8 + Mathf.Sin(a) * r * 0.4f - 1, 3, 3, QuadBatch.Tint(DirtLit, 0.8f));
                    }
                if (!boss)
                {
                    float barY = (float)(by - BarHeight(e.kind)), frac = (float)Math.Max(0, Math.Min(1, e.hp / Math.Max(1e-9, e.maxHp)));
                    Rect(bx - e.r, barY, e.r * 2, 4, BarBack);
                    Rect(bx - e.r, barY, e.r * 2 * frac, 4, e.elite ? Gold : Green);
                }
                if (e.stun > 0) DrawStunStars(bx, ey - e.r - 6, (float)e.stun);
                if (e.castT > 0) DrawCastFlash(bx, by, e.castT, e.kind == BodyKind.Gatekeeper ? e.fbTellMax : Middle.CastTime(e.kind), Hex.Of(CastColor(e.kind)));
            }
        }

        // a dazed body tips toward the way it fell and rocks back upright as the daze runs out
        void DrawTumble(Enemy e, int frame, double x, double y)
        {
            float k = Mathf.Clamp01((float)e.stun / Middle.BurrowDaze);
            float ang = e.tumbleDir * (1.1f * k * k + 0.25f * k * Mathf.Sin(Fc * 0.9f));
            var r = SpriteSet.BodyR(e.kind, frame, false);
            _q.Rotated(A.Texture, r, ToPanel(x, y + r.H * 0.3f), -r.W / 2f, -r.H * 0.8f, r.W, r.H, ang, QuadBatch.Alpha(1));
        }

        void DrawStunStars(double x, double y, float stun)
        {
            float t = Fc * 0.09f, left = stun / Balance.HookHold, a = Mathf.Min(1, left * 3) * 0.95f;
            var c = QuadBatch.Tint(Hex.Of("#ffe9a8"), a);
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
        void DrawCastFlash(double x, double y, int t, int total, Color32 col)
        {
            float k = 1 - t / (float)Math.Max(1, total);
            Ring(x, y, 5 + 7 * k, 1 + 1.5f * k, QuadBatch.Tint(col, 0.35f + 0.6f * k));
            Glow(x, y, 8 + 5 * k, col, 0.5f * k);
        }

        // ---- shells ----
        static readonly Color32 HostileDefault = Hex.C("#ff4d4d");
        const float BoltDrawR = 6, BoltSizeMax = 1.05f, BoltSizeMin = 0.65f, BoltFadeMin = 0.55f;

        void DrawShells(RunState run)
        {
            string bolt = Weapons.All[0].Color;
            foreach (var p in run.projectiles)
            {
                var col = string.IsNullOrEmpty(p.color) ? HostileDefault : Hex.Of(p.color);
                Interp.At(run, p, Alpha, out var sx, out var sy);
                float alpha = 1;
                if (p.lob)   // the mortar: the mark it will land on fills as it comes, the shell arcs over
                {
                    float t = p.flight / (float)Math.Max(1, p.flightMax);
                    Dot(p.tx, p.ty, (float)p.blastR, (float)p.blastR * 0.55f, QuadBatch.Tint(col, 0.10f + 0.22f * t));
                    Ring(p.tx, p.ty, (float)p.blastR * (1.25f - 0.25f * t), 2, QuadBatch.Tint(col, 0.45f + 0.5f * t));
                    Dot(sx, sy, (float)p.r, (float)p.r * 0.45f, Shadow);
                    double arc = sy - Mathf.Sin(t * Mathf.PI) * 70;
                    Glow(sx, arc, (float)p.r * 3, col, 0.7f * ShellGlow);
                    Shell(sx, arc, (float)p.r, col, 1);
                    continue;
                }
                if (p.alt)
                {
                    bool hook = p.mode == "hook";
                    Ring(p.tx, p.ty, (float)(hook ? Balance.HookAoe : Balance.AltAoe), 2, QuadBatch.Tint(col, 0x48 / 255f));
                    alpha = 0.6f;
                }
                if (p.friendly)
                {
                    // the Bolt shrinks and dims along its falloff: the gun saying what the hit is worth
                    bool shrink = !p.alt && p.color == bolt && p.fNear.HasValue;
                    float st = shrink ? Mathf.Clamp01((float)((Math.Sqrt((sx - p.ox) * (sx - p.ox) + (sy - p.oy) * (sy - p.oy)) - p.fNear.Value) / Math.Max(1, p.fFar - p.fNear.Value))) : 0;
                    float dr = shrink ? BoltDrawR * (BoltSizeMax + (BoltSizeMin - BoltSizeMax) * st) : (float)p.r;
                    float fade = (shrink ? 1 - (1 - BoltFadeMin) * st : 1) * alpha;
                    float R = Mathf.Max(2, Mathf.Round(dr * 3.2f));
                    Glow(sx, sy, R, col, fade * ShellGlow);
                    Shell(sx, sy, dr, QuadBatch.Tint(col, fade), fade);
                    if (shrink) Ring(sx, sy, dr + 2.5f, 1, QuadBatch.Tint(col, 0.5f * fade));
                }
                else
                {
                    bool hot = p.heavy || p.splitAt > 0;   // the gunner's shell (and the splitter's): a glow and a hot core
                    if (hot) Glow(sx, sy, Mathf.Round((float)p.r * 4), col, 0.8f * ShellGlow);
                    Shell(sx, sy, (float)p.r, QuadBatch.Tint(col, alpha), alpha);
                    if (hot) Dot(sx, sy, (float)p.r * 0.55f, (float)p.r * 0.55f, HotCore);
                }
            }
        }

        public const float ShellGlow = 0.65f;   // every shell's light, softened so a cluster reads as shells, not one ball
        static readonly Color32 ShellRim = new Color32(0, 0, 0, 230), HotCore = Hex.C("#fff4e0");

        // a shell: its disc inside a one-pixel black rim, so shells that overlap stay countable
        void Shell(double x, double y, float r, Color32 c, float alpha)
        {
            Dot(x, y, r + 1.25f, r + 1.25f, QuadBatch.Tint(ShellRim, alpha * alpha));   // a fading shell's rim fades faster: no dark smudge
            Dot(x, y, r, r, c);
        }

        // ---- the player ----
        void DrawPlayer(RunState run)
        {
            var pl = run.player;
            Interp.At(run, pl, Alpha, out var plx, out var ply);
            float readyProg = run.readyT > 0 ? 1 - run.readyT / (float)Balance.Ready : 1;
            float a = Mathf.Atan2(Aim.y - (float)ply, Aim.x - (float)plx);
            int fr = pl.anim > 0 ? (int)Math.Floor(pl.anim) % 4 : -1, frame = fr < 0 ? 1 : fr;
            float bob = fr < 0 ? Mathf.Round(Mathf.Sin(Fc * 0.07f)) : ((fr & 1) == 1 ? -2 : 0);
            float hop = run.readyT > 0 ? -Mathf.Sin(readyProg * Mathf.PI) * 7 : 0;
            bool flick = pl.iframes > 0 && (pl.iframes / Math.Max(1, Balance.Sec(0.067))) % 2 == 0;
            double oy = ply + bob + hop;
            var wandCol = Hex.Of(Weapons.All[pl.weaponIdx].Color);
            bool behind = Mathf.Sin(a) < -0.35f;

            double lagD = Math.Sqrt((pl.lagX - plx) * (pl.lagX - plx) + (pl.lagY - ply) * (pl.lagY - ply));
            if (lagD > 3)   // the trailing hitbox after a blink, drawn faintly where it really is
            {
                float fade = Mathf.Min(1, (float)lagD / Balance.BlinkDist);
                // the lag is not snapped; carried by the player's own blend it moves with the drawn body, not a tick behind
                double gx = pl.lagX + (plx - pl.x), gy = pl.lagY + (ply - pl.y);
                Ring(gx, gy + Balance.PlayerHitDy, Balance.PlayerHitR, 1, QuadBatch.Tint(Sprites.BlinkBlue, 0.5f * fade));
                Spr(SpriteSet.PlayerR(frame, false), gx, gy + bob + hop - 4, 0.18f * fade);
            }
            DrawMomentumTrail(pl, plx, ply, frame);
            Dot(plx, ply + 13, 11, 4, Shadow);
            float alpha = flick ? 0.35f : 1;
            float glowExtra = run.readyT > 0 ? 0.5f * readyProg : 0;
            Vector2 tip = default;
            if (behind) tip = DrawWand(plx, oy, a, wandCol, glowExtra, alpha);
            Spr(SpriteSet.PlayerR(frame, false), plx, oy - 4, alpha);
            if (!behind) tip = DrawWand(plx, oy, a, wandCol, glowExtra, alpha);
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
        // ---- momentum: afterimages in the blink's blue that lengthen and brighten as the meter fills ----
        public const float TrailFrom = 0.2f;   // below this much momentum, no trail
        const int TrailLen = 5, TrailGhosts = 3;   // the ring keeps five samples; at most three are drawn
        const float TrailEvery = 2.5f;          // cosmetic ticks between samples (60 a second): frame-rate free

        readonly Vector2[] _trail = new Vector2[TrailLen];
        int _trailN;
        float _trailAt = -1;

        /// <summary>How many afterimages a meter shows, and how bright the nearest is.</summary>
        public static (int ghosts, float alpha) TrailFor(double momentum)
        {
            float m = Mathf.Clamp01((float)momentum);
            if (m < TrailFrom) return (0, 0);
            float k = (m - TrailFrom) / (1 - TrailFrom);
            return (1 + Mathf.RoundToInt(k * (TrailGhosts - 1)), 0.08f + 0.16f * k);
        }

        void DrawMomentumTrail(Player pl, double x, double y, int frame)
        {
            if (Fc - _trailAt >= TrailEvery || _trailAt < 0 || Fc < _trailAt)
            {
                _trailAt = Fc;
                // a door or a blink is a jump: the trail starts again rather than drawing a ghost across the room
                if (_trailN > 0 && (_trail[0] - new Vector2((float)x, (float)y)).sqrMagnitude > Interp.MaxStep * Interp.MaxStep * 4) _trailN = 0;
                for (int i = TrailLen - 1; i > 0; i--) _trail[i] = _trail[i - 1];
                _trail[0] = new Vector2((float)x, (float)y);
                _trailN = Mathf.Min(TrailLen, _trailN + 1);
            }
            var (ghosts, a) = TrailFor(pl.momentum);
            var r = SpriteSet.PlayerR(frame, true);
            for (int i = Mathf.Min(ghosts, _trailN - 1); i >= 1; i--)   // [0] is about where the player stands
            {
                var g = _trail[i];
                if ((g - new Vector2((float)x, (float)y)).sqrMagnitude < 9) continue;   // standing still: nothing trails
                _q.Centred(A.Texture, r, g.x + Origin.x, g.y - 4 + Origin.y, QuadBatch.Tint(Sprites.BlinkBlue, a * (1 - (i - 1) / (float)TrailLen)));
            }
        }

        Vector2 DrawWand(double px, double py, float a, Color32 color, float extra, float alpha)
        {
            if (_wandCols == null) { _wandCols = new Color32[WandParts.Length]; for (int i = 0; i < WandParts.Length; i++) _wandCols[i] = Hex.C(WandParts[i].c); }
            int side = Mathf.Cos(a) >= 0 ? 1 : -1;
            float hx = (float)px + side * 11, hy = (float)py + 5;
            var pivot = ToPanel(hx, hy);
            var white = SpriteSet.WhiteTexel;
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
