namespace Depths
{
    /// <summary>The middle enemies (C# only): Splitter, Mortar and Sprayer walk and aim like a shooter (TickOrder.StepRanged)... [h:Middle-1]</summary>
    public static class Middle
    {
        public static bool Is(BodyKind k) => k == BodyKind.Splitter || k == BodyKind.Mortar || k == BodyKind.Burrower || k == BodyKind.Sprayer || k == BodyKind.Egg;
        public static bool Anchored(BodyKind k) => k == BodyKind.Egg;
        public static bool Ranged(BodyKind k) => k == BodyKind.Splitter || k == BodyKind.Mortar || k == BodyKind.Sprayer;
        public static int CastTime(BodyKind k) => k == BodyKind.Sprayer ? SprayWind : k == BodyKind.Egg ? EggWind : Balance.CastTime;
        public static bool Sharp => Balance.Tuned && Balance.TuneMiddle;

        // ---- Splitter ----
        public static double SplitRange => Sharp ? 120 : 170;      // px from the muzzle
        public const int SplitCount = 3;
        public static double SplitSpread => Sharp ? 0.22 : 0.42;   // radians either side
        public static double SplitShardSpeed => Balance.PerSec(380);
        public const double SplitShardR = 4, SplitShardDmg = 1;

        // ---- Mortar ----
        public static int LobFlight => Balance.Sec(1.0);
        public static double LobBlast => Sharp ? 56 : 34;          // px
        public const double LobDmg = 2;
        public const double LobTrustMin = 0.3, LobTrustMax = 0.85;  // share of the lead it trusts, rolled per shell
        public const double LobLeadMax = 230;                       // px: the furthest ahead it marks

        // ---- Sprayer ----
        public static int SprayWind => Balance.Sec(Sharp ? 0.85 : 1.0);
        public const int SprayCount = 5;
        public static double SprayCone => Sharp ? 0.19 : 0.35;     // radians either side
        public const double SprayCadence = 0.82;                   // x its cooldown when Sharp (Spawn.Body)
        const double SprayReach = 360;                             // px of each line checked for an ally

        // ---- Turret egg ----
        public static int EggWind => Balance.Sec(0.6);
        public const int EggRing = 8;

        // ---- Burrower ----
        public static double BurrowWalk => Balance.PerSec(66);                // lab-era walk
        public static double BurrowWalkSharp => Balance.PerSec(150);
        public static double BurrowWalkEase => Balance.Ease(0.02);            // per JS tick: ~0.25 s to its walk
        public static double BurrowTurnWalk => Balance.PerSec(4.5);           // rad/s
        public static double BurrowWeave => Balance.PerSec(2.6);              // rad/s of the weave's phase
        public const double BurrowWeaveAmp = 0.5;                             // rad either side of its heading
        public static double BurrowRun => Balance.PerSec(Sharp ? 480 : 300);  // top speed under the floor: above a full-momentum runner
        public static double BurrowRamp => Balance.Ease(0.0068);              // per JS tick: ~0.7 s from walk to top speed
        public static double BurrowBrake => Balance.Ease(0.03);               // per JS tick: it sheds speed faster than it gains it
        public static double BurrowRoot => Balance.Ease(0.12);                // per JS tick: it stops within ~0.1 s as it digs
        public static double BurrowTurn => Balance.PerSec(3.4);               // rad/s under the floor
        public static double BurrowArrive => Balance.SecF(0.14);              // slows within this many ticks of its mark
        public static double BurrowLeadMax => Balance.SecF(1.0);              // the longest lead it ever takes
        public const double BurrowTrustMin = 0.1, BurrowTrustMax = 0.8;       // share of the lead, rolled per dive
        public const double BurrowSlop = 26;                                  // px of random offset, rolled per dive
        public const double BurrowSurfaceAt = 14;                             // px from its mark: up it comes
        public static int BurrowSurface => Balance.Sec(1.6);
        public static int BurrowDig => Balance.Sec(0.45);
        public static int BurrowUnder => Balance.Sec(Sharp ? 2.2 : 1.2);
        public static int BurrowRise => Balance.Sec(0.6);
        public const double BurrowHitR = 30, BurrowDmg = 2;

