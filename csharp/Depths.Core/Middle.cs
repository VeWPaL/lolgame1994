namespace Depths
{
    /// <summary>
    /// The middle enemies (C# only, after js-final; the owner's idea of 2026-10-12): five bodies for
    /// floors 3-10 whose threat a skilled player can read and dodge, so skill orders deaths there the
    /// way the gunner's shell does not. NOT in the spawn pool yet - the lab spawns them for playtesting.
    /// Splitter, Mortar and Sprayer walk and aim like a shooter (<c>StepRanged</c>) and differ in what
    /// leaves when the cast ends (<see cref="Release"/>); the Burrower and the Turret egg have their own
    /// steps. Every dial is in seconds or per-second units.
    /// </summary>
    public static class Middle
    {
        public static bool Is(BodyKind k) => k >= BodyKind.Splitter;

        /// <summary>Never moved by the hook's yank or its field: the egg is a fixture the room is built around.</summary>
        public static bool Anchored(BodyKind k) => k == BodyKind.Egg;

        /// <summary>Walks a standoff and casts at an intercept, as a shooter does.</summary>
        public static bool Ranged(BodyKind k) => k == BodyKind.Splitter || k == BodyKind.Mortar || k == BodyKind.Sprayer;

        /// <summary>How long the cast tell runs before the release: the Sprayer's long wind-up is its whole design.</summary>
        public static int CastTime(BodyKind k) => k == BodyKind.Sprayer ? SprayWind : k == BodyKind.Egg ? EggWind : Balance.CastTime;

        // ---- Splitter: a slow shell that bursts into shards at a set range; punishes standing still ----
        public const double SplitRange = 170;               // px from the muzzle
        public const int SplitCount = 3;
        public const double SplitSpread = 0.42;             // radians either side of the heading
        public static double SplitShardSpeed => Balance.PerSec(380);
        public const double SplitShardR = 4, SplitShardDmg = 1;

        // ---- Mortar: lobs at where you stand; the marker is the whole flight, so moving beats it ----
        public static int LobFlight => Balance.Sec(1.0);
        public const double LobBlast = 34, LobDmg = 2;

        // ---- Sprayer: a long, visible wind-up, then a short cone of weak shots ----
        public static int SprayWind => Balance.Sec(1.0);
        public const int SprayCount = 5;
        public const double SprayCone = 0.35;               // radians either side

        // ---- Turret egg: never walks; a slow ring every few seconds shapes the room ----
        public static int EggWind => Balance.Sec(0.6);
        public const int EggRing = 8;

        // ---- Burrower: walks, digs in, runs under to your last spot, a tell, then up under you ----
        public static double BurrowWalk => Balance.PerSec(66);
        public static double BurrowRun => Balance.PerSec(300);
        public static int BurrowSurface => Balance.Sec(1.6);
        public static int BurrowDig => Balance.Sec(0.45);
        public static int BurrowUnder => Balance.Sec(1.2);   // the longest it stays down; it surfaces where it got to
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
                    // where the player stands as it leaves, not where they are going: the marker is honest
                    double tx = TickOrder.HitboxX, ty = TickOrder.HitboxY + Balance.PlayerHitDy;
                    run.projectiles.Add(new Projectile
                    {
                        x = e.x, y = e.y, ox = e.x, oy = e.y, tx = tx, ty = ty, lob = true, flightMax = LobFlight,
                        r = e.pr, dmg = LobDmg, blastR = LobBlast, friendly = false, owner = e, color = LobColor,
                    });
                    break;
                }
                case BodyKind.Sprayer:
                    for (int i = 0; i < SprayCount; i++)
                    {
                        double a = e.castAim + SprayCone * (2.0 * i / (SprayCount - 1) - 1);
                        run.projectiles.Add(Shell(e, a, e.pspd, e.pr, e.dmg, SprayColor, 0));
                    }
                    break;
                case BodyKind.Egg:
                    for (int i = 0; i < EggRing; i++)
                        run.projectiles.Add(Shell(e, e.castAim + i * 2 * System.Math.PI / EggRing, e.pspd, e.pr, e.dmg, EggColor, 0));
                    break;
                default:   // a kind that casts must say what leaves, or its cast would finish and fire nothing
                    throw new System.InvalidOperationException("Middle.Release: no release for " + e.kind);
            }
        }

        static Projectile Shell(Enemy e, double a, double speed, double r, double dmg, string color, double splitAt) => new Projectile
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

        /// <summary>A lob, once a tick: it flies the straight line to its mark over its flight and only lands
        /// there; the player is hit if they are inside the blast as it lands. True once it has landed.</summary>
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
            var pl = run.player;
            if (Hit.PlayerHit(p.tx, p.ty, p.blastR, pl.lagX, pl.lagY))
            {
                double dx = pl.lagX - p.tx, dy = pl.lagY + Balance.PlayerHitDy - p.ty, d = System.Math.Sqrt(dx * dx + dy * dy);
                if (d == 0) { dx = 0; dy = 1; d = 1; }
                Combat.DamagePlayer(run, p.dmg, dx / d, dy / d, 2.5 * Balance.KnockPGain, false);
            }
            return true;
        }

        /// <summary>The egg: no walking; counts its cooldown, winds up, and lets a ring go (through the
        /// shared cast clock, so a stun still lets a finished cast leave).</summary>
        public static void StepEgg(RunState run, Enemy e, double roomPress)
        {
            if (e.castT > 0 || e.castReady) { TickOrder.FireCommittedShot(run, e, roomPress); return; }
            if (--e.shootCd > 0) return;
            e.castAim = run.rng.Jitter() * 2 * System.Math.PI / EggRing;   // the ring turns a little each volley
            e.castT = EggWind;
        }

        /// <summary>
        /// The Burrower's cycle on <c>move</c>/<c>moveT</c>: walk (slow, hittable), dig (a tell, rooted),
        /// under (buried: no contact, no hits; it runs at the spot the player stood on when it dug), rise
        /// (a tell at that spot, still buried), then up - and the spot is struck as it breaks the floor.
        /// </summary>
        public static void StepBurrower(RunState run, Enemy e, double edx, double edy, double dist, double sm)
        {
            switch (e.move)
            {
                case "dig":
                    if (--e.moveT > 0) return;
                    e.buried = true;
                    e.markX = TickOrder.HitboxX; e.markY = TickOrder.HitboxY + Balance.PlayerHitDy;
                    e.move = "under"; e.moveT = BurrowUnder;
                    return;
                case "under":
                {
                    double mx = e.markX - e.x, my = e.markY - e.y, md = System.Math.Sqrt(mx * mx + my * my), step = BurrowRun * Balance.DepthRate(run.floor) * sm;
                    if (md <= step) { e.x = e.markX; e.y = e.markY; }
                    else { e.x += mx / md * step; e.y += my / md * step; }
                    if (md <= step || --e.moveT <= 0) { e.markX = e.x; e.markY = e.y; e.move = "rise"; e.moveT = BurrowRise; }
                    return;
                }
                case "rise":
                    if (--e.moveT > 0) return;
                    e.buried = false;
                    var pl = run.player;
                    if (Hit.PlayerHit(e.x, e.y, BurrowHitR, pl.lagX, pl.lagY))
                    {
                        double dx = pl.lagX - e.x, dy = pl.lagY + Balance.PlayerHitDy - e.y, d = System.Math.Sqrt(dx * dx + dy * dy);
                        if (d == 0) { dx = 0; dy = 1; d = 1; }
                        Combat.DamagePlayer(run, BurrowDmg, dx / d, dy / d, 6 * Balance.KnockPGain, false);
                    }
                    e.move = "walk"; e.moveT = BurrowSurface;
                    return;
                default:   // "walk", and "idle" on the first step
                    if (e.move != "walk") { e.move = "walk"; e.moveT = BurrowSurface; }
                    double v = BurrowWalk * Balance.DepthRate(run.floor) * sm;   // deeper floors move faster, as every body does
                    e.x += edx / dist * v; e.y += edy / dist * v;
                    if (--e.moveT <= 0) { e.move = "dig"; e.moveT = BurrowDig; }
                    return;
            }
        }
    }
}
