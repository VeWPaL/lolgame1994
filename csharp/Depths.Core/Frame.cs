using System;

namespace Depths
{
    /// <summary>One room's rectangle, in world space. [h:Frame-1]</summary>
    public readonly struct RoomBounds
    {
        public readonly double L, T, R, B;

        public RoomBounds(double l, double t, double r, double b)
        {
            L = l; T = t; R = r; B = b;
        }

        /// <summary>The rectangle the standard room occupies, which is also the fallback.</summary>
        public static RoomBounds Standard => new RoomBounds(50, 130, 750, 580);

        public double W => R - L;
        public double H => B - T;
        public double Cx => (L + R) / 2;
        public double Cy => (T + B) / 2;

        public bool Fits(double viewW, double viewH) => W <= viewW && H <= viewH;

        public override string ToString() => $"({L},{T})-({R},{B})";
    }

    /// <summary>The wall shorthand, as a mutable holder rather than four loose fields. [h:Frame-2]</summary>
    public struct RoomFrame
    {
        public RoomBounds Bounds { get; private set; }

        public double Left => Bounds.L;
        public double Right => Bounds.R;
        public double Top => Bounds.T;
        public double Bottom => Bounds.B;
        public double Width => Bounds.W;
        public double Height => Bounds.H;
        public double MidX => Bounds.Cx;
        public double MidY => Bounds.Cy;

        /// <summary>The ONE writer, and the function to look at when a wall is in the wrong place. [h:Frame-3]</summary>
        public static RoomFrame Sync(RoomBounds bounds)
        {
            var f = new RoomFrame();
            f.Bounds = bounds;
            return f;
        }

        public static RoomFrame Standard => Sync(RoomBounds.Standard);
    }

    /// <summary>The camera, as a render-time transform rather than a change of coordinates.</summary>
    public struct Camera
    {
        public double X, Y;
        public double ViewW, ViewH;

        /// <summary>The world point under this screen point.</summary>
        public (double X, double Y) ScreenToWorld(double sx, double sy) => (sx + X, sy + Y);

        /// <summary>The screen point this world point lands at.</summary>
        public (double X, double Y) WorldToScreen(double wx, double wy) => (wx - X, wy - Y);
    }

    /// <summary>The camera arithmetic, with no game state in it: bounds in, target in, transform out. [h:Frame-4]</summary>
    public static class Frame
    {
        /// <summary>Where the view's top-left corner goes, given a room and a focus point.</summary>
        public static (double X, double Y) CameraTarget(
            RoomBounds b, double focusX, double focusY, double viewW, double viewH)
        {
            // A room that FITS is centred, not pinned to its origin: the 700x450 room in a 960x600
            // viewport used to sit with 210px of dead space to the right and 80 to the left.
            double x = b.Fits(viewW, viewH)
                ? b.Cx - viewW / 2
                : Math.Max(b.L, Math.Min(b.R - viewW, focusX - viewW / 2));

            double y = b.Fits(viewW, viewH)
                ? b.Cy - viewH / 2
                : Math.Max(b.T, Math.Min(b.B - viewH, focusY - viewH / 2));

            return (x, y);
        }

        /// <summary>The transform, with the viewport folded in.</summary>
        public static Camera Update(
            RoomBounds b, double focusX, double focusY, double viewW, double viewH)
        {
            var (x, y) = CameraTarget(b, focusX, focusY, viewW, viewH);
            return new Camera { X = x, Y = y, ViewW = viewW, ViewH = viewH };
        }

        /// <summary>Where a room's rectangle starts. [h:Frame-5]</summary>
        public static RoomBounds RoomBoundsAt(double w, double h)
        {
            var s = RoomBounds.Standard;
            return new RoomBounds(s.L, s.T, s.L + w, s.T + h);
        }
    }
}
