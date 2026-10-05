using System.Collections.Generic;

namespace Depths
{
    /// <summary>
    /// The mutable bodies the tick drives: the player, the enemies, the projectiles, and the
    /// transient pickups. Classes rather than structs because they are mutated tick over tick, and
    /// matching <c>Body</c>'s style for the same reason - a struct copy of a body that is being
    /// knocked back would hand the knockback to a detached duplicate.
    ///
    /// <para>
    /// Field lists follow the interface contract. Where a method below needs a number the contract
    /// does not name - <c>Enemy.cdMin</c>, <c>Enemy.shootCd</c>, <c>Enemy.pspd</c> and so on, all
    /// of which <c>src/60-tick.js:28-35</c> writes on the body - the field lives here with a
    /// comment, rather than being smuggled through a static or reconstructed from elsewhere. The
    /// JavaScript body has these too; <c>spawnEnemy</c> sets them.
    /// </para>
    /// </summary>
    public sealed class Player
    {
        public double x, y, vx, vy, kvx, kvy;
        public double hp, maxHp;
        public int armor;
        /// <summary>
        /// Weapon cooldowns in ticks, and iframes in ticks.
        /// <para>
        /// <c>cooldown</c> is a <c>double</c> on purpose. Read out of the running game, the Arcane
        /// Beam's cadence is 18.2 ticks - not 18 - and the room phase does
        /// <c>Math.min(cooldown, WEAPONS[w].cooldown)</c> on it. An <c>int</c> field would either
        /// truncate the table to 18 (a silent balance change no whole-number test would catch) or
        /// force a rounding decision at every call site. <c>altCooldown</c> and <c>iframes</c> are
        /// genuinely whole and stay <c>int</c>.
        /// </para>
        /// </summary>
        public double cooldown;
        public int altCooldown, iframes;
        public double momentum;
        public int weaponIdx;
        public string altMode = "blast";
        public bool hasSilver, hasGold;
        public double lagX, lagY;
    
        /// <summary>
        /// The player's collision radius, and the second term in a pickup's touch test
        /// (<c>pk.r + player.r</c>). Read out of the running game as 13. It is separate from
        /// <c>Balance.PlayerHitR</c> (10), which is the SHELL hit radius - the pickup is easier to
        /// walk into than a shell is to be hit by, and folding the two together would shrink every
        /// pickup's reach.
        /// </summary>
        public double r = 13;

        /// <summary>
        /// Ticks of blink charge owed, granted as a fraction by a cleared room. A double, because
        /// the refund is <c>BlinkRecharge * 0.5</c> = 367.5 and the original keeps the fraction.
        /// </summary>
        public double blinkRegen;
}

    /// <summary>One enemy. The contract's fields first, then the cast and cooldown state the
    /// committed-shot bookkeeping reads and writes.</summary>
    public sealed class Enemy
    {
        public BodyKind kind;
        public double x, y;
        public double hp, maxHp;
        public int packId, packSlot;
        public int castT;
        public bool castReady;
        public int phase, moveT;

        /// <summary>'idle', 'volley', 'sweep', 'wall'. A string like the original, which switches
        /// on the literal.</summary>
        public string move = "idle";

        // --- the committed shot: fireCommittedShot reads and writes these (src/60-tick.js:28-35)
        public double castAim;
        public double cdMin, cdVar, shootCd;
        public double pspd, pr, dmg;

        // --- boss move cadence: StepBoss's bag pick and cooldown (src/60-tick.js:159-180)
        public double bossCd;
    }

