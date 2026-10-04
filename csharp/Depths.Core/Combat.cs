using System.Collections.Generic;

namespace Depths
{
    /// <summary>
    /// The four small helpers the projectile pass calls, ported from
    /// <c>src/40-combat.js</c> and <c>src/50-run.js</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the whole of slice 2. The projectile loop itself is 150 lines
    /// (<c>src/60-tick.js:592-741</c>) but it is not independently portable: it calls six helpers, and
    /// two of them - <c>explode</c> at 38 lines and <c>damagePlayer</c> at 70 - carry the death and
    /// iframes rules that have their own parity tests. The other four are here because they are small
    /// enough to land as one commit, they are leaves (nothing they call is unported except
    /// <c>dropLoot</c>, below), and pinning them now means the loop commit is a single reviewable
    /// change rather than 275 lines across three files.
    /// </para>
    /// <para>
    /// Each is a faithful transcription. Where the original relies on JavaScript's
    /// <c>undefined</c> - <c>if(e.aggroTimer!==undefined)</c>, <c>if(p.fNear===undefined)</c> - the C#
    /// asks the same question a different way, because the original's question is "does this body
    /// carry this field at all" and not "is it zero". A body with <c>aggroTimer == 0</c> and a body with
    /// no aggro timer are different states in the game and must stay different here.
    /// </para>
    /// </remarks>
    public static class Combat
    {
        /// <summary>
        /// Damage falloff for a shell, as a multiplier on its damage.
        /// <para>
        /// src/40-combat.js:133-137. A shell with no falloff band carries no <c>fNear</c> and deals full
        /// damage; a shell with one loses damage linearly from <c>fMin</c> at <c>fNear</c> down to
        /// <c>fNear</c> and <c>fMin</c> at <c>fFar</c>, measured from where it was FIRED
        /// (<c>ox</c>/<c>oy</c>) rather than from where it is now - which is why the muzzle is carried
        /// on the projectile at all.
        /// </para>
        /// <para>
        /// The three falloff fields are deliberately NOT on <see cref="Projectile"/>. They belong to
        /// <c>explode</c>, which is where the blast radius reads them, and a field in the port that no
        /// ported code reads is the drift PORTED.md exists to prevent. The nullable parameters are how
        /// "the original shell has no band" is expressed without inventing a sentinel value.
        /// </para>
        /// </summary>
        public static double FalloffMult(Projectile p, double? fNear = null,
                                         double? fMin = null, double? fFar = null)
        {
            if (!fNear.HasValue) return 1.0;

            var near = fNear.Value;
            var travelled = System.Math.Max(0.0,
                System.Math.Sqrt((p.x - p.ox) * (p.x - p.ox) + (p.y - p.oy) * (p.y - p.oy)) - near);
            // Math.max(1, fFar - fNear) in the original: a band with no width must not divide by zero,
            // and a NEGATIVE width must not invert the ramp into a bonus.
            var width = System.Math.Max(1.0, (fFar ?? 0.0) - near);
            var t = travelled / width;

            return 1.0 - (1.0 - (fMin ?? 1.0)) * System.Math.Min(1.0, t);
        }

        /// <summary>
        /// Mark a body as having noticed the player. src/40-combat.js:104-107.
        /// <para>
        /// Only ever RAISES the aggro timer, never lowers it: a body that has seen you should not lose
        /// interest because the next shell took a moment to arrive. That is why this is a Max and not
        /// an assignment, and it is the whole reason a fleeing player cannot shake a gunner by
        /// breaking line of sight for one tick.
        /// </para>
        /// <para>
        /// <c>hasAggro</c> stands in for <c>e.aggroTimer!==undefined</c>. A body without the field
        /// is a walker or a boss in some code paths, and treating its missing timer as zero would give
        /// it a timer of <c>Balance.AggroTime</c> and make it notice things it previously could not.
        /// </para>
        /// </summary>
        public static void AlertEnemy(Body e, bool hasAggro = true)
        {
            e.NoticeTimer = 0;
            e.Alerted = true;
            if (hasAggro)
                e.AggroTimer = System.Math.Max(e.AggroTimer, Balance.AggroTime);
        }

