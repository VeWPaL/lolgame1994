using System;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>A scratch harness that prints what the port generates. [h:GeneratorSignatureDump-1]</summary>
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