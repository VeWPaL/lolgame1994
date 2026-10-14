using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The generator, checked against floors read out of the running JavaScript. [h:GeneratorParityTests-1]</summary>
    [TestFixture]
    public sealed class GeneratorParityTests
    {
        // READ OUT OF THE RUNNING JAVASCRIPT, via Rnd.set(seed); generateDungeon(); then rooms sorted by x then y... [h:GeneratorParityTests-2]
        [TestCase(0u, 16, 19,
            "0,4:b:E:|1,1:i:E:|1,4:n:EW:|2,1:n:EW:|2,4:n:EW:|3,1:n:SW:|3,2:n:ENS:|3,3:s:NS:|3,4:n:ENW:|4,2:n:EW:|4,4:n:SW:|4,5:n:NS:|4,6:n:N:GSE|5,2:n:SW:|5,3:n:N:K|5,6:s::")]
        [TestCase(1u, 18, 43,
            "0,4:i:E:|1,1:b:S:|1,2:n:EN:|1,4:n:EW:|1,6:n:E:G|2,2:n:EW:|2,4:n:EW:|2,6:n:EW:|3,2:n:ESW:|3,3:s:NS:|3,4:n:NSW:|3,5:n:NS:|3,6:n:NW:|4,1:s::|4,2:n:EW:SN|5,2:n:SW:|5,3:n:NS:|5,4:n:N:K")]
        [TestCase(2u, 18, 21,
            "0,1:s::|0,2:n:ES:SN|0,3:i:N:|1,0:n:E:G|1,2:n:EW:|1,6:b:E:|2,0:n:SW:|2,1:n:NS:|2,2:n:NSW:|2,3:n:EN:|2,6:n:EW:|3,3:s:SW:|3,4:n:NS:|3,5:n:ENS:|3,6:n:NW:|4,5:n:EW:|5,4:n:S:K|5,5:n:NW:")]
        [TestCase(3u, 18, 23,
            "0,1:i:E:|1,0:s::|1,1:n:EW:SN|2,1:n:EW:|3,1:n:ESW:|3,2:n:NS:|3,3:s:EN:|4,1:n:EW:|4,3:n:SW:|4,4:n:ENS:|4,5:n:NS:|4,6:n:EN:|5,0:n:S:K|5,1:n:NW:|5,4:n:EW:|5,6:b:W:|6,4:n:SW:|6,5:n:N:G")]
        [TestCase(42u, 18, 25,
            "0,5:b:E:|1,3:s::|1,4:n:ES:SN|1,5:n:NSW:|1,6:n:EN:|2,4:n:EW:|2,6:n:EW:|3,2:n:ES:|3,3:s:NS:|3,4:n:NW:|3,6:n:W:K|4,2:n:EW:|5,0:n:E:G|5,2:n:EW:|6,0:n:SW:|6,1:n:NS:|6,2:n:NSW:|6,3:i:N:")]
        [TestCase(12345u, 18, 19,
            "0,1:n:S:K|0,2:n:EN:|1,0:i:E:|1,2:n:SW:|1,3:n:ENS:|1,4:n:NS:|1,5:n:NS:|1,6:b:N:|2,0:n:EW:|2,3:n:EW:|3,0:n:SW:|3,1:n:ENS:|3,2:n:NS:|3,3:s:NW:|4,1:n:EW:|5,1:n:EW:|6,0:s::|6,1:n:W:GSN")]
        [TestCase(12346u, 18, 21,
            "0,1:n:S:G|0,2:n:EN:|1,2:n:SW:|1,3:n:EN:|1,5:s::|2,1:n:E:K|2,3:n:ESW:|2,4:n:NS:|2,5:n:NS:SW|2,6:i:N:|3,1:n:EW:|3,3:s:EW:|4,1:n:SW:|4,2:n:NS:|4,3:n:ENW:|5,3:n:EW:|6,3:n:SW:|6,4:b:N:")]
        [TestCase(99999u, 18, 45,
            "1,1:n:ES:|1,2:n:NS:|1,3:n:ENS:|1,4:n:NS:|1,5:i:N:|2,1:n:EW:|2,3:n:EW:|3,1:n:W:G|3,3:s:EW:|3,5:b:E:|4,3:n:SW:|4,4:n:ENS:|4,5:n:NW:SS|4,6:s::|5,4:n:EW:|6,2:n:S:K|6,3:n:NS:|6,4:n:NW:")]
        [TestCase(2654435761u, 18, 21,
            "1,0:b:E:|1,6:i:E:|2,0:n:SW:|2,1:n:NS:|2,2:n:EN:|2,6:n:EW:|3,2:n:ESW:|3,3:s:NS:|3,4:n:ENS:|3,5:n:NS:|3,6:n:NW:|4,1:n:ES:|4,2:n:NW:|4,4:n:EW:|5,1:n:W:GSE|5,3:n:S:K|5,4:n:NW:|6,1:s::")]
        [TestCase(4294967295u, 18, 23,
            "0,1:n:ES:|0,2:n:NS:|0,3:n:ENS:|0,4:n:NS:|0,5:b:N:|1,1:n:W:K|1,3:n:EW:|2,3:n:EW:|3,3:s:SW:|3,4:n:EN:|4,2:s::|4,4:n:EW:|5,2:n:ES:SW|5,3:n:NS:|5,4:n:ENW:|6,2:n:W:G|6,4:n:SW:|6,5:i:N:")]
        [TestCase(777u, 18, 21,
            "0,5:s::|1,0:b:E:|1,4:n:ES:|1,5:n:NS:SW|1,6:i:N:|2,0:n:SW:|2,1:n:NS:|2,2:n:EN:|2,4:n:EW:|3,2:n:ESW:|3,3:s:NS:|3,4:n:ENW:|4,0:n:S:K|4,1:n:NS:|4,2:n:NW:|4,4:n:SW:|4,5:n:NS:|4,6:n:N:G")]
        [TestCase(31337u, 18, 33,
            "1,3:n:ES:|1,4:n:NS:|1,5:i:N:|2,1:n:ES:|2,2:n:NS:|2,3:n:ENW:|2,6:b:E:|3,1:n:EW:|3,3:s:SW:|3,4:n:ENS:|3,5:n:NS:|3,6:n:NW:|4,0:s::|4,1:n:W:KSN|4,4:n:EW:|5,4:n:SW:|5,5:n:NS:|5,6:n:N:G")]
        [TestCase(8675309u, 16, 36,
            "0,2:n:E:K|1,2:n:EW:|1,5:b:E:|2,2:n:SW:|2,3:n:ENS:|2,4:n:NS:|2,5:n:NW:SS|2,6:s::|3,3:s:EW:|4,2:n:ES:|4,3:n:NSW:|4,4:n:NS:|4,5:n:NS:|4,6:i:N:|5,2:n:EW:|6,2:n:W:G")]
        public void AFloorMatchesTheJavaScriptExactly(uint seed, int rooms, int draws, string signature)
        {
            var rng = new Rng(seed);
            var d = Dungeon.FromSeed(rng);

            Assert.That(d.AllRooms.Count, Is.EqualTo(rooms),
                $"seed {seed} built {d.AllRooms.Count} rooms, the JavaScript builds {rooms}");
            Assert.That(rng.RunCalls, Is.EqualTo(draws),
                $"seed {seed} drew {rng.RunCalls} times from the run stream, the JavaScript draws {draws}. " +
                "A count difference means the generator consumed the stream differently - look there before " +
                "looking at the geometry.");
            Assert.That(d.Signature(), Is.EqualTo(signature),
                $"seed {seed} built a different floor.\n  port: {d.Signature()}\n  java: {signature}");
        }

        /// <summary>A floor is a TREE, and a tree is the property that makes every progression rule hold. [h:GeneratorParityTests-3]</summary>
        [Test]
        public void EveryFloorIsATree()
        {
            foreach (var seed in Seeds(400))
            {
                var d = Dungeon.FromSeed(new Rng(seed));

                // THE SECRET IS EXCLUDED, and that is not a convenience. [h:GeneratorParityTests-4]
                var walkable = d.AllRooms.Where(r => r.Type != RoomKind.Secret).ToList();
                var secret = d.AllRooms.Single(r => r.Type == RoomKind.Secret);
                Assert.That(secret.Doors.Count, Is.EqualTo(0),
                    $"seed {seed}: the secret room has {secret.Doors.Count} doors, so it is reachable " +
                    "normally and the fake wall is not the only way in");

                var doors = walkable.Sum(r => r.Doors.Count);
                Assert.That(doors, Is.EqualTo(2 * (walkable.Count - 1)),
                    $"seed {seed}: {walkable.Count} walkable rooms have {doors} doors between them, so the " +
                    "map has a loop in it - one route between two rooms is what every progression rule assumes");
            }
        }

        /// <summary>Every walkable room is reachable from the start, on every seed. [h:GeneratorParityTests-5]</summary>
        [Test]
        public void EveryRoomIsReachableFromTheStart()
        {
            foreach (var seed in Seeds(400))
            {
                var d = Dungeon.FromSeed(new Rng(seed));
                var walkable = d.AllRooms.Where(r => r.Type != RoomKind.Secret).ToList();
                var start = walkable.Single(r => r.Type == RoomKind.Start);
                var seen = new System.Collections.Generic.HashSet<Room> { start };
                var queue = new System.Collections.Generic.Queue<Room>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var r = queue.Dequeue();
                    foreach (var door in r.Doors)
                    {
                        var next = d.Neighbour(r, door);
                        if (next != null && seen.Add(next)) queue.Enqueue(next);
                    }
                }
                Assert.That(seen.Count, Is.EqualTo(walkable.Count),
                    $"seed {seed}: only {seen.Count} of {walkable.Count} walkable rooms are reachable " +
                    "from the start");
            }
        }

        /// <summary>The shape the design depends on, asserted as a shape on every seed rather than as a property of the thirteen... [h:GeneratorParityTests-6]</summary>
        [Test]
        public void EveryFloorHasOneOfEachThingThatMatters()
        {
            foreach (var seed in Seeds(400))
            {
                var d = Dungeon.FromSeed(new Rng(seed));
                Assert.That(d.AllRooms.Count(r => r.Type == RoomKind.Boss), Is.EqualTo(1),
                    $"seed {seed} does not have exactly one boss room");
                Assert.That(d.AllRooms.Count(r => r.Type == RoomKind.Item), Is.EqualTo(1),
                    $"seed {seed} does not have exactly one upgrade room");
                Assert.That(d.AllRooms.Count(r => r.KeyReward), Is.EqualTo(1),
                    $"seed {seed} does not have exactly one silver key");
                Assert.That(d.AllRooms.Count(r => r.GoldReward), Is.EqualTo(1),
                    $"seed {seed} does not have exactly one gold key");
                Assert.That(d.AllRooms.Count(r => r.Type == RoomKind.Secret), Is.EqualTo(1),
                    $"seed {seed} does not have exactly one secret");
                Assert.That(d.AllRooms.Count(r => r.Type == RoomKind.Start), Is.EqualTo(1),
                    $"seed {seed} does not have exactly one start");
                // Both trunk tips are where the walk stopped, so both are dead ends by construction.
                Assert.That(d.AllRooms.Single(r => r.Type == RoomKind.Boss).IsEndpoint, Is.True,
                    $"seed {seed}: the boss room is not an endpoint, so something hangs off the end of the " +
                    "map - and a branch there puts the key BEHIND the door it is supposed to open");
                Assert.That(d.AllRooms.Single(r => r.Type == RoomKind.Item).IsEndpoint, Is.True,
                    $"seed {seed}: the upgrade room is not an endpoint");
                // The keys go in branch ENDS, never on the trunk. A key on the trunk is a toll.
                Assert.That(d.AllRooms.Single(r => r.KeyReward).IsEndpoint, Is.True,
                    $"seed {seed}: the silver key is not at a dead end, so it is on the spine and the " +
                    "detour is not a decision");
                Assert.That(d.AllRooms.Single(r => r.GoldReward).IsEndpoint, Is.True,
                    $"seed {seed}: the gold key is not at a dead end, so it is on the spine");
                // And the two keys are never in the same room.
                var silver = d.AllRooms.Single(r => r.KeyReward);
                var gold = d.AllRooms.Single(r => r.GoldReward);
                Assert.That(silver, Is.Not.SameAs(gold), $"seed {seed} put both keys in one room");
            }
        }

        /// <summary>The gold key must not be behind the boss. [h:GeneratorParityTests-7]</summary>
        [Test]
        public void TheGoldKeyIsNotBehindTheBossDoor()
        {
            foreach (var seed in Seeds(400))
            {
                var d = Dungeon.FromSeed(new Rng(seed));
                var boss = d.AllRooms.Single(r => r.Type == RoomKind.Boss);
                var gold = d.AllRooms.Single(r => r.GoldReward);

                // Walk everywhere REACHABLE from the start without entering the boss. If the gold key is
                // in there, the floor is completable.
                var start = d.AllRooms.Single(r => r.Type == RoomKind.Start);
                var seen = new System.Collections.Generic.HashSet<Room> { start };
                var queue = new System.Collections.Generic.Queue<Room>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var r = queue.Dequeue();
                    foreach (var door in r.Doors)
                    {
                        var next = d.Neighbour(r, door);
                        if (next == null || ReferenceEquals(next, boss)) continue;
                        if (seen.Add(next)) queue.Enqueue(next);
                    }
                }
                Assert.That(seen.Contains(gold), Is.True,
                    $"seed {seed}: the gold key can only be reached THROUGH the boss room, so the boss " +
                    "door is sealed on a key that is behind it and the floor cannot be finished");
            }
        }

        /// <summary>One seed, one floor, every time - which is the whole reason the cursors are instance state. [h:GeneratorParityTests-8]</summary>
        [Test]
        public void OneSeedOneFloor()
        {
            var a = Dungeon.FromSeed(new Rng(12345)).Signature();
            var b = Dungeon.FromSeed(new Rng(12345)).Signature();
            var c = Dungeon.FromSeed(new Rng(12345)).Signature();
            Assert.That(b, Is.EqualTo(a), "the same seed built two different floors");
            Assert.That(c, Is.EqualTo(a), "the third floor from one seed differed from the first two");
            Assert.That(Dungeon.FromSeed(new Rng(12346)).Signature(), Is.Not.EqualTo(a),
                "two neighbouring seeds built the identical floor, so the stream is not being derived apart");
        }

        /// <summary>Neighbouring seeds must give different floors, and over a wide spread. [h:GeneratorParityTests-9]</summary>
        [Test]
        public void NeighbouringSeedsDoNotCollapse()
        {
            var distinct = new System.Collections.Generic.HashSet<string>();
            for (var i = 0u; i < 3000; i++) distinct.Add(Dungeon.FromSeed(new Rng(i)).Signature());
            Assert.That(distinct.Count, Is.GreaterThan(2900),
                $"3000 seeds produced only {distinct.Count} distinct floors, so the generator is " +
                "collapsing runs together");
        }

        /// <summary>Every seed produces a floor, on a wide spread. [h:GeneratorParityTests-10]</summary>
        [Test]
        public void EverySeedProducesAFloor()
        {
            foreach (var seed in Seeds(5000))
            {
                var rng = new Rng(seed);
                Assert.DoesNotThrow(() => Dungeon.FromSeed(rng),
                    $"seed {seed} exhausted all 800 build attempts");
            }
        }

        /// <summary>Rooms hold their own bounds rather than reading a global, so a room knows its size.</summary>
        [Test]
        public void ARoomKnowsItsOwnBounds()
        {
            var d = Dungeon.FromSeed(new Rng(7));
            foreach (var r in d.AllRooms)
            {
                Assert.That(r.Bounds.W, Is.EqualTo(700), $"room ({r.X},{r.Y}) is {r.Bounds.W} wide");
                Assert.That(r.Bounds.H, Is.EqualTo(450), $"room ({r.X},{r.Y}) is {r.Bounds.H} tall");
                Assert.That(r.Bounds.Cx, Is.EqualTo(400), $"room ({r.X},{r.Y}) is centred at {r.Bounds.Cx}");
            }
        }

        private static IEnumerable<uint> Seeds(int n)
        {
            for (uint i = 0; i < n; i++)
            {
                // A stride rather than i itself, so the sample is not a run of adjacent seeds. [h:GeneratorParityTests-11]
                yield return i * 0x9E3779B1u;   // uint arithmetic already wraps; no >>> needed in C#
            }
        }
    }
}