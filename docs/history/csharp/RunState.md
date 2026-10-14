# RunState.cs - moved comments

Long comments moved out of `csharp/Depths.Core/RunState.cs`. The code keeps a one-line gist tagged `[h:RunState-N]`.

## [h:RunState-1]
near: `public sealed class RunState`

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

## [h:RunState-2]
near: `public int kills;`

/// <summary>
/// Kills this run. Written by <c>killEnemy</c> and read by the clear condition, which is why
/// it lives on the run rather than on the room: a body that dies belongs to no room once it is
/// spliced out, and the tally has to outlive the splice.
/// </summary>

## [h:RunState-3]
near: `public void Start(uint root)`

/// <summary>
/// startGame: reseed, generate floor 1 FROM THE RUN STREAM (as the game does, so every later
/// draw lines up), stand in the start room with a fresh body, and play.
/// </summary>

## [h:RunState-4]
near: `public readonly List<Enemy> enemies = new List<Enemy>();`

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

## [h:RunState-5]
near: `public Trans? trans;`

// transient, matching the JavaScript's module-level trans / readyT / roomFade / fadeT /
// fadeTicks, all of which the tick reads above its early returns. They reset to zero / null
// on a fresh run; none of them survive a scene change, because the constructor builds them
// and RunState is built per run.

## [h:RunState-6]
near: `public bool hook;`

/// <summary>
/// Whether the run's alternate weapon is the hook rather than the blast. The room phase owns
/// both sides of this: picking up a hook sets it, picking up a blast clears it, and picking
/// up a second hook leaves it set. Starting value is false - a run opens on the blast, which
/// is what the original does, so the first pickup of either kind is a change.
/// </summary>

## [h:RunState-7]
near: `public void EndRun(bool won)`

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

## [h:RunState-8]
near: `public int curX = Map.Start, curY = Map.Start;`

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

## [h:RunState-9]
near: `public Room? CurrentRoom => dungeon.RoomAt(curX, curY);`

/// <summary>
/// The room the player is standing in. Null only if the cursor names a cell nothing was
/// generated at, which the generator cannot produce; the room phase treats null as a no-op
/// rather than throwing, so a malformed cursor cannot end a run from inside a tick.
/// </summary>

## [h:RunState-10]
near: `public uint rootSeed;`

/// <summary>
/// The run's root seed - the seven characters the player typed. The floor seeds are derived
/// from THIS and not from each other, which is what makes each floor a pure function of
/// (root, floor). See <see cref="Rng.FloorSeed"/> for why that is not a chain.
/// </summary>

## [h:RunState-11]
near: `public void Descend()`

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
/// <item>the blink: charges back to 2, blinkRegen to 0, grace to 0</item>
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

## [h:RunState-12]
near: `public bool bossWarned;`

/// <summary>
/// Whether the boss has been announced on the map yet. Per-floor, so <see cref="Descend"/>
/// clears it: a player who descends and comes back up should be warned again.
/// </summary>

## [h:RunState-13]
near: `public int blinkCharges = 2;`

/// <summary>
/// Blink charges held, 0 to 2. <see cref="Descend"/> refills to 2, so arriving on a new floor
/// never costs the player an escape they were owed.
/// </summary>

## [h:RunState-14]
near: `public int frameCount;`

/// <summary>
/// Frames elapsed. Read by the pack scan, which fires on
/// <c>frameCount % BrunchScanTicks == 0</c> - so it counts FRAMES rather than ticks, and the
/// distinction matters only if the two ever diverge.
/// </summary>
