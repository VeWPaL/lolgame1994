using System.Collections.Generic;

namespace Depths
{
    /// <summary>The mutable bodies the tick drives: the player, the enemies, the projectiles, and the transient pickups. [h:Actors-1]</summary>
    public sealed class Player
    {
        public double x, y, vx, vy, kvx, kvy;
        /// <summary>Where the last <see cref="Interp.Snap"/> found it (the screen draws between that and now), and that
        /// snap's stamp; nothing in the simulation reads them.</summary>
        public double px, py;
        public int snap;
        public double hp, maxHp;
        public double armor;   // fractional: armour absorbs a 1.8 shell from 2 and keeps 0.2, as in the game
        /// <summary>The regenerating heart (C# only): drained after armour, before red; refills in a live fight once
        /// regenHeartT (fight ticks since the last hit) reaches RegenDelay, and fully on a won fight.</summary>
        public double regenHeart, regenHeartMax;
        public int regenHeartT;
        /// <summary>Weapon cooldowns in ticks, and iframes in ticks. [h:Actors-2]</summary>
        public double cooldown;
        public int altCooldown, iframes;
        public double momentum;
        public int weaponIdx;
        public string altMode = "blast";
        public bool altHeld;   // the alt button was down last tick: a held button is one click (Balance.Tuned)
        public bool hasSilver, hasGold;
        public double lagX, lagY;
    
        /// <summary>The player's collision radius, and the second term in a pickup's touch test (pk.r + player.r). [h:Actors-3]</summary>
        public double r = 13;

        /// <summary>Ticks of blink charge owed, granted as a fraction by a cleared room. [h:Actors-4]</summary>
        public double blinkRegen;

        // ---- the movement fields, added for the tickPlayer port ---- Every default below was READ OUT OF THE RUNNING... [h:Actors-5]

        /// <summary>Base movement speed, before the momentum bonus and before firing slow-motion. [h:Actors-6]</summary>
        public double speed = 0.935 * Balance.PlayerMove;   // px per tick, fixed when the body is made

        /// <summary>The firing slow-motion multiplier, eased toward 1 every tick. [h:Actors-7]</summary>
        public double slowMult = 1;

        /// <summary>How much the current shot slows the player, 0 to 1. Recovers every tick.</summary>
        public double shootSlow;

        /// <summary>Ticks of walk-cycle phase. Scaled by distance travelled, not by time.</summary>
        public double anim;

        /// <summary>The direction the player is actually travelling, smoothed. [h:Actors-8]</summary>
        public double trendVx, trendVy;

        /// <summary>The player's direction as a slower, noisier estimate - what the player BELIEVES they are doing. [h:Actors-9]</summary>
        public double beliefVx, beliefVy;

        /// <summary>The last committed movement direction, as a unit vector. [h:Actors-10]</summary>
        public double dirX, dirY;

        /// <summary>How much the player is currently swerving, 0 to 1. [h:Actors-11]</summary>
        public double swerve;

        /// <summary>Ticks of post-blink speed boost left, and the direction it was thrown in.</summary>
        public int boost;
        public double boostX, boostY;

        /// <summary>Ticks of muzzle-flash left. Decremented here so the flash can outlive the shot.</summary>
        public int muzzleTimer;
}

    /// <summary>One enemy. The contract's fields first, then the cast and cooldown state the
    /// committed-shot bookkeeping reads and writes.</summary>
    public sealed class Enemy
    {
        public BodyKind kind;
        public double x, y;
        /// <summary>Where the last <see cref="Interp.Snap"/> found it (the screen draws between that and now), and that
        /// snap's stamp; nothing in the simulation reads them.</summary>
        public double px, py;
        public int snap;
        public double hp, maxHp;
        /// <summary>Which pack this body belongs to, and its slot within it. [h:Actors-12]</summary>
        public int? packId;
        public int packSlot;
        public int castT;
        public bool castReady;
        public int phase, moveT;

        /// <summary>'idle', 'volley', 'sweep', 'wall'. A string like the original, which switches
        /// on the literal.</summary>
        public string move = "idle";

        // --- the committed shot: fireCommittedShot reads and writes these (src/60-tick.js:28-35)
        public double castAim;
        public double cdMin, cdVar, shootCd;
        public double cdCarry;   // off the JS rate: the cooldown's overrun when the cast began, less CastLag, owed to the next cooldown
        public double pspd, pr, dmg;

        // --- boss move cadence: StepBoss's bag pick and cooldown (src/60-tick.js:159-180)
        public double bossCd;
    
        /* ---- the pack and shield fields, added for the body-phase port ---- */

