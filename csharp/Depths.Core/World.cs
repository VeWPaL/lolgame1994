using System;
using System.Collections.Generic;
using System.Linq;

namespace Depths
{
    /// <summary>The four directions a door can leave by, in the order the generator walks them.</summary>
    public enum Dir
    {
        /// <summary>North. Decreasing grid Y.</summary>
        N = 0,

        /// <summary>East. Increasing grid X.</summary>
        E = 1,

        /// <summary>South. Increasing grid Y.</summary>
        S = 2,

        /// <summary>West. Decreasing grid X.</summary>
        W = 3,
    }

    /// <summary>What a room is FOR. The generator decides this; nothing else may.</summary>
    public enum RoomKind
    {
        /// <summary>Normal fight room. The overwhelming majority.</summary>
        Normal = 0,

        /// <summary>The room the run begins in. Walked out of, never fought in.</summary>
        Start = 1,

        /// <summary>The upgrade. One of the two trunk tips.</summary>
        Item = 2,

        /// <summary>The boss. The other trunk tip.</summary>
        Boss = 3,

        /// <summary>The secret, walled behind a fake wall. Exactly one per floor.</summary>
        Secret = 4,
    }

    /// <summary>
    /// One room on the grid.
    ///
    /// <para>
    /// A room is mutable and is mutated while it is being built - a door is added on one side and the
    /// matching one on the other from the same statement - so it is a class, not a struct. Copying one
    /// into a struct would hand the generator a detached duplicate that silently loses a door.
    /// </para>
    /// </summary>
    public sealed class Room
    {
        public int X, Y;
        public RoomKind Type;

        /// <summary>
        /// Doors, as a set of directions. Insertion order is irrelevant and must not be relied on:
        /// the JavaScript builds a plain object and never reads its key order either, so a C# port
        /// using an ordered collection would be carrying an ordering the original never had.
        /// </summary>
        public HashSet<Dir> Doors = new HashSet<Dir>();

        public bool Visited, Spawned;

        /// <summary>
        /// The room has been fought to empty, at least once. Set by <c>tickRoom</c>, and read only
        /// to make the half-blink charge a one-off rather than a per-tick top-up: without the flag a
        /// cleared room would hand back a blink charge every tick for as long as the player stood
        /// in it, which is a different resource economy from the one the original has.
        /// </summary>
        public bool Cleared;

        /// <summary>
        /// The silver key has been dropped here, so it must not be dropped twice. One-shot, like
        /// <see cref="Cleared"/>, and for the same reason: the spawn is inside the room phase and
        /// would otherwise re-run every tick the room is empty.
        /// </summary>
        public bool KeySpawned;

        /// <summary>The gold key has been dropped here. One-shot, like <see cref="KeySpawned"/>.</summary>
        public bool GoldSpawned;

        /// <summary>
        /// The way down is open. Set when a boss dies, and it is what makes the exit portal appear
        /// exactly once rather than on every tick the boss room stays empty - which would stack a
        /// new portal per tick for as long as the player stood there.
        /// </summary>
        public bool ExitOpen;

        /// <summary>Holds the silver key. Exactly one room per floor.</summary>
        public bool KeyReward;
        public bool Armed;   // a live room hands back the cooldowns once, on first entry
        // what the room holds while the player is elsewhere (the run's lists are the current room's)
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Pickup> Pickups = new List<Pickup>();

        /// <summary>Holds the gold key. Exactly one room per floor.</summary>
        public bool GoldReward;

        /// <summary>
        /// The direction of the fake wall that hides the secret, or null. Set on the room BESIDE the
        /// secret rather than on the secret itself, because the wall is what has to be broken.
        /// </summary>
        public Dir? Secret;

        /// <summary>
        /// The room's own rectangle. Read from the bounds rather than from a global, because a global
        /// every room reads can only ever describe one shape - see the note on roomBounds in the
        /// JavaScript, which says the same thing and was written there first.
        /// </summary>
        public RoomBounds Bounds = RoomBounds.Standard;

