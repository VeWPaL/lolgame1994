using System.Collections.Generic;

namespace Depths
{
    /// <summary>
    /// Which bodies a run meets where (C# only, Balance.TunePools; owner 2026-10-13). Each run rolls its own
    /// group of <see cref="GroupSize"/> kinds for every area, so every stretch of the climb is a different fight
    /// from run to run; the next area's newcomers start turning up <see cref="PreviewFloors"/> floors early, rarely,
    /// so the player sees what is coming. Depth still scales every kind (Spawn.Body: health, speed, cadence),
    /// so a kind met on floor 2 and again on floor 11 is a harder fight the second time.
    ///
    /// <para>The groups come from their own stream, seeded by the run's seed, so they spend no draw of the run's
    /// and the same seed always gives the same groups. Every group has a body that comes to you (Lunger or
    /// Burrower; the Brunch only arrives as a pack) and one that shoots from a distance (Shooter, Splitter, Mortar, Sprayer), and shares at
    /// most <see cref="MaxShared"/> kinds with the area before it.</para>
    /// </summary>
    public static class Pools
    {
        public static readonly BodyKind[] Kinds =
        {
            BodyKind.Lunger, BodyKind.Brunch, BodyKind.Shooter, BodyKind.Gunner,
            BodyKind.Splitter, BodyKind.Mortar, BodyKind.Burrower, BodyKind.Sprayer, BodyKind.Egg,
        };
        public const int GroupSize = 4, MaxShared = 2, PreviewFloors = 2;
        /// <summary>A newcomer's weight against a group kind's 1: two floors before its area, then one floor before.</summary>
        public const double PreviewFar = 0.12, PreviewNear = 0.25;
        public const int MaxEggs = 2;   // turrets per room: more is a bullet field, not a fight
        const uint StreamSalt = 0x9E3779B9;

        /// <summary>A slot body that walks in to you. The Brunch comes too, but only by the pack roll, so a group
        /// whose only walker it is would often have none (the cold critic): it does not count.</summary>
        public static bool Comes(BodyKind k) => k == BodyKind.Lunger || k == BodyKind.Burrower;
        public static bool Shoots(BodyKind k) => k == BodyKind.Shooter || k == BodyKind.Splitter || k == BodyKind.Mortar || k == BodyKind.Sprayer;
        /// <summary>A kind a wave slot can hold on its own: not the Gunner (one a room, by the heavy roll) nor the Brunch (packs).</summary>
        public static bool Slot(BodyKind k) => k != BodyKind.Gunner && k != BodyKind.Brunch;

        /// <summary>The run's groups, one per <see cref="Area"/> in order.</summary>
        public static BodyKind[][] Roll(uint seed)
        {
            var rng = new Rng(seed ^ StreamSalt);
            var groups = new BodyKind[4][];
            var bag = new List<BodyKind>(Kinds);
            for (int a = 0; a < groups.Length; a++)
            {
                BodyKind[]? pick = null;
                for (int tries = 0; tries < 500 && pick == null; tries++)
                {
                    for (int i = bag.Count - 1; i > 0; i--) { int j = (int)(rng.Run() * (i + 1)); (bag[i], bag[j]) = (bag[j], bag[i]); }
                    var g = bag.GetRange(0, GroupSize).ToArray();
                    if (Valid(g, a > 0 ? groups[a - 1] : null)) pick = g;
                }
                groups[a] = pick ?? new[] { BodyKind.Lunger, BodyKind.Brunch, BodyKind.Shooter, BodyKind.Gunner };   // never reached: kept total
            }
            return groups;
        }

        public static bool Valid(BodyKind[] g, BodyKind[]? before)
        {
            bool comes = false, shoots = false;
            int slots = 0, shared = 0;
            foreach (var k in g)
            {
                comes |= Comes(k); shoots |= Shoots(k);
                if (Slot(k)) slots++;
                if (before != null && System.Array.IndexOf(before, k) >= 0) shared++;
            }
            return comes && shoots && slots >= 2 && shared <= MaxShared;
        }

        /// <summary>How often a kind is drawn on a floor: 1 in its area's group, a preview weight in the last
        /// floors before the area whose group brings it, else 0.</summary>
        public static double Weight(BodyKind[][] groups, int floor, BodyKind k)
        {
            var area = AreaRules.AreaForFloor(floor);
            int a = (int)area;
            if (System.Array.IndexOf(groups[a], k) >= 0) return 1;
            if (area == Area.Final) return 0;
            int last = LastFloor(area), left = last - floor;   // 0 on the area's last floor
            if (left >= PreviewFloors || System.Array.IndexOf(groups[a + 1], k) < 0) return 0;
            return left == 0 ? PreviewNear : PreviewFar;
        }

        /// <summary>The deepest floor of an area (Area.cs: 4, 8, 12); the Final area has none.</summary>
        public static int LastFloor(Area area) => area == Area.Area1 ? 4 : area == Area.Area2 ? 8 : area == Area.Area3 ? 12 : int.MaxValue;

        /// <summary>The groups as text, for the bot's records and the dev line: "Shallows: Lunger Brunch ...".</summary>
        public static string Describe(BodyKind[][] groups)
        {
            var parts = new List<string>();
            for (int a = 0; a < groups.Length; a++) parts.Add((Area)a + ": " + string.Join(" ", groups[a]));
            return string.Join(" | ", parts);
        }
    }
}
