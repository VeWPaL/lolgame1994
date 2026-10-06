using System;
using System.Collections.Generic;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The C# playtest bot's runner (csharp/Depths.Playtest): deterministic, exact about damage
    /// and healing, and never loops forever. Expected amounts come from the game's rules, not the bot.</summary>
    [TestFixture, Category("csharp-only")]
    public sealed class PlaytestBotTests
    {
        static readonly string[] Ends = { "death", "timeout", "stuck", "error" };
        static int Sixty => 60 * Balance.TickHz;   // the rubric's stuck window: 60 simulated seconds

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
            // an over-booked hit comes back as phantom healing nothing explains
            Assert.That(r.healBy.Keys.Where(k => k.StartsWith("unseen")), Is.Empty);
        }

        // one scripted tick: the harness sets the scene (the bot never does), then the runner plays it
        static Runner OneTick(Action<RunState> scene) => Ticks(1, (r, t) => { if (t == 0) scene(r); });

        // a few scripted ticks: the script may set the scene before any of them
        static Runner Ticks(int n, Action<RunState, int> script)
        {
            var run = new Runner(1, "novice", n / (60.0 * Balance.TickHz), new Script(script));
            run.Play();
            Assert.That(run.Result.ticks, Is.EqualTo(n));
            return run;
        }

        sealed class Script : IPolicy
        {
            readonly Action<RunState, int> _f;
            public Script(Action<RunState, int> f) => _f = f;
            public BotAction Decide(RunState run, int t) { _f(run, t); return BotAction.None; }
        }

        static Projectile ShellOnHitbox(Player p, BodyKind from, double dmg = 2) => new Projectile
        {
            x = p.lagX, y = p.lagY + Balance.PlayerHitDy, vx = 0.01, r = 5, dmg = dmg, friendly = false,
            owner = Enemy.Of(from, Balance.RoomLeft + 30, Balance.RoomTop + 30),
        };

        static Enemy Dummy(double x, double y)
        {
            // a held, near-unkillable body: it makes the room live and does nothing else
            var d = Enemy.Of(BodyKind.Lunger, x, y);
            d.hp = d.maxHp = 1e9;
            d.stun = 1e9;
            return d;
        }

        static Dictionary<string, double> D(params (string k, double v)[] kv) => kv.ToDictionary(x => x.k, x => x.v);

        [Test]
        public void AShotIsBookedOnItsShootersKindAndMatchesTheGamesTally()
        {
            var run = OneTick(r => r.projectiles.Add(ShellOnHitbox(r.player, BodyKind.Gunner)));
            Assert.That(run.Run.dmgTaken, Is.EqualTo(2), "the scripted shell did not land");
            Assert.That(run.Result.dmgBySource, Is.EqualTo(D(("shot:gunner", 2))));
            Assert.That(run.Result.floors[0].dmg, Is.EqualTo(run.Run.dmgTaken),
                "with no armour the layers must equal dmgTaken");
            Assert.That(run.Result.healBy, Is.Empty);
        }

        [Test]
        public void AnOrdinaryHitOnArmourCostsTheArmourRateNotTheWholeHit()
        {
            // a 2 HP gunner shell on armour 4: armour pays floor(2 x 0.6) = 1 and covers it all
            var run = OneTick(r =>
            {
                r.player.armor = 4;
                r.projectiles.Add(ShellOnHitbox(r.player, BodyKind.Gunner));
            });
            Assert.That(run.Run.player.armor, Is.EqualTo(3), "fixture: the game's armour rule changed");
            Assert.That(run.Result.dmgBySource, Is.EqualTo(D(("shot:gunner", 1))));
            Assert.That(run.Result.healBy, Is.Empty, "an over-costed hit came back as healing");
            Assert.That(run.Result.maskedHits, Is.EqualTo(0));
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
                var brunch = Enemy.Of(BodyKind.Brunch, p.lagX, p.lagY - Balance.PlayerHitDy - 13);
                brunch.hp = brunch.maxHp / 2;   // it spends itself on the contact and is gone after the step
                r.enemies.Add(brunch);
                // a live shell flying past 30px off, and one leaving the room 380px off: neither is the source
                var past = ShellOnHitbox(p, BodyKind.Shooter);
                past.x = p.x + 30; past.y = p.y; past.vx = 3;
                r.projectiles.Add(past);
                var gone = ShellOnHitbox(p, BodyKind.Gunner);
                gone.x = Balance.RoomRight + 29; gone.y = p.y; gone.vx = 3;
                r.projectiles.Add(gone);
            });
            Assert.That(run.Run.dmgTaken, Is.EqualTo(1), "the scripted contact did not land");
            Assert.That(run.Run.enemies.Any(e => e.kind == BodyKind.Brunch), Is.False, "fixture: the Brunch survived");
            Assert.That(run.Result.dmgBySource, Is.EqualTo(D(("contact:brunch", 1))));
        }

        [Test]
        public void AContactIsMeasuredToTheBodysEdge()
        {
            // a gunner (r 22) touches the contact point from 28px; a shooter (r 14) 26px off is nearer
            // by centre but does not touch it
            var run = OneTick(r =>
            {
                var p = r.player;
                double hy = p.lagY - Balance.PlayerHitDy;
                r.enemies.Add(Enemy.Of(BodyKind.Shooter, p.lagX, hy + 26));
                r.enemies.Add(Enemy.Of(BodyKind.Gunner, p.lagX, hy - 28));
            });
            Assert.That(run.Run.dmgTaken, Is.EqualTo(1), "the scripted contact did not land");
            Assert.That(run.Result.dmgBySource, Is.EqualTo(D(("contact:gunner", 1))));
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
            Assert.That(run.Result.dmgBySource, Is.EqualTo(D(("shot:shooter", 2))));
            Assert.That(run.Result.healBy, Is.EqualTo(D(("heart", 2))));
            Assert.That(run.Result.maskedHits, Is.EqualTo(1));
        }

        [Test]
        public void ASecondHiddenHitIsReplayedAsExactlyAsTheFirst()
        {
            // two hits a second apart (after the i-frames), the second hidden by a heart
            int second = Balance.Iframes + 5;
            var run = Ticks(second + 1, (r, t) =>
            {
                var p = r.player;
                if (t == 0) { p.regenHeart = 0; r.projectiles.Add(ShellOnHitbox(p, BodyKind.Gunner)); }
                if (t == second)
                {
                    r.pickups.Add(Pickup.Of("heart", p.x, p.y, 16));
                    r.projectiles.Add(ShellOnHitbox(p, BodyKind.Gunner));
                }
            });
            Assert.That(run.Run.dmgTaken, Is.EqualTo(4), "fixture: both shells must land");
            Assert.That(run.Result.dmgBySource, Is.EqualTo(D(("shot:gunner", 4))));
            Assert.That(run.Result.healBy, Is.EqualTo(D(("heart", 2))));
            Assert.That(run.Result.maskedHits, Is.EqualTo(1));
        }

        [Test]
        public void ARegenRefillAndAHitOnTheSameTickAreBothCounted()
        {
            // regen 1 of 2, one tick before its refill, in a live room: the tick refills it to 2 and then
            // a 2 HP shell empties it, so the regen layer only shows -1
            var run = OneTick(r =>
            {
                var p = r.player;
                p.regenHeart = 1; p.regenHeartT = Balance.RegenDelay - 1;
                r.enemies.Add(Dummy(Balance.RoomRight - 40, Balance.RoomBottom - 40));
                r.projectiles.Add(ShellOnHitbox(p, BodyKind.Gunner));
            });
            Assert.That(run.Run.player.regenHeart, Is.EqualTo(0), "fixture: the refill or the hit did not happen");
            Assert.That(run.Run.player.hp, Is.EqualTo(6));
            Assert.That(run.Result.dmgBySource, Is.EqualTo(D(("shot:gunner", 2))));
            Assert.That(run.Result.healBy, Is.EqualTo(D(("regen", 1))));
            Assert.That(run.Result.regenHealed, Is.EqualTo(1));
        }

        [Test]
        public void ABossHitOnArmourAndAHalfArmourOnTheSameTickAreBothCounted()
        {
            // the Warden's 3 HP shell costs armour 3 at full weight (an ordinary 3 would cost 1); the half
            // armour taken in the same tick hides 1 of it from the armour delta
            var run = OneTick(r =>
            {
                var p = r.player;
                p.armor = 4;
                r.pickups.Add(Pickup.Of("halfarmor", p.x, p.y, 16));
                r.projectiles.Add(ShellOnHitbox(p, BodyKind.Boss, 3));
            });
            Assert.That(run.Run.player.armor, Is.EqualTo(2), "fixture: 4 - 3 + 1");
            Assert.That(run.Result.dmgBySource, Is.EqualTo(D(("shot:boss", 3))));
            Assert.That(run.Result.healBy, Is.EqualTo(D(("halfarmor", 1))));
            Assert.That(run.Result.maskedHits, Is.EqualTo(1));
        }

        [Test]
        public void HealPickupsTakenTogetherAreEachBookedUnderTheirOwnKind()
        {
            var run = OneTick(r =>
            {
                var p = r.player;
                p.hp = 2;
                foreach (var k in new[] { "heart", "armor", "halfheart" }) r.pickups.Add(Pickup.Of(k, p.x, p.y, 16));
            });
            Assert.That(run.Run.player.hp, Is.EqualTo(5), "fixture: 2 + 2 + 1");
            Assert.That(run.Result.healBy, Is.EqualTo(D(("heart", 2), ("halfheart", 1), ("armor", 2))));
        }

        [TestCase(2.0, new string[0], false, "heart", 2.0, null, 0.0)]
        [TestCase(1.0, new string[0], false, "halfheart", 1.0, null, 0.0)]
        [TestCase(1.0, new string[0], true, "unseen:red", 1.0, null, 0.0)]
        [TestCase(1.0, new[] { "heart" }, true, "heart", 1.0, null, 0.0)]
        [TestCase(3.0, new[] { "heart", "halfheart" }, false, "heart", 2.0, "halfheart", 1.0)]
        [TestCase(3.0, new[] { "halfheart" }, false, "halfheart", 1.0, "heart", 2.0)]
        public void AHealIsNamedByWhatLeftTheFloorElseByItsAmount(double gain, string[] gone, bool atCap,
                                                                    string k1, double v1, string? k2, double v2)
        {
            var got = new List<(string, double)>();
            Runner.Share(gain, gone.ToList(), "heart", "halfheart", atCap, "red", (k, v) => got.Add((k, v)));
            var want = new List<(string, double)> { (k1, v1) };
            if (k2 != null) want.Add((k2, v2));
            Assert.That(got, Is.EqualTo(want));
        }

        [Test]
        public void ADeathNamesItsSource()
        {
            var run = OneTick(r =>
            {
                r.player.hp = 1; r.player.regenHeart = 0;
                r.projectiles.Add(ShellOnHitbox(r.player, BodyKind.Gunner));
            });
            Assert.That(run.Result.end, Is.EqualTo("death"));
            Assert.That(Json.Text(run.Result.cause), Is.EqualTo("[\"shot:gunner\",2]"));
        }

        [TestCase(false, 0)]
        [TestCase(true, 1)]
        public void BossTimeCountsOnlyWhileTheBossRoomIsLive(bool live, int ticks)
        {
            // the scene is set during tick 0, so tick 1 is the one counted
            var run = Ticks(2, (r, t) =>
            {
                if (t != 0) return;
                var boss = r.dungeon.AllRooms.First(x => x.Type == RoomKind.Boss);
                r.curX = boss.X; r.curY = boss.Y;
                if (live) r.enemies.Add(Dummy(Balance.RoomRight - 40, Balance.RoomBottom - 40));
            });
            Assert.That(run.Result.floors[0].bossTicks, Is.EqualTo(ticks));
        }

        static string? CauseRoom(RunResult r) =>
            r.cause is IDictionary<string, object> c && c.TryGetValue("room", out var v) ? v as string : null;

        [Test]
        public void ABotThatMakesNoProgressIsReportedStuck()
        {
            var r = new Runner(1, "novice", 5, new IdlePolicy()).Play();
            Assert.That(r.end, Is.EqualTo("stuck"));
            Assert.That(r.ticks, Is.InRange(Sixty, Sixty + 5), "the stuck check did not fire at 60 seconds");
            Assert.That(CauseRoom(r), Is.EqualTo("start"));
        }

        sealed class FireAt : IPolicy
        {
            readonly Enemy _e;
            public FireAt(Enemy e) => _e = e;
            public BotAction Decide(RunState run, int t) =>
                new BotAction(new Input(0, 0, fire: true, aimX: _e.x, aimY: _e.y));
        }

        static Runner DummyRoom(Func<Enemy, IPolicy> policy)
        {
            // no kills and no room change, only the dummy's HP can move
            var dummy = Dummy(Balance.MidX + 150, Balance.MidY);
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
            Assert.That(idle.ticks, Is.InRange(Sixty, Sixty + 5));
        }

        /// <summary>Walks through one door and back, for ever, between two empty rooms.</summary>
        sealed class Pacer : IPolicy
        {
            public Room? Home;
            public Dir Way;
            public BotAction Decide(RunState run, int t)
            {
                var d = run.CurrentRoom == Home ? Way : (Dir)(((int)Way + 2) % 4);   // N E S W: +2 is the way back
                int dx = d == Dir.E ? 1 : d == Dir.W ? -1 : 0, dy = d == Dir.S ? 1 : d == Dir.N ? -1 : 0;
                return new BotAction(new Input(dx, dy));
            }
        }

        [Test]
        public void EnteringARoomIsProgress()
        {
            var pacer = new Pacer();
            var run = new Runner(1, "novice", 2.5, pacer);
            var start = run.Run.CurrentRoom!;
            var d = new[] { Dir.N, Dir.E, Dir.S, Dir.W }.First(x =>
                start.Doors.Contains(x) && run.Run.dungeon.Neighbour(start, x)!.Type == RoomKind.Normal);
            run.Run.dungeon.Neighbour(start, d)!.Spawned = true;   // the harness empties it: no fight, no kill
            pacer.Home = start; pacer.Way = d;
            var r = run.Play();
            Assert.That(r.kills, Is.EqualTo(0));
            Assert.That(r.floors[0].rooms, Is.GreaterThan(4), "fixture: the pacer did not change rooms");
            Assert.That(r.end, Is.EqualTo("timeout"), "room changes alone were called stuck");
        }

        sealed class Throws : IPolicy
        {
            public BotAction Decide(RunState run, int t) =>
                t < 100 ? BotAction.None : throw new InvalidOperationException("boom");
        }

        [Test]
        public void AnExceptionEndsTheRunAsAnErrorAndIsRecorded()
        {
            var r = new Runner(1, "novice", 1, new Throws()).Play();
            Assert.That(r.end, Is.EqualTo("error"));
            Assert.That(r.errors, Has.Count.EqualTo(1));
            Assert.That(r.errors[0], Does.StartWith("bot: InvalidOperationException: boom @ "));
        }

        [Test]
        public void AGameExceptionIsBlamedOnTheGameNotTheBot()
        {
            // an item id the game does not know, under the player: Items.Give throws inside TickRoom
            var run = OneTick(r =>
                r.pickups.Add(new Pickup { x = r.player.x, y = r.player.y, kind = "item", id = "no_such_item" }));
            Assert.That(run.Result.end, Is.EqualTo("error"));
            Assert.That(run.Result.errors[0],
                Does.StartWith("game: ArgumentException: no item called no_such_item @ "));
            Assert.That(run.Result.errors[0], Does.Not.Contain("Runner").And.Not.Contain("/"));
        }

        [Test]
        public void EachFloorIsClosedOnTheDescentAndItsHpBalances()
        {
            var run = new Runner(1, "skilled", 7);
            var r = run.Play();
            Assert.That(r.floors.Count, Is.GreaterThanOrEqualTo(3), "fixture: two floors left in 7 minutes");
            Assert.That(r.floors.Skip(1).Any(f => f.dmg > 0), Is.True, "fixture: no damage after floor 1");
            for (int i = 0; i < r.floors.Count; i++)
            {
                var f = r.floors[i];
                Assert.That(f.floor, Is.EqualTo(i + 1));
                Assert.That(f.hpOut, Is.Not.Null);
                // every floor's own damage and healing explain its HP in and out
                Assert.That(f.hpIn + f.armorIn + f.healed - f.dmg,
                    Is.EqualTo(f.hpOut!.Value + f.armorOut!.Value).Within(1e-9),
                    "floor " + f.floor + " books damage or healing that happened on another floor");
                if (i + 1 < r.floors.Count)
                {
                    Assert.That(f.bossKilled, Is.True);
                    Assert.That(f.bossTicks, Is.GreaterThan(0));
                    Assert.That(f.rooms, Is.GreaterThan(1));
                    Assert.That(f.cleared, Is.InRange(1, f.rooms));
                    Assert.That(r.floors[i + 1].hpIn, Is.EqualTo(f.hpOut));
                }
            }
            // the counters a player would see
            Assert.That(r.items, Is.Not.Empty.And.All.Match(@"^[a-z_]+@\d+$"));
            Assert.That(r.blinks, Is.GreaterThan(0), "seven minutes of fights with no dodge blink");
            Assert.That(r.dodgeBlinks, Is.EqualTo(r.blinks));
            Assert.That(r.kills, Is.EqualTo(run.Run.kills));
        }

        static Runner LowWith(string item, double hp, double regen)
        {
            // a low player holding one active, in a live room the bot can only shoot at
            var run = new Runner(1, "average", 10.0 / 60);
            Items.Give(run.Run, item);
            run.Run.player.hp = hp; run.Run.player.regenHeart = regen;
            run.Run.enemies.Add(Dummy(Balance.MidX + 200, Balance.MidY));
            run.Play();
            return run;
        }

        [Test]
        public void QIsPressedWhenLowAndCountsOnlyWhenItHeals()
        {
            var cup = LowWith("tin_cup", 1, 0).Result;
            Assert.That(cup.qPresses, Is.GreaterThan(0), "low and holding a heal, Q was never pressed");
            Assert.That(cup.actives, Is.GreaterThan(0));
            Assert.That(cup.healBy["active"], Is.EqualTo(2 * cup.actives), "Tin Cup heals 2 a use");

            var whistle = LowWith("bone_whistle", 1, 0).Result;   // an active that heals nothing here
            Assert.That(whistle.qPresses, Is.GreaterThan(0));
            Assert.That(whistle.actives, Is.EqualTo(0), "a press that did not heal was counted as a heal");

            // low means every heart, regenerating included (the JS hp): 1 red + 2 regen is not low
            Assert.That(LowWith("tin_cup", 1, 2).Result.qPresses, Is.EqualTo(0));
        }

        [Test]
        public void TheJsonIsLfOnlyAndReadable()
        {
            var b = new Batch { label = "x", brunch = "A+", tickHz = 210, minutes = 1,
                                seeds = { 1 }, profiles = { "novice" } };
            b.runs.Add(new Runner(1, "novice", 0.1).Play());
            string text = Json.Write(b);
            Assert.That(text, Does.Not.Contain("\r"));
            Assert.That(text, Does.Contain("\"brunch\": \"A+\""));
        }
    }
}
