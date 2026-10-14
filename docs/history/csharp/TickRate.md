# TickRate.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/TickRate.cs`. The code keeps a one-line gist tagged `[h:TickRate-N]`.

## [h:TickRate-1]
near: `[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]`

/// <summary>
/// Runs a test (or every test under it) at a tick rate, and restores the rate after, so a rate
/// set by one test never leaks into the next. Inner attributes run after outer ones, so they win.
/// </summary>
