using System;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The owner's playtest notes of 2026-10-13, as behaviour (C# only, Balance.Tuned): the Brunch wall
    /// forms as you walk in and rushes you when you are close; the Beam takes 2-3 bolts a Brunch; the
    /// Warden's statues act; a room drops at most one thing, from its last kill, about 3 rooms in 4;
    /// a Lunger dodges a shot it can see coming, except inside the Scatter's reach, and is never a free
    /// kill on room entry. Each test also shows the untuned (JS) behaviour it changes, so it cannot pass
    /// by testing nothing.
    /// </summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class TunedTests
    {
        static RunState Started(uint seed = 4040)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            run.readyT = 0; run.fadeT = 0; run.state = "playing";
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
            Place(run, Balance.MidX, Balance.RoomBottom - 60);
            return run;
        }

        static void Place(RunState run, double x, double y)
        {
            var p = run.player;
            p.x = p.lagX = x; p.y = p.lagY = y; p.vx = p.vy = 0; p.kvx = p.kvy = 0;
            TickOrder.HitboxX = x; TickOrder.HitboxY = y;
        }

        static void Step(RunState run, int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
            {
                TickOrder.TickPlayer(run, Input.None);
                TickOrder.TickProjectiles(run);
                TickOrder.TickBodies(run);
            }
        }

        static double D(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        // ---- the Beam on a Brunch ----

        // bolts of the real hit path, 0.5 + 3 strength, until a floor-1 Brunch dies; `from` is how far the bolt has flown
        static int BeamBoltsToKill(double from)
        {
            var run = Started();
            var b = Spawn.Body(run, BodyKind.Brunch, Balance.MidX, Balance.MidY);
            b.noticeTimer = int.MaxValue / 2;
            run.enemies.Add(b);
            var w = Weapons.All[Weapons.BeamIndex];
            int n = 0;
            while (b.hp > 0 && n < 20)
            {
                n++;
                run.projectiles.Add(new Projectile { x = b.x, y = b.y, ox = b.x - from, oy = b.y, vx = 0.001, dx = 1, r = w.R, dmg = w.Dmg + 3,
                                                     friendly = true, fNear = w.FNear, fFar = w.FFar, fMin = w.FMin, scale = 1, gun = Weapons.BeamIndex });
                TickOrder.TickProjectiles(run);
                run.projectiles.Clear();
            }
            return n;
        }

        [Test]
        public void ABrunchTakesTwoToThreeBeamBolts()
        {
            Assert.That(BeamBoltsToKill(0), Is.InRange(2, 3), "point blank");
            Assert.That(BeamBoltsToKill(1000), Is.InRange(2, 3), "past the Beam's falloff");
            Balance.Tuned = false;
            try { Assert.That(BeamBoltsToKill(0), Is.EqualTo(1), "the JS rule: one bolt, the playtest complaint"); }
            finally { Balance.Tuned = true; }
        }

        [Test]
        public void OnlyTheBeamIsResisted()
        {
            var run = Started();
            var b = Spawn.Body(run, BodyKind.Brunch, Balance.MidX, Balance.MidY);
            b.noticeTimer = int.MaxValue / 2;
            run.enemies.Add(b);
            double hp = b.hp;
            run.projectiles.Add(new Projectile { x = b.x, y = b.y, ox = b.x, oy = b.y, vx = 0.001, dx = 1, r = 6, dmg = 1, friendly = true, scale = 1, gun = 0 });
            TickOrder.TickProjectiles(run);
            Assert.That(hp - b.hp, Is.EqualTo(1).Within(1e-9), "a Bolt hits a Brunch for its whole damage");
        }

        // ---- loot ----

        [Test]
        public void ARoomDropsOneThingFromItsLastKillAboutThreeTimesInFour()
        {
            int rooms = 2000, dropped = 0;
            for (int r = 0; r < rooms; r++)
            {
                var run = Started((uint)(1000 + r));   // seeded per room; the rooms differ
                int n = 2 + r % 7;
                for (int i = 0; i < n; i++) run.enemies.Add(Enemy.Of(i % 3 == 0 ? BodyKind.Brunch : BodyKind.Lunger, 200 + i * 30, 300));
                for (int i = n - 1; i >= 0; i--)
                {
                    Kills.KillEnemy(run, i);
                    if (i > 0) Assert.That(run.pickups, Is.Empty, "room " + r + ": a kill before the last dropped");
                }
                Assert.That(run.pickups.Count, Is.LessThanOrEqualTo(1), "room " + r + ": more than one pickup");
                dropped += run.pickups.Count;
            }
            double rate = dropped / (double)rooms;
            Assert.That(rate, Is.InRange(0.71, 0.79), "rooms with a drop: " + rate.ToString("0.000"));
        }

        [Test]
        public void TheRoomDropKeepsTheBandSplit()
        {
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < 26000; i++)
            {
                var d = Loot.RoomDrop((i + 0.5) / 26000 * Balance.RoomDropChance, 0, 0);
                counts[d!.kind] = (counts.TryGetValue(d.kind, out var c) ? c : 0) + 1;
            }
            Assert.That(counts["halfheart"], Is.EqualTo(9000).Within(2)); Assert.That(counts["heart"], Is.EqualTo(9000).Within(2));
            Assert.That(counts["halfarmor"], Is.EqualTo(4000).Within(2)); Assert.That(counts["armor"], Is.EqualTo(4000).Within(2));
            Assert.That(Loot.RoomDrop(Balance.RoomDropChance, 0, 0), Is.Null, "a roll past the chance drops nothing");
        }

        // ---- the Warden's wall ----

        [Test]
        public void TheWardensBrunchAct()
        {
            foreach (bool tuned in new[] { true, false })
            {
                Balance.Tuned = tuned;
                try
                {
                    var run = Started();
                    var w = Spawn.Body(run, BodyKind.Boss, Balance.MidX, Balance.RoomTop + 100);
                    w.noticeTimer = int.MaxValue / 2;   // the Warden itself holds still: only the wall acts
                    run.enemies.Add(w);
                    TickOrder.BeginBoss(run, w, "wall");
                    for (int t = 0; t < Balance.Sec(1) && w.wallBodies == null; t++) TickOrder.ResolveBoss(run, w);
                    Assert.That(w.wallBodies, Is.Not.Null.And.Not.Empty, "the Warden called its wall");
                    var start = w.wallBodies!.Select(b => (b.x, b.y)).ToList();
                    Step(run, Balance.Sec(1.5));
                    double moved = w.wallBodies!.Select((b, i) => D(b.x, b.y, start[i].x, start[i].y)).Max();
                    if (tuned) Assert.That(moved, Is.GreaterThan(20), "tuned: the statues move");
                    else Assert.That(moved, Is.LessThan(1), "the JS: statues stand until struck");
                }
                finally { Balance.Tuned = true; }
            }
        }

        // ---- Brunch: the wall forms fast, and they rush you up close ----

        static (RunState run, Enemy shooter, Enemy[] pack) Wall(double playerX, double playerY)
        {
            var run = Started();
            var s = Spawn.Body(run, BodyKind.Shooter, Balance.MidX, Balance.RoomTop + 50);
            s.noticeTimer = int.MaxValue / 2;
            run.enemies.Add(s);
            var pack = new Enemy[5];
            for (int i = 0; i < pack.Length; i++)
            {
                var b = Spawn.Body(run, BodyKind.Brunch, Balance.RoomLeft + 60 + (i % 2) * 18, Balance.RoomTop + 60 + (i / 2) * 18);
                b.packId = 77; b.packSlot = i; b.noticeTimer = 0; b.alerted = true;
                run.enemies.Add(b);
                pack[i] = b;
            }
            Place(run, playerX, playerY);
            return (run, s, pack);
        }

        [Test]
        public void TheWallFormsFasterThanItUsedTo()
        {
            double Moved(bool tuned)
            {
                Balance.Tuned = tuned;
                try
                {
                    var (run, _, pack) = Wall(Balance.MidX, Balance.RoomBottom - 40);
                    var start = pack.Select(b => (b.x, b.y)).ToArray();
                    Step(run, Balance.Sec(0.6));
                    return pack.Select((b, i) => D(b.x, b.y, start[i].x, start[i].y)).Average();
                }
                finally { Balance.Tuned = true; }
            }
            double tuned = Moved(true), js = Moved(false);
            Assert.That(tuned, Is.GreaterThan(1.6 * js), "in 0.6 s the pack walks " + tuned.ToString("0") + " px against " + js.ToString("0"));
        }

        [Test]
        public void ABrunchRushesAPlayerInsideTheScatterReach()
        {
            // the edge case: the player beside the pack, the shooter across the room
            var (run, s, pack) = Wall(Balance.RoomLeft + 70, Balance.RoomTop + 170);
            double before = pack.Average(b => D(b.x, b.y, TickOrder.HitboxX, TickOrder.HitboxY));
            Assert.That(before, Is.LessThan(Balance.CloseRange));
            Step(run, Balance.Sec(0.3));
            double after = pack.Where(b => b.hp > 0).Average(b => D(b.x, b.y, TickOrder.HitboxX, TickOrder.HitboxY));
            Assert.That(after, Is.LessThan(before - 15), "they come at the player, not across the room to the shooter");
        }

        [Test]
        public void AFormedWallHoldsAsThePlayerWalksUpToIt()
        {
            // the first rush rule charged any Brunch within reach, so a formed wall body-slammed a player coming
            // for the shooter (the bot: +5 HP of damage a floor). The wall must stay a wall.
            var (run, s, pack) = Wall(Balance.MidX, Balance.RoomBottom - 40);
            Step(run, Balance.Sec(2.5));   // the wall forms
            Place(run, Balance.MidX, pack.Average(b => b.y) + 120);   // the player walks up, inside the Scatter's reach
            var start = pack.Select(b => (b.x, b.y)).ToArray();
            Step(run, Balance.Sec(0.3));
            double most = pack.Select((b, i) => D(b.x, b.y, start[i].x, start[i].y)).Max();
            Assert.That(most, Is.LessThan(Balance.BrunchShieldSpeed * Balance.TickHz * 0.3 + 8),
                        "no Brunch left its slot at chase speed (" + most.ToString("0") + " px in 0.3 s)");
        }

        // ---- the Lunger's dodge ----

        // settled: in the room a while (its startle window long gone); fresh: just spawned, notice delay already rolled 0 -
        // the worst real case, since the critic showed a real notice delay is at most 16/60 s
        static (RunState run, Enemy lunger) LungerAt(double distance, bool fresh = false)
        {
            var run = Started();
            var l = Spawn.Body(run, BodyKind.Lunger, Balance.MidX, Balance.RoomBottom - 60 - distance);
            l.noticeTimer = 0;
            l.lifeT = fresh ? 0 : Balance.Sec(10);
            l.lungeCd = Balance.Sec(10);   // it must not lunge first: the test is about the shot
            run.enemies.Add(l);
            return (run, l);
        }

        static Projectile Bolt(RunState run, Enemy at)
        {
            var w = Weapons.All[0];
            var p = run.player;
            double dx = at.x - p.x, dy = at.y - p.y, d = Math.Sqrt(dx * dx + dy * dy);
            var bolt = new Projectile { x = p.x, y = p.y, ox = p.x, oy = p.y, vx = dx / d * w.Speed, vy = dy / d * w.Speed, dx = dx / d, dy = dy / d,
                                        r = w.R, dmg = 10, friendly = true, scale = 1, fNear = w.FNear, fFar = w.FFar, fMin = w.FMin, gun = 0 };
            run.projectiles.Add(bolt);
            return bolt;
        }

        static (bool dodged, double hp) Fly(RunState run, Enemy l, Projectile shot)
        {
            bool dodged = false;
            for (int t = 0; t < Balance.Sec(1.2) && (run.projectiles.Contains(shot) || l.lungeState == "dodge"); t++)
            {
                Step(run);
                dodged |= l.lungeState == "dodge";
            }
            return (dodged, l.hp);
        }

        [Test]
        public void ALungerSidestepsABoltItCanSeeComing()
        {
            var (run, l) = LungerAt(300);
            double hp = l.hp;
            var (dodged, after) = Fly(run, l, Bolt(run, l));
            Assert.That(dodged, "it dodged");
            Assert.That(after, Is.EqualTo(hp), "and the bolt missed");
            Balance.Tuned = false;
            try
            {
                var (run2, l2) = LungerAt(300);
                var (d2, after2) = Fly(run2, l2, Bolt(run2, l2));
                Assert.That(d2, Is.False); Assert.That(after2, Is.LessThan(l2.maxHp), "the JS Lunger takes it");
            }
            finally { Balance.Tuned = true; }
        }

        [Test]
        public void InsideTheScatterReachTheShotLands()
        {
            var (run, l) = LungerAt(Balance.CloseRange - 50);
            var (dodged, hp) = Fly(run, l, Bolt(run, l));
            Assert.That(dodged, Is.False, "close enough still kills");
            Assert.That(hp, Is.LessThan(l.maxHp));
        }

        [Test]
        public void ALungerSpawnedBesideYouIsStartledNotFree()
        {
            var (run, l) = LungerAt(60, fresh: true);
            double hp = l.hp;
            var (dodged, after) = Fly(run, l, Bolt(run, l));
            Assert.That(dodged, "a body new to the room, beside the player, dodges the point-blank shot");
            Assert.That(after, Is.EqualTo(hp));
            Assert.That(l.startleSpent, "the startle is spent");
            var (again, after2) = Fly(run, l, Bolt(run, l));   // the cooldown aside, close range kills from now on
            l.dodgeCd = 0;
            var (third, after3) = Fly(run, l, Bolt(run, l));
            Assert.That(third, Is.False, "once startled, inside the Scatter's reach it no longer dodges");
            Assert.That(after3, Is.LessThan(after2 + 1e-9));
        }

        [Test]
        public void ALungerDoesNotDodgeOutOfItsRecovery()
        {
            // the critic's bug: a dodge from "recover" skipped the recovery and the lunge cooldown, the punish window
            var (run, l) = LungerAt(300);
            l.lungeState = "recover"; l.lungeT = Balance.Sec(0.5); l.lungeCd = 0;
            var (dodged, hp) = Fly(run, l, Bolt(run, l));
            Assert.That(dodged, Is.False);
            Assert.That(hp, Is.LessThan(l.maxHp), "a whiffed lunge can be punished");
        }

        [Test]
        public void ADodgeFromABlastNeverRunsBackThroughIt()
        {
            // the critic's bug: by a wall, "away from the mark" ran into the wall and was flipped straight through the mark
            var run = Started();
            var l = Spawn.Body(run, BodyKind.Lunger, Balance.RoomLeft + 20, Balance.MidY);
            l.noticeTimer = 0; l.lifeT = Balance.Sec(10); l.lungeCd = Balance.Sec(10);
            run.enemies.Add(l);
            Place(run, Balance.RoomRight - 40, Balance.MidY);
            double mx = Balance.RoomLeft + 70, my = Balance.MidY;   // the mark 50 px out from it, toward the room
            run.projectiles.Add(new Projectile { x = mx + 300, y = my, vx = -Balance.AltSpeed, vy = 0, speed = Balance.AltSpeed, tx = mx, ty = my,
                                                 alt = true, mode = "blast", r = Balance.AltR, friendly = true, color = "#ff8a3d" });
            double before = 0, nearest = double.PositiveInfinity;
            bool dodged = false;
            for (int t = 0; t < Balance.Sec(1); t++)
            {
                double was = D(l.x, l.y, mx, my);   // it walks in before the shell is close enough to fear: measure from the dash's start
                Step(run);
                if (l.lungeState == "dodge")
                {
                    if (!dodged) before = was;
                    dodged = true; nearest = Math.Min(nearest, D(l.x, l.y, mx, my));
                }
                else if (dodged) break;
            }
            Assert.That(dodged, "it saw the burst coming");
            // not merely "ends further": dashing straight through the burst and out the far side also ends further
            Assert.That(nearest, Is.GreaterThanOrEqualTo(before), "at no point of the dash was it nearer the burst than when it started");
        }

        [Test]
        public void ABlastBurstingOnANeighbourIsAThreat()
        {
            var run = Started();
            var near = Spawn.Body(run, BodyKind.Lunger, Balance.MidX, Balance.MidY - 100);
            var by = Spawn.Body(run, BodyKind.Lunger, Balance.MidX + 45, Balance.MidY - 100);
            foreach (var b in new[] { near, by }) { b.noticeTimer = 0; b.lifeT = Balance.Sec(10); b.lungeCd = Balance.Sec(10); run.enemies.Add(b); }
            Place(run, Balance.MidX - 300, Balance.MidY - 100);   // the shell's path meets `near` first, from the left
            run.player.altCooldown = 0;
            Weapons.FireAlt(run, by.x + 200, by.y);   // aimed past both: it bursts on the first body it meets
            bool dodged = false;
            for (int t = 0; t < Balance.Sec(1.5) && run.projectiles.Any(p => p.alt); t++) { Step(run); dodged |= by.lungeState == "dodge"; }
            Assert.That(dodged, "the body beside the one the blast will meet dodged too");
        }

        [Test]
        public void TheWardensBrunchGuardTheWarden()
        {
            var run = Started();
            var w = Spawn.Body(run, BodyKind.Boss, Balance.MidX, Balance.RoomTop + 100);
            w.noticeTimer = int.MaxValue / 2;
            run.enemies.Add(w);
            TickOrder.BeginBoss(run, w, "wall");
            for (int t = 0; t < Balance.Sec(1) && w.wallBodies == null; t++) TickOrder.ResolveBoss(run, w);
            Step(run, Balance.Sec(1));
            Assert.That(w.wallBodies!.Any(b => b.shieldTarget == w), "awake, they guard the Warden (the critic doubted PickShield could choose it)");
        }

        [Test]
        public void ARushingBrunchSpendsItselfOnContact()
        {
            var (run, s, pack) = Wall(Balance.RoomLeft + 70, Balance.RoomTop + 170);
            var b = pack[0];
            Step(run, Balance.Sec(0.3));
            Assert.That(b.rushing, "beside the player, far from its slot: rushing");
            b.x = TickOrder.HitboxX; b.y = TickOrder.HitboxY - Balance.PlayerHitDy;   // on the player
            double hp = b.hp;
            TickOrder.TickBodies(run);
            Assert.That(b.hp, Is.LessThan(hp), "it paid half its health for the hit, as a chasing Brunch does");
        }

        [Test]
        public void ALungerClearsABlastAimedAtIt()
        {
            var (run, l) = LungerAt(320);
            double hp = l.hp;
            run.player.altCooldown = 0;
            Weapons.FireAlt(run, l.x, l.y);
            var shell = run.projectiles.Single(p => p.alt);
            bool dodged = false;
            for (int t = 0; t < Balance.Sec(2) && run.projectiles.Contains(shell); t++) { Step(run); dodged |= l.lungeState == "dodge"; }
            Assert.That(dodged, "it saw the blast coming");
            Assert.That(l.hp, Is.EqualTo(hp), "and was outside it when it went off");
        }

        [Test]
        public void TheDodgeHasACooldown()
        {
            var (run, l) = LungerAt(300);
            Fly(run, l, Bolt(run, l));
            Assert.That(l.dodgeCd, Is.GreaterThan(0));
            double hp = l.hp;
            var (dodged, after) = Fly(run, l, Bolt(run, l));   // the second, at once
            Assert.That(dodged, Is.False, "no second dodge inside the cooldown");
            Assert.That(after, Is.LessThan(hp), "so pressure lands");
        }

        [Test]
        public void ADodgeCoversTheSameGroundAtBothRates()
        {
            double Dodge(int hz)
            {
                int was = Balance.TickHz;
                Balance.TickHz = hz;
                try
                {
                    var (run, l) = LungerAt(300);
                    double x0 = l.x;
                    Bolt(run, l);
                    bool started = false;   // where the dash ends, before it walks back in
                    for (int t = 0; t < hz * 2; t++)
                    {
                        Step(run);
                        if (l.lungeState == "dodge") started = true;
                        else if (started) break;
                    }
                    Assert.That(started, "it dodged at " + hz + " Hz");
                    return Math.Abs(l.x - x0);
                }
                finally { Balance.TickHz = was; }
            }
            double at60 = Dodge(60), at210 = Dodge(210);
            Assert.That(at60, Is.GreaterThan(Balance.LungerDodgeDist * 0.8));
            Assert.That(at60, Is.EqualTo(at210).Within(16), "one step of lunge travel at 60 Hz is 14.7 px");
        }
    }
}
