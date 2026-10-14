# Tick.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Tick.cs`. The code keeps a one-line gist tagged `[h:Tick-N]`.

## [h:Tick-1]
near: `public readonly struct Input`

/// <summary>
/// The input one tick reads. The original accumulates <c>keys</c> at module scope; this is the
/// engine-independent line, so the tick takes it as an argument and never touches the DOM.
/// </summary>

## [h:Tick-2]
near: `public static class TickOrder`

/// <summary>
/// The tick skeleton, in the original's load-bearing order. "The one function ... where the
/// rules interleave" (src/60-tick.js:1-11). The full ~870-line body is out of scope for this
/// pass; what lands here, and must not regress, is:
///
///  * the state gate and the two health checks that bracket everything,
///  * the run and floor clocks, which advance on every tick that passes the gate - including
///    transition and ready ticks, because neither branch ticks gameplay but both tick time,
///  * the trans / ready / fade early returns, with the fade bookkeeping placed above them for
///    the same reason it is in the original: presentation that must keep running through
///    states where the simulation has stopped lives above the returns,
///  * the order gate itself - the PLAYER first, then projectiles, then bodies, then the room -
///    recorded in <see cref="RunState.phaseLog"/> until the passes carry real state. The order was
///    wrong here (it said projectiles first) until the game's was measured from a live tick; see
///    the comment on the gate below, because the player-first order is load-bearing twice over.
///
/// <para>
/// RNG discipline is the same split the original's suite insists on: the boss move pick is a
/// <c>run</c> draw (it decides what the run is), every cooldown and spread roll is a
/// <c>jitter</c> draw (behaviour noise), and nothing in the update path touches <c>art</c>.
/// An <see cref="RunState"/> that never fires a move still counts what it spends, which is how
/// a test proves an extra <c>run</c> draw cannot hide inside a stub.
/// </para>
/// </summary>

## [h:Tick-3]
near: `public static void TickMomentum(RunState run, double moved)`

/// <summary>
/// Charges Momentum on movement under pressure, src/60-tick.js:37-44. Runs no RNG and
/// reads only the player; the empty-room guard awaits the per-room enemy list, which
/// belongs to the room port and is noted as remaining work.
/// </summary>

## [h:Tick-4]
near: `if (run.enemies.Count == 0) return;`

/* NO MOMENTUM IN AN EMPTY ROOM, and this guard was MISSING until the player phase was
   ported and its acceleration curve disagreed with the game's by four decimal places.

   The original returns immediately when the current room has no bodies. Momentum is a
   PRESSURE mechanic - it charges from being chased and shot at, and it exists to make
   that survivable. Charging it in an empty room inverts the design: a player walking
   alone becomes faster the longer they walk, which is a free speed bonus with no
   counterweight.

   How it hid: nothing in the port called `TickMomentum` until `TickPlayer` landed, and
   `TickPlayer` reads momentum in the SAME tick, so the error was invisible until there
   was a caller and a measurement to compare against. The symptom was an acceleration
   curve that read 0.433763 where the game reads 0.433643 - a momentum of 0.0017, which
   is invisible in isolation and obvious as a curve.

   Measured, after the fix: a player running east in an empty room for 400 ticks has
   momentum 0, and the terminal speed is 1.4025. With bodies present the same 200 ticks
   reaches 1. */

## [h:Tick-5]
near: `public static void FireCommittedShot(RunState run, Enemy e, double roomPress)`

/// <summary>
/// A cast that has finished LEAVES, src/60-tick.js:28-35. The cooldown roll is a jitter
/// draw - behaviour noise that must never reach into the run stream - and the pressure
/// term shortens it in a crowded room.
/// </summary>

## [h:Tick-6]
near: `public static void StepBoss(RunState run, Enemy e)`

