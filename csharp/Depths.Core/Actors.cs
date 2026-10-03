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
        public int cooldown, altCooldown, iframes;
        public double momentum;
        public int weaponIdx;
        public string altMode = "blast";
        public bool hasSilver, hasGold;
        public double lagX, lagY;
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
    /// A transient pickup in the current room. The contract names the collection
    /// (<c>RunState.transients</c>) without a type, so this is the placeholder record the list
    /// holds until the item port lands. Kept here rather than widening <c>Body</c>, because a
    /// pickup is not a body - nothing moves it and nothing shoots it.
    /// </summary>
    public sealed class Pickup
    {
        public double x, y;
        public string kind = "";
    }
}
