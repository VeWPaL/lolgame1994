# ScreenshotMode.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/ScreenshotMode.cs`. The code keeps a one-line gist tagged `[h:ScreenshotMode-N]`.

## [h:ScreenshotMode-1]
near: `public sealed class ScreenshotMode : MonoBehaviour`

/// <summary>
/// A built player started with <c>-depthsShot path.png [-depthsView menu|controls|audio|gameplay|game|hearts|boss|sandbox|summary|pause|banner|warning|lowhp|ledger|lab|labrow|secret|rail] [-depthsSeed n]</c>
/// opens that view, captures one frame and quits. It is how the game is checked by eye from a script.
/// <c>game</c> also measures the tick rate in real time and <c>hearts</c> samples the health row's
/// pixels; each logs a "[Depths] check" line, and the player exits 1 if a check fails.
/// </summary>

## [h:ScreenshotMode-2]
near: `IEnumerator SecretView()`

// A full page: a fixture with every line of both ledgers written (keys, mail, a held item and
// passives, a part-spent blink, momentum, a charted floor). The harness sets these by hand.
// The fake wall: the frame shows its crack, then the blast opens it both ways and the room behind holds the hook.
