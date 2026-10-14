# TickOrderTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/TickOrderTests.cs`. The code keeps a one-line gist tagged `[h:TickOrderTests-N]`.

## [h:TickOrderTests-1]
near: `[TestFixture]`

/// <summary>
/// The tick skeleton: gates, clocks, the order gate, and RNG discipline.
///
/// <para>
/// These pin the shape of <see cref="TickOrder.Update"/> rather than the fight. Where the
/// passes are still stubs, the observable is the recorded phase log and the stream counts -
/// which is deliberate: a swap of two passes, and an extra <c>Rnd.run</c> draw, must each be
/// caught by a failing assertion and not merely change numbers nobody reads.
/// </para>
/// </summary>

## [h:TickOrderTests-2]
near: `[Test]`

/// <summary>
/// The pass order, and WHY IT IS THIS ORDER - re-measured from the running game on
/// 2026-10-04 rather than taken from the comment that states it.
/// <para>
/// The order is player, projectiles, bodies, room. The reason it matters is not tidiness:
/// on a tick where the player walks through a door, <c>trans</c> is set inside the player
/// pass and the tick returns immediately - so the projectile and body passes do not run at
/// all on that tick. Measured, in the live game, with a body and a projectile placed in the
/// room and the transition forced exactly as a player opening a door would set it:
/// <para>
///     body moved       0.00
///     projectile moved 0.00
/// <para>
/// So the shell a player would have dodged does not advance on the tick they leave through
/// the door, and a body does not step. Port the passes in any other order and that shell
/// moves on a tick it must not - which is a different fight from the game's in every room
/// containing both a shooter and a body.
/// </para>
/// </summary>

## [h:TickOrderTests-3]
near: `var run = PlayingRun();`

// Several bugs in update() were ORDER bugs - a hit landing after the body it hit had
// already moved. Until the passes carry real state, the recorded sequence is the pin;
// a swap of two of them must fail this, not change a number nobody reads.
//
// AND THIS ORDER WAS MEASURED, because it was wrong here first. The test asserted
// projectiles, bodies, player, room and named itself after that. The game runs PLAYER
// FIRST: the integration is at src/60-tick.js:432-517 and the projectile loop does not
// begin until 594. Found by putting a probe inside update() on each pass and reading the
// sequence out of one live tick.
//
// Reading the source does not settle it, because the four passes are not four named blocks
// - `update()` is one function with the loops inline, so "projectiles then bodies" looks
// true to anyone scanning for the loops in order and is only true from line 594 onward.
// The player pass is above them all.
//
// It is load-bearing twice over, which is why a wrong order here is not a cosmetic label:
//   - the player pass can set `trans` and src/60-tick.js:517 is `if(trans) return;`, a
//     WHOLE-TICK abort, so on a tick the player walks through a door the other three passes
//     must not run at all;
//   - a gunner steps after the player has moved, so its aim is solved against where the
//     player IS rather than where they were.

## [h:TickOrderTests-4]
near: `[Test]`

/// <summary>
/// Momentum charges on movement UNDER PRESSURE and bleeds away when stalled.
/// <para>
/// The "under pressure" part is load-bearing and this test used to leave it out. The original
/// returns from <c>tickMomentum</c> immediately when the room has no bodies - the meter is a
/// reward for being chased, not for walking - and this port had lost that guard. It was found
/// by porting the player phase and comparing its acceleration curve against the game's, which
/// disagreed in the fourth decimal place: the C# player was quietly charging momentum in an
/// empty room, which is a free speed bonus with no counterweight.
/// </para>
/// <para>
/// The room is populated below, and the empty-room case is asserted separately, because the
/// guard is the whole point of the test.
/// </para>
/// </summary>

## [h:TickOrderTests-5]
near: `[Test]`

/// <summary>
/// No bodies in the room means no momentum at all, however far the player walks. Measured in
/// the running game: 400 ticks running east in an empty room leave the meter at exactly 0,
/// and the terminal speed is 1.4025 rather than something higher.
/// </summary>

## [h:TickOrderTests-6]
near: `var run = PlayingRun();`

// The gate itself is bookkeeping: no boss firing, no committed shot, no draw. An extra
// draw anywhere in Update would shift every later seed-driven decision, and that is
// precisely the failure this test fails loudly.