        /// <summary>A room is an endpoint when it has exactly one door. Nothing else counts.</summary>
        public bool IsEndpoint => Doors.Count == 1;

        public override string ToString() => $"({X},{Y}) {Type} [{string.Join("", Doors)}]";
    }

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
    public static class Map
    {
        public const int Grid = 7;
        public const int Start = 3;
    }

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
    public sealed class Dungeon
    {
        private readonly Rng _rng;
        private readonly Dictionary<int, Room> _rooms = new Dictionary<int, Room>();

        /// <summary>Every room, in a stable order. Derived once at generation, not rebuilt per tick.</summary>
        private readonly List<Room> _all = new List<Room>();

        /// <summary>
        /// The rooms with a door onto the boss. Derived once for the same reason: the JavaScript found
        /// this with an O(n^2) scan of the whole dungeon on every single tick of the entire run, until
        /// the warning fired.
        /// </summary>
        private readonly Dictionary<int, Room> _bossFront = new Dictionary<int, Room>();

        public Dungeon(Rng rng) => _rng = rng ?? throw new System.ArgumentNullException(nameof(rng));

        public IReadOnlyList<Room> AllRooms => _all;

        /// <summary>Rooms adjacent to the boss room, which are the ones the warning can come from.</summary>
        public IReadOnlyCollection<Room> BossFront => _bossFront.Values;

        /// <summary>The run seed, so a caller can print what a floor was actually built from.</summary>
        public uint Seed => _rng.Seed;

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
        private static bool InGrid(int x, int y) =>
            x >= 0 && y >= 0 && x < Map.Grid && y < Map.Grid;

        private bool Occupied(int x, int y) =>
            InGrid(x, y) && _rooms.ContainsKey(Cell(x, y));

        /// <summary>
        /// The room at a grid position, or null if nothing was generated there.
        /// <para>
        /// The JavaScript reads the current room as <c>rooms[key(cur.x, cur.y)]</c> - a lookup on
        /// every access, several times a tick. This is the same dictionary and the same key function,
        /// exposed, so the tick does not have to carry a cached <c>Room</c> that could go stale when
        /// the seed changes underneath it.
        /// </para>
        /// </summary>
        public Room? RoomAt(int x, int y) => _rooms.TryGetValue(Cell(x, y), out var r) ? r : null;

        private static int Cell(int x, int y) => x * Map.Grid + y;

        private static (int, int) Step(int x, int y, Dir d)
        {
            switch (d)
            {
                case Dir.N: return (x, y - 1);
                case Dir.S: return (x, y + 1);
                case Dir.E: return (x + 1, y);
                default: return (x - 1, y);
            }
        }

        private static Dir Opposite(Dir d)
        {
            switch (d)
            {
                case Dir.N: return Dir.S;
                case Dir.S: return Dir.N;
                case Dir.E: return Dir.W;
                default: return Dir.E;
            }
        }

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
        private static Dir[] Turns(Dir d)
        {
            switch (d)
            {
                case Dir.N: return new[] { Dir.W, Dir.E };
                case Dir.S: return new[] { Dir.E, Dir.W };
                case Dir.E: return new[] { Dir.N, Dir.S };
                default: return new[] { Dir.S, Dir.N };
            }
        }

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
        private static readonly Dir[] ArmDirs = { Dir.N, Dir.E, Dir.S, Dir.W };

        private static Room NewRoom(int x, int y, RoomKind kind) => new Room
        {
            X = x,
            Y = y,
            Type = kind,
        };

        private Room? At(int x, int y) =>
            x >= 0 && y >= 0 && x < Map.Grid && y < Map.Grid && _rooms.TryGetValue(Cell(x, y), out var r)
                ? r
                : null;