        /// <summary>
        /// Apply a landed shell's slow. src/40-combat.js:108 - one line, and the zeroing of
        /// <c>pursuit</c> is the load-bearing half.
        /// <para>
        /// <c>Pursuit</c> is the Brunch/Lunger commitment ramp, so a slowed body starts its approach
        /// over from zero: being hit interrupts the approach rather than merely slowing it. Without
        /// that second assignment a slowed pack would resume at the speed it had built up, which is
        /// not what the original does and is not obviously right either - it was worth being explicit
        /// about rather than transcribing silently.
        /// </para>
        /// </summary>
        public static void SlowEnemy(Body e)
        {
            e.SlowT = Balance.HitSlowTicks;
            e.Pursuit = 0;
        }
    }

    /// <summary>
    /// The removal half of a kill. src/50-run.js:471-476.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Order matters and is not incidental. The body is read for its position BEFORE the splice,
    /// because after the splice the index no longer refers to it; the tally is bumped
    /// after the splice, so a room that empties mid-iteration cannot see the count before the body
    /// is gone; and the drop is placed at the coordinates captured from the body rather than from the
    /// room.
    /// </para>
    /// <para>
    /// <c>dropLoot</c> is NOT ported and this is the one incompleteness in the slice, so it is named
    /// rather than hidden: the drop is skipped and the tally still moves. That is a deliberately
    /// partial function - a room's loot table is presentation-adjacent and PORTED.md keeps it out of
    /// scope - but a partial function is exactly the shape that gets mistaken for a complete one, so
    /// <c>DropsLoot</c> exists as a named seam for when the loot table lands.
    /// </para>
    /// </remarks>
    public static class Kills
    {
        /// <summary>
        /// Whether <see cref="KillEnemy"/> places a drop. False until the loot table is ported, and
        /// named so that a reader wondering where the pickup went finds the answer here.
        ///
        /// <para>
        /// A <c>static readonly</c> rather than a <c>const</c>, and the reason is worth one line
        /// because it is the compiler doing a useful job: as a <c>const</c> the compiler proved the
        /// drop branch unreachable and refused to build (CS0162), which is a louder signal than a
        /// silent omission would have been. As a field the branch compiles and is simply not taken,
        /// which is what "not ported yet" should look like.
        /// </para>
        /// </summary>
        public static readonly bool DropsLoot = false;

        /// <summary>
        /// Remove the body at <paramref name="index"/>, count the kill, and place its drop.
        /// </summary>
        public static void KillEnemy(RunState run, List<Body> enemies, int index)
        {
            var e = enemies[index];
            var x = e.X;
            var y = e.Y;

            enemies.RemoveAt(index);
            run.kills++;

            if (DropsLoot)
            {
                // src/50-run.js:474 - `const d=dropLoot(e.x,e.y); if(d) r.pickups.push(d);`
                // dropLoot returns null for a body with nothing to drop, so the push is conditional
                // rather than unconditional: a picker with a null kind would be worse than no picker.
                var d = Loot.Drop(x, y);
                if (d != null) run.pickups.Add(d);
            }
        }
    }

    /// <summary>
    /// The loot seam. Deliberately empty: <c>dropLoot</c> is not ported, and a function that always
    /// returns null is here so <see cref="Kills.KillEnemy"/> can be written in its final shape rather
    /// than with a hole where the drop goes.
    /// </summary>
    public static class Loot
    {
        /// <summary>
        /// The drop for a body at the given position, or null when it drops nothing.
        /// <para>
        /// Always null today. When this is ported it must draw from <c>run.rng.run</c> in the same
        /// place the original does, because the projectile pass's draw count is asserted and a drop
        /// that spent a draw would move every spawn-plan number.
        /// </para>
        /// </summary>
        public static Pickup? Drop(double x, double y) => null;
    }
}