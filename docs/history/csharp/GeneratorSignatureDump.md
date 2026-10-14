# GeneratorSignatureDump.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/GeneratorSignatureDump.cs`. The code keeps a one-line gist tagged `[h:GeneratorSignatureDump-N]`.

## [h:GeneratorSignatureDump-1]
near: `[TestFixture]`

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