        /// <summary>Which pack this body belongs to, and its slot within it. [h:Actors-13]</summary>
        public Enemy? shieldTarget;

        /// <summary>The Brunch bodies currently guarding THIS body, rebuilt every tick by the body phase. [h:Actors-14]</summary>
        public List<Enemy>? shieldGuardFor;
        /// <summary>The list <see cref="shieldGuardFor"/> borrows when set: the guard lists are rebuilt every tick, so this keeps that allocation-free.</summary>
        internal List<Enemy>? guardBuf;

        // The fields the projectile phase reads and writes (src/60-tick.js tickProjectiles).
        public double r, armour = 1, mass = 1;
        public int noticeTimer, slowT;
        public double hitFlash, stun;   // fractional in the game: a hook field stuns 3 x its power and flashes by it
        public int? aggroTimer;   // every spawned body has one; null models the game's `undefined` guard
        public bool alerted;
        public double pursuit;

        // Movement (src/30-enemies.js stepLunge, idleWander; 40-combat.js knockEnemy).
        public double kvx, kvy, curSpeed, walkSpeed, runSpeed, flank, anim;
        public double vx, vy;   // a Brunch eases its velocity toward its slot rather than stepping
        public string lungeState = "approach";
        public int lungeT, lungeCd;
        public double lungeDx, lungeDy, lungeLen;
        /// <summary>An eased dash in progress (Balance.TuneEase): its length in ticks (0: none, or the flat JS dash) and
        /// the distance it covers; lungeT counts it down.</summary>
        public int dashN;
        public double dashLen;
        public int idleTimer;
        public double idleDirX, idleDirY;

        // Ranged bodies (shooter, gunner): the standoff band, walking speed, sight, and the dodge.
        public double sense, close, far, speed;
        public int dodgeCd;

        // The Warden.
        public int volleyLeft, volleyT, wallT;
        public bool sweepDone;
        public List<Enemy>? wallBodies;
        public int? wallAge;   // a called statue's age; null until its first sweep tick
        // the hook's resistance: which field last charged this body, how many hooks it has taken, how
        // long since it was last in one, and the power the current field acts with
        public int hookMark, hookStacks, hookCalm;
        /// <summary>Hunter's Mark (placeholder, C# only): ticks left marked; a marked body takes MarkVuln x damage.</summary>
        public int markT;
        /// <summary>What a player hit is multiplied by: armour, the mark while it lasts, and the tuned damage cut (Balance.PlayerDmg).</summary>
        public double Vuln => (markT > 0 ? armour * Balance.MarkVuln : armour) * Balance.PlayerDmg;
        public double hookPower;
        public int trait;      // 0, or the band trait a ranged spawn rolled (TRAIT_HOLD 1, TRAIT_CLOSE 2)
        public int lifeT;              // ticks this body has been stepped (the room's ready freeze does not count)
        public bool startleSpent;      // a Lunger's once-a-life startled dodge is used (Balance.Tuned)
        public bool rushing;           // a guarding Brunch rushing the player instead of holding its slot (Balance.Tuned)
        public bool buried;            // a Burrower underground: no shell, blast, field, contact or shove reaches it
        public double markX, markY;    // where it will surface
        public double heading;         // a Burrower's travel direction (Middle.Steer)
        public double digTrust, digOffX, digOffY;   // a dive's share of the lead and its slop, rolled as it goes under
        /// <summary>A turret egg's form (Eggs.cs): fixed, circling <see cref="eggHost"/> at eggA/eggR, or riding
        /// <see cref="rail"/> (points x0,y0,x1,y1...) at railS px along it, railDir +1/-1, round it when railLoop.</summary>
        public EggForm eggForm;
        public Enemy? eggHost;
        public double eggA, eggR;
        public double[]? rail;
        public double railS, railLen;
        public int railDir = 1;
        public bool railLoop;

        /// <summary>A live body of a kind at a point, with the table's radius, armour, mass and health.</summary>
        public static Enemy Of(BodyKind kind, double x, double y)
        {
            var row = Bodies.Of(kind);
            // spawnEnemy scales a ranged body by the tempo: faster walk, shorter cooldowns (measured: shooter cdMin 105 -> 70)
            return new Enemy { sense = row.Sense ?? 0, close = row.Close ?? 0, far = row.Far ?? 0,
                               speed = (row.Base ?? 0) * Balance.TempoRate, cdMin = (row.CdMin ?? 0) / Balance.TempoRate,
                               cdVar = (row.CdVar ?? 0) / Balance.TempoRate, pspd = row.PShotSpeed ?? 0, pr = row.PShotRadius ?? 0,
                               dmg = (row.Dmg ?? 0) * (Balance.JsReference ? Balance.JsShotDmg / Bodies.ShotDmg : 1), kind = kind, x = x, y = y, r = row.Radius, armour = row.Armour, mass = row.Mass,
                               hp = row.Hp, maxHp = row.Hp, walkSpeed = row.Walk ?? 0, runSpeed = row.Run ?? 0, curSpeed = row.Walk ?? 0, aggroTimer = kind == BodyKind.Boss ? 9999 : 0 };   // measured: every body has one; the boss starts committed
        }
}

