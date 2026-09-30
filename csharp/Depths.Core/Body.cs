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
    }

    /// <summary>
    /// A build-dependent trait. It may change WHERE a fight happens and never HOW HARD it is - the
    /// same rule the depth ladder obeys, pointed the other way.
    /// </summary>
    public enum Trait
    {
        /// <summary>No trait. Most bodies, most rooms.</summary>
        None = 0,

        /// <summary>
        /// Stands further out than the gun in hand is worth. The answer to a close gun: the fight
        /// moves to a distance the gun's falloff punishes.
        /// </summary>
        Hold = 1,

        /// <summary>
        /// Walks in. The answer to a single-target gun with a long reload: the player has to hold a
        /// moving body at knife range on a cadence chosen for a target across the room.
        /// </summary>
        Close = 2,
    }

    /// <summary>
    /// One body in the world.
    ///
    /// <para>
    /// THE FIELD OPTIONALITY IS LOAD-BEARING, and it is the first thing in the port that is not a
    /// plain transcription. The JavaScript builds a body with <c>Object.assign</c> and then ADDS
    /// fields conditionally: a walker gets <c>walkSpeed</c> and a ranged body does not. The tick then
    /// decides what kind of body it is holding with <c>e.walkSpeed!==undefined</c>.
    /// </para>
    ///
    /// <para>
    /// That test is load-bearing and it is also the single most dangerous line in the game. Give a
    /// ranged body a <c>walkSpeed</c> and it does not become a faster shooter - it becomes a lunger,
    /// and its shell, its cadence, its muzzle prediction and its standoff rule all stop running, with
    /// nothing anywhere reporting why. A trait did exactly that during development and the suite
    /// caught it, but only because someone noticed that "make it close faster" is a speed change and
    /// a speed change is difficulty.
    /// </para>
    ///
    /// <para>
    /// So <see cref="HasWalkSpeed"/> is a named property rather than a null check scattered through
    /// the simulation, and it exists for the same reason <c>doorRect</c> exists in the JavaScript:
    /// a value that two places must agree on should have one name.
    /// </para>
    /// </summary>
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

        /// <summary>
        /// Present on a walker and absent on a ranged body. DO NOT SET THIS OUTSIDE THE SPAWNER.
        /// <see cref="HasWalkSpeed"/> is the only sanctioned way to ask.
        /// </summary>
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

        /// <summary>
        /// THE type test. The tick uses this to choose between the walker branch and the ranged one,
        /// so it is a named property rather than a null check written out at each call site.
        /// </summary>
        public bool HasWalkSpeed => WalkSpeed.HasValue;

        /// <summary>
        /// The trait is only ever meaningful on a body that HAS a standoff band. A walker has nothing
        /// to move, so asking one to hold further out is a question it cannot answer - and the only
        /// way the previous version answered it was by writing <see cref="WalkSpeed"/>, which is the
        /// bug documented on that field.
        /// </summary>
        public bool CanTakeTrait => !HasWalkSpeed && Far.HasValue;

        /// <summary>
        /// The standoff the body actually holds, derived fresh rather than stored. This is the whole
        /// mechanism a trait moves, and it is deliberately not a field: a cached standoff is a
        /// second number that has to be kept in step with the two it is derived from.
        /// </summary>
        public double Standoff(double roomPressure, double closure) =>
            Far!.Value - (Far!.Value - Close!.Value) * roomPressure * closure;
    }
}