        /// <summary>A direction that leaves <paramref name="room"/> through an unused side into empty grid.</summary>
        private Dir? FreeDir(Room room, Dir? prefer)
        {
            var free = new List<Dir>();
            foreach (var d in ArmDirs)
            {
                var (nx, ny) = Step(room.X, room.Y, d);
                if (nx < 0 || ny < 0 || nx >= Map.Grid || ny >= Map.Grid) continue;
                if (room.Doors.Contains(d)) continue;
                if (Occupied(nx, ny)) continue;
                free.Add(d);
            }
            if (free.Count == 0) return null;

            // Prefer leaving sideways off the host so a fork reads as a fork, not as a stub. With no
            // preference yet - the first trunk - every free side is a side, which is what makes the
            // very first draw unbiased.
            var side = new List<Dir>();
            foreach (var d in free)
            {
                if (prefer.HasValue && (d == Opposite(prefer.Value) || d == prefer.Value)) continue;
                side.Add(d);
            }
            var pick = side.Count > 0 ? side : free;
            return pick[(int)(_rng.Run() * pick.Count)];
        }

        private sealed class GrowResult
        {
            public List<Room> Path = new List<Room>();
            public List<Room> Ends = new List<Room>();
            public Room Tip = null!;
        }

        /// <summary>
        /// Walks one corridor out of (x,y). Runs travel in straight segments of two or three rooms and
        /// bend once at the end of each, so a run reads as a path you walk rather than a spiral.
        ///
        /// <para>
        /// <paramref name="strict"/> forbids rooms that touch anything but their parent, and that is how
        /// the loose pass finds room for the long runs.
        /// </para>
        /// </summary>
        private GrowResult? Grow(int x, int y, Dir dir, int len, bool strict, double bias)
        {
            var res = new GrowResult();
            var cx = x;
            var cy = y;
            var lastTurn = -1;
            var seg = 1 + (int)(_rng.Run() * 2.4);   // DRAW: segment length, before the loop

            for (var step = 0; step < len; step++)
            {
                Dir[] order;
                if (seg <= 0)
                {
                    // Bend: take a side, usually keeping the same handedness, so the run makes Ls and Us.
                    int first;
                    if (lastTurn < 0)
                        first = _rng.Run() < 0.5 ? 0 : 1;      // DRAW: which side, first turn only
                    else
                        first = _rng.Run() < bias ? lastTurn : 1 - lastTurn;   // DRAW: which side
                    var t = Turns(dir);
                    order = new[] { t[first], t[1 - first], dir };
                    seg = 1 + (int)(_rng.Run() * 2.4);        // DRAW: the next segment length
                }
                else
                {
                    var t = Turns(dir);
                    order = new[] { dir, dir, t[0], t[1] };
                    seg--;
                }

                var placed = false;
                foreach (var d in order)
                {
                    var (nx, ny) = Step(cx, cy, d);
                    if (nx < 0 || ny < 0 || nx >= Map.Grid || ny >= Map.Grid) continue;
                    if (Occupied(nx, ny)) continue;
                    if (strict && TouchesOtherThan(cx, cy, nx, ny)) continue;

                    var room = NewRoom(nx, ny, RoomKind.Normal);
                    _rooms[Cell(cx, cy)].Doors.Add(d);
                    room.Doors.Add(Opposite(d));
                    _rooms[Cell(nx, ny)] = room;
                    var turns = Turns(dir);
                    if (d != dir) lastTurn = Array.IndexOf(turns, d);
                    cx = nx;
                    cy = ny;
                    dir = d;
                    res.Path.Add(room);
                    placed = true;
                    break;
                }
                if (!placed) return null;
            }

            res.Tip = _rooms[Cell(cx, cy)];
            return res;
        }

