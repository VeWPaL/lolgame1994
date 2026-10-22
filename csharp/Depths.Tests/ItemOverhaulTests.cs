using System;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The item overhaul's first eight (docs/items.md, owner 2026-10-22), played when tuned (Balance.TuneItems).</summary>
    [TestFixture, Category("csharp-only"), TunedOn(true)]
    public sealed class ItemOverhaulTests
    {
        static RunState Room()
        {
            var run = new RunState(new Rng(77));
            run.Start(77);
            run.readyT = 0; run.fadeT = 0; run.state = "playing";
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear();
            var p = run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.MidY;
            TickOrder.HitboxX = p.x; TickOrder.HitboxY = p.y;
            return run;
        }

        static Enemy Held(RunState run, BodyKind k, double x, double y, double hp = 100)
        {
            var e = Spawn.Body(run, k, x, y);
            e.hp = e.maxHp = hp; e.stun = 1e9;   // stands still and takes what comes
            run.enemies.Add(e);
            return e;
        }

        static Projectile Shot(RunState run, double x, double y, double vx, double vy, double dmg = 4) => new Projectile
        {
            x = x, y = y, ox = x, oy = y, vx = vx, vy = vy, r = 5, dmg = dmg, friendly = true, scale = 1,
            dx = Math.Sign(vx), dy = Math.Sign(vy), gun = 0,
        };

        static Projectile Hostile(double x, double y) => new Projectile { x = x, y = y, vx = 0.5, r = 5, dmg = 2, friendly = false };

        [Test]
        public void TunedTheRunPlaysTheNewRosterAndUntunedTheJsOne()
        {
            Assert.That(Items.Roster, Is.SameAs(Items.Overhaul));
            var run = Room();
            var offer = Items.Pool(run.rng, 10, null, null);
            Assert.That(offer.All(id => Items.Overhaul.Any(d => d.Id == id)), "an item from outside the overhaul was offered");
            bool was = Balance.TuneItems;
            try { Balance.TuneItems = false; Assert.That(Items.Roster, Is.SameAs(Items.All)); }
            finally { Balance.TuneItems = was; }
        }

        [Test]
        public void AnActiveRechargesByWonFightsNotByTime()
        {
            var run = Room();
            Items.Give(run, "hush_bell");
            Assert.That(Items.Ready(run), "a new active starts full");
            run.projectiles.Add(Hostile(run.player.x + 60, run.player.y));
            Assert.That(Items.UseActive(run));
            Assert.That(Items.Ready(run), Is.False, "used, it is empty");
            Items.FightWon(run);
            Assert.That(Items.Ready(run), Is.False, "Hush Bell needs two won fights");
            Items.FightWon(run);
            Assert.That(Items.Ready(run));
            Assert.That(Items.Has(run, "hush_bell"), "a recharging active is never used up");
        }

        [Test]
        public void HushBellClearsTheHostileShellsInReachAndThrowsTheBodiesBack()
        {
            var run = Room();
            var p = run.player;
            Items.Give(run, "hush_bell");
            var near = Hostile(p.x + 100, p.y); var far = Hostile(p.x + Items.HushReach + 40, p.y);
            var mine = Shot(run, p.x + 50, p.y, 3, 0);
            run.projectiles.AddRange(new[] { near, far, mine });
            var body = Held(run, BodyKind.Lunger, p.x + 120, p.y);
            body.stun = 0;
            Assert.That(Items.UseActive(run));
            Assert.That(run.projectiles, Does.Not.Contain(near));
            Assert.That(run.projectiles, Does.Contain(far), "a shell out of reach was taken");
            Assert.That(run.projectiles, Does.Contain(mine), "the player's own shot was taken");
            Assert.That(body.kvx, Is.GreaterThan(0), "the body was not thrown back");
            var empty = Room(); Items.Give(empty, "hush_bell");
            Assert.That(Items.UseActive(empty), Is.False, "a bell in an empty room spent its charge");
            Assert.That(Items.Ready(empty));
        }

        [Test]
        public void TimeGlassSlowsTheBodiesAndTheirShellsButNotYoursAndRunsOut()
        {
            var run = Room();
            var p = run.player;
            Items.Give(run, "time_glass");
            var hostile = Hostile(p.x - 200, p.y - 150); hostile.vx = 2;
            var mine = Shot(run, p.x - 200, p.y + 150, 2, 0);
            run.projectiles.AddRange(new[] { hostile, mine });
            Held(run, BodyKind.Lunger, p.x + 200, p.y);
            Assert.That(Items.UseActive(run));
            double h0 = hostile.x, m0 = mine.x;
            TickOrder.TickProjectiles(run);
            Assert.That(hostile.x - h0, Is.EqualTo(2 * Items.TimeSlow).Within(1e-9));
            Assert.That(mine.x - m0, Is.EqualTo(2).Within(1e-9), "the player's shot was slowed");
            for (int t = 0; t < Balance.Sec(2.5) + 2; t++) TickOrder.TickBodies(run);
            Assert.That(run.timeSlowT, Is.EqualTo(0), "slow time never ran out");
        }

        [Test]
        public void RicochetShellsBounceOnceOffAWall()
        {
            var run = Room();
            Items.Give(run, "ricochet_shells");
            run.player.cooldown = 0;
            Weapons.Fire(run, Balance.RoomLeft - 100, run.player.y);
            var s = run.projectiles.Last();
            Assert.That(s.bounce, Is.EqualTo(1));
            s.x = Balance.RoomLeft + 2; s.vx = -6; s.vy = 0; s.dx = -1; s.dy = 0;
            TickOrder.TickProjectiles(run);
            Assert.That(run.projectiles, Does.Contain(s), "it left the room instead of bouncing");
            Assert.That(s.vx, Is.GreaterThan(0)); Assert.That(s.bounce, Is.EqualTo(0));
            var plain = Room(); plain.player.cooldown = 0;
            Weapons.Fire(plain, Balance.RoomLeft - 100, plain.player.y);
            Assert.That(plain.projectiles.Last().bounce, Is.EqualTo(0), "a bounce without the item");
        }

        [Test]
        public void EmberRoundsBurnAPointASecondForThreeSeconds()
        {
            var run = Room();
            Items.Give(run, "ember_rounds");
            var e = Held(run, BodyKind.Shooter, Balance.MidX + 200, Balance.MidY);
            run.projectiles.Add(Shot(run, e.x - 1, e.y, 0.1, 0, 4));
            TickOrder.TickProjectiles(run);
            double after = e.hp;
            Assert.That(e.burnT, Is.EqualTo(Items.BurnFor));
            for (int t = 0; t < Balance.Sec(3.5); t++) TickOrder.TickBodies(run);
            Assert.That(after - e.hp, Is.EqualTo(3 * Items.BurnDmg).Within(1e-9), "three seconds of burning, a point a second");
        }

        [Test]
        public void AKeptUpFireStillBurns()
        {
            // the critic's case: a hit every half second must not keep restarting the burn before its first point
            double Left(bool ember)
            {
                var run = Room();
                if (ember) Items.Give(run, "ember_rounds");
                var e = Held(run, BodyKind.Shooter, Balance.MidX + 200, Balance.MidY, 1000);
                for (int t = 0; t < Balance.Sec(4); t++)
                {
                    if (t % Balance.Sec(0.5) == 0) run.projectiles.Add(Shot(run, e.x - 1, e.y, 0.1, 0, 4));
                    TickOrder.TickProjectiles(run);
                    TickOrder.TickBodies(run);
                }
                return e.hp;
            }
            Assert.That(Left(false) - Left(true), Is.GreaterThanOrEqualTo(3 * Items.BurnDmg), "a body kept under fire never burned");
        }

        [Test]
        public void ARicochetFlownBackIsNotStrongerThanItWasOnTheWayOut()
        {
            // its fall-off reads the path flown (out to the wall and back), not the straight line to the gun
            var run = Room();
            double x0 = Balance.RoomLeft + 60;
            var s = Shot(run, x0, Balance.MidY, -6, 0);
            s.bounce = 1; s.fNear = 50; s.fFar = 400; s.fMin = 0.3;
            run.projectiles.Add(s);
            for (int t = 0; t < 30; t++) TickOrder.TickProjectiles(run);
            Assert.That(s.vx, Is.GreaterThan(0), "fixture: it never bounced");
            double path = (x0 - Balance.RoomLeft) + (s.x - Balance.RoomLeft);
            var flown = new Projectile { x = s.x, y = s.y, ox = s.x - path, oy = s.y, fNear = 50, fFar = 400, fMin = 0.3 };
            Assert.That(Combat.FalloffMult(s), Is.EqualTo(Combat.FalloffMult(flown)).Within(1e-9), "flying back toward the gun gave the shell back its strength");
            Assert.That(Combat.FalloffMult(s), Is.LessThan(1), "fixture: the path is inside the full-damage reach, so this proves nothing");
        }

        [Test]
        public void TimeGlassSlowsTheBodiesTimersNotOnlyTheirSteps()
        {
            var run = Room();
            Items.Give(run, "time_glass");
            var e = Held(run, BodyKind.Shooter, Balance.MidX + 200, Balance.MidY);
            e.stun = 0; e.noticeTimer = 1000;   // its notice clock counts once per tick it is stepped
            Assert.That(Items.UseActive(run));
            int ticks = Balance.Sec(2);
            for (int t = 0; t < ticks; t++) TickOrder.TickBodies(run);
            Assert.That(1000 - e.noticeTimer, Is.EqualTo(ticks * Items.TimeSlow).Within(2), "its clock ran at full speed");
            run.timeSlowT = Balance.Sec(1);
            Rooms.EnterRoom(run, run.curX, run.curY, Dir.S);
            Assert.That(run.timeSlowT, Is.EqualTo(0), "slow time followed the player out of the room");
        }

        [Test]
        public void ChainSparkJumpsHalfTheHitToTheNearestOtherBodyInReach()
        {
            var run = Room();
            Items.Give(run, "chain_spark");
            var a = Held(run, BodyKind.Shooter, Balance.MidX + 200, Balance.MidY);
            var b = Held(run, BodyKind.Shooter, a.x, a.y + 100);
            var c = Held(run, BodyKind.Shooter, a.x, a.y - Items.ChainReach - 30);
            run.projectiles.Add(Shot(run, a.x - 1, a.y, 0.1, 0, 4));
            TickOrder.TickProjectiles(run);
            double hitA = 100 - a.hp;
            Assert.That(hitA, Is.GreaterThan(0));
            Assert.That(100 - b.hp, Is.EqualTo(hitA * Items.ChainShare).Within(1e-9));
            Assert.That(c.hp, Is.EqualTo(100), "the spark jumped further than its reach");
        }

        [Test]
        public void GlassHeartHitsHalfAgainAsHardAndCostsAHeart()
        {
            double Hit(bool glass)
            {
                var run = Room();
                double max = run.player.maxHp;
                if (glass) { Items.Give(run, "glass_heart"); Assert.That(run.player.maxHp, Is.EqualTo(max - 1)); }
                var e = Held(run, BodyKind.Shooter, Balance.MidX + 200, Balance.MidY);
                run.projectiles.Add(Shot(run, e.x - 1, e.y, 0.1, 0, 4));
                TickOrder.TickProjectiles(run);
                return 100 - e.hp;
            }
            Assert.That(Hit(false), Is.GreaterThan(0), "fixture: the shot missed, so the comparison proves nothing");
            Assert.That(Hit(true), Is.EqualTo(Hit(false) * Items.GlassMult).Within(1e-9));
        }

        [Test]
        public void PhoenixFeatherTurnsTheFirstDeathOnlyIntoTwoHearts()
        {
            var run = Room();
            var p = run.player;
            Items.Give(run, "phoenix_feather");
            p.hp = 1; p.regenHeart = 0; p.armor = 0;
            Combat.DamagePlayer(run, 2, 0, 1, 0);
            Assert.That(p.hp, Is.EqualTo(4), "the feather did not raise the player");
            Assert.That(Items.Has(run, "phoenix_feather"), Is.False, "it did not burn away");
            Assert.That(p.iframes, Is.GreaterThanOrEqualTo(Items.PhoenixGrace));
            p.iframes = 0; p.hp = 1;
            Combat.DamagePlayer(run, 2, 0, 1, 0);
            Assert.That(p.hp, Is.LessThanOrEqualTo(0), "a second death was saved");
        }

        [Test]
        public void TinCupHealsAHeartAndRechargesInThreeFights()
        {
            var run = Room();
            var p = run.player;
            Items.Give(run, "tin_cup");
            p.hp = 2;
            Assert.That(Items.UseActive(run)); Assert.That(p.hp, Is.EqualTo(4));
            for (int i = 0; i < 2; i++) Items.FightWon(run);
            Assert.That(Items.Ready(run), Is.False);
            Items.FightWon(run);
            Assert.That(Items.Ready(run));
        }

        [Test]
        public void TheBotUsesEachActiveByItsRuleAndOnlyWhenCharged()
        {
            var run = Room();
            var p = run.player;
            Items.Give(run, "hush_bell");
            Assert.That(BotPolicy.WantsActive(run), Is.False, "rang the bell at nothing");
            for (int i = 0; i < 3; i++) run.projectiles.Add(Hostile(p.x + 100, p.y + i * 20));
            Assert.That(BotPolicy.WantsActive(run), "three shells close and it kept the bell");
            Items.UseActive(run);
            run.projectiles.Add(Hostile(p.x + 10, p.y));
            Assert.That(BotPolicy.WantsActive(run), Is.False, "it pressed an empty active");

            var cup = Room(); Items.Give(cup, "tin_cup");
            Assert.That(BotPolicy.WantsActive(cup), Is.False, "drank at full health");
            cup.player.regenHeart = 0;
            Assert.That(BotPolicy.WantsActive(cup), Is.False, "drank for the regenerating heart, which the cup cannot fill");
            cup.player.hp -= 2;
            Assert.That(BotPolicy.WantsActive(cup));
        }
    }
}