/// <summary>
/// The Warden's turn, src/60-tick.js stepBoss: the move clock, the statue sweep by age, the
/// wall's own expiry, the phase gates, then either resolving the committed move or, once idle
/// and off cooldown, one run draw for the move from a bag weighted by phase and one jitter draw
/// for the next cooldown. The boss does not walk; it moves by its sweep and by being knocked.
/// </summary>

## [h:Tick-7]
near: `public static void Update(RunState run, Input input)`

/// <summary>
/// The update() gate, src/60-tick.js:256-340. Only the load-bearing skeleton for now:
/// gates, clocks, the early returns, and the order gate.
/// </summary>

## [h:Tick-8]
near: `if (run.player.hp <= 0) { run.state = "gameover"; return; }`

// THE DEATH BACKSTOP. Checked on the way in, before any early return below can fire,
// and the lab is protected one level down in the original at damagePlayer. A zero-hp
// player must not keep playing out a transition.

## [h:Tick-9]
near: `run.phaseLog.Clear();`

/* THE ORDER GATE, AND THE ORDER WAS WRONG.

   This logged projectiles, bodies, player, room. The game runs PLAYER, projectiles, bodies,
   room - measured, not read, by putting a probe inside `update()` on each pass and
   recording the sequence from one live tick:

       player -> projectiles -> bodies -> room

   The player integration is at src/60-tick.js:432-517 and the projectile loop does not
   begin until 594. So the player is integrated, walks, can open a door, and only THEN are
   shells advanced and bodies stepped.

   WHY IT MATTERS, and it is not cosmetic ordering. Two of the four passes read state the
   player pass writes on the same tick:

     - `checkDoorTransition` inside the player pass can set `trans`, and src/60-tick.js:517
       is `if(trans) return;` - a WHOLE-TICK abort. On a tick where the player walks through
       a door, projectiles and bodies do not run at all. Run the projectile pass first and
       that shell advances on a tick it must not, in the tick the player leaves the room.

     - The gunner's aim is solved against the player's position, and the swerve meter is
       computed from the player's TARGET velocity BEFORE integration (the comment at
       src/60-tick.js:466 says so). A gunner that steps before the player has moved leads
       the position the player is about to be at, not the one they were at.

   So this was not a stale label on a stub. When the passes carry real state, this order
   would make the port play a different fight from the game in every room containing a
   shell and a body. Each pass is still a stub that records it ran; what changed is the
   order they are recorded in, which is the thing the passes will have to honour. */

## [h:Tick-10]
near: `TickProjectiles(run);`

/* THE ROOM PHASE RUNS FOR REAL. The other three still only record that they ran.
   `TickRoom` returns true when the player reached the exit portal, which is the
   original's mid-function `return` - and that return SKIPS everything after the pickup
   loop, so it has to be honoured here or a descent would also re-open the boss exit on
   the same tick. */

## [h:Tick-11]
near: `static readonly List<Enemy> Snapshot = new List<Enemy>();`

/// <summary>
/// The body phase, src/60-tick.js tickBodies, for the LUNGER: knock drift, slow and stun clocks,
/// notice, aggro (AGGRO_RANGE from the lagged hitbox), stepLunge or the idle wander, the wall,
/// contact damage with its knock-back, then separation and the wall again. Pack assembly runs
/// first, as in the game. Any other kind that would act throws: their movement is not ported.
/// </summary>

## [h:Tick-12]
near: `static void StepBrunch(RunState run, Enemy e, double edx, double edy, double sm)`

/// <summary>
/// A Brunch on the hunt (src/60-tick.js tickBodies, the Brunch branch): the speed ramp, then a
/// slot in the wall before its pack's target (or the player when it has none, or when the slot
/// is already reached), at the guard or chase speed, easing its velocity a fraction per tick.
/// </summary>

## [h:Tick-13]
near: `static void StepRanged(RunState run, Enemy e, double edx, double edy, double dist, double sm, double roomPress)`

