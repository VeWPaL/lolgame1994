using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity.Art
{
    /// <summary>
    /// A small pixel picture of every item (12x12 rows, baked with the sprite outline like the bodies),
    /// so the kit, the carried row and the floor show the thing itself instead of its two-letter tile.
    /// m is the item's own colour (ItemArt), d its shade, l its light; k iron, r red, y flame.
    /// An item without a picture keeps its lettered tile.
    /// </summary>
    public static class ItemIcons
    {
        public const int Size = 12;

        static readonly Dictionary<string, string[]> Rows = new Dictionary<string, string[]>
        {
            ["tin_cup"] = new[] {
                "............", ".llllllll...", ".lmmmmmml...", ".mmmmmmmmmm.", ".mmmmmmmm..m", ".mmmmmmmm..m",
                ".mmmmmmmm..m", ".mmmmmmmmmm.", ".dmmmmmmd...", "..dmmmmd....", "...dddd.....", "............" },
            ["heavy_hands"] = new[] {
                "............", "..m.m.m.....", ".mm.m.m.m...", ".mm.m.m.m...", ".mmmmmmmm...", "mmmmmmmmm...",
                "mmlmmmmmm...", "mmmmmmmmd...", ".mmmmmmd....", "..mmmmd.....", "..dddd......", "............" },
            ["weighted_rod"] = new[] {
                "..........dd", ".........dld", "........dld.", ".......lmd..", "......lmd...", ".....lmd....",
                "....lmd.....", "...kkk......", "..kkkkk.....", "..kkkkk.....", "...kkk......", "............" },
            ["bone_whistle"] = new[] {
                "............", "............", "............", "..llllllll..", ".lmmmmmmmmm.", "mmmkmmmmmmmm",
                "mmmmmmmmmmdm", ".dddddddddd.", "...........d", "............", "............", "............" },
            ["iron_ribs"] = new[] {
                ".....kk.....", "..lmmkkmml..", ".m...kk...m.", "..mmmkkmmm..", ".m...kk...m.", "..mmmkkmmm..",
                ".m...kk...m.", "..mmmkkmmm..", "...m.kk.m...", ".....kk.....", "............", "............" },
            ["swift_boots"] = new[] {
                "............", "..mmm.......", "..mmm...l.l.", "..mmm..lll..", "..mmm.lll...", "..mmmlll....",
                "..mmmmmmm...", "..mmmmmmmmm.", "..mmmmmmmmmm", "..dddddddddd", "............", "............" },
            ["lucky_coin"] = new[] {
                "....dddd....", "..ddmmmmdd..", ".dmmllllmmd.", ".dmlmmmmmmd.", "dmlmmkkmmmmd", "dmlmkmmkmmmd",
                "dmmmkmmkmmmd", "dmmmmkkmmmmd", ".dmmmmmmmmd.", ".dmmmmmmmmd.", "..ddmmmmdd..", "....dddd...." },
            ["steady_hand"] = new[] {
                "...m.m......", "...m.m.m....", ".m.m.m.m....", ".m.m.m.m....", ".mmmmmmm.m..", ".mmmmmmmmm..",
                ".mmmmmmmm...", "..mmmmmmm...", "..mmmmmm....", "...mmmm.....", "...dddd.....", "............" },
            ["weighted_grip"] = new[] {
                ".....kk.....", "....kllk....", "....kmmk....", "....dddd....", "....mmmm....", "....dddd....",
                "....mmmm....", "....dddd....", "....mmmm....", "....kkkk....", ".....kk.....", "............" },
            ["hunters_mark"] = new[] {
                ".....m......", "...mmmmm....", "..m..m..m...", ".m...m...m..", ".m..rrr..m..", "mmmmr.rmmmmm",
                ".m..rrr..m..", ".m...m...m..", "..m..m..m...", "...mmmmm....", ".....m......", "............" },
            ["glass_wands"] = new[] {
                "l.........l.", ".l.......l..", "..m.....m...", "...m...m....", "....m.m.....", ".....m......",
                "....m.m.....", "...d...d....", "..d.....d...", ".d.......d..", "d.........d.", "............" },
            ["brass_compass"] = new[] {
                ".....kk.....", "....dddd....", "..ddmmmmdd..", ".dmllllllmd.", ".dml..r.lmd.", "dml...r..lmd",
                "dml..kkk.lmd", "dml...l..lmd", ".dml..l.lmd.", ".dmlllllmmd.", "..ddmmmmdd..", "....dddd...." },
            ["lantern_friend"] = new[] {
                ".....kk.....", "....k..k....", "...kkkkkk...", "...kmmmmk...", "...kmyymk...", "...kyyyyk...",
                "...kmyymk...", "...kmmmmk...", "...kkkkkk...", "....kkkk....", "............", "............" },
        };

        public static bool Has(string id) => id != null && Rows.ContainsKey(id);
        public static IEnumerable<string> Ids => Rows.Keys;

        /// <summary>The item's picture at a pixel size, outlined like every sprite.</summary>
        public static Img Bake(string id, int px)
        {
            var m = Hex.C(ItemArt.Color(id));
            var pal = new Dictionary<char, Color32>
            {
                ['m'] = m, ['d'] = Hex.Mix(m, Hex.C("#000000"), 0.42f), ['l'] = Hex.Mix(m, Hex.C("#ffffff"), 0.45f),
                ['k'] = Hex.C("#4b4f58"), ['r'] = Hex.C("#c93b3b"), ['y'] = Hex.C("#fff1a0"),
            };
            return Sprites.Bake(Rows[id], pal, px);
        }
    }
}
