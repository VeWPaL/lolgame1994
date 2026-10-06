using System.Collections.Generic;

namespace Depths
{
    /// <summary>A room transition in flight: the fade out, then the enter. src/50-run.js:106-109.</summary>
    public sealed class Trans
    {
        public int t;
        public int nx, ny;
        public string from = "";
    }

    /// <summary>
    /// The run-scoped holder: everything that is one instance per run and must not outlive it or be
    /// shared between two runs. Finding #8 made this a class rather than module state - the
    /// JavaScript kept <c>PACK_CURSOR</c> and the RNG streams reachable from every function, so a
    /// second run from the same seed silently began part-way through cursors the first run had
    /// advanced. Here the streams, the planner cursor and the run counters live ON the object that
    /// owns them, so two runs cannot share them by construction.
    ///
    /// <para>
    /// The RNG, the planner and the dungeon all draw from the ONE run stream owned here - same as
    /// the JavaScript's single <c>Rnd</c> at module scope, but owned rather than global. The
    /// constructor takes the <see cref="Rng"/> rather than a seed so the caller controls seeding;
    /// two <see cref="RunState"/>s over two <see cref="Rng"/>s built from the same seed replay
    /// independently and never observe each other's cursor.
    /// </para>
    ///
    /// <para>
    /// <c>dungeon</c> is per-floor in the original (a new <c>Dungeon</c> would be generated per
    /// floor); today it is constructed once per run to hold the seed, and a floor swap assigns a
    /// fresh <see cref="Dungeon"/> from the run's own stream. <c>planner</c> is per-run because a
    /// pack id only has to be unique within a run - ressetting it per floor would let two packs
    /// in different floors collide with a body that outlives the transition. That is the same
    /// finding #8, one object down.
    /// </para>
    ///
    /// <para>
    /// <c>phaseLog</c> is the observable pin for the tick-order gate until the passes carry real
    /// state: the projectile, body, player and room passes each append their name as they run, and
    /// the order they appear in is load-bearing (src/60-tick.js:8-10). It is diagnostic state, not
    /// gameplay state - clearing it costs nothing, and a swap of two passes that touched no other
    /// field is exactly the mutation the full port will not be able to hide from it. It leaves
    /// when the passes grow real bodies, because then order becomes observable from the state
    /// they touch.
    /// </para>
    /// </summary>
    public sealed class RunState
    {
        public Rng rng;
        public Dungeon dungeon;
        public WavePlanner planner;
        public Player player;
        public List<Projectile> projectiles;
        public List<Pickup> transients;

        public int floor;
        public int ticks;
        public int floorTicks;
        public int hits;

        /// <summary>
        /// Kills this run. Written by <c>killEnemy</c> and read by the clear condition, which is why
        /// it lives on the run rather than on the room: a body that dies belongs to no room once it is
        /// spliced out, and the tally has to outlive the splice.
        /// </summary>
        public int kills;

        /// <summary>Damage the player was dealt this run, before armour (run.dmgTaken).</summary>
        public double dmgTaken;

        /// <summary>Shells fired this run, one per pellet (run.shots).</summary>
        public int shots;

        /// <summary>Whether this run broke a secret wall (run.secret).</summary>
        public bool secret;

        /// <summary>FLANK_CURSOR: the golden-angle flank handed to each walker spawned this run.</summary>
        public double flankCursor;

        /// <summary>A hook's ground spell (hookFields): where, how big, and how long it has left.</summary>
        public sealed class HookField { public double x, y, r; public int life, max, id; }
        public readonly List<HookField> hookFields = new List<HookField>();

        /// <summary>The build: the items carried, and the stats they make.</summary>
        public readonly List<ItemSlot> loadout = new List<ItemSlot>();
        public readonly Stats stats = new Stats();
        public int hookFieldId;

        /// <summary>Whether the boss / item door on this floor has been paid for (bossUnlocked, itemUnlocked).</summary>
        public bool bossUnlocked, itemUnlocked;
        /// <summary>The lock being worked (unlockDoor), and for how long.</summary>
        public Dir? unlockDir;
        public Room? unlockRoom;
        public int unlockT;
        /// <summary>The blink bar filling across a room arrival (player.blinkRestore): from, ticks left, span.</summary>
        public double blinkRestoreFrom;
        public int blinkRestoreT, blinkRestoreSpan;