/// <summary>
/// A shooter or gunner in sight of the player: hold a standoff that shrinks as the room empties
/// (longer while guarded), walk (a gunner roots while casting), then cast at an intercept - the
/// player's CURRENT velocity trusted by how settled they look, solved from where the muzzle WILL be
/// for a walking shooter - with a swerve-widened jitter, swept clear of our own bodies.
/// </summary>

## [h:Tick-14]
near: `public static bool TryBlink(RunState run, Input input)`

/// <summary>
/// The blink key (keydown in the game, so it lands BETWEEN ticks): only in a live room with a
/// charge, no pause, no transition and no ready freeze. Returns whether it blinked.
/// </summary>

## [h:Tick-15]
near: `public static void DoBlink(RunState run, Input input)`

/// <summary>
/// doBlink: BLINK_DIST along the held keys (else the current velocity, else the aim), clamped;
/// the enemies' hitbox stays where the player WAS; i-frames for the whole travel, a one-hit
/// grace, and a burst of speed in the blink's direction.
/// </summary>

## [h:Tick-16]
near: `public static void TickProjectiles(RunState run)`

/// <summary>
/// The projectile phase, src/60-tick.js tickProjectiles. Newest first, so a removal never skips
/// one. Each shell moves, ages, and is culled 30px outside the room; then a friendly shell hits
/// at most one body (for a piercing bolt, the first along its flight), and an enemy shell is eaten
/// by a Brunch (unless it guards the shooter), else blocked by a lunger, else lands on the
/// player's lagged hitbox. A landing shell does NOT stop the loop for the older ones - the game
/// had that bug until 2026-10-06. Alt shells throw until explode is ported. The sound and FX the
/// game makes here are the view's, not the simulation's.
/// </summary>

## [h:Tick-17]
near: `public static bool TickRoom(RunState run)`

/// <summary>
/// The room phase, ported. This is the first of the four passes to carry real behaviour, and
/// it was chosen because it is the one whose dependencies are all already here: pickups, the
/// player, the room's own flags, and five constants that <c>Balance</c> already had.
///
/// <para>
/// <b>What is ported and what is not.</b> The room-cleared bookkeeping, both reward drops,
/// the whole pickup-touch loop, the weapon swap, the heart and armour top-ups, both keys, the
/// hook and blast alt-modes, and the boss-death exit are all here. The <c>item</c> branch is
/// NOT: it calls <c>Items.give</c>, which has no C# counterpart yet, and a stub that quietly
/// collected nothing would be a port that looks done and plays differently. It throws
/// throws instead, so the gap is a crash rather than a
/// behaviour change - which is the right way for a parity port to be incomplete.
/// </para>
///
/// <para>
/// <b>Two behaviours that are easy to lose and are kept deliberately.</b>
/// </para>
/// <list type="number">
/// <item>The <c>hold</c> flag. A pickup taken this tick is parked where it stood and ignored
/// until the player steps off it. That is what lets the weapon swap put the old gun on the
/// floor at the player's feet - it would otherwise be re-collected on the same tick, in a
/// loop, and the swap would never settle. The check order matters too: <c>hold</c> is
/// cleared BEFORE the touch test, not after, so stepping off and back on re-collects.</item>
/// <item>The exit portal's smaller radius. Every other pickup is collected at
/// <c>pk.r + player.r</c>; the exit is collected at the bare <c>PLAYER_HIT_R</c>. Walking
/// into the way down is a deliberate act and should not be something you do by brushing past
/// the edge of a 30px circle, so the test is tighter on purpose.</item>
/// </list>
///
/// <para>
/// Returns true when the phase reached the exit portal and the run descended, which is the
/// original's <c>return</c> out of the middle of the function - everything after the pickup
/// loop is skipped that tick. The death check immediately after the loop is deliberately
/// NOT skipped in that case, because it is outside the loop in the original too: reaching
/// the exit and dying on the same tick ends the run as a death, and the boss-death check
/// beneath it does not run.
/// </para>
/// </summary>