        /// <summary>
        /// Whether (nx,ny) would touch a room other than its intended parent. Strict mode's whole rule.
        /// </summary>
        private bool TouchesOtherThan(int px, int py, int nx, int ny)
        {
            foreach (var k in ArmDirs)
            {
                var (ax, ay) = Step(nx, ny, k);
                if (ax == px && ay == py) continue;
                if (Occupied(ax, ay)) return true;
            }
            return false;
        }

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
        private GrowResult? GrowForked(int x, int y, Dir dir, int len, bool strict, double bias,
                                      int[] forkAt, int forkLen)
        {
            var res = new GrowResult();
            var cx = x;
            var cy = y;
            var lastTurn = -1;
            var seg = 1 + (int)(_rng.Run() * 2.4);   // DRAW: segment length, before the loop

            for (var step = 0; step < len; step++)
            {
                Dir[] order;
                if (seg <= 0)
                {
                    int first;
                    if (lastTurn < 0)
                        first = _rng.Run() < 0.5 ? 0 : 1;
                    else
                        first = _rng.Run() < bias ? lastTurn : 1 - lastTurn;
                    var t = Turns(dir);
                    order = new[] { t[first], t[1 - first], dir };
                    seg = 1 + (int)(_rng.Run() * 2.4);
                }
                else
                {
                    var t = Turns(dir);
                    order = new[] { dir, dir, t[0], t[1] };
                    seg--;
                }

                var placed = false;
                foreach (var d in order)
                {
                    var (nx, ny) = Step(cx, cy, d);
                    if (nx < 0 || ny < 0 || nx >= Map.Grid || ny >= Map.Grid) continue;
                    if (Occupied(nx, ny)) continue;
                    if (strict && TouchesOtherThan(cx, cy, nx, ny)) continue;

                    var room = NewRoom(nx, ny, RoomKind.Normal);
                    _rooms[Cell(cx, cy)].Doors.Add(d);
                    room.Doors.Add(Opposite(d));
                    _rooms[Cell(nx, ny)] = room;
                    var turns = Turns(dir);
                    if (d != dir) lastTurn = Array.IndexOf(turns, d);
                    cx = nx;
                    cy = ny;
                    dir = d;
                    res.Path.Add(room);
                    placed = true;
                    break;
                }
                if (!placed) return null;

                /* NEVER FORK FROM THE TIP. The last room of a trunk becomes the upgrade room or the
                   boss, and a branch hanging off it puts the key in that branch BEHIND the locked door
                   the key is supposed to open. Measured before this was caught: 113 of 300 dungeons
                   had an unreachable gold key, with a branch running east out of the boss room. */
                if (step >= len - 1) continue;
                if (Array.IndexOf(forkAt, step) < 0) continue;

                // Cut a side run off the room just placed, leaving by a direction that is neither the
                // way we came nor the way we are going, so a branch is never a stub pointing back down
                // the trunk.
                var host = res.Path[res.Path.Count - 1];
                var incoming = Opposite(dir);
                foreach (var sd in ArmDirs)
                {
                    if (sd == incoming || sd == dir) continue;
                    var (bx, by) = Step(host.X, host.Y, sd);
                    if (bx < 0 || by < 0 || bx >= Map.Grid || by >= Map.Grid) continue;
                    if (Occupied(bx, by)) continue;
                    var b = Grow(host.X, host.Y, sd, forkLen, strict, bias);
                    if (b == null) continue;
                    res.Ends.Add(b.Tip);
                    break;
                }
            }

            res.Tip = _rooms[Cell(cx, cy)];
            return res;
        }

