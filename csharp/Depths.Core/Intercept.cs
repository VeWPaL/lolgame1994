namespace Depths
{
    /// <summary>
    /// The result of an intercept solve: where to aim, and how long the shot will take to arrive.
    /// </summary>
    public readonly struct Intercept
    {
        /// <summary>Unit vector from the shooter toward the point the shot will arrive at.</summary>
        public readonly double Dx, Dy;

        /// <summary>Distance from the shooter to that arrival point.</summary>
        public readonly double Dist;

        /// <summary>Total ticks from the moment of solving until the shot arrives.</summary>
        public readonly double Ticks;

        /// <summary>
        /// How much of the target's motion was believed: 1 is a fully settled heading, and the floor
        /// is whatever the caller sets. This is the number that makes a reversal a downgrade rather
        /// than an escape.
        /// </summary>
        public readonly double Confidence;

        public Intercept(double dx, double dy, double dist, double ticks, double confidence)
        {
            Dx = dx; Dy = dy; Dist = dist; Ticks = ticks; Confidence = confidence;
        }

        /// <summary>The arrival point in absolute world coordinates, from a shooter at (sx, sy).</summary>
        public (double X, double Y) Arrival(double sx, double sy)
        {
            return (sx + Dx * Dist, sy + Dy * Dist);
        }
    }

    /// <summary>
    /// Everything in the game that aims at a moving player goes through here.
    ///
    /// <para>
    /// There are two consumers - the lunger's lunge and the gunner's shell - and they are the same
    /// problem with different terms. That is not a coincidence to be tidied away later; it is why
    /// they agree with each other about the player. Both read the same smoothed heading, both scale
    /// that reading by the same <c>swerve</c> signal, and both shrink when the player thrashes.
    /// </para>
    /// </summary>
    public static class InterceptSolver
    {
        /// <summary>
        /// The one iteration, shared by both consumers so they cannot drift apart.
        /// </summary>
        /// <param name="baseX">Believed target X at the moment of solving.</param>
        /// <param name="baseY">Believed target Y at the moment of solving.</param>
        /// <param name="vx">Believed target velocity, already scaled by confidence.</param>
        /// <param name="vy">Believed target velocity, already scaled by confidence.</param>
        /// <param name="sx">Shooter X.</param>
        /// <param name="sy">Shooter Y.</param>
        /// <param name="speed">Projectile speed, px/tick.</param>
        /// <param name="delay">
        /// Ticks before the projectile actually exists - the cast. Seeded from the target's position
        /// at launch rather than at solve time, because otherwise the half second of visible
        /// charging is a half second of free movement the prediction never saw.
        /// </param>
        /// <param name="iterations">
        /// How many fixed-point passes. Not a free parameter: convergence is set by the ratio of
        /// target speed to projectile speed, and a fast projectile converges slowly. See
        /// <see cref="Balance.LungeIter"/> and <see cref="Balance.GunIter"/>.
        /// </param>
        private static Intercept Solve(double baseX, double baseY, double vx, double vy,
                                       double sx, double sy, double speed, int delay, int iterations)
        {
            // Seeded at the target's CURRENT position, not its predicted one.
            double ax = baseX - sx, ay = baseY - sy, need = 0;
            for (int k = 0; k < iterations; k++)
            {
                need = delay + System.Math.Sqrt(ax * ax + ay * ay) / speed;
                ax = baseX + vx * need - sx;
                ay = baseY + vy * need - sy;
            }
            double d = System.Math.Sqrt(ax * ax + ay * ay);
            if (d == 0) d = 1;                     // the original's `|| 1` guard, preserved
            return new Intercept(ax / d, ay / d, d, need, 0);
        }

        /// <summary>
        /// The lunger's lunge.
        /// </summary>
        /// <remarks>
        /// The velocity the lunger BELIEVES, which is not the velocity the player has.
        ///
        /// <para>
        /// Instantaneous velocity is a bad thing to aim from, and not only because it is noisy. A
        /// player one tick into pressing a key is still nearly stationary, so a lunger that reads the
        /// raw number commits to a point that stops being true immediately - and a player
        /// mid-reversal is read at whatever the easing happens to be sitting on that tick. So it reads
        /// a smoothed heading, and it reads it in proportion to how SETTLED the player looks, using
        /// the same swerve the gunners use to widen their aim. One signal, two consumers, and it
        /// means the two gunners agree about the player.
        /// </para>
        ///
        /// <para>
        /// The consequence is the point. Hold a line and you are read in full, so the intercept is
        /// right and the lunge lands. Start reversing and the lunger loses confidence in you, the
        /// solution shortens, and the lunge it commits to is a smaller one - which the player can
        /// then slip. Baiting is not free: it is a downgrade. You get a weaker attack instead of no
        /// attack.
        /// </para>
        /// </remarks>
        public static Intercept Lunge(double baseX, double baseY,
                                      double trendVx, double trendVy, double swerve,
                                      double sx, double sy)
        {
            double conf = Balance.LungeConfMin + (1 - Balance.LungeConfMin) * (1 - swerve);
            var r = Solve(baseX, baseY, trendVx * conf, trendVy * conf, sx, sy,
                          Balance.LungeSpeed, Balance.LungeWindup, Balance.LungeIter);
            return new Intercept(r.Dx, r.Dy, r.Dist, r.Ticks, conf);
        }

        /// <summary>
        /// The gunner's shell.
        /// </summary>
        /// <remarks>
        /// Two things here are the whole difference from the lunge, and both were bugs before they
        /// were features.
        ///
        /// <para>
        /// The target is the player's HITBOX, ten pixels below the point their sprite is drawn from,
        /// because that is the circle <see cref="Hit.PlayerHit"/> tests. An intercept solved against
        /// the origin is aimed ten pixels above the thing it is trying to hit, every single time, by
        /// an amount small enough to look like nothing and large enough to matter.
        /// </para>
        ///
        /// <para>
        /// And the delay is the cast, which is a quarter of a second of visible muzzle light during
        /// which the player keeps moving. A gunner that fires the instant its cooldown runs out gives
        /// the player nothing to read and the only counter is not being there; a gunner that solves
        /// without counting its own charge is worse than that, because it spends the charge on a
        /// target that has already left.
        /// </para>
        ///
        /// <para>
        /// The spread is NOT applied here. It is the caller's job and it is passed in already
        /// rolled, which keeps this function pure and therefore testable. See
        /// <see cref="SwerveReach(double)"/> and <see cref="GunSpread"/> for what the caller scales it by.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Where a gunner aims: a real intercept against a moving target, from the muzzle it will
        /// actually fire from.
        /// </summary>
        /// <param name="lagX">The target's lagged x - the hitbox origin, not the drawn sprite.</param>
        /// <param name="lagY">The target's lagged y.</param>
        /// <param name="trendVx">
        /// The target's CURRENT velocity. The name is the JavaScript field name kept for parity, but
        /// it is the value that matters and it CHANGED: this used to receive a 71-tick-old EMA and
        /// treat it as the present. A 400px shell flies about 195 ticks and a player covers roughly
        /// 1.4px a tick, so seventy-one ticks of stale velocity is a hundred pixels of error by
        /// arrival - measured as a straight runner missing by a consistent 86-108px at 400px, and as
        /// being literally unhittable at 200px with a full Momentum meter.
        ///
        /// The smoothing was not removed, it was DEMOTED, which is the accurate word: an EMA exists
        /// to stop a body reacting to one tick of knockback noise, and the swerve confidence already
        /// does that job and does it better. So noise rejection lives in the confidence and freshness
        /// lives in the velocity. They were one knob doing two jobs, and a committed player paid for
        /// the compromise by a hundred pixels while a reverser - whom the confidence already handles
        /// - got nothing from it.
        /// </param>
        /// <param name="trendVy">The target's current y velocity.</param>
        /// <param name="swerve">How unsettled the target looks, 0..1. Scales the whole belief down.</param>
        /// <param name="sx">The muzzle x the shell will leave from.</param>
        /// <param name="sy">The muzzle y the shell will leave from.</param>
        /// <param name="shellSpeed">Shell speed in px per tick.</param>
        public static Intercept Gun(double lagX, double lagY,
                                    double trendVx, double trendVy, double swerve,
                                    double sx, double sy, double shellSpeed)
        {
            double conf = 1 - swerve;
            double baseY = lagY + Balance.PlayerHitDy;      // the hitbox, not the sprite origin
            var r = Solve(lagX, baseY, trendVx * conf, trendVy * conf, sx, sy,
                          shellSpeed, Balance.CastTime, Balance.GunIter);
            return new Intercept(r.Dx, r.Dy, r.Dist, r.Ticks, conf);
        }

        /// <summary>
        /// How far away a target has to be before counterstrafing buys anything, as a 0..1 ramp.
        /// Zero inside the deadzone, one at the far end of it.
        /// </summary>
        public static double SwerveReach(double dist) => SwerveReach(dist, Balance.Room.Standard);

        /// <summary>The same ramp, in a named room.</summary>
        /// <remarks>
        /// The deadzone is a property of the ROOM, so the room is a parameter. Frozen at one room's
        /// size it reads a long way off in any other, and a gunner in a big room then reads a
        /// reversing player at full strength from across it - the counter to the whole mechanic,
        /// silently switched off. The overload above resolves to the standard room, which is the only
        /// room this port has until world state lands, and is the same trade the JavaScript made with
        /// its ROOM_LEFT shorthand.
        /// </remarks>
        public static double SwerveReach(double dist, Balance.Room room)
        {
            // The room is a parameter because the deadzone is a property of the ROOM - half its
            // width, less a margin. Frozen at one room's size it reads a long way off in any other,
            // and a gunner in a big room then reads a reversing player at full strength from across
            // it: the counter to the whole mechanic, silently off. The no-room overload resolves to
            // the standard room, which is the only room this port has until world state lands - the
            // same trade the JavaScript made with its ROOM_LEFT shorthand.
            double dz = Balance.SwerveDeadzone(room), top = Balance.SwerveFull(room);
            double t = (dist - dz) / (top - dz);
            if (t < 0) return 0;
            return t > 1 ? 1 : t;
        }

        /// <summary>
        /// The total angular spread a gunner fires with, in radians: a floor so that no two shots are
        /// identical, plus the counterstrafing allowance opened up by distance.
        /// </summary>
        public static double GunSpread(double swerve, double dist)
        {
            return Balance.GunSpreadFloor + Balance.SwerveAim * swerve * SwerveReach(dist);
        }
    }
}
