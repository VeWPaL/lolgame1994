# Eggs.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Eggs.cs`. The code keeps a one-line gist tagged `[h:Eggs-N]`.

## [h:Eggs-1]
near: `public enum EggForm { Fixed, Orbit, Rail }`

/// <summary>
/// The turret egg's three forms (C# only, owner 2026-10-13). <see cref="EggForm.Fixed"/> sits where it
/// spawned (the lab egg). <see cref="EggForm.Orbit"/> circles another body and lets a smaller, slower ring
/// go: a turret that comes to you with its escort. <see cref="EggForm.Rail"/> rides a track laid through
/// the room, back and forth or round a loop, firing as it goes; it only appears in the Kiln Works, where
/// rails belong. Every ring is turned to keep its shells off the egg's own side (<see cref="Eggs.Ring"/>).
/// </summary>

## [h:Eggs-2]
near: `public static (double[] pts, bool loop) Track(int pattern, bool flipX, bool flipY)`

/// <summary>
/// A track through the room as points (x0, y0, x1, y1, ...), closed (a loop the egg circles) or open (a line
/// it rides back and forth), mirrored as asked so each pattern has four faces. Inside the room by
/// <see cref="RailInset"/> on every side. Allocates: called once per egg, at spawn.
/// </summary>
