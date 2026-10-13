using UnityEngine;

namespace Depths.Unity.Hud
{
    /// <summary>
    /// Every place the ledger HUD draws, in panel units at the 1280x720 reference. One table, read by
    /// the HUD, the overlays, the layout tests and the screenshot pixel check, so none of them can drift.
    /// Two ledger boards stand the full height of the screen beside the room: the body on the left, the
    /// map and the kit on the right. The band above the room is the home of the floor heading and the
    /// Warden's notice; the band below holds the Warden's bar.
    /// </summary>
    public static class HudLayout
    {
        /// <summary>The room with its walls, from WorldView.Origin (the HUD must never draw over it).</summary>
        public static Rect Room => new Rect(
            Balance.RoomLeft - Art.Materials.WallThick + WorldView.Origin.x, Balance.RoomTop - Art.Materials.WallThick + WorldView.Origin.y,
            Balance.RoomRight - Balance.RoomLeft + Art.Materials.WallThick * 2, Balance.RoomBottom - Balance.RoomTop + Art.Materials.WallThick * 2);

        // ---- the two boards ----
        public const int BoardW = 258, BoardH = 688, BoardTop = 16, PaperPad = 10;
        public const int LeftX = 8, RightX = 1280 - 8 - BoardW;
        public static Rect Board(bool left) => new Rect(left ? LeftX : RightX, BoardTop, BoardW, BoardH);
        /// <summary>Where writing starts on a page, and how wide it may run.</summary>
        public static int Ink0(bool left) => (left ? LeftX : RightX) + PaperPad + 10;
        public const int InkW = BoardW - (PaperPad + 10) * 2;   // 218
        public const int HeaderY = BoardTop + 28, TextScale = 2, LabelH = 7 * TextScale;

        // ---- the body ----
        /// <summary>Hearts: one row, Isaac order (red, the regen heart, armour), wrapping onto a second line.</summary>
        public const int HeartPx = 4, HeartW = 7 * HeartPx + 4, HeartH = 6 * HeartPx + 4, HeartStep = 31, HeartsPerLine = 7, HeartLines = 2;
        public const int HeartsY = BoardTop + 60, HeartLineStep = 32;
        public static int HeartCapacity => HeartsPerLine * HeartLines;
        public static Rect HeartsBox => new Rect(Ink0(true), HeartsY, InkW, HeartLineStep * HeartLines);

        /// <summary>The i-th heart of the row (all kinds share it): its sprite's top-left.</summary>
        public static Vector2 HeartTopLeft(int i) =>
            new Vector2(Ink0(true) + (i % HeartsPerLine) * HeartStep, HeartsY + (i / HeartsPerLine) * HeartLineStep);

        /// <summary>The middle of a heart sprite's left and right lobes (row 2 of heartRows, clear of the highlight).</summary>
        public static Vector2 LeftLobe(Vector2 topLeft) => topLeft + new Vector2(2 + 1 * HeartPx + HeartPx / 2f, 2 + 2 * HeartPx + HeartPx / 2f);
        public static Vector2 RightLobe(Vector2 topLeft) => topLeft + new Vector2(2 + 5 * HeartPx + HeartPx / 2f, 2 + 2 * HeartPx + HeartPx / 2f);

        public const int Rule1 = HeartsY + HeartLineStep * HeartLines + 6;
        public const int KeysLabelY = Rule1 + 10;
        public static Rect KeyWell(int i) => new Rect(Ink0(true) + i * 62, KeysLabelY + 20, 54, 32);
        public const int BlinkLabelY = KeysLabelY + 64;
        public static Rect BlinkWell(int i) => new Rect(Ink0(true) + i * 112, BlinkLabelY + 20, 106, 22);
        public const int MomentumLabelY = BlinkLabelY + 54;
        public static Rect MomentumWell => new Rect(Ink0(true), MomentumLabelY + 20, InkW, 22);
        public const int Rule2 = MomentumLabelY + 56;
        public const int DepthLabelY = Rule2 + 10;
        public static Rect DepthSeal => new Rect(Ink0(true), DepthLabelY + 20, 76, 58);
        public const int TallyX = LeftX + PaperPad + 10 + 88, TallyH = 20;
        public const int AreaY = DepthLabelY + 86;
        public const int Rule3 = AreaY + 44;
        public const int SlainLabelY = Rule3 + 10;
        public const int SlainY = SlainLabelY + 22;
        public const int Rule4 = SlainY + 36;
        public const int SeedLabelY = Rule4 + 10;
        public static Rect SeedTag => new Rect(Ink0(true) + 4, SeedLabelY + 22, 208, 56);

        // ---- the map and the kit ----
        public const int MapCell = 32, MapBox = 27;
        public static Rect Map => new Rect(RightX + (BoardW - MapCell * 7) / 2, BoardTop + 58, MapCell * 7, MapCell * 7);
        public const int KitHeaderY = BoardTop + 302;
        public const int SlotSize = 66, KitRowStep = 78;
        public static Rect Slot(int i) => new Rect(Ink0(false), KitHeaderY + 34 + i * KitRowStep, SlotSize, SlotSize);
        /// <summary>The name plate beside each slot: what is in it, and the key that fires it.</summary>
        public static Rect Plate(int i) { var s = Slot(i); return new Rect(s.xMax + 8, s.y + 4, InkW - SlotSize - 8, SlotSize - 8); }
        public const int CarryLabelY = KitHeaderY + 34 + KitRowStep * 3 + 4;
        public const int CarryY = CarryLabelY + 20, CarryTile = 22, CarryStep = 27, CarryPerLine = 8;

        // ---- the bands above and below the room ----
        public static Rect BossPlate => new Rect(290, 614, 700, 82);
        public static Rect BossWell => new Rect(304, 650, 672, 30);
        public static Rect Banner => new Rect(430, 10, 420, 100);
        public static Rect Warning => new Rect(450, 22, 380, 78);
        public static Rect DevLine => new Rect(290, 4, 700, 40);
        /// <summary>How far the last-heart glow reaches out from the room's rim (into the gap and the bands).</summary>
        public const int RimGlow = 16;

        /// <summary>Every rectangle the HUD itself draws in, for the layout tests.</summary>
        public static Rect[] HudRects() => new[]
        {
            Board(true), Board(false), HeartsBox, KeyWell(0), KeyWell(1), BlinkWell(0), BlinkWell(1), MomentumWell, DepthSeal, SeedTag,
            Map, Slot(0), Slot(1), Slot(2), Plate(0), Plate(1), Plate(2), BossPlate, BossWell, Banner, Warning, DevLine,
        };
    }
}
