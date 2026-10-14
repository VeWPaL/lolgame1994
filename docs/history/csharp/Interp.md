# Interp.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Interp.cs`. The code keeps a one-line gist tagged `[h:Interp-N]`.

## [h:Interp-1]
near: `public static class Interp`

/// <summary>
/// Drawing between ticks (C# only). The simulation steps at a fixed <c>Balance.TickHz</c>, the screen at
/// whatever the monitor or the frame cap gives, so a frame lands between two ticks. Drawn at the latest
/// tick, motion judders (a 144 Hz screen shows each tick for 2 or 3 frames); drawn at the blend of the last
/// two, it is smooth at any frame rate for one tick of display delay.
///
/// <para>The presenter calls <see cref="Snap"/> before every tick, then draws at <c>Lerp(prev, now, alpha)</c>
/// with alpha the clock's leftover fraction of a tick. Nothing in the simulation reads these fields, so
/// the game plays the same with or without them (the bot never calls Snap).</para>
/// </summary>
