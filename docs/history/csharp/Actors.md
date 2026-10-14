# Actors.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Actors.cs`. The code keeps a one-line gist tagged `[h:Actors-N]`.

## [h:Actors-1]
near: `public sealed class Player`

/// <summary>
/// The mutable bodies the tick drives: the player, the enemies, the projectiles, and the
/// transient pickups. Classes rather than structs because they are mutated tick over tick, and
/// matching <c>Body</c>'s style for the same reason - a struct copy of a body that is being
/// knocked back would hand the knockback to a detached duplicate.
///
/// <para>
/// Field lists follow the interface contract. Where a method below needs a number the contract
/// does not name - <c>Enemy.cdMin</c>, <c>Enemy.shootCd</c>, <c>Enemy.pspd</c> and so on, all
/// of which <c>src/60-tick.js:28-35</c> writes on the body - the field lives here with a
/// comment, rather than being smuggled through a static or reconstructed from elsewhere. The
/// JavaScript body has these too; <c>spawnEnemy</c> sets them.
/// </para>
/// </summary>

## [h:Actors-2]
near: `public double cooldown;`

/// <summary>
/// Weapon cooldowns in ticks, and iframes in ticks.
/// <para>
/// <c>cooldown</c> is a <c>double</c> on purpose. Read out of the running game, the Arcane
/// Beam's cadence is 18.2 ticks - not 18 - and the room phase does
/// <c>Math.min(cooldown, WEAPONS[w].cooldown)</c> on it. An <c>int</c> field would either
/// truncate the table to 18 (a silent balance change no whole-number test would catch) or
/// force a rounding decision at every call site. <c>altCooldown</c> and <c>iframes</c> are
/// genuinely whole and stay <c>int</c>.
/// </para>
/// </summary>

## [h:Actors-3]
near: `public double r = 13;`

/// <summary>
/// The player's collision radius, and the second term in a pickup's touch test
/// (<c>pk.r + player.r</c>). Read out of the running game as 13. It is separate from
/// <c>Balance.PlayerHitR</c> (10), which is the SHELL hit radius - the pickup is easier to
/// walk into than a shell is to be hit by, and folding the two together would shrink every
/// pickup's reach.
/// </summary>

## [h:Actors-4]
near: `public double blinkRegen;`

/// <summary>
/// Ticks of blink charge owed, granted as a fraction by a cleared room. A double, because
/// the refund is <c>BlinkRecharge * 0.5</c> = 367.5 and the original keeps the fraction.
/// </summary>

## [h:Actors-5]
near: ``

/* ---- the movement fields, added for the tickPlayer port ----
   Every default below was READ OUT OF THE RUNNING GAME on a fresh run with seed 4242, not
   reasoned about. `speed` is the important one: it is 1.122, which is 0.935 * PLAYER_MOVE,
   and it is the BASE speed - the tick's real top speed is speed * (1 + MoveSpeedBonus) and
   comes to 1.4025 in an empty room. Anything that quotes 1.4025 as "the player's speed" is
   quoting the post-bonus figure and will be wrong for any calculation about acceleration. */

## [h:Actors-6]
near: `public double speed = 0.935 * Balance.PlayerMove;   // px per tick, fixed when the body is made`

/// <summary>
/// Base movement speed, before the momentum bonus and before firing slow-motion. Read out of
/// the running game: 1.122. The tick's terminal speed is this times (1 + MoveSpeedBonus),
/// which is 1.4025 in an empty room and 1.6045 against a pack with the meter full.
/// </summary>

## [h:Actors-7]
near: `public double slowMult = 1;`

/// <summary>
/// The firing slow-motion multiplier, eased toward 1 every tick. Starts at 1 - full speed -
/// and is pulled DOWN toward <c>1 - shootSlow</c> while the player is firing on the move.
/// </summary>

## [h:Actors-8]
near: `public double trendVx, trendVy;`

/// <summary>
/// The direction the player is actually travelling, smoothed. The LUNGERS aim at this.
/// <para>
/// It is a DIRECTION and not a velocity: it is renormalised after smoothing, so its magnitude
/// is not a measure of speed and a player pressing against a wall still reports the direction
/// they are trying to go.
/// </para>
/// </summary>

