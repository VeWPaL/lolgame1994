# AllocMeter.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Tests/EditMode/AllocMeter.cs`. The code keeps a one-line gist tagged `[h:AllocMeter-N]`.

## [h:AllocMeter-1]
near: `public static class AllocMeter`

/// <summary>
/// "Allocates nothing", checked the way Unity can check it: the Test Framework's AllocatingGCMemory
/// constraint, which records the profiler's GC.Alloc samples. GC.GetAllocatedBytesForCurrentThread
/// returns 0 under Unity's Mono whatever is allocated (a 1 MB array read as 0, 2026-10-13), and
/// GC.GetTotalMemory and Profiler.GetMonoUsedSizeLong miss small allocations, so tests that used them
/// passed on nothing. The self-tests below keep this one honest.
/// </summary>
