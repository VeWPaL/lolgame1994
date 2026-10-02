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
    }
}