    /// <summary>A shell in flight. Widened from seven fields to the 23 the projectile pass in src/60-tick.js actually reads... [h:Actors-15]</summary>
    public sealed class Projectile
    {
        // --- motion and identity
        public double x, y, vx, vy;
        /// <summary>Where the last <see cref="Interp.Snap"/> found it (the screen draws between that and now), and that
        /// snap's stamp; nothing in the simulation reads them.</summary>
        public double px, py;
        public int snap;
        public double r, dmg;
        public bool friendly, heavy;

        // --- read by the projectile pass (src/60-tick.js:592-741)
        /// <summary>Ticks since the shell was spawned. Read by the pass.</summary>
        public int age;

        /// <summary>True for the alt/weapon-thrown shell, which flies to a fixed point, passes THROUGH bodies while phase is set... [h:Actors-16]</summary>
        public bool alt;

        /// <summary>The alt shell's phase: while true it ignores bodies entirely. [h:Actors-17]</summary>
        public bool phase;

        /// <summary>Fixed destination for an alt shell, in world px.</summary>
        public double tx, ty;

        /// <summary>The direction of travel, and the MOUTH it left from. [h:Actors-18]</summary>
        public double dx, dy, ox, oy;


        /// <summary>Per-tick step length, used by the alt shell's "stop ON the point" test.</summary>
        public double speed;

        /// <summary>Remaining pierce charges; decremented on each body hit.</summary>
        public int pierce;

        /// <summary>Damage multiplier, multiplied down by PIERCE_FALLOFF on each body. [h:Actors-19]</summary>
        public double scale = 1;   // every bolt the game fires carries scale 1; 0 would silently do no damage

        /// <summary>The bodies this shell has already counted, so it cannot hit one twice.</summary>
        public List<Enemy>? hit;

        /// <summary>The shell's colour, for the view.</summary>
        public string color = "";

        /// <summary>Who fired it - the body, so a body cannot shoot itself.</summary>
        public Enemy? owner;

        /// <summary>Which WEAPON the alt shell was cast with. [h:Actors-20]</summary>
        public string mode = "";

        /// <summary>The weapon's falloff band, carried by the shell (null fNear: no falloff).</summary>
        public double? fNear;
        public double fFar, fMin = 1;

        // the middle enemies' shells (Middle.cs): a lob flies to (tx, ty) over flightMax ticks and only
        // lands there, hitting within blastR; a shell with splitAt bursts into shards that far from (ox, oy)
        public bool lob;
        public int flight, flightMax;
        public double blastR, splitAt;

        /// <summary>The gun a player shell came from (Weapons.All index), or -1: a body can resist one gun (Balance.BrunchBeamTaken).</summary>
        public int gun = -1;
    }

    /// <summary>A pickup in the current room, widened for the tickRoom port. [h:Actors-21]</summary>
    public sealed class Pickup
    {
        public double x, y;
        /// <summary>Collection radius, in the original's units. See the note above.</summary>
        public double r = 16;
        public string kind = "";
        /// <summary>Weapon index for a <c>weapon</c> pickup; the previous index once swapped.</summary>
        public int w;
        /// <summary>Item id for an <c>item</c> pickup.</summary>
        public string id = "";
        /// <summary>Charges for an <c>item</c> pickup.</summary>
        public int? charges;   // an item's charges as it lies here; null: the definition's own
        /// <summary>Suppress re-collection until the player steps off. See the note above.</summary>
        public bool hold;

        /// <summary>The weapon pickup: grants <paramref name="index"/> and parks the old one here.</summary>
        public static Pickup Weapon(double x, double y, int index, double r = 16) =>
            new Pickup { x = x, y = y, kind = "weapon", w = index, r = r };

        /// <summary>A plain pickup with a radius, for the rewards the room phase spawns.</summary>
        public static Pickup Of(string kind, double x, double y, double r) =>
            new Pickup { x = x, y = y, kind = kind, r = r };
    }
}
