namespace Depths
{
    /// <summary>S5 Old Mudmouth (docs/bosses.md): a giant Burrower, the first region boss. Untouchable underground; every
    /// leap and ring is told by a crack, and a blast on the crack unearths it (stunned on the surface at double damage).</summary>
    public static class Mudmouth
    {
        public const string Name = "OLD MUDMOUTH";

        public enum Attack { Leap, Spit, Ring, Double, Tunnel, Brood }
        public const int Attacks = 6;
        public static readonly string[] Keys = { "mudmouth:leap", "mudmouth:spit", "mudmouth:ring", "mudmouth:double", "mudmouth:tunnel", "mudmouth:brood" };
        public static string Key(Attack a) => Keys[(int)a];

        // ---- tells: every one at least TellMin, fixed at every depth
        public const double TellMin = 0.35;
        public static int CrackTell => Balance.Sec(0.72);   // the floor cracks over it: the blast's window, and the leap's or ring's tell
        public static int LeapTime => Balance.Sec(0.6);     // in the air: its landing mark fills the whole flight
        public static int SpitWind => Balance.Sec(0.45);    // the cast flash before the mud leaves
        public static int SpitFlight => Balance.Sec(0.9);   // each lob's landing mark is on the floor this long
        public static int TunnelTell => Balance.Sec(0.7);   // a raised dirt line along the tunnel before it bursts through
        public static int BroodWind => Balance.Sec(0.7);    // the cast flash before its brood digs out
        public static int ShiftTime => Balance.Sec(2.4);    // a phase change: it surfaces and roars, the room changes
        public static int DeathBeat => Balance.Sec(1.5);

        // ---- the moves
        public static int DigTime => Balance.Sec(0.45);
        public static int UnderMin => Balance.Sec(0.4);
        public static int UnderMax => Balance.Sec(1.6);
        public static double UnderSpeed => Balance.PerSec(230);
        public const double LeapRange = 300, RingRange = 170;   // px from the player: close enough, it cracks the floor
        public const double LeapR = 58, LeapDmg = 2, LeapMax = 380, LeapLead = 0.5;   // lead: share of the player's flight-long run it trusts
        public static int LandRest => Balance.Sec(1.0);     // on the surface after a landing: the window to shoot it
        public static int StunTime => Balance.Sec(2.0);
        public const double StunTaken = 2;                  // damage taken while unearthed
        public const int SpitN = 3;
        public const double SpitBlast = 46, SpitSpread = 90, SpitDmg = 2;
        public const int RingN = 12;
        public const double RingDmg = 1, ShellR = 7, RingSlow = 0.7;
        public const int LandN = 10;   // shells a landing throws
        public static double LandSpeed => Balance.PerSec(180);
        public static double RingSpeed => Balance.PerSec(200);
        public static double TunnelSpeed => Balance.PerSec(620);
        public const double TunnelMin = 260, TunnelMax = 480, TunnelOver = 140, TunnelR = 34, TunnelDmg = 2;
        public const double BroodApart = 80;
        public const int BroodCap = 2;
        public static int AddWake => Balance.Sec(0.5);
        public static double Walk => Balance.PerSec(70);
        public const double Standoff = 200;
        public static int Recover => Balance.Sec(0.35);
        public const string ShellColor = "#c9a46a", LobColor = "#8a6a3a";

        // ---- the ground: from phase 2 the mud it passes under stays soft and slows the player
        public static int MudLife => Balance.Sec(3.0);
        public const double MudR = 30, MudStep = 26, MudSlow = 0.6;

        // ---- the cadence: gaps between attacks, divided by the floor's DepthRate
        public static int CdMin => Balance.Sec(0.16);
        public static int CdVar => Balance.Sec(0.3);
        public static int FirstCd => Balance.Sec(1.0);
        /// <summary>Its share of its area's flat health (Bosses.AreaHp), tuned with the bot to the 150-180 s band.</summary>
        public const double HpScale = 1.35;

        /// <summary>Weights by phase for Leap, Spit, Ring, Double, Tunnel, Brood; 0 is not in that phase.</summary>
        public static readonly double[][] Weights =
        {
            new double[] { 3, 1.5, 1.7, 0, 0, 0 },
            new double[] { 1.5, 1.3, 1.3, 2.3, 0, 0 },
            new double[] { 1, 1, 1, 1.6, 3, 0.8 },
        };
        public const double RepeatWeight = 0.3;

