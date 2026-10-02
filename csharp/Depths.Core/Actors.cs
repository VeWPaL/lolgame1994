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

    public sealed class Projectile
    {
        public double x, y, vx, vy;
        public double r, dmg;
        public bool friendly, heavy;
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