        /// <summary>
        /// startGame: reseed, generate floor 1 FROM THE RUN STREAM (as the game does, so every later
        /// draw lines up), stand in the start room with a fresh body, and play.
        /// </summary>
        public void Start(uint root)
        {
            rng.Set(root);
            rootSeed = root;
            dungeon = Dungeon.FromSeed(rng);
            planner = new WavePlanner(rng);
            curX = Map.Start; curY = Map.Start;
            player = new Player { x = Balance.MidX, y = Balance.MidY, lagX = Balance.MidX, lagY = Balance.MidY, hp = 8, maxHp = 8 };
            enemies.Clear(); pickups.Clear(); projectiles.Clear(); hookFields.Clear();
            loadout.Clear(); stats.Reset();   // Items.reset: a new run starts from nothing
            floor = 1; floorTicks = 0; ticks = 0; kills = 0; hits = 0; shots = 0; dmgTaken = 0; secret = false;
            blinkCharges = 2; blinkGrace = 0; graceSpent = false; blinkRestoreT = 0;
            flankCursor = 0; bossUnlocked = itemUnlocked = false; unlockDir = null; unlockRoom = null; unlockT = 0;
            trans = null; readyT = 0; bossWarnT = 0; bossWarned = false;
            roomFade = 1; fadeTicks = Balance.Sec(0.4); fadeT = fadeTicks;
            state = "playing";
        }

        /// <summary>
        /// The bodies in the room being fought, and the pickups on its floor. Both are needed by
        /// <c>killEnemy</c> (which splices a body out and pushes a drop) and neither existed on the
        /// port before it - <c>Room</c> carried geometry and doors but no occupants, because nothing
        /// that has been ported so far creates anything.
        ///
        /// <para>
        /// They are LISTS, not sets, and that is load-bearing rather than incidental: the projectile
        /// pass walks bodies backwards so a concurrent splice cannot corrupt the iteration, and
        /// <c>killEnemy</c> takes an INDEX. A dictionary keyed by identity would make the index
        /// meaningless, and the tie-break the piercing hit order depends on is arrival order along the
        /// flight line, not array order.
        /// </para>
        /// </summary>
        /// <summary>
        /// The living bodies in the current room.
        /// <para>
        /// <b>THIS IS <c>List&lt;Enemy&gt;</c> AND IT USED TO BE <c>List&lt;Body&gt;</c>.</b> The port
        /// had the archetype where the instance belongs: <c>Body</c> holds the build-dependent STATS
        /// for a kind - radius, health, speed, senses - and <c>Enemy</c> holds the mutable state of
        /// one that exists right now: position, health, cooldowns, pack, shield target.
        /// </para>
        /// <para>
        /// The distinction matters the moment the body phase lands, and it is the same shape of
        /// mistake as the pickup list: two bodies of the same kind would share their position. It was
        /// invisible while the list held archetypes, because archetypes are never asked where they
        /// are. Porting the pack pass asked, and every field it needs - <c>shieldTarget</c>,
        /// <c>shieldGuardFor</c> - lives on <c>Enemy</c> already, because the boss work had put them
        /// there.
        /// </para>
        /// <para>
        /// So the archetype stays where it belongs: a wave planner reads <c>Body</c> rows to decide
        /// WHAT to spawn, and a spawn turns one into an <c>Enemy</c>. The list holds the second.
        /// </para>
        /// </summary>
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Pickup> pickups = new List<Pickup>();

        public string state;

        // transient, matching the JavaScript's module-level trans / readyT / roomFade / fadeT /
        // fadeTicks, all of which the tick reads above its early returns. They reset to zero / null
        // on a fresh run; none of them survive a scene change, because the constructor builds them
        // and RunState is built per run.
        public Trans? trans;
        public int readyT;
        public int fadeT;
        public int fadeTicks;
        public double roomFade;

        public readonly List<string> phaseLog = new List<string>();

        public RunState(Rng rng)
        {
            this.rng = rng ?? throw new System.ArgumentNullException(nameof(rng));
            dungeon = new Dungeon(rng);
            planner = new WavePlanner(rng);
            player = new Player();
            projectiles = new List<Projectile>();
            transients = new List<Pickup>();
            floor = 1;
            state = "start";
            fadeTicks = Balance.Sec(0.4);   // JS fadeTicks=sec(0.4), src/50-run.js:109
        }
    
