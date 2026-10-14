# World.cs - moved comments

Long comments moved out of `csharp/Depths.Core/World.cs`. The code keeps a one-line gist tagged `[h:World-N]`.

## [h:World-1]
near: `public sealed class Room`

/// <summary>
/// One room on the grid.
///
/// <para>
/// A room is mutable and is mutated while it is being built - a door is added on one side and the
/// matching one on the other from the same statement - so it is a class, not a struct. Copying one
/// into a struct would hand the generator a detached duplicate that silently loses a door.
/// </para>
/// </summary>

## [h:World-2]
near: `public HashSet<Dir> Doors = new HashSet<Dir>();`

/// <summary>
/// Doors, as a set of directions. Insertion order is irrelevant and must not be relied on:
/// the JavaScript builds a plain object and never reads its key order either, so a C# port
/// using an ordered collection would be carrying an ordering the original never had.
/// </summary>

## [h:World-3]
near: `public bool Cleared;`

/// <summary>
/// The room has been emptied, at least once. Set by <c>tickRoom</c> (the Unity sandbox resets it per
/// wave), so its one-off rewards (half a blink charge; a fought room's regen refill) are paid once.
/// </summary>

## [h:World-4]
near: `public bool KeySpawned;`

/// <summary>
/// The silver key has been dropped here, so it must not be dropped twice. One-shot, like
/// <see cref="Cleared"/>, and for the same reason: the spawn is inside the room phase and
/// would otherwise re-run every tick the room is empty.
/// </summary>

## [h:World-5]
near: `public bool ExitOpen;`

/// <summary>
/// The way down is open. Set when a boss dies, and it is what makes the exit portal appear
/// exactly once rather than on every tick the boss room stays empty - which would stack a
/// new portal per tick for as long as the player stood there.
/// </summary>

## [h:World-6]
near: `public Dir? Secret;`

/// <summary>
/// The direction of the fake wall that hides the secret, or null. Set on the room BESIDE the
/// secret rather than on the secret itself, because the wall is what has to be broken.
/// </summary>

## [h:World-7]
near: `public RoomBounds Bounds = RoomBounds.Standard;`

/// <summary>
/// The room's own rectangle. Read from the bounds rather than from a global, because a global
/// every room reads can only ever describe one shape - see the note on roomBounds in the
/// JavaScript, which says the same thing and was written there first.
/// </summary>

## [h:World-8]
near: `public static class Map`

/// <summary>
/// The map constants, grouped because they are meaningless apart.
///
/// <para>
/// 7x7 is small enough that a floor fits with room to spare and large enough that a two-trunk tree
/// with two side runs can be laid out without every build being thrown away for cramping - which
/// is the trade <c>tryBuild</c>'s retry loop is spending. <see cref="Start"/> is the middle because
/// the tree grows in every direction and a start in a corner has three sides of grid to lose.
/// </para>
/// </summary>

## [h:World-9]
near: `public sealed class Dungeon`

/// <summary>
/// THE DUNGEON, and the generator that builds it.
///
/// <para>
/// This is the first ported type that holds mutable state across a run, and the reason it is an
/// instance rather than a static class is finding #8. The JavaScript keeps two cursors -
/// <c>FLANK_CURSOR</c>, which decides which slice of the circle the next lunger takes, and
/// <c>PACK_CURSOR</c>, which hands out wall identities - at module scope, where nothing can reset
/// them and <c>startGame</c> did not. They carried on counting across restarts, so the second run
/// from a seed began part-way round the flank walk: same rooms, same bodies, every lunger circling
/// from a slightly different angle, and a difference with no seed to explain it.
/// </para>
///
/// <para>
/// C# makes that mistake easier rather than harder. A module-level helper reads naturally as a
/// <c>static</c> method, the compiler will not object, and nothing in the type system connects a
/// static counter to the run that owns it. So this generator takes its <see cref="Rng"/> by
/// constructor and holds it as a field. Two generators cannot share a stream, and a generator
/// cannot outlive the run it was seeded for, because the type makes both impossible rather than
/// merely discouraged.
/// </para>
///
/// <para>
/// <b>THE CONTRACT IS DRAW ORDER.</b> Nothing here may consult the stream except where the
/// JavaScript does, in the same sequence. A single extra or omitted draw renumbers every seed
/// after it, which is precisely the failure the three-stream split exists to prevent and which
/// nobody notices until a friend pastes a seed and gets a different floor.
/// </para>
/// </summary>

## [h:World-10]
near: `private readonly Dictionary<int, Room> _bossFront = new Dictionary<int, Room>();`

/// <summary>
/// The rooms with a door onto the boss. Derived once for the same reason: the JavaScript found
/// this with an O(n^2) scan of the whole dungeon on every single tick of the entire run, until
/// the warning fired.
/// </summary>

