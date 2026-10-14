# Rng.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Rng.cs`. The code keeps a one-line gist tagged `[h:Rng-N]`.

## [h:Rng-1]
near: `public interface IRng`

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

## [h:Rng-2]
near: `public sealed class Mulberry32 : IRng`

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

## [h:Rng-3]
near: `public sealed class Rng`

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

## [h:Rng-4]
near: `public const uint OffJitter = 0x9E3779B9u;`

/// <summary>
/// Each stream is seeded from the one number the player can see, through a different constant.
/// A fixed irrational-looking offset means the streams do not overlap for nearby seeds: seed 1
/// and seed 2 must not produce the same dungeon, which they would if both streams were simply
/// seeded with the seed.
/// </summary>

## [h:Rng-5]
near: `public const int Width = 7;`

/// <summary>
/// A short readable seed. base36 keeps it typable and copyable, and seven characters is 78
/// billion possibilities - far more than a player will exhaust and far fewer than they will
/// mistype. Fixed width, so a seed is always the same shape on screen.
/// </summary>

## [h:Rng-6]
near: `private Mulberry32 _run = new Mulberry32(0);`

// Provisional, so the compiler can see definite assignment. Set() is the real initialiser and
// overwrites all three before anything can draw - and _seeded is what actually guarantees
// that, which is a better guarantee than a constructor the compiler happens to accept.

## [h:Rng-7]
near: `public int RunCalls { get; private set; }`

/// <summary>
/// A call count per stream, so the tests can prove the split is being respected rather than
/// merely intended. An unused diagnostic is a comment; a counted one is a test.
/// </summary>

## [h:Rng-8]
near: `private void RequireSeeded()`

/// <summary>
/// A game with an unseeded RNG is a game whose first run is unreproducible, and that is the
/// run everybody remembers. The JavaScript seeds once at module load for exactly this reason;
/// here it is a throw rather than a silent zero-seed stream, because a stream that quietly
/// works is a stream nobody notices is wrong.
/// </summary>

## [h:Rng-9]
near: `public static uint FloorSeed(uint root, int n)`

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

## [h:Rng-10]
near: `string s = ToBase36(n);`

// (n >>> 0).toString(36).toUpperCase() in the original, zero-padded to seven. Built by hand
// rather than by Convert.ToString because ToString("X") is HEXADECIMAL, and a hex seed is
// a different, longer, more mistypeable thing.

## [h:Rng-11]
near: `public static uint? Decode(string? text)`

/// <summary>
/// Parses a player-typed seed. Returns null rather than throwing or guessing, because a seed
/// the player mistyped has to come back as "not a seed" and not as a different seed - a
/// silently-corrected typo is a run nobody can share.
/// </summary>

## [h:Rng-12]
near: `if (acc > 4294967295.0) return null;`

// Above 32 bits is refused, as the game refuses it (src/05-rng.js decode): the 7-character
// field reaches 36^7-1, so this rejects the top 94.5% rather than wrapping. Wrapping, as this
// port used to, accepted ZZZZZZZ as a different seed the game would not play.
