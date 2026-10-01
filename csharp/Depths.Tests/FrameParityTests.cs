using System;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The room frame and the camera, against numbers read out of the running JavaScript rather than
    /// transcribed from its source expressions.
    ///
    /// <para>
    /// The one that matters is <see cref="TheOversizedRoomClampMovesTheView"/>. The clamp in
    /// <c>cameraTarget</c> was wrong in the JavaScript for as long as the camera existed - written
    /// <c>Math.min(b.l, ...)</c> with the player's offset inside, which pinned the view to the room's
    /// left edge whenever the player was right of centre. It passed every check that existed because
    /// every room in the game fits on screen, so the branch never ran: the property that made the
    /// camera safe to introduce was the same property that hid the bug in it.
    ///
    /// <para>
    /// So this file deliberately tests a room that does NOT fit. That is the whole reason the camera
    /// is worth a port and the whole reason a parity table of standard-room numbers would be worth
    /// very little.
    /// </para>
    /// </summary>
    [TestFixture]
    public class FrameParityTests
    {
        // read out of the browser: STD_ROOM, ROOM_W/ROOM_H, and the canvas size
        private static readonly RoomBounds Std = RoomBounds.Standard;
        private const double ViewW = 960, ViewH = 600;

        [Test]
        public void TheStandardRoomIsTheOneTheGameUses()
        {
            Assert.That(Std.L, Is.EqualTo(50));
            Assert.That(Std.T, Is.EqualTo(130));
            Assert.That(Std.R, Is.EqualTo(750));
            Assert.That(Std.B, Is.EqualTo(580));
            Assert.That(Std.W, Is.EqualTo(700));
            Assert.That(Std.H, Is.EqualTo(450));
            Assert.That(Std.Cx, Is.EqualTo(400));
            Assert.That(Std.Cy, Is.EqualTo(355));
        }

        [Test]
        public void TheFrameIsWrittenByOneCallAndReadAsProperties()
        {
            var f = RoomFrame.Sync(Std);
            Assert.Multiple(() =>
            {
                Assert.That(f.Left, Is.EqualTo(50));
                Assert.That(f.Right, Is.EqualTo(750));
                Assert.That(f.Top, Is.EqualTo(130));
                Assert.That(f.Bottom, Is.EqualTo(580));
                Assert.That(f.Width, Is.EqualTo(700));
                Assert.That(f.Height, Is.EqualTo(450));
                Assert.That(f.MidX, Is.EqualTo(400));
                Assert.That(f.MidY, Is.EqualTo(355));
            });
        }

        [Test]
        public void EveryRoomStartsAtTheSameOriginSoCoordinatesArePerRoom()
        {
            // 1400x900, the oversized shape the lab uses
            var big = Frame.RoomBoundsAt(1400, 900);
            Assert.Multiple(() =>
            {
                Assert.That(big.L, Is.EqualTo(Std.L), "a room has to start at the standard origin");
                Assert.That(big.T, Is.EqualTo(Std.T));
                Assert.That(big.R, Is.EqualTo(1450));
                Assert.That(big.B, Is.EqualTo(1030));
            });
        }

        [Test]
        public void ARoomThatFitsIsCentredNotPinnedToItsOrigin()
        {
            // measured in the JavaScript: cam = (-80, 55) in a standard room
            var c = Frame.Update(Std, focusX: 400, focusY: 355, ViewW, ViewH);
            Assert.Multiple(() =>
            {
                Assert.That(c.X, Is.EqualTo(-80).Within(1e-9));
                Assert.That(c.Y, Is.EqualTo(55).Within(1e-9));
                Assert.That(c.ViewW, Is.EqualTo(960));
                Assert.That(c.ViewH, Is.EqualTo(600));
            });
        }

        [Test]
        public void CentringIgnoresWhereThePlayerIsInAFittingRoom()
        {
            // the property that made the camera safe to introduce: not one pixel moves, so every
            // screenshot and every existing check is unchanged by its presence
            var a = Frame.Update(Std, focusX: 60, focusY: 140, ViewW, ViewH);
            var b = Frame.Update(Std, focusX: 740, focusY: 570, ViewW, ViewH);
            Assert.Multiple(() =>
            {
                Assert.That(a.X, Is.EqualTo(b.X));
                Assert.That(a.Y, Is.EqualTo(b.Y));
            });
        }

        [Test]
        public void TheOversizedRoomClampMovesTheView()
        {
            // room l50 r1450, view 960 wide, so the left edge may range over [50, 490].
            // Measured from the JavaScript at five positions across the room.
            var big = Frame.RoomBoundsAt(1400, 900);
            const double midY = 130 + 450.0;   // half way down a 900-tall room

            var expected = new[] { 50.0, 50.0, 270.0, 490.0, 490.0 };
            for (int i = 0; i < 5; i++)
            {
                double fx = 50 + i / 4.0 * 1400;
                var (x, _) = Frame.CameraTarget(big, fx, midY, ViewW, ViewH);
                Assert.That(x, Is.EqualTo(expected[i]).Within(1e-9),
                    $"at {i * 25}% across a 1400-wide room the camera landed at {x}, wanted {expected[i]}");
            }
        }

        [Test]
        public void TheClampOrderIsMaxThenMinAndNotTheOtherWayRound()
        {
            // The exact shape of the bug: min-then-max with the bounds transposed reads plausibly
            // and pins the camera to the room's left edge, so the view never moves at all. This is
            // here so that anyone "simplifying" the clamp trips a test rather than a playtest.
            var big = Frame.RoomBoundsAt(1400, 900);

            // right of centre is where the old version broke, and where most play happens
            var (x, _) = Frame.CameraTarget(big, focusX: 1200, focusY: 0, viewW: ViewW, viewH: ViewH);
            Assert.That(x, Is.EqualTo(490).Within(1e-9),
                "the camera is pinned to the room's left edge whenever the player is right of centre, " +
                "so the view never moves - the branch that was wrong and that no check exercised");

            // and it must never show anything outside the room
            var (xMin, _) = Frame.CameraTarget(big, focusX: -9999, focusY: 0, viewW: ViewW, viewH: ViewH);
            Assert.That(xMin, Is.GreaterThanOrEqualTo(big.L - 1e-9), "the camera showed space left of the room");
            var (xMax, _) = Frame.CameraTarget(big, focusX: 9999, focusY: 0, viewW: ViewW, viewH: ViewH);
            Assert.That(xMax, Is.LessThanOrEqualTo(big.R - ViewW + 1e-9), "the camera showed space right of the room");
        }

        [Test]
        public void ScreenAndWorldAreInversesAcrossTheTransform()
        {
            // the bug this whole file's origin is about: the cursor is SCREEN space and everything
            // else is world, so mixing them is wrong by exactly the camera offset - measured at up to
            // 21.28 degrees of aim error, changing sign across the frame.
            var big = Frame.RoomBoundsAt(1400, 900);
            var c = Frame.Update(big, focusX: 1100, focusY: 500, ViewW, ViewH);

            foreach (var (sx, sy) in new[] { (0.0, 0.0), (480.0, 300.0), (960.0, 600.0), (137.0, 559.0) })
            {
                var (wx, wy) = c.ScreenToWorld(sx, sy);
                var (bx, by) = c.WorldToScreen(wx, wy);
                Assert.Multiple(() =>
                {
                    Assert.That(bx, Is.EqualTo(sx).Within(1e-9));
                    Assert.That(by, Is.EqualTo(sy).Within(1e-9));
                });
            }
        }

        [Test]
        public void TheTransformIsTheIdentityForARoomThatFits()
        {
            // which is the claim the whole camera rests on: in a standard room the world is drawn
            // through a pure offset and nothing in the game is aware a camera exists
            var c = Frame.Update(Std, focusX: 400, focusY: 355, ViewW, ViewH);
            var (wx, wy) = c.ScreenToWorld(480.0, 300.0);
            Assert.Multiple(() =>
            {
                Assert.That(wx, Is.EqualTo(400).Within(1e-9));
                Assert.That(wy, Is.EqualTo(355).Within(1e-9));
            });
        }
    }
}