        public static Attack Pick(int phase, int last, double roll, bool canBrood = true)
        {
            var w = Weights[System.Math.Max(1, System.Math.Min(3, phase)) - 1];
            double W(int i) => i == (int)Attack.Brood && !canBrood ? 0 : i == last ? w[i] * RepeatWeight : w[i];
            double sum = 0;
            for (int i = 0; i < Attacks; i++) sum += W(i);
            double at = roll * sum;
            int pick = 0;
            for (int i = 0; i < Attacks; i++)
            {
                double wi = W(i);
                if (wi <= 0) continue;
                pick = i;
                at -= wi;
                if (at < 0) break;
            }
            return (Attack)pick;
        }

        /// <summary>Its soft mud: a ring of patches, each live for MudLife ticks.</summary>
        public sealed class Mud
        {
            public const int Max = 96;
            public readonly double[] x = new double[Max], y = new double[Max];
            public readonly int[] life = new int[Max];
            public int next;
            public double lastX = double.NaN, lastY = double.NaN;

            public void Lay(double px, double py)
            {
                if (!double.IsNaN(lastX) && (px - lastX) * (px - lastX) + (py - lastY) * (py - lastY) < MudStep * MudStep) return;
                x[next] = px; y[next] = py; life[next] = MudLife;
                next = (next + 1) % Max;
                lastX = px; lastY = py;
            }

            public void Tick() { for (int i = 0; i < Max; i++) if (life[i] > 0) life[i]--; }

            public bool At(double px, double py)
            {
                for (int i = 0; i < Max; i++)
                    if (life[i] > 0 && (px - x[i]) * (px - x[i]) + (py - y[i]) * (py - y[i]) <= MudR * MudR) return true;
                return false;
            }

            public void Clear() { System.Array.Clear(life, 0, Max); lastX = lastY = double.NaN; }
        }

        public static void Init(RunState run, Enemy e, double rate)
        {
            e.hp = e.maxHp = Bosses.Health(run.floor, HpScale);
            e.phase = 1; e.move = "idle"; e.moveT = 0; e.bossCd = FirstCd;
            e.aggroTimer = 9999;
            e.cdMin = CdMin / rate; e.cdVar = CdVar / rate;
            e.speed = Walk * rate;
            e.curSpeed = UnderSpeed * rate;
            e.fbLast = -1; e.fbLeft = 0; e.castT = 0;
            e.mud = new Mud();
            e.takenMult = 1;
        }

        /// <summary>The player's speed share from its mud: MudSlow on a live patch, else 1 (Tick.TickPlayer).</summary>
        public static double SlowAt(RunState run)
        {
            var p = run.player;
            foreach (var e in run.enemies)
                if (e.mud != null && e.hp > 0 && e.mud.At(p.x, p.y + Balance.PlayerHitDy)) return MudSlow;
            return 1;
        }

        public static int BroodAlive(RunState run)
        {
            int n = 0;
            foreach (var o in run.enemies) if (o.rbAdd && o.hp > 0) n++;
            return n;
        }

        /// <summary>Its clocks alone while a knock or a hook stuns it (Tick's stun branch): the mud dries, the unearthing runs out.</summary>
        public static void StepGround(Enemy e)
        {
            e.mud?.Tick();
            if (e.move == "stunned" && e.moveT > 1) e.moveT--;   // a knock's stun does not lengthen its time unearthed
        }

