# TunedTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/TunedTests.cs`. The code keeps a one-line gist tagged `[h:TunedTests-N]`.

## [h:TunedTests-1]
near: `[TestFixture, Category("csharp-only"), TunedOn(true)]`

/// <summary>
/// The owner's playtest notes of 2026-10-13, as behaviour (C# only, Balance.Tuned): the Brunch wall
/// forms as you walk in and rushes you when you are close; the Beam takes 2-3 bolts a Brunch; the
/// Warden's statues act; a room drops at most one thing, from its last kill, about 3 rooms in 4;
/// a Lunger dodges a shot it can see coming, except inside the Scatter's reach, and is never a free
/// kill on room entry. Each test also shows the untuned (JS) behaviour it changes, so it cannot pass
/// by testing nothing.
/// </summary>
