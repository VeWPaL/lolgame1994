using System;
using System.Collections.Generic;
using System.Linq;

namespace Depths
{
    /// <summary>One body's place in a wave, before it exists.</summary>
    public sealed class SpawnSlot
    {
        public double X, Y;

        /// <summary>Distance from the door the player will come in by. The whole scoring function hangs off this.</summary>
        public double FromEntry;

        public BodyKind Kind;

        /// <summary>Bodies in the pack that takes this slot over, or 0 if this slot is a single body.</summary>
        public int Pack;
    }

    /// <summary>
    /// One body's place inside a pack, and the two numbers the formation code is given.
    ///
    /// <para>
    /// A pack id and a slot index are the ENTIRE interface between "where a pack was placed" and "how
    /// a pack moves". Nothing downstream needs to know how many packs a floor has, where they started,
    /// or what a pack is: it asks which wall a body belongs to and where in it the body stands.
    /// </para>
    /// </summary>
    public sealed class PlannedBody
    {
        public double X, Y;

        /// <summary>What this body will be. Always Brunch here - a pack is the only multi-body case.</summary>
        public BodyKind Kind;

        /// <summary>Zero for a lone body. Every body of one pack shares the same non-zero id.</summary>
        public int PackId;

        public int PackSlot;
    }

    /// <summary>A whole wave, as data: the slots, and the bodies those slots become.</summary>
    public sealed class WavePlan
    {
        public List<SpawnSlot> Slots = new List<SpawnSlot>();
        public List<PlannedBody> Bodies = new List<PlannedBody>();

        /// <summary>How many bodies actually arrive - a pack slot is several bodies.</summary>
        public int BodyCount => Bodies.Count;
    }

    /// <summary>
    /// THE SPAWN PLANNER, and the one piece of run-scoped state in the port that is not the RNG.
    ///
    /// <para>
    /// One per RUN, not one per floor. That distinction is the whole reason this is a class rather than
    /// a <c>static int</c>, and it comes from finding #8: the JavaScript kept <c>PACK_CURSOR</c> at
    /// module scope, which meant it survived <c>startGame</c> and could not be reset by a floor change
    /// either. Both halves of that were wrong for different reasons - it leaked ACROSS runs, and
    /// resetting it per FLOOR would have been wrong too, because a pack id only has to be unique within
    /// a run and a body that outlives a floor transition must not collide with a pack it has never met.
    /// </para>
    ///
    /// <para>
    /// So the cursor lives here, on the object that outlives floors, and the constructor is where it
    /// starts. C# would not have warned about a <c>static</c> here - a module-level counter reads
    /// naturally as static, and nothing in the type system connects a static field to the run that owns
    /// it. The class makes the scoping structural instead of a comment: two planners cannot share a
    /// cursor, and a planner cannot be reused for a second run without being constructed again.
    /// </para>
    ///
    /// <para>
    /// <b>THE CONTRACT IS DRAW ORDER, as in the generator.</b> The plan costs a fixed 640 draws for the
    /// candidate loop plus a tail that depends on the room, and the tail is what the parity rows pin -
    /// measured across floors 1 to 12, 644 draws for a two-body room with no pack, 650 for a five-body
    /// room with one.
    /// </para>
    /// </summary>
    public sealed class WavePlanner
    {
        private readonly Rng _rng;
        private int _packCursor = 1;

        /// <summary>How many packs this run has handed out. Exposed so the tests can prove ids are not reused.</summary>
        public int PacksIssued => _packCursor - 1;

