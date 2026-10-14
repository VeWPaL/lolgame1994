using System.Collections.Generic;

namespace Depths
{
    /// <summary>The helpers the projectile phase calls, ported from src/40-combat.js and src/50-run.js. [h:Combat-1]</summary>
    public static class Combat
    {
        /// <summary>Damage falloff as a multiplier: full inside fNear, easing linearly to fMin at fFar, measured from the muzzle... [h:Combat-2]</summary>
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

        /// <summary>src/40-combat.js damagePlayer. [h:Combat-3]</summary>
        public static bool DamagePlayer(RunState run, double amount, double kx, double ky, double force, bool fromBoss = false)
        {
            var p = run.player;
            if (p.iframes > 0) return false;
            if (run.blinkGrace > 0 && !run.graceSpent)
            {
                run.blinkGrace = 0;
                run.graceSpent = true;
                if (force != 0) { p.kvx += kx * force * Balance.KnockPScale; p.kvy += ky * force * Balance.KnockPScale; }
                p.momentum *= Balance.MomentumHitKeep;
                return false;
            }
            run.dmgTaken += amount;
            double rem = amount;
            if (p.armor > 0 && Balance.JsReference) { double used = System.Math.Min(p.armor, rem); p.armor -= used; rem -= used; }
            else if (p.armor > 0)
            {
                // armour pays its own, smaller, cost; what it cannot cover reaches the hearts at full weight
                double cost = ArmorCost(amount, fromBoss);
                double paid = System.Math.Min(p.armor, cost);
                p.armor -= paid;
                rem = System.Math.Ceiling(amount * (cost - paid) / cost);
            }
            if (rem > 0 && p.regenHeart > 0) { double r = System.Math.Min(p.regenHeart, rem); p.regenHeart -= r; rem -= r; }
            if (rem > 0) p.hp -= rem;
            p.regenHeartT = 0;   // any hit that lands, armour-only included, restarts the refill clock
            if (p.hp < 0 && p.hp > -1e-6) p.hp = 0;   // a float epsilon is not a death
            if (run.state == "dev" && p.hp < 1) p.hp = 1;
            p.momentum *= Balance.MomentumHitKeep;
            if (force != 0) { p.kvx += kx * force * Balance.KnockPScale; p.kvy += ky * force * Balance.KnockPScale; }   // scaled as Knock scales a body's
            p.iframes = Balance.Iframes;
            return true;
        }

        /// <summary>What a hit costs in armour: ArmorTake x, rounded down, at least 1; the Warden's at full weight.</summary>
        public static double ArmorCost(double amount, bool fromBoss) =>
            fromBoss ? amount : System.Math.Max(1, System.Math.Floor(amount * Balance.ArmorTake));
    }

    /// <summary>The right-click blast going off: src/40-combat.js explode (the blast mode) and tryBreakSecret.</summary>
    public static class Blast
    {
        static readonly List<Enemy> Caught = new List<Enemy>();

        static double Smooth(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);

        /// <summary>One damage pool shared by everyone caught, divided by count^DISPERSE, so a crowd takes less each; a shove... [h:Combat-4]</summary>
        public static void Explode(RunState run, double x, double y, string mode = "blast")
        {
            run.booms++;
            bool hook = mode == "hook";
            double aoe = hook ? Balance.HookAoe : Balance.AltAoe, pool = hook ? 0 : Balance.AltPool;
            var en = run.enemies;
            Caught.Clear();
            foreach (var e in en)
                if (!e.buried && System.Math.Sqrt((x - e.x) * (x - e.x) + (y - e.y) * (y - e.y)) < aoe + e.r) Caught.Add(e);
            double share = Caught.Count > 0 ? pool / System.Math.Pow(Caught.Count, Balance.Disperse) : 0;
            for (int j = en.Count - 1; j >= 0; j--)
            {
                var e = en[j];
                if (!Caught.Contains(e)) continue;
                double dist = System.Math.Sqrt((x - e.x) * (x - e.x) + (y - e.y) * (y - e.y));
                double t = System.Math.Min(1, dist / (aoe + e.r));
                if (share != 0) { e.hp -= share * e.Vuln; e.hitFlash = Balance.HitFlash; }
                Combat.AlertEnemy(e);
                Combat.SlowEnemy(e);
                if (hook)
                {
                    // the yank to the point, scaled by how far out and by mass so the catch arrives together, then held
                    if (!Middle.Anchored(e.kind)) Movement.Knock(run, e, x - e.x, y - e.y, Balance.HookPull * dist * Balance.HookPullGain * e.mass);
                    e.stun = System.Math.Max(e.stun, Balance.HookHold);
                }
                else
                {
                    double near = 1 - Smooth(t);
                    Movement.Knock(run, e, e.x - x, e.y - y, Balance.AltKnock * (Balance.AltKnockFar + (Balance.AltKnockNear - Balance.AltKnockFar) * near));
                }
                if (e.hp <= 0) { Kills.KillEnemy(run, j); if (j > en.Count) j = en.Count; }   // the Warden takes its statues with it
            }
            if (hook) run.hookFields.Add(new RunState.HookField { x = x, y = y, r = Balance.HookAoe, life = Balance.HookField, max = Balance.HookField, id = ++run.hookFieldId });
            TryBreakSecret(run, x, y);
        }

