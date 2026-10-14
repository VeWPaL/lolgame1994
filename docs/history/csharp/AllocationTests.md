# AllocationTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/AllocationTests.cs`. The code keeps a one-line gist tagged `[h:AllocationTests-N]`.

## [h:AllocationTests-1]
near: `[TestFixture, Category("csharp-only")]`

/// <summary>
/// CONVENTIONS: no per-tick allocations in C# hot paths (Unity GC hitches). Measured with the
/// runtime's per-thread allocation counter, after a warm-up that may size pools and buffers.
/// </summary>

## [h:AllocationTests-2]
near: `Assert.That(bytes, Is.LessThan(24 * 600 / 10), "600 fight ticks allocated " + bytes + " bytes: something allocates every`

// no PER-TICK allocation: the smallest object is 24 bytes, so one a tick would be 14400 here. A few event
// objects in 10 s of fighting (contact hits, pack re-targets) are not the GC churn this guards against.
// It caught TickUnlock's literal array: 67200 bytes, 112 a tick, in every run since the port.
