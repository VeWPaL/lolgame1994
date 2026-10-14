namespace Depths
{
    /// <summary>Which kind of body this is. The kind is not cosmetic: the tick branches on it.</summary>
    public enum BodyKind
    {
        Lunger,
        Brunch,
        Shooter,
        Gunner,
        Boss,
        // the middle enemies (Middle.cs; C# only, lab-only until they join the pool). Appended: Bodies.Rows is indexed by kind
        Splitter,
        Mortar,
        Burrower,
        Sprayer,
        Egg,
    }

    /// <summary>A build-dependent trait. [h:Body-1]</summary>
    public enum Trait
    {
        /// <summary>No trait. Most bodies, most rooms.</summary>
        None = 0,

        /// <summary>Stands further out than the gun in hand is worth. [h:Body-2]</summary>
        Hold = 1,

        /// <summary>Walks in. The answer to a single-target gun with a long reload: the player has to hold a moving body at knife... [h:Body-3]</summary>
        Close = 2,
    }

    /// <summary>One body in the world. [h:Body-4]</summary>
    public sealed class Body
    {
        // ---- identity and place
        public BodyKind Kind;
        public string Type = "";
        public double X, Y;
        public int Art;
        public int Bar;

        // ---- the table row, and the per-body state on top of it
        public double Mass;
        public double Radius;
        public double MaxHp;
        public double Armour;

        /// <summary>Present on a walker and absent on a ranged body. [h:Body-5]</summary>
        public double? WalkSpeed;
        public double? RunSpeed;
        public double CurSpeed;

        /// <summary>Present only on a ranged body: its standoff band and its sense radius.</summary>
        public double? Speed;
        public double? Sense;
        public double? Range;
        public double? Close;
        public double? Far;

        public double? CdMin;
        public double? CdVar;
        public double? ShootCd;
        public double? Dmg;
        public double? PShotSpeed;
        public double? PShotRadius;

        // ---- the lunge, on walkers only
        public string LungeState = "";
        public int LungeT, LungeCd;
        public double LungeDx, LungeDy, LungeFromX, LungeFromY;
        public double LungeChargeFx, LungeTrail;

        // ---- the cast, on ranged bodies only
        public int CastT;
        public bool CastReady;
        public double CastAim;

        // ---- shared transient state
        public double KvX, KvY;
        public int Stun, SlowT, AggroTimer, NoticeTimer, DodgeCd, Pursuit;
        public bool Alerted;
        public double HitFlash;
        public double[] IdleDir = new double[2];
        public int IdleTimer;
        public int Anim;
        public double Flank;

        // ---- the trait
        public Trait Trait = Trait.None;

        /// <summary>THE type test. The tick uses this to choose between the walker branch and the ranged one, so it is a named... [h:Body-6]</summary>
        public bool HasWalkSpeed => WalkSpeed.HasValue;

        /// <summary>The trait is only ever meaningful on a body that HAS a standoff band. [h:Body-7]</summary>
        public bool CanTakeTrait => !HasWalkSpeed && Far.HasValue;

        /// <summary>The standoff the body actually holds, derived fresh rather than stored. [h:Body-8]</summary>
        public double Standoff(double roomPressure, double closure) =>
            Far!.Value - (Far!.Value - Close!.Value) * roomPressure * closure;
    }
}