        /// <summary>
        /// Whether the run's alternate weapon is the hook rather than the blast. The room phase owns
        /// both sides of this: picking up a hook sets it, picking up a blast clears it, and picking
        /// up a second hook leaves it set. Starting value is false - a run opens on the blast, which
        /// is what the original does, so the first pickup of either kind is a change.
        /// </summary>
        public bool hook;

        /// <summary>
        /// Ends the run. <paramref name="won"/> is true only for a victory; every other ending is a
        /// death and is recorded as one.
        /// <para>
        /// The original's <c>endRun</c> also writes a record and stops the clock, neither of which
        /// exists on the C# side yet. This sets the state and leaves the rest to the run-summary
        /// port, and the note is here because "the tick calls EndRun and the run does not end" is
        /// exactly the kind of gap that reads as a passing test.
        /// </para>
        /// </summary>
        public void EndRun(bool won)
        {
            state = won ? "won" : "gameover";
        }

        /// <summary>
        /// Where the player is on the grid, and the room that position names.
        /// <para>
        /// The JavaScript keeps a module-scope cursor <c>cur</c> and reads
        /// <c>rooms[key(cur.x, cur.y)]</c>. This port keeps the cursor on the run instead, which is
        /// the difference that matters: a module-scope cursor survives <c>startGame</c>, so two runs
        /// from the same seed would begin in the last room of the previous one. On the run, a new
        /// run is a new cursor.
        /// </para>
        /// </summary>
        public int curX = Map.Start, curY = Map.Start;

        /// <summary>
        /// The room the player is standing in. Null only if the cursor names a cell nothing was
        /// generated at, which the generator cannot produce; the room phase treats null as a no-op
        /// rather than throwing, so a malformed cursor cannot end a run from inside a tick.
        /// </summary>
        public Room? CurrentRoom => dungeon.RoomAt(curX, curY);

        /// <summary>
        /// The run's root seed - the seven characters the player typed. The floor seeds are derived
        /// from THIS and not from each other, which is what makes each floor a pure function of
        /// (root, floor). See <see cref="Rng.FloorSeed"/> for why that is not a chain.
        /// </summary>
        public uint rootSeed;

        /// <summary>Rooms entered on this floor. Reset by <see cref="Descend"/>.</summary>
        public int roomsThisFloor;

        /// <summary>Ticks for the descent fade. Read out of the running game: 189.</summary>
        public const int FadeDescend = 189;

