using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// A fresh run walking into its first normal room, against the running game: recorded by
    /// tools/room-entry-parity.js on 2026-10-06. startGame (the floor generated from the run stream),
    /// passDoor through the first start-room door that leads to a normal room, update() until the
    /// transition has entered it. Compared: both streams' totals since the reseed, where the player
    /// stands, the ready freeze, and every body the wave spawned - kind, position, health, idle and
    /// notice timers, pack id and slot, flank and first cooldown.
    /// </summary>
    [TestFixture]
    public sealed class RoomEntryParityTests
    {
        static void Check(uint seed, Dir door, int jitter, int runDraws, double px, double py, int readyT,
            (BodyKind kind, double x, double y, double hp, int idle, int notice, int packId, int slot, double flank, double shootCd)[] bodies)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            Rooms.PassDoor(run, run.CurrentRoom!, door);
            for (int t = 0; t < 200 && run.trans != null; t++) TickOrder.Update(run, Input.None);
            Assert.That(run.trans, Is.Null, "the transition never finished");
            Assert.That(run.rng.JitterCalls, Is.EqualTo(jitter), "jitter draws since the reseed");
            Assert.That(run.rng.RunCalls, Is.EqualTo(runDraws), "run draws since the reseed");
            Assert.That(run.player.x, Is.EqualTo(px).Within(1e-9));
            Assert.That(run.player.y, Is.EqualTo(py).Within(1e-9));
            Assert.That(run.readyT, Is.EqualTo(readyT));
            Assert.That(run.enemies.Count, Is.EqualTo(bodies.Length), "bodies spawned");
            for (int i = 0; i < bodies.Length; i++)
            {
                var e = run.enemies[i]; var w = bodies[i]; string at = " (body " + i + ")";
                Assert.That(e.kind, Is.EqualTo(w.kind), "kind" + at);
                Assert.That(e.x, Is.EqualTo(w.x).Within(1e-6), "x" + at);
                Assert.That(e.y, Is.EqualTo(w.y).Within(1e-6), "y" + at);
                Assert.That(e.hp, Is.EqualTo(w.hp).Within(1e-6), "hp" + at);
                Assert.That(e.idleTimer, Is.EqualTo(w.idle), "idleTimer" + at);
                Assert.That(e.noticeTimer, Is.EqualTo(w.notice), "noticeTimer" + at);
                Assert.That(e.packId ?? 0, Is.EqualTo(w.packId), "packId" + at);
                if (w.slot >= 0) Assert.That(e.packSlot, Is.EqualTo(w.slot), "packSlot" + at);
                Assert.That(e.flank, Is.EqualTo(w.flank).Within(1e-9), "flank" + at);
                Assert.That(e.shootCd, Is.EqualTo(w.shootCd).Within(1e-6), "shootCd" + at);
            }
        }

        [Test]
        public void Seed1() => Check(1u, Dir.N, 10, 692, 400.0, 546.0, 158, new[]
            {
                (BodyKind.Gunner, 661.7805443918332, 210.37640998046845, 10.8, 39, 17, 0, -1, 0.0, 177.7787879994139),
                (BodyKind.Lunger, 128.03157918713987, 260.5253753378056, 20.25, 112, 43, 0, -1, 0.0, 0.0),
                (BodyKind.Lunger, 391.5210816729814, 207.4123386326246, 20.25, 143, 47, 0, -1, 2.399963229728653, 0.0),
                (BodyKind.Shooter, 652.0740178665146, 495.8680812618695, 7.56, 142, 22, 0, -1, 0.0, 91.36710429564118),
            });

        [Test]
        public void Seed42() => Check(42u, Dir.N, 11, 672, 400.0, 546.0, 158, new[]
            {
                (BodyKind.Shooter, 670.1384136369452, 216.36389287235215, 7.56, 101, 55, 0, -1, 0.0, 111.32336263731122),
                (BodyKind.Brunch, 149.9436109215021, 231.47971806349233, 2.7, 128, 39, 1, 0, 0.0, 0.0),
                (BodyKind.Brunch, 136.9447690913741, 256.4797180366652, 2.7, 202, 31, 1, 1, 2.399963229728653, 0.0),
                (BodyKind.Brunch, 123.94361097730258, 231.48092256015792, 2.7, 140, 39, 1, 2, 4.799926459457306, 0.0),
                (BodyKind.Brunch, 136.94013641189605, 206.47971830493668, 2.7, 113, 32, 1, 3, 0.9167043820063725, 0.0),
            });

        [Test]
        public void Seed4242() => Check(4242u, Dir.N, 13, 691, 400.0, 546.0, 158, new[]
            {
                (BodyKind.Gunner, 669.7437345245853, 217.10990960197523, 10.8, 165, 47, 0, -1, 0.0, 145.0991799449548),
                (BodyKind.Shooter, 133.96447905898094, 257.98759953165427, 7.56, 169, 33, 0, -1, 0.0, 124.14192659594119),
                (BodyKind.Lunger, 407.15216156467795, 220.33640223369002, 20.25, 176, 54, 0, -1, 0.0, 0.0),
                (BodyKind.Lunger, 669.5764698712155, 474.49813733110204, 20.25, 78, 25, 0, -1, 2.399963229728653, 0.0),
                (BodyKind.Shooter, 128.19675772450864, 486.64563838019967, 7.56, 96, 18, 0, -1, 0.0, 121.63352361693978),
            });

        [Test]
        public void Seed777() => Check(777u, Dir.N, 4, 665, 400.0, 546.0, 158, new[]
            {
                (BodyKind.Lunger, 170.1066415188834, 206.98467796994373, 20.25, 7, 44, 0, -1, 0.0, 0.0),
                (BodyKind.Lunger, 669.1258157370612, 278.58031697943807, 20.25, 21, 21, 0, -1, 2.399963229728653, 0.0),
            });

        [Test]
        public void Seed31337() => Check(31337u, Dir.S, 12, 683, 400.0, 164.0, 158, new[]
            {
                (BodyKind.Gunner, 649.5397175140679, 498.21517939679325, 10.8, 26, 2, 0, -1, 0.0, 125.63494865782559),
                (BodyKind.Lunger, 139.5399083448574, 486.0008363346569, 20.25, 9, 25, 0, -1, 0.0, 0.0),
                (BodyKind.Lunger, 407.94375429395586, 495.5701880636625, 20.25, 25, 8, 0, -1, 2.399963229728653, 0.0),
                (BodyKind.Lunger, 131.3639505840838, 223.6950360792689, 20.25, 143, 39, 0, -1, 4.799926459457306, 0.0),
                (BodyKind.Shooter, 664.2905517639592, 214.80691892514005, 7.56, 143, 36, 0, -1, 0.0, 92.32796336710453),
            });

        [Test]
        public void Seed99999() => Check(99999u, Dir.E, 15, 694, 84.0, 355.0, 158, new[]
            {
                (BodyKind.Gunner, 673.6254705991596, 223.22166478261352, 10.8, 39, 19, 0, -1, 0.0, 128.84717043302953),
                (BodyKind.Brunch, 667.8617804199457, 503.02477227523923, 2.7, 17, 36, 1, 0, 0.0, 0.0),
                (BodyKind.Brunch, 662.5880864620169, 526.8008988509507, 2.7, 124, 22, 1, 1, 2.399963229728653, 0.0),
                (BodyKind.Brunch, 644.3451259102648, 510.66676010066834, 2.7, 128, 39, 1, 2, 4.799926459457306, 0.0),
                (BodyKind.Brunch, 634.6347218731787, 488.3323898086119, 2.7, 125, 1, 1, 3, 0.9167043820063725, 0.0),
                (BodyKind.Brunch, 658.8771684316371, 490.6604421633583, 2.7, 156, 18, 1, 4, 3.3166676117350256, 0.0),
                (BodyKind.Lunger, 128.2066044472158, 502.62670042784885, 20.25, 42, 12, 0, -1, 5.716630841463679, 0.0),
            });
    }
}
