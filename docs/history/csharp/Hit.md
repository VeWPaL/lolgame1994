# Hit.cs - moved comments

Long comments moved out of `csharp/Depths.Core/Hit.cs`. The code keeps a one-line gist tagged `[h:Hit-N]`.

## [h:Hit-1]
near: `public static class Hit`

/// <summary>
/// The one place that asks "did that hit me".
///
/// <para>
/// Both the shells and the bodies go through it, which is what keeps those two paths agreeing.
/// It is deliberately a single function and not a shape on a body: when the two disagree at the
/// boundary, players read it as unfair, and they are right to.
/// </para>
/// </summary>

## [h:Hit-2]
near: `public static bool PlayerHit(double x, double y, double r, double lagX, double lagY)`

/// <summary>Tests a point against the player's hitbox.</summary>
/// <param name="x">Test point X, in world pixels.</param>
/// <param name="y">Test point Y, in world pixels.</param>
/// <param name="r">Radius of whatever is testing - a shell, or a body.</param>
/// <param name="lagX">The player's lagged X: where enemies believe the player is.</param>
/// <param name="lagY">The player's lagged Y. <c>PlayerHitDy</c> is added to it internally.</param>
/// <returns>True if the two overlap.</returns>
/// <remarks>
/// The Y comparison is the thing that gets forgotten. The hitbox is a circle ten pixels BELOW
/// the coordinate the body is drawn from, and it trails the body by twenty-odd pixels at a
/// run. A shot that is aimed at, or measured against, the drawn position rather than this
/// circle is systematically high by the length of the trail - enough to turn a shot that
/// should have connected into one that passes over the player's head with a hitbox's width to
/// spare.
///
/// <para>
/// That exact bug is why the gunner's intercept solves for a point ten pixels below where
/// the player is drawn. A lunger must NOT aim here, because it steers a body and would
/// visibly drift low - but a projectile has no excuse at all for missing the one part of the
/// player it is going to hit.
/// </para>
/// </remarks>
