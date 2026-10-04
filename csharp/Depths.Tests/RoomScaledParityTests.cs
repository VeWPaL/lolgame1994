using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The room-scaled numbers, at three room sizes, read out of the running JavaScript.
    /// </summary>
    /// <remarks>
    /// WHY THIS TABLE EXISTS AND WHY THE THIRD ROW IS SMALLER THAN THE FIRST.
    /// <para>
    /// Every other parity table in this project pins a constant or a function of a SEED, so a drift in
    /// one is caught by comparing names. These are functions of the ROOM — <c>room.W * 0.25</c> and
    /// friends — and no amount of grepping finds a value that is right in one room and wrong in
    /// another.
    /// </para>
    /// <para>
    /// That is not hypothetical. The fix for a frozen room number REPRODUCED the bug it was fixing:
    /// <c>swerveDeadzone</c> was made a function of the room while the ramp beside it was left as a
    /// value captured once at load. The two sat in the same expression, one following the room and one
    /// frozen at the standard room's answer, and the assertion in place was
    /// <c>SwerveFull() &gt; SwerveDeadzone()</c> — which passes for both, because 805 &gt; 420 is true
    /// and so is 1344 &gt; 420. A check that a quantity is non-empty cannot distinguish a quantity that
    /// follows its input from one that is constant.
    /// </para>
    /// <para>
    /// So the rule this table enforces is: <b>ask for the value the input implies, in an input where
    /// the two answers disagree.</b> 700×450 is the room that existed when the balance file loaded, so
    /// it is the one room a load-time capture gets right by construction. 1680×760 is twice as wide.
    /// 350×225 is HALF as wide, and it is the row that matters most: a constant cannot narrow.
    /// </para>
    /// <para>
    /// Values are read from <c>depths.html?parity</c>, which now emits this section. Regenerate rather
    /// than hand-computing: hand-computing a parity value produced three wrong numbers out of nine once
    /// already, and the test was what failed each time.
    /// </para>
    /// </remarks>
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
            // Stated separately from the table above because the table pins VALUES and this pins the
            // SHAPE. A capture-at-load bug passes every row of the table above except the small one,
            // and it passes this test in the standard room too — so the assertions have to be about
            // the direction of travel, not about the standard room agreeing with itself.
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

            // and the ramp can never saturate inside the room it is scaled to. Under the old fixed
            // 130px ramp, any room wider than 430px had a stretch past which "further is worse" could
            // not be expressed at all — which is the entire defect this area exists to prevent.
            foreach (var room in new[] { std, big, small })
                Assert.That(Balance.SwerveFull(room), Is.LessThan(room.W),
                    $"in a {room.W}-wide room the spread is fully open before the far wall, so there "
                    + "is nowhere left for distance to matter");
        }

        [Test]
        public void TheSpawnOffsetsDoNotFollowTheRoom()
        {
            // The other half of "follows the room": something must NOT move with it. The spawn dials
            // are offsets from a wall, not fractions, and a spawn planner that tracked room size would
            // quietly stop meaning anything in a big room. Asserted so the direction is explicit.
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