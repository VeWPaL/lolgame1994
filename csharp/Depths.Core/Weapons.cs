namespace Depths
{
    /// <summary>One row of the weapon table, src/00-balance.js WEAPONS.</summary>
    public sealed class WeaponDef
    {
        public string Name = "", Color = "";
        public double Cooldown, Speed, Dmg, Spread, R = 5;
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
        public static readonly WeaponDef[] All =
        {
            new WeaponDef { Name = "Bolt", Color = "#c79bff", Cooldown = 133, Speed = 2.8, Dmg = 7, Spread = 0.05, R = 6,
                            Shrink = true, FNear = 130, FFar = 400, FMin = 0.5 },
            new WeaponDef { Name = "Scatter", Color = "#e8502a", Cooldown = 231, Speed = 2.4, Dmg = 3.9, Count = 8, Spread = 0.045,
                            FNear = 180, FFar = 320, FMin = 0.22, MuzzleJitter = 5, PelletAngle = 0.008, PelletSpeedVar = 0.25 },
            new WeaponDef { Name = "Arcane Beam", Color = "#3fa9ff", Cooldown = 18.2, Speed = 4, Dmg = 0.5, Spread = 0.16, R = 4,
                            SpreadFromPrecision = true, FNear = 170, FFar = 470, FMin = 0.6 },
            new WeaponDef { Name = "Voidball", Color = "#3f8a4a", Cooldown = 84, Speed = 3.2, Dmg = 3.4, Spread = 0.02,
                            FNear = 150, FFar = 440, FMin = 0.55, Pierce = 3 },
        };

        public const double PrecisionSpreadFloor = 0.04, PrecisionStep = 0.2;
        public const double ShootSlowMax = 0.7, ShootSlowMain = 0.35;
        public static readonly int MuzzleTicks = Balance.Sec(0.1);

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
    }
}
