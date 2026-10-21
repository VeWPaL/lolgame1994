using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Depths.Playtest;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>The playtest bot's player (BotPolicy, from tools/playtest.js), its command line and its
    /// report (the definitions of tools/playtest-report.js).</summary>
    [TestFixture, Category("csharp-only")]
    public sealed class PlaytestToolTests
    {
        // ---- the player

        [Test]
        public void TheProfilesAreTheJsBotsTable()
        {
            // tools/playtest.js: react (ticks at its 210 Hz, which this suite runs at), aimErr, dodge, band, blast, heal
            var js = new (string, int, double, double, double, double, double, double)[]
            {
                ("novice", 55, 0.14, 0.2, 110, 220, 0.25, 1),
                ("average", 38, 0.07, 0.55, 150, 260, 0.55, 2),
                ("skilled", 24, 0.03, 0.9, 170, 280, 0.85, 2),
            };
            var table = Profile.All.Select(p => (p.Name, p.ReactTicks, p.AimErr, p.Dodge, p.BandMin, p.BandMax, p.Blast, p.Heal));
            Assert.That(table, Is.EqualTo(js));
        }

        [Test]
        public void TheBotRngIsTheJsBotsXorshift()
        {
            // bs=(seed*2654435761+profile.length*977)>>>0, then xorshift 13/17/5, over 2^32
            var a = new BotRng(1, "novice");
            Assert.That(new[] { a.Next(), a.Next(), a.Next() },
                Is.EqualTo(new[] { 0.9084104385692626, 0.07584352069534361, 0.8662234484218061 }));
            var b = new BotRng(31337, "skilled");
            Assert.That(new[] { b.Next(), b.Next(), b.Next() },
                Is.EqualTo(new[] { 0.31965998793020844, 0.24317877227440476, 0.20186557015404105 }));
        }

        static RunState Floor(uint seed)
        {
            var run = new RunState(new Rng(seed));
            run.Start(seed);
            return run;
        }

        // the player in the start room's centre with one body due east at distance d
        static (RunState run, BotPolicy bot) Facing(double d, string profile = "average")
        {
            var run = Floor(1);
            run.enemies.Add(Enemy.Of(BodyKind.Lunger, run.player.x + d, run.player.y));
            return (run, new BotPolicy(Profile.Get(profile), 1));
        }

        static int React(string profile) => Profile.Get(profile).ReactTicks;

        [Test]
        public void ADodgeWaitsForTheReactionTime()
        {
            // the body is inside the band, so circling moves only north or south; a shell 40px north
            // falls onto the player, and only a dodge moves east or west
            var (run, bot) = Facing(200);
            var p = run.player;
            run.projectiles.Add(new Projectile { x = p.x, y = p.y - 40, vy = 2, r = 5, dmg = 2 });
            int react = React("average");
            Assert.That(bot.Decide(run, 0).Input.dx, Is.EqualTo(0), "dodged a shell it has only just seen");
            Assert.That(bot.Decide(run, react - 1).Input.dx, Is.EqualTo(0), "dodged before its reaction time");
            Assert.That(bot.Decide(run, react).Input.dx, Is.Not.EqualTo(0), "never dodged a shell heading for it");
        }

        [Test]
        public void AShellFurtherThanThirtyTicksOutIsNotDodged()
        {
            var (run, bot) = Facing(200);
            var p = run.player;
            run.projectiles.Add(new Projectile { x = p.x, y = p.y - 100, vy = 2, r = 5, dmg = 2 });   // 50 ticks out
            bot.Decide(run, 0);
            Assert.That(bot.Decide(run, React("average")).Input.dx, Is.EqualTo(0));
        }

        [Test]
        public void ACloseShellIsSometimesBlinkedAndOnlyAfterTheReactionTime()
        {
            // 20px out (10 ticks): under the blink threshold of 12; the skilled bot blinks at 0.9 x 0.15 a tick
            var (run, bot) = Facing(200, "skilled");
            var p = run.player;
            run.projectiles.Add(new Projectile { x = p.x, y = p.y - 20, vy = 2, r = 5, dmg = 2 });
            int react = React("skilled"), early = 0, late = 0;
            for (int t = 0; t < react; t++) if (bot.Decide(run, t).Input.blink) early++;
            for (int t = react; t < react + 200; t++) if (bot.Decide(run, t).Input.blink) late++;
            Assert.That(early, Is.EqualTo(0));
            Assert.That(late, Is.InRange(5, 60), "200 chances at 0.135 should blink about 27 times");
        }

        // a Mortar shell due to land `ticks` from now at (dx, dy) from the player's hitbox
        static Projectile Lob(Player p, double dx, double dy, int ticks) => new Projectile
        {
            x = p.x, y = p.y, ox = p.x, oy = p.y, tx = p.lagX + dx, ty = p.lagY + Balance.PlayerHitDy + dy, lob = true,
            flightMax = Middle.LobFlight, flight = Middle.LobFlight - ticks, r = 6, dmg = 2, blastR = 56,
        };

        // a Burrower in the air, airborne for `airborne` ticks, landing at (dx, dy) from the player's hitbox
        static Enemy Leaping(Player p, double dx, double dy, int airborne)
        {
            var e = Enemy.Of(BodyKind.Burrower, p.x - 150, p.y);
            e.buried = true; e.move = "leap"; e.moveT = Middle.BurrowLeap - airborne;
            e.markX = p.lagX + dx; e.markY = p.lagY + Balance.PlayerHitDy + dy;
            return e;
        }

        [Test]
        public void AMortarMarkIsRunOutOfOnceSeen()
        {
            // the body east is in the band, so circling moves only north or south: east or west is the mark's doing
            var (run, bot) = Facing(200);
            var p = run.player;
            run.projectiles.Add(Lob(p, 20, 0, 50));   // its centre 20px east: the way out is west
            int react = React("average");
            Assert.That(bot.Decide(run, 0).Input.dx, Is.EqualTo(0), "ran from a mark it has only just seen");
            Assert.That(bot.Decide(run, react - 1).Input.dx, Is.EqualTo(0), "ran before its reaction time");
            var go = bot.Decide(run, react).Input;
            Assert.That((go.dx, go.dy), Is.EqualTo((-1, 0)), "did not run straight out of the mark");
        }

        [Test]
        public void AMarkFarAwayIsIgnoredAndOneOnItsCourseIsNot()
        {
            int react = React("average");
            var (run, bot) = Facing(200);
            run.projectiles.Add(Lob(run.player, 0, 200, 50));
            bot.Decide(run, 0);
            Assert.That(bot.Decide(run, react).Input.dx, Is.EqualTo(0), "ran from a mark 200px off it was never near");

            // standing 80px west of a Burrower's landing and walking at it: by the landing it is well inside (at any tick rate)
            int left = Math.Min(20, Middle.BurrowLeap - react);
            (run, bot) = Facing(200);
            run.player.vx = 70.0 / left;
            run.enemies.Add(Leaping(run.player, 80, 0, Middle.BurrowLeap - left));
            Assert.That(bot.Decide(run, 0).Input.dx, Is.EqualTo(-1), "walked on into a landing");
            (run, bot) = Facing(200);
            run.enemies.Add(Leaping(run.player, 80, 0, Middle.BurrowLeap - left));
            Assert.That(bot.Decide(run, 0).Input.dx, Is.EqualTo(0), "standing still, a mark 80px off is no threat");
        }

        [Test]
        public void ALeapIsDodgedOnlyOnceTheBotHasSeenItInTheAir()
        {
            int react = React("average");
            var (run, bot) = Facing(200);
            run.enemies.Add(Leaping(run.player, -10, 0, react - 1));
            Assert.That(bot.Decide(run, 0).Input.dx, Is.EqualTo(0), "dodged a leap before it could have seen it");
            (run, bot) = Facing(200);
            run.enemies.Add(Leaping(run.player, -10, 0, react));
            Assert.That(bot.Decide(run, 0).Input.dx, Is.EqualTo(1), "never ran from a Burrower landing on it");
        }

        [Test]
        public void AMarkTooCloseToRunFromIsSometimesBlinkedAndOneWithTimeToSpareNever()
        {
            // dead centre, 3 ticks left: no run clears 48px in 3 ticks, so the skilled bot rolls its blink each tick
            int react = React("skilled"), soon = 0, spare = 0;
            var (run, bot) = Facing(200, "skilled");
            var e = Leaping(run.player, 0, 0, Middle.BurrowLeap - 3);
            run.enemies.Add(e);
            for (int t = 0; t < 200; t++) if (bot.Decide(run, t).Input.blink) soon++;
            Assert.That(soon, Is.InRange(5, 60), "200 chances at 0.135 should blink about 27 times");

            (run, bot) = Facing(200, "skilled");
            run.projectiles.Add(Lob(run.player, 30, 0, Middle.LobFlight - 1));
            for (int t = 0; t < 200; t++) if (bot.Decide(run, t).Input.blink) spare++;
            Assert.That(spare, Is.EqualTo(0), "blinked from a mark it could walk out of");
            Assert.That(react, Is.LessThan(Middle.BurrowLeap), "fixture: the skilled bot cannot see a leap in time");
        }

        [Test]
        public void AMarkAgainstAWallIsLeftAlongTheWallNotIntoIt()
        {
            // the player against the west wall, a landing just east of it: the way out is along the wall, never west
            var (run, bot) = Facing(200);
            var p = run.player;
            p.x = p.lagX = run.CurrentRoom!.Bounds.L + 14;
            run.enemies.Add(Leaping(p, 16, 0, React("average")));
            var go = bot.Decide(run, 0).Input;
            Assert.That(go.dx, Is.EqualTo(0), "pressed into the wall");
            Assert.That(go.dy, Is.Not.EqualTo(0), "stood still in the mark");
        }

        [Test]
        public void AShellAndAMarkAtOnceAreBothDodged()
        {
            // a shell falling from the north onto the player (a sidestep is east or west) inside a landing to the south-east
            var (run, bot) = Facing(200);
            var p = run.player;
            int react = React("average");
            run.projectiles.Add(new Projectile { x = p.x, y = p.y - 40, vy = 2, r = 5, dmg = 2 });
            run.enemies.Add(Leaping(p, 10, 10, Middle.BurrowLeap - 25));
            bot.Decide(run, 0);
            var both = bot.Decide(run, react).Input;
            Assert.That(both.dx, Is.EqualTo(1), "the sidestep (east, the stronger pull) was lost to the mark");
            Assert.That(both.dy, Is.EqualTo(-1), "the mark's way out (north) was lost to the sidestep");
        }

        // a Lunger winding up at the player from `from` px east, its line passing `off` px north of the hitbox
        static Enemy Winding(Player p, double from, double off)
        {
            var e = Enemy.Of(BodyKind.Lunger, p.lagX + from, p.lagY + Balance.PlayerHitDy - off);
            e.lungeState = "wind"; e.lungeT = Balance.LungeWindup; e.lungeDx = -1; e.lungeDy = 0; e.lungeLen = from + 40;
            return e;
        }

        static (double x, double y) MarksAt(BotPolicy bot, RunState run, int t)
        {
            bool blink = false;
            return bot.Marks(run, run.CurrentRoom!, t, false, ref blink) ? bot.Move : (double.NaN, double.NaN);
        }

        [TestCase(6, 1)]     // the line 6px north of the hitbox: off it to the south
        [TestCase(-6, -1)]   // and 6px south: off it to the north (whichever way the bot happens to circle)
        public void ALungersLineIsSteppedOffOnceSeenToTheSideTheBotIsOn(double off, int way)
        {
            var (run, bot) = Facing(200);
            run.enemies.Add(Winding(run.player, 150, off));
            int react = React("average");
            Assert.That(double.IsNaN(MarksAt(bot, run, 0).x), "stepped off a line it has only just seen");
            Assert.That(double.IsNaN(MarksAt(bot, run, react - 1).x), "stepped off before its reaction time");
            var (x, y) = MarksAt(bot, run, react);
            Assert.That(y * way, Is.GreaterThan(1.5), "did not step off to its own side");
            Assert.That(Math.Abs(x), Is.LessThan(0.2), "stepped along the line, not off it");
        }

        [Test]
        public void OffALineIntoAWallCrossesTheLineInstead()
        {
            // the bot against the south wall, a lunge line 6px north of it: south is the wall, sliding would run along the line
            var (run, bot) = Facing(200);
            var p = run.player;
            p.y = p.lagY = run.CurrentRoom!.Bounds.B - 14;
            run.enemies.Add(Winding(p, 150, 6));
            MarksAt(bot, run, 0);   // first seen
            var (x, y) = MarksAt(bot, run, React("average"));
            Assert.That(double.IsNaN(y), Is.False, "never stepped off the line (NaN compares below every number)");
            Assert.That(y, Is.LessThan(-1.5), "pressed into the wall or slid along the line instead of crossing it");
            Assert.That(Math.Abs(x), Is.LessThan(0.2));
        }

        [Test]
        public void ADashIsReadFromItsFirstTickBeforeTheBodyHasMoved()
        {
            // the tell's standstill: on the tick the dash starts its velocity is still zero
            var (run, bot) = Facing(200);
            var p = run.player;
            var b = Enemy.Of(BodyKind.Brunch, p.lagX - 70, p.lagY + Balance.PlayerHitDy - 4);
            b.rushing = true; b.rushT = 30; b.rushDx = 1; b.rushDy = 0; b.vx = b.vy = 0;
            b.runSpeed = Balance.BrunchRun;
            run.enemies.Add(b);
            MarksAt(bot, run, 0);
            Assert.That(MarksAt(bot, run, React("average")).y, Is.GreaterThan(1.5), "blind to a dash whose body has not moved yet");
        }

        [Test]
        public void ALineThatMissesIsIgnored()
        {
            var (run, bot) = Facing(200);
            var e = Winding(run.player, 150, 60);   // 60px off the hitbox: clear of it
            run.enemies.Add(e);
            MarksAt(bot, run, 0);
            Assert.That(double.IsNaN(MarksAt(bot, run, React("average")).x), "dodged a line 60px away");
            e.lungeDx = 1; e.y = run.player.lagY + Balance.PlayerHitDy;   // on the hitbox's row, but charging away from it
            Assert.That(double.IsNaN(MarksAt(bot, run, React("average") + 1).x), "dodged a lunge going the other way");
        }

        [Test]
        public void ABrunchsTellAndItsDashAreSteppedOff()
        {
            var (run, bot) = Facing(200);
            var p = run.player;
            var b = Enemy.Of(BodyKind.Brunch, p.lagX - 70, p.lagY + Balance.PlayerHitDy - 4);   // west, a hair north
            b.rushWind = Balance.BrunchRushWind;
            run.enemies.Add(b);
            int react = React("average");
            Assert.That(double.IsNaN(MarksAt(bot, run, 0).x));
            var tell = MarksAt(bot, run, react);   // the tell points straight at the hitbox: either side is off it
            Assert.That(Math.Abs(tell.y), Is.GreaterThan(1.5), "stood on a Brunch's tell");
            Assert.That(Math.Abs(tell.x), Is.LessThan(0.2), "backed along the tell's line instead of leaving it");

            // a fresh tell is a fresh reaction: the dash after it is seen as part of it
            b.rushWind = 0; b.rushing = true; b.rushT = 40; b.rushDx = 1; b.rushDy = 0; b.runSpeed = Balance.BrunchRun;
            Assert.That(MarksAt(bot, run, react + 1).y, Is.GreaterThan(1.5), "stood in a Brunch's dash");
            b.rushing = false; b.rushT = 0;
            MarksAt(bot, run, react + 2);   // the attack is over: the clock is forgotten
            b.rushWind = Balance.BrunchRushWind;
            Assert.That(double.IsNaN(MarksAt(bot, run, react + 3).x), "a new tell was taken as already seen");
        }

        [TestCase(50, -1, true)]     // too close: back off
        [TestCase(200, 0, true)]     // in the band: circle only
        [TestCase(350, 1, true)]     // too far: close in, and fire (under 420)
        [TestCase(500, 1, false)]    // out of range: no fire
        public void TheBandAndTheRange(double d, int dx, bool fire)
        {
            var (run, bot) = Facing(d);
            var input = bot.Decide(run, 0).Input;
            Assert.That(input.dx, Is.EqualTo(dx));
            Assert.That(input.fire, Is.EqualTo(fire));
        }

        [Test]
        public void TheBotStaysOffTheWalls()
        {
            var (run, bot) = Facing(200);
            run.player.x = Balance.RoomLeft + 20;   // inside the 70px margin
            run.enemies[0].x = run.player.x + 200;
            Assert.That(bot.Decide(run, 0).Input.dx, Is.EqualTo(1));
        }

        [TestCase(3, true)]
        [TestCase(2, false)]
        public void TheAltIsThrownOnlyIntoACrowdOfThree(int near, bool ever)
        {
            var (run, bot) = Facing(100);
            var p = run.player;
            if (near >= 2) run.enemies.Add(Enemy.Of(BodyKind.Lunger, p.x - 100, p.y));
            if (near >= 3) run.enemies.Add(Enemy.Of(BodyKind.Lunger, p.x, p.y + 100));
            int react = React("average"), alts = 0;
            for (int k = 0; k < 20; k++) if (bot.Decide(run, k * react).Input.alt) alts++;
            Assert.That(alts > 0, Is.EqualTo(ever), alts + " throws in 20 decisions");
        }

        static double AimError(RunState run, Input input)
        {
            var p = run.player; var e = run.enemies[0];
            return Math.Atan2(input.aimY - p.y, input.aimX - p.x) - Math.Atan2(e.y - p.y, e.x - p.x);
        }

        [Test]
        public void TheAimIsNoisyAndHeldUntilTheNextDecision()
        {
            var (run, bot) = Facing(200, "novice");
            int react = React("novice");
            double a0 = AimError(run, bot.Decide(run, 0).Input);
            Assert.That(Math.Abs(a0), Is.InRange(1e-6, 1.0), "the novice aims perfectly");
            Assert.That(AimError(run, bot.Decide(run, react - 1).Input), Is.EqualTo(a0),
                "re-decided before its reaction time");
            Assert.That(AimError(run, bot.Decide(run, react).Input), Is.Not.EqualTo(a0));
        }

        // a quiet room with the exit east and one pickup west: which way the bot walks
        static int Towards(string kind, Action<Player> set)
        {
            var run = Floor(1);
            var p = run.player;
            set(p);
            run.pickups.Add(Pickup.Of("exit", p.x + 100, p.y, 30));
            run.pickups.Add(Pickup.Of(kind, p.x - 100, p.y, 16));
            return (int)new BotPolicy(Profile.Get("average"), 1).Decide(run, 0).Input.dx;
        }

        [Test]
        public void WantedPickupsComeBeforeTheExit()
        {
            Assert.That(Towards("heart", p => p.hp = 3), Is.EqualTo(-1), "a heart below max was not wanted");
            Assert.That(Towards("heart", p => { }), Is.EqualTo(1), "a heart at max was wanted");
            Assert.That(Towards("halfarmor", p => p.armor = 0), Is.EqualTo(-1), "armour below max was not wanted");
            Assert.That(Towards("armor", p => p.armor = Balance.MaxArmor), Is.EqualTo(1));
            Assert.That(Towards("key", p => { }), Is.EqualTo(-1), "a key was not wanted");
            Assert.That(Towards("weapon", p => { }), Is.EqualTo(1), "a weapon swap was wanted");
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
        public void TheRouteTakesUnvisitedRoomsBeforeTheItemRoomAndTheBoss()
        {
            var run = Floor(7);
            var far = run.dungeon.AllRooms.Where(r => r.Type == RoomKind.Normal)
                .OrderByDescending(r => Math.Abs(r.X - run.curX) + Math.Abs(r.Y - run.curY)).First();
            VisitAllBut(run, far);
            run.bossUnlocked = run.itemUnlocked = true;   // both open, and both still come later
            Assert.That(Walk(run, new BotPolicy(Profile.Get("novice"), 7)), Is.SameAs(far));
        }

        [Test]
        public void TheFirstStepIsTheFirstOpenDoorInNorthSouthEastWestOrder()
        {
            // a fresh floor: every neighbour is unvisited, so BFS order alone picks the first step
            var order = new[] { Dir.N, Dir.S, Dir.E, Dir.W };
            for (uint seed = 1; seed < 60; seed++)
            {
                var run = Floor(seed);
                var start = run.CurrentRoom!;
                var open = order.Where(d => start.Doors.Contains(d)
                                            && run.dungeon.Neighbour(start, d)!.Type == RoomKind.Normal).ToList();
                if (open.Count < 2) continue;   // one open door would pass in any order
                Assert.That(new BotPolicy(Profile.Get("novice"), seed).Route(run), Is.EqualTo(open[0]), "seed " + seed);
                return;
            }
            Assert.Fail("fixture: no seed under 60 has a start room with two open doors");
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

        // ---- the report

        [Test]
        public void TheSignTestIsExact()
        {
            Assert.That(Report.SignTest(0, 0), Is.EqualTo(1));
            Assert.That(Report.SignTest(5, 5), Is.EqualTo(1));
            Assert.That(Report.SignTest(3, 0), Is.EqualTo(0.25).Within(1e-12));          // 2 * (1/8)
            Assert.That(Report.SignTest(23, 2), Is.EqualTo(2.0 * 326 / 33554432).Within(1e-15));   // 2 * (1 + 25 + 300) / 2^25
            Assert.That(Report.SignTest(2, 23), Is.EqualTo(Report.SignTest(23, 2)));
            Assert.That(Report.SignTest(500, 400), Is.GreaterThan(0).And.LessThan(0.01));   // large n stays finite
        }

        [Test]
        public void DeathsArePairedBySeedAndProfile()
        {
            RunResult R(uint seed, string prof, string end) => new RunResult { seed = seed, profile = prof, end = end };
            // asymmetric (3 saved, 1 newly died), and one seed played by two profiles with opposite
            // outcomes, so swapped columns or pairing on the seed alone both give a different answer
            var a = new Batch { label = "a", seeds = { 1, 2, 3, 4 }, profiles = { "novice", "skilled" } };
            var b = new Batch { label = "b", seeds = { 1, 2, 3, 4 }, profiles = { "novice", "skilled" } };
            a.runs.Add(R(1, "novice", "death")); b.runs.Add(R(1, "novice", "timeout"));     // saved
            a.runs.Add(R(1, "skilled", "timeout")); b.runs.Add(R(1, "skilled", "death"));   // newly died, same seed
            a.runs.Add(R(2, "novice", "death")); b.runs.Add(R(2, "novice", "timeout"));     // saved
            a.runs.Add(R(3, "novice", "death")); b.runs.Add(R(3, "novice", "timeout"));     // saved
            a.runs.Add(R(4, "novice", "death")); b.runs.Add(R(4, "novice", "death"));       // both died
            b.runs.Add(R(9, "novice", "death"));                                            // unpaired: ignored
            void Is3(string? prof, int saved, int lost, double p)
            {
                var f = Report.Flips(a, b, prof);
                Assert.That((f.saved, f.lost), Is.EqualTo((saved, lost)), prof ?? "all");
                Assert.That(f.p, Is.EqualTo(p).Within(1e-12), prof ?? "all");
            }
            Is3(null, 3, 1, 0.625);       // 2 * (1 + 4) / 16
            Is3("novice", 3, 0, 0.25);
            Is3("skilled", 0, 1, 1.0);
            string md = Report.Build(new[] { a, b });
            Assert.That(md, Does.Contain("| b saved | b newly died | p |"));
            Assert.That(Row(md, "all"), Does.Contain("| 3 | 1 | 0.625 |"));
            Assert.That(Row(md, "skilled"), Does.Contain("| 0 | 1 | 1.000 |"));
            Assert.That(Report.Build(new[] { a }), Does.Not.Contain("paired by seed"), "a single label has nothing to pair");
        }

        static RunResult HandRun(string profile, string end, int hz, int floor = 2, double dmg2 = 2)
        {
            var r = new RunResult
            {
                seed = 1, profile = profile, floor = floor, ticks = hz * 180, end = end,
                hits = 8, shots = 10, kills = 6,
                cause = end == "death" ? new object[] { "contact:lunger", 3.0 } : null,
            };
            if (end == "error") r.errors.Add("game: X: y @ Z.W");
            r.dmgBySource["contact:lunger"] = 4; r.dmgBySource["shot:gunner"] = 2;
            r.floors.Add(new FloorRecord { floor = 1, ticks = hz * 120, bossTicks = hz * 30, bossKilled = true,
                                           dmg = 4, healed = 2, regen = 2, hpOut = 6 });
            r.floors.Add(new FloorRecord { floor = 2, ticks = hz * 60, bossTicks = 0, dmg = dmg2, hpOut = 0 });
            return r;
        }

        static Batch Hand(string label, string profile, double dmg2 = 2, int hz = 210, double minutes = 20)
        {
            return new Batch { label = label, tickHz = hz, minutes = minutes, brunch = "A+",
                               seeds = { 1 }, profiles = { profile }, runs = { HandRun(profile, "death", hz, 2, dmg2) } };
        }

        static string Row(string md, string name) =>
            md.Split('\n').First(l => l.StartsWith("| " + name + " |", StringComparison.Ordinal));

        [TestCase(210)]
        [TestCase(60)]
        public void TheReportUsesTheJsDefinitions(int hz)
        {
            // floor 1 was left (2 min, 30 s of boss), floor 2 was not; damage is 4 then 2, healing 2 then 0
            string md = Report.Build(new[] { Hand("a", "novice", 2, hz) });
            Assert.That(Row(md, "minutes per cleared floor"), Does.EndWith("| 2.0 | 2.0 |"), "only floors left count");
            Assert.That(Row(md, "boss fight, seconds"), Does.EndWith("| 30.0 | 30.0 |"), "only floors left count");
            Assert.That(Row(md, "damage taken per floor (hp, 2 = 1 heart)"), Does.EndWith("| 3.0 | 3.0 |"),
                "every floor counts");
            Assert.That(Row(md, "healing per floor (hp, regen included)"), Does.EndWith("| 1.0 | 1.0 |"));
            Assert.That(Row(md, "  of which the regenerating heart"), Does.EndWith("| 1.0 | 1.0 |"));
            Assert.That(Row(md, "accuracy"), Does.EndWith("| 80% | 80% |"));
            Assert.That(Row(md, "kills per minute"), Does.EndWith("| 2.0 | 2.0 |"),
                "6 kills in 3 minutes at the header's rate");
            Assert.That(md, Does.Contain("| death | contact:lunger 3.0 hp |"));
            Assert.That(md, Does.Contain("## Where the damage comes from\n\n" +
                                         "**a** (total 6.0 hp): contact:lunger 67%, shot:gunner 33%\n"));
            Assert.That(md, Does.Contain("| novice | 1 | 1 | 2.0 | 0 | 4.0 | 2.0 (2.0) | 30.0 | yes | 0.0 > 6.0 |"));
        }

        [Test]
        public void EndStatesAreCountedApartAndTheFloorIsAMedian()
        {
            var b = Hand("a", "novice");
            b.runs.Clear();
            b.runs.Add(HandRun("novice", "death", 210, 1));
            b.runs.Add(HandRun("novice", "stuck", 210, 2));
            b.runs.Add(HandRun("novice", "error", 210, 3));
            b.runs.Add(HandRun("novice", "timeout", 210, 10));
            string md = Report.Build(new[] { b });
            Assert.That(Row(md, "deaths"), Does.EndWith("| 1 | 1 |"));
            Assert.That(Row(md, "stuck runs (bot or softlock)"), Does.EndWith("| 1 | 1 |"));
            Assert.That(Row(md, "runs with errors"), Does.EndWith("| 1 | 1 |"));
            Assert.That(Row(md, "median floor reached"), Does.EndWith("| 2.5 | 2.5 |"),
                "1, 2, 3, 10: the median, not the mean 4");
        }

        [TestCase(0.25, "0.3")]
        [TestCase(0.75, "0.8")]
        [TestCase(-0.25, "-0.2")]
        [TestCase(2.0, "2.0")]
        public void NumbersRoundLikeTheJs(double v, string s) => Assert.That(Report.F1(v), Is.EqualTo(s));

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

            string longer = Report.Build(new[] { Hand("a", "novice"), Hand("b", "novice", 2, 210, 60) });
            Assert.That(longer, Does.Contain("Warning: minutes differ: 20 vs 60"));
            string faster = Report.Build(new[] { Hand("a", "novice"), Hand("b", "novice", 2, 60) });
            Assert.That(faster, Does.Contain("Warning: tick rates differ: 210 vs 60"));
        }

        [Test]
        public void TheReportReadsTheTickRateBackFromTheHeader()
        {
            // a 60 Hz batch, written and read back: 7200 ticks of floor are 2 minutes at 60, not 0.6 at 210
            var b = Json.Read(Json.Write(Hand("a", "novice", 2, 60)), "a.json");
            Assert.That(b.tickHz, Is.EqualTo(60));
            Assert.That(Row(Report.Build(new[] { b }), "minutes per cleared floor"), Does.EndWith("| 2.0 | 2.0 |"));
        }

        [Test]
        public void PlayWritesTheHeaderItWasAskedFor()
        {
            string dir = Path.Combine(Path.GetTempPath(), "depths-playtest-" + Guid.NewGuid().ToString("N"));
            string brunch = Balance.BrunchVariant;
            var outw = Console.Out;
            try
            {
                Console.SetOut(TextWriter.Null);
                int code = Program.Main(new[] { "play", "--label", "e2e", "--seeds", "3", "--profiles", "novice",
                                                "--minutes", "0.25", "--brunch", "B", "--out", dir });
                Console.SetOut(outw);
                Assert.That(code, Is.EqualTo(0));
                var b = Json.Read(File.ReadAllText(Path.Combine(dir, "e2e.json")), "e2e.json");
                Assert.That(b.label, Is.EqualTo("e2e"));
                Assert.That(b.tickHz, Is.EqualTo(Balance.TickHz));
                Assert.That(b.minutes, Is.EqualTo(0.25));
                Assert.That(b.seeds, Is.EqualTo(new uint[] { 3 }));
                Assert.That(b.profiles, Is.EqualTo(new[] { "novice" }));
                Assert.That(b.brunch, Is.EqualTo("B"), "--brunch did not reach the game or the header");
                Assert.That(b.commit, Is.EqualTo(BuildInfo.Commit),
                    "the header does not name the build (null only without git)");
                Assert.That(b.runs.Single().ticks, Is.EqualTo((int)Math.Round(0.25 * 60 * Balance.TickHz)));
            }
            finally
            {
                Console.SetOut(outw);
                Balance.BrunchVariant = brunch;   // the switch is process-wide; the CLI owns its process, a test does not
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // ---- the command line

        [TestCase("play", "--seeds", "-1")]
        [TestCase("play", "--seeds", "99999999999")]
        [TestCase("play", "--seeds", "1,,2")]
        [TestCase("play", "--minutes", "-3")]
        [TestCase("play", "--minutes", "0")]
        [TestCase("play", "--profiles", "expert")]
        [TestCase("play", "--brunch", "C")]
        [TestCase("play", "--label", "../x")]
        [TestCase("play", "--out", "")]
        [TestCase("play", "--bogus", "1")]
        [TestCase("play", "--label")]
        [TestCase("report")]
        [TestCase("report", "--out", "somewhere")]
        [TestCase("report", "a", "--out", " ")]
        [TestCase("report", "a", "b", "c")]
        [TestCase("frob")]
        [TestCase]
        public void ABadCommandLineIsRefusedWithOneLine(params string[] args)
        {
            var e = Assert.Throws<UsageException>(() => Cli.Parse(args))!;
            Assert.That(e.Message, Is.Not.Empty.And.Not.Contain("\n"));
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
        public void MainExitsWithOneOnBadInputAndBadFiles()
        {
            var err = Console.Error;
            var seen = new StringWriter();
            Console.SetError(seen);   // the one-line messages are not test output
            try { MainOnBadInput(); }
            finally { Console.SetError(err); }
            Assert.That(seen.ToString(), Does.Not.Contain("internal error").And.Not.Contain("   at "));
        }

        const string Head = "{\"label\":\"x\",\"seeds\":[1],\"profiles\":[\"novice\"],\"runs\":";
        static readonly string[] BadFiles =
        {
            "null",
            "{\"runs\":",
            Head + "[null]}",
            Head + "[{\"profile\":\"novice\",\"end\":\"death\",\"floors\":null}]}",
            Head + "[{\"profile\":\"novice\",\"end\":\"death\",\"floors\":[null]}]}",
            Head + "[{\"profile\":\"novice\",\"end\":\"death\",\"floors\":[],\"cause\":[\"x\"]}]}",
            Head + "[{\"profile\":\"novice\",\"end\":\"death\",\"floors\":[],\"cause\":[1,2]}]}",
            Head + "[{\"profile\":\"novice\",\"end\":\"death\",\"floors\":[],\"cause\":\"x\"}]}",
        };

        static void MainOnBadInput()
        {
            Assert.That(Program.Main(new string[0]), Is.EqualTo(1));
            Assert.That(Program.Main(new[] { "frob" }), Is.EqualTo(1));
            Assert.That(Program.Main(new[] { "play", "--seeds", "-1" }), Is.EqualTo(1));
            Assert.That(Program.Main(new[] { "play", "--out", "" }), Is.EqualTo(1));
            string dir = Path.Combine(Path.GetTempPath(), "depths-playtest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                for (int i = 0; i < BadFiles.Length; i++)
                {
                    File.WriteAllText(Path.Combine(dir, "bad" + i + ".json"), BadFiles[i]);
                    Assert.That(Program.Main(new[] { "report", "bad" + i, "--out", dir }), Is.EqualTo(1), BadFiles[i]);
                }
                Assert.That(Program.Main(new[] { "report", "missing", "--out", dir }), Is.EqualTo(1));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
