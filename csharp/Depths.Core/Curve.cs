namespace Depths
{
    /// <summary>
    /// The difficulty curve (C# only, 2026-10-07): a per-floor shape laid over the depth ladder, plus
    /// the targets the playtest bot's `curve` command checks it against. The JS ladder is untouched.
    /// </summary>
    public static class Curve
    {
        /// <summary>Whether the shape applies. The game: always. Tests default it off (assembly [Curve]) so the parity rows keep the JS ladder.</summary>
        public static bool On = true;

        /// <summary>
        /// One stage of the shape, from floor First until the next stage's First. Tough, Rate and Drops
        /// multiply; Bodies, Pack and Heavy add. Fields, not constants, so the bot's --dial can try a value.
        /// </summary>
        public sealed class Stage
        {
            public readonly string Name;
            public readonly int First;
            public double Tough = 1, Rate = 1, Bodies, Pack, Heavy, Drops = 1;
            public Stage(string name, int first) { Name = name; First = first; }
            public override string ToString() =>
                $"{Name} (from floor {First}): tough x{Tough} rate x{Rate} bodies {Bodies:+0.##;-0.##;0} pack {Pack:+0.##;-0.##;0} heavy {Heavy:+0.##;-0.##;0} drops x{Drops}";
        }

        /// <summary>
        /// The shape. START (1-2) is a bump while the player has no kit, MIDDLE (3-10) a dip, then the
        /// climb; the last stage holds from its floor on while the exponential ladder under it keeps climbing.
        /// </summary>
        // Why each column (measured with the bot's curve command):
        //   Tough   health: longer fights cost more HP; the dip's main dial.
        //   Rate    cadence and ranged approach (still under DepthRateCap): shells reward dodging, so it widens the skill gap.
        //   Bodies  added to each room's roll (a fraction only counts once the ladder's own adds it to a whole body); fewer bodies, fewer kills, fewer drops.
        //   Pack    added to the Brunch pack chance; packs are the middle's main attrition.
        //   Heavy   added to the area's gunner chance; the gunner's 4 HP shell is what kills, and skill barely dodges it.
        //   Drops   the kill drop chance x this; 0 in START so the first floors cost hearts for real.
        public static readonly Stage[] Stages =
        {
            new Stage("start", 1) { Tough = 0.85, Rate = 1.30, Heavy = -0.30, Drops = 0 },                  // fast shooters, fewer gunners, no pickups
            new Stage("peak", 2) { Tough = 0.90, Rate = 1.30, Heavy = -0.30, Drops = 0 },                   // a little tougher
            new Stage("soft", 3) { Tough = 0.75, Bodies = -0.5, Pack = -0.15, Heavy = -0.30, Drops = 0.2 }, // the softest floor, a first trickle of pickups
            new Stage("middle", 4) { Tough = 0.90, Bodies = -0.5, Pack = -0.10, Heavy = -0.35, Drops = 0.11 }, // one body less, fewer packs and gunners, scarce drops
            new Stage("ramp", 11) { Tough = 1.30, Drops = 0.25 },                                           // the gate before the last area
            new Stage("climb", 12) { Tough = 1.30, Heavy = 0.10, Drops = 0.25 },                            // and a gunner more often
            new Stage("end", 13) { Drops = 0.25 },   // the last area's mix and the exponential ladder carry the climb from here
        };

        static readonly Stage Flat = new Stage("off", 1);

        /// <summary>The stage in force on a floor: the flat stage when the curve is off.</summary>
        public static Stage At(int floor)
        {
            if (!On) return Flat;
            var at = Stages[0];
            foreach (var s in Stages) if (s.First <= floor) at = s;
            return at;
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
