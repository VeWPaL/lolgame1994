namespace Depths
{
    public enum SpikeKind { None, Elite, Crowded }

    /// <summary>Spike rooms (Balance.TuneSpike): on floors 5-12 a few normal rooms are harder, an elite body or a crowded
    /// wave, and always pay a drop. Marked on the map once their door is seen.</summary>
    public static class Spikes
    {
        public const int FirstFloor = 5, LastFloor = 12, MostAFloor = 2;
        public const double ChanceFirst = 0.20, ChanceLast = 0.33;
        public const double EliteHp = 3.0, EliteRate = 1.2, Crowd = 1.8;
        public const double SureDrop = 1e9;   // a loot scale that makes the room's drop certain (still one draw)
        const uint StreamSalt = 0x51CE5u;

        public static bool On => Balance.Tuned && Balance.TuneSpike && Balance.TuneLoot;   // the tuned loot rule pays them

        /// <summary>A normal room's chance to be a spike: 0.20 on floor 5 rising evenly to 0.33 on 12, 0 elsewhere.</summary>
        public static double Chance(int floor) => floor < FirstFloor || floor > LastFloor ? 0
            : ChanceFirst + (ChanceLast - ChanceFirst) * (floor - FirstFloor) / (double)(LastFloor - FirstFloor);

        /// <summary>Marks the floor's spike rooms from their own stream: the run stream and every other roll are untouched.</summary>
        public static void Mark(RunState run)
        {
            foreach (var r in run.dungeon.AllRooms) r.Spike = SpikeKind.None;
            double c = Chance(run.floor);
            if (!On || c <= 0) return;
            var rng = new Rng(Rng.FloorSeed(run.rootSeed, run.floor) ^ StreamSalt);
            int n = 0;
            foreach (var r in run.dungeon.AllRooms)
            {
                if (r.Type != RoomKind.Normal || NextToStart(r)) continue;
                bool spike = rng.Run() < c, elite = rng.Run() < 0.5;   // two draws a room, always
                if (spike && n < MostAFloor) { r.Spike = elite ? SpikeKind.Elite : SpikeKind.Crowded; n++; }
            }
        }

        static bool NextToStart(Room r) => System.Math.Abs(r.X - Map.Start) + System.Math.Abs(r.Y - Map.Start) <= 1;

        /// <summary>A body that may be made the elite: not an egg, a Brunch (it spends itself), a Burrower (its homing is
        /// tuned to just outpace a run) or a boss.</summary>
        public static bool CanBeElite(BodyKind k) => k != BodyKind.Egg && k != BodyKind.Brunch && k != BodyKind.Burrower && k != BodyKind.Boss;

        /// <summary>The wave's strongest body (most health) becomes the elite: 3x health, its cadence x1.2 (its pace in Tick).</summary>
        public static Enemy? MakeElite(RunState run)
        {
            Enemy? best = null;
            foreach (var e in run.enemies)
                if (CanBeElite(e.kind) && (best == null || e.maxHp > best.maxHp)) best = e;
            if (best == null) return null;
            best.elite = true;
            best.hp = best.maxHp = best.maxHp * EliteHp;
            best.cdMin /= EliteRate; best.cdVar /= EliteRate; best.shootCd /= EliteRate;
            return best;
        }

        /// <summary>An elite room's crowning: its strongest body, or, for a wave with none that can be (eggs, Brunch,
        /// Burrowers), one of the floor's own kinds (JoinKind) joins it near (x, y), off the body there.</summary>
        public static Enemy Crown(RunState run, double x, double y)
        {
            var e = MakeElite(run);
            if (e != null) return e;
            double dx = Balance.MidX - x, dy = Balance.MidY - y, d = System.Math.Sqrt(dx * dx + dy * dy);
            if (d > 1) { x += dx / d * CrownStep; y += dy / d * CrownStep; }
            run.enemies.Add(Spawn.Body(run, JoinKind(run), x, y));
            return MakeElite(run)!;
        }

        /// <summary>The kind that joins a crownless wave: the floor's likeliest kind that can be an elite (the run meets
        /// only its area's kinds), or a Lunger when the run has no groups.</summary>
        public static BodyKind JoinKind(RunState run)
        {
            if (run.pools == null) return BodyKind.Lunger;
            BodyKind best = BodyKind.Lunger;
            double w = 0;
            foreach (var k in Pools.Kinds)
            {
                double kw = Pools.Weight(run.pools, run.floor, k);
                if (CanBeElite(k) && Pools.Slot(k) && kw > w) { w = kw; best = k; }
            }
            return best;
        }

        public const double CrownStep = 48;   // px a joining elite stands off the slot's own body
    }
}