        /// <summary>One tick: the mud, the phase check (not while unearthed), then its move or the walk and the next attack.</summary>
        public static void Step(RunState run, Enemy e, double edx, double edy, double dist, double sm)
        {
            e.mud?.Tick();
            if (e.move != "stunned")
            {
                double frac = e.hp / e.maxHp;
                if (e.phase == 1 && frac <= Balance.BossPhase1) Shift(run, e, 2);
                else if (e.phase == 2 && frac <= Balance.BossPhase2) Shift(run, e, 3);
            }
            switch (e.move)
            {
                case "shift": if (--e.moveT <= 0) { e.move = "idle"; e.bossCd = System.Math.Min(e.bossCd, Balance.Sec(0.3)); } return;
                case "stunned":
                    if (--e.moveT > 0) return;
                    e.takenMult = 1; e.move = "idle"; e.bossCd = System.Math.Min(e.bossCd, Balance.Sec(0.3));
                    return;
                case "dig": StepDig(run, e); return;
                case "under": StepUnder(run, e, sm); return;
                case "crack":
                    if (--e.moveT > 0) return;
                    if ((Attack)e.fbAttack == Attack.Ring) Burst(run, e); else Leap(run, e);
                    return;
                case "leap": StepLeap(run, e); return;
                case "dirt": if (--e.moveT <= 0) { e.move = "tunnel"; e.fbGone = 0; e.fbHot = false; } return;
                case "tunnel": StepTunnel(run, e); return;
                case "cast": StepCast(run, e); return;
                case "rest": if (--e.moveT <= 0) e.move = "idle"; return;
            }
            if (dist > Standoff)
            {
                double v = e.speed * sm;
                e.x += edx / dist * v; e.y += edy / dist * v;
            }
            if (FloorBoss.LobsLive(run, e)) return;   // one attack at a time: never a new tell over its own live marks
            if ((e.bossCd -= 1) > 0) return;
            var a = Pick(e.phase, e.fbLast, run.rng.Run(), BroodAlive(run) == 0);
            double over = e.bossCd;
            e.bossCd = e.cdMin + run.rng.Jitter() * e.cdVar;
            if (Balance.TickHz != Balance.JsHz) e.bossCd += over;
            Begin(run, e, a);
        }

        // a phase change: the move in hand is called off (it surfaces where it is), and the room changes with the phase
        static void Shift(RunState run, Enemy e, int n)
        {
            e.phase = n;
            e.buried = false; e.fbHot = false;
            e.move = "shift"; e.moveT = ShiftTime; e.castT = 0; e.fbLeft = 0; e.takenMult = 1;
            e.bossCd = Balance.Sec(0.3);
            Movement.Clamp(e);
            run.thuds++;
        }

        public static void Begin(RunState run, Enemy e, Attack a)
        {
            e.fbLast = (int)a;
            e.fbAttack = (int)a;
            switch (a)
            {
                case Attack.Spit: Cast(e, SpitWind); break;
                case Attack.Brood: Cast(e, BroodWind); break;
                default:
                    e.fbLeft = a == Attack.Double ? 1 : 0;
                    e.move = "dig"; e.moveT = DigTime;
                    break;
            }
        }

        static void Cast(Enemy e, int ticks) { e.move = "cast"; e.castT = ticks; e.fbTellMax = ticks; e.castReady = false; }

        static void StepDig(RunState run, Enemy e)
        {
            if (--e.moveT > 0) return;
            e.buried = true;
            if ((Attack)e.fbAttack == Attack.Tunnel) { AimTunnel(e); return; }
            e.move = "under"; e.moveT = UnderMax;
        }

        static void StepUnder(RunState run, Enemy e, double sm)
        {
            double hx = TickOrder.HitboxX - e.x, hy = TickOrder.HitboxY + Balance.PlayerHitDy - e.y, d = System.Math.Sqrt(hx * hx + hy * hy);
            double v = e.curSpeed * sm;
            if (d > 1) { double s = System.Math.Min(v, d); e.x += hx / d * s; e.y += hy / d * s; }
            Movement.Clamp(e);
            if (e.phase >= 2) e.mud!.Lay(e.x, e.y);
            --e.moveT;
            double range = (Attack)e.fbAttack == Attack.Ring ? RingRange : LeapRange;
            if ((d <= range && UnderMax - e.moveT >= UnderMin) || e.moveT <= 0) { e.move = "crack"; e.moveT = e.fbTellMax = CrackTell; }
        }

        /// <summary>A blast went off at (x, y): a Mudmouth cracking the floor within its reach is knocked out of the ground.</summary>
        public static void Blasted(RunState run, double x, double y)
        {
            foreach (var e in run.enemies)
            {
                if (e.kind != BodyKind.Mudmouth || e.hp <= 0 || e.move != "crack") continue;
                double dx = x - e.x, dy = y - e.y;
                if (dx * dx + dy * dy < (Balance.AltAoe + e.r) * (Balance.AltAoe + e.r)) Unearth(run, e);
            }
        }

        public static void Unearth(RunState run, Enemy e)
        {
            e.buried = false; e.fbLeft = 0;
            e.move = "stunned"; e.moveT = StunTime; e.takenMult = StunTaken;
            e.unearthed++;
            run.thuds++;
        }