## [h:Actors-9]
near: `public double beliefVx, beliefVy;`

/// <summary>
/// The player's direction as a slower, noisier estimate - what the player BELIEVES they are
/// doing. The GUNNERS read this, not <see cref="trendVx"/>, and the difference is the whole
/// counterstrafe mechanic: they lead a noisier picture of the player than the lungers do, so
/// a clean reversal fools them and not the lungers.
/// </summary>

## [h:Actors-10]
near: `public double dirX, dirY;`

/// <summary>
/// The last committed movement direction, as a unit vector. The swerve meter compares the new
/// direction against this, so it only rises while the player is already moving - standing
/// still and starting is not a reversal.
/// </summary>

## [h:Actors-11]
near: `public double swerve;`

/// <summary>
/// How much the player is currently swerving, 0 to 1. Rises on a reversal and decays every
/// tick; the gunners trust their intercept less the higher it is. See
/// <see cref="Balance.SwerveDecay"/> for the rate, which was wrong in this port until a
/// measurement caught it.
/// </summary>

## [h:Actors-12]
near: `public int? packId;`

/// <summary>
/// Which pack this body belongs to, and its slot within it.
/// <para>
/// <c>null</c> for a body that is not part of a pack - which is every non-Brunch body, and the
/// Brunch that spawns alone. It is nullable rather than a sentinel int because the JavaScript
/// tests <c>packId === undefined</c>, and a sentinel would make "pack zero" and "no pack"
/// indistinguishable - a pack with id 0 is a real pack and must not be skipped.
/// </para>
/// </summary>

## [h:Actors-13]
near: `public Enemy? shieldTarget;`

/// <summary>
/// Which pack this body belongs to, and its slot within it. <c>null</c> for a body that is not
/// part of a pack.
/// <para>
/// The pair is what makes the Brunch a SHIELD rather than a crowd: a pack is a set of bodies
/// that agree on a centroid and take slots around it, and the slot index is what decides which
/// arc position a body holds. Two bodies with the same <c>packId</c> and the same
/// <c>packSlot</c> would fight for the same place, which is why the generator hands out both.
/// </para>
/// </summary>
/// <summary>
/// The body this one is shielding. Set by the pack's scan, held until that body dies or leaves
/// the room.
/// <para>
/// <b>A PACK COMMITS.</b> The scan condition is
/// <c>!shieldTarget &amp;&amp; (frameCount % BrunchScanTicks === 0)</c> - the
/// <c>!shieldTarget</c> guard means a pack that already has a LIVING target never re-picks, even
/// if a much nearer ranged body walks in. Measured in the running game: a pack guarding a
/// distant gunner does not switch to a shooter that arrives 20px away.
/// </para>
/// <para>
/// That is a deliberate property rather than an oversight. The pack's behaviour is a function
/// of the target it has, and re-deciding every 20 ticks would make a Brunch's escort visibly
/// indecisive in a room with several ranged bodies - and would let the player pull a pack off
/// its charge by walking a rival into range, which turns a threat into a switch.
/// </para>
/// </summary>

## [h:Actors-14]
near: `public List<Enemy>? shieldGuardFor;`

/// <summary>
/// The Brunch bodies currently guarding THIS body, rebuilt every tick by the body phase.
/// <para>
/// It lives on the guarded body rather than on the Brunch because the question it answers is
/// asked of the guarded body: "am I being escorted?" The answer changes a ranged body's
/// standoff from <c>far</c> to <c>far * GuardStandoffMult</c>, so a pack pulls its charge back
/// and gives the player room to reach it. That is the whole point of a shield - it protects,
/// and protection that leaves no opening is not an invitation.
/// </para>
/// </summary>

## [h:Actors-15]
near: `public sealed class Projectile`