        public WavePlanner(Rng rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        /// <summary>
        /// Where the player walks in, which is what "far from the door" is measured from.
        ///
        /// <para>
        /// Inside the wall by 36 rather than ON it, because the door itself is drawn at the wall and a
        /// spawn that cleared the doorway by a body's radius would still be standing in it.
        /// </para>
        /// </summary>
        public static (double X, double Y) EntryPoint(Dir dir)
        {
            switch (dir)
            {
                case Dir.N: return (Balance.MidX, Balance.RoomTop + Balance.EntryInset);
                case Dir.S: return (Balance.MidX, Balance.RoomBottom - Balance.EntryInset);
                case Dir.E: return (Balance.RoomRight - Balance.EntryInset, Balance.MidY);
                case Dir.W: return (Balance.RoomLeft + Balance.EntryInset, Balance.MidY);
                // The middle of the room, for a direction that is not one of the four. The JavaScript
                // gets this from `||` on a table lookup; a switch needs it stated.
                default: return (Balance.MidX, Balance.MidY);
            }
        }

        // Math.Sqrt(dx*dx + dy*dy), NOT Math.hypot. See TheHypotDivergenceIsRealAndDoesNotMatter -
        // measured, not assumed.
        private static double Dist(double ax, double ay, double bx, double by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Picks <paramref name="count"/> places in the room for bodies to stand.
        ///
        /// <para>
        /// 320 candidate points are drawn and filtered rather than points being accepted one at a time,
        /// because the filters are not independent: a point rejected for being in the middle of the
        /// room should not consume the body it was drawn for. 320 against a room 700x450 leaves a
        /// healthy surplus after both filters, and the surplus is what the spacing pass chooses from.
        /// </para>
        ///
        /// <para>
        /// THE FIRST PICK IS OPTIMISED AGAINST THE DOOR AND THE LATER ONES AGAINST THE BODIES ALREADY
        /// PLACED. That is deliberate. A room where the first body is chosen for distance from the other
        /// bodies has nobody to compare it to, so it lands wherever the arithmetic happens to put it -
        /// which is why the nearest-of number is a large finite 1e4 and not Infinity. With Infinity
        /// every candidate scores the same and "farthest from the door" silently degenerates into "the
        /// first candidate drawn", which is the top-left of the room every time.
        /// </para>
        /// </summary>
        public List<SpawnSlot> Plan(int count, Dir fromDir)
        {
            var (ex, ey) = EntryPoint(fromDir);
            var cand = new List<double[]>();
            var spare = new List<double[]>();

            for (var i = 0; i < 320; i++)
            {
                var x = Balance.RoomLeft + Balance.SpawnMargin +
                        _rng.Run() * (Balance.RoomRight - Balance.RoomLeft - Balance.SpawnMargin * 2);
                var y = Balance.RoomTop + Balance.SpawnMargin +
                        _rng.Run() * (Balance.RoomBottom - Balance.RoomTop - Balance.SpawnMargin * 2);

                if (Dist(x, y, Balance.MidX, Balance.MidY) < Balance.SpawnMid) continue;  // the middle stays walkable
                if (Dist(x, y, ex, ey) < Balance.SpawnDoor) spare.Add(new[] { x, y });     // nothing starts in a doorway
                else cand.Add(new[] { x, y });
            }

            // Doorway-adjacent points are usable, just worse, so they are only drawn on when there are
            // not enough good ones. Filling from `spare` cannot overflow the room: it is bounded by 320.
            foreach (var s in spare)
            {
                if (cand.Count >= count + 4) break;
                cand.Add(s);
            }

            // The fallback ring. Unreachable in practice - 320 candidates against a count of at most 7 -
            // but a planner that can return fewer slots than asked for has a null reference waiting.
            while (cand.Count < count)
            {
                cand.Add(new[]
                {
                    Balance.MidX + Math.Cos(_rng.Run() * 6.283) * 200,
                    Balance.MidY + Math.Sin(_rng.Run() * 6.283) * 140,
                });
            }

            var pts = new List<SpawnSlot>(count);
            for (var i = 0; i < count; i++)
            {
                double? bestScore = null, roomyScore = null;
                double[]? best = null, roomy = null;

                foreach (var c in cand)
                {
                    var n = Nearest(c[0], c[1], pts);
                    if (roomyScore == null || n > roomyScore.Value) { roomyScore = n; roomy = c; }
                    if (n < Balance.SpawnSep) continue;
                    var score = Dist(c[0], c[1], ex, ey) + n * 1.4;
                    if (bestScore == null || score > bestScore.Value) { bestScore = score; best = c; }
                }

                // Room too tight for the spacing to be satisfiable: take the roomiest slot left rather
                // than the first, so a cramped room still gets its bodies spread out.
                var pick = best ?? roomy!;
                pts.Add(new SpawnSlot
                {
                    X = pick[0],
                    Y = pick[1],
                    FromEntry = Dist(pick[0], pick[1], ex, ey),
                });
            }

            return pts;
        }

        /// <summary>
        /// The distance to the nearest already-placed point, or a large finite number when there are
        /// none yet. See the note on 1e4 above - Infinity here silently breaks the first pick.
        /// </summary>
        private static double Nearest(double x, double y, List<SpawnSlot> pts)
        {
            if (pts.Count == 0) return 1e4;
            var m = double.PositiveInfinity;
            foreach (var p in pts)
            {
                var d = Dist(p.X, p.Y, x, y);
                if (d < m) m = d;
            }
            return m;
        }

        /// <summary>
        /// How many bodies a pack is, by weight.
        ///
        /// <para>
        /// The walk subtracts as it goes and falls off the end into the smallest size, which is only
        /// correct because the weights sum to exactly 1 - so the fall-off is unreachable in practice and
        /// the last return exists to satisfy the compiler rather than to be behaviour.
        /// </para>
        /// </summary>
        public int RollPack()
        {
            var r = _rng.Run();
            for (var i = 0; i < Bodies.BrunchPack.Length; i++)
            {
                if (r < Bodies.BrunchWeight[i]) return Bodies.BrunchPack[i];
                r -= Bodies.BrunchWeight[i];
            }
            return Bodies.BrunchPack[0];
        }

        /// <summary>
        /// Plans a whole wave for a room the player enters from <paramref name="fromDir"/>.
        ///
        /// <para>
        /// The density roll is 2 plus a fraction of a body, floored at 2. The floor of two is not
        /// politeness: a one-body room is not a fight, it is an obstacle, and the extra body from the
        /// depth ladder has to be able to make a room fuller rather than emptier.
        /// </para>
        /// <para>
        /// <c>depthBodies</c> ADDS to the roll rather than moving it, and that is the design: the
        /// ladder cannot change WHICH bodies a room draws, only how many. A deeper floor should be a
        /// fuller room of the same fight, not a different fight - the player learns the shapes on floor
        /// one and the shapes are still the shapes on floor twelve.
        /// </para>
        /// </summary>
        public WavePlan PlanWave(int floor, Dir fromDir, Area area = Area.Area1)
        {
            var mix = Bodies.MixFor(area);
            var rolled = 2 + (int)(_rng.Run() * (2 + Balance.PressureRate));
            var n = Math.Max(2, (int)Math.Floor(rolled + Balance.DepthBodies(floor, 0)));

            var pts = Plan(n, fromDir);

            // At most one gunner, and only where there are enough bodies to space it from the rest.
            // The n>=3 test SHORT-CIRCUITS, so a two-body room does not draw here at all. That is
            // load-bearing: it is why a two-body room costs 644 draws and a four-body room 647.
            var heavy = n >= 3 && _rng.Run() < mix.Heavy;

            // A pack is the most interesting thing a room can contain and the most reliable cover, so
            // if deep floors were only tougher they would be the same rooms with longer fights. Capped
            // in DepthPack, because a room that is always a pack is a single shape.
            var pack = _rng.Run() < Balance.DepthPack(floor, mix.Brunch) && n >= 2 ? RollPack() : 0;

            // Farthest-from-the-door first, so slot 0 is the one a gunner takes.
            var slots = pts.OrderByDescending(p => p.FromEntry).ToList();

            foreach (var s in slots)
            {
                s.Kind = _rng.Run() < mix.Lunger ? BodyKind.Lunger : BodyKind.Shooter;
            }
            if (heavy) slots[0].Kind = BodyKind.Gunner;
            foreach (var s in slots)
            {
                // No gunner starts on the doorstep, and no shooter starts inside its own comfortable
                // range either - a shooter at 150px is a body the player cannot answer with a Scatter.
                if (s.Kind != BodyKind.Gunner && s.Kind == BodyKind.Shooter && s.FromEntry < Balance.SpawnFar)
                    s.Kind = BodyKind.Lunger;
            }

            var plan = new WavePlan { Slots = slots };

            if (pack > 0 && slots.Count > 1)
            {
                // Never slot 0, because that is the gunner's.
                var victim = slots[1 + (int)(_rng.Run() * (slots.Count - 1))];
                victim.Kind = BodyKind.Brunch;
                victim.Pack = pack;
            }

            foreach (var s in plan.Slots)
            {
                if (s.Pack <= 0)
                {
                    plan.Bodies.Add(new PlannedBody { X = s.X, Y = s.Y, Kind = s.Kind });
                    continue;
                }

                /* THE ID IS PER PACK, TAKEN ONCE, BEFORE THE BODIES. The JavaScript incremented the
                   counter inside the body loop as well as after it, so every body in a pack got its own
                   id and no two of them ever agreed on which pack they were in. The wall rule asks for
                   packC[packId].n >= BrunchWallMin - a count of bodies sharing one id - so with a
                   unique id per body every pack counted 1 and the wall could never form.

                   Measured over 40 seeds and 221 rooms containing Brunch: 1227 bodies, 1227 distinct
                   ids, and ZERO packs reaching the threshold. An entire documented mechanic was dead in
                   the game and live only in the boss's hand-placed wall, which uses a fixed id and so
                   was unaffected. That is why it was never noticed: the one wall anyone had seen was
                   the one that worked. */
                var packId = _packCursor++;

                for (var i = 0; i < s.Pack; i++)
                {
                    // Two rings, so the knot is not a straight line - a line of Brunch is a wall you
                    // can walk around, which defeats the point of a pack being cover.
                    var a = (i / (double)s.Pack) * 6.283;
                    var rad = (i % 2) != 0 ? 25 : 13;
                    plan.Bodies.Add(new PlannedBody
                    {
                        X = s.X + Math.Cos(a) * rad,
                        Y = s.Y + Math.Sin(a) * rad,
                        Kind = BodyKind.Brunch,
                        PackId = packId,
                        PackSlot = i,
                    });
                }
            }

            return plan;
        }
    }

    /// <summary>
    /// THE HYPOT DIVERGENCE IS REAL AND DOES NOT MATTER - measured, because it is the kind of thing
    /// that gets assumed either way and both assumptions are wrong.
    ///
    /// <para>
    /// The JavaScript measures every distance with <c>Math.hypot</c>, which is not
    /// <c>Math.sqrt(dx*dx + dy*dy)</c>. Counted in the browser, replacing one with the other changes the
    /// RESULT 8,509,056 times, with a maximum relative error of 4.397e-16 - about two units in the last
    /// place. It is a different function, not a slower version of the same one.
    /// </para>
    ///
    /// <para>
    /// And it changed nothing here. Over 8,000 plans - 400 seeds, four entry directions, five counts -
    /// the resulting positions were byte-identical, zero differences. A two-ulp difference is far too
    /// small to reorder 320 candidates or to flip a comparison between two of them unless they are
    /// already tied to that precision.
    /// </para>
    ///
    /// <para>
    /// So the port uses <c>Math.Sqrt</c> throughout, which is what <c>Hit</c> and <c>Intercept</c>
    /// already do, and the parity table asserts positions EXACTLY while asserting distances to a tenth
    /// of a pixel. The distances genuinely do differ - 2,910 of the 8,000 first points - so a test
    /// comparing them at full double precision would fail on correct code, and one comparing them
    /// loosely would be hiding the difference rather than measuring it.
    /// </para>
    /// </summary>
    public static class HypotNote
    {
    }
}