# Bootstrap.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Editor/Bootstrap.cs`. The code keeps a one-line gist tagged `[h:Bootstrap-N]`.

## [h:Bootstrap-1]
near: `public static class Bootstrap`

/// <summary>
/// Builds the generated half of the project from scratch: the mixer, the panel settings, both
/// scenes, build and player settings. Idempotent; run from the menu or in batch mode:
/// Unity -batchmode -projectPath unity -executeMethod Depths.Unity.EditorTools.Bootstrap.Run -quit
/// Scenes and assets are generated rather than hand-edited so they can be rebuilt and diffed.
/// </summary>

## [h:Bootstrap-2]
near: `static AudioMixer EnsureMixer()`

/* THE MIXER. Unity has no public API to create a mixer or its groups, so this uses the
   internal AudioMixerController the "Create > Audio Mixer" menu uses. If a Unity update moves
   it, this returns null, the menu falls back to AudioListener.volume for Master, and the log
   says so - the game never depends on the reflection succeeding. */
