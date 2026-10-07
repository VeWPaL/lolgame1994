namespace Depths
{
    /// <summary>
    /// The difficulty curve (C# only, 2026-10-06): a per-floor shape laid over the depth ladder, plus
    /// the targets the playtest bot's `curve` command checks it against. The JS ladder is untouched.
    /// </summary>
    public static class Curve
    {
        /// <summary>Whether the shape applies. The game: always. Tests default it off (assembly [Curve]) so the parity rows keep the JS ladder.</summary>
        public static bool On = true;

        /// <summary>One floor's shape. Tough, Rate, Hit and Drops multiply; Bodies, Pack and Heavy (the gunner chance) add.</summary>
        public readonly struct Step
        {
            public readonly double Tough, Rate, Bodies, Pack, Heavy, Hit, Drops;
            public Step(double tough, double rate, double bodies, double pack, double heavy, double hit, double drops)
            { Tough = tough; Rate = rate; Bodies = bodies; Pack = pack; Heavy = heavy; Hit = hit; Drops = drops; }
            public override string ToString() =>
                $"tough x{Tough} rate x{Rate} bodies {Bodies:+0.##;-0.##;0} pack {Pack:+0.##;-0.##;0} heavy {Heavy:+0.##;-0.##;0} hit x{Hit} drops x{Drops}";
        }

        /// <summary>
        /// The shape, floor 1 first; floors past the end repeat the last row (the ladder under it keeps
        /// climbing). START (1-2) is a bump while the player has no kit, MIDDLE (3-10) a dip, then the climb.
        /// </summary>
        public static readonly Step[] Default =
        {
            new Step(0.85, 1.30, 0.0, 0.00, 0.00, 0.75, 0.0),   // 1
            new Step(0.90, 1.30, 0.5, 0.00, 0.00, 0.75, 0.0),   // 2
            new Step(0.75, 1.00, -0.5, -0.15, -0.30, 1.0, 0.3),   // 3
            new Step(0.85, 1.00, -0.5, -0.10, -0.20, 1.0, 0.2),   // 4
            new Step(0.85, 1.00, -0.5, -0.10, -0.20, 1.0, 0.2),   // 5
            new Step(0.85, 1.00, -0.5, -0.10, -0.20, 1.0, 0.2),   // 6
            new Step(0.85, 1.00, -0.5, -0.10, -0.20, 1.0, 0.2),   // 7
            new Step(0.85, 1.00, -0.5, -0.10, -0.20, 1.0, 0.2),   // 8
            new Step(0.85, 1.00, -0.5, -0.10, -0.20, 1.0, 0.2),   // 9
            new Step(0.85, 1.00, -0.5, -0.10, -0.20, 1.0, 0.2),   // 10
            new Step(1.00, 1.00, 0.0, 0.00, 0.00, 1.0, 0.25),   // 11
            new Step(1.10, 1.00, 0.5, 0.00, 0.05, 1.0, 0.25),   // 12
            new Step(1.15, 1.00, 0.5, 0.00, 0.10, 1.0, 0.25),   // 13
        };

        /// <summary>The table in force; the game never swaps it, the tests that pin the shape and the check do.</summary>
        public static Step[] Table = Default;

        static readonly Step Flat = new Step(1, 1, 0, 0, 0, 1, 1);

        /// <summary>The shape at a floor: the flat row when the curve is off, the last row past the table's end.</summary>
        public static Step At(int floor)
        {
            if (!On || Table.Length == 0) return Flat;
            int i = System.Math.Max(1, floor) - 1;
            return Table[System.Math.Min(i, Table.Length - 1)];
        }

        /// <summary>An enemy hit at this floor in whole HP: the base times the floor's Hit, rounded half up, never below 1.</summary>
        public static double HitAt(int floor, double amount)
        {
            if (!On || Balance.JsReference || amount <= 0) return amount;
            return System.Math.Max(1, System.Math.Floor(amount * At(floor).Hit + 0.5));
        }

        // ------------------------------------------------------------- targets

        /// <summary>The bands, in floors: START 1-2, MIDDLE 3-10, END from 13 (floors 11-12 are the ramp, in no band).</summary>
        public const int StartLast = 2, MiddleFirst = 3, MiddleLast = 10, EndFirst = 13;

        /// <summary>A floor counts toward a band only when at least this many runs of the profile reached it.</summary>
        public const int MinEntries = 30;

        /// <summary>A target band hazard (deaths / floor entries) for one profile. Max is inclusive; 1 means no ceiling.</summary>
        public readonly struct Band
        {
            public readonly double Min, Max;
            public Band(double min, double max) { Min = min; Max = max; }
        }

        /// <summary>One profile's targets (the frozen rubric of 2026-10-06, a proposal the owner may retune).</summary>
        public readonly struct Target
        {
            public readonly string Profile;
            public readonly Band Start, Middle, End;
            public readonly double MedianFloorMin, MedianFloorMax;
            public Target(string profile, Band start, Band middle, Band end, double floorMin, double floorMax)
            { Profile = profile; Start = start; Middle = middle; End = end; MedianFloorMin = floorMin; MedianFloorMax = floorMax; }
        }

        /// <summary>Least skilled first: the check also wants each band's hazard to fall strictly down this list.</summary>
        public static readonly Target[] Targets =
        {
            new Target("novice", new Band(0.06, 0.14), new Band(0.01, 0.04), new Band(0.10, 1), 7, 12),
            new Target("average", new Band(0.02, 0.06), new Band(0.002, 0.015), new Band(0.04, 1), 0, 1e9),
            new Target("skilled", new Band(0.005, 0.03), new Band(0, 0.007), new Band(0.02, 1), 14, 1e9),
        };

        /// <summary>The novice's median HP (red + regen) leaving floor 2 is at most this: the start costs real hearts.</summary>
        public const double NoviceHpLeavingStartMax = 5;

        /// <summary>No single damage source deals more than this share of START damage: the start is hard, not unfair.</summary>
        public const double StartTopSourceMax = 0.5;

        /// <summary>Heart pickups stay at least this share of each profile's healing, so a drop still matters.</summary>
        public const double HeartShareMin = 0.03;

        /// <summary>END must not fall: its second half's hazard may sit below its first half's by at most this many standard errors.</summary>
        public const double EndFallSe = 2;
    }
}
