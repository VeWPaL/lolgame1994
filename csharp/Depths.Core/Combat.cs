using System.Collections.Generic;

namespace Depths
{
    /// <summary>
    /// The helpers the projectile phase calls, ported from <c>src/40-combat.js</c> and
    /// <c>src/50-run.js</c>. They act on <see cref="Enemy"/>, the live body; an earlier version took
    /// <see cref="Body"/>, the archetype, which a run never holds, so nothing in a run could call them.
    /// Where the original asks <c>x!==undefined</c>, the C# asks with a nullable rather than a zero.
    /// </summary>
    public static class Combat
    {
        /// <summary>
        /// Damage falloff as a multiplier: full inside fNear, easing linearly to fMin at fFar, measured
        /// from the muzzle (ox, oy). No band (fNear null) is full damage. src/40-combat.js falloffMult.
        /// </summary>
        public static double FalloffMult(Projectile p, double? fNear = null,
                                         double? fMin = null, double? fFar = null)
        {
            if (!fNear.HasValue) return 1.0;
            var near = fNear.Value;
            var travelled = System.Math.Max(0.0,
                System.Math.Sqrt((p.x - p.ox) * (p.x - p.ox) + (p.y - p.oy) * (p.y - p.oy)) - near);
            // Math.max(1, fFar - fNear): a band with no width must not divide by zero or invert
            var width = System.Math.Max(1.0, (fFar ?? 0.0) - near);
            return 1.0 - (1.0 - (fMin ?? 1.0)) * System.Math.Min(1.0, travelled / width);
        }

        /// <summary>The shell's own band, as the game reads it in the projectile pass.</summary>
        public static double FalloffMult(Projectile p) => FalloffMult(p, p.fNear, p.fMin, p.fFar);

        /// <summary>A landed hit wakes a body from anywhere and commits it for at least AGGRO_TIME.</summary>
        public static void AlertEnemy(Enemy e)
        {
            e.noticeTimer = 0;
            e.alerted = true;
            if (e.aggroTimer.HasValue) e.aggroTimer = System.Math.Max(e.aggroTimer.Value, Balance.AggroTime);
        }

        /// <summary>A hit slows a body and restarts its approach ramp.</summary>
        public static void SlowEnemy(Enemy e)
        {
            e.slowT = Balance.HitSlowTicks;
            e.pursuit = 0;
        }

        /// <summary>
        /// src/40-combat.js damagePlayer. Returns whether the hit landed. Order matters and is the
        /// game's: i-frames refuse it; an unspent blink grace forgives it (knockback and the momentum
        /// cost still apply, no i-frames); otherwise armour absorbs first, momentum keeps 55%, the
        /// knock is applied and i-frames start. The lab cannot die.
        /// </summary>
        public static bool DamagePlayer(RunState run, double amount, double kx, double ky, double force)
        {
            var p = run.player;
            if (p.iframes > 0) return false;
            if (run.blinkGrace > 0 && !run.graceSpent)
            {
                run.blinkGrace = 0;
                run.graceSpent = true;
                if (force != 0) { p.kvx += kx * force; p.kvy += ky * force; }
                p.momentum *= Balance.MomentumHitKeep;
                return false;
            }
            run.dmgTaken += amount;
            double rem = amount;
            if (p.armor > 0) { double used = System.Math.Min(p.armor, rem); p.armor -= used; rem -= used; }
            if (rem > 0) p.hp -= rem;
            if (p.hp < 0 && p.hp > -1e-6) p.hp = 0;   // a float epsilon is not a death
            if (run.state == "dev" && p.hp < 1) p.hp = 1;
            p.momentum *= Balance.MomentumHitKeep;
            if (force != 0) { p.kvx += kx * force; p.kvy += ky * force; }
            p.iframes = Balance.Iframes;
            return true;
        }
    }

    public static class Kills
    {
        /// <summary>
        /// src/50-run.js killEnemy: remove the body by index, take the Warden's wall with it (not
        /// kills, no loot), count the kill, and roll the drop - one draw from the run stream, every
        /// time, which is why the drop could not stay a stub without moving every later number.
        /// </summary>
        public static void KillEnemy(RunState run, int index)
        {
            var en = run.enemies;
            var e = en[index];
            en.RemoveAt(index);
            if (e.kind == BodyKind.Boss)
                for (int i = en.Count - 1; i >= 0; i--)
                    if (en[i].packId == Balance.BossWallId) en.RemoveAt(i);
            run.kills++;
            var d = Loot.Drop(run.rng, e.x, e.y);
            if (d != null) run.pickups.Add(d);
        }
    }

    public static class Loot
    {
        /// <summary>src/10-art.js dropLoot: 18% a heart, 8% armour, otherwise nothing. Always one draw.</summary>
        public static Pickup? Drop(Rng rng, double x, double y)
        {
            double roll = rng.Run();
            if (roll < 0.18) return Pickup.Of("heart", x, y, 10);
            if (roll < 0.26) return Pickup.Of("armor", x, y, 10);
            return null;
        }
    }
}