        /// <summary>
        /// Goes down a floor. Ported from the original's <c>descend()</c>, with every value below
        /// measured out of the running game rather than transcribed - the browser was put into the
        /// dirtiest state it could be put into (velocity, knockback, spent blinks, a live
        /// transition, an armed door, a shell in flight, every flag set) and then asked to descend.
        ///
        /// <para>
        /// <b>What this resets, and why each one matters.</b> It is not housekeeping - every line
        /// below was measured to a specific value that a partial port would leave stale, and a stale
        /// one produces a floor that is playable but wrong rather than a crash.
        /// </para>
        /// <list type="bullet">
        /// <item>the floor and both clocks - <c>floorTicks</c> and <c>roomsThisFloor</c> go to 0</item>
        /// <item>the RNG, reseeded from <c>(rootSeed, floor)</c>, and a NEW dungeon generated from
        /// it. The root seed itself is kept, which is what makes the new floor reproducible.</item>
        /// <item>the cursor, back to the start cell (3, 3)</item>
        /// <item>the player, to the room centre (400, 355) with the lagged hitbox moved WITH it -
        /// leaving lag behind would make the gunners aim at where the player was a floor ago</item>
        /// <item>all velocity and knockback, to zero</item>
        /// <item>the blink: charges back to 2, regen to 0, grace to 0</item>
        /// <item>every per-floor flag: boss and item unlocked, secret found, boss warned, the
        /// transition, the armed door and its timer</item>
        /// <item>the shells in flight - a projectile from the old floor must not arrive in the new
        /// one</item>
        /// <item>the fade: 189 ticks, <c>roomFade</c> to 1, and the descent timer armed</item>
        /// </list>
        ///
        /// <para>
        /// <b>Not ported, and it says so.</b> The original also clears three art caches, writes a
        /// record for the deepest floor, and resets the pause and frame-accumulator state. The caches
        /// are presentation and belong with the renderer; the record belongs with the save layer; the
        /// pause state has no meaning in a tick that is called rather than driven by a clock. None of
        /// them can change a parity row, and pretending otherwise would be a comment claiming more
        /// than the code does.
        /// </para>
        /// </summary>
        public void Descend()
        {
            int from = floor;
            floor = from + 1;
            floorTicks = 0;
            roomsThisFloor = 0;

            // the old room keeps what it held; the new floor is generated from the run stream itself,
            // as the game does, so the draws after it line up
            var oldRoom = CurrentRoom;
            if (oldRoom != null) { oldRoom.Enemies.Clear(); oldRoom.Enemies.AddRange(enemies); oldRoom.Pickups.Clear(); oldRoom.Pickups.AddRange(pickups); }
            enemies.Clear(); pickups.Clear();
            rng.Set(Rng.FloorSeed(rootSeed, floor));
            dungeon = Dungeon.FromSeed(rng);
            bossUnlocked = itemUnlocked = false; unlockDir = null; unlockRoom = null; unlockT = 0;

            curX = Map.Start;
            curY = Map.Start;

            player.x = Balance.MidX; player.y = Balance.MidY;
            player.lagX = Balance.MidX; player.lagY = Balance.MidY;
            player.vx = 0; player.vy = 0;
            player.kvx = 0; player.kvy = 0;

            // Shells from the floor above must not arrive here.
            projectiles.Clear();
            transients.Clear();

            /* PICKUPS AND BODIES BELONG TO THE ROOM, SO A DESCENT DOES NOT CLEAR THEM.

               This is the second time this line has been written in this port, and the first version
               was wrong - which is worth recording because both versions were reasonable.

               In the JavaScript a pickup lives on the ROOM (`r.pickups`), so descending builds a new
               dungeon and the old room keeps its contents while the player's cursor lands in a new
               room that has none. Measured: an exit portal and a heart on floor 1, then a descent -
               the old room still holds 2 pickups, and the new start room holds 0.

               The first version of this port read that as "descend must clear the list" and added
               `pickups.Clear()`, on the reasoning that this port keeps the current room's pickups on
               the RUN rather than on the ROOM and so something has to do the job `rooms[key()]` does
               for free. A test caught it immediately. The reasoning was sound and the conclusion was
               wrong: clearing is not what makes a new room empty, REPLACING THE ROOM is, and a
               descent that clears the run's list is clearing the OLD room's contents as a side effect
               of arriving somewhere new.

               So nothing is cleared here. `enterRoom` - which is where the original actually empties a
               room - is the port's remaining gap on this path, and it is named rather than papered
               over, because the moment it lands it has to do the clearing instead. */

            trans = null;
            readyT = 0;
            bossWarnT = 0;
            bossWarned = false;

            blinkCharges = 2;
            player.blinkRegen = 0;
            blinkGrace = 0;
            graceSpent = false;

            fadeTicks = FadeDescend;
            fadeT = FadeDescend;
            roomFade = 1;
            descendFrom = from;
            descendT = FadeDescend;

            state = "playing";
        }

        /// <summary>
        /// Whether the boss has been announced on the map yet. Per-floor, so <see cref="Descend"/>
        /// clears it: a player who descends and comes back up should be warned again.
        /// </summary>
        public bool bossWarned;

        /// <summary>Ticks the boss warning still has to run, decremented once a tick.</summary>
        public int bossWarnT;

        /// <summary>The floor being descended FROM, for the descent banner. Set by <see cref="Descend"/>.</summary>
        public int descendFrom;

        /// <summary>Ticks left in the descent fade. Set by <see cref="Descend"/>.</summary>
        public int descendT;

        /// <summary>
        /// Blink charges held, 0 to 2. <see cref="Descend"/> refills to 2, so arriving on a new floor
        /// never costs the player an escape they were owed.
        /// </summary>
        public int blinkCharges = 2;

        /// <summary>Ticks of post-blink invulnerability left. Cleared on a descent.</summary>
        public int blinkGrace;

        /// <summary>Whether this blink's grace has already forgiven a hit (player.graceSpent).</summary>
        public bool graceSpent;

        /// <summary>
        /// Frames elapsed. Read by the pack scan, which fires on
        /// <c>frameCount % BrunchScanTicks == 0</c> - so it counts FRAMES rather than ticks, and the
        /// distinction matters only if the two ever diverge.
        /// </summary>
        public int frameCount;
}
}
