namespace Depths
{
    /// <summary>One row of the body table, in ticks and px per tick at the rate it was built for.</summary>
    public sealed class BodyRow
    {
        public double Mass, Radius, Armour, Hp;
        public int Art, Bar;
        public double? Walk, Run, Base;
        public double? Sense, Range, Close, Far, CdMin, CdVar, Dmg, PShotSpeed, PShotRadius;
    }

    /// <summary>The body table, the spawner, and the build-dependent trait. [h:Bodies-3]</summary>
    public static class Bodies
    {
        /// <summary>The knobs the table is written in multiples of, so the relationships survive the port. [h:Bodies-4]</summary>
        public const double Tough = 1.35;      // the global HP multiplier
        public const double ShotDmg = 2;       // whole HP since 2026-10-06 (JS: 1.8): one heart; a gunner's is two
        public const double Armour = 0.66;     // per-hit multiplier, so it scales every pellet

        // Every row names its Armour explicitly; a forgotten one reads 0.0. [h:Bodies-1]
        // Read from Balance, which the constant audit checks against the game. [h:Bodies-2]
        public static double BrunchWalk => Balance.BrunchWalkSpeed;
        public static double BrunchRun => Balance.BrunchRun;

        /// <summary>The table, rebuilt only when the tick rate changes (its speeds and cadences are per tick) - and built rather... [h:Bodies-5]</summary>
        public static BodyRow[] Rows
        {
            get
            {
                if (_rows == null || _rowsHz != Balance.TickHz) { _rows = BuildRows(); _rowsHz = Balance.TickHz; }
                return _rows;
            }
        }

        static BodyRow[]? _rows;
        static int _rowsHz;

        static BodyRow[] BuildRows() => new[]
        {
            // lunger: the baseline body, and the only one that reads the player's position directly
            new BodyRow { Mass = 1, Radius = 14, Art = 2, Bar = 26, Hp = 15 * Tough, Armour = Armour,
                      Walk = Balance.LungerWalk * Balance.PlayerMove,
                      Run  = Balance.LungerRun * Balance.PlayerMove },

            // brunch: the pressure. Small, fast, and it arrives as a pack rather than as a threat
            new BodyRow { Mass = 0.5, Radius = 8, Art = 1, Bar = 11, Hp = 2 * Tough, Armour = 1,
                      Walk = BrunchWalk, Run = BrunchRun },

            // shooter: the committed shell. Walks while it charges, which is what makes it a body
            // rather than a turret
            new BodyRow { Mass = 0.8, Radius = 14, Art = 2, Bar = 26, Hp = 5.6 * Tough,
                      Base = Balance.PerSec(94.5) * Balance.LungerPay, Sense = 600, Range = 520, Close = 150, Far = 250,
                      CdMin = Balance.Sec(0.5), CdVar = Balance.Sec(0.4), Dmg = ShotDmg,
                      PShotSpeed = Balance.PerSec(462), PShotRadius = 5, Armour = Armour },

            // gunner: the heavy shot. Roots itself to charge, so its accuracy is what it is
            new BodyRow { Mass = 2.4, Radius = 22, Art = 3, Bar = 32, Hp = 8 * Tough, Armour = Armour,
                      Base = Balance.PerSec(63) * Balance.LungerPay, Sense = 700, Range = 600, Close = 120, Far = 200,
                      CdMin = Balance.Sec(0.8), CdVar = Balance.Sec(0.6), Dmg = ShotDmg * 2,
                      PShotSpeed = Balance.PerSec(430.5), PShotRadius = 7 },

            // boss: the Warden. HP is sized from measured weapon DPS, which is the only way to size a health bar. 343*TOUGH... [h:Bodies-6]
            new BodyRow { Mass = 4, Radius = 28, Art = 4, Bar = 40, Hp = 343 * Tough, Armour = Armour,
                      Base = Balance.PerSec(126) * Balance.LungerPay,
                      Walk = 0.42 * Balance.PlayerMove, Run = 0.72 * Balance.PlayerMove },

            // the middle enemies (Middle.cs), C# only. Ranged rows walk a standoff like the shooter
            // splitter: a slow shell that bursts at its range, so it keeps a little further out
            new BodyRow { Mass = 0.9, Radius = 14, Art = 2, Bar = 26, Hp = 6 * Tough, Armour = Armour,
                      Base = Balance.PerSec(85) * Balance.LungerPay, Sense = 620, Range = 520, Close = 170, Far = 280,
                      CdMin = Balance.Sec(1.1), CdVar = Balance.Sec(0.5), Dmg = ShotDmg,
                      PShotSpeed = Balance.PerSec(360), PShotRadius = 6 },
            // mortar: heavy and far back; the lob ignores shot speed (Middle.LobFlight)
            new BodyRow { Mass = 1.6, Radius = 16, Art = 2, Bar = 28, Hp = 7 * Tough, Armour = Armour,
                      Base = Balance.PerSec(55) * Balance.LungerPay, Sense = 700, Range = 640, Close = 230, Far = 360,
                      CdMin = Balance.Sec(1.6), CdVar = Balance.Sec(0.6), Dmg = Middle.LobDmg,
                      PShotSpeed = Balance.PerSec(400), PShotRadius = 7 },
            // burrower: the toughest of the five, because it is only hittable between dives
            new BodyRow { Mass = 1.2, Radius = 15, Art = 2, Bar = 26, Hp = 9 * Tough, Armour = Armour },
            // sprayer: has to come close for its short cone; weak shots, many of them
            new BodyRow { Mass = 0.9, Radius = 14, Art = 2, Bar = 26, Hp = 6 * Tough, Armour = Armour,
                      Base = Balance.PerSec(90) * Balance.LungerPay, Sense = 560, Range = 360, Close = 110, Far = 190,
                      CdMin = Balance.Sec(1.4), CdVar = Balance.Sec(0.6), Dmg = 1,
                      PShotSpeed = Balance.PerSec(400), PShotRadius = 4 },
            // turret egg: never moves (mass is so a shove barely does), so it gets the most health
            new BodyRow { Mass = 40, Radius = 14, Art = 2, Bar = 26, Hp = 12 * Tough, Armour = Armour,
                      Base = 0, Sense = 100000, CdMin = Balance.Sec(2.6), CdVar = Balance.Sec(0.8), Dmg = 1,
                      PShotSpeed = Balance.PerSec(190), PShotRadius = 5 },
        };

