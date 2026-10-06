using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The room loop as a player meets it, through Update alone: walk out of the start room, arrive in
    /// a live room whose doors the fight has shut, clear it, and find the doors open again. Not a
    /// parity table (RoomEntryParityTests pins the numbers); this pins that the pieces are connected.
    /// </summary>
    [TestFixture]
    public sealed class RoomLoopTests
    {
        static Input Toward(RunState run, double x, double y) =>
            new Input(System.Math.Sign(System.Math.Round(x - run.player.x)), System.Math.Sign(System.Math.Round(y - run.player.y)));

        [TestCase(1u)]
        [TestCase(4242u)]
        [TestCase(31337u)]
        public void WalkIntoALiveRoomAndClearIt(uint seed)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            var start = run.CurrentRoom!;
            var d = start.Doors.First(k => run.dungeon.Neighbour(start, k)!.Type == RoomKind.Normal);
            var (dx, dy) = Rooms.DoorPoint(d);
            // aim 40px past the door, as a player walks through it
            double tx = dx + System.Math.Sign(dx - Balance.MidX) * 40, ty = dy + System.Math.Sign(dy - Balance.MidY) * 40;
            for (int t = 0; t < 2000 && run.CurrentRoom == start; t++) TickOrder.Update(run, Toward(run, tx, ty));
            var room = run.CurrentRoom!;
            Assert.That(room, Is.Not.SameAs(start), "the player never left the start room");
            Assert.That(room.Type, Is.EqualTo(RoomKind.Normal));
            Assert.That(run.enemies, Is.Not.Empty, "a normal room arrived empty");
            Assert.That(room.Doors.All(k => !Rooms.DoorPassable(run, room, k)), "a door is open while the fight is on");

            // clear it (by hand: this test is about the doors, not the fight), then the doors open
            foreach (var e in run.enemies.ToList()) e.hp = 0;
            run.enemies.Clear();
            run.readyT = 0;
            TickOrder.Update(run, Input.None);
            Assert.That(room.Cleared, "the room did not register as cleared");
            Assert.That(room.Doors.Where(k => !Rooms.DoorSealed(run, room, k)).All(k => Rooms.DoorPassable(run, room, k)),
                "a cleared room kept an unsealed door shut");

            // and the start room still holds what it held when the player left (nothing), not this room's
            Assert.That(start.Enemies, Is.Empty);
        }
    }
}