## [h:Tick-18]
near: `var room = run.CurrentRoom;`

// The current room. The C# model keeps the CURRENT room's bodies on `RunState.enemies`
// rather than on `Room`, because the body list is rebuilt per room and belongs to the
// tick's working set; `Room` carries the durable facts (type, doors, flags). That is a
// different shape from the JavaScript, where one object holds both, and it is why this
// line reads `run.enemies` where the original reads `r.enemies` - same list, different
// owner.

## [h:Tick-19]
near: `if (room == null) return false;`

// The generator cannot produce a cursor on an empty cell, so this is unreachable in a
// well-formed run. It is handled rather than asserted because a null here would
// otherwise be a NullReferenceException from inside the tick, which is a crash the
// player sees rather than a diagnosable failure.

## [h:Tick-20]
near: `if (pk.hold) { if (!touching) pk.hold = false; continue; }`

// `hold` clears BEFORE the touch test, so stepping off a held pickup and back on
// collects it again - and a held pickup the player is still standing on is not
// collected a second time.

## [h:Tick-21]
near: `if (descended) return true;`

/* THE ORIGINAL RETURNS OUT OF THE MIDDLE OF THE FUNCTION HERE, and that has to be
   honoured. `descend()` already reseeded, moved the player and rebuilt the dungeon, so
   running the death check and the boss check against the NEW floor would be checking a
   floor the player never saw - and re-opening an exit on it is a second, invisible bug.

   The only thing after the loop that is still correct to skip is the whole tail, so this
   returns rather than falling through. It returns true: the caller wants to know whether it
   descended. */

## [h:Tick-22]
near: `room.ExitOpen = true;`

// The way down opens when the boss dies and is walked into, rather than the run
// ending on the kill. The one-shot flag is what stops a new portal appearing on
// every tick the player stands in an empty boss room.

## [h:Tick-23]
near: `public static double HitboxX, HitboxY;`

/// <summary>
/// The player's lagged hitbox, published once per tick by <see cref="TickPlayer"/> and read by
/// the gunner intercept in the body phase.
/// <para>
/// This is the only value one phase computes and a later phase reads, and it is module state
/// rather than a local or a parameter because it is the same channel the C# side has to
/// reproduce. The gunners solve against THIS point rather than the player's real position, so
/// they lead toward where the player is visually leaving - which is the entire reason the
/// intercept reads as leading rather than chasing.
/// </para>
/// </summary>

## [h:Tick-24]
near: `public static bool TickPlayer(RunState run, Input input,`

/// <summary>
/// The player phase: input, acceleration, movement, the wall clamp, the lagged hitbox, and the
/// cooldowns. Ported from <c>tickPlayer</c> in <c>src/60-tick.js</c>.
///
/// <para>
/// <b>Ported:</b> the whole movement integration, the acceleration curve, the directional
/// estimate the lungers aim at, the swerve meter, the wall clamp with its door gaps, the
/// lagged hitbox, and every cooldown decrement. <b>Not ported:</b> the boss warning, the
/// blink, the firing, the on-use field effects and the hook resistance - each of those is a
/// helper with its own dependencies, and a stub that silently did nothing would be a phase
/// that looks done and plays differently.
/// </para>
///
/// <para>
/// <b>The return value is load-bearing.</b> False means a door transition started and the rest
/// of the tick must not run, which is the original's <c>if(trans) return;</c> - a WHOLE-TICK
/// abort, and the reason the four phases are ordered player-first. Measured live in
/// <c>TickOrderTests</c>: on a transition tick the body and the projectile both move 0.00.
/// </para>
///
/// <para>
/// <b>The door gap is the fiddly part and is preserved exactly.</b> A player against a wall
/// only has their velocity zeroed if they are NOT in a doorway's gap: the test is
/// <c>abs(player.x - MidX) &lt; DoorWidth/2</c> for a north or south wall and the transpose
/// for east and west. Without it a player standing in a doorway stops dead and cannot leave,
/// which is a room you cannot exit.
/// </para>
/// </summary>

