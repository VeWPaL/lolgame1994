using System;
using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity
{
    public enum HeartKind { Red, Regen, Armour }
    public enum HeartFill { Empty, Half, Full }

    /// <summary>One heart of the health row: its centre in panel units, what it is and how full.</summary>
    public readonly struct HeartSlot
    {
        public readonly Vector2 Centre;
        public readonly HeartKind Kind;
        public readonly HeartFill Fill;
        public HeartSlot(Vector2 c, HeartKind k, HeartFill f) { Centre = c; Kind = k; Fill = f; }
    }

    /// <summary>
    /// The health row above the room, as data: 1 HP is half a heart. Red hearts (dark when empty),
    /// then the regenerating heart, then armour (no empty slots). The painter draws it; the screenshot
    /// check takes kinds and fills from the spec but positions from Size/Step, so it cannot catch a bad spacing.
    /// </summary>
    public static class HeartRow
    {
        public const float Size = 10, Step = 26;   // half-width of a heart, distance between centres
        public static readonly Color Red = new Color32(230, 57, 90, 255), RedEmpty = new Color32(58, 37, 48, 255),
            Regen = new Color32(205, 78, 150, 255), RegenEmpty = new Color32(70, 38, 64, 255),
            Armour = new Color32(170, 176, 186, 255);

        public static List<HeartSlot> Layout(Depths.Player pl, Vector2 first, List<HeartSlot> into = null)
        {
            var row = into ?? new List<HeartSlot>();
            row.Clear();
            var at = first;
            int hearts = (int)Math.Ceiling(pl.maxHp / 2), hp = (int)Math.Max(0, Math.Round(pl.hp));
            for (int i = 0; i < hearts; i++, at.x += Step) row.Add(new HeartSlot(at, HeartKind.Red, FillOf(hp, i)));
            int regen = (int)Math.Ceiling(pl.regenHeartMax / 2), rg = (int)Math.Max(0, Math.Round(pl.regenHeart));
            for (int i = 0; i < regen; i++, at.x += Step) row.Add(new HeartSlot(at, HeartKind.Regen, FillOf(rg, i)));
            int ar = (int)Math.Max(0, Math.Round(pl.armor));
            for (int i = 0; 2 * i < ar; i++, at.x += Step) row.Add(new HeartSlot(at, HeartKind.Armour, FillOf(ar, i)));
            return row;
        }

        static HeartFill FillOf(int points, int i) =>
            points >= 2 * i + 2 ? HeartFill.Full : points == 2 * i + 1 ? HeartFill.Half : HeartFill.Empty;

        public static Color Lit(HeartKind k) => k == HeartKind.Red ? Red : k == HeartKind.Regen ? Regen : Armour;

        /// <summary>The colour under an unlit half; null for armour, which draws nothing there.</summary>
        public static Color? Unlit(HeartKind k) => k == HeartKind.Red ? RedEmpty : k == HeartKind.Regen ? RegenEmpty : (Color?)null;

        /// <summary>The centres of a heart's two lobes, the points a pixel check samples.</summary>
        public static Vector2 LeftLobe(HeartSlot h) => new Vector2(h.Centre.x - Size * 0.5f, h.Centre.y - Size * 0.3f);
        public static Vector2 RightLobe(HeartSlot h) => new Vector2(h.Centre.x + Size * 0.5f, h.Centre.y - Size * 0.3f);
    }
}
