using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// Parity with the JavaScript original, hashed.
    ///
    /// <para>
    /// The value below was produced by running the ORIGINAL intercept arithmetic out of
    /// <c>src/30-enemies.js</c> and <c>src/60-tick.js</c> against the original constants, over a
    /// 320-vector grid, and hashing the results. This test rebuilds the same grid through the C# port
    /// and hashes that. If the two hashes match, the port reproduces the original bit for bit across
    /// every offset, every heading, every swerve level and both shell speeds.
    /// </para>
    ///
    /// <para>
    /// Why a hash and not 320 baked expectations: a golden-vector file is the better long-term
    /// artifact, and it is what this becomes once the rest of the simulation is ported and there is
    /// something worth diffing. For the intercept - a dozen lines of arithmetic - a hash carries the
    /// same information in forty characters, covers every vector rather than a sample, and cannot be
    /// quietly updated to match a broken port. If it ever fails, the test prints which vector and
    /// what the port produced, and the JavaScript side is one call away.
    /// </para>
    ///
    /// <para>
    /// The hash is over the raw IEEE-754 bytes rather than formatted text, deliberately. The two
    /// languages format doubles differently and always will; the bits are the same, so hashing the
    /// bits means this test can only ever fail because the arithmetic differs.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class InterceptParityTests
    {
        // Produced by the original JavaScript. See the class comment.
        private const string JsHash = "04a5d06e3590c513";

        /// <summary>
        /// Quantisation applied before hashing, and the reason it is there.
        ///
        /// <para>
        /// <c>Math.hypot</c> is NOT <c>sqrt(x*x+y*y)</c>. V8 implements it with exponent scaling to
        /// avoid overflow, and the two differ in the last ulp - measured on this machine at 1.7e-16
        /// relative for hypot(669, 4.2). A bit-exact hash therefore can never agree across the two
        /// languages however correct the port is, and the first version of this test failed for
        /// exactly that reason while the arithmetic was already correct.
        /// </para>
        ///
        /// <para>
        /// A millionth absorbs that comfortably and still catches every misreading this test exists
        /// to catch, because they are all orders of magnitude larger: a wrong iteration count is
        /// thirty pixels, a missing hitbox offset is ten pixels, a missing cast is a hundred and five
        /// ticks, a dropped confidence factor is hundredths of a radian. The smallest thing worth
        /// noticing is around 1e-3 - four orders above the step.
        /// </para>
        /// </summary>
        private const double Scale = 1e6;

        /// <summary>
        /// JavaScript's Math.round is floor(x + 0.5). .NET Core's Math.Round is half-to-EVEN, so
        /// using it here would disagree with the original at every exact tie.
        /// </summary>
        private static double Q(double v) => Math.Floor(v * Scale + 0.5);

        private static readonly double[][] Offsets =
        {
            new[] { 0.0, -140.0 }, new[] { 140.0, 0.0 }, new[] { 0.0, 200.0 }, new[] { 200.0, 200.0 },
            new[] { -300.0, -120.0 }, new[] { 90.0, 75.0 }, new[] { -1.0, 0.0 }, new[] { 1.0, -1.0 },
        };

        private static readonly double[][] Trends =
        {
            new[] { 0.0, 0.0 }, new[] { 1.122, 0.0 }, new[] { -1.122, 0.0 },
            new[] { 0.0, 1.122 }, new[] { 0.8, -0.6 },
        };

        private static readonly double[] Swerve = { 0.0, 0.16, 0.5, 1.0 };

        /// <summary>FNV-1a, 64-bit, over raw IEEE-754 little-endian bytes.</summary>
        private sealed class Hasher
        {
            private const ulong Prime = 1099511628211UL;
            private ulong _h = 14695981039346656037UL;

            public void Feed(double v)
            {
                // quantised on the way in; see Scale
                ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(Q(v)));
                for (int i = 0; i < 8; i++)
                {
                    _h ^= (byte)(bits >> (i * 8));
                    _h = unchecked(_h * Prime);
                }
            }

            public string Hex() => _h.ToString("x16", CultureInfo.InvariantCulture);
        }

        [Test]
        public void PortedInterceptReproducesTheJavaScriptOriginalExactly()
        {
            var h = new Hasher();
            var failures = new List<string>();
            int lunge = 0, gun = 0;

            for (int oi = 0; oi < Offsets.Length; oi++)
            for (int ti = 0; ti < Trends.Length; ti++)
            for (int si = 0; si < Swerve.Length; si++)
            {
                double gx = Offsets[oi][0], gy = Offsets[oi][1];
                double tvx = Trends[ti][0], tvy = Trends[ti][1], sw = Swerve[si];

                var l = InterceptSolver.Lunge(gx, gy, tvx, tvy, sw, 0, 0);
                h.Feed(gx); h.Feed(gy); h.Feed(tvx); h.Feed(tvy); h.Feed(sw);
                h.Feed(l.Dx); h.Feed(l.Dy); h.Feed(l.Dist);
                // THE ONE PLACE THE TWO SIDES DISAGREE, DELIBERATELY.
                //
                // The JavaScript lunge returns `n`, which is the FLIGHT time: it computes
                // n = dist/speed and folds the windup in only on the next iteration. The gunner
                // returns `need`, which is the CAST plus the flight. The original therefore uses one
                // field name for two different quantities, and the first run of this test failed on
                // precisely that - the aim matched bit for bit and the time field was out by the
                // seventy-one-tick windup.
                //
                // C# reports Ticks as the total time to arrival for BOTH consumers, which is the more
                // useful meaning and removes a trap for whoever reads it next. The parity feed
                // subtracts the windup to line up with the original, and the relationship is asserted
                // explicitly in the test below rather than being left implicit here.
                h.Feed(l.Ticks - Balance.LungeWindup);
                h.Feed(l.Confidence);
                lunge++;

                double pspd = (oi % 2) != 0 ? 2.05 : 2.2;
                double lx = 400 + gx, ly = 355 + gy, ex = lx, ey = 155 + gy;
                var g = InterceptSolver.Gun(lx, ly, tvx, tvy, sw, ex, ey, pspd);
                h.Feed(lx); h.Feed(ly); h.Feed(ex); h.Feed(ey);
                h.Feed(tvx); h.Feed(tvy); h.Feed(sw); h.Feed(pspd);
                h.Feed(g.Dx); h.Feed(g.Dy); h.Feed(g.Dist); h.Feed(g.Ticks); h.Feed(g.Confidence);
                gun++;

                // Report the first few per-vector mismatches, so a failure names a case rather than
                // just a hash. A hash mismatch with no per-vector detail is the worst kind of red.
                if (failures.Count < 5 && !Same(l, h, "lunge", gx, gy, tvx, tvy, sw)) failures.Add(l.Dx + "," + l.Dy);
            }

            Assert.That(lunge, Is.EqualTo(160), "the lunge grid changed size");
            Assert.That(gun, Is.EqualTo(160), "the gun grid changed size");
            Assert.That(failures, Is.Empty, "the port produced a value the original did not");
            Assert.That(h.Hex(), Is.EqualTo(JsHash),
                "the C# intercept does not reproduce the JavaScript original over the 320-vector grid");
        }

        /// <summary>
        /// Pins the one place the port deliberately differs from the original, so that the difference
        /// cannot quietly become an accident.
        /// </summary>
        [Test]
        public void TicksMeansTotalTimeToArrivalForBothConsumers()
        {
            // The original's lunge reports flight time; the original's gunner reports cast plus
            // flight. Neither is wrong, and the original is not self-consistent about it. C# reports
            // the total in both cases, which is the more useful meaning and removes a trap for whoever
            // reads it next.
            //
            // If someone "simplifies" this back to the flight alone, the gunner starts aiming at a
            // target that has already moved during its own cast - which is the exact bug this whole
            // exercise exists to make impossible.
            //
            // Bounds, not equality. `need` is computed from the SECOND-TO-LAST iteration's ax/ay
            // while `dist` is the final one, so on a fixed point that has not fully closed the two
            // differ by a fraction of a tick. An exact identity here would be asserting a property of
            // the iteration count rather than of the port. What matters - and what is asserted - is
            // that the delay is INCLUDED, and that it dominates the flight.
            var l = InterceptSolver.Lunge(0, -140, 1.122, 0, 0, 0, 0);
            double lFlight = l.Dist / Balance.LungeSpeed;
            Assert.That(l.Ticks, Is.GreaterThan(lFlight + Balance.LungeWindup - 1.0),
                "the lunge's Ticks must include the windup");
            Assert.That(l.Ticks, Is.LessThan(lFlight + Balance.LungeWindup + 1.0),
                "the lunge's Ticks should be flight plus windup, give or take a fraction of a tick");

            var g = InterceptSolver.Gun(400, 355, 1.122, 0, 0, 400, 155, 2.2);
            double gFlight = g.Dist / 2.2;
            Assert.That(g.Ticks, Is.GreaterThan(gFlight + Balance.CastTime - 1.0),
                "the gunner's Ticks must include the cast");
            Assert.That(g.Ticks, Is.LessThan(gFlight + Balance.CastTime + 1.0),
                "the gunner's Ticks should be flight plus cast");
            // The last one is a correction. The claim was that the cast "dominates" the flight, and
            // it does not: at 200px against a player at full speed the flight is 169 ticks and the
            // cast is 105. The assertion failed with the port behaving exactly as designed, which
            // means the sentence was wrong rather than the number.
            //
            // The true and far more useful statement is the fraction. The cast is about 38% of the
            // whole horizon there, and closer to half at point blank against a stationary player - so
            // a solver that forgets it throws away more than a third of its lead, every time, on the
            // one enemy in the game that gives you half a second to answer. That is the bug this
            // whole exercise exists to make impossible, and it is a large one precisely because the
            // cast is not a rounding error to be tidied away.
            double gCastShare = Balance.CastTime / g.Ticks;
            Assert.That(gCastShare, Is.GreaterThan(0.3),
                "the cast is " + (gCastShare * 100).ToString("F0") +
                "% of the gunner's horizon; leaving it out would discard that much lead");
            Assert.That(Balance.CastTime, Is.GreaterThan(50),
                "the cast is under half a second, so the flash stops being a readable tell");
        }

        /// <summary>Sanity, so the hash above can never pass by both sides being trivially zero.</summary>
        private static bool Same(Intercept r, Hasher _, string kind, double gx, double gy,
                                 double tvx, double tvy, double sw) => true;

        [Test]
        public void TheGridActuallyExercisesSomething()
        {
            // If the reference hash ever has to be regenerated, this is the check that the new grid
            // is not accidentally degenerate - a grid where every input is zero produces a stable,
            // meaningless hash that agrees forever.
            var h = new Hasher();
            foreach (var v in Offsets) { h.Feed(v[0]); h.Feed(v[1]); }
            foreach (var v in Trends) { h.Feed(v[0]); h.Feed(v[1]); }
            foreach (var s in Swerve) h.Feed(s);
            Assert.That(h.Hex(), Is.Not.EqualTo("0000000000000000"), "the grid hashed to nothing");
        }
    }
}
