using System;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// A scratch harness that prints what the port generates. Not a test.
    ///
    /// <para>
    /// This exists because a parity table has to be READ OUT OF THE RUNNING JAVASCRIPT rather than
    /// computed, and the first half of doing that is seeing what the port says. It prints the same
    /// signature string the JavaScript side emits, so the two can be laid side by side - and any
    /// disagreement is a draw-order bug, which cannot be diagnosed by reading the C#.
    ///
    /// <para>
    /// It is skipped by default so it cannot rot into a false green. Run it by name.
    /// </para>
    /// </summary>
    [TestFixture]
    [Explicit("prints generator signatures; not an assertion")]
    public sealed class GeneratorSignatureDump
    {
        [Test]
        public void Dump()
        {
            uint[] seeds =
            {
                0, 1, 2, 3, 42, 12345, 12346, 99999, 2654435761, 4294967295,
            };
            foreach (var seed in seeds)
            {
                var rng = new Rng(seed);
                var d = Dungeon.FromSeed(rng);
                TestContext.Out.WriteLine($"seed {seed}  rooms {d.AllRooms.Count}  calls {rng.RunCalls}");
                TestContext.Out.WriteLine("  " + d.Signature());
            }
        }
    }
}