    /// <summary>
    /// A shell in flight. Widened from seven fields to the 23 the projectile pass in
    /// <c>src/60-tick.js</c> actually reads (lines 592-741), measured rather than guessed - the
    /// previous version declared only x, y, vx, vy, r, dmg, friendly, heavy, so sixteen of the
    /// fields the pass depends on did not exist on the port at all.
    /// </summary>
    /// <remarks>
    /// WHAT IS HERE AND WHAT IS DELIBERATELY NOT. Shells are CONSTRUCTED with 29 distinct fields
    /// across 40-combat.js, 50-run.js, 60-tick.js and 80-ui.js. Seven of those are never read by the
    /// projectile pass - <c>fNear</c>, <c>fMid</c>, <c>fFar</c> (the damage-falloff band, read by the
    /// blast radius in <c>explode</c>), <c>from</c> (provenance for the view), <c>it</c> (the
    /// arcane-beam lifetime counter, which the view decrements), <c>shrink</c> and <c>heavy</c>
    /// (both view concerns). They are not added now: a field in the port that no ported code reads is
    /// exactly the drift PORTED.md exists to prevent, and the falloff trio belongs with
    /// <c>explode</c> rather than with the loop that calls it.
    ///
    /// <c>hit</c> is the list of bodies this shell has already counted. It is what stops a piercing
    /// bolt re-hitting the body it is currently inside, and it is the reason a bolt damages a line of
    /// Brunesh once each rather than every body it overlaps on a single tick.
    /// </remarks>
    public sealed class Projectile
    {
        // --- motion and identity
        public double x, y, vx, vy;
        public double r, dmg;
        public bool friendly, heavy;

        // --- read by the projectile pass (src/60-tick.js:592-741)
        /// <summary>Ticks since the shell was spawned. Read by the pass.</summary>
        public int age;

        /// <summary>
        /// True for the alt/weapon-thrown shell, which flies to a fixed point, passes THROUGH bodies
        /// while <see cref="phase"/> is set, and resolves on arrival rather than on contact.
        /// </summary>
        public bool alt;

        /// <summary>
        /// The alt shell's phase: while true it ignores bodies entirely. The bolt is resolved from
        /// the weapon it was THROWN with (<c>mode</c>), not the one held when it lands.
        /// </summary>
        public bool phase;

        /// <summary>Fixed destination for an alt shell, in world px.</summary>
        public double tx, ty;

        /// <summary>
        /// The direction of travel, and the MOUTH it left from. <c>dx</c>/<c>dy</c> are normalised;
        /// <c>ox</c>/<c>oy</c> are the spawn point. Both are fixed at spawn on purpose: the piercing
        /// hit order is arrival order along the flight line, so no amount of jostling inside a knot can
        /// reshuffle which body a bolt reaches first.
        /// </summary>
        public double dx, dy, ox, oy;


        /// <summary>Per-tick step length, used by the alt shell's "stop ON the point" test.</summary>
        public double speed;

        /// <summary>Remaining pierce charges; decremented on each body hit.</summary>
        public int pierce;

        /// <summary>
        /// Damage multiplier, multiplied down by PIERCE_FALLOFF on each body. This is what makes lining
        /// the pack up a decision rather than a free delete.
        /// </summary>
        public double scale;

        /// <summary>The bodies this shell has already counted, so it cannot hit one twice.</summary>
        public List<Body>? hit;

        /// <summary>The shell's colour, for the view.</summary>
        public string color = "";

        /// <summary>Who fired it - the body, so a body cannot shoot itself.</summary>
        public Body? owner;

        /// <summary>
        /// Which WEAPON the alt shell was cast with. Resolved on arrival rather than from whatever is
        /// held then: swapping to the other right-click mid-flight used to detonate a pull as a blast.
        /// </summary>
        public string mode = "";
    }

    /// <summary>
    /// A pickup in the current room, widened for the <c>tickRoom</c> port.
    /// <para>
    /// The first version of this record carried only a position and a kind, which was enough to
    /// name the collection and not enough to run it. Four fields had to be added because the room
    /// phase reads all of them and two of them change behaviour rather than merely position:
    /// </para>
    /// <list type="bullet">
    /// <item><c>r</c> - the collection radius. The original tests
    /// <c>dist &lt; pk.r + player.r</c>, so a heart at r=16 and a key at r=14 are picked up at
    /// genuinely different distances. Carrying one constant here would quietly change which
    /// pickups are reachable at the edge of a room.</item>
    /// <item><c>hold</c> - set the tick after a pickup is taken, and cleared the tick after the
    /// player steps off it. This is what stops a just-collected item being re-collected on the
    /// same tick, and it is the reason the weapon swap parks the old gun rather than deleting it.</item>
    /// <item><c>w</c>, <c>id</c>, <c>charges</c> - the payload. A weapon pickup carries the index
    /// it grants, an item carries its id and how many charges came with it.</item>
    /// </list>
    /// <para>
    /// A pickup is still not a body: nothing moves it and nothing shoots it.
    /// </para>
    /// </summary>
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
        public int charges;
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
