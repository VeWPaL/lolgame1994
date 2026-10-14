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

    /// <summary>The run-scoped holder: everything that is one instance per run and must not outlive it or be shared between... [h:RunState-1]</summary>
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
        /// <summary>This run's kinds per area (Pools.Roll), or null: the JS's fixed mix.</summary>
        public BodyKind[][]? pools;
        /// <summary>The latest <see cref="Interp.Snap"/>'s stamp: drawing only, 0 until the first.</summary>
        public int snap;
        public int floorTicks;
        public int hits;

        /// <summary>Kills this run. Written by killEnemy and read by the clear condition, which is why it lives on the run rather... [h:RunState-2]</summary>
        public int kills;
        public int picked;   // pickups collected this run: the presenter's cue (nothing in the simulation reads it)
        public int thuds;    // heavy landings (a Burrower's leap): the presenter's screen shake
        public int booms;    // explosions (the blast, the hook, a Mortar shell landing): the presenter's screen shake

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

        /// <summary>startGame: reseed, generate floor 1 FROM THE RUN STREAM (as the game does, so every later draw lines up)... [h:RunState-3]</summary>
        public void Start(uint root)
        {
            rng.Set(root);
            rootSeed = root;
            dungeon = Dungeon.FromSeed(rng);
            planner = new WavePlanner(rng);
            pools = Balance.Tuned && Balance.TunePools ? Pools.Roll(root) : null;
            curX = Map.Start; curY = Map.Start;
            int regen = Balance.JsReference ? 0 : Balance.RegenHp;   // the last heart regenerates; the JS has none
            player = new Player { x = Balance.MidX, y = Balance.MidY, lagX = Balance.MidX, lagY = Balance.MidY,
                                  hp = 8 - regen, maxHp = 8 - regen, regenHeart = regen, regenHeartMax = regen };
            enemies.Clear(); pickups.Clear(); projectiles.Clear(); hookFields.Clear();
            loadout.Clear(); stats.Reset();   // Items.reset: a new run starts from nothing
            floor = 1; floorTicks = 0; ticks = 0; kills = 0; hits = 0; shots = 0; dmgTaken = 0; secret = false;
            blinkCharges = 2; blinkGrace = 0; graceSpent = false; blinkRestoreT = 0;
            flankCursor = 0; bossUnlocked = itemUnlocked = false; unlockDir = null; unlockRoom = null; unlockT = 0;
            trans = null; readyT = 0; bossWarnT = 0; bossWarned = false;
            roomFade = 1; fadeTicks = Balance.FadeStart; fadeT = fadeTicks;
            state = "playing";
        }

        /// <summary>The bodies in the room being fought, and the pickups on its floor. [h:RunState-4]</summary>
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Pickup> pickups = new List<Pickup>();

        public string state;

        // transient, matching the JavaScript's module-level trans / readyT / roomFade / fadeT / fadeTicks, all of which... [h:RunState-5]
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
            fadeTicks = Balance.FadeStart;
        }
    
        /// <summary>Whether the run's alternate weapon is the hook rather than the blast. [h:RunState-6]</summary>
        public bool hook;

        /// <summary>Ends the run. won is true only for a victory; every other ending is a death and is recorded as one. The... [h:RunState-7]</summary>
        public void EndRun(bool won)
        {
            state = won ? "won" : "gameover";
        }

        /// <summary>Where the player is on the grid, and the room that position names. [h:RunState-8]</summary>
        public int curX = Map.Start, curY = Map.Start;

        /// <summary>The room the player is standing in. [h:RunState-9]</summary>
        public Room? CurrentRoom => dungeon.RoomAt(curX, curY);

        /// <summary>The run's root seed - the seven characters the player typed. [h:RunState-10]</summary>
        public uint rootSeed;

        /// <summary>Rooms entered on this floor. Reset by <see cref="Descend"/>.</summary>
        public int roomsThisFloor;

        /// <summary>Ticks for the descent fade: 0.9s (189 at the JS rate).</summary>
        public static int FadeDescend => Balance.Sec(0.9);

        /// <summary>Goes down a floor. Ported from the original's descend(), with every value below measured out of the running... [h:RunState-11]</summary>
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
            if (Balance.Tuned) hookFields.Clear();   // a field thrown on the way out stays on the floor above

            // The old room keeps its pickups and bodies (stashed above, as the JS keeps r.pickups); the
            // run's lists were then cleared because they now stand for the new start room, which is empty.

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

        /// <summary>Whether the boss has been announced on the map yet. [h:RunState-12]</summary>
        public bool bossWarned;

        /// <summary>Ticks the boss warning still has to run, decremented once a tick.</summary>
        public int bossWarnT;

        /// <summary>The floor being descended FROM, for the descent banner. Set by <see cref="Descend"/>.</summary>
        public int descendFrom;

        /// <summary>Ticks left in the descent fade. Set by <see cref="Descend"/>.</summary>
        public int descendT;

        /// <summary>Blink charges held, 0 to 2. [h:RunState-13]</summary>
        public int blinkCharges = 2;

        /// <summary>Ticks of post-blink invulnerability left. Cleared on a descent.</summary>
        public int blinkGrace;

        /// <summary>Whether this blink's grace has already forgiven a hit (player.graceSpent).</summary>
        public bool graceSpent;

        /// <summary>Frames elapsed. Read by the pack scan, which fires on frameCount % BrunchScanTicks == 0 - so it counts FRAMES... [h:RunState-14]</summary>
        public int frameCount;
}
}
