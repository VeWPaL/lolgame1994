using UnityEngine;

namespace Depths.Unity.Hud
{
    /// <summary>
    /// Every place the ledger HUD draws, in panel units at the 1280x720 reference. One table, read by
    /// the HUD, the overlays, the layout tests and the screenshot pixel check, so none of them can drift.
    /// The room keeps the middle; two lecterns stand in the side margins, each holding a ledger page:
    /// the body on the left, the map and the kit on the right.
    /// </summary>
    public static class HudLayout
    {
        /// <summary>The room with its walls, from WorldView.Origin (the HUD must never draw over it).</summary>
        public static Rect Room => new Rect(
            Balance.RoomLeft - Art.Materials.WallThick + WorldView.Origin.x, Balance.RoomTop - Art.Materials.WallThick + WorldView.Origin.y,
            Balance.RoomRight - Balance.RoomLeft + Art.Materials.WallThick * 2, Balance.RoomBottom - Balance.RoomTop + Art.Materials.WallThick * 2);

        public const float Screen_W = 1280, Screen_H = 720;

        // ---- the two lecterns: a plate as tall as the room, a ledge, a post to the floor ----
        public const int LecternW = 246, LecternH = 482, LecternTop = 119, PaperPad = 10;
        public const int LeftX = 14, RightX = 1280 - 14 - LecternW;
        public static Rect LeftLectern => new Rect(LeftX, LecternTop, LecternW, LecternH);
        public static Rect RightLectern => new Rect(RightX, LecternTop, LecternW, LecternH);
        /// <summary>The ledge under each plate, a little wider than it; the post runs from it off the screen.</summary>
        public const int LedgeOut = 6, LedgeH = 14, PostW = 44;
        /// <summary>The whole stand image (plate + ledge + post), as baked and placed.</summary>
        public static Rect Stand(bool left) => new Rect((left ? LeftX : RightX) - LedgeOut, LecternTop - 8, LecternW + LedgeOut * 2, 720 - (LecternTop - 8));

        // ---- the left page: the body ----
        /// <summary>Row labels sit left of the red margin; values are written right of it.</summary>
        public const int LabelX = LeftX + 14, MarginX = LeftX + 86, ValueX = LeftX + 93;
        public const int HeartPx = 3, HeartW = 7 * HeartPx + 4, HeartH = 6 * HeartPx + 4, HeartStep = 23, HeartsPerLine = 6;
        public const int HeaderY = LecternTop + 14;   // the struck title's top
        public const int BloodY = LecternTop + 56, BloodLineStep = 26, BloodLines = 2;
        public const int MendingY = BloodY + BloodLineStep * BloodLines + 6;
        public const int MailY = MendingY + 32;
        public const int KeysY = MailY + 32;
        public const int BlinkY = KeysY + 32;
        public const int MomentumY = BlinkY + 32;
        public const int DepthY = MomentumY + 34;
        public const int SlainY = DepthY + 92;
        public const int SeedY = SlainY + 32;
        /// <summary>Row tops in reading order, with the ruled line under each (the bake rules them).</summary>
        public static readonly int[] LeftRows = { BloodY, MendingY, MailY, KeysY, BlinkY, MomentumY, DepthY, SlainY, SeedY };
        public static readonly string[] LeftLabels = { "Blood", "Mending", "Mail", "Keys", "Blink", "Momentum", "Depth", "Slain", "Seed" };
        public static int RowBottom(int i) => i + 1 < LeftRows.Length ? LeftRows[i + 1] - 2 : SeedY + 30;

        /// <summary>A heart's top-left: each kind has its own written line (red wraps onto a second).</summary>
        public static Vector2 HeartTopLeft(HeartKind kind, int i)
        {
            int line = kind == HeartKind.Red ? i / HeartsPerLine : 0, col = i % HeartsPerLine;
            int top = kind == HeartKind.Red ? BloodY + 2 + line * BloodLineStep : kind == HeartKind.Regen ? MendingY + 2 : MailY + 2;
            return new Vector2(ValueX + col * HeartStep, top);
        }

        /// <summary>How many hearts of a kind the page has room for; past that it writes "+N".</summary>
        public static int HeartCapacity(HeartKind kind) => kind == HeartKind.Red ? HeartsPerLine * BloodLines : HeartsPerLine;

        /// <summary>The middle of a heart sprite's left and right lobes (row 2 of heartRows, clear of the highlight).</summary>
        public static Vector2 LeftLobe(Vector2 topLeft) => topLeft + new Vector2(2 + 1 * HeartPx + HeartPx / 2f, 2 + 2 * HeartPx + HeartPx / 2f);
        public static Vector2 RightLobe(Vector2 topLeft) => topLeft + new Vector2(2 + 5 * HeartPx + HeartPx / 2f, 2 + 2 * HeartPx + HeartPx / 2f);

        public const int KeyGap = 30;
        public const int BlinkW = 60, BlinkH = 12, BlinkGap = 8;
        public const int MomentumW = 134, MomentumH = 12;
        /// <summary>The depth seal: a little wooden block on the page with the floor struck into it.</summary>
        public static Rect DepthSeal => new Rect(ValueX, DepthY + 4, 62, 46);
        public const int TallyX = ValueX + 72, TallyH = 18;

        // ---- the right page: the map, then the kit ----
        public const int MapCell = 28, MapBox = 24;
        public static Rect Map => new Rect(RightX + (LecternW - MapCell * 7) / 2, LecternTop + 48, MapCell * 7, MapCell * 7);
        public const int KitHeaderY = LecternTop + 262;
        public const int SlotSize = 64, SlotGap = 11, SlotY = LecternTop + 292;
        public static Rect Slot(int i) => new Rect(RightX + (LecternW - (SlotSize * 3 + SlotGap * 2)) / 2 + i * (SlotSize + SlotGap), SlotY, SlotSize, SlotSize);
        public const int CaptionY = SlotY + SlotSize + 4, ItemNameY = CaptionY + 22, CarryY = ItemNameY + 34;
        public const int CarryTile = 18, CarryStep = 22, CarryPerLine = 9;

        // ---- under the room: the Warden's bar; above it: banners ----
        public static Rect BossPlate => new Rect(290, 612, 700, 54);
        public static Rect BossWell => new Rect(302, 636, 676, 20);
        public static Rect Banner => new Rect(430, 10, 420, 100);
        public static Rect Warning => new Rect(450, 22, 380, 78);
        public static Rect DevLine => new Rect(290, 672, 700, 44);

        /// <summary>Every rectangle the HUD itself draws in, for the layout tests.</summary>
        public static Rect[] HudRects() => new[]
        {
            Stand(true), Stand(false), DepthSeal, Map, Slot(0), Slot(1), Slot(2), BossPlate, BossWell, Banner, Warning, DevLine,
            new Rect(ValueX, BloodY, HeartStep * (HeartsPerLine - 1) + HeartW, BloodLineStep * BloodLines),
        };
    }
}
