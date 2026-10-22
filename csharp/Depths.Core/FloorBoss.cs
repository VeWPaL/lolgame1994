namespace Depths
{
    /// <summary>The floor boss of floors 1-13 (Balance.TuneFloorBoss): the Gatekeeper. One body, five telegraphed attacks
    /// weighted by area; three in phase 1, a fourth from phase 2, a fifth from phase 3 (docs/DESIGN.md, "The Gatekeeper").</summary>
    public static class FloorBoss
    {
        public const string Name = "THE GATEKEEPER";

        public enum Attack { Charge, Slam, Fan, Ring, Rampage }
        public const int Attacks = 5;

        /// <summary>Whether this floor's boss room holds the Gatekeeper: tuned, and not the final floor.</summary>
        public static bool Spawns(RunState run) => Balance.Tuned && Balance.TuneFloorBoss && !run.finalFloor;

        /// <summary>The kind a boss room on this floor spawns: the Gatekeeper on 1-13 (tuned), else the Warden.</summary>
        public static BodyKind KindFor(RunState run) => Spawns(run) ? BodyKind.Gatekeeper : BodyKind.Boss;

        // ---- tells: every one at least 0.3 s, fixed at every depth (the pace scales the gaps between them, never a tell)
        public static int ChargeTell => Balance.Sec(0.6);    // an orange dashed line along the charge
        public static int RampageTell => Balance.Sec(0.4);   // each of the three charges in a rampage
        public static int FanTell => Balance.Sec(0.5);       // the cast flash ring before a fan
        public static int RingTell => Balance.Sec(0.7);      // the cast flash ring before a ring
        public static int SlamWind => Balance.Sec(0.3);      // the cast flash before the lobs leave
        public static int SlamFlight => Balance.Sec(0.95);   // each lob's landing mark is on the floor this long
        public const double TellMin = 0.3;                    // seconds: the brief's floor, checked by FloorBossTests

        // ---- the moves
        public static double ChargeSpeed => Balance.PerSec(640);
        public const double ChargeMin = 220, ChargeMax = 430, ChargeOver = 110;   // px: the line runs this far past the player
        public static int ChargeRecover => Balance.Sec(0.9);   // it stands winded after a charge: the window to punish it
        public static int Recover => Balance.Sec(0.55);
        public static double Walk => Balance.PerSec(80);
        public const double Standoff = 170;
        public const int SlamN = 3;
        public const double SlamBlast = 50, SlamSpread = 95, SlamDmg = 2;
        public const int FanN = 5;
        public const double FanCone = 0.42, FanDmg = 1, FanR = 7;   // radians either side of the aim
        public static double FanSpeed => Balance.PerSec(330);
        public const int RingN = 16;
        public const double RingDmg = 1, RingR = 7;
        public static double RingSpeed => Balance.PerSec(190);
        public const int RampageN = 3;
        public const string ShellColor = "#7fe0ff", LobColor = "#ffb03a";

        // ---- the cadence: gaps between attacks, divided by the floor's DepthRate
        public static int CdMin => Balance.Sec(1.0);
        public static int CdVar => Balance.Sec(0.7);
        public static int FirstCd => Balance.Sec(1.0);
        public static int PhaseCd => Balance.Sec(0.7);
        public const double Hp = 345;   // x Bodies.Tough, then x DepthTough like every body

        /// <summary>Weights by area (rows: Shallows, Kiln Works, Flooded Galleries, the Deep) for Charge, Slam, Fan, Ring,
        /// Rampage. Each area leans on one gimmick; the Deep weighs them evenly.</summary>
        public static readonly double[][] Weights =
        {
            new double[] { 4, 1, 2, 2, 2 },   // the Shallows: the charge
            new double[] { 1, 4, 2, 2, 2 },   // the Kiln Works: the slam's lobs
            new double[] { 1, 1, 4, 3, 2 },   // the Flooded Galleries: shells
            new double[] { 2, 2, 2, 2, 2 },   // the Deep: everything
        };
        public const double RepeatWeight = 0.35;   // the attack just used weighs this share: the same twice is rare

        /// <summary>How many attacks a phase has: three, then one more each phase.</summary>
        public static int Unlocked(int phase) => phase >= 3 ? 5 : phase == 2 ? 4 : 3;

        /// <summary>The attack a roll in [0,1) picks for an area and phase, the last attack weighed down.</summary>
        public static Attack Pick(Area area, int phase, int last, double roll)
        {
            var w = Weights[(int)area];
            int n = Unlocked(phase);
            double sum = 0;
            for (int i = 0; i < n; i++) sum += i == last ? w[i] * RepeatWeight : w[i];
            double at = roll * sum;
            for (int i = 0; i < n; i++)
            {
                at -= i == last ? w[i] * RepeatWeight : w[i];
                if (at < 0) return (Attack)i;
            }
            return (Attack)(n - 1);
        }

        public static void Init(Enemy e, double rate)
        {
            e.phase = 1; e.move = "idle"; e.moveT = 0; e.bossCd = FirstCd;
            e.aggroTimer = 9999;
            e.cdMin = CdMin / rate; e.cdVar = CdVar / rate;
            e.speed = Walk * rate;
            e.fbLast = -1; e.fbLeft = 0; e.castT = 0;
        }

        /// <summary>Whether the Gatekeeper's body hurts on contact this tick: only on a tick it charged, after its line was shown.</summary>
        public static bool Hurts(Enemy e) => e.fbHot;

        /// <summary>One tick of the Gatekeeper: the phase check, then the move it is in, or the walk and the cooldown to the next.</summary>
        public static void Step(RunState run, Enemy e, double edx, double edy, double dist, double sm)
        {
            e.fbHot = false;
            double frac = e.hp / e.maxHp;
            if (e.phase == 1 && frac <= Balance.BossPhase1) Phase(e, 2);
            else if (e.phase == 2 && frac <= Balance.BossPhase2) Phase(e, 3);
            switch (e.move)
            {
                case "tell": StepTell(run, e); return;
                case "charge": StepCharge(run, e, sm); return;
                case "cast": StepCast(run, e); return;
                case "rest": if (--e.moveT <= 0) e.move = "idle"; return;
            }
            // idle: close to its standoff at a walk, then the next attack when the gap runs out
            if (dist > Standoff)
            {
                double v = e.speed * sm;
                e.x += edx / dist * v; e.y += edy / dist * v;
            }
            if ((e.bossCd -= 1) > 0) return;
            var a = Pick(AreaRules.AreaForFloor(run.floor), e.phase, e.fbLast, run.rng.Run());
            double over = e.bossCd;
            e.bossCd = e.cdMin + run.rng.Jitter() * e.cdVar;
            if (Balance.TickHz != Balance.JsHz) e.bossCd += over;
            Begin(run, e, a);
        }

        static void Phase(Enemy e, int n)
        {
            e.phase = n;
            e.move = "rest"; e.moveT = Recover; e.castT = 0; e.fbLeft = 0;   // the move it was in is called off, its warning too
            e.bossCd = PhaseCd;
        }

        public static void Begin(RunState run, Enemy e, Attack a)
        {
            e.fbLast = (int)a;
            e.fbAttack = (int)a;
            switch (a)
            {
                case Attack.Charge: Tell(run, e, ChargeTell); break;
                case Attack.Rampage: e.fbLeft = RampageN - 1; Tell(run, e, RampageTell); break;
                case Attack.Slam: Cast(e, SlamWind); break;
                case Attack.Fan: Cast(e, FanTell); break;
                case Attack.Ring: Cast(e, RingTell); break;
            }
        }

        // a charge's tell: the line is locked now, at the player's hitbox and on past it, and drawn for the whole tell
        static void Tell(RunState run, Enemy e, int ticks)
        {
            double hx = TickOrder.HitboxX - e.x, hy = TickOrder.HitboxY + Balance.PlayerHitDy - e.y, d = System.Math.Sqrt(hx * hx + hy * hy);
            if (d < 1) { hx = 0; hy = 1; d = 1; }
            e.fbDx = hx / d; e.fbDy = hy / d;
            e.fbLen = WallClip(e, System.Math.Max(ChargeMin, System.Math.Min(ChargeMax, d + ChargeOver)));
            e.move = "tell"; e.moveT = ticks; e.fbTellMax = ticks;
        }

        // the line stops at the wall, so the drawn line is the ground it covers
        static double WallClip(Enemy e, double len)
        {
            double lo = Balance.RoomLeft + e.r, hi = Balance.RoomRight - e.r, top = Balance.RoomTop + e.r, bot = Balance.RoomBottom - e.r;
            if (e.fbDx > 1e-9) len = System.Math.Min(len, (hi - e.x) / e.fbDx);
            if (e.fbDx < -1e-9) len = System.Math.Min(len, (lo - e.x) / e.fbDx);
            if (e.fbDy > 1e-9) len = System.Math.Min(len, (bot - e.y) / e.fbDy);
            if (e.fbDy < -1e-9) len = System.Math.Min(len, (top - e.y) / e.fbDy);
            return System.Math.Max(0, len);
        }

        static void StepTell(RunState run, Enemy e)
        {
            if (--e.moveT > 0) return;
            e.move = "charge";
            e.fbGone = 0;
            e.moveT = System.Math.Max(1, (int)System.Math.Ceiling(e.fbLen / ChargeSpeed));
        }

        static void StepCharge(RunState run, Enemy e, double sm)
        {
            e.fbHot = true;
            double step = System.Math.Min(ChargeSpeed, e.fbLen - e.fbGone);
            e.x += e.fbDx * step; e.y += e.fbDy * step;
            e.fbGone += step;
            if (--e.moveT > 0 && e.fbGone < e.fbLen - 1e-9) return;
            if (e.fbLeft > 0) { e.fbLeft--; Tell(run, e, RampageTell); return; }
            e.move = "rest"; e.moveT = ChargeRecover;
        }

        static void Cast(Enemy e, int ticks)
        {
            e.move = "cast"; e.castT = ticks; e.fbTellMax = ticks; e.castReady = false;
        }

        static void StepCast(RunState run, Enemy e)
        {
            if (--e.castT > 0) return;
            e.castT = 0;
            var p = run.player;
            double hx = TickOrder.HitboxX, hy = TickOrder.HitboxY + Balance.PlayerHitDy;
            double aim = System.Math.Atan2(hy - e.y, hx - e.x);
            switch ((Attack)e.fbAttack)
            {
                case Attack.Slam:
                {
                    // one on the player, two either side across the line from the boss: a step along it is safe, a step across is not
                    double nx = -System.Math.Sin(aim), ny = System.Math.Cos(aim);
                    for (int i = 0; i < SlamN; i++)
                    {
                        double k = i - (SlamN - 1) / 2.0, j = (run.rng.Jitter() - 0.5) * 30;
                        double tx = Clamp(hx + nx * k * SlamSpread + j, Balance.RoomLeft + SlamBlast * 0.5, Balance.RoomRight - SlamBlast * 0.5);
                        double ty = Clamp(hy + ny * k * SlamSpread + j, Balance.RoomTop + SlamBlast * 0.5, Balance.RoomBottom - SlamBlast * 0.5);
                        run.projectiles.Add(new Projectile
                        {
                            x = e.x, y = e.y, ox = e.x, oy = e.y, tx = tx, ty = ty, lob = true, flightMax = SlamFlight,
                            r = 8, dmg = SlamDmg, blastR = SlamBlast, friendly = false, owner = e, color = LobColor,
                        });
                    }
                    break;
                }
                case Attack.Fan:
                    for (int i = 0; i < FanN; i++)
                        run.projectiles.Add(Middle.Shell(e, aim + FanCone * (2.0 * i / (FanN - 1) - 1), FanSpeed, FanR, FanDmg, ShellColor, 0));
                    break;
                case Attack.Ring:
                {
                    double a0 = run.rng.Jitter() * 2 * System.Math.PI / RingN;
                    for (int i = 0; i < RingN; i++)
                        run.projectiles.Add(Middle.Shell(e, a0 + 2 * System.Math.PI * i / RingN, RingSpeed, RingR, RingDmg, ShellColor, 0));
                    break;
                }
            }
            e.move = "rest"; e.moveT = Recover;
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
