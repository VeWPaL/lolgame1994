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
    ///
    /// <para>
    /// On its own this class is NOT the design. The design is the split into three streams, which is
    /// <see cref="Rng"/> below; this is only the generator underneath it.
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

    /// <summary>
    /// The random source, and the three streams it is split into.
    ///
    /// <para>
    /// THE PART THAT ACTUALLY MATTERS IS NOT THE SEED. It is the split into three streams, and this
    /// is the class that exists to port it, because the bare <see cref="Mulberry32"/> above is only
    /// the generator and not the design.
    /// </para>
    ///
    /// <para>
    /// A single stream is a trap, and a slow one. Everything draws from it in call order, so adding
    /// one idle-wander roll - or changing the art, which rolls a great deal - shifts every subsequent
    /// draw, and the dungeon published last month is no longer the dungeon that seed produces. Seeds
    /// silently rot, and nobody notices until a friend pastes one back, gets a different run, and
    /// concludes the feature is broken.
    /// </para>
    ///
    /// <para>
    /// So randomness is sorted by WHAT IT DECIDES:
    /// </para>
    /// <list type="bullet">
    /// <item><c>run</c> - what the run IS. Dungeon shape, room growth, spawn placement, pack
    /// composition, which bodies, item rolls. A seed fixes this and nothing else.</item>
    /// <item><c>jitter</c> - cosmetic timing and behaviour noise: cooldown rolls, idle wander, a
    /// gunner's dodge, a shell's spread. It has to be reproducible, or the same seed plays
    /// differently twice, but it must never be able to reach into the run.</item>
    /// <item><c>art</c> - texture noise: the grain in the wood, the fleck in the paper. Purely
    /// cosmetic, consumed a great deal, and therefore the one most likely to change without anyone
    /// changing the game.</item>
    /// </list>
    ///
    /// <para>
    /// The player is shown ONE number. The other two are derived from it, so a seed is a single
    /// thing to copy and the streams stay independent of each other.
    /// </para>
    /// </summary>
    public sealed class Rng
    {
        /// <summary>
        /// Each stream is seeded from the one number the player can see, through a different constant.
        /// A fixed irrational-looking offset means the streams do not overlap for nearby seeds: seed 1
        /// and seed 2 must not produce the same dungeon, which they would if both streams were simply
        /// seeded with the seed.
        /// </summary>
        public const uint OffJitter = 0x9E3779B9u;
        public const uint OffArt = 0x85EBCA6Bu;

        /// <summary>
        /// A short readable seed. base36 keeps it typable and copyable, and seven characters is 78
        /// billion possibilities - far more than a player will exhaust and far fewer than they will
        /// mistype. Fixed width, so a seed is always the same shape on screen.
        /// </summary>
        public const int Width = 7;
        public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // Provisional, so the compiler can see definite assignment. Set() is the real initialiser and
        // overwrites all three before anything can draw - and _seeded is what actually guarantees
        // that, which is a better guarantee than a constructor the compiler happens to accept.
        private Mulberry32 _run = new Mulberry32(0);
        private Mulberry32 _jitter = new Mulberry32(0);
        private Mulberry32 _art = new Mulberry32(0);
        private bool _seeded;
        private uint _seed;

        /// <summary>
        /// A call count per stream, so the tests can prove the split is being respected rather than
        /// merely intended. An unused diagnostic is a comment; a counted one is a test.
        /// </summary>
        public int RunCalls { get; private set; }
        public int JitterCalls { get; private set; }
        public int ArtCalls { get; private set; }

        public uint Seed => _seed;
        public string SeedText => Encode(_seed);

        public Rng(uint seed) => Set(seed);

        public uint Set(uint seed)
        {
            _seed = seed;
            _run = new Mulberry32(seed);
            _jitter = new Mulberry32(seed ^ OffJitter);
            _art = new Mulberry32(seed ^ OffArt);
            RunCalls = 0;
            JitterCalls = 0;
            ArtCalls = 0;
            _seeded = true;
            return seed;
        }

        /// <summary>
        /// A game with an unseeded RNG is a game whose first run is unreproducible, and that is the
        /// run everybody remembers. The JavaScript seeds once at module load for exactly this reason;
        /// here it is a throw rather than a silent zero-seed stream, because a stream that quietly
        /// works is a stream nobody notices is wrong.
        /// </summary>
        private void RequireSeeded()
        {
            if (!_seeded)
                throw new System.InvalidOperationException(
                    "this Rng has never been seeded. Call Set(seed) before drawing - a draw from an " +
                    "unseeded source is a run nobody can repeat, which is the failure the seed exists " +
                    "to prevent.");
        }

        public double Run() { RequireSeeded(); RunCalls++; return _run.NextDouble(); }
        public double Jitter() { RequireSeeded(); JitterCalls++; return _jitter.NextDouble(); }
        public double Art() { RequireSeeded(); ArtCalls++; return _art.NextDouble(); }

        /// <summary>Convenience, so a call site reads as a decision rather than as plumbing.</summary>
        public double Range(double lo, double hi) => lo + Run() * (hi - lo);
        public int Int(int n) => (int)(Run() * n);
        public T Pick<T>(System.Collections.Generic.IList<T> items) => items[(int)(Run() * items.Count)];

        /// <summary>
        /// THE FLOOR SEED. Floor N is generated from the root seed the player typed, mixed with N, and
        /// NOT from the previous floor's seed. That distinction is the whole reason it is written this
        /// way.
        ///
        /// <para>
        /// Chaining - reseeding from the current seed each time - would be simpler and is wrong: the
        /// streams advance as the floor is generated, so floor 3's seed would depend on how many draws
        /// floor 2 happened to make. Two players typing the same seed would get different floor 3.
        /// Deriving from a FIXED root instead makes every floor a pure function of (root, floor), so
        /// the whole run replays from the one seed the player can read out loud, and floor 12 is the
        /// same dungeon for everyone who starts from the same seven characters.
        /// </para>
        ///
        /// <para>
        /// The mix is mulberry32's own output function rather than an arbitrary constant, because any
        /// fixed multiplier risks mapping nearby floors onto nearby seeds, and adjacent floors having
        /// visibly similar layouts is exactly the failure this is meant to prevent.
        /// </para>
        /// </summary>
        public static uint FloorSeed(uint root, int n)
        {
            unchecked
            {
                uint t = root ^ (uint)((n + 1) * (int)0x9E3779B1);
                uint a = t;
                a ^= a >> 16; a *= 0x21F0AAADu;
                a ^= a >> 15; a *= 0x735A2D97u;
                a ^= a >> 15;
                return a;
            }
        }

        public static string Encode(uint n)
        {
            // (n >>> 0).toString(36).toUpperCase() in the original, zero-padded to seven. Built by hand
            // rather than by Convert.ToString because ToString("X") is HEXADECIMAL, and a hex seed is
            // a different, longer, more mistypeable thing.
            string s = ToBase36(n);
            while (s.Length < Width) s = "0" + s;
            return s;
        }

        private static string ToBase36(uint n)
        {
            if (n == 0) return "0";
            char[] buf = new char[7];
            int i = 7;
            while (n > 0 && i > 0)
            {
                buf[--i] = Alphabet[(int)(n % 36)];
                n /= 36;
            }
            return new string(buf, i, 7 - i);
        }

        /// <summary>
        /// Parses a player-typed seed. Returns null rather than throwing or guessing, because a seed
        /// the player mistyped has to come back as "not a seed" and not as a different seed - a
        /// silently-corrected typo is a run nobody can share.
        /// </summary>
        public static uint? Decode(string? text)
        {
            if (text == null) return null;
            var filtered = new System.Text.StringBuilder();
            foreach (char c in text.Trim().ToUpperInvariant())
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z')) filtered.Append(c);
            }
            string t = filtered.ToString();
            if (t.Length == 0 || t.Length > Width) return null;

            // base36, up to seven digits, which is comfortably inside a double
            double acc = 0;
            foreach (char c in t) acc = acc * 36 + Alphabet.IndexOf(c);
            if (double.IsNaN(acc) || double.IsInfinity(acc) || acc < 0) return null;

            // The wrap has to be EXPLICIT. `(uint)acc` on a double above uint.MaxValue does not wrap
            // the way JavaScript's `>>> 0` does - it saturates to 4294967295, which is a different
            // seed. Seven base36 digits reach 36^7-1 = 78364164095, comfortably past 2^32, so a
            // player who reads a maximal seed off the screen could not type it back in and get the
            // same run. That is precisely the failure the whole seed feature exists to prevent, and
            // it is invisible until someone actually types the biggest seed the game can show.
            ulong wrapped = (ulong)acc % 4294967296UL;
            return (uint)wrapped;
        }
    
}
}