## [h:Tick-25]
near: `double bonus = System.Math.Min(Balance.MomCap, System.Math.Min(Balance.MoveSpeedHardCap, moveSpeedBonus ?? run.stats.Val`

// moveSpeedBonus() in the game: the speed STAT plus the Momentum meter's share, capped. The
// momentum term was missing here, invisible to every test that ran in an empty room (where
// momentum stays 0) until the blink parity walked a player past a body.
// the stat is held to the JS cap and only momentum may go past it (untuned the two caps are one, and this is the JS)

## [h:Tick-26]
near: `double accel = Balance.Ease(Balance.MoveAccel * (1 + p.momentum * Balance.MomAccel));`

/* THE ACCELERATION RAMP IS THE FEEL, and it is why this is not a velocity assignment.
   `MoveAccel` closes 11.6% of the remaining gap per JS tick (Ease converts it), so the player takes about 40
   ticks - about a fifth of a second - to reach top speed, and Momentum makes a charged
   player accelerate harder rather than merely faster. Measured in the browser: the
   terminal speed is 1.4025 and the first ten ticks are
   0.16269, 0.306508, 0.433643, 0.54603, 0.645381, 0.733207, 0.810845, 0.879477,
   0.940147, 0.99378. */

## [h:Tick-27]
near: `p.trendVx += (p.vx - p.trendVx) * Balance.LungeTrack;`

// The direction-of-travel estimate, for the lungers. It is smoothed more gently than the
// velocity itself (LungeTrack) and is renormalised, so it is a DIRECTION and not a
// magnitude that drifts.

## [h:Tick-28]
near: `double bsp = System.Math.Sqrt(p.vx * p.vx + p.vy * p.vy);`

/* The second, separate estimate - the player's BELIEF about their own direction, used by
   the gunners. It is a different number from `trendVx` on purpose: the gunners read a
   slower, noisier estimate than the lungers do, which is why a counterstrafe works
   against one and not the other. */

## [h:Tick-29]
near: `double msp = System.Math.Sqrt(targetVx * targetVx + targetVy * targetVy);`

/* THE SWERVE METER. It rises when the player's new direction disagrees with the one they
   were already committed to, and decays every tick. The gunners read it to decide how
   much to trust their intercept, so it is the mechanic that makes movement a defence.

   The decay is per-tick and absolute, not multiplicative: a full meter falls to 0.5765 in
   one second, which is 0.0035 a tick. That number was WRONG in this port for a while -
   see the note on `Balance.SwerveDecay` - and nothing caught it, because nothing read
   the constant until now. */

## [h:Tick-30]
near: `var room = run.CurrentRoom;`

/* THE WALL CLAMP, AND WHY IT ZEROES VELOCITY RATHER THAN JUST THE POSITION.

   `ClampPlayer` above has already stopped the position. This zeroes the velocity
   component pointing into a wall - but ONLY outside a doorway's gap. Both halves of that
   sentence are load-bearing:

   - Zeroing velocity unconditionally would stop a player dead in a doorway, and a room
     you cannot leave is a bug rather than a wall.
   - Not zeroing it at all would leave the player reporting full speed forever while
     covering no pixels, which the Momentum meter and the gunners both used to read.
     That is solved elsewhere by measuring displacement, but the velocity itself should
     still not lie.

   Measured, against a wall whose door is genuinely CLOSED: the player stops at exactly
   `RoomLeft + r` = 63 with `vx` = 0. Against the start room's west side the door is open,
   so no clamp applies and the player walks through - which is correct, and is why a
   probe that measures "the wall" has to check the door first. */

## [h:Tick-31]
near: `double moved = System.Math.Sqrt((p.x - wasX) * (p.x - wasX) + (p.y - wasY) * (p.y - wasY));`

