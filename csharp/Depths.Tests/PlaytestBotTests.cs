using System;
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
        }

        [Test]
        public void ABotThatMakesNoProgressIsReportedStuck()
        {
            var r = new Runner(1, "novice", 5, new IdlePolicy()).Play();
            Assert.That(r.end, Is.EqualTo("stuck"));
            Assert.That(r.ticks, Is.LessThan(5 * 60 * Balance.TickHz), "the stuck check never fired; the run ran to its cap");
            Assert.That(r.cause, Is.Not.Null);
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
