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

    /// <summary>One body's place inside a pack, and the two numbers the formation code is given. [h:SpawnPlan-1]</summary>
    public sealed class PlannedBody
    {
        public double X, Y;

        /// <summary>What this body will be. Always Brunch here - a pack is the only multi-body case.</summary>
        public BodyKind Kind;

        /// <summary>Zero for a lone body. Every body of one pack shares the same non-zero id.</summary>
        public int PackId;

        public int PackSlot;
        /// <summary>The band trait rolled for a ranged body (0 none, 1 hold, 2 close); applied by Spawn.Body.</summary>
        public int Trait;
    }

    /// <summary>A whole wave, as data: the slots, and the bodies those slots become.</summary>
    public sealed class WavePlan
    {
        public List<SpawnSlot> Slots = new List<SpawnSlot>();
        public List<PlannedBody> Bodies = new List<PlannedBody>();

    }

    /// <summary>THE SPAWN PLANNER, and the one piece of run-scoped state in the port that is not the RNG. [h:SpawnPlan-2]</summary>
    public sealed class WavePlanner
    {
        private readonly Rng _rng;
        private int _packCursor = 1;

        /// <summary>How many packs this run has handed out. Exposed so the tests can prove ids are not reused.</summary>
        public int PacksIssued => _packCursor - 1;

        public WavePlanner(Rng rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        /// <summary>Where the player walks in, which is what "far from the door" is measured from. [h:SpawnPlan-3]</summary>
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

        /// <summary>Picks count places in the room for bodies to stand. [h:SpawnPlan-4]</summary>
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

        /// <summary>The distance to the nearest already-placed point, or a large finite number when there are none yet. [h:SpawnPlan-5]</summary>
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

        /// <summary>How many bodies a pack is, by weight. [h:SpawnPlan-6]</summary>
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

        /// <summary>Plans a whole wave for a room the player enters from fromDir. [h:SpawnPlan-7]</summary>
        public WavePlan PlanWave(int floor, Dir fromDir, Area area = Area.Area1, int weaponIdx = 0, BodyKind[][]? pools = null, double crowd = 1)
        {
            if (pools != null) return PlanPooled(floor, fromDir, area, weaponIdx, pools, crowd);
            var mix = Bodies.MixFor(area);
            var rolled = 2 + (int)(_rng.Run() * (2 + Balance.PressureRate));
            var n = Math.Max(2, (int)Math.Floor((rolled + Balance.DepthBodies(floor, 0)) * crowd));

            var pts = Plan(n, fromDir);

            // At most one gunner, and only where there are enough bodies to space it from the rest. [h:SpawnPlan-8]
            var heavy = n >= 3 && _rng.Run() < mix.Heavy + Curve.At(floor).Heavy;

            // A pack is the most interesting thing a room can contain and the most reliable cover, so if deep floors were... [h:SpawnPlan-9]
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
            if (crowd > 1) ExtraPack(slots);

            return Fill(plan, weaponIdx);
        }

        // a crowded spike room's second Brunch pack, on a slot that is not the gunner's or a pack already
        void ExtraPack(List<SpawnSlot> slots)
        {
            for (int i = 1; i < slots.Count; i++)
                if (slots[i].Kind != BodyKind.Brunch && slots[i].Kind != BodyKind.Gunner) { slots[i].Kind = BodyKind.Brunch; slots[i].Pack = RollPack(); return; }
        }

        /// <summary>The wave from the run's area groups (Pools, Balance.TunePools): the same count, the same spots, the same... [h:SpawnPlan-10]</summary>
        WavePlan PlanPooled(int floor, Dir fromDir, Area area, int weaponIdx, BodyKind[][] pools, double crowd)
        {
            var mix = Bodies.MixFor(area);
            var rolled = 2 + (int)(_rng.Run() * (2 + Balance.PressureRate));
            var n = Math.Max(2, (int)Math.Floor((rolled + Balance.DepthBodies(floor, 0)) * crowd));
            var pts = Plan(n, fromDir);
            double wGunner = Pools.Weight(pools, floor, BodyKind.Gunner), wBrunch = Pools.Weight(pools, floor, BodyKind.Brunch);
            var heavy = n >= 3 && wGunner > 0 && _rng.Run() < (mix.Heavy + Curve.At(floor).Heavy) * wGunner;
            var pack = wBrunch > 0 && n >= 2 && _rng.Run() < Balance.DepthPack(floor, mix.Brunch) * wBrunch ? RollPack() : 0;
            var slots = pts.OrderByDescending(p => p.FromEntry).ToList();

            var kinds = new List<BodyKind>();
            var weights = new List<double>();
            foreach (var k in Pools.Kinds)
            {
                double w = Pools.Weight(pools, floor, k);
                if (w > 0 && Pools.Slot(k)) { kinds.Add(k); weights.Add(w); }
            }
            if (kinds.Count == 0) { kinds.Add(BodyKind.Lunger); weights.Add(1); }   // a floor whose groups are all Gunner and Brunch
            int eggs = 0;
            foreach (var s in slots)
            {
                s.Kind = Draw(kinds, weights, eggs >= Pools.MaxEggs);
                if (s.Kind == BodyKind.Egg) eggs++;
            }
            if (heavy) slots[0].Kind = BodyKind.Gunner;
            foreach (var s in slots)
            {
                if (s.Kind == BodyKind.Gunner || Pools.Comes(s.Kind) || s.FromEntry >= Balance.SpawnFar) continue;
                double comes = 0;
                for (int i = 0; i < kinds.Count; i++) if (Pools.Comes(kinds[i])) comes += weights[i];
                if (comes <= 0) continue;   // nothing that walks in: the shooter keeps its spot
                double r = _rng.Run() * comes;
                for (int i = 0; i < kinds.Count; i++)
                    if (Pools.Comes(kinds[i]) && (r -= weights[i]) < 0) { s.Kind = kinds[i]; break; }
            }
            var plan = new WavePlan { Slots = slots };
            if (pack > 0 && slots.Count > 1)
            {
                var victim = slots[1 + (int)(_rng.Run() * (slots.Count - 1))];
                victim.Kind = BodyKind.Brunch;
                victim.Pack = pack;
            }
            if (crowd > 1 && wBrunch > 0) ExtraPack(slots);   // only where the floor's groups hold the Brunch
            return Fill(plan, weaponIdx);
        }

        BodyKind Draw(List<BodyKind> kinds, List<double> weights, bool noEgg)
        {
            double total = 0;
            for (int i = 0; i < kinds.Count; i++) if (!(noEgg && kinds[i] == BodyKind.Egg)) total += weights[i];
            if (total <= 0) return BodyKind.Lunger;
            double r = _rng.Run() * total;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (noEgg && kinds[i] == BodyKind.Egg) continue;
                if ((r -= weights[i]) < 0) return kinds[i];
            }
            for (int i = kinds.Count - 1; i >= 0; i--) if (!(noEgg && kinds[i] == BodyKind.Egg)) return kinds[i];   // rounding at the top end
            return BodyKind.Lunger;
        }

        // The bodies of a planned wave, slot by slot: a lone body with its trait roll, a pack as its knot.
        WavePlan Fill(WavePlan plan, int weaponIdx)
        {
            foreach (var s in plan.Slots)
            {
                if (s.Pack <= 0)
                {
                    // THE TRAIT ROLL IS PART OF SPAWNING A BODY, and the port left it out, so every gunner cost two draws fewer... [h:SpawnPlan-11]
            // rollTrait reads the gun the PLAYER holds (TRAIT_TABLE by weapon), not the body's kind
            int trait = Spawn.TakesTrait(s.Kind) ? Spawn.RollTrait(_rng, weaponIdx) : 0;
            plan.Bodies.Add(new PlannedBody { X = s.X, Y = s.Y, Kind = s.Kind, Trait = trait });
                    continue;
                }

                // THE ID IS PER PACK, TAKEN ONCE, BEFORE THE BODIES. [h:SpawnPlan-12]
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
}
