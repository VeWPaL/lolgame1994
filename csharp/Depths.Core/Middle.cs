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
        // named, not "k >= Splitter": a kind appended later must not land in Release by enum order
        public static bool Is(BodyKind k) => k == BodyKind.Splitter || k == BodyKind.Mortar || k == BodyKind.Burrower || k == BodyKind.Sprayer || k == BodyKind.Egg;

        /// <summary>Never moved by the hook's yank or its field: the egg is a fixture the room is built around.</summary>
        public static bool Anchored(BodyKind k) => k == BodyKind.Egg;

        /// <summary>Walks a standoff and casts at an intercept, as a shooter does.</summary>
        public static bool Ranged(BodyKind k) => k == BodyKind.Splitter || k == BodyKind.Mortar || k == BodyKind.Sprayer;

        /// <summary>How long the cast tell runs before the release: the Sprayer's long wind-up is its whole design.</summary>
        public static int CastTime(BodyKind k) => k == BodyKind.Sprayer ? SprayWind : k == BodyKind.Egg ? EggWind : Balance.CastTime;

        /// <summary>How much of the shooter's aim jitter a kind keeps: the Splitter and the Sprayer aim tighter (owner
        /// 2026-10-13: "a little better aimed"). 1 for the shooter and the gunner, so their shots are the JS's.</summary>
        public static double AimJitter(BodyKind k) => Sharp && (k == BodyKind.Splitter || k == BodyKind.Sprayer) ? 0.5 : 1;

        /// <summary>The 2026-10-13 retune of the five (Balance.TuneMiddle): every changed dial and behaviour below
        /// reads this, so the bot can measure it apart and the lab-era bodies are one switch away.</summary>
        public static bool Sharp => Balance.Tuned && Balance.TuneMiddle;

        // ---- Splitter: a slow shell that bursts into shards at a set range; punishes standing still ----
        public static double SplitRange => Sharp ? 120 : 170;      // px from the muzzle: it splits sooner
        public const int SplitCount = 3;
        public static double SplitSpread => Sharp ? 0.30 : 0.42;   // radians either side of the heading
        public static double SplitShardSpeed => Balance.PerSec(380);
        public const double SplitShardR = 4, SplitShardDmg = 1;

        // ---- Mortar: lobs where you are going, led like a Lunger's lunge; the marker shows it the whole flight,
        // so turning beats it and running straight on does not ----
        public static int LobFlight => Balance.Sec(1.0);
        public static double LobBlast => Sharp ? 56 : 34;          // px: "significantly wider"
        public const double LobDmg = 2;

        // ---- Sprayer: a long, visible wind-up, then a short cone of weak shots, aimed as it leaves ----
        public static int SprayWind => Balance.Sec(Sharp ? 0.85 : 1.0);
        public const int SprayCount = 5;
        public static double SprayCone => Sharp ? 0.26 : 0.35;     // radians either side
        public const double SprayCadence = 0.82;                   // x its cooldown when Sharp (Spawn.Body)

        // ---- Turret egg: never walks; a slow ring every few seconds shapes the room. Two variants (Eggs.cs): one
        // circling a body (a smaller, slower ring) and, in the Kiln Works, one riding a rail ----
        public static int EggWind => Balance.Sec(0.6);
        public const int EggRing = 8;

        // ---- Burrower: walks, digs in, chases you underground to where a straight line would take you, a tell,
        // then up under that spot: running straight is what it punishes ----
        public static double BurrowWalk => Balance.PerSec(66);
        public static double BurrowRun => Balance.PerSec(Sharp ? 430 : 300);   // fast enough to catch a runner
        public static int BurrowSurface => Balance.Sec(1.6);
        public static int BurrowDig => Balance.Sec(0.45);
        public static int BurrowUnder => Balance.Sec(Sharp ? 1.8 : 1.2);   // the longest it stays down; it surfaces where it got to
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
                    // where the player will be when it lands, if they keep going as they believe they are (the Lunger's
                    // lead): the marker shows that spot for the whole flight, so a turn beats it
                    var (tx, ty) = Sharp ? Lead(run, LobFlight) : (TickOrder.HitboxX, TickOrder.HitboxY + Balance.PlayerHitDy);
                    double m = LobBlast * 0.5;
                    tx = System.Math.Max(Balance.RoomLeft + m, System.Math.Min(Balance.RoomRight - m, tx));
                    ty = System.Math.Max(Balance.RoomTop + m, System.Math.Min(Balance.RoomBottom - m, ty));
                    run.projectiles.Add(new Projectile
                    {
                        x = e.x, y = e.y, ox = e.x, oy = e.y, tx = tx, ty = ty, lob = true, flightMax = LobFlight,
                        r = e.pr, dmg = LobDmg, blastR = LobBlast, friendly = false, owner = e, color = LobColor,
                    });
                    break;
                }
                case BodyKind.Sprayer:
                {
                    // aimed again as it leaves (the wind-up showed THAT it fires, not where), then swung off its own
                    if (!Sharp)   // the lab-era cone: the aim the cast began with, no check for its own
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
                        if (!clear.HasValue && !Movement.LineClear(run, e, a, SprayReach, e.pr)) continue;   // no clear cone: hold the shells that would hit a friend
                        run.projectiles.Add(Shell(e, a, e.pspd, e.pr, e.dmg, SprayColor, 0));
                    }
                    break;
                }
                case BodyKind.Egg:
                    Eggs.Ring(run, e);
                    break;
                default:   // a kind that casts must say what leaves, or its cast would finish and fire nothing
                    throw new System.InvalidOperationException("Middle.Release: no release for " + e.kind);
            }
        }

        /// <summary>The Lunger's kind of lead (Movement.LungeIntercept: trusted by how settled the player looks,
        /// 1 - swerve), <paramref name="ticks"/> ahead of the hitbox - but on the player's velocity now, not the
        /// believed heading at walking speed: the belief keeps its last direction after a stop, so a player who
        /// had ever moved could stand still and never be hit (the cold critic, 2026-10-13).</summary>
        public static (double x, double y) Lead(RunState run, double ticks)
        {
            var p = run.player;
            double k = (1 - p.swerve) * ticks;
            return (TickOrder.HitboxX + p.vx * k, TickOrder.HitboxY + Balance.PlayerHitDy + p.vy * k);
        }

        const double SprayReach = 360;   // px: how far along a line the Sprayer checks for its own (its Range)

        // the Sprayer's release aim: an intercept on the player's velocity for the flight, a halved jitter
        static double SprayAim(RunState run, Enemy e)
        {
            var p = run.player;
            double conf = System.Math.Max(0, 1 - p.swerve * Balance.SwerveTrustRooted);
            double hx = TickOrder.HitboxX, hy = TickOrder.HitboxY + Balance.PlayerHitDy, sx = hx - e.x, sy = hy - e.y;
            for (int k = 0; k < 8; k++)
            {
                double t = System.Math.Sqrt(sx * sx + sy * sy) / e.pspd;
                sx = hx + p.vx * conf * t - e.x; sy = hy + p.vy * conf * t - e.y;
            }
            double dist = System.Math.Sqrt((hx - e.x) * (hx - e.x) + (hy - e.y) * (hy - e.y));
            return System.Math.Atan2(sy, sx) + (run.rng.Jitter() - 0.5) * 2 * Aim.GunSpread(p.swerve, dist) * AimJitter(e.kind);
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
            Eggs.Move(run, e);
            if (e.castT > 0 || e.castReady) { TickOrder.FireCommittedShot(run, e, roomPress); return; }
            if (--e.shootCd > 0) return;
            e.castAim = run.rng.Jitter() * 2 * System.Math.PI / EggRing;   // the ring turns a little each volley
            e.castT = EggWind;
        }

        /// <summary>
        /// Where a Burrower under the floor heads: the spot the player reaches, at their present velocity (trusted
        /// by how settled they look), by the time it has got there and risen - an intercept, solved as the Lunger's
        /// lunge is. It arrives as they do: a runner meets it, a turn on the tell escapes it. Inside the room.
        /// </summary>
        public static void BurrowMark(RunState run, Enemy e, double step)
        {
            var p = run.player;
            double conf = 1 - p.swerve, vx = p.vx * conf, vy = p.vy * conf;
            double hx = TickOrder.HitboxX, hy = TickOrder.HitboxY + Balance.PlayerHitDy, lx = hx, ly = hy, rm = e.r + 4;
            for (int k = 0; k < 6; k++)
            {
                double t = System.Math.Sqrt((lx - e.x) * (lx - e.x) + (ly - e.y) * (ly - e.y)) / System.Math.Max(1e-9, step) + BurrowRise;
                lx = System.Math.Max(Balance.RoomLeft + rm, System.Math.Min(Balance.RoomRight - rm, hx + vx * t));
                ly = System.Math.Max(Balance.RoomTop + rm, System.Math.Min(Balance.RoomBottom - rm, hy + vy * t));
            }
            e.markX = lx; e.markY = ly;
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
                    // the chase: every tick it re-aims at where a straight line takes the player by the time it is there
                    // and has risen, so it surfaces ahead of a runner and under a stander
                    double step = BurrowRun * Balance.DepthRate(run.floor) * sm;
                    if (Sharp) BurrowMark(run, e, step);   // else: the spot the player stood on when it dug
                    double mx = e.markX - e.x, my = e.markY - e.y, md = System.Math.Sqrt(mx * mx + my * my);
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
