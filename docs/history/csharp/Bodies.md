# Bodies.cs - moved comments

## [h:Bodies-1] the armour rule

Every row in <see cref="Rows"/> sets <c>Armour</c> explicitly, and that is a rule rather
than a coincidence. <c>BodyRow.Armour</c> is a plain <c>double</c>, so a row that simply
forgot it reads 0.0 - and this port used to disagree with the JavaScript on exactly that
point, in OPPOSITE directions: C# forgot to a body that absorbs everything, JavaScript
forgot to a body that takes full damage while every other body is multiplied by 0.66. The
Warden was the casualty in JS, where the omission was deliberate and commented as such, so
both ports were wrong and neither one marked the other wrong.

<para>
The fix is not a cleverer default - a default would only move the failure to whichever
value someone happened to pick. It is that every row names its armour, so a row that
forgets is a test failure. <c>BodyParityTests</c> asserts it, and asserts the rule it has
to obey: every body at least as wide as a lunger is armoured, and the Brunch - half a
lunger's radius - is the single body that is not.
</para>

## [h:Bodies-2] the old BrunchRun 1.75 (stale)

Removed 2026-10-06. Bodies.cs kept its own BrunchWalk (0.62*PlayerMove = 0.744) and BrunchRun (1.75)
after the game moved to BRUNCH_WALK 0.52*PLAYER_MOVE (0.624) and BRUNCH_CHASE_SPEED 2.1, and the lunger
at 18*TOUGH after the game moved to 15*TOUGH. BodyParityTests pinned the stale numbers by hand, so it
passed. The rows now read Balance.BrunchWalkSpeed / Balance.BrunchRun, which tools/constant-audit.js
checks against the running game. The old rationale:

The Brunch's last-resort chase speed: the one it uses when it has nothing left to shield.
Measured against the player's 1.2 in a room large enough that a 14s chase never reaches a
wall, 1.35 grew the gap ~107px per 2s - a pack that could never run the player down, which
is the only job this sprint exists for. 1.62 merely held the gap and never closed it.
1.75 is the first value on the sweep that closes (~62px per 2s from 450px). The BRUNCH_RAMP
announcement is unchanged, so the pack is still slower than the player for ~1.1s.
</summary>

## [h:Bodies-3]
near: `public static class Bodies`

/// <summary>
/// The body table, the spawner, and the build-dependent trait.
///
/// <para>
/// Every number in <see cref="Rows"/> was read out of the running JavaScript rather than
/// transcribed from its source expressions, because a transcription error in a balance table is
/// invisible until a fight quietly feels wrong. The source forms are in the comments so the
/// relationship is not lost - the point of a table is that the numbers are in ONE place, and that
/// includes knowing what each one is a multiple of.
/// </para>
/// </summary>

## [h:Bodies-4]
near: `public const double Tough = 1.35;      // the global HP multiplier`

/// <summary>
/// The knobs the table is written in multiples of, so the relationships survive the port.
/// </summary>

## [h:Bodies-5]
near: `public static BodyRow[] Rows`

/// <summary>
/// The table, rebuilt only when the tick rate changes (its speeds and cadences are per tick) - and built rather than written out, so the
/// derived numbers are computed the same way the JavaScript computes them instead of being
/// frozen at whatever they happened to be when this was typed.
///
/// <para>
/// The nullable fields are the same conditional-field trick the JavaScript uses, for the same
/// reason: a walker has no standoff band, and pretending otherwise with a sentinel number is
/// how two code paths end up disagreeing about what kind of body they are holding.
/// </para>
/// </summary>

## [h:Bodies-6]
near: `new BodyRow { Mass = 4, Radius = 28, Art = 4, Bar = 40, Hp = 343 * Tough, Armour = Armour,`

// boss: the Warden. HP is sized from measured weapon DPS, which is the only way to size
// a health bar. 343*TOUGH rather than 520*TOUGH because the boss now obeys ARMOUR like
// every other body, and holding the effective pool fixed across that change is what keeps
// it a repair to the rule rather than a 1.52x pacing change to every boss fight in the game

## [h:Bodies-7]
near: `public static Body Spawn(BodyKind kind, int floor, Rng rng)`

/// <summary>
/// Builds a body. The depth ladder is read HERE and nowhere else on this type, and it is read
/// as a function of the floor rather than as a field on the run, because a difficulty that
/// reads the player is a difficulty that measures the player.
/// </summary>
///
/// <param name="kind">Which body to build.</param>
/// <param name="floor">
/// The depth. Read through <see cref="Balance.DepthTough"/> and
/// <see cref="Balance.DepthRate"/>, which take nothing else - the depth-only rule enforced by
/// signature rather than by a check that could be deleted.
/// </param>
/// <param name="rng">
/// The source to draw the body's spawn jitter from - the <c>jitter</c> stream, never
/// <c>run</c>. A body is part of what the run IS, but the jitter in its spawn - notice delay,
/// dodge cooldown - is not, and pulling it from the run stream would let a cosmetic roll
/// change the dungeon.
/// </param>

