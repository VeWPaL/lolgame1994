using System;

namespace Depths.Unity
{
    /// <summary>Turns frame time into simulation ticks at Balance.TickHz: a fixed-step accumulator, so the game runs at the... [h:TickClock-1]</summary>
    public sealed class TickClock
    {
        /// <summary>A longer frame (a hitch, a breakpoint) is cut to this, so the game never fast-forwards.</summary>
        public const double MaxFrameSeconds = 0.25;

        double _acc;   // in ticks

        public long Ticks { get; private set; }

        /// <summary>Real seconds cut from long frames: the game deliberately did not play them.</summary>
        public double DroppedSeconds { get; private set; }

        /// <summary>How far the clock is into the next tick, 0 to 1: where the screen draws between the last two.</summary>
        public double Alpha => _acc;

        /// <summary>The ticks to run for a frame of <paramref name="seconds"/>; none while paused, and no catch-up after.</summary>
        public int Advance(double seconds, bool paused)
        {
            if (paused || !(seconds > 0)) return 0;   // also NaN, which would poison the accumulator for good
            if (seconds > MaxFrameSeconds) DroppedSeconds += seconds - MaxFrameSeconds;
            _acc += Math.Min(seconds, MaxFrameSeconds) * Balance.TickHz;
            int n = (int)Math.Floor(_acc);
            _acc -= n;
            Ticks += n;
            return n;
        }
    }
}
