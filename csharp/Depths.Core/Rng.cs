namespace Depths
{
    /// <summary>The random source the simulation draws from. [h:Rng-1]</summary>
    public interface IRng
    {
        /// <summary>A double in [0, 1).</summary>
        double NextDouble();

        /// <summary>An integer in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }

    /// <summary>mulberry32, ported to produce the SAME stream as the JavaScript original. [h:Rng-2]</summary>
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

    }

    /// <summary>The random source, and the three streams it is split into. [h:Rng-3]</summary>
    public sealed class Rng
    {
        /// <summary>Each stream is seeded from the one number the player can see, through a different constant. [h:Rng-4]</summary>
        public const uint OffJitter = 0x9E3779B9u;
        public const uint OffArt = 0x85EBCA6Bu;

        /// <summary>A short readable seed. [h:Rng-5]</summary>
        public const int Width = 7;
        public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // Provisional, so the compiler can see definite assignment. [h:Rng-6]
        private Mulberry32 _run = new Mulberry32(0);
        private Mulberry32 _jitter = new Mulberry32(0);
        private Mulberry32 _art = new Mulberry32(0);
        private bool _seeded;
        private uint _seed;

        /// <summary>A call count per stream, so the tests can prove the split is being respected rather than merely intended. [h:Rng-7]</summary>
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

        /// <summary>A game with an unseeded RNG is a game whose first run is unreproducible, and that is the run everybody... [h:Rng-8]</summary>
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
        public T Pick<T>(System.Collections.Generic.IList<T> items) => items[(int)(Run() * items.Count)];

        /// <summary>THE FLOOR SEED. Floor N is generated from the root seed the player typed, mixed with N, and NOT from the... [h:Rng-9]</summary>
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
            // (n >>> 0).toString(36).toUpperCase() in the original, zero-padded to seven. [h:Rng-10]
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

        /// <summary>Parses a player-typed seed. [h:Rng-11]</summary>
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

            // Above 32 bits is refused, as the game refuses it (src/05-rng.js decode): the 7-character field reaches... [h:Rng-12]
            if (acc > 4294967295.0) return null;
            return (uint)acc;
        }
    
}
}