## [h:World-11]
near: `private static bool InGrid(int x, int y) =>`

/* THE GRID KEY, and why it must never be asked about a cell outside the grid.

   x*Grid+y is injective for 0 <= x,y < Grid and that is the WHOLE of its guarantee. One step
   outside, it stops being injective in the worst possible way:

       Cell(4,7) = 35      <- out of grid, y is 7
       Cell(5,0) = 35      <- IN grid, and in seed 3 this is the silver key room

   Strict mode asks "does this candidate touch any room but its parent?" by probing all four
   neighbours, and two of those four are off the grid whenever the candidate is on an edge. So
   an off-grid probe silently aliased onto a real room on the far side, strict mode refused a
   perfectly good placement, and the trunk turned the other way. It matched on 6 of the first
   10 seeds and failed on the 4 that happened to put a room where the alias pointed - which is
   the worst possible signature for this class of bug, because a parity table sampled at the
   wrong seeds reads as a pass.

   Every probe therefore goes through Occupied, which bounds-checks first. Cell is private and
   its precondition is only honoured by callers that have already established InGrid. */

## [h:World-12]
near: `public Room? RoomAt(int x, int y) => _rooms.TryGetValue(Cell(x, y), out var r) ? r : null;`

/// <summary>
/// The room at a grid position, or null if nothing was generated there.
/// <para>
/// The JavaScript reads the current room as <c>rooms[key(cur.x, cur.y)]</c> - a lookup on
/// every access, several times a tick. This is the same dictionary and the same key function,
/// exposed, so the tick does not have to carry a cached <c>Room</c> that could go stale when
/// the seed changes underneath it.
/// </para>
/// </summary>

## [h:World-13]
near: `private static Dir[] Turns(Dir d)`

/// <summary>
/// The two directions that are NOT straight ahead, and the hand they turn with.
///
/// <para>
/// The ORDER IS THE MEANING. <c>[W,E]</c> heading north and <c>[E,W]</c> heading south are the
/// same pair read in opposite senses, and the generator indexes this by a 0-or-1 roll to pick
/// a side. Swapping the two entries does not produce a different dungeon - it produces the same
/// set of dungeons with the left and right branches swapped, which the parity tests would
/// catch only if they compared a specific seed's side run rather than a room count.
/// </para>
/// </summary>

## [h:World-14]
near: `private static readonly Dir[] ArmDirs = { Dir.N, Dir.E, Dir.S, Dir.W };`

/// <summary>
/// The direction list, in the order the generator considers them.
///
/// <para>
/// N, E, S, W is not alphabetical and not a compass walk. It is the order the secret-wall scan
/// pushes candidates in, and that scan picks one with a single draw at the end - so the order
/// decides WHICH wall a floor hides its secret behind. Reordering this list produces dungeons
/// that are all individually valid and none of them reproducible.
/// </para>
/// </summary>

## [h:World-15]
near: `var side = new List<Dir>();`

// Prefer leaving sideways off the host so a fork reads as a fork, not as a stub. With no
// preference yet - the first trunk - every free side is a side, which is what makes the
// very first draw unbiased.

## [h:World-16]
near: `private GrowResult? Grow(int x, int y, Dir dir, int len, bool strict, double bias)`

/// <summary>
/// Walks one corridor out of (x,y). Runs travel in straight segments of two or three rooms and
/// bend once at the end of each, so a run reads as a path you walk rather than a spiral.
///
/// <para>
/// <paramref name="strict"/> forbids rooms that touch anything but their parent, and that is how
/// the loose pass finds room for the long runs.
/// </para>
/// </summary>

## [h:World-17]
near: `private bool TouchesOtherThan(int px, int py, int nx, int ny)`

/// <summary>
/// Whether (nx,ny) would touch a room other than its intended parent. Strict mode's whole rule.
/// </summary>

## [h:World-18]
near: `private GrowResult? GrowForked(int x, int y, Dir dir, int len, bool strict, double bias,`

/// <summary>
/// One trunk, with side runs cut off it.
///
/// <para>
/// This is <see cref="Grow"/> with forks added, and it is a separate method rather than a flag
/// on <see cref="Grow"/> because the fork has to happen from INSIDE the walk - a branch can only
/// be cut from a room that actually exists - and threading that through meant giving
/// <see cref="Grow"/> two return shapes and one more parameter, for a caller that wants a
/// different answer.
/// </para>
/// </summary>

## [h:World-19]
near: `if (step >= len - 1) continue;`

/* NEVER FORK FROM THE TIP. The last room of a trunk becomes the upgrade room or the
   boss, and a branch hanging off it puts the key in that branch BEHIND the locked door
   the key is supposed to open. Measured before this was caught: 113 of 300 dungeons
   had an unreachable gold key, with a branch running east out of the boss room. */