## [h:Bodies-8]
near: `b.WalkSpeed = c.Walk;`

// A walker. The depth rate does NOT scale a walker's top speed: the rate dial is the
// reaction-time dial, and a body that outruns the player cannot be responded to.
// Lungers and Brunch keep their speeds at every depth.

## [h:Bodies-9]
near: `public static Trait TraitFor(BodyKind gun, Rng rng)`

/// <summary>
/// Which answer a gun gets, and how reliably. The weight is not difficulty - it is how
/// reliably the answer works, which decides how many bodies in a room take it. The ceilings
/// stop a stack: a room of traited bodies must all still stand in roughly the same place.
///
/// <para>
/// The pairing is the design. A close gun wants to be standing next to something, so it is
/// answered by standing further out than it is worth. A single-target gun with a long reload
/// wants a target that holds still, so it is answered by walking in.
/// </para>
/// </summary>

## [h:Bodies-10]
near: `public static readonly int[] BrunchPack = { 4, 5, 6, 7, 8 };`

/// <summary>
/// How many bodies a Brunch pack is, and how likely each size is.
///
/// <para>
/// The sizes ascend and so do the weights, deliberately: a big pack is the rarer event, so a
/// room with eight is something to remember rather than the default. The weights sum to exactly
/// 1, which is why the roll can walk the table subtracting as it goes and fall off the end into
/// the smallest size.
/// </para>
/// </summary>

## [h:Bodies-11]
near: `public struct AreaMix`

/// <summary>
/// The enemy-mix dials for one area: which bodies a normal slot rolls, how often a room
/// carries a heavy, and the base pack chance the depth ladder starts from.
///
/// <para>
/// This is the ONLY thing an area changes. The ladder itself (<see cref="Balance.DepthTough"/>,
/// <see cref="Balance.DepthRate"/>, <see cref="Balance.DepthBodies"/>,
/// <see cref="Balance.DepthPack"/>) is the same shape in every area - an area is an identity,
/// not a harder or easier version of the climb.
/// </para>
/// </summary>

## [h:Bodies-12]
near: `public static AreaMix MixFor(Area area)`

/// <summary>
/// The mix for an area. Area1 reproduces the measured numbers the port pinned before areas
/// existed (lunger 0.5, heavy 0.55, brunch 0.45 == <see cref="BrunchChance"/>), so a default
/// Area1 plan is byte-identical to the pre-area plan the 127 parity rows were read from.
///
/// <para>
/// Area2/Area3/Final are explicit guesses, marked as such: they are design intent awaiting a
/// playtest, not values read out of the running JavaScript. Area1 is the only row that is
/// measured. Do not retune an area by feel - change it, then re-measure.
/// </para>
/// </summary>

## [h:Bodies-13]
near: `public const int BrunchWallMin = 3;`

/// <summary>
/// A pack smaller than this is a knot rather than a wall.
///
/// <para>
/// The formation rule asks whether the bodies sharing one pack id number at least this many, so
/// this is the line between "arrives as a crowd" and "arrives as a wall you have to go around".
/// It is asserted in the spawn tests because a pack that can never reach it is a documented
/// mechanic that is dead in the game - which is exactly what finding #1 was, and it shipped
/// because the only wall anyone had seen was the boss's, which uses a fixed id and so was
/// unaffected.
/// </para>
/// </summary>

## [h:Bodies-14]
near: `public static Trait ApplyTrait(Body b, Trait t)`

/// <summary>
/// Applies a trait to a body, in place.
///
/// <para>
/// RANGED BODIES ONLY, and that is not a preference - it is the bug this shape exists to
/// prevent. The first version had a "close faster" trait that set WalkSpeed, intending to make
/// a shooter press in. It did not: writing WalkSpeed onto a body that lacked it CONVERTED it
/// into a walker, and from that tick its whole ranged kit stopped running. So the only fields
/// touched here are Far and Close, and <see cref="Body.CanTakeTrait"/> is the guard.
/// </para>
///
/// <para>
/// Both are shifted by the SAME factor, which is the only safe version: the band keeps its
/// width, so a body does not start dithering, and the change is a place rather than a
/// personality. Scaling only Far would widen the band as a side effect, which is a different
/// and less legible thing.
/// </para>
/// </summary>