        public static BodyRow Of(BodyKind kind) => Rows[(int)kind];

        // ------------------------------------------------------------- the Brunch pack

        /// <summary>How many bodies a Brunch pack is, and how likely each size is. [h:Bodies-10]</summary>
        public static readonly int[] BrunchPack = { 4, 5, 6, 7, 8 };
        public static readonly double[] BrunchWeight = { 0.30, 0.25, 0.20, 0.15, 0.10 };

        /// <summary>How often a room contains a pack at all, before the depth ladder touches it.</summary>
        public const double BrunchChance = 0.45;

        /// <summary>The enemy-mix dials for one area: which bodies a normal slot rolls, how often a room carries a heavy, and the... [h:Bodies-11]</summary>
        public struct AreaMix
        {
            /// <summary>Probability a normal slot is a lunger; the rest are shooters.</summary>
            public double Lunger;

            /// <summary>Probability a room with enough bodies carries one gunner.</summary>
            public double Heavy;

            /// <summary>The base the pack chance ramps from, before the depth ladder's step.</summary>
            public double Brunch;
        }

        /// <summary>The mix for an area. [h:Bodies-12]</summary>
        public static AreaMix MixFor(Area area)
        {
            switch (area)
            {
                case Area.Area2: return new AreaMix { Lunger = 0.45, Heavy = 0.60, Brunch = 0.50 };
                case Area.Area3: return new AreaMix { Lunger = 0.55, Heavy = 0.50, Brunch = 0.60 };
                case Area.Final: return new AreaMix { Lunger = 0.50, Heavy = 0.65, Brunch = 0.55 };
                default: return new AreaMix { Lunger = 0.50, Heavy = 0.55, Brunch = BrunchChance };
            }
        }

        /// <summary>A pack smaller than this is a knot rather than a wall. [h:Bodies-13]</summary>
        public const int BrunchWallMin = 3;
    }
}
