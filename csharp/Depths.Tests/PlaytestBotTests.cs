using System;
using System.Collections.Generic;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The C# playtest bot (csharp/Depths.Playtest): deterministic, honest about damage, and never loops forever.</summary>
    [TestFixture]
    public sealed class PlaytestBotTests
    {
        static readonly string[] Ends = { "death", "timeout", "stuck", "error" };

        [Test]
        public void SameSeedProfileAndMinutesGiveTheSameJson()
        {
            string a = Json.Write(new Runner(7, "average", 1.5).Play());
            string b = Json.Write(new Runner(7, "average", 1.5).Play());
            Assert.That(b, Is.EqualTo(a), "two plays of one seed and profile differ, so an A/B delta would be noise");
            string c = Json.Write(new Runner(42, "average", 1.5).Play());
            Assert.That(c, Is.Not.EqualTo(a), "two seeds gave the same run, so the seed is not reaching the game");
        }

        [Test]
        public void AShortRunEndsInAValidStateWithNoErrors()
        {
            var r = new Runner(1, "novice", 1).Play();
            Assert.That(Ends, Does.Contain(r.end));
            Assert.That(r.errors, Is.Empty);
            Assert.That(r.ticks, Is.GreaterThan(0).And.LessThanOrEqualTo(60 * Balance.TickHz));
            Assert.That(r.floors.Sum(f => f.ticks), Is.EqualTo(r.ticks));
            Assert.That(r.shots, Is.GreaterThan(0), "a minute in and the bot never fired");
        }

        [Test]
        public void DamageAttributionSumsToTheHpActuallyLost()
        {
            var run = new Runner(31337, "novice", 2.5);
            var p0 = run.Run.player;
            double start = p0.hp + p0.regenHeart + p0.armor;
            var r = run.Play();
            var p = run.Run.player;
            double bySource = r.dmgBySource.Values.Sum(), byFloor = r.floors.Sum(f => f.dmg);
            Assert.That(bySource, Is.GreaterThan(0), "the fixture took no damage, so it checks nothing");
            Assert.That(r.healBy.Values.Sum(), Is.EqualTo(r.healed).Within(1e-9));
            Assert.That(byFloor, Is.EqualTo(bySource).Within(1e-9));
            // every layer: red, the regenerating heart and armour
            Assert.That(start + r.healed - bySource, Is.EqualTo(p.hp + p.regenHeart + p.armor).Within(1e-9),
                "damage minus healing does not match the HP the player actually lost");
            Assert.That(r.dmgBySource.Keys.All(k => k.StartsWith("shot:") || k.StartsWith("contact:")), Is.True);
            // armour costs less than the hit, so the layers can only fall short of the game's own tally
            Assert.That(bySource, Is.LessThanOrEqualTo(run.Run.dmgTaken + 1e-9));
        }

        // one scripted tick: the harness sets the scene (the bot never does), then the runner plays it
        static Runner OneTick(Action<RunState> scene)
        {
            var run = new Runner(1, "novice", 1.0 / (60 * Balance.TickHz), new IdlePolicy());
            scene(run.Run);
            run.Play();
            Assert.That(run.Result.ticks, Is.EqualTo(1));
            return run;
        }

        static Projectile ShellOnHitbox(Player p, BodyKind from) => new Projectile
        {
            x = p.lagX, y = p.lagY + Balance.PlayerHitDy, vx = 0.01, r = 5, dmg = 2, friendly = false,
            owner = Enemy.Of(from, Balance.RoomLeft + 30, Balance.RoomTop + 30),
        };

        [Test]
        public void AShotIsBookedOnItsShootersKindAndMatchesTheGamesTally()
        {
            var run = OneTick(r => r.projectiles.Add(ShellOnHitbox(r.player, BodyKind.Gunner)));
            Assert.That(run.Run.dmgTaken, Is.EqualTo(2), "the scripted shell did not land");
            Assert.That(run.Result.dmgBySource, Is.EqualTo(new Dictionary<string, double> { ["shot:gunner"] = 2 }));
            Assert.That(run.Result.floors[0].dmg, Is.EqualTo(run.Run.dmgTaken), "with no armour the layers must equal dmgTaken");
        }

        [Test]
        public void AContactIsBookedOnTheBodyTouchingTheHitboxNotTheOneNearestTheCentre()
        {
            // the game tests contact at (lagX, lagY - PlayerHitDy): the Brunch above touches it, the
            // lunger below is nearer the player's centre but does not
            var run = OneTick(r =>
            {
                var p = r.player;
                r.enemies.Add(Enemy.Of(BodyKind.Lunger, p.lagX, p.lagY + 20));
                r.enemies.Add(Enemy.Of(BodyKind.Brunch, p.lagX, p.lagY - Balance.PlayerHitDy - 13));
            });
            Assert.That(run.Run.dmgTaken, Is.EqualTo(1), "the scripted contact did not land");
            Assert.That(run.Result.dmgBySource, Is.EqualTo(new Dictionary<string, double> { ["contact:brunch"] = 1 }));
        }

        [Test]
        public void AHitAndAHeartOnTheSameTickAreBothCounted()
        {
            // red 4 of 6, no regen or armour: a 2 HP shell and a heart cancel in the red layer
            var run = OneTick(r =>
            {
                var p = r.player;
                p.hp = 4; p.regenHeart = 0;
                r.pickups.Add(Pickup.Of("heart", p.x, p.y, 16));
                r.projectiles.Add(ShellOnHitbox(p, BodyKind.Shooter));
            });
            Assert.That(run.Run.player.hp, Is.EqualTo(4), "the fixture did not net out, so it tests nothing");
            Assert.That(run.Run.dmgTaken, Is.EqualTo(2));
            Assert.That(run.Result.dmgBySource, Is.EqualTo(new Dictionary<string, double> { ["shot:shooter"] = 2 }));
            Assert.That(run.Result.healBy, Is.EqualTo(new Dictionary<string, double> { ["heart"] = 2 }));
            Assert.That(run.Result.maskedHits, Is.EqualTo(1));
        }

        static int StuckTicks => (int)(Runner.StuckSeconds * Balance.TickHz);

        static string? CauseRoom(RunResult r) =>
            r.cause is IDictionary<string, object> c && c.TryGetValue("room", out var v) ? v as string : null;

        [Test]
        public void ABotThatMakesNoProgressIsReportedStuck()
        {
            var r = new Runner(1, "novice", 5, new IdlePolicy()).Play();
            Assert.That(r.end, Is.EqualTo("stuck"));
            Assert.That(r.ticks, Is.InRange(StuckTicks, StuckTicks + 5), "the stuck check fired late, or not at all");
            Assert.That(CauseRoom(r), Is.EqualTo("start"));
        }

        sealed class FireAt : IPolicy
        {
            readonly Enemy _e;
            public FireAt(Enemy e) => _e = e;
            public BotAction Decide(RunState run, int t) => new BotAction(new Input(0, 0, fire: true, aimX: _e.x, aimY: _e.y));
        }

        static Runner DummyRoom(Func<Enemy, IPolicy> policy)
        {
            // a held, near-unkillable body in the start room: no kills and no room change, only its HP
            var dummy = Enemy.Of(BodyKind.Lunger, Balance.MidX + 150, Balance.MidY);
            dummy.hp = dummy.maxHp = 1e9;
            dummy.stun = 1e9;
            var run = new Runner(1, "novice", 1.5, policy(dummy));
            run.Run.enemies.Add(dummy);
            return run;
        }

        [Test]
        public void DamageToALiveEnemyIsProgress()
        {
            var shooting = DummyRoom(e => new FireAt(e)).Play();
            Assert.That(shooting.end, Is.EqualTo("timeout"), "a long fight with no kill was called stuck");
            Assert.That(shooting.hits, Is.GreaterThan(0));
            var idle = DummyRoom(e => new IdlePolicy()).Play();
            Assert.That(idle.end, Is.EqualTo("stuck"), "control: the same room with no damage dealt must be stuck");
            Assert.That(idle.ticks, Is.InRange(StuckTicks, StuckTicks + 5));
        }

        sealed class Throws : IPolicy
        {
            public BotAction Decide(RunState run, int t) => t < 100 ? BotAction.None : throw new InvalidOperationException("boom");
        }

        [Test]
        public void AnExceptionEndsTheRunAsAnErrorAndIsRecorded()
        {
            var r = new Runner(1, "novice", 1, new Throws()).Play();
            Assert.That(r.end, Is.EqualTo("error"));
            Assert.That(r.errors, Has.Count.EqualTo(1));
            Assert.That(r.errors[0], Does.Contain("boom"));
        }
    }
}
