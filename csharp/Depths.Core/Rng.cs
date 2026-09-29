namespace Depths
{
    /// <summary>
    /// The random source the simulation draws from.
    ///
    /// <para>
    /// An interface rather than a static <c>Random</c> because determinism is not a nicety here, it
    /// is the thing the whole test suite stands on. The JavaScript suite replaces <c>Math.random</c>
    /// with a seeded mulberry32, so every run of it sees the same dungeon - which is the difference
    /// between a green suite that means something and one that means "nothing threw today". The C#
    /// port has to be able to say exactly the same thing, and it can only do that if the generator is
    /// injectable.
    /// </para>
    /// </summary>
    public interface IRng
    {
        /// <summary>A double in [0, 1).</summary>
        double NextDouble();

        /// <summary>An integer in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }

    /// <summary>
    /// mulberry32, ported to produce the SAME stream as the JavaScript original.
    ///
    /// <para>
    /// That is the point of hand-porting this rather than using <see cref="System.Random"/>. The
    /// JavaScript suite seeds <c>Math.random</c> with mulberry32 at a known seed, and the whole game -
    /// dungeon shape, spawn plans, pack composition, spread rolls - hangs off that stream in call
    /// order. Reproducing the generator exactly means a C# run and a JavaScript run of the same
    /// seed walk the same dungeon, so the two implementations can be diffed against each other
    /// directly instead of being compared by eye.
    /// </para>
    ///
    /// <para>
    /// The bit fiddling is identical on purpose. <c>Math.imul</c> and C#'s unchecked int multiply are
    /// the same operation, and the shift-and-xor mixing has to be too, or the two streams diverge on
    /// the very first draw and everything after it is noise.
    /// </para>
    /// </summary>
    public sealed class Mulberry32 : IRng
    {
        private uint _seed;

        public Mulberry32(uint seed) => _seed = seed;

        public double NextDouble()
        {
            unchecked
            {
                _seed += 0x6D2B79F5u;
                uint t = _seed;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public int Next(int maxExclusive) => (int)(NextDouble() * maxExclusive);

        /// <summary>Fisher-Yates, in place. Matches the original's shuffle exactly.</summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
