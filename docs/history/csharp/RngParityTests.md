# RngParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RngParityTests.cs`. The code keeps a one-line gist tagged `[h:RngParityTests-N]`.

## [h:RngParityTests-1]
near: `[TestFixture]`

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

## [h:RngParityTests-2]
near: `var r = new Rng(777u);`

// THE reason for the split, and the failure it prevents: a shell's spread roll shifting
// the dungeon. Seeds rot silently, and the first symptom is a friend pasting a seed back
// and getting a different run.

## [h:RngParityTests-3]
near: `uint root = 1234567u;`

// NOT chained. If it were, floor 3's seed would depend on how many draws floor 2 made,
// two players typing the same seed would get different floor 3s, and the whole point of a
// seed the player can read out loud is gone.

## [h:RngParityTests-4]
near: `Assert.That(Rng.Decode("4g5h6j"), Is.EqualTo(Rng.Decode("4G5H6J")),`

// the original strips anything that is not alphanumeric and upper-cases first, so a
// lowercase or punctuated paste still works. Being stricter here would mean a friend
// pasting "4g5h6j" is told their own seed is invalid.

## [h:RngParityTests-5]
near: `Assert.That(Rng.Decode("ZZZZZZZ"), Is.Null, "a seed the game refuses is accepted by the port");`

// Read from the running game 2026-10-06: Rnd.decode("ZZZZZZZ") is null, "1Z141Z4" is null,
// "1Z141Z3" is 4294967295. The port used to wrap ZZZZZZZ to 1054752767 under a stale premise
// ("the original does n>>>0"); it does not, it refuses.
