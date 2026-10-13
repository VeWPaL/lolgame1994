using System;

namespace Depths.Unity
{
    /// <summary>
    /// Turns frame time into simulation ticks at <c>Balance.TickHz</c>: a fixed-step accumulator, so the
    /// game runs at the same speed at any frame rate. Kept out of GameView so it can be tested.
    /// </summary>
    public sealed class TickClock
    {
        /// <summary>A longer frame (a hitch, a breakpoint) is cut to this, so the game never fast-forwards.</summary>
        public const double MaxFrameSeconds = 0.25;

        double _acc;   // in ticks

        public long Ticks { get; private set; }

        /// <summary>The ticks to run for a frame of <paramref name="seconds"/>; none while paused, and no catch-up after.</summary>
        public int Advance(double seconds, bool paused)
        {
            if (paused || !(seconds > 0)) return 0;   // also NaN, which would poison the accumulator for good
            _acc += Math.Min(seconds, MaxFrameSeconds) * Balance.TickHz;
            int n = (int)Math.Floor(_acc);
            _acc -= n;
            Ticks += n;
            return n;
        }
    }
}