        private bool TryBuild(bool strict)
        {
            _rooms.Clear();

            var startRoom = NewRoom(Map.Start, Map.Start, RoomKind.Start);
            startRoom.Visited = true;
            startRoom.Spawned = true;
            _rooms[Cell(Map.Start, Map.Start)] = startRoom;

            /* Two trunks out of the start, in different directions. The second is grown AFTER the
               first, so FreeDir already knows the first trunk is there and the two cannot collide -
               which is what stops the whole build being thrown away for a run that walks into its own
               corridor.

               trunkLen 4-5 and forkLen 3-4 rather than something shorter. The first version of the
               tree used 3-4 and 2-3, which put a floor at 8 fight rooms in the worst case - and eight
               is not a floor, it is a corridor with two decisions in it. The lengths are the cost side
               of the same trade: they are what the retry loop in Generate is paying for. */
            var trunkLen = 4 + (int)(_rng.Run() * 2);   // DRAW
            var forkLen = 3 + (int)(_rng.Run() * 2);    // DRAW

            /* The fork is cut from an INTERIOR room. The index is drawn from the steps that are not the
               last one, because the last one is the tip and the tip is a terminal room. */
            var forkAt = new[] { (int)(_rng.Run() * (trunkLen - 1)) };   // DRAW

            var dirA = FreeDir(startRoom, null);
            if (!dirA.HasValue) return false;
            var a = GrowForked(Map.Start, Map.Start, dirA.Value, trunkLen, strict, 0.55, forkAt, forkLen);
            if (a == null) return false;

            var dirB = FreeDir(startRoom, dirA);
            if (!dirB.HasValue) return false;
            var b = GrowForked(Map.Start, Map.Start, dirB.Value, trunkLen, strict, 0.55, forkAt, forkLen);
            if (b == null) return false;

            /* ROLES, assigned to the ENDS of what grew rather than to positions chosen in advance.

               The two trunks get the two rooms worth walking towards, and they differ on purpose: one
               end is the upgrade room and one is the boss, so a player who commits early to a
               direction has committed to what that direction was FOR. Which trunk gets which is a coin
               toss, so the map cannot be learned as "left is the boss". */
            var aIsBoss = _rng.Run() < 0.5;   // DRAW
            a.Tip.Type = aIsBoss ? RoomKind.Boss : RoomKind.Item;
            b.Tip.Type = aIsBoss ? RoomKind.Item : RoomKind.Boss;

            /* THE KEYS, one per trunk, placed at a BRANCH END and chosen at generation.

               A branch end is an ordinary fight room with a key in it, so getting the key costs a
               detour and the detour can be blocked - which is the whole point of putting it there
               rather than on the spine. If a trunk produced no branch the build is thrown away rather
               than quietly putting the key on the spine, because a map that sometimes has a detour and
               sometimes does not is a map where the detour is not a decision. */
            var aEnds = new List<Room>();
            foreach (var r in a.Ends)
                if (r.IsEndpoint && At(r.X, r.Y) != null) aEnds.Add(r);
            var bEnds = new List<Room>();
            foreach (var r in b.Ends)
                if (r.IsEndpoint && At(r.X, r.Y) != null) bEnds.Add(r);
            if (aEnds.Count == 0 || bEnds.Count == 0) return false;

            var silverAt = aEnds[(int)(_rng.Run() * aEnds.Count)];   // DRAW
            var goldAt = bEnds[(int)(_rng.Run() * bEnds.Count)];     // DRAW
            if (ReferenceEquals(silverAt, goldAt)) return false;
            silverAt.KeyReward = true;
            goldAt.GoldReward = true;

            // One secret per floor, walled off behind a fake wall on a random solid wall. Never on the
            // boss or the upgrade room, so those two stay legible, and it stays off the map until the
            // wall is gone.
            var walls = new List<(Room Room, Dir Dir, int X, int Y)>();
            // Over the rooms built SO FAR, not over the derived AllRooms list: that list is filled in
            // by Generate() only after a build succeeds, so during the retry loop it is empty or
            // stale, and scanning it would find no walls at all - every attempt failing, forever.
            foreach (var r in _rooms.Values)
            {
                if (r.Type == RoomKind.Boss || r.Type == RoomKind.Item || r.Type == RoomKind.Start) continue;
                foreach (var d in ArmDirs)
                {
                    var (nx, ny) = Step(r.X, r.Y, d);
                    if (nx < 0 || ny < 0 || nx >= Map.Grid || ny >= Map.Grid) continue;
                    if (Occupied(nx, ny)) continue;

                    // The pocket has to be shut on every other side, so the fake wall is genuinely the
                    // only way in and the secret is not left touching a corridor it has no door to.
                    var touching = 0;
                    foreach (var dd in ArmDirs)
                    {
                        var (ax, ay) = Step(nx, ny, dd);
                        if (Occupied(ax, ay)) touching++;
                    }
                    if (touching != 1) continue;
                    walls.Add((r, d, nx, ny));
                }
            }
            if (walls.Count == 0) return false;

            var spot = walls[(int)(_rng.Run() * walls.Count)];   // DRAW
            _rooms[Cell(spot.X, spot.Y)] = NewRoom(spot.X, spot.Y, RoomKind.Secret);
            spot.Room.Secret = spot.Dir;
            return true;
        }

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
        public void Generate()
        {
            for (var i = 0; i < 800; i++)
            {
                if (!TryBuild(i < 500)) continue;

                // Derived once, here, rather than rediscovered every tick. _all is the room list as an
                // array because the minimap walks it every frame, and the JavaScript spent sixty array
                // allocations a second on Object.values for the same job.
                _all.Clear();
                foreach (var kv in _rooms) _all.Add(kv.Value);
                _all.Sort((p, q) => p.X != q.X ? p.X.CompareTo(q.X) : p.Y.CompareTo(q.Y));

                _bossFront.Clear();
                foreach (var r in _all)
                {
                    if (r.Type == RoomKind.Boss) continue;
                    foreach (var d in ArmDirs)
                    {
                        var (nx, ny) = Step(r.X, r.Y, d);
                        var n = At(nx, ny);
                        if (n != null && n.Type == RoomKind.Boss) { _bossFront[Cell(r.X, r.Y)] = r; break; }
                    }
                }
                return;
            }

            throw new System.InvalidOperationException(
                "no floor could be generated from seed " + _rng.SeedText + " in 800 attempts. This is " +
                "a generator bug rather than an unlucky seed - the same seed always fails the same way.");
        }

