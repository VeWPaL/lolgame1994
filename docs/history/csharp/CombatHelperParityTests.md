# CombatHelperParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/CombatHelperParityTests.cs`. The code keeps a one-line gist tagged `[h:CombatHelperParityTests-N]`.

## [h:CombatHelperParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The four helpers the projectile pass calls: <c>falloffMult</c>, <c>alertEnemy</c>,
/// <c>slowEnemy</c> and <c>killEnemy</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value here was READ OUT OF THE RUNNING JAVASCRIPT, never computed. That is the
/// only legitimate way to acquire a parity value, and it is not a formality: hand-computing one
/// produced three wrong numbers out of nine once already, and the test was what failed each time.
/// The read was done by evaluating the real functions in a browser at TICK_HZ=210.
/// </para>
/// <para>
/// The degenerate cases are here because they are where a transcription breaks. A zero-width falloff
/// band and a NEGATIVE one both have to land on the floor rather than dividing by zero or inverting
/// the ramp into a damage bonus — and the game's answer to both is the same number, which is not
/// obvious from reading <c>Math.max(1, fFar-fNear)</c> and is obvious from measuring it.
/// </para>
/// </remarks>

## [h:CombatHelperParityTests-2]
near: `Assert.Multiple(() =>`

// Both of these are the same shape of mistake: `fFar <= fNear` makes the denominator zero
// or negative, and a naive transcription produces a division by zero, a NaN, or a ramp that
// goes UP with distance — which would be a damage bonus for shooting from further away.
