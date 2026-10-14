using UnityEngine;

namespace Depths.Unity.Hud
{
    /// <summary>Every place the ledger HUD draws, in panel units at the 1280x720 reference. [h:HudLayout-1]</summary>
    public static class HudLayout
    {
        /// <summary>The room with its walls, from WorldView.Origin (the HUD must never draw over it).</summary>
        public static Rect Room => new Rect(
            Balance.RoomLeft - Art.Materials.WallThick + WorldView.Origin.x, Balance.RoomTop - Art.Materials.WallThick + WorldView.Origin.y,
            Balance.RoomRight - Balance.RoomLeft + Art.Materials.WallThick * 2, Balance.RoomBottom - Balance.RoomTop + Art.Materials.WallThick * 2);

        // ---- the two boards ----
        public const int BoardW = 262, BoardH = 688, BoardTop = 16, PaperPad = 10;
        public const int LeftX = 4, RightX = 1280 - 4 - BoardW;
        public static Rect Board(bool left) => new Rect(left ? LeftX : RightX, BoardTop, BoardW, BoardH);
        /// <summary>Where writing starts on a page, and how wide it may run.</summary>
        public static int Ink0(bool left) => (left ? LeftX : RightX) + PaperPad + 10;
        public const int InkW = BoardW - (PaperPad + 10) * 2;   // 222
        public const int TextScale = 2, LabelH = 7 * TextScale;
        public static int PaperBottom => BoardTop + BoardH - PaperPad;

        // ---- the body: read top to bottom as icons, no form labels ----
        /// <summary>Hearts: one row of eight across the page, Isaac order (red, the regen heart, armour).</summary>
        public const int HeartPx = 4, HeartW = 7 * HeartPx + 4, HeartH = 6 * HeartPx + 4, HeartStep = 30, HeartsPerLine = 8, HeartCapacity = 16;
        public const int HeartsX = LeftX + PaperPad, HeartsY = BoardTop + 26;   // no page heading: the hearts open it
        public static Rect HeartsBox => new Rect(HeartsX, HeartsY, HeartStep * (HeartsPerLine - 1) + HeartW, HeartH);

        /// <summary>The i-th heart of the row (all kinds share it): its sprite's top-left.</summary>
        public static Vector2 HeartTopLeft(int i, int count = HeartsPerLine) =>
            new Vector2(HeartsX + i * HeartStepFor(count), HeartsY);

        /// <summary>One row always: past eight the hearts close up so the row keeps its width (they never wrap onto the
        /// momentum bar and the keys).</summary>
        public static float HeartStepFor(int count) =>
            count <= HeartsPerLine ? HeartStep : (HeartStep * (HeartsPerLine - 1)) / (float)(System.Math.Min(count, HeartCapacity) - 1);

        /// <summary>The middle of a heart sprite's left and right lobes (row 2 of heartRows, clear of the highlight).</summary>
        public static Vector2 LeftLobe(Vector2 topLeft) => topLeft + new Vector2(2 + 1 * HeartPx + HeartPx / 2f, 2 + 2 * HeartPx + HeartPx / 2f);
        public static Vector2 RightLobe(Vector2 topLeft) => topLeft + new Vector2(2 + 5 * HeartPx + HeartPx / 2f, 2 + 2 * HeartPx + HeartPx / 2f);

        /// <summary>Momentum: a slim bar straight under the hearts (it is how the body is moving).</summary>
        public static Rect MomentumWell => new Rect(HeartsX + 2, HeartsY + HeartH + 6, HeartStep * (HeartsPerLine - 1) + HeartW - 4, 12);
        /// <summary>Each icon row: a pictogram in the margin, then its wells.</summary>
        public const int IconX = LeftX + PaperPad + 8, RowX = IconX + 26;
        public const int KeysY = BoardTop + 98;
        public static Rect KeyWell(int i) => new Rect(RowX + i * 60, KeysY, 54, 32);
        public const int BlinkY = KeysY + 44;
        public static Rect BlinkWell(int i) => new Rect(RowX + i * 100, BlinkY + 4, 94, 20);
        public const int Rule2 = BlinkY + 38;
        public static Rect DepthSeal => new Rect(Ink0(true), Rule2 + 12, 76, 58);
        public const int AreaX = LeftX + PaperPad + 10 + 88, AreaY = Rule2 + 16;
        /// <summary>The descent: each area a band of four floors, passed floors struck through, this one inked.</summary>
        public const int LadderY = Rule2 + 86, BandStep = 68, RungW = 50, RungH = 24, RungStep = 56;
        public static Rect Rung(int band, int i) => new Rect(Ink0(true) + i * RungStep, LadderY + band * BandStep + 20, band == 3 ? InkW : RungW, RungH);
        public static int BandLabelY(int band) => LadderY + band * BandStep;
        public const int Rule3 = LadderY + BandStep * 4 + 4;
        /// <summary>The footer: what has been slain, and the seed on its tag.</summary>
        public const int SlainY = Rule3 + 14;
        public static Rect SeedTag => new Rect(Ink0(true) + 6, SlainY + 36, 208, 56);

        // ---- the map and the kit ----
        public const int MapCell = 32, MapBox = 27, MapMaxCell = 46;
        public static Rect Map => new Rect(RightX + (BoardW - MapCell * 7) / 2, BoardTop + 26, MapCell * 7, MapCell * 7);   // no page heading: the map opens it
        public const int KitY = BoardTop + 266;   // the first slot's top: no heading, the slots speak for themselves
        public const int SlotSize = 66, KitRowStep = 78;
        public static Rect Slot(int i) => new Rect(Ink0(false), KitY + i * KitRowStep, SlotSize, SlotSize);
        /// <summary>The name plate beside each slot: what is in it, and the key that fires it.</summary>
        public static Rect Plate(int i) { var s = Slot(i); return new Rect(s.xMax + 8, s.y + 4, InkW - SlotSize - 8, SlotSize - 8); }
        public const int CarryLabelY = KitY + KitRowStep * 3 + 4;
        public const int CarryY = CarryLabelY + 20, CarryTile = 28, CarryStep = 31, CarryPerLine = 7;

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
            Board(true), Board(false), HeartsBox, KeyWell(0), KeyWell(1), BlinkWell(0), BlinkWell(1), MomentumWell, DepthSeal, SeedTag, Rung(0, 0), Rung(3, 0),
            Map, Slot(0), Slot(1), Slot(2), Plate(0), Plate(1), Plate(2), BossPlate, BossWell, Banner, Warning, DevLine,
        };
    }
}
