# Combat.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Combat.cs`. The code keeps a one-line gist tagged `[h:Combat-N]`.

## [h:Combat-1]
near: `public static class Combat`

/// <summary>
/// The helpers the projectile phase calls, ported from <c>src/40-combat.js</c> and
/// <c>src/50-run.js</c>. They act on <see cref="Enemy"/>, the live body; an earlier version took
/// <see cref="Body"/>, the archetype, which a run never holds, so nothing in a run could call them.
/// Where the original asks <c>x!==undefined</c>, the C# asks with a nullable rather than a zero.
/// </summary>

## [h:Combat-2]
near: `public static double FalloffMult(Projectile p, double? fNear = null,`

/// <summary>
/// Damage falloff as a multiplier: full inside fNear, easing linearly to fMin at fFar, measured
/// from the muzzle (ox, oy). No band (fNear null) is full damage. src/40-combat.js falloffMult.
/// </summary>

## [h:Combat-3]
near: `public static bool DamagePlayer(RunState run, double amount, double kx, double ky, double force, bool fromBoss = false)`

/// <summary>
/// src/40-combat.js damagePlayer. Returns whether the hit landed. Order matters and is the
/// game's: i-frames refuse it; an unspent blink grace forgives it (knockback and the momentum
/// cost still apply, no i-frames); otherwise armour absorbs first (at ArmorCost, the rest
/// passing on at full weight), then the regenerating heart, then the red; the regen clock
/// restarts, momentum keeps 55%, the knock is applied and i-frames start. The lab cannot die.
/// </summary>

## [h:Combat-4]
near: `public static void Explode(RunState run, double x, double y, string mode = "blast")`

/// <summary>
/// One damage pool shared by everyone caught, divided by count^DISPERSE, so a crowd takes less
/// each; a shove strongest at the centre and fading to the rim; then the fake wall, if it is
/// close enough. Hook fields are not ported.
/// </summary>

## [h:Combat-5]
near: `public static void KillEnemy(RunState run, int index)`

/// <summary>
/// src/50-run.js killEnemy: remove the body by index, take the Warden's wall with it (not
/// kills, no loot), count the kill, and roll the drop - one draw from the run stream, every
/// time, which is why the drop could not stay a stub without moving every later number.
/// </summary>

## [h:Combat-6]
near: `public static Pickup? Drop(Rng rng, double x, double y, double scale = 1)`

/// <summary>
/// dropLoot: 9% a half heart, 9% a heart, 4% half armour, 4% armour, otherwise nothing. Always
/// one draw. The JS has no halves (18% heart, 8% armour); the split, 2026-10-06, cuts the
/// healing a kill pays by a quarter while keeping the same draws and the same drop rate.
/// </summary>
