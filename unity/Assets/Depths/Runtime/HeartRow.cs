using System;
using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity
{
    public enum HeartKind { Red, Regen, Armour }
    public enum HeartFill { Empty, Half, Full }

    /// <summary>One heart of the health row: what it is and how full.</summary>
    public readonly struct HeartSlot
    {
        public readonly HeartKind Kind;
        public readonly HeartFill Fill;
        public HeartSlot(HeartKind k, HeartFill f) { Kind = k; Fill = f; }
    }

    /// <summary>The health row as data: 1 HP is half a heart. [h:HeartRow-1]</summary>
    public static class HeartRow
    {
        public static readonly Color Red = new Color32(0xe8, 0x39, 0x5a, 255), RedEmpty = new Color32(0x3a, 0x25, 0x30, 255),
            Regen = new Color32(0xcd, 0x4e, 0x96, 255), RegenEmpty = new Color32(0x6b, 0x34, 0x59, 255),
            Armour = new Color32(0x9a, 0xa0, 0xab, 255);

        public static List<HeartSlot> Layout(Depths.Player pl, List<HeartSlot> into = null)
        {
            var row = into ?? new List<HeartSlot>();
            row.Clear();
            int hearts = (int)Math.Ceiling(pl.maxHp / 2), hp = (int)Math.Max(0, Math.Round(pl.hp));
            for (int i = 0; i < hearts; i++) row.Add(new HeartSlot(HeartKind.Red, FillOf(hp, i)));
            int regen = (int)Math.Ceiling(pl.regenHeartMax / 2), rg = (int)Math.Max(0, Math.Round(pl.regenHeart));
            for (int i = 0; i < regen; i++) row.Add(new HeartSlot(HeartKind.Regen, FillOf(rg, i)));
            int ar = (int)Math.Max(0, Math.Round(pl.armor));
            for (int i = 0; 2 * i < ar; i++) row.Add(new HeartSlot(HeartKind.Armour, FillOf(ar, i)));
            return row;
        }

        static HeartFill FillOf(int points, int i) =>
            points >= 2 * i + 2 ? HeartFill.Full : points == 2 * i + 1 ? HeartFill.Half : HeartFill.Empty;

        public static Color Lit(HeartKind k) => k == HeartKind.Red ? Red : k == HeartKind.Regen ? Regen : Armour;

        /// <summary>The colour of an unlit half: the red and gray hearts share the dark slot, the regen heart has its own.</summary>
        public static Color Unlit(HeartKind k) => k == HeartKind.Regen ? RegenEmpty : RedEmpty;
    }
}