        // out of the crack and into the air: the landing is where the player is, with half their run over the flight trusted
        static void Leap(RunState run, Enemy e)
        {
            var p = run.player;
            double hx = p.x + p.vx * LeapTime * LeapLead, hy = p.y + Balance.PlayerHitDy + p.vy * LeapTime * LeapLead;
            double dx = hx - e.x, dy = hy - e.y, d = System.Math.Sqrt(dx * dx + dy * dy);
            if (d > LeapMax) { dx *= LeapMax / d; dy *= LeapMax / d; }
            double m = LeapR * 0.6;
            e.leapX = e.x; e.leapY = e.y;
            e.markX = Clamp(e.x + dx, Balance.RoomLeft + m, Balance.RoomRight - m);
            e.markY = Clamp(e.y + dy, Balance.RoomTop + m, Balance.RoomBottom - m);
            e.move = "leap"; e.moveT = e.fbTellMax = LeapTime;
        }

        static void StepLeap(RunState run, Enemy e)
        {
            double t = 1 - (e.moveT - 1) / (double)System.Math.Max(1, e.fbTellMax);
            e.x = e.leapX + (e.markX - e.leapX) * t; e.y = e.leapY + (e.markY - e.leapY) * t;
            if (--e.moveT > 0) return;
            e.x = e.markX; e.y = e.markY;
            var key = Key((Attack)e.fbAttack);
            Strike(run, e.x, e.y, LeapR, LeapDmg, key, 5);
            double a0 = run.rng.Jitter() * 2 * System.Math.PI / LandN;   // the landing throws mud out from its mark
            for (int i = 0; i < LandN; i++)
            {
                var s = Middle.Shell(e, a0 + 2 * System.Math.PI * i / LandN, LandSpeed, ShellR, RingDmg, ShellColor, 0);
                s.book = key;
                run.projectiles.Add(s);
            }
            run.thuds++;
            if (e.phase >= 2) e.mud!.Lay(e.x, e.y);
            if (e.fbLeft > 0) { e.fbLeft--; Leap(run, e); return; }   // the double: the second mark drawn from this landing
            e.buried = false;
            e.move = "rest"; e.moveT = LandRest;
        }

        // the ring: it bursts up where it cracked, throwing a slow ring of mud out from itself
        static void Burst(RunState run, Enemy e)
        {
            e.buried = false;
            double a0 = run.rng.Jitter() * 2 * System.Math.PI / RingN;
            int waves = e.phase >= 3 ? 2 : 1;   // in phase 3 a slower second ring behind, turned half a gap
            for (int w = 0; w < waves; w++)
                for (int i = 0; i < RingN; i++)
                {
                    var s = Middle.Shell(e, a0 + 2 * System.Math.PI * (i + 0.5 * w) / RingN, RingSpeed * (w == 0 ? 1 : RingSlow), ShellR, RingDmg, ShellColor, 0);
                    s.book = Key(Attack.Ring);
                    run.projectiles.Add(s);
                }
            run.thuds++;
            e.move = "rest"; e.moveT = LandRest;
        }

        // the tunnel's line: locked now from where it dug, through the player and past them, stopped by the wall
        static void AimTunnel(Enemy e)
        {
            double hx = TickOrder.HitboxX - e.x, hy = TickOrder.HitboxY + Balance.PlayerHitDy - e.y, d = System.Math.Sqrt(hx * hx + hy * hy);
            if (d < 1) { hx = 0; hy = 1; d = 1; }
            e.fbDx = hx / d; e.fbDy = hy / d;
            e.fbLen = WallClip(e, System.Math.Max(TunnelMin, System.Math.Min(TunnelMax, d + TunnelOver)));
            e.leapX = e.x; e.leapY = e.y;
            e.move = "dirt"; e.moveT = e.fbTellMax = TunnelTell;
        }

        static double WallClip(Enemy e, double len)
        {
            double lo = Balance.RoomLeft + e.r, hi = Balance.RoomRight - e.r, top = Balance.RoomTop + e.r, bot = Balance.RoomBottom - e.r;
            if (e.fbDx > 1e-9) len = System.Math.Min(len, (hi - e.x) / e.fbDx);
            if (e.fbDx < -1e-9) len = System.Math.Min(len, (lo - e.x) / e.fbDx);
            if (e.fbDy > 1e-9) len = System.Math.Min(len, (bot - e.y) / e.fbDy);
            if (e.fbDy < -1e-9) len = System.Math.Min(len, (top - e.y) / e.fbDy);
            return System.Math.Max(0, len);
        }