/// <summary>
/// A shell in flight. Widened from seven fields to the 23 the projectile pass in
/// <c>src/60-tick.js</c> actually reads (lines 592-741), measured rather than guessed - the
/// previous version declared only x, y, vx, vy, r, dmg, friendly, heavy, so sixteen of the
/// fields the pass depends on did not exist on the port at all.
/// </summary>
/// <remarks>
/// WHAT IS HERE AND WHAT IS DELIBERATELY NOT. Shells are CONSTRUCTED with 29 distinct fields
/// across 40-combat.js, 50-run.js, 60-tick.js and 80-ui.js. Seven of those are never read by the
/// projectile pass - <c>fNear</c>, <c>fMid</c>, <c>fFar</c> (the damage-falloff band, read by the
/// blast radius in <c>explode</c>), <c>from</c> (provenance for the view), <c>it</c> (the
/// arcane-beam lifetime counter, which the view decrements), <c>shrink</c> and <c>heavy</c>
/// (both view concerns). They are not added now: a field in the port that no ported code reads is
/// exactly the drift PORTED.md exists to prevent, and the falloff trio belongs with
/// <c>explode</c> rather than with the loop that calls it.
///
/// <c>hit</c> is the list of bodies this shell has already counted. It is what stops a piercing
/// bolt re-hitting the body it is currently inside, and it is the reason a bolt damages a line of
/// Brunesh once each rather than every body it overlaps on a single tick.
/// </remarks>

## [h:Actors-16]
near: `public bool alt;`

/// <summary>
/// True for the alt/weapon-thrown shell, which flies to a fixed point, passes THROUGH bodies
/// while <see cref="phase"/> is set, and resolves on arrival rather than on contact.
/// </summary>

## [h:Actors-17]
near: `public bool phase;`

/// <summary>
/// The alt shell's phase: while true it ignores bodies entirely. The bolt is resolved from
/// the weapon it was THROWN with (<c>mode</c>), not the one held when it lands.
/// </summary>

## [h:Actors-18]
near: `public double dx, dy, ox, oy;`

/// <summary>
/// The direction of travel, and the MOUTH it left from. <c>dx</c>/<c>dy</c> are normalised;
/// <c>ox</c>/<c>oy</c> are the spawn point. Both are fixed at spawn on purpose: the piercing
/// hit order is arrival order along the flight line, so no amount of jostling inside a knot can
/// reshuffle which body a bolt reaches first.
/// </summary>

## [h:Actors-19]
near: `public double scale = 1;   // every bolt the game fires carries scale 1; 0 would silently do no damage`

/// <summary>
/// Damage multiplier, multiplied down by PIERCE_FALLOFF on each body. This is what makes lining
/// the pack up a decision rather than a free delete.
/// </summary>

## [h:Actors-20]
near: `public string mode = "";`

/// <summary>
/// Which WEAPON the alt shell was cast with. Resolved on arrival rather than from whatever is
/// held then: swapping to the other right-click mid-flight used to detonate a pull as a blast.
/// </summary>

## [h:Actors-21]
near: `public sealed class Pickup`

/// <summary>
/// A pickup in the current room, widened for the <c>tickRoom</c> port.
/// <para>
/// The first version of this record carried only a position and a kind, which was enough to
/// name the collection and not enough to run it. Four fields had to be added because the room
/// phase reads all of them and two of them change behaviour rather than merely position:
/// </para>
/// <list type="bullet">
/// <item><c>r</c> - the collection radius. The original tests
/// <c>dist &lt; pk.r + player.r</c>, so a heart at r=16 and a key at r=14 are picked up at
/// genuinely different distances. Carrying one constant here would quietly change which
/// pickups are reachable at the edge of a room.</item>
/// <item><c>hold</c> - set the tick after a pickup is taken, and cleared the tick after the
/// player steps off it. This is what stops a just-collected item being re-collected on the
/// same tick, and it is the reason the weapon swap parks the old gun rather than deleting it.</item>
/// <item><c>w</c>, <c>id</c>, <c>charges</c> - the payload. A weapon pickup carries the index
/// it grants, an item carries its id and how many charges came with it.</item>
/// </list>
/// <para>
/// A pickup is still not a body: nothing moves it and nothing shoots it.
/// </para>
/// </summary>
