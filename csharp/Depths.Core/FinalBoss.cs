namespace Depths
{
    /// <summary>The final boss of floor 14 (Balance.TuneFinalBoss): the Sunken King. Three phases, each a change of state, six
    /// telegraphed attacks, and a floor that cracks, glows and burns in patterns read ahead (docs/DESIGN.md, "The Sunken King").</summary>
    public static class FinalBoss
    {
        public const string Name = "THE SUNKEN KING";

        public enum Attack { Fan, Slam, Charge, Rift, Leap, Nova, Quake }
        public const int Attacks = 7;

        /// <summary>Where a hit came from, for the bookkeeping (RunState.finalBy): the six attacks, the three floor patterns, the pits.</summary>
        public enum Source { Fan, Slam, Charge, Rift, Leap, Nova, Fault, Checker, Quadrant, Pit }
        public const int Sources = 10;
        public static readonly string[] SourceNames = { "fan", "slam", "charge", "rift", "leap", "nova", "fault", "checker", "quadrant", "pit" };

        /// <summary>What the King's body is doing in each phase: seated on its throne, standing and walking, risen off the floor.</summary>
        public enum Posture { Seated, Standing, Risen }
        public static Posture PostureOf(Enemy e) => e.phase >= 3 ? Posture.Risen : e.phase == 2 ? Posture.Standing : Posture.Seated;

        /// <summary>Whether this floor's boss room holds the King: tuned, switched on, and the last floor.</summary>
        public static bool Spawns(RunState run) => Balance.Tuned && Balance.TuneFinalBoss && run.finalFloor;

        // ---- tells: every one at least TellMin, checked by FinalBossTests
        public const double TellMin = 0.35;
        public static int FanTell => Balance.Sec(0.55);      // the cast flash ring before a fan
        public static int NovaTell => Balance.Sec(0.75);     // the cast flash ring before a full ring
        public static int SlamWind => Balance.Sec(0.4);      // the cast flash before the lobs leave
        public static int SlamFlight => Balance.Sec(0.85);    // each lob's landing mark is on the floor this long
        public static int ChargeTell => Balance.Sec(0.55);   // an orange dashed line along the charge
        public static int AgainTell => Balance.Sec(0.45);    // the risen King's second charge, off its own line
        public static int RiftTell => Balance.Sec(0.6);     // a filled strip on the floor, from the King through the player to the wall
        public static int LeapTime => Balance.Sec(0.95);     // in the air: its landing ring is on the floor all the while
        public static int CrackTell => Balance.Sec(0.75);     // a floor pattern's cells crack and glow this long before they burn
        public static int QuadTell => Balance.Sec(1.2);      // phase 3's quadrants: a longer read, a longer walk
        public static int FallTell => Balance.Sec(2.0);      // ground that collapses for good cracks this long first
        public static int Burn => Balance.Sec(1.5);          // a pattern's cells burn this long, then go cold
        public static int ShiftTime => Balance.Sec(2.4);
        public static int QuakeCast => Balance.Sec(0.5);     // its arms up as a pattern cracks; then it fights on over the crack     // a phase change: the King's new posture and the floor's collapse
        public static int DeathBeat => Balance.Sec(1.8);     // its body breaking, then the flash, then the win

        // ---- the moves
        public const double SlamBlast = 58, SlamSpread = 100, SlamDmg = 2;
        public const int SlamN = 3;
        public const int FanN = 9;
        public const double FanCone = 0.6, FanDmg = 1, ShellR = 7;
        public static double FanSpeed => Balance.PerSec(420);
        public const int NovaN = 18;
        public const double NovaSlow = 0.7;   // the second ring's speed, as a share of the first's
        public const double NovaDmg = 1;
        public static double NovaSpeed => Balance.PerSec(240);
        public static double ChargeSpeed => Balance.PerSec(660);
        public const double ChargeMin = 240, ChargeMax = 460, ChargeOver = 120;
        public const double RiftW = 48, RiftDmg = 2;          // px: the strip's full width
        public const double RiftSpread = 0.55;                // radians between a risen King's three strips
        public const double LeapR = 70, LeapDmg = 2;
        public const int LeapShells = 10;
        public static double LeapShellSpeed => Balance.PerSec(200);
        public const double FloorDmg = 1;
        public static int Recover => Balance.Sec(0.3);
        public static int ChargeRecover => Balance.Sec(0.9);
        public static int LeapRecover => Balance.Sec(0.9);
        public static int RiftFx => Balance.Sec(0.3);
        public static double Walk => Balance.PerSec(85);
        public static double Float => Balance.PerSec(115);
        public const double Standoff = 190;
        public const double Hp = 640;   // x Bodies.Tough, then x DepthTough like every body
        public const string ShellColor = "#ff6a3d", LobColor = "#ffb03a", RiftColor = "#ff5a2a";

        // ---- the cadence: not scaled by depth (there is one depth it lives on)
        public static int CdMin => Balance.Sec(0.16);
        public static int CdVar => Balance.Sec(0.3);
        public static int FirstCd => Balance.Sec(1.0);

        public static double ThroneX => Balance.MidX;
        public const double ThroneY = Balance.RoomTop + 62;

        /// <summary>Weights by phase for Fan, Slam, Charge, Rift, Leap, Nova, Quake; 0 is not in that phase.</summary>
        public static readonly double[][] Weights =
        {
            new double[] { 1, 3, 0, 0, 0, 0, 3 },       // seated: shells and lobs from the throne, the fault lines
            new double[] { 0.5, 2.5, 3, 2.5, 0, 0, 3 },     // standing: it charges, it splits the floor, the checker
            new double[] { 0.4, 0, 2.5, 2.5, 3, 3, 3 },   // risen: it leaps, it rings, the quadrants
        };
        public const double RepeatWeight = 0.3;

        /// <summary>The attack a roll in [0,1) picks in a phase, the last attack weighed down.</summary>
        public static Attack Pick(int phase, int last, double roll, bool shellsOnly = false)
        {
            var w = Weights[System.Math.Max(1, System.Math.Min(3, phase)) - 1];
            double W(int i) => shellsOnly && !Shells((Attack)i) ? 0 : i == last ? w[i] * RepeatWeight : w[i];
            double sum = 0;
            for (int i = 0; i < Attacks; i++) sum += W(i);
            double at = roll * sum;
            int pick = -1;
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

        /// <summary>The attacks that only throw shells (no line, strip or landing on the floor): the ones it may use while the
        /// floor is cracking, so the walk off a crack is made under fire but never across a second floor tell.</summary>
        public static bool Shells(Attack a) => a == Attack.Fan || a == Attack.Nova || a == Attack.Slam;

        public static void Init(Enemy e)
        {
            e.x = ThroneX; e.y = ThroneY;
            e.phase = 1; e.move = "idle"; e.moveT = 0; e.bossCd = FirstCd;
            e.aggroTimer = 9999;
            e.cdMin = CdMin; e.cdVar = CdVar;
            e.speed = Walk;
            e.fbLast = -1; e.fbLeft = 1; e.castT = 0;   // fbLeft: the phase's opener (its floor pattern) is still to come
            e.ground = new Ground();
        }

        /// <summary>Whether the King's body hurts on contact this tick: only as it charges, after its line was shown.</summary>
        public static bool Hurts(Enemy e) => e.fbHot;

        /// <summary>One tick of the King: the floor's clocks and burns, the phase check, then its move or the next attack.</summary>
        public static void Step(RunState run, Enemy e, double edx, double edy, double dist, double sm)
        {
            e.fbHot = false;
            var g = e.ground!;
            StepGround(run, g);
            double frac = e.hp / e.maxHp;
            if (e.phase == 1 && frac <= Balance.BossPhase1) Shift(run, e, 2);
            else if (e.phase == 2 && frac <= Balance.BossPhase2) Shift(run, e, 3);
            switch (e.move)
            {
                case "shift": if (--e.moveT <= 0) { e.move = "idle"; e.bossCd = System.Math.Min(e.bossCd, Balance.Sec(0.3)); } return;
                case "tell": StepTell(e); return;
                case "charge": StepCharge(e); return;
                case "cast": StepCast(run, e); return;
                case "rift": StepRift(run, e); return;
                case "leap": StepLeap(run, e); return;
                case "quake": if (--e.moveT <= 0) { e.move = "rest"; e.moveT = Recover; } return;
                case "rest": if (--e.moveT <= 0) e.move = "idle"; return;
            }
            // idle: seated it holds the throne; standing it walks to its standoff; risen it drifts there quicker
            if (e.phase >= 2 && dist > Standoff)
            {
                double v = (e.phase >= 3 ? Float : Walk) * sm;
                e.x += edx / dist * v; e.y += edy / dist * v;
            }
            if (LobsLive(run, e)) return;   // never a new tell over its own live marks
            if ((e.bossCd -= 1) > 0) return;
            bool cracking = g.Cracking;   // over a cracking floor: shells only, never a second thing on the ground
            if (cracking && e.fbLeft > 0) return;
            var a = e.fbLeft > 0 ? Attack.Quake : Pick(e.phase, e.fbLast, run.rng.Run(), cracking);
            e.fbLeft = 0;
            double over = e.bossCd;
            e.bossCd = e.cdMin + run.rng.Jitter() * e.cdVar;
            if (Balance.TickHz != Balance.JsHz) e.bossCd += over;
            Begin(run, e, a);
        }

        /// <summary>The King settled into a phase as if it had fought its way there: its posture and its arena's fallen ground,
        /// nothing cracking (the lab's dropped specimens and the screenshot views; the fight itself goes through Shift).</summary>
        public static void Settle(Enemy e, int phase)
        {
            var g = e.ground!;
            e.phase = System.Math.Max(1, System.Math.Min(3, phase));
            e.move = "idle"; e.moveT = 0; e.castT = 0; e.fbLeft = 0;
            g.ClearPatterns();
            for (int i = 0; i < Ground.N; i++) g.pit[i] = (e.phase == 2 && Ground.Centre(i)) || (e.phase == 3 && Ground.Edge(i));
            if (e.phase == 3 && g.safe < 0) g.safe = 0;
            if (e.phase >= 2 && e.hp > e.maxHp * Balance.BossPhase1) e.hp = e.maxHp * (Balance.BossPhase1 - 0.01);
            if (e.phase >= 3 && e.hp > e.maxHp * Balance.BossPhase2) e.hp = e.maxHp * (Balance.BossPhase2 - 0.01);
        }

        /// <summary>Its slam's shells still in the air (their landing marks on the floor).</summary>
        public static bool LobsLive(RunState run, Enemy e)
        {
            foreach (var p in run.projectiles) if (p.lob && p.owner == e) return true;   // a loop, not Exists: no closure a tick
            return false;
        }

        // a phase change: the move it was in is called off with its warning, the floor's patterns go cold, and the arena changes
        static void Shift(RunState run, Enemy e, int n)
        {
            e.phase = n;
            e.move = "shift"; e.moveT = ShiftTime; e.castT = 0; e.fbLeft = 1;
            e.bossCd = Balance.Sec(0.3);
            var g = e.ground!;
            g.ClearPatterns();
            if (n == 2)
            {
                // the throne breaks: the floor's heart cracks and falls away for good
                for (int i = 0; i < Ground.N; i++) if (Ground.Centre(i)) g.Warn(i, FallTell, true, Source.Pit);
            }
            else
            {
                // the King rises: the pit closes, and the edge of the room cracks and falls away instead
                for (int i = 0; i < Ground.N; i++)
                {
                    if (Ground.Centre(i)) g.pit[i] = false;
                    if (Ground.Edge(i)) g.Warn(i, FallTell, true, Source.Pit);
                }
                g.safe = (int)(run.rng.Jitter() * 4) % 4;   // the first quadrant spared; the next one clockwise after it
            }
        }

        public static void Begin(RunState run, Enemy e, Attack a)
        {
            e.fbLast = (int)a;
            e.fbAttack = (int)a;
            switch (a)
            {
                case Attack.Fan: Cast(e, FanTell); break;
                case Attack.Nova: Cast(e, NovaTell); break;
                case Attack.Slam: Cast(e, SlamWind); break;
                case Attack.Charge:
                    e.fbCharges = e.phase >= 3 ? 1 : 0;   // risen, it charges twice: the second off a shorter line of its own
                    Aim(e, ChargeMin, ChargeMax, ChargeOver); e.move = "tell"; e.moveT = e.fbTellMax = ChargeTell;
                    break;
                case Attack.Rift:
                {
                    // one strip standing, three risen (fanned about the aim): each from the King through to the wall
                    Aim(e, 2000, 2000, 0);
                    var g = e.ground!;
                    g.rifts = e.phase >= 3 ? 3 : 1;
                    double a0 = System.Math.Atan2(e.fbDy, e.fbDx);
                    for (int k = 0; k < g.rifts; k++)
                    {
                        double ak = a0 + (k - (g.rifts - 1) / 2.0) * RiftSpread;
                        g.rdx[k] = System.Math.Cos(ak); g.rdy[k] = System.Math.Sin(ak);
                        g.rlen[k] = WallClip(e, g.rdx[k], g.rdy[k], 2000);
                    }
                    g.riftX = e.x; g.riftY = e.y;
                    e.move = "rift"; e.moveT = e.fbTellMax = RiftTell;
                    break;
                }
                case Attack.Leap:
                {
                    double m = LeapR * 0.6;
                    e.leapX = e.x; e.leapY = e.y;
                    e.markX = Clamp(TickOrder.HitboxX, Balance.RoomLeft + m, Balance.RoomRight - m);
                    e.markY = Clamp(TickOrder.HitboxY + Balance.PlayerHitDy, Balance.RoomTop + m, Balance.RoomBottom - m);
                    e.move = "leap"; e.moveT = e.fbTellMax = LeapTime;
                    break;
                }
                case Attack.Quake: Quake(run, e); break;
            }
        }

        // the phase's floor pattern: its cells crack now, burn when the crack runs out; the King holds its arms up until then
        static void Quake(RunState run, Enemy e)
        {
            var g = e.ground!;
            int tell = CrackTell;
            if (e.phase == 1)
            {
                int par = run.rng.Jitter() < 0.5 ? 0 : 1;   // the fault lines: every other row, from the top or the second
                for (int i = 0; i < Ground.N; i++) if (Ground.Row(i) % 2 == par) g.Warn(i, tell, false, Source.Fault);
            }
            else if (e.phase == 2)
            {
                int par = run.rng.Jitter() < 0.5 ? 0 : 1;   // the checker: every other cell, so the safe one is always beside you
                for (int i = 0; i < Ground.N; i++) if ((Ground.Col(i) + Ground.Row(i)) % 2 == par && !g.pit[i]) g.Warn(i, tell, false, Source.Checker);
            }
            else
            {
                tell = QuadTell;   // the quadrants: three burn, the one it spares turns clockwise each time and is shown ahead
                if (g.safe < 0) g.safe = 0;
                for (int i = 0; i < Ground.N; i++) if (!Ground.Edge(i) && Ground.Quadrant(i) != g.safe && !g.pit[i]) g.Warn(i, tell, false, Source.Quadrant);
                g.lastSafe = g.safe;
                g.safe = (g.safe + 1) % 4;
            }
            e.move = "quake"; e.moveT = QuakeCast; e.fbTellMax = tell;
        }

        // a line locked now at the player's hitbox and on past it, stopped by the wall: a charge's or a rift's
        static void Aim(Enemy e, double min, double max, double over)
        {
            double hx = TickOrder.HitboxX - e.x, hy = TickOrder.HitboxY + Balance.PlayerHitDy - e.y, d = System.Math.Sqrt(hx * hx + hy * hy);
            if (d < 1) { hx = 0; hy = 1; d = 1; }
            e.fbDx = hx / d; e.fbDy = hy / d;
            e.fbLen = WallClip(e, e.fbDx, e.fbDy, System.Math.Max(min, System.Math.Min(max, d + over)));
        }

        static double WallClip(Enemy e, double dx, double dy, double len)
        {
            double lo = Balance.RoomLeft + e.r, hi = Balance.RoomRight - e.r, top = Balance.RoomTop + e.r, bot = Balance.RoomBottom - e.r;
            if (dx > 1e-9) len = System.Math.Min(len, (hi - e.x) / dx);
            if (dx < -1e-9) len = System.Math.Min(len, (lo - e.x) / dx);
            if (dy > 1e-9) len = System.Math.Min(len, (bot - e.y) / dy);
            if (dy < -1e-9) len = System.Math.Min(len, (top - e.y) / dy);
            return System.Math.Max(0, len);
        }

        static void StepTell(Enemy e)
        {
            if (--e.moveT > 0) return;
            e.move = "charge";
            e.fbGone = 0;
            e.moveT = System.Math.Max(1, (int)System.Math.Ceiling(e.fbLen / ChargeSpeed));
        }

        static void StepCharge(Enemy e)
        {
            e.fbHot = true;
            double step = System.Math.Min(ChargeSpeed, e.fbLen - e.fbGone);
            e.x += e.fbDx * step; e.y += e.fbDy * step;
            e.fbGone += step;
            if (--e.moveT > 0 && e.fbGone < e.fbLen - 1e-9) return;
            if (e.fbCharges > 0)
            {
                e.fbCharges--;
                Aim(e, ChargeMin, ChargeMax, ChargeOver); e.move = "tell"; e.moveT = e.fbTellMax = AgainTell;
                return;
            }
            e.move = "rest"; e.moveT = ChargeRecover;
        }

        /// <summary>Whether a point is on a rift's strip (the drawn width, plus the player's hitbox radius).</summary>
        public static bool OnRift(double ax, double ay, double dx, double dy, double len, double px, double py)
        {
            double rx = px - ax, ry = py - ay, along = rx * dx + ry * dy;
            if (along < 0 || along > len) return false;
            double ox = rx - dx * along, oy = ry - dy * along;
            return System.Math.Sqrt(ox * ox + oy * oy) < RiftW / 2 + Balance.PlayerHitR;
        }

        static void StepRift(RunState run, Enemy e)
        {
            if (--e.moveT > 0) return;
            var pl = run.player;
            var g = e.ground!;
            double px = pl.lagX, py = pl.lagY + Balance.PlayerHitDy;
            for (int k = 0; k < g.rifts; k++)
            {
                if (!OnRift(g.riftX, g.riftY, g.rdx[k], g.rdy[k], g.rlen[k], px, py)) continue;
                double side = (px - g.riftX) * -g.rdy[k] + (py - g.riftY) * g.rdx[k] >= 0 ? 1 : -1;   // thrown off the strip, to its side
                Hurt(run, Source.Rift, RiftDmg, -g.rdy[k] * side, g.rdx[k] * side, 3);
                break;   // one eruption hit, however many strips it stands on
            }
            g.riftFx = RiftFx;
            run.booms++;
            e.move = "rest"; e.moveT = Recover;
        }

        static void StepLeap(RunState run, Enemy e)
        {
            double t = 1 - (e.moveT - 1) / (double)System.Math.Max(1, e.fbTellMax);
            e.x = e.leapX + (e.markX - e.leapX) * t; e.y = e.leapY + (e.markY - e.leapY) * t;
            if (--e.moveT > 0) return;
            e.x = e.markX; e.y = e.markY;
            var pl = run.player;
            if (Hit.PlayerHit(e.markX, e.markY, LeapR, pl.lagX, pl.lagY))
                Hurt(run, Source.Leap, LeapDmg, pl.lagX - e.markX, pl.lagY + Balance.PlayerHitDy - e.markY, 3);
            double a0 = run.rng.Jitter() * 2 * System.Math.PI / LeapShells;   // the landing throws a slow ring out from its mark
            for (int i = 0; i < LeapShells; i++)
            {
                var s = Middle.Shell(e, a0 + 2 * System.Math.PI * i / LeapShells, LeapShellSpeed, ShellR, 1, ShellColor, 0);
                s.src = (int)Source.Leap;
                run.projectiles.Add(s);
            }
            run.booms++;
            e.move = "rest"; e.moveT = LeapRecover;
        }

        static void Hurt(RunState run, Source s, double dmg, double kx, double ky, double knock)
        {
            double d = System.Math.Sqrt(kx * kx + ky * ky);
            if (d < 1e-9) { kx = 0; ky = 1; d = 1; }
            run.hitSrc = (int)s;
            Combat.DamagePlayer(run, dmg, kx / d, ky / d, knock * Balance.KnockPGain, false);
            run.hitSrc = -1;
        }

        static void Cast(Enemy e, int ticks)
        {
            e.move = "cast"; e.castT = ticks; e.fbTellMax = ticks; e.castReady = false;
        }

        static void StepCast(RunState run, Enemy e)
        {
            if (--e.castT > 0) return;
            e.castT = 0;
            double hx = TickOrder.HitboxX, hy = TickOrder.HitboxY + Balance.PlayerHitDy;
            double aim = System.Math.Atan2(hy - e.y, hx - e.x);
            switch ((Attack)e.fbAttack)
            {
                case Attack.Slam:
                {
                    // one on the player, two either side across the line from the King: a step along it is safe, across is not
                    double nx = -System.Math.Sin(aim), ny = System.Math.Cos(aim);
                    // standing, two more along the line, before and behind: a cross, so only the diagonals are safe
                    int n = e.phase >= 2 ? SlamN + 2 : SlamN;
                    double ax = System.Math.Cos(aim), ay = System.Math.Sin(aim);
                    for (int i = 0; i < n; i++)
                    {
                        double k = i < SlamN ? i - (SlamN - 1) / 2.0 : 0, along = i < SlamN ? 0 : i == SlamN ? -1 : 1, j = (run.rng.Jitter() - 0.5) * 30;
                        double tx = Clamp(hx + nx * k * SlamSpread + ax * along * SlamSpread + j, Balance.RoomLeft + SlamBlast * 0.5, Balance.RoomRight - SlamBlast * 0.5);
                        double ty = Clamp(hy + ny * k * SlamSpread + ay * along * SlamSpread + j, Balance.RoomTop + SlamBlast * 0.5, Balance.RoomBottom - SlamBlast * 0.5);
                        run.projectiles.Add(new Projectile
                        {
                            x = e.x, y = e.y, ox = e.x, oy = e.y, tx = tx, ty = ty, lob = true, flightMax = SlamFlight,
                            r = 8, dmg = SlamDmg, blastR = SlamBlast, friendly = false, owner = e, color = LobColor, src = (int)Source.Slam,
                        });
                    }
                    break;
                }
                case Attack.Fan:
                    for (int i = 0; i < FanN; i++)
                    {
                        var s = Middle.Shell(e, aim + FanCone * (2.0 * i / (FanN - 1) - 1), FanSpeed, ShellR, FanDmg, ShellColor, 0);
                        s.src = (int)Source.Fan;
                        run.projectiles.Add(s);
                    }
                    break;
                case Attack.Nova:
                {
                    // two rings at once, the slower one turned half a gap: a wave with a second wave behind it
                    double a0 = run.rng.Jitter() * 2 * System.Math.PI / NovaN;
                    for (int w = 0; w < 2; w++)
                        for (int i = 0; i < NovaN; i++)
                        {
                            double a = a0 + 2 * System.Math.PI * (i + 0.5 * w) / NovaN;
                            var s = Middle.Shell(e, a, NovaSpeed * (w == 0 ? 1 : NovaSlow), ShellR, NovaDmg, ShellColor, 0);
                            s.src = (int)Source.Nova;
                            run.projectiles.Add(s);
                        }
                    break;
                }
            }
            e.move = "rest"; e.moveT = Recover;
        }

        // the floor's clocks: a crack runs out into a burn (or a pit, for ground that falls), a burn runs out cold; then the
        // cell under the player's hitbox hurts if it burns or has fallen
        static void StepGround(RunState run, Ground g)
        {
            if (g.riftFx > 0) g.riftFx--;
            for (int i = 0; i < Ground.N; i++)
            {
                if (g.warn[i] > 0)
                {
                    if (--g.warn[i] > 0) continue;
                    if (g.falls[i]) { g.pit[i] = true; g.falls[i] = false; }
                    else g.burn[i] = Burn;
                }
                else if (g.burn[i] > 0) g.burn[i]--;
            }
            var pl = run.player;
            double px = pl.lagX, py = pl.lagY + Balance.PlayerHitDy;
            int c = Ground.CellAt(px, py);
            if (c < 0 || !g.Hot(c)) return;
            Hurt(run, g.pit[c] ? Source.Pit : g.src[c], FloorDmg, px - Ground.CentreX(c), py - Ground.CentreY(c), 2);
        }

        /// <summary>The King is down: its body breaks for DeathBeat (the room's win waits for it), its shells and marks go with it (TickRoom).</summary>
        public static void Fall(RunState run, Enemy e)
        {
            run.finaleT = run.finaleMax = DeathBeat;
            run.finaleX = e.x; run.finaleY = e.y;
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>The arena's floor as a grid of cells over the room, each one quiet, cracking (warn), burning, or fallen (pit).</summary>
        public sealed class Ground
        {
            public const int Cols = 10, Rows = 6, N = Cols * Rows;
            public const double CellW = (Balance.RoomRight - Balance.RoomLeft) / (double)Cols, CellH = (Balance.RoomBottom - Balance.RoomTop) / (double)Rows;
            public readonly int[] warn = new int[N], warnMax = new int[N], burn = new int[N];
            public readonly bool[] falls = new bool[N], pit = new bool[N];
            public readonly Source[] src = new Source[N];
            /// <summary>Phase 3: the quadrant the next pattern spares (shown ahead), and the one the last spared; -1 before.</summary>
            public int safe = -1, lastSafe = -1;
            /// <summary>A rift's eruption, for the presenter: ticks left and its strip.</summary>
            public int riftFx;
            /// <summary>A rift's strips (one standing, three risen): their count, origin, ways and lengths to the wall.</summary>
            public int rifts;
            public double riftX, riftY;
            public readonly double[] rdx = new double[3], rdy = new double[3], rlen = new double[3];

            public static int Col(int i) => i % Cols;
            public static int Row(int i) => i / Cols;
            public static double Left(int i) => Balance.RoomLeft + Col(i) * CellW;
            public static double Top(int i) => Balance.RoomTop + Row(i) * CellH;
            public static double CentreX(int i) => Left(i) + CellW / 2;
            public static double CentreY(int i) => Top(i) + CellH / 2;
            /// <summary>The cell a point is in, or -1 outside the room.</summary>
            public static int CellAt(double x, double y)
            {
                if (x < Balance.RoomLeft || x >= Balance.RoomRight || y < Balance.RoomTop || y >= Balance.RoomBottom) return -1;
                int c = System.Math.Min(Cols - 1, (int)((x - Balance.RoomLeft) / CellW)), r = System.Math.Min(Rows - 1, (int)((y - Balance.RoomTop) / CellH));
                return r * Cols + c;
            }
            /// <summary>The ring of cells along the walls (phase 3's fallen edge).</summary>
            public static bool Edge(int i) => Col(i) == 0 || Col(i) == Cols - 1 || Row(i) == 0 || Row(i) == Rows - 1;
            /// <summary>The heart of the room, four by two (phase 2's pit).</summary>
            public static bool Centre(int i) => Col(i) >= 3 && Col(i) <= 6 && Row(i) >= 2 && Row(i) <= 3;
            /// <summary>Phase 3's quadrants of the inner floor, clockwise from the top left: 0 1 / 3 2.</summary>
            public static int Quadrant(int i)
            {
                bool east = Col(i) >= Cols / 2, south = Row(i) >= Rows / 2;
                return south ? (east ? 2 : 3) : (east ? 1 : 0);
            }

            public bool Hot(int i) => pit[i] || burn[i] > 0;
            /// <summary>A pattern or a collapse is cracking: the King starts nothing new over it.</summary>
            public bool Cracking
            {
                get
                {
                    for (int i = 0; i < N; i++) if (warn[i] > 0) return true;
                    return false;
                }
            }

            public void Warn(int i, int ticks, bool fall, Source s)
            {
                warn[i] = warnMax[i] = ticks; falls[i] = fall; src[i] = s; burn[i] = 0;
            }

            public void ClearPatterns()
            {
                for (int i = 0; i < N; i++) { warn[i] = 0; burn[i] = 0; falls[i] = false; }
            }
        }
    }
}
