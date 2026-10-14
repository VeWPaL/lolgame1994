using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The room-scaled numbers, at three room sizes, read out of the running JavaScript. [h:RoomScaledParityTests-1]</summary>
    [TestFixture]
    public sealed class RoomScaledParityTests
    {
        // (roomWidth, roomHeight, aggroRange, swerveDeadzone, swerveFullBase, swerveFull)
        [TestCase(700, 450, 707, 175, 385, 560)]
        [TestCase(1680, 760, 1567, 420, 924, 1344)]
        [TestCase(350, 225, 354, 88, 193, 281)]
        public void ARoomDerivedNumberTakesTheValueTheRoomImplies(
            int w, int h, int aggro, int deadzone, int rampBase, int full)
        {
            var room = new Balance.Room(50, 130, 50 + w, 130 + h);
            Assert.That(room.W, Is.EqualTo(w));
            Assert.That(room.H, Is.EqualTo(h));

            Assert.That(Balance.AggroRange(room), Is.EqualTo(aggro),
                $"aggro range at {w}x{h}: 0.85 of the diagonal");
            Assert.That(Balance.SwerveDeadzone(room), Is.EqualTo(deadzone),
                $"swerve deadzone at {w}x{h}: a quarter of the width");
            Assert.That(Balance.SwerveFullBase(room), Is.EqualTo(rampBase),
                $"swerve ramp width at {w}x{h}: 0.55 of the width");
            Assert.That(Balance.SwerveFull(room), Is.EqualTo(full),
                $"swerve full-spread distance at {w}x{h}");
        }

        [Test]
        public void TheRampNarrowsInASmallRoomAndWidensInABigOne()
        {
            // Stated separately from the table above because the table pins VALUES and this pins the SHAPE. [h:RoomScaledParityTests-2]
            var std = Balance.Room.Standard;
            var big = new Balance.Room(50, 130, 50 + 1680, 130 + 760);
            var small = new Balance.Room(50, 130, 50 + 350, 130 + 225);

            Assert.Multiple(() =>
            {
                Assert.That(Balance.SwerveFullBase(big), Is.GreaterThan(Balance.SwerveFullBase(std)),
                    "a room twice as wide must have a wider ramp, or the ramp is not a fraction of the room");
                Assert.That(Balance.SwerveFullBase(small), Is.LessThan(Balance.SwerveFullBase(std)),
                    "a room half as wide must have a narrower ramp — this is the row a load-time "
                    + "capture cannot fake, because it is the one that moves the other way");
                Assert.That(Balance.SwerveDeadzone(big), Is.GreaterThan(Balance.SwerveDeadzone(std)));
                Assert.That(Balance.SwerveDeadzone(small), Is.LessThan(Balance.SwerveDeadzone(std)));
                Assert.That(Balance.AggroRange(big), Is.GreaterThan(Balance.AggroRange(std)));
                Assert.That(Balance.AggroRange(small), Is.LessThan(Balance.AggroRange(std)));
            });

            // and the ramp can never saturate inside the room it is scaled to. [h:RoomScaledParityTests-3]
            foreach (var room in new[] { std, big, small })
                Assert.That(Balance.SwerveFull(room), Is.LessThan(room.W),
                    $"in a {room.W}-wide room the spread is fully open before the far wall, so there "
                    + "is nowhere left for distance to matter");
        }

        [Test]
        public void TheSpawnOffsetsDoNotFollowTheRoom()
        {
            // The other half of "follows the room": something must NOT move with it. [h:RoomScaledParityTests-4]
            var small = new Balance.Room(50, 130, 50 + 350, 130 + 225);
            var big = new Balance.Room(50, 130, 50 + 1680, 130 + 760);
            Assert.Multiple(() =>
            {
                Assert.That(Balance.SpawnMargin, Is.EqualTo(76));
                Assert.That(Balance.SpawnMid, Is.EqualTo(104));
                Assert.That(Balance.SpawnSep, Is.EqualTo(158));
                Assert.That(Balance.SpawnDoor, Is.EqualTo(130));
                Assert.That(Balance.SpawnFar, Is.EqualTo(210));
            });
            // they are constants, so asking at two room sizes is the same answer twice - which is the
            // point being made, and is why this test cannot be a table row
            Assert.That(big.W, Is.Not.EqualTo(small.W));
        }
    }
}