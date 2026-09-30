using System;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The three streams, the floor seed, and the base36 codec - checked against values read out of
    /// the running JavaScript rather than recomputed from the formula.
    ///
    /// <para>
    /// This is the class where being "nearly right" is the same as being broken. The bare
    /// <c>Mulberry32</c> was already ported and correct, and on its own it is not the design - the
    /// design is the SPLIT, and a port with one stream and a port with three produce different
    /// dungeons from the same seven characters. Nothing in the game would notice. The friend you
    /// pasted a seed to would simply get a different run.
    /// </para>
    ///
    /// <para>
    /// Every figure here is a measurement. Two of the expectations were written wrong on the first
    /// pass - both in <see cref="TheSeedCodecAgreesWithTheJavaScript"/> - and the JavaScript was
    /// right in both cases, which is why the vectors are transcribed rather than derived.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class RngParityTests
    {
        // ------------------------------------------------- the generator itself

        [Test]
        public void Mulberry32ProducesTheSameStreamAsTheJavaScript()
        {
            // seed 1, first six draws, exactly as the browser reports them
            double[] expected =
            {
                0.627073940588, 0.002735721180, 0.527447039960,
                0.981050967472, 0.968377898214, 0.281103502959,
            };
            var rng = new Mulberry32(1);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(rng.NextDouble(), Is.EqualTo(expected[i]).Within(1e-12),
                    "draw " + i + " of mulberry32(1) has diverged, so every seed in the game is wrong");
            }
        }

        // ------------------------------------------------- the split

        [Test]
        public void TheThreeStreamsAreThreeDifferentStreams()
        {
            // seed 1234567, first five draws of each. If any column matches another, two streams are
            // sharing a generator and the whole point of the split is gone.
            double[] run = { 0.607467930997, 0.191446891520, 0.437513126759, 0.337278673425, 0.126033252105 };
            double[] jitter = { 0.053529832046, 0.269312135642, 0.864696480567, 0.738777662162, 0.835735494737 };
            double[] art = { 0.198311642278, 0.232303196331, 0.182366358815, 0.981524407165, 0.603069302160 };

            var r = new Rng(1234567u);
            for (int i = 0; i < 5; i++)
            {
                Assert.That(r.Run(), Is.EqualTo(run[i]).Within(1e-12), "run draw " + i);
                Assert.That(r.Jitter(), Is.EqualTo(jitter[i]).Within(1e-12), "jitter draw " + i);
                Assert.That(r.Art(), Is.EqualTo(art[i]).Within(1e-12), "art draw " + i);
            }
        }

        [Test]
        public void ADrawFromOneStreamCannotReachIntoAnother()
        {
            // THE reason for the split, and the failure it prevents: a shell's spread roll shifting
            // the dungeon. Seeds rot silently, and the first symptom is a friend pasting a seed back
            // and getting a different run.
            var r = new Rng(777u);
            r.Run(); r.Run(); r.Run();
            Assert.That(r.JitterCalls, Is.EqualTo(0), "drawing from run advanced the jitter stream");
            Assert.That(r.ArtCalls, Is.EqualTo(0), "drawing from run advanced the art stream");
            Assert.That(r.RunCalls, Is.EqualTo(3));

            r.Jitter();
            Assert.That(r.RunCalls, Is.EqualTo(3), "drawing from jitter advanced the run stream");
            Assert.That(r.ArtCalls, Is.EqualTo(0), "drawing from jitter advanced the art stream");
        }

        [Test]
        public void SetResetsTheCallCounts()
        {
            // counts are a diagnostic, and a diagnostic that lies is worse than none
            var r = new Rng(1u);
            r.Run(); r.Jitter(); r.Art();
            r.Set(1u);
            Assert.That(r.RunCalls, Is.EqualTo(0));
            Assert.That(r.JitterCalls, Is.EqualTo(0));
            Assert.That(r.ArtCalls, Is.EqualTo(0));
        }

        [Test]
        public void NeighbouringSeedsDoNotCollapseOntoEachOther()
        {
            // the reason the two offsets exist. Seeded with the raw seed, all three streams would be
            // identical for identical seeds and nearly identical for adjacent ones.
            double firstOf(uint seed)
            {
                var r = new Rng(seed);
                return r.Run();
            }
            Assert.That(firstOf(1234567u), Is.EqualTo(0.607467930997).Within(1e-12));
            Assert.That(firstOf(1234568u), Is.EqualTo(0.828549127327).Within(1e-12));
            Assert.That(firstOf(1u), Is.EqualTo(0.627073940588).Within(1e-12));
            Assert.That(firstOf(2u), Is.EqualTo(0.734250944341).Within(1e-12));
            // and the wrap, which is the case most likely to be special-cased wrongly
            Assert.That(firstOf(4294967295u), Is.EqualTo(0.896422614111).Within(1e-12));
            Assert.That(firstOf(0u), Is.EqualTo(0.266429208685).Within(1e-12));
        }

        // ------------------------------------------------- the floor seed

        [Test]
        public void TheFloorSeedMatchesTheJavaScript()
        {
            uint[] expected =
            {
                4247955138u, 2830667164u, 679752083u, 877719564u, 3127098494u,
                1347621906u, 3094205066u, 2589053383u, 3004253337u,
            };
            for (int i = 0; i < 8; i++)
            {
                Assert.That(Rng.FloorSeed(1234567u, i + 1), Is.EqualTo(expected[i]),
                    "floor " + (i + 1) + " seed");
            }
            Assert.That(Rng.FloorSeed(1234567u, 999), Is.EqualTo(expected[8]), "floor 999 seed");
        }

        [Test]
        public void EveryFloorIsAPureFunctionOfRootAndFloor()
        {
            // NOT chained. If it were, floor 3's seed would depend on how many draws floor 2 made,
            // two players typing the same seed would get different floor 3s, and the whole point of a
            // seed the player can read out loud is gone.
            uint root = 1234567u;
            uint a3 = Rng.FloorSeed(root, 3);
            for (int f = 1; f <= 20; f++) { Rng.FloorSeed(root, f); }   // churn the streams
            Assert.That(Rng.FloorSeed(root, 3), Is.EqualTo(a3),
                "asking for floor 3's seed twice gave two answers, so it is being derived from " +
                "something that moves");
        }

        [Test]
        public void AdjacentFloorsDoNotLookAlike()
        {
            // the mixing is mulberry32's own output function rather than a fixed multiplier precisely
            // so that nearby floors land far apart
            for (int f = 1; f < 40; f++)
            {
                uint a = Rng.FloorSeed(1234567u, f), b = Rng.FloorSeed(1234567u, f + 1);
                int bits = System.Numerics.BitOperations.PopCount(a ^ b);
                Assert.That(bits, Is.GreaterThan(4),
                    "floors " + f + " and " + (f + 1) + " differ in only " + bits +
                    " bits, so their dungeons will look like the same dungeon");
            }
        }

        [Test]
        public void DifferentRootsDoNotShareFloors()
        {
            var seen = new System.Collections.Generic.HashSet<uint>();
            for (uint root = 1; root <= 500; root++)
            {
                Assert.That(seen.Add(Rng.FloorSeed(root, 1)),
                    "two different roots produced the same floor 1 seed");
            }
        }

        // ------------------------------------------------- the codec

        [Test]
        public void TheSeedCodecAgreesWithTheJavaScript()
        {
            Assert.That(Rng.Encode(0u), Is.EqualTo("0000000"));
            Assert.That(Rng.Encode(1u), Is.EqualTo("0000001"));
            Assert.That(Rng.Encode(42u), Is.EqualTo("0000016"));
            Assert.That(Rng.Encode(1234567u), Is.EqualTo("000QGLJ"));
            Assert.That(Rng.Encode(2654435761u), Is.EqualTo("17WDRQP"));
            Assert.That(Rng.Encode(4294967295u), Is.EqualTo("1Z141Z3"));
        }

        [Test]
        public void TheSeedIsAlwaysSevenCharacters()
        {
            // fixed width, so a seed is always the same shape on screen
            for (uint n = 0; n < 40000; n += 617)
            {
                Assert.That(Rng.Encode(n).Length, Is.EqualTo(7), "seed " + n + " is not seven characters");
            }
        }

        [Test]
        public void DecodingRoundTrips()
        {
            foreach (uint n in new uint[] { 0, 1, 42, 1234567, 2654435761, 4294967295 })
            {
                Assert.That(Rng.Decode(Rng.Encode(n)), Is.EqualTo(n), "seed " + n + " did not survive a round trip");
            }
        }

        [Test]
        public void DecodingToleratesWhatAPlayerWillActuallyType()
        {
            // the original strips anything that is not alphanumeric and upper-cases first, so a
            // lowercase or punctuated paste still works. Being stricter here would mean a friend
            // pasting "4g5h6j" is told their own seed is invalid.
            Assert.That(Rng.Decode("4g5h6j"), Is.EqualTo(Rng.Decode("4G5H6J")),
                "a lowercase paste is rejected, so a seed typed by hand silently fails");
            Assert.That(Rng.Decode(" 0000001 "), Is.EqualTo(1u), "a paste with whitespace is rejected");
        }

        [Test]
        public void DecodingRejectsWhatIsNotASeed()
        {
            Assert.That(Rng.Decode(null), Is.Null);
            Assert.That(Rng.Decode(""), Is.Null, "an empty box is not a seed");
            Assert.That(Rng.Decode("!!!"), Is.Null, "punctuation alone is not a seed");
            // eight characters is one too many: 36^8 is far past what a fixed-width field can show
            Assert.That(Rng.Decode("00000001"), Is.Null, "an eight-character paste is accepted, so the " +
                "fixed width is a suggestion rather than a rule");
        }

        [Test]
        public void ASeedTooLargeForThirtyTwoBitsWrapsRatherThanFailing()
        {
            // "ZZZZZZZ" is seven valid base36 digits, which is 36^7-1 = 78364164095 - well past 2^32.
            // The original does `n>>>0` and wraps to 1054752767, and the port has to wrap identically.
            //
            // This is a REAL BUG the test caught rather than a hypothetical: `(uint)someDouble` in C#
            // does not wrap the way `>>> 0` does in JavaScript, it saturates to 4294967295. Those are
            // different seeds, so the largest seed the game can display could not be typed back in to
            // get the run that is on screen - which is the entire promise of the feature.
            Assert.That(Rng.Decode("ZZZZZZZ"), Is.EqualTo(1054752767u),
                "a maximal seed does not wrap the way the original wraps, so the largest seed on " +
                "screen cannot be typed back in. (C# saturates a double-to-uint cast; it does not wrap.)");
        }

        [Test]
        public void TheLargestSeedOnScreenIsNotItsOwnRoundTripAndDoesNotNeedToBe()
        {
            // The field is seven base36 characters and the largest such value does not fit in
            // thirty-two bits, so ZZZZZZZ -> 1054752767 -> 0HFZ0FZ. That is correct and intended.
            // What has to hold is the weaker and actually-load-bearing property: the wrap is
            // DETERMINISTIC, so the same characters always give the same run, and any seed that
            // does fit round-trips exactly.
            Assert.That(Rng.Encode(Rng.Decode("ZZZZZZZ")!.Value), Is.EqualTo("0HFZ0FZ"),
                "the wrap is not stable, so the same seven characters give two different runs");
            Assert.That(Rng.Decode("0HFZ0FZ"), Is.EqualTo(1054752767u), "and it does not survive the trip back");
            // every seed that fits, round-trips exactly
            Assert.That(Rng.Encode(4294967295u), Is.EqualTo("1Z141Z3"));
            Assert.That(Rng.Decode("1Z141Z3"), Is.EqualTo(4294967295u));
        }
    }
}
