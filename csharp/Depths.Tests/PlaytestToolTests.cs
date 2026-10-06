using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The playtest bot's player (BotPolicy), its command line and its report.</summary>
    [TestFixture]
    public sealed class PlaytestToolTests
    {
        [Test]
        public void TheProfilesAreTheJsBotsTable()
        {
            // tools/playtest.js: react, aimErr, dodge, band, blast, heal
            var js = new (string, int, double, double, double, double, double, double)[]
            {
                ("novice", 55, 0.14, 0.2, 110, 220, 0.25, 1),
                ("average", 38, 0.07, 0.55, 150, 260, 0.55, 2),
                ("skilled", 24, 0.03, 0.9, 170, 280, 0.85, 2),
            };
            var table = Profile.All.Select(p => (p.Name, p.React, p.AimErr, p.Dodge, p.BandMin, p.BandMax, p.Blast, p.Heal));
            Assert.That(table, Is.EqualTo(js));
        }

        static RunState Floor(uint seed)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            return run;
        }

        [Test]
        public void ADodgeWaitsForTheReactionTime()
        {
            // a body 200px east (inside the band: circling moves only north or south) and a shell 40px
            // north falling onto the player; only a dodge moves east or west
            var run = Floor(1);
            var p = run.player;
            run.enemies.Add(Enemy.Of(BodyKind.Lunger, p.x + 200, p.y));
            run.projectiles.Add(new Projectile { x = p.x, y = p.y - 40, vy = 2, r = 5, dmg = 2 });
            var bot = new BotPolicy(Profile.Get("average"), 1);
            int react = Profile.Get("average").React * Balance.TickHz / 210;
            Assert.That(bot.Decide(run, 0).Input.dx, Is.EqualTo(0), "dodged a shell it has only just seen");
            Assert.That(bot.Decide(run, react - 1).Input.dx, Is.EqualTo(0), "dodged before its reaction time");
            Assert.That(bot.Decide(run, react).Input.dx, Is.Not.EqualTo(0), "never dodged a shell heading for it");
        }

        // follow the route room by room, as the bot would walk it; the room where it stops
        static Room Walk(RunState run, BotPolicy bot)
        {
            for (int i = 0; i < 40; i++)
            {
                var d = bot.Route(run);
                if (d == null) return run.CurrentRoom!;
                var n = run.dungeon.Neighbour(run.CurrentRoom!, d.Value)!;
                run.curX = n.X; run.curY = n.Y;
            }
            throw new AssertionException("the route never settles");
        }

        static void VisitAllBut(RunState run, Room? keep)
        {
            foreach (var r in run.dungeon.AllRooms) r.Visited = r != keep;
        }

        [Test]
        public void TheRouteTakesUnvisitedRoomsBeforeTheBoss()
        {
            var run = Floor(7);
            var far = run.dungeon.AllRooms.Where(r => r.Type == RoomKind.Normal)
                .OrderByDescending(r => Math.Abs(r.X - run.curX) + Math.Abs(r.Y - run.curY)).First();
            VisitAllBut(run, far);
            run.bossUnlocked = true;   // the boss is open, and still comes last
            Assert.That(Walk(run, new BotPolicy(Profile.Get("novice"), 7)), Is.SameAs(far));
        }

        [Test]
        public void TheRouteRespectsSealedDoorsUntilTheKeyIsHeld()
        {
            var run = Floor(7);
            var start = run.CurrentRoom!;
            VisitAllBut(run, null);
            var bot = new BotPolicy(Profile.Get("novice"), 7);
            Assert.That(bot.Route(run), Is.Null, "walked toward a sealed item or boss door without its key");
            run.player.hasSilver = true;
            run.player.hasGold = true;
            Assert.That(Walk(run, bot).Type, Is.EqualTo(RoomKind.Item),
                "with both keys the item room comes before the boss");
            run.curX = start.X; run.curY = start.Y;
            run.player.hasSilver = false;
            Assert.That(Walk(run, bot).Type, Is.EqualTo(RoomKind.Boss));
        }

        static Batch Hand(string label, string profile, double dmg2)
        {
            int hz = 210;
            var r = new RunResult
            {
                seed = 1, profile = profile, floor = 2, ticks = hz * 180, end = "death",
                hits = 8, shots = 10, kills = 6,
                cause = new object[] { "contact:lunger", 3.0 },
            };
            r.floors.Add(new FloorRecord { floor = 1, ticks = hz * 120, bossTicks = hz * 30, bossKilled = true,
                                           dmg = 4, healed = 2, hpOut = 6 });
            r.floors.Add(new FloorRecord { floor = 2, ticks = hz * 60, bossTicks = 0, dmg = dmg2, hpOut = 0 });
            return new Batch { label = label, tickHz = hz, minutes = 20, brunch = "A+",
                               seeds = { 1 }, profiles = { profile }, runs = { r } };
        }

        static string Row(string md, string name) =>
            md.Split('\n').First(l => l.StartsWith("| " + name + " |", StringComparison.Ordinal));

        [Test]
        public void TheReportUsesTheJsDefinitions()
        {
            string md = Report.Build(new[] { Hand("a", "novice", 2) });
            // floor 1 was left (2 min, 30 s of boss), floor 2 was not; damage is 4 then 2
            Assert.That(Row(md, "minutes per cleared floor"), Does.EndWith("| 2.0 | 2.0 |"), "only floors left count");
            Assert.That(Row(md, "boss fight, seconds"), Does.EndWith("| 30.0 | 30.0 |"), "only floors left count");
            Assert.That(Row(md, "damage taken per floor (hp, 2 = 1 heart)"), Does.EndWith("| 3.0 | 3.0 |"),
                "every floor counts");
            Assert.That(Row(md, "accuracy"), Does.EndWith("| 80% | 80% |"));
            Assert.That(md, Does.Contain("| death | contact:lunger 3.0 hp |"));
        }

        [Test]
        public void AnABReportShowsDeltasAndSaysWhatIsMissing()
        {
            string same = Report.Build(new[] { Hand("a", "novice", 2), Hand("b", "novice", 4) });
            Assert.That(Row(same, "damage taken per floor (hp, 2 = 1 heart)"), Does.EndWith("| 3.0 | 4.0 | +1.0 |"));
            Assert.That(Row(same, "deaths"), Is.EqualTo("| deaths | 1 | 1 | = |"));
            Assert.That(same, Does.Not.Contain("Warning"));

            string apart = Report.Build(new[] { Hand("a", "novice", 2), Hand("b", "skilled", 2) });
            Assert.That(apart, Does.Contain("Warning: profiles differ: novice vs skilled"));
            int novice = apart.IndexOf("## novice", StringComparison.Ordinal);
            Assert.That(Row(apart.Substring(novice), "deaths"), Is.EqualTo("| deaths | 1 | n/a | n/a |"));
            Assert.That(apart, Does.Not.Contain("- skilled: \n").And.Not.Contain("- novice: \n"));
        }

        [TestCase("play", "--seeds", "-1")]
        [TestCase("play", "--seeds", "99999999999")]
        [TestCase("play", "--seeds", "1,,2")]
        [TestCase("play", "--minutes", "-3")]
        [TestCase("play", "--minutes", "0")]
        [TestCase("play", "--profiles", "expert")]
        [TestCase("play", "--brunch", "C")]
        [TestCase("play", "--label", "../x")]
        [TestCase("play", "--bogus", "1")]
        [TestCase("play", "--label")]
        [TestCase("report")]
        [TestCase("report", "--out", "somewhere")]
        [TestCase("report", "a", "b", "c")]
        [TestCase("frob")]
        public void ABadCommandLineIsRefusedWithOneLine(params string[] args)
        {
            var e = Assert.Throws<UsageException>(() => Cli.Parse(args))!;
            Assert.That(e.Message, Is.Not.Empty);
            if (args[0] != "frob") Assert.That(e.Message, Does.Not.Contain("\n"));
        }

        [Test]
        public void TheDefaultsAreTheJsBots()
        {
            var c = Cli.Parse(new[] { "play" });
            Assert.That(c.Label, Is.EqualTo("current"));
            Assert.That(c.Seeds, Is.EqualTo(new uint[] { 1, 7, 42, 777, 2024, 31337 }));
            Assert.That(c.Profiles, Is.EqualTo(new[] { "novice", "average", "skilled" }));
            Assert.That(c.Minutes, Is.EqualTo(20));
            Assert.That(Cli.Parse(new[] { "report", "a", "b" }).Labels, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void MainExitsNonZeroOnBadInputAndBadFiles()
        {
            var err = Console.Error;
            Console.SetError(TextWriter.Null);   // the one-line messages are not test output
            try { MainOnBadInput(); }
            finally { Console.SetError(err); }
        }

        static void MainOnBadInput()
        {
            Assert.That(Program.Main(new[] { "play", "--seeds", "-1" }), Is.EqualTo(1));
            string dir = Path.Combine(Path.GetTempPath(), "depths-playtest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "n.json"), "null");
                File.WriteAllText(Path.Combine(dir, "bad.json"), "{\"runs\":");
                Assert.That(Program.Main(new[] { "report", "n", "--out", dir }), Is.EqualTo(1));
                Assert.That(Program.Main(new[] { "report", "bad", "--out", dir }), Is.EqualTo(1));
                Assert.That(Program.Main(new[] { "report", "missing", "--out", dir }), Is.EqualTo(1));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
