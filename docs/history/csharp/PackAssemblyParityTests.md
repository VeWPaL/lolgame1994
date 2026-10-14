# PackAssemblyParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/PackAssemblyParityTests.cs`. The code keeps a one-line gist tagged `[h:PackAssemblyParityTests-N]`.

## [h:PackAssemblyParityTests-1]
near: `[TestFixture]`

/// <summary>
/// Parity for <c>TickOrder.AssemblePacks</c> - the first third of the body phase, and the part
/// that decides what a Brunch IS rather than how it moves.
///
/// <para>
/// Every expectation was measured out of the running game by <c>tools/pack-parity.js</c>. The
/// Brunch work this session produced - the bomb-rush, the shield, the commitment - lived as prose
/// and tuning numbers until this file pinned it as behaviour.
/// </para>
///
/// <para>
/// <b>The property worth most here is the last one.</b> A pack COMMITS to its target: the scan
/// condition is <c>!shieldTarget &amp;&amp; (frameCount % BrunchScanTicks === 0)</c>, and the
/// <c>!shieldTarget</c> guard means a pack with a living target never re-picks. Measured in the
/// browser: a pack guarding a distant gunner did not switch to a shooter standing 20px from its
/// centroid across 400 ticks. My probe expected a switch and got none, and the game was right -
/// the probe was wrong about what the guard does.
/// </para>
///
/// <para>
/// That is a deliberate design property rather than an oversight. Re-deciding every 20 ticks would
/// make a Brunch's escort visibly indecisive in a room with several ranged bodies, and it would
/// let the player pull a pack off its charge by walking a rival into range - which turns a threat
/// into a switch.
/// </para>
/// </summary>

## [h:PackAssemblyParityTests-2]
near: `private static List<Enemy> Pack(RunState run, int n, double cx, double cy, int packId = 1)`

/// <summary>
/// A ring of Brunch around a point, as the generator makes them. Positions do not matter to
/// the pack pass - it reads only the centroid - but they are spread so that a test which
/// accidentally measures a body's own position rather than the centroid's would notice.
/// </summary>

## [h:PackAssemblyParityTests-3]
near: `[Test]`

/// <summary>
/// A pack with nothing to shield has NO target. Measured: a six-body pack in a room with no
/// ranged body came out of the assembly pass with every <c>shieldTarget</c> null - which is
/// what makes it walk at the player instead of assembling a wall.
/// </summary>

## [h:PackAssemblyParityTests-4]
near: `[Test]`

/// <summary>
/// A pack with a living target guards it, and the guarded body carries the WHOLE pack in its
/// guard list. Measured: 6 of 6 guarding, and a guard list of 6.
/// </summary>

## [h:PackAssemblyParityTests-5]
near: `[Test]`

/// <summary>
/// The target dies, the pack stops guarding the corpse, and the bodies move. Measured: 6 of 6
/// guarding before, 0 of 6 after, and the pack had moved more than 2px in 40 ticks.
/// </summary>

## [h:PackAssemblyParityTests-6]
near: `[Test]`

/// <summary>
/// A pack that has NO target re-picks on the scan cadence, and picks the NEAREST ranged body.
/// Measured: with the targets cleared and a shooter 20px from the centroid and a gunner 260px
/// away, all six switched to the shooter on the very first tick.
/// </summary>

## [h:PackAssemblyParityTests-7]
near: `[Test]`

/// <summary>
/// <b>AND IT COMMITS.</b> A pack with a living target does NOT re-pick, however near a rival
/// walks in. Measured: a pack guarding a gunner 260px away did not switch to a shooter 20px
/// from its centroid across 400 frames.
/// <para>
/// This is the assertion that would catch a "helpful" future change to the scan condition.
/// Removing the <c>!shieldTarget</c> guard makes a Brunch's escort indecisive in a room with
/// several ranged bodies, and lets a player pull a pack off its charge by walking a rival into
/// range.
/// </para>
/// </summary>

## [h:PackAssemblyParityTests-8]
near: `[Test]`

/// <summary>
/// A target that is no longer in the room counts as gone. The staleness test checks
/// membership, not just health, because a target removed by the room's own bookkeeping is
/// alive and absent - and a pack that kept guarding it would hold a wall around nothing.
/// </summary>

## [h:PackAssemblyParityTests-9]
near: `[Test]`

/// <summary>
/// Two packs in one room keep SEPARATE centroids and separate targets. Measured as a
/// structural property rather than a number: a shared dictionary keyed by pack id means one
/// pack's centroid cannot leak into another's.
/// </summary>

## [h:PackAssemblyParityTests-10]
near: `[Test]`

/// <summary>
/// A pack of one is below <c>BrunchShieldMin</c>, so it has no business forming an arc - which
/// is checked in the movement pass rather than here. What this asserts is the threshold
/// itself, because it is the line between "shield" and "bomb rush" and both halves of the
/// Brunch design hang off it.
/// </summary>

## [h:PackAssemblyParityTests-11]
near: `[TestCase(1, 0.632612)]`

/// <summary>
/// THE BRUNCH RAMP, MEASURED TICK BY TICK, AND IT IS NOT THE OBVIOUS FORMULA.
///
/// <para>
/// A Brunch that has noticed the player climbs from its walk speed to its run speed. The
/// shape of that climb had been asserted in this project as
/// <c>curSpeed = walk * (1 + RAMP_GAIN * min(1, pursuit/RAMP))</c>, which caps at 1.8096.
/// <b>That formula is wrong.</b> Measured in the running game with a Brunch in a corner so it
/// never reaches the player, the curve passes 1.8096 and keeps going, approaching 2.1.
/// </para>
///
/// <para>
/// The real line is <c>curSpeed += (runSpeed - curSpeed) * LungerAccel * gain</c> - an
/// EXPONENTIAL EASE toward the run speed, with the pursuit ramp scaling how hard each tick
/// pulls. Two consequences that the "obvious" formula gets wrong:
/// </para>
/// <list type="bullet">
/// <item>It never arrives. At 600 ticks it is 2.099676, not 2.1 - so a pack is always very
/// slightly short of its run speed, and the shortfall shrinks forever.</item>
/// <item>It does not depend on the walk speed at all except as the starting value. Changing
/// the walk speed moves the whole curve; changing the ramp gain changes how fast it gets
/// there.</item>
/// </list>
///
/// <para>
/// Simulating that line reproduces all ten measured points to six decimal places, with zero
/// difference - which is the strongest statement available without a full port, and it is why
/// the numbers below are worth pinning.
/// </para>
/// </summary>

## [h:PackAssemblyParityTests-12]
near: `[Test]`

/// <summary>
/// The ramp approaches the run speed and never reaches it - so the "obvious formula" reading is
/// excluded by the data rather than by argument. <c>walk * (1 + RAMP_GAIN)</c> is 1.8096.
/// </summary>

## [h:PackAssemblyParityTests-13]
near: `[Test]`

/// <summary>
/// The ramp is faster against a fleeing player than a momentum-full one, which is the whole
/// reason the number is 2.1: it has to beat 1.6045 or the bomb-rush is scenery.
/// </summary>