        // along the line under the floor, the dirt erupting at its head (fbHot once it has hit): it bursts out at the end
        static void StepTunnel(RunState run, Enemy e)
        {
            double step = System.Math.Min(TunnelSpeed, e.fbLen - e.fbGone);
            e.x += e.fbDx * step; e.y += e.fbDy * step;
            e.fbGone += step;
            if (e.phase >= 2) e.mud!.Lay(e.x, e.y);
            if (!e.fbHot && Strike(run, e.x, e.y, TunnelR, TunnelDmg, Key(Attack.Tunnel), 4)) e.fbHot = true;
            if (e.fbGone < e.fbLen - 1e-9) return;
            e.buried = false; e.fbHot = false;
            run.thuds++;
            e.move = "rest"; e.moveT = LandRest;
        }

        static void StepCast(RunState run, Enemy e)
        {
            if (--e.castT > 0) return;
            e.castT = 0;
            double hx = TickOrder.HitboxX, hy = TickOrder.HitboxY + Balance.PlayerHitDy;
            double aim = System.Math.Atan2(hy - e.y, hx - e.x);
            if ((Attack)e.fbAttack == Attack.Spit)
            {
                // one on the player, two either side across the line from it: a step along the line is safe
                double nx = -System.Math.Sin(aim), ny = System.Math.Cos(aim), ax = System.Math.Cos(aim), ay = System.Math.Sin(aim);
                int n = e.phase >= 2 ? SpitN + 2 : SpitN;   // from phase 2 two more along the line: a cross, the diagonals safe
                for (int i = 0; i < n; i++)
                {
                    double k = i < SpitN ? i - (SpitN - 1) / 2.0 : 0, along = i < SpitN ? 0 : i == SpitN ? -1 : 1, j = (run.rng.Jitter() - 0.5) * 30;
                    double tx = Clamp(hx + nx * k * SpitSpread + ax * along * SpitSpread + j, Balance.RoomLeft + SpitBlast * 0.5, Balance.RoomRight - SpitBlast * 0.5);
                    double ty = Clamp(hy + ny * k * SpitSpread + ay * along * SpitSpread + j, Balance.RoomTop + SpitBlast * 0.5, Balance.RoomBottom - SpitBlast * 0.5);
                    run.projectiles.Add(new Projectile
                    {
                        x = e.x, y = e.y, ox = e.x, oy = e.y, tx = tx, ty = ty, lob = true, flightMax = SpitFlight,
                        r = 9, dmg = SpitDmg, blastR = SpitBlast, friendly = false, owner = e, color = LobColor, book = Key(Attack.Spit),
                    });
                }
            }
            else
            {
                // the brood: two small Burrowers dig out beside it, across the line to the player
                double nx = -System.Math.Sin(aim), ny = System.Math.Cos(aim);
                for (int s = -1; s <= 1; s += 2)
                {
                    double bx = Clamp(e.x + nx * s * BroodApart, Balance.RoomLeft + 20, Balance.RoomRight - 20);
                    double by = Clamp(e.y + ny * s * BroodApart, Balance.RoomTop + 20, Balance.RoomBottom - 20);
                    var b = Spawn.Body(run, BodyKind.Burrower, bx, by);
                    b.rbAdd = true; b.book = Key(Attack.Brood);
                    b.noticeTimer = AddWake; b.alerted = true;
                    run.enemies.Add(b);
                }
            }
            e.move = "rest"; e.moveT = Recover;
        }

        /// <summary>A strike on the player's lagged hitbox within r, booked to `key`; true when it was in reach.</summary>
        static bool Strike(RunState run, double x, double y, double r, double dmg, string key, double knock)
        {
            var pl = run.player;
            if (!Hit.PlayerHit(x, y, r, pl.lagX, pl.lagY)) return false;
            double dx = pl.lagX - x, dy = pl.lagY + Balance.PlayerHitDy - y, d = System.Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-9) { dx = 0; dy = 1; d = 1; }
            run.hitBook = key;
            Combat.DamagePlayer(run, dmg, dx / d, dy / d, knock * Balance.KnockPGain, false);
            run.hitBook = null;
            return true;
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
