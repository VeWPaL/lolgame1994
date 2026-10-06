namespace Depths
{
    /// <summary>One row of the weapon table, src/00-balance.js WEAPONS.</summary>
    public sealed class WeaponDef
    {
        public string Name = "", Color = "";
        public double CooldownS, SpeedPs, Dmg, Spread, R = 5;   // seconds between shots (before tempo); px/s
        public double Cooldown => Balance.SecF(CooldownS);       // in ticks at the current rate
        public double Speed => Balance.PerSec(SpeedPs);          // px per tick
        public int Count = 1, Pierce;
        public bool Shrink, SpreadFromPrecision;
        public double? FNear;
        public double FFar, FMin = 1;
        // buckshot: a weapon with a muzzle jitter scatters each pellet's origin, angle and speed
        public double? MuzzleJitter;
        public double PelletAngle, PelletSpeedVar;
    }

    /// <summary>
    /// The four guns and the act of firing one, src/40-combat.js fireWeapon. Values read from the
    /// running game 2026-10-06 (WEAPONS evaluated, so sec() and SPEEDUP are already applied).
    /// </summary>
    public static class Weapons
    {
        // Cooldowns as the JS wrote them: frames at its old 60 fps (38 x SPEEDUP ticks is 38/60 s), or sec(1.1).
        public static readonly WeaponDef[] All =
        {
            new WeaponDef { Name = "Bolt", Color = "#c79bff", CooldownS = 38 / 60.0, SpeedPs = 588, Dmg = 7, Spread = 0.05, R = 6,
                            Shrink = true, FNear = 130, FFar = 400, FMin = 0.5 },
            new WeaponDef { Name = "Scatter", Color = "#e8502a", CooldownS = 1.1, SpeedPs = 504, Dmg = 3.9, Count = 8, Spread = 0.045,
                            FNear = 180, FFar = 320, FMin = 0.22, MuzzleJitter = 5, PelletAngle = 0.008, PelletSpeedVar = 0.25 },
            new WeaponDef { Name = "Arcane Beam", Color = "#3fa9ff", CooldownS = 5.2 / 60, SpeedPs = 840, Dmg = 0.5, Spread = 0.16, R = 4,
                            SpreadFromPrecision = true, FNear = 170, FFar = 470, FMin = 0.6 },
            new WeaponDef { Name = "Voidball", Color = "#3f8a4a", CooldownS = 24 / 60.0, SpeedPs = 672, Dmg = 3.4, Spread = 0.02,
                            FNear = 150, FFar = 440, FMin = 0.55, Pierce = 3 },
        };

        public const double PrecisionSpreadFloor = 0.04, PrecisionStep = 0.2;
        public const double ShootSlowMax = 0.7, ShootSlowMain = 0.35;
        public static int MuzzleTicks => Balance.Sec(0.1);

        /// <summary>
        /// Fires the held weapon at a world point. Strength is added once per SHOT and split across the
        /// pellets; precision narrows a weapon whose cone is a stat. Both are parameters until the stats
        /// system is ported (base strength is the character's 3). Spread and buckshot draw from the
        /// JITTER stream, never the run stream, so firing cannot move the dungeon.
        /// </summary>
        public static void Fire(RunState run, double aimX, double aimY, double strength = 3, double precision = 0)
        {
            var p = run.player;
            var w = All[p.weaponIdx];
            double ang0 = System.Math.Atan2(aimY - p.y, aimX - p.x);
            double spread = w.SpreadFromPrecision
                ? w.Spread * System.Math.Max(PrecisionSpreadFloor, 1 - PrecisionStep * System.Math.Max(0, precision))
                : w.Spread;
            double dmg = (w.Dmg * w.Count + strength) / w.Count;
            for (int i = 0; i < w.Count; i++)
            {
                double sx = p.x, sy = p.y, a, spd = w.Speed;
                if (w.MuzzleJitter.HasValue)
                {
                    sx += (run.rng.Jitter() - 0.5) * 2 * w.MuzzleJitter.Value;
                    sy += (run.rng.Jitter() - 0.5) * 2 * w.MuzzleJitter.Value;
                    a = ang0 + (run.rng.Jitter() - 0.5) * 2 * w.PelletAngle;
                    spd = w.Speed * (1 + (run.rng.Jitter() - 0.5) * 2 * w.PelletSpeedVar);
                }
                else a = ang0 + (w.Count > 1 ? (i - (w.Count - 1) / 2.0) * spread : (run.rng.Jitter() - 0.5) * 2 * spread);
                double c = System.Math.Cos(a), s = System.Math.Sin(a);
                run.projectiles.Add(new Projectile
                {
                    x = sx, y = sy, vx = c * spd, vy = s * spd, r = w.R, dmg = dmg, friendly = true, color = w.Color,
                    ox = sx, oy = sy, fNear = w.FNear, fFar = w.FFar, fMin = w.FMin,
                    pierce = w.Pierce, scale = 1, dx = c, dy = s,
                });
            }
            run.shots += w.Count;
            p.cooldown = w.Cooldown / Balance.TempoRate;
            p.muzzleTimer = MuzzleTicks;
            p.shootSlow = System.Math.Min(ShootSlowMax, p.shootSlow + ShootSlowMain);
        }

        /// <summary>
        /// fireAlt for the BLAST: a slow bolt to the cursor (clamped to the room), on its own cooldown.
        /// The hook (a secret-room pickup) is not ported and throws.
        /// </summary>
        public static void FireAlt(RunState run, double aimX, double aimY)
        {
            var p = run.player;
            bool hook = p.altMode == "hook";
            if (hook)
            {
                // a second right click while the hook flies detonates it where it is (after HOOK_EARLY_MIN)
                int live = run.projectiles.FindIndex(q => q.alt && q.mode == "hook");
                if (live >= 0)
                {
                    var q = run.projectiles[live];
                    if (q.age >= Balance.HookEarlyMin) { Blast.Explode(run, q.x, q.y, "hook"); run.projectiles.RemoveAt(live); }
                    return;
                }
            }
            if (p.altCooldown > 0) return;
            double tx = System.Math.Max(Balance.RoomLeft, System.Math.Min(Balance.RoomRight, aimX));
            double ty = System.Math.Max(Balance.RoomTop, System.Math.Min(Balance.RoomBottom, aimY));
            double a = System.Math.Atan2(ty - p.y, tx - p.x);
            run.projectiles.Add(new Projectile
            {
                x = p.x, y = p.y,
                vx = System.Math.Cos(a) * (hook ? Balance.HookSpeed : Balance.AltSpeed), vy = System.Math.Sin(a) * (hook ? Balance.HookSpeed : Balance.AltSpeed),
                speed = hook ? Balance.HookSpeed : Balance.AltSpeed, r = hook ? Balance.HookR : Balance.AltR, friendly = true,
                color = hook ? "#2a6fc4" : "#ff8a3d", alt = true, phase = hook, mode = hook ? "hook" : "blast", age = 0, tx = tx, ty = ty,
            });
            p.altCooldown = (int)(Balance.AltCooldown / Balance.TempoRate);
            p.muzzleTimer = MuzzleTicks;
            p.shootSlow = System.Math.Min(ShootSlowMax, p.shootSlow + Balance.ShootSlowAlt);
        }
    }
}
