using System;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Is = NUnit.Framework.Is;

namespace Depths.Unity.Tests
{
    /// <summary>"Allocates nothing", checked the way Unity can check it: the Test Framework's AllocatingGCMemory constraint... [h:AllocMeter-1]</summary>
    public static class AllocMeter
    {
        /// <summary>Runs <paramref name="work"/> once to warm it (Mono allocates on a method's first call), then
        /// <paramref name="repeats"/> times measured, and fails if any of those allocated.</summary>
        public static void AssertNone(Action work, int repeats, string what)
        {
            work();
            Assert.That(() => { for (int i = 0; i < repeats; i++) work(); }, AllocIs.Not.AllocatingGCMemory(), what);
        }
    }

    public class AllocMeterTests
    {
        [Test]
        public void TheMeterCatchesOneSmallAllocation()
        {
            object keep = null;
            Assert.Throws<AssertionException>(() => AllocMeter.AssertNone(() => keep = new byte[16], 1, "a 16-byte array"),
                                              "the meter must fail on a single small allocation, or it proves nothing");
            Assert.That(keep, Is.Null.Or.Not.Null);
        }

        [Test]
        public void TheMeterPassesWorkThatAllocatesNothing()
        {
            int x = 0;
            AllocMeter.AssertNone(() => x++, 1000, "a counter");
            Assert.That(x, Is.EqualTo(1001), "one warm call, then the thousand measured");
        }
    }
}
