using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// An item room against the running game: recorded by tools/item-room-parity.js on 2026-10-06.
    /// startGame, optionally give items (luck, and what is excluded from the offer), enter the floor's
    /// item room from the west. Compared: both streams' totals, the stats read, and every pedestal -
    /// the two weapons from the shuffle and the two items from the luck-weighted pool.
    /// </summary>
    [TestFixture]
    public sealed class ItemRoomParityTests
    {
        static void Check(uint seed, string[] give, int jitter, int runDraws, double luck, double strength,
                          (string kind, double x, double y, int w, string id)[] pickups)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            foreach (var id in give) Items.Give(run, id);
            Room? ir = null;
            foreach (var r in run.dungeon.AllRooms) if (r.Type == RoomKind.Item) { ir = r; break; }
            Rooms.EnterRoom(run, ir!.X, ir.Y, Dir.W);
            Assert.That(run.rng.JitterCalls, Is.EqualTo(jitter), "jitter draws");
            Assert.That(run.rng.RunCalls, Is.EqualTo(runDraws), "run draws");
            Assert.That(run.stats.Value("luck"), Is.EqualTo(luck));
            Assert.That(run.stats.Value("strength"), Is.EqualTo(strength));
            Assert.That(run.pickups.Count, Is.EqualTo(pickups.Length), "pedestals");
            for (int i = 0; i < pickups.Length; i++)
            {
                var q = run.pickups[i]; var w = pickups[i];
                Assert.That(q.kind, Is.EqualTo(w.kind), "kind " + i);
                Assert.That(q.x, Is.EqualTo(w.x).Within(1e-9), "x " + i);
                Assert.That(q.y, Is.EqualTo(w.y).Within(1e-9), "y " + i);
                if (w.w >= 0) Assert.That(q.w, Is.EqualTo(w.w), "weapon " + i);
                if (w.id != "") Assert.That(q.id, Is.EqualTo(w.id), "item " + i);
            }
        }

        [Test]
        public void Seed1() => Check(1u, new string[] {  }, 0, 48, 0.0, 3.0,
            new[] { ("weapon", 145.0, 355.0, 1, ""), ("weapon", 655.0, 355.0, 2, ""), ("item", 485.0, 355.0, -1, "bone_whistle"), ("item", 315.0, 355.0, -1, "lucky_coin") });

        [Test]
        public void Seed42() => Check(42u, new string[] {  }, 0, 30, 0.0, 3.0,
            new[] { ("weapon", 145.0, 355.0, 2, ""), ("weapon", 655.0, 355.0, 3, ""), ("item", 485.0, 355.0, -1, "iron_ribs"), ("item", 315.0, 355.0, -1, "heavy_hands") });

        [Test]
        public void Seed4242() => Check(4242u, new string[] {  }, 0, 45, 0.0, 3.0,
            new[] { ("weapon", 145.0, 355.0, 2, ""), ("weapon", 655.0, 355.0, 1, ""), ("item", 485.0, 355.0, -1, "iron_ribs"), ("item", 315.0, 355.0, -1, "swift_boots") });

        [Test]
        public void Seed777() => Check(777u, new string[] {  }, 0, 26, 0.0, 3.0,
            new[] { ("weapon", 145.0, 355.0, 2, ""), ("weapon", 655.0, 355.0, 3, ""), ("item", 485.0, 355.0, -1, "hunters_mark"), ("item", 315.0, 355.0, -1, "weighted_grip") });

        [Test]
        public void Seed31337_Holding() => Check(31337u, new string[] { "lucky_coin", "glass_wands" }, 0, 38, 0.0, 6.0,
            new[] { ("weapon", 145.0, 355.0, 1, ""), ("weapon", 655.0, 355.0, 2, ""), ("item", 485.0, 355.0, -1, "weighted_grip"), ("item", 315.0, 355.0, -1, "hunters_mark") });

        [Test]
        public void Seed99999_Holding() => Check(99999u, new string[] { "brass_compass", "glass_wands" }, 0, 50, 0.0, 6.0,
            new[] { ("weapon", 145.0, 355.0, 3, ""), ("weapon", 655.0, 355.0, 1, ""), ("item", 485.0, 355.0, -1, "tin_cup"), ("item", 315.0, 355.0, -1, "iron_ribs") });
    }
}
