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

    /// <summary>One room on the grid. [h:World-1]</summary>
    public sealed class Room
    {
        public int X, Y;
        public RoomKind Type;

        /// <summary>Doors, as a set of directions. [h:World-2]</summary>
        public HashSet<Dir> Doors = new HashSet<Dir>();

        public bool Visited, Spawned;

        /// <summary>The room has been emptied, at least once. [h:World-3]</summary>
        public bool Cleared;
        /// <summary>Its wave spawned bodies (a Normal room's plan, the Warden): the wave decides, not the type.
        /// Only clearing such a room refills the regen heart; a real run's start, item and secret rooms never do.</summary>
        public bool Fought;

        /// <summary>The silver key has been dropped here, so it must not be dropped twice. [h:World-4]</summary>
        public bool KeySpawned;

        /// <summary>The gold key has been dropped here. One-shot, like <see cref="KeySpawned"/>.</summary>
        public bool GoldSpawned;

        /// <summary>The way down is open. [h:World-5]</summary>
        public bool ExitOpen;

        /// <summary>Holds the silver key. Exactly one room per floor.</summary>
        public bool KeyReward;
        public bool Armed;   // a live room hands back the cooldowns once, on first entry
        public SpikeKind Spike;   // a spike room (Spikes.Mark): an elite or a crowded wave, a sure drop
        // what the room holds while the player is elsewhere (the run's lists are the current room's)
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Pickup> Pickups = new List<Pickup>();

        /// <summary>Holds the gold key. Exactly one room per floor.</summary>
        public bool GoldReward;

        /// <summary>The direction of the fake wall that hides the secret, or null. [h:World-6]</summary>
        public Dir? Secret;

        /// <summary>The room's own rectangle. [h:World-7]</summary>
        public RoomBounds Bounds = RoomBounds.Standard;

        /// <summary>A room is an endpoint when it has exactly one door. Nothing else counts.</summary>
        public bool IsEndpoint => Doors.Count == 1;

        public override string ToString() => $"({X},{Y}) {Type} [{string.Join("", Doors)}]";
    }

    /// <summary>The map constants, grouped because they are meaningless apart. [h:World-8]</summary>
    public static class Map
    {
        public const int Grid = 7;
        public const int Start = 3;
    }

    /// <summary>THE DUNGEON, and the generator that builds it. [h:World-9]</summary>
    public sealed class Dungeon
    {
        private readonly Rng _rng;
        private readonly Dictionary<int, Room> _rooms = new Dictionary<int, Room>();

        /// <summary>Every room, in a stable order. Derived once at generation, not rebuilt per tick.</summary>
        private readonly List<Room> _all = new List<Room>();

        /// <summary>The rooms with a door onto the boss. [h:World-10]</summary>
        private readonly Dictionary<int, Room> _bossFront = new Dictionary<int, Room>();

        public Dungeon(Rng rng) => _rng = rng ?? throw new System.ArgumentNullException(nameof(rng));

        public IReadOnlyList<Room> AllRooms => _all;

        /// <summary>The run seed, so a caller can print what a floor was actually built from.</summary>
        public uint Seed => _rng.Seed;

        // THE GRID KEY, and why it must never be asked about a cell outside the grid. [h:World-11]
        private static bool InGrid(int x, int y) =>
            x >= 0 && y >= 0 && x < Map.Grid && y < Map.Grid;

        private bool Occupied(int x, int y) =>
            InGrid(x, y) && _rooms.ContainsKey(Cell(x, y));

        /// <summary>The room at a grid position, or null if nothing was generated there. [h:World-12]</summary>
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

        /// <summary>The two directions that are NOT straight ahead, and the hand they turn with. [h:World-13]</summary>
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

        /// <summary>The direction list, in the order the generator considers them. [h:World-14]</summary>
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

            // Prefer leaving sideways off the host so a fork reads as a fork, not as a stub. [h:World-15]
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

        /// <summary>Walks one corridor out of (x,y). [h:World-16]</summary>
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

        /// <summary>Whether (nx,ny) would touch a room other than its intended parent. [h:World-17]</summary>
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

        /// <summary>One trunk, with side runs cut off it. [h:World-18]</summary>
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

                // NEVER FORK FROM THE TIP. [h:World-19]
                if (step >= len - 1) continue;
                if (Array.IndexOf(forkAt, step) < 0) continue;

                // Cut a side run off the room just placed, leaving by a direction that is neither the way we came nor the way... [h:World-20]
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

            // Two trunks out of the start, in different directions. [h:World-21]
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

            // ROLES, assigned to the ENDS of what grew rather than to positions chosen in advance. [h:World-22]
            var aIsBoss = _rng.Run() < 0.5;   // DRAW
            a.Tip.Type = aIsBoss ? RoomKind.Boss : RoomKind.Item;
            b.Tip.Type = aIsBoss ? RoomKind.Item : RoomKind.Boss;

            // THE KEYS, one per trunk, placed at a BRANCH END and chosen at generation. [h:World-23]
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

            // One secret per floor, walled off behind a fake wall on a random solid wall. [h:World-24]
            var walls = new List<(Room Room, Dir Dir, int X, int Y)>();
            // Over the rooms built SO FAR, not over the derived AllRooms list: that list is filled in by Generate() only... [h:World-25]
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

        /// <summary>Builds a floor, retrying until one takes. [h:World-26]</summary>
        public void Generate()
        {
            for (var i = 0; i < 800; i++)
            {
                if (!TryBuild(i < 500)) continue;

                // Derived once, here, rather than rediscovered every tick. [h:World-27]
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

        /// <summary>A stable text form of the whole graph, for the parity tests. [h:World-28]</summary>
        public string Signature()
        {
            var parts = new List<string>(_all.Count);
            foreach (var r in _all)
            {
                // Sorted ALPHABETICALLY, not by enum value. [h:World-29]
                var doors = string.Concat(r.Doors.Select(d => d.ToString()).OrderBy(s => s, StringComparer.Ordinal));
                var flags = (r.KeyReward ? "K" : "") + (r.GoldReward ? "G" : "") +
                            (r.Secret.HasValue ? "S" + r.Secret.Value : "");
                // Lowercased, because the JavaScript room types are literally 'normal', 'start', 'item', 'boss' and 'secret'... [h:World-30]
                var kind = char.ToLowerInvariant(r.Type.ToString()[0]);
                parts.Add(r.X + "," + r.Y + ":" + kind + ":" + doors + ":" + flags);
            }
            return string.Join("|", parts);
        }
    }
}