/* MOMENTUM IS EARNED FROM DISPLACEMENT, NOT FROM VELOCITY, and the knockback is
   subtracted first. Both details are the difference between a meter that charges when
   you move and one that charges when a Brunch hits you - which, given that the meter
   exists to make pressure survivable, is the difference between the mechanic working and
   being inverted.

   Measured: a player pinned against a closed wall for 200 ticks has momentum 0, and the
   same 200 ticks in open floor with bodies in the room reaches 1. */

## [h:Tick-32]
near: `p.lagX += (p.x - p.lagX) * Balance.HitboxLagEase;`

/* THE LAGGED HITBOX, and the publish. This trails the real position and catches up, so
   after a blink the enemies aim at where the player was for a moment - which is the
   whole reason a blink reads as an escape rather than a teleport.

   Measured after 30 ticks running east: player 431.6515, hitbox 414.1123. The gap is
   real and deliberate; if it ever reads equal for a moving player, the lag is broken. */

## [h:Tick-33]
near: `internal static void ClampPlayer(RunState run) => Rooms.ClampPlayer(run);`

/// <summary>
/// Keeps the player inside the room. Position only - the velocity clamp, and its door-gap
/// exception, live in <see cref="TickPlayer"/> because they need the room and the mid-point.
/// </summary>

## [h:Tick-34]
near: `public static bool DoorPassable(RunState run, Room room, Dir d) => Rooms.DoorPassable(run, room, d);`

/// <summary>
/// Whether the player is standing in a doorway on side <paramref name="d"/> of the room.
/// <para>
/// A door is passable when it exists, is unlocked, and - for the two coloured doors - has
/// been opened. The original resolves this in <c>doorPassable</c>; this is the C# half, and
/// it is what lets the wall clamp exempt a player who is in a gap.
/// </para>
/// </summary>

## [h:Tick-35]
near: `public static bool CheckDoorTransition(RunState run) => Rooms.CheckDoorTransition(run);`

/// <summary>
/// Whether the player is standing in a doorway, and therefore whether the tick should start a
/// room transition.
/// <para>
/// The original's <c>checkDoorTransition</c>. Returns true when a transition has just begun,
/// which is the signal the player phase returns to the dispatcher and the reason the rest of
/// the tick does not run.
/// </para>
/// <para>
/// <b>NOT PORTED, and it says so.</b> The real function tests whether the player is inside a
/// door's gap AND the door is unlocked AND they are moving into it, and then starts a fade.
/// All three of those need the transition and unlock machinery, which lands with the world
/// port. What is here is the geometry half - which side of the room the player is on and
/// whether a door exists there - and it returns false rather than a guess, so a player at a
/// wall is never teleported into the next room by a stub.
/// </para>
/// </summary>

## [h:Tick-36]
near: `static readonly Dictionary<int, PackCentroid> Packs = new Dictionary<int, PackCentroid>();`

/// <summary>
/// A pack's centroid, computed once per tick and shared by every body in it.
/// <para>
/// The centroid is what makes a Brunch a SHIELD rather than a crowd: bodies do not steer at
/// the player, they steer at slots around a shared point, and the point is where the pack
/// actually is. Averaging positions rather than picking a leader also means a pack survives
/// losing any single member - there is no body whose death changes where the others go.
/// </para>
/// </summary>

## [h:Tick-37]
near: `public static void AssemblePacks(RunState run)`

/// <summary>
/// The pack assembly pass: centroids, target selection, and the guard lists. Ported from the
/// first three sections of <c>tickBodies</c> in <c>src/60-tick.js</c>.
///
/// <para>
/// This is the part of the body phase that decides what a Brunch IS rather than how it moves,
/// and it is where the session's Brunch work lives: the bomb-rush when there is nothing to
/// shield, the shield arc when there is, and the commitment that makes a pack predictable.
/// </para>
///
/// <para>
/// <b>Measured behaviour, all from the running game:</b>
/// </para>
/// <list type="bullet">
/// <item>a pack with no ranged body in the room has NO target, and walks at the player</item>
/// <item>a pack guarding a living body sets that body's guard list to the whole pack
/// (measured: 6 of 6)</item>
/// <item>when the guarded body dies the pack re-targets, and all 6 stop guarding the corpse</item>
/// <item><b>a pack COMMITS.</b> A pack that already has a living target never re-picks, even
/// with a nearer ranged body 20px away. The scan is <c>!shieldTarget &amp;&amp;
/// (frameCount % BrunchScanTicks === 0)</c>, and the guard means it. Measured: no switch in
/// 400 ticks with a shooter standing 20px from the pack centroid.</item>
/// </list>
/// </summary>

## [h:Tick-38]
near: `var packs = Packs;`

// --- centroids. One pass to sum, one to divide, which is what the original does and is
// cheaper than carrying a running mean through the second loop.
// reused every tick (pooled centroids), and kept for the body pass to read

## [h:Tick-39]
near: `bool stale = tgt == null || tgt.hp <= 0 || !run.enemies.Contains(tgt);`

/* STALE MEANS GONE, DEAD, OR NOT IN THIS ROOM. All three have to be checked: a target
   that died, a target that was spliced out by the room's own bookkeeping, and a target
   that was never here at all - `null` is the ordinary case on the first tick. */

## [h:Tick-40]
near: `foreach (var o in run.enemies)`

// --- guard lists, rebuilt from scratch every tick. They are a derived view: a body that
// died must not still appear to be escorted, and rebuilding is cheaper than proving that
// last tick's list is still correct.

## [h:Tick-41]
near: `private static Enemy? PickShield(List<Enemy> enemies, PackCentroid c)`

/// <summary>
/// The nearest living ranged body to a pack centroid, or null if there is none.
/// <para>
/// NEAREST, not best, and not first: the original walks every enemy and keeps the smallest
/// squared distance. Squared, not actual, so the comparison never takes a square root - and
/// it is squared rather than by straight-line distance for a second reason, which is that the
/// test the projectile phase uses is also squared, and the two agree about what "near" means.
/// </para>
/// <para>
/// A pack will therefore shield a shooter it is standing next to rather than a distant gunner,
/// which is the intended reading: the pack protects what is closest, because that is what it
/// can reach.
/// </para>
/// </summary>

## [h:Tick-42]
near: `public static bool IsRanged(BodyKind k) =>`

/// <summary>
/// The three body kinds that shoot from range, and therefore the three worth shielding. A
/// melee body is not shielded because nothing can reach it from outside the pack.
/// </summary>

## [h:Tick-43]
near: `public readonly struct Pt`

/// <summary>A point in the world, returned by the arc geometry. Public because the parity
/// tests assert against it - a geometry helper that cannot be measured is a geometry helper
/// nobody can port.</summary>

## [h:Tick-44]
near: `public static Pt? BrunchArcSlot(double tx, double ty, double px, double py,`

