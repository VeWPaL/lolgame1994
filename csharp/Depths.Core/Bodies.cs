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

    /// <summary>
    /// The body table, the spawner, and the build-dependent trait.
    ///
    /// <para>
    /// Every number in <see cref="Rows"/> was read out of the running JavaScript rather than
    /// transcribed from its source expressions, because a transcription error in a balance table is
    /// invisible until a fight quietly feels wrong. The source forms are in the comments so the
    /// relationship is not lost - the point of a table is that the numbers are in ONE place, and that
    /// includes knowing what each one is a multiple of.
    /// </para>
    /// </summary>
    public static class Bodies
    {
        /// <summary>
        /// The knobs the table is written in multiples of, so the relationships survive the port.
        /// </summary>
        public const double Tough = 1.35;      // the global HP multiplier
        public const double ShotDmg = 2;       // whole HP since 2026-10-06 (JS: 1.8): one heart; a gunner's is two
        public const double Armour = 0.66;     // per-hit multiplier, so it scales every pellet

        // Every row names its Armour explicitly; a forgotten one reads 0.0. [h:Bodies-1]
        // Read from Balance, which the constant audit checks against the game. [h:Bodies-2]
        public static double BrunchWalk => Balance.BrunchWalkSpeed;
        public static double BrunchRun => Balance.BrunchRun;

        /// <summary>
        /// The table, rebuilt only when the tick rate changes (its speeds and cadences are per tick) - and built rather than written out, so the
        /// derived numbers are computed the same way the JavaScript computes them instead of being
        /// frozen at whatever they happened to be when this was typed.
        ///
        /// <para>
        /// The nullable fields are the same conditional-field trick the JavaScript uses, for the same
        /// reason: a walker has no standoff band, and pretending otherwise with a sentinel number is
        /// how two code paths end up disagreeing about what kind of body they are holding.
        /// </para>
        /// </summary>
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

            // boss: the Warden. HP is sized from measured weapon DPS, which is the only way to size
            // a health bar. 343*TOUGH rather than 520*TOUGH because the boss now obeys ARMOUR like
            // every other body, and holding the effective pool fixed across that change is what keeps
            // it a repair to the rule rather than a 1.52x pacing change to every boss fight in the game
            new BodyRow { Mass = 4, Radius = 28, Art = 4, Bar = 40, Hp = 343 * Tough, Armour = Armour,
                      Base = Balance.PerSec(126) * Balance.LungerPay,
                      Walk = 0.42 * Balance.PlayerMove, Run = 0.72 * Balance.PlayerMove },
        };

        public static BodyRow Of(BodyKind kind) => Rows[(int)kind];

        // the archetype's own spawn windows, as ported (24 and 135 ticks at the JS rate); a live spawn's are longer
        public static double ArchNoticeWindow => Balance.SecF(4.0 / 35);
        public static double ArchIdleWindow => Balance.SecF(9.0 / 14);

        /// <summary>
        /// Builds a body. The depth ladder is read HERE and nowhere else on this type, and it is read
        /// as a function of the floor rather than as a field on the run, because a difficulty that
        /// reads the player is a difficulty that measures the player.
        /// </summary>
        ///
        /// <param name="kind">Which body to build.</param>
        /// <param name="floor">
        /// The depth. Read through <see cref="Balance.DepthTough"/> and
        /// <see cref="Balance.DepthRate"/>, which take nothing else - the depth-only rule enforced by
        /// signature rather than by a check that could be deleted.
        /// </param>
        /// <param name="rng">
        /// The source to draw the body's spawn jitter from - the <c>jitter</c> stream, never
        /// <c>run</c>. A body is part of what the run IS, but the jitter in its spawn - notice delay,
        /// dodge cooldown - is not, and pulling it from the run stream would let a cosmetic roll
        /// change the dungeon.
        /// </param>
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
                // A walker. The depth rate does NOT scale a walker's top speed: the rate dial is the
                // reaction-time dial, and a body that outruns the player cannot be responded to.
                // Lungers and Brunch keep their speeds at every depth.
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

        /// <summary>
        /// Which answer a gun gets, and how reliably. The weight is not difficulty - it is how
        /// reliably the answer works, which decides how many bodies in a room take it. The ceilings
        /// stop a stack: a room of traited bodies must all still stand in roughly the same place.
        ///
        /// <para>
        /// The pairing is the design. A close gun wants to be standing next to something, so it is
        /// answered by standing further out than it is worth. A single-target gun with a long reload
        /// wants a target that holds still, so it is answered by walking in.
        /// </para>
        /// </summary>
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

        /// <summary>
        /// How many bodies a Brunch pack is, and how likely each size is.
        ///
        /// <para>
        /// The sizes ascend and so do the weights, deliberately: a big pack is the rarer event, so a
        /// room with eight is something to remember rather than the default. The weights sum to exactly
        /// 1, which is why the roll can walk the table subtracting as it goes and fall off the end into
        /// the smallest size.
        /// </para>
        /// </summary>
        public static readonly int[] BrunchPack = { 4, 5, 6, 7, 8 };
        public static readonly double[] BrunchWeight = { 0.30, 0.25, 0.20, 0.15, 0.10 };

        /// <summary>How often a room contains a pack at all, before the depth ladder touches it.</summary>
        public const double BrunchChance = 0.45;

        /// <summary>
        /// The enemy-mix dials for one area: which bodies a normal slot rolls, how often a room
        /// carries a heavy, and the base pack chance the depth ladder starts from.
        ///
        /// <para>
        /// This is the ONLY thing an area changes. The ladder itself (<see cref="Balance.DepthTough"/>,
        /// <see cref="Balance.DepthRate"/>, <see cref="Balance.DepthBodies"/>,
        /// <see cref="Balance.DepthPack"/>) is the same shape in every area - an area is an identity,
        /// not a harder or easier version of the climb.
        /// </para>
        /// </summary>
        public struct AreaMix
        {
            /// <summary>Probability a normal slot is a lunger; the rest are shooters.</summary>
            public double Lunger;

            /// <summary>Probability a room with enough bodies carries one gunner.</summary>
            public double Heavy;

            /// <summary>The base the pack chance ramps from, before the depth ladder's step.</summary>
            public double Brunch;
        }

        /// <summary>
        /// The mix for an area. Area1 reproduces the measured numbers the port pinned before areas
        /// existed (lunger 0.5, heavy 0.55, brunch 0.45 == <see cref="BrunchChance"/>), so a default
        /// Area1 plan is byte-identical to the pre-area plan the 127 parity rows were read from.
        ///
        /// <para>
        /// Area2/Area3/Final are explicit guesses, marked as such: they are design intent awaiting a
        /// playtest, not values read out of the running JavaScript. Area1 is the only row that is
        /// measured. Do not retune an area by feel - change it, then re-measure.
        /// </para>
        /// </summary>
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

        /// <summary>
        /// A pack smaller than this is a knot rather than a wall.
        ///
        /// <para>
        /// The formation rule asks whether the bodies sharing one pack id number at least this many, so
        /// this is the line between "arrives as a crowd" and "arrives as a wall you have to go around".
        /// It is asserted in the spawn tests because a pack that can never reach it is a documented
        /// mechanic that is dead in the game - which is exactly what finding #1 was, and it shipped
        /// because the only wall anyone had seen was the boss's, which uses a fixed id and so was
        /// unaffected.
        /// </para>
        /// </summary>
        public const int BrunchWallMin = 3;

        /// <summary>
        /// Applies a trait to a body, in place.
        ///
        /// <para>
        /// RANGED BODIES ONLY, and that is not a preference - it is the bug this shape exists to
        /// prevent. The first version had a "close faster" trait that set WalkSpeed, intending to make
        /// a shooter press in. It did not: writing WalkSpeed onto a body that lacked it CONVERTED it
        /// into a walker, and from that tick its whole ranged kit stopped running. So the only fields
        /// touched here are Far and Close, and <see cref="Body.CanTakeTrait"/> is the guard.
        /// </para>
        ///
        /// <para>
        /// Both are shifted by the SAME factor, which is the only safe version: the band keeps its
        /// width, so a body does not start dithering, and the change is a place rather than a
        /// personality. Scaling only Far would widen the band as a side effect, which is a different
        /// and less legible thing.
        /// </para>
        /// </summary>
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
