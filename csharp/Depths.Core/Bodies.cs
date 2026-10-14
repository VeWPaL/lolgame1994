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
                      PShotSpeed = Balance.PerSec(300), PShotRadius = 6 },
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
            new BodyRow { Mass = 40, Radius = 18, Art = 2, Bar = 30, Hp = 12 * Tough, Armour = Armour,
                      Base = 0, Sense = 100000, CdMin = Balance.Sec(2.6), CdVar = Balance.Sec(0.8), Dmg = 1,
                      PShotSpeed = Balance.PerSec(190), PShotRadius = 5 },
        };

        public static BodyRow Of(BodyKind kind) => Rows[(int)kind];

        // the archetype's own spawn windows, as ported (24 and 135 ticks at the JS rate); a live spawn's are longer
        public static double ArchNoticeWindow => Balance.SecF(4.0 / 35);
        public static double ArchIdleWindow => Balance.SecF(9.0 / 14);

        /// <summary>Builds a body. The depth ladder is read HERE and nowhere else on this type, and it is read as a function of... [h:Bodies-7]</summary>
        public static Body Spawn(BodyKind kind, int floor, Rng rng)
        {
            BodyRow c = Of(kind);
            double tough = Balance.DepthTough(floor);
            double rate = Balance.DepthRate(floor);

            var b = new Body
            {
                Kind = kind,
                Type = kind.ToString().ToLowerInvariant(),
                Art = c.Art,
                Bar = c.Bar,
                Mass = c.Mass,
                Radius = c.Radius,
                MaxHp = c.Hp * tough,
                Armour = c.Armour,

                // transients, drawn from jitter so they never touch the run stream
                NoticeTimer = (int)(rng.Jitter() * ArchNoticeWindow),
                IdleTimer = (int)(rng.Jitter() * ArchIdleWindow),
                IdleDir = new[] { rng.Jitter() * 2 - 1, rng.Jitter() * 2 - 1 },
                Flank = rng.Jitter() * 6.283185307179586,
            };

            if (c.Walk.HasValue)
            {
                // A walker. The depth rate does NOT scale a walker's top speed: the rate dial is the reaction-time dial, and a... [h:Bodies-8]
                b.WalkSpeed = c.Walk;
                b.RunSpeed = c.Run;
                b.CurSpeed = c.Walk.Value;
                b.AggroTimer = 0;
                b.LungeState = "approach";
            }
            else
            {
                // A ranged body. depthRate scales BOTH its approach speed and its cadence, and the
                // bound on that dial is the reason a gunner never outruns the player.
                b.Speed = c.Base * Balance.PressureRate * rate;
                b.Sense = c.Sense;
                b.Range = c.Range;
                b.Close = c.Close;
                b.Far = c.Far;
                b.CdMin = c.CdMin / Balance.PressureRate / rate;
                b.CdVar = c.CdVar / Balance.PressureRate / rate;
                b.Dmg = c.Dmg;
                b.PShotSpeed = c.PShotSpeed;
                b.PShotRadius = c.PShotRadius;
                b.ShootCd = b.CdMin + rng.Jitter() * b.CdVar;
                b.AggroTimer = 0;
            }

            return b;
        }

        // ------------------------------------------------------------------ the trait

        /// <summary>Which answer a gun gets, and how reliably. [h:Bodies-9]</summary>
        public static Trait TraitFor(BodyKind gun, Rng rng)
        {
            Trait t;
            double weight;
            switch (gun)
            {
                case BodyKind.Lunger: t = Trait.Close; weight = 0.50; break;
                case BodyKind.Brunch: t = Trait.Hold; weight = 0.70; break;
                case BodyKind.Shooter: t = Trait.Hold; weight = 0.62; break;
                default: t = Trait.Close; weight = 0.50; break;
            }
            return rng.Run() < 0.5 * weight ? t : Trait.None;
        }

        public const double TraitHoldScale = 1.45;    // shift the band outward by this much
        public const double TraitCloseScale = 0.72;   // ...or inward by this much
        public const double TraitFarCeil = 0.78;      // far must stay under sense*this
        public const double TraitFarFloor = 90;       // ...and above this
        public const double TraitBandMin = 40;        // a band narrower than this is a jitter, not a stance

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

        /// <summary>Applies a trait to a body, in place. [h:Bodies-14]</summary>
        public static Trait ApplyTrait(Body b, Trait t)
        {
            if (t == Trait.None || !b.CanTakeTrait) return Trait.None;

            double k = t == Trait.Hold ? TraitHoldScale : TraitCloseScale;
            double cap = b.Sense!.Value * TraitFarCeil;
            double far = b.Far!.Value * k;
            double close = b.Close!.Value * k;

            if (far > cap) far = cap;
            if (far < TraitFarFloor) far = TraitFarFloor;
            if (far - close < TraitBandMin) close = far - TraitBandMin;

            b.Far = far;
            b.Close = close;
            b.Trait = t;
            return t;
        }
    }
}