/// <summary>
/// Where slot <paramref name="slot"/> of an <paramref name="n"/>-body pack stands when
/// shielding a body at (<paramref name="tx"/>, <paramref name="ty"/>) against a player at
/// (<paramref name="px"/>, <paramref name="py"/>). Null when the pack is too small to have a
/// shape.
///
/// <para>
/// Ported from <c>brunchArcSlot</c> in <c>src/00-balance.js</c>, and it is the single function
/// that decides whether a Brunch is a shield or a crowd. Three things about it are not
/// obvious, and each was a bug:
/// </para>
///
/// <para>
/// <b>1. The stand-off is a FRACTION OF THE GAP, not a constant.</b> The arc has to fit
/// between the guarded body and the player, and that distance is not fixed - it is whatever the
/// fight has produced so far, and it gets small. A constant radius put the wall behind the
/// player: measured, a 164px gap with a 118px stand-off formed the wall 46px on the far side
/// of the person it was meant to protect, and line of sight was blocked 27-39% of the way
/// rather than closed. Now it is <see cref="Balance.BrunchShieldFrac"/> of the real gap.
/// </para>
///
/// <para>
/// <b>2. The cone is DERIVED from the target's apparent width, with no floor that can
/// dominate.</b> The wall was specified as a minor arc subtending 45-60 degrees, and against a
/// shooter that shape was mostly empty: measured, a 3-pack blocked 0 of a 23px shot line, a
/// 6-pack blocked 2, a 12-pack blocked 4 - the same 33% for every size, because the floor
/// could never lose. The cone is now the target's own apparent width plus a body's margin
/// either side, and the only remaining limit is the 60-degree ceiling.
/// </para>
///
/// <para>
/// <b>3. The surplus stacks BEHIND the front rank at the same angle.</b> Spreading columns to
/// the cone's edges regardless of whether they fit there put 12 bodies on a wall that 6 already
/// overran - half the pack standing in empty floor beside its own shield. Columns are now
/// spaced at <see cref="Balance.BrunchArcGap"/> and CLIPPED to the arc's own length, and the
/// second rank repeats the angle because it is deliberately the same face.
/// </para>
///
/// <para>
/// Measured slot positions for a shooter 200px from the player, as (slot, distance along the
/// bearing from the target): a 6-pack gives 49.64, 65.6, 50, 67, 49.64, 65.6 - three pairs of
/// front and back rank, the odd slots 17px behind, and the pair on the centreline where the
/// column calculation lands on a whole number.
/// </para>
/// </summary>

## [h:Tick-45]
near: `double col = System.Math.Floor(slot / 2.0) - ((cols - 1) / 2.0);`

/* Symmetric about the centreline, in HALF-STEPS - and the half-step is NOT optional.

   The original writes `(slot/2|0) - ((cols-1)/2)` in JavaScript, where `/` is float
   division and `|0` truncates ONCE at the end. Writing that as C# INTEGER division is
   wrong in a way that only shows on odd packs: for n=3, `(cols-1)/2` is `(2-1)/2` = 0.5
   in the original and `0` in C#, so every column shifts by half a step and the whole
   three-body arrangement mirrors.

   Measured: the game lays a 3-pack out at 49.642207661, 66.32762017, 49.642207661. With
   integer division the C# produced 50, 67, 49.642207661 - the middle and outer bodies
   swapped. Six bodies are unaffected, because `cols-1 = 2` divides evenly, which is
   exactly why an even-pack-only test suite would never have found it.

   So the half-step is carried as a DOUBLE: the FIRST term truncates, exactly as `|0`
   does in the original, and the second does not - because in JavaScript `((cols-1)/2)` has
   no `|0` on it and stays fractional. Getting that wrong in the other direction mirrors
   the odd-pack arrangement again, so both halves are spelled out. */

## [h:Tick-46]
near: `double radius = System.Math.Min(d * Balance.BrunchShieldFrac, d - BrunchRadius - 2)`

/* The stand-off. Two clamps, both of which were bugs once:
   - never further out than a share of the gap, or the wall ends up behind the player;
   - never closer than `d - brunchRadius - 2`, or it converges ON the guarded body rather
     than in front of it. */

## [h:Tick-47]
near: `double apparent = System.Math.Atan2((targetRadius == 0 ? 12 : targetRadius)`

/* The cone: the target's own apparent width at the arc's radius, plus one body's margin
   either side. The floor cannot dominate because it is a floor on a DERIVED value - a
   small enemy at close range gets a shield sized to it. */

## [h:Tick-48]
near: `public const double BrunchRadius = 8;`

/// <summary>
/// A Brunch's collision radius. Read out of the running game: <c>ENEMY.brunch.r</c> = 8. It
/// belongs to a body archetype, which the port does not have yet, so it is named here and the
/// spawn table replaces it.
/// </summary>