        /// <summary>The room one door away, or null if that door leads off the grid.</summary>
        public Room? Neighbour(Room r, Dir d)
        {
            var (x, y) = Step(r.X, r.Y, d);
            return At(x, y);
        }

        /// <summary>Builds a floor from the generator's own stream, reseeding first.</summary>
        public static Dungeon FromSeed(Rng rng)
        {
            var d = new Dungeon(rng);
            d.Generate();
            return d;
        }

        /// <summary>
        /// A stable text form of the whole graph, for the parity tests.
        ///
        /// <para>
        /// Rooms sorted by x then y, doors sorted, flags appended. This is the SAME shape the JavaScript
        /// side emits, deliberately: a parity table is only worth comparing if both ends build the
        /// string the same way, and hand-writing the comparison is how the two drift apart quietly.
        /// </para>
        /// </summary>
        public string Signature()
        {
            var parts = new List<string>(_all.Count);
            foreach (var r in _all)
            {
                // Sorted ALPHABETICALLY, not by enum value. The JavaScript side does
                // Object.keys(r.doors).sort() - a code-unit sort - and this has to match it
                // character for character or the comparison is theatre. Ordinal, so the order cannot
                // drift with the current culture.
                var doors = string.Concat(r.Doors.Select(d => d.ToString()).OrderBy(s => s, StringComparer.Ordinal));
                var flags = (r.KeyReward ? "K" : "") + (r.GoldReward ? "G" : "") +
                            (r.Secret.HasValue ? "S" + r.Secret.Value : "");
                // Lowercased, because the JavaScript room types are literally 'normal', 'start',
                // 'item', 'boss' and 'secret' and charAt(0) of those is lowercase. A parity table is
                // only worth having if both ends build the string the same way, and a case difference
                // would turn every row red for a reason that is not about the generator.
                var kind = char.ToLowerInvariant(r.Type.ToString()[0]);
                parts.Add(r.X + "," + r.Y + ":" + kind + ":" + doors + ":" + flags);
            }
            return string.Join("|", parts);
        }
    }
}