        public const string SplitColor = "#ff6ad5", LobColor = "#ffb03a", SprayColor = "#ff4d4d", EggColor = "#b6ff5c";

        /// <summary>The cast has finished: what leaves, by kind. The cooldown was already rolled by the caller.</summary>
        public static void Release(RunState run, Enemy e)
        {
            switch (e.kind)
            {
                case BodyKind.Splitter:
                    run.projectiles.Add(Shell(e, e.castAim, e.pspd, e.pr, e.dmg, SplitColor, SplitRange));
                    break;
                case BodyKind.Mortar:
                {
                    var (tx, ty) = Sharp ? LobMark(run) : (TickOrder.HitboxX, TickOrder.HitboxY + Balance.PlayerHitDy);
                    double m = LobBlast * 0.5;
                    tx = Clamp(tx, Balance.RoomLeft + m, Balance.RoomRight - m);
                    ty = Clamp(ty, Balance.RoomTop + m, Balance.RoomBottom - m);
                    run.projectiles.Add(new Projectile
                    {
                        x = e.x, y = e.y, ox = e.x, oy = e.y, tx = tx, ty = ty, lob = true, flightMax = LobFlight,
                        r = e.pr, dmg = LobDmg, blastR = LobBlast, friendly = false, owner = e, color = LobColor,
                    });
                    break;
                }
                case BodyKind.Sprayer:
                {
                    if (!Sharp)
                    {
                        for (int i = 0; i < SprayCount; i++)
                            run.projectiles.Add(Shell(e, e.castAim + SprayCone * (2.0 * i / (SprayCount - 1) - 1), e.pspd, e.pr, e.dmg, SprayColor, 0));
                        break;
                    }
                    double aim = SprayAim(run, e);
                    double? clear = Movement.ClearCone(run, e, aim, SprayCone, SprayCount, SprayReach);
                    if (clear.HasValue) aim = clear.Value;
                    for (int i = 0; i < SprayCount; i++)
                    {
                        double a = aim + SprayCone * (2.0 * i / (SprayCount - 1) - 1);
                        if (!clear.HasValue && !Movement.LineClear(run, e, a, SprayReach, e.pr)) continue;
                        run.projectiles.Add(Shell(e, a, e.pspd, e.pr, e.dmg, SprayColor, 0));
                    }
                    break;
                }
                case BodyKind.Egg:
                    Eggs.Ring(run, e);
                    break;
                default:
                    throw new System.InvalidOperationException("Middle.Release: no release for " + e.kind);
            }
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        // where the lob lands: the player's velocity over the flight, trusted by how settled they look and by a share
        // rolled per shell, never more than LobLeadMax ahead
        static (double x, double y) LobMark(RunState run)
        {
            var p = run.player;
            double k = (1 - p.swerve) * (LobTrustMin + (LobTrustMax - LobTrustMin) * run.rng.Jitter()) * LobFlight;
            double lx = p.vx * k, ly = p.vy * k, len = System.Math.Sqrt(lx * lx + ly * ly);
            if (len > LobLeadMax) { lx *= LobLeadMax / len; ly *= LobLeadMax / len; }
            return (TickOrder.HitboxX + lx, TickOrder.HitboxY + Balance.PlayerHitDy + ly);
        }

        // the Sprayer aims again as it fires: the shooter's intercept and jitter, from where it stands now
        static double SprayAim(RunState run, Enemy e)
        {
            var p = run.player;
            double conf = System.Math.Max(0, 1 - p.swerve * Balance.SwerveTrustRooted);
            double hx = TickOrder.HitboxX, hy = TickOrder.HitboxY + Balance.PlayerHitDy;
            double dist = System.Math.Sqrt((hx - e.x) * (hx - e.x) + (hy - e.y) * (hy - e.y));
            return Aim.Intercept(e.x, e.y, hx, hy, p.vx * conf, p.vy * conf, 0, e.pspd)
                   + (run.rng.Jitter() - 0.5) * 2 * Aim.GunSpread(p.swerve, dist);
        }

        internal static Projectile Shell(Enemy e, double a, double speed, double r, double dmg, string color, double splitAt) => new Projectile
        {
            x = e.x, y = e.y, ox = e.x, oy = e.y, vx = System.Math.Cos(a) * speed, vy = System.Math.Sin(a) * speed,
            dx = System.Math.Cos(a), dy = System.Math.Sin(a), r = r, dmg = dmg, friendly = false, owner = e,
            color = color, splitAt = splitAt,
        };

        /// <summary>A Splitter shell at its range: three shards fanned about its heading, from where it stands.</summary>
        public static void Split(RunState run, Projectile p)
        {
            double h = System.Math.Atan2(p.vy, p.vx);
            for (int i = 0; i < SplitCount; i++)
            {
                double a = h + SplitSpread * (2.0 * i / (SplitCount - 1) - 1);
                run.projectiles.Add(new Projectile
                {
                    x = p.x, y = p.y, ox = p.x, oy = p.y, vx = System.Math.Cos(a) * SplitShardSpeed, vy = System.Math.Sin(a) * SplitShardSpeed,
                    dx = System.Math.Cos(a), dy = System.Math.Sin(a), r = SplitShardR, dmg = SplitShardDmg, friendly = false,
                    owner = p.owner, color = p.color,
                });
            }
        }

        /// <summary>A lob, once a tick: the straight line to its mark over its flight; it strikes only as it lands.</summary>
        public static bool StepLob(RunState run, Projectile p)
        {
            if (++p.flight < p.flightMax)
            {
                double t = p.flight / (double)p.flightMax;
                p.x = p.ox + (p.tx - p.ox) * t;
                p.y = p.oy + (p.ty - p.oy) * t;
                return false;
            }
            p.x = p.tx; p.y = p.ty;
            Strike(run, p.tx, p.ty, p.blastR, p.dmg, 2.5);
            return true;
        }

        static void Strike(RunState run, double x, double y, double r, double dmg, double knock)
        {
            var pl = run.player;
            if (!Hit.PlayerHit(x, y, r, pl.lagX, pl.lagY)) return;
            double dx = pl.lagX - x, dy = pl.lagY + Balance.PlayerHitDy - y, d = System.Math.Sqrt(dx * dx + dy * dy);
            if (d == 0) { dx = 0; dy = 1; d = 1; }
            Combat.DamagePlayer(run, dmg, dx / d, dy / d, knock * Balance.KnockPGain, false);
        }

        /// <summary>The egg: rides (Eggs.Move), counts its cooldown, winds up, lets a ring go through the shared cast clock.</summary>
        public static void StepEgg(RunState run, Enemy e, double roomPress)
        {
            Eggs.Move(run, e);
            if (e.castT > 0 || e.castReady) { TickOrder.FireCommittedShot(run, e, roomPress); return; }
            if (--e.shootCd > 0) return;
            e.castAim = run.rng.Jitter() * 2 * System.Math.PI / EggRing;
            e.castT = EggWind;
        }

        /// <summary>The Burrower's cycle on move/moveT: walk (hittable), dig (a rooted tell), under (buried: no contact, no... [h:Middle-2]</summary>
        public static void StepBurrower(RunState run, Enemy e, double edx, double edy, double dist, double sm)
        {
            switch (e.move)
            {
                case "dig":
                    if (Sharp) Steer(e, e.heading, 0, BurrowRoot, 0, sm);   // a short skid, then rooted for the tell
                    if (--e.moveT > 0) return;
                    e.buried = true;
                    e.markX = TickOrder.HitboxX; e.markY = TickOrder.HitboxY + Balance.PlayerHitDy;
                    e.move = "under"; e.moveT = BurrowUnder;
                    if (Sharp)
                    {
                        e.digTrust = BurrowTrustMin + (BurrowTrustMax - BurrowTrustMin) * run.rng.Jitter();
                        double a = run.rng.Jitter() * 2 * System.Math.PI, r = BurrowSlop * run.rng.Jitter();
                        e.digOffX = System.Math.Cos(a) * r; e.digOffY = System.Math.Sin(a) * r;
                        e.curSpeed = System.Math.Max(e.curSpeed, BurrowWalkSharp * Balance.DepthRate(run.floor));
                    }
                    return;
                case "under":
                    if (Sharp) Dive(run, e, sm);
                    else
                    {
                        double step = BurrowRun * Balance.DepthRate(run.floor) * sm;
                        double mx = e.markX - e.x, my = e.markY - e.y, md = System.Math.Sqrt(mx * mx + my * my);
                        if (md <= step) { e.x = e.markX; e.y = e.markY; }
                        else { e.x += mx / md * step; e.y += my / md * step; }
                        if (md <= step || --e.moveT <= 0) Surface(e);
                    }
                    return;
                case "rise":
                    if (--e.moveT > 0) return;
                    e.buried = false;
                    Strike(run, e.x, e.y, BurrowHitR, BurrowDmg, 6);
                    e.move = "walk"; e.moveT = BurrowSurface; e.curSpeed = 0;
                    return;
                default:   // "walk", and "idle" on the first step
                    if (e.move != "walk") { e.move = "walk"; e.moveT = BurrowSurface; e.heading = System.Math.Atan2(edy, edx); }
                    if (Sharp)
                    {
                        double want = System.Math.Atan2(edy, edx) + BurrowWeaveAmp * System.Math.Sin(e.lifeT * BurrowWeave);
                        Steer(e, want, BurrowWalkSharp * Balance.DepthRate(run.floor), BurrowWalkEase, BurrowTurnWalk, sm);
                    }
                    else
                    {
                        double v = BurrowWalk * Balance.DepthRate(run.floor) * sm;
                        e.x += edx / dist * v; e.y += edy / dist * v;
                    }
                    if (--e.moveT <= 0) { e.move = "dig"; e.moveT = BurrowDig; }
                    return;
            }
        }

        static void Surface(Enemy e) { e.markX = e.x; e.markY = e.y; e.move = "rise"; e.moveT = BurrowRise; }

        // one tick under the floor: the mark is the player plus the share of their lead this dive trusts, plus its slop;
        // it gathers speed toward its top, eases off as it arrives, and turns at a limited rate, so it curves in
        static void Dive(RunState run, Enemy e, double sm)
        {
            var p = run.player;
            double top = BurrowRun * Balance.DepthRate(run.floor);
            double hx = TickOrder.HitboxX, hy = TickOrder.HitboxY + Balance.PlayerHitDy;
            double gap = System.Math.Sqrt((hx - e.x) * (hx - e.x) + (hy - e.y) * (hy - e.y));
            double lead = System.Math.Min(BurrowLeadMax, gap / System.Math.Max(1e-9, e.curSpeed) + BurrowRise) * e.digTrust * (1 - p.swerve);
            double rm = e.r + 4;
            e.markX = Clamp(hx + p.vx * lead + e.digOffX, Balance.RoomLeft + rm, Balance.RoomRight - rm);
            e.markY = Clamp(hy + p.vy * lead + e.digOffY, Balance.RoomTop + rm, Balance.RoomBottom - rm);
            double mx = e.markX - e.x, my = e.markY - e.y, md = System.Math.Sqrt(mx * mx + my * my);
            if (md <= BurrowSurfaceAt) { Surface(e); return; }
            double speed = System.Math.Min(top, System.Math.Max(BurrowWalkSharp * 0.25, md / BurrowArrive));
            // near its mark it may turn as tightly as its speed needs to come inside the surfacing radius, so it
            // settles onto the mark instead of circling it (the critic: 150 px/s at 6.8 rad/s orbits at 22 px)
            double turn = md < 80 ? System.Math.Max(BurrowTurn * 2, e.curSpeed / (BurrowSurfaceAt * 0.6)) : BurrowTurn;
            Steer(e, System.Math.Atan2(my, mx), speed, speed < e.curSpeed ? BurrowBrake : BurrowRamp, turn, sm);
            if (--e.moveT <= 0) Surface(e);
        }

        // speed eases toward `speed` by `ease`, the heading turns toward `want` by at most `turn` a tick (0: it keeps its heading)
        static void Steer(Enemy e, double want, double speed, double ease, double turn, double sm)
        {
            if (turn > 0)
            {
                double d = System.Math.IEEERemainder(want - e.heading, 2 * System.Math.PI);
                e.heading += d > turn ? turn : d < -turn ? -turn : d;
            }
            e.curSpeed += (speed - e.curSpeed) * ease;
            e.x += System.Math.Cos(e.heading) * e.curSpeed * sm;
            e.y += System.Math.Sin(e.heading) * e.curSpeed * sm;
        }
    }
}
