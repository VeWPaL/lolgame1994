# RunStateTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RunStateTests.cs`. The code keeps a one-line gist tagged `[h:RunStateTests-N]`.

## [h:RunStateTests-1]
near: `[TestFixture]`

/// <summary>
/// The run-scoped holder, and the one-seed-one-run property it exists to protect.
///
/// <para>
/// Finding #8, in the holder's own terms: the JavaScript kept <c>PACK_CURSOR</c> and the RNG
/// cursors outside any function, so module state outlived <c>startGame</c> and a second run
/// from the same seed began with cursors the first run had moved. These tests pin the C# shape
/// of the fix - every run's cursors live on its own object - by constructing two runs from the
/// same seed and proving they are independent copies rather than two handles on one cursor.
/// </para>
/// </summary>

## [h:RunStateTests-2]
near: `var first = new RunState(new Rng(12345u));`

// seed 12345 floor 1 plans exactly one Brunch pack (the parity table pins this), so
// PacksIssued moves - and a cursor kept statically would move with it and leak into
// the next run, which is finding #8's exact symptom.

## [h:RunStateTests-3]
near: `var first = new RunState(new Rng(7u));`

// The planner's cursor is per-run rather than per-floor: pack ids are unique across
// floors, so a fresh RunState must start from 1 no matter what the previous run left
// behind. This is the structural version of the JS resetRunCursors() call that used
// to be missing entirely.