        /// <summary>tickFields: each live field drains, holds and sucks in what stands in it, at a power
        /// charged once per field per body from how many hooks that body has already taken.</summary>
        public static void TickFields(RunState run)
        {
            var en = run.enemies;
            for (int i = run.hookFields.Count - 1; i >= 0; i--)
            {
                var f = run.hookFields[i];
                if (--f.life <= 0) { run.hookFields.RemoveAt(i); continue; }
                for (int j = en.Count - 1; j >= 0; j--)
                {
                    var e = en[j];
                    double dx = f.x - e.x, dy = f.y - e.y, d = System.Math.Sqrt(dx * dx + dy * dy);
                    if (d > f.r + e.r || e.buried) continue;
                    if (e.hookMark != f.id)
                    {
                        e.hookMark = f.id;
                        int n = e.hookStacks;
                        e.hookPower = n < Balance.HookResist.Length ? Balance.HookResist[n] : Balance.HookResist[Balance.HookResist.Length - 1];
                        e.hookStacks = System.Math.Min(Balance.HookResist.Length - 1, n + 1);
                        e.hookCalm = 0;
                    }
                    double power = e.hookPower;
                    e.stun = System.Math.Max(e.stun, Balance.HookFieldStun * power);
                    if (d > 1 && !Middle.Anchored(e.kind)) { e.x += dx / d * Balance.HookSuck * power; e.y += dy / d * Balance.HookSuck * power; }
                    e.hp -= Balance.HookDps * power / Balance.TickHz * e.Vuln;
                    e.hitFlash = System.Math.Max(e.hitFlash, Balance.HookFieldFlash * power);
                    Combat.AlertEnemy(e);
                    if (e.hp <= 0) { Kills.KillEnemy(run, j); if (j > en.Count) j = en.Count; }
                }
            }
        }

        /// <summary>tickHookResist: a body out of the fields for HOOK_FORGET ticks forgets one stack.</summary>
        public static void TickHookResist(RunState run)
        {
            foreach (var e in run.enemies)
            {
                if (e.hookMark == 0 || e.hookStacks <= 0) continue;
                if (++e.hookCalm >= Balance.HookForget) { e.hookStacks--; e.hookCalm = 0; }
            }
        }

        /// <summary>A right-click impact within 80% of the blast radius of a fake wall opens it, both sides.</summary>
        public static bool TryBreakSecret(RunState run, double x, double y)
        {
            var room = run.CurrentRoom;
            if (room == null || !room.Secret.HasValue) return false;
            var d = room.Secret.Value;
            double px = d == Dir.E ? Balance.RoomRight : d == Dir.W ? Balance.RoomLeft : Balance.MidX;
            double py = d == Dir.S ? Balance.RoomBottom : d == Dir.N ? Balance.RoomTop : Balance.MidY;
            if (System.Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) > Balance.AltAoe * 0.8) return false;
            OpenSecret(run, room);
            return true;
        }

        /// <summary>Open a room's fake wall, both sides, and count the secret found.</summary>
        public static void OpenSecret(RunState run, Room room)
        {
            var d = room.Secret!.Value;
            var sec = run.dungeon.Neighbour(room, d);
            room.Secret = null;
            room.Doors.Add(d);
            if (sec != null) sec.Doors.Add(d == Dir.N ? Dir.S : d == Dir.S ? Dir.N : d == Dir.E ? Dir.W : Dir.E);
            run.secret = true;
        }
    }

    public static class Kills
    {
        /// <summary>src/50-run.js killEnemy: remove the body by index, take the Warden's wall with it (not kills, no loot), count... [h:Combat-5]</summary>
        public static void KillEnemy(RunState run, int index)
        {
            var en = run.enemies;
            var e = en[index];
            en.RemoveAt(index);
            if (e.kind == BodyKind.Boss)
                for (int i = en.Count - 1; i >= 0; i--)
                    if (en[i].packId == Balance.BossWallId) en.RemoveAt(i);
            run.kills++;
            if (Balance.Tuned && Balance.TuneLoot && !Balance.JsReference)
            {
                // tuned: still one draw a kill, but only the room's last kill can drop, and only one thing -
                // a Brunch pack no longer rolls the room's whole loot pool
                double roll = run.rng.Run();
                var last = en.Count == 0 ? Loot.RoomDrop(roll, e.x, e.y, Curve.At(run.floor).Drops) : null;
                if (last != null) run.pickups.Add(last);
                return;
            }
            var d = Loot.Drop(run.rng, e.x, e.y, Curve.At(run.floor).Drops);
            if (d != null) run.pickups.Add(d);
        }
    }

    public static class Loot
    {
        /// <summary>dropLoot: 9% a half heart, 9% a heart, 4% half armour, 4% armour, otherwise nothing. [h:Combat-6]</summary>
        public static Pickup? Drop(Rng rng, double x, double y, double scale = 1)
        {
            double roll = rng.Run();
            if (Balance.JsReference) return roll < 0.18 ? Pickup.Of("heart", x, y, 10) : roll < 0.26 ? Pickup.Of("armor", x, y, 10) : null;
            roll /= scale;   // the curve's Drops: every band shrinks (or grows) by one factor, still one draw
            if (roll < 0.09) return Pickup.Of("halfheart", x, y, 10);
            if (roll < 0.18) return Pickup.Of("heart", x, y, 10);
            if (roll < 0.22) return Pickup.Of("halfarmor", x, y, 10);
            if (roll < 0.26) return Pickup.Of("armor", x, y, 10);
            return null;
        }

        /// <summary>The tuned drop (Balance.Tuned): a cleared room's last kill drops RoomDropChance of the
        /// time (times the curve's Drops), split as Drop splits it (9:9:4:4); one roll decides both.</summary>
        public static Pickup? RoomDrop(double roll, double x, double y, double scale = 1)
        {
            double chance = System.Math.Min(1, Balance.RoomDropChance * scale);
            if (roll >= chance) return null;
            double u = roll / chance * 26;
            return Pickup.Of(u < 9 ? "halfheart" : u < 18 ? "heart" : u < 22 ? "halfarmor" : "armor", x, y, 10);
        }
    }
}