## [h:World-20]
near: `var host = res.Path[res.Path.Count - 1];`

// Cut a side run off the room just placed, leaving by a direction that is neither the
// way we came nor the way we are going, so a branch is never a stub pointing back down
// the trunk.

## [h:World-21]
near: `var trunkLen = 4 + (int)(_rng.Run() * 2);   // DRAW`

/* Two trunks out of the start, in different directions. The second is grown AFTER the
   first, so FreeDir already knows the first trunk is there and the two cannot collide -
   which is what stops the whole build being thrown away for a run that walks into its own
   corridor.

   trunkLen 4-5 and forkLen 3-4 rather than something shorter. The first version of the
   tree used 3-4 and 2-3, which put a floor at 8 fight rooms in the worst case - and eight
   is not a floor, it is a corridor with two decisions in it. The lengths are the cost side
   of the same trade: they are what the retry loop in Generate is paying for. */

## [h:World-22]
near: `var aIsBoss = _rng.Run() < 0.5;   // DRAW`

/* ROLES, assigned to the ENDS of what grew rather than to positions chosen in advance.

   The two trunks get the two rooms worth walking towards, and they differ on purpose: one
   end is the upgrade room and one is the boss, so a player who commits early to a
   direction has committed to what that direction was FOR. Which trunk gets which is a coin
   toss, so the map cannot be learned as "left is the boss". */

## [h:World-23]
near: `var aEnds = new List<Room>();`

/* THE KEYS, one per trunk, placed at a BRANCH END and chosen at generation.

   A branch end is an ordinary fight room with a key in it, so getting the key costs a
   detour and the detour can be blocked - which is the whole point of putting it there
   rather than on the spine. If a trunk produced no branch the build is thrown away rather
   than quietly putting the key on the spine, because a map that sometimes has a detour and
   sometimes does not is a map where the detour is not a decision. */

## [h:World-24]
near: `var walls = new List<(Room Room, Dir Dir, int X, int Y)>();`

// One secret per floor, walled off behind a fake wall on a random solid wall. Never on the
// boss or the upgrade room, so those two stay legible, and it stays off the map until the
// wall is gone.

## [h:World-25]
near: `foreach (var r in _rooms.Values)`

// Over the rooms built SO FAR, not over the derived AllRooms list: that list is filled in
// by Generate() only after a build succeeds, so during the retry loop it is empty or
// stale, and scanning it would find no walls at all - every attempt failing, forever.

## [h:World-26]
near: `public void Generate()`

/// <summary>
/// Builds a floor, retrying until one takes.
///
/// <para>
/// Strict for the first 500 attempts and loose after, because most builds fail for want of
/// space rather than for want of luck - so it spends the strict passes on the trees worth
/// having and lets the rest be a little cramped rather than refusing to produce a floor.
/// </para>
///
/// <para>
/// <b>DELIBERATE DIVERGENCE.</b> The JavaScript assigns nothing when all 800 attempts fail and
/// returns, leaving the caller holding the PREVIOUS floor's rooms - a run that walks into a
/// dungeon that is not on the screen, with no error anywhere. Here it throws instead. Measured
/// over 3000 seeds it has never happened, so the divergence is unobservable in normal play;
/// it is here because the failure mode is silent, and a silent failure in a generator is worse
/// than an exception in a test that nobody runs.
/// </para>
/// </summary>

## [h:World-27]
near: `_all.Clear();`

// Derived once, here, rather than rediscovered every tick. _all is the room list as an
// array because the minimap walks it every frame, and the JavaScript spent sixty array
// allocations a second on Object.values for the same job.

## [h:World-28]
near: `public string Signature()`

/// <summary>
/// A stable text form of the whole graph, for the parity tests.
///
/// <para>
/// Rooms sorted by x then y, doors sorted, flags appended. This is the SAME shape the JavaScript
/// side emits, deliberately: a parity table is only worth comparing if both ends build the
/// string the same way, and hand-writing the comparison is how the two drift apart quietly.
/// </para>
/// </summary>

## [h:World-29]
near: `var doors = string.Concat(r.Doors.Select(d => d.ToString()).OrderBy(s => s, StringComparer.Ordinal));`

// Sorted ALPHABETICALLY, not by enum value. The JavaScript side does
// Object.keys(r.doors).sort() - a code-unit sort - and this has to match it
// character for character or the comparison is theatre. Ordinal, so the order cannot
// drift with the current culture.

## [h:World-30]
near: `var kind = char.ToLowerInvariant(r.Type.ToString()[0]);`

// Lowercased, because the JavaScript room types are literally 'normal', 'start',
// 'item', 'boss' and 'secret' and charAt(0) of those is lowercase. A parity table is
// only worth having if both ends build the string the same way, and a case difference
// would turn every row red for a reason that is not about the generator.
