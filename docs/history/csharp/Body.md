# Body.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Body.cs`. The code keeps a one-line gist tagged `[h:Body-N]`.

## [h:Body-1]
near: `public enum Trait`

/// <summary>
/// A build-dependent trait. It may change WHERE a fight happens and never HOW HARD it is - the
/// same rule the depth ladder obeys, pointed the other way.
/// </summary>

## [h:Body-2]
near: `Hold = 1,`

/// <summary>
/// Stands further out than the gun in hand is worth. The answer to a close gun: the fight
/// moves to a distance the gun's falloff punishes.
/// </summary>

## [h:Body-3]
near: `Close = 2,`

/// <summary>
/// Walks in. The answer to a single-target gun with a long reload: the player has to hold a
/// moving body at knife range on a cadence chosen for a target across the room.
/// </summary>

## [h:Body-4]
near: `public sealed class Body`

/// <summary>
/// One body in the world.
///
/// <para>
/// THE FIELD OPTIONALITY IS LOAD-BEARING, and it is the first thing in the port that is not a
/// plain transcription. The JavaScript builds a body with <c>Object.assign</c> and then ADDS
/// fields conditionally: a walker gets <c>walkSpeed</c> and a ranged body does not. The tick then
/// decides what kind of body it is holding with <c>e.walkSpeed!==undefined</c>.
/// </para>
///
/// <para>
/// That test is load-bearing and it is also the single most dangerous line in the game. Give a
/// ranged body a <c>walkSpeed</c> and it does not become a faster shooter - it becomes a lunger,
/// and its shell, its cadence, its muzzle prediction and its standoff rule all stop running, with
/// nothing anywhere reporting why. A trait did exactly that during development and the suite
/// caught it, but only because someone noticed that "make it close faster" is a speed change and
/// a speed change is difficulty.
/// </para>
///
/// <para>
/// So <see cref="HasWalkSpeed"/> is a named property rather than a null check scattered through
/// the simulation, and it exists for the same reason <c>doorRect</c> exists in the JavaScript:
/// a value that two places must agree on should have one name.
/// </para>
/// </summary>

## [h:Body-5]
near: `public double? WalkSpeed;`

/// <summary>
/// Present on a walker and absent on a ranged body. DO NOT SET THIS OUTSIDE THE SPAWNER.
/// <see cref="HasWalkSpeed"/> is the only sanctioned way to ask.
/// </summary>

## [h:Body-6]
near: `public bool HasWalkSpeed => WalkSpeed.HasValue;`

/// <summary>
/// THE type test. The tick uses this to choose between the walker branch and the ranged one,
/// so it is a named property rather than a null check written out at each call site.
/// </summary>

## [h:Body-7]
near: `public bool CanTakeTrait => !HasWalkSpeed && Far.HasValue;`

/// <summary>
/// The trait is only ever meaningful on a body that HAS a standoff band. A walker has nothing
/// to move, so asking one to hold further out is a question it cannot answer - and the only
/// way the previous version answered it was by writing <see cref="WalkSpeed"/>, which is the
/// bug documented on that field.
/// </summary>

## [h:Body-8]
near: `public double Standoff(double roomPressure, double closure) =>`

/// <summary>
/// The standoff the body actually holds, derived fresh rather than stored. This is the whole
/// mechanism a trait moves, and it is deliberately not a field: a cached standoff is a
/// second number that has to be kept in step with the two it is derived from.
/// </summary>
