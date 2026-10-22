using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Depths.Unity.Art;
using Depths.Unity.Hud;

namespace Depths.Unity.Tests
{
    /// <summary>The ledger HUD: where it draws (never on the room), what it carries, and what a frame costs.</summary>
    public class HudLayoutTests
    {
        static readonly Rect Screen = new Rect(0, 0, 1280, 720);

        [Test]
        public void TheRoomIsWhereTheBriefSaysItIs()
        {
            // the room with its walls, at 1280x720: x 274-1006, y 119-601 (WorldView.Origin, walls 16)
            var r = HudLayout.Room;
            Assert.That(r.xMin, Is.EqualTo(274)); Assert.That(r.xMax, Is.EqualTo(1006));
            Assert.That(r.yMin, Is.EqualTo(119)); Assert.That(r.yMax, Is.EqualTo(601));
        }

        [Test]
        public void NothingTheHudDrawsOverlapsTheRoomOrLeavesTheScreen()
        {
            var room = HudLayout.Room;
            foreach (var r in HudLayout.HudRects())
            {
                Assert.That(r.Overlaps(room), Is.False, "the HUD rect " + r + " covers the room " + room);
                Assert.That(r.xMin >= 0 && r.yMin >= 0 && r.xMax <= Screen.xMax && r.yMax <= Screen.yMax, "the HUD rect " + r + " leaves the screen");
            }
        }

        [Test]
        public void TheBoardsStandTheHeightOfTheScreenBesideTheRoom()
        {
            foreach (bool left in new[] { true, false })
            {
                var b = HudLayout.Board(left);
                Assert.That(b.yMin, Is.LessThanOrEqualTo(16)); Assert.That(b.yMax, Is.GreaterThanOrEqualTo(704), "no dead band above or below the boards");
            }
            Assert.That(HudLayout.Banner.yMax, Is.LessThan(HudLayout.Room.yMin), "the heading lives in the band above the room");
            Assert.That(HudLayout.Banner.xMin, Is.GreaterThan(HudLayout.Board(true).xMax)); Assert.That(HudLayout.Banner.xMax, Is.LessThan(HudLayout.Board(false).xMin));
        }

        [Test]
        public void TheHeartsAreOneRowThatWrapsOnTheBodyPage()
        {
            var paper = new Rect(HudLayout.LeftX + HudLayout.PaperPad, HudLayout.BoardTop + HudLayout.PaperPad,
                                 HudLayout.BoardW - HudLayout.PaperPad * 2, HudLayout.BoardH - HudLayout.PaperPad * 2);
            for (int i = 0; i < HudLayout.HeartCapacity; i++)
            {
                var tl = HudLayout.HeartTopLeft(i, HudLayout.HeartCapacity);
                var r = new Rect(tl.x + 1, tl.y + 1, HudLayout.HeartW - 2, HudLayout.HeartH - 2);   // the visible heart, not its clear pad
                Assert.That(paper.Contains(r.min) && paper.Contains(r.max), "heart " + i + " at " + r + " falls off the page " + paper);
            }
            Assert.That(HudLayout.HeartTopLeft(1).x - HudLayout.HeartTopLeft(0).x, Is.EqualTo(HudLayout.HeartStep));
            Assert.That(HudLayout.HeartTopLeft(HudLayout.HeartsPerLine, 9).y, Is.EqualTo(HudLayout.HeartTopLeft(0).y), "a ninth heart stays on the row");
            Assert.That(HudLayout.HeartTopLeft(15, 16).x, Is.EqualTo(HudLayout.HeartTopLeft(7, 8).x).Within(0.01f), "a full row is as wide as eight");
            Assert.That(HudLayout.HeartTopLeft(0).y + HudLayout.HeartH, Is.LessThanOrEqualTo(HudLayout.MomentumWell.y), "never onto the momentum bar");
            Assert.That(HudLayout.HeartW - 4, Is.GreaterThanOrEqualTo(28), "hearts are drawn at twice the old 14px");
        }

        [Test]
        public void TheBodyReadsTopToBottomWithoutOverlaps()
        {
            Assert.That(HudLayout.HeartTopLeft(7).y, Is.EqualTo(HudLayout.HeartTopLeft(0).y), "eight hearts sit in one row");
            Assert.That(HudLayout.HeartsBox.yMax, Is.LessThan(HudLayout.MomentumWell.yMin), "momentum sits under the hearts");
            Assert.That(HudLayout.MomentumWell.yMax, Is.LessThan(HudLayout.KeyWell(0).yMin));
            Assert.That(HudLayout.KeyWell(0).yMax, Is.LessThan(HudLayout.BlinkWell(0).yMin));
            Assert.That(HudLayout.BlinkWell(1).xMax, Is.LessThanOrEqualTo(HudLayout.LeftX + HudLayout.BoardW - HudLayout.PaperPad));
            Assert.That(HudLayout.BlinkWell(0).yMax, Is.LessThan(HudLayout.Rule2));
            Assert.That(HudLayout.DepthSeal.yMax, Is.LessThan(HudLayout.LadderY));
            Assert.That(HudLayout.Rung(3, 0).yMax, Is.LessThan(HudLayout.Rule3));
            Assert.That(HudLayout.Rung(2, 3).xMax, Is.LessThanOrEqualTo(HudLayout.Ink0(true) + HudLayout.InkW));
            Assert.That(HudLayout.Rungs(3), Is.EqualTo(2), "the Deep is floors 13 and 14");
            Assert.That(HudLayout.Rung(3, 1).xMax, Is.LessThanOrEqualTo(HudLayout.Ink0(true) + HudLayout.InkW), "floor 14's rung runs off the paper");
            Assert.That(HudLayout.Rung(3, 0).xMax, Is.LessThan(HudLayout.Rung(3, 1).xMin), "13 and 14 overlap");
            Assert.That(HudLayout.SeedTag.yMax, Is.LessThan(HudLayout.PaperBottom));
            Assert.That(HudLayout.PaperBottom - HudLayout.SeedTag.yMax, Is.LessThan(48), "no dead paper band under the footer");
            Assert.That(HudLayout.Map.yMax, Is.LessThan(HudLayout.KitY), "the map and the kit");
            Assert.That(HudLayout.Plate(2).xMax, Is.LessThanOrEqualTo(HudLayout.RightX + HudLayout.BoardW - HudLayout.PaperPad), "plates are on the page");
            Assert.That(HudLayout.Slot(2).yMax, Is.LessThan(HudLayout.CarryLabelY));
            Assert.That(HudLayout.CarryY + HudLayout.CarryStep * 2, Is.LessThan(HudLayout.PaperBottom));
            Assert.That(HudLayout.Map.width, Is.GreaterThanOrEqualTo(0.8f * HudLayout.InkW), "the map fills its page");
        }

        [Test]
        public void TheLobesTheCheckSamplesAreInsideTheHeartSprite()
        {
            var tl = new Vector2(100, 50);
            Vector2 l = HudLayout.LeftLobe(tl) - tl, r = HudLayout.RightLobe(tl) - tl;
            int px = HudLayout.HeartPx;
            Assert.That(Sprites.HeartRows[(int)((l.y - 2) / px)][(int)((l.x - 2) / px)], Is.EqualTo('#'));
            Assert.That(Sprites.HeartRows[(int)((r.y - 2) / px)][(int)((r.x - 2) / px)], Is.EqualTo('#'));
            Assert.That((l.x - 2) / px, Is.LessThan(3.5f), "the left lobe is lit by a half heart");
            Assert.That((r.x - 2) / px, Is.GreaterThan(3.5f), "the right lobe is not");
        }
    }

    public class LedgerArtTests
    {
        [Test]
        public void HalfAHeartIsExactlyHalfItsPixels()
        {
            // 1 HP is half a heart: the lit pixels of a half heart are half of a full one's, split down the middle
            foreach (var s in new[] { Sprites.HeartScheme.Red, Sprites.HeartScheme.Regen, Sprites.HeartScheme.Gray })
            {
                var pal = Sprites.HeartPal(s);
                var full = LedgerArt.Heart(s, 2, 4); var half = LedgerArt.Heart(s, 1, 4); var none = LedgerArt.Heart(s, 0, 4);
                int Lit(Img g) => g.Px.Count(c => !c.Equals(Sprites.Outline) && c.a > 0 && (Same(c, pal['R']) || Same(c, pal['H'])));
                Assert.That(Lit(half) * 2, Is.EqualTo(Lit(full)), s + ": half a heart is not half the lit pixels");
                Assert.That(Lit(none), Is.EqualTo(0));
                int split = 2 + 14;
                for (int y = 0; y < half.H; y++)
                    for (int x = split; x < half.W; x++)
                        Assert.That(Same(half.Get(x, y), pal['R']) || Same(half.Get(x, y), pal['H']), Is.False, s + " lit right of the middle at " + x + "," + y);
            }
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        [Test]
        public void EveryItemHasAPictureInTheAtlas()
        {
            foreach (var d in Items.All)
            {
                Assert.That(ItemIcons.Has(d.Id), d.Id + " still shows its lettered tile");
                var img = ItemIcons.Bake(d.Id, 2);
                Assert.That(img.W, Is.EqualTo(ItemIcons.Size * 2 + 4)); Assert.That(img.H, Is.EqualTo(ItemIcons.Size * 2 + 4));
                Assert.That(img.Px.Count(c => c.a > 0), Is.GreaterThan(80), d.Id + "'s picture is nearly empty");
                Assert.That(SpriteSet.Atlas.Has(SpriteSet.ItemIcon(d.Id, 2)) && SpriteSet.Atlas.Has(SpriteSet.ItemIcon(d.Id, 4)), d.Id + " is not in the atlas");
            }
        }

        [Test]
        public void AnEmptyRegenHeartIsItsOwnRoseNotAnEmptyRedOne()
        {
            var regen = LedgerArt.Heart(Sprites.HeartScheme.Regen, 0, 4);
            var red = LedgerArt.Heart(Sprites.HeartScheme.Red, 0, 4);
            var r = regen.Get(2 + 4 * 2 + 2, 2 + 4 * 2 + 2); var d = red.Get(2 + 4 * 2 + 2, 2 + 4 * 2 + 2);
            Assert.That(Same(r, (Color32)HeartRow.RegenEmpty), "an empty regen container is " + r);
            Assert.That(Math.Abs(r.r - d.r) + Math.Abs(r.g - d.g) + Math.Abs(r.b - d.b), Is.GreaterThan(60), "empty regen and empty red look alike");
        }

        [Test]
        public void TheHudHandHasEveryLetterItWrites()
        {
            var words = new List<string> { "THE BODY", "THE MAP", "THE KIT", "KEYS", "BLINK", "MOMENTUM", "DEPTH", "SLAIN", "SEED", "CARRYING", "THE WARDEN", "PHASE 3",
                                           "SOMETHING HEAVY IS DOWN THERE", "THE GOLD KEY OPENS ITS DOOR", "Empty", "nothing yet", "x12", "+3", "Blast", "Hook", "13 AND DOWN", "FLOOR 14" };
            words.AddRange(LedgerArt.BandNames);
            words.AddRange(LedgerHud.AreaNames);
            words.AddRange(Weapons.All.Select(w => w.Name));
            words.AddRange(Items.All.Select(i => i.Name));
            foreach (var w in words)
                foreach (var ch in w) Assert.That(LedgerType.Has(ch), "the 5x7 hand has no '" + ch + "' for \"" + w + "\"");
        }

        [Test]
        public void TheWrapBreaksAtSpacesAndNeverLosesALetter()
        {
            string s = "The Flooded Galleries";
            int from = 0, lines = 0; var got = "";
            while (from < s.Length)
            {
                var (end, next) = LedgerType.Wrap(s, from, LedgerHud.AreaNames.Length * 0 + 140, 2);
                Assert.That(LedgerType.Width(end - from, 2), Is.LessThanOrEqualTo(140), "line " + lines + " is too wide");
                got += (lines > 0 ? " " : "") + s.Substring(from, end - from);
                from = next; lines++;
            }
            Assert.That(got, Is.EqualTo(s));
            Assert.That(lines, Is.EqualTo(2));
        }

        [Test]
        public void TheBoardsAreBakedAtTheirLayoutSizeWithTheirLabelsStruck()
        {
            foreach (bool left in new[] { true, false })
            {
                var img = LedgerArt.Board(left);
                var at = HudLayout.Board(left);
                Assert.That(img.W, Is.EqualTo((int)at.width)); Assert.That(img.H, Is.EqualTo((int)at.height));
                var rim = img.Get(0, img.H / 2);
                Assert.That(rim.r + rim.g + rim.b, Is.LessThan(150), "no dark rim on the plank: " + rim);
            }
            // a row is led by its pictogram in the margin: the key is inked where the keys row starts
            var body = LedgerArt.Board(true);
            int ox = HudLayout.IconX - HudLayout.LeftX - 2, oy = HudLayout.KeysY + 11 - HudLayout.BoardTop, ink = 0;
            for (int y = oy; y < oy + 10; y++) for (int x = ox; x < ox + 18; x++) { var c = body.Get(x, y); if (c.r + c.g + c.b < 330) ink++; }
            Assert.That(ink, Is.GreaterThan(20), "the key pictogram is not inked at the keys row");
            // the wells are dark with a lit bevel at their foot
            var w = HudLayout.BlinkWell(0);
            int wx = (int)w.x - HudLayout.LeftX, wy = (int)w.y - HudLayout.BoardTop;
            var inside = body.Get(wx + 10, wy + 8); var foot = body.Get(wx + 10, wy + (int)w.height - 1);
            Assert.That(inside.r + inside.g + inside.b, Is.LessThan(120), "the blink well is not dark");
            Assert.That(foot.r + foot.g + foot.b, Is.GreaterThan(inside.r + inside.g + inside.b + 100), "the well has no lit bevel");
        }

        [Test]
        public void TheBannerStrikesTheFloorInTheAreasAccent()
        {
            var img = HudOverlays.BannerArt(5);
            var accent = Hex.C(AreaPalette.For(Area.Area2).Accent);
            int hits = img.Px.Count(c => c.r == accent.r && c.g == accent.g && c.b == accent.b);
            Assert.That(hits, Is.GreaterThan(200), "FLOOR 5 is not struck in the Kiln Works' accent");
        }
    }

    public class RunRecordsTests
    {
        [Test]
        public void ARecordIsKeptOnlyWhenBeatenAndSurvivesARestart()
        {
            var store = new MemoryStore();
            var r = new RunRecords(store);
            var first = r.Record(2, 5);
            Assert.That(first.NewFloor && first.NewRooms, "a first run sets both records");
            Assert.That(store.Saves, Is.EqualTo(1));
            var same = r.Record(2, 5);
            Assert.That(same.NewFloor || same.NewRooms, Is.False, "matching a record is not a new one");
            var deeper = new RunRecords(store).Record(4, 3);
            Assert.That(deeper.NewFloor, Is.True); Assert.That(deeper.NewRooms, Is.False);
            Assert.That(deeper.DeepestFloor, Is.EqualTo(4)); Assert.That(deeper.BestRooms, Is.EqualTo(5), "a worse count does not lower the best");
            Assert.That(new RunRecords(store).DeepestFloor, Is.EqualTo(4), "the record did not survive a restart");
        }

        [Test]
        public void AWinIsCountedAndTheQuickestIsKept()
        {
            var store = new MemoryStore();
            var none = new RunRecords(store).Record(6, 20);
            Assert.That(none.Wins, Is.EqualTo(0)); Assert.That(none.NewBestWin, Is.False, "a death set a winning time");
            var first = new RunRecords(store).Record(14, 30, true, 2700);
            Assert.That(first.Wins, Is.EqualTo(1)); Assert.That(first.NewBestWin && first.BestWin == 2700, "a first win is the best yet");
            var slower = new RunRecords(store).Record(14, 30, true, 3000);
            Assert.That(slower.Wins, Is.EqualTo(2)); Assert.That(slower.NewBestWin, Is.False); Assert.That(slower.BestWin, Is.EqualTo(2700));
            var quicker = new RunRecords(store).Record(14, 30, true, 2400);
            Assert.That(quicker.NewBestWin && quicker.BestWin == 2400 && quicker.Wins == 3);
            Assert.That(new RunRecords(store).Wins, Is.EqualTo(3), "the wins did not survive a restart");
            Assert.That(new RunRecords(store).BestWin, Is.EqualTo(2400));
        }
    }

    public class LedgerHudTests
    {
        static RunState Run()
        {
            var run = new RunState(new Rng(7));
            run.Start(7);
            return run;
        }

        [Test]
        public void ASteadyFrameOfViewWorkAllocatesNothing()
        {
            // what GameView.Update calls every frame besides the sim and the room's mesh: the overlays in their idle
            // states, the warning's alpha, the music switches, the derived FX (a player measured ~6 collections a second)
            var run = Run();
            var o = new HudOverlays();
            var fx = new Depths.Unity.Art.FxTracker();
            int n = 0;
            AllocMeter.AssertNone(() =>
            {
                n++;
                o.SetPause(false, run);
                o.SetSummary(false, run, default);
                o.SetBanner(run.floor, (n % 40) / 40f);   // the banner fading, as after a descent
                o.SetWarning(HudOverlays.WarningAlpha(run));
                Depths.Unity.Audio.MusicEngine.SetArea(AreaRules.AreaForFloor(run.floor));
                Depths.Unity.Audio.MusicEngine.SetFight(n % 2 == 0);
                fx.Update(run, 1 / 60f);
            }, 200, "200 frames of overlay, music and FX work");
        }

        [Test]
        public void TheHudHasItsBoardsItsSeedTagAndNoDevLineByDefault()
        {
            var hud = new LedgerHud();
            var run = Run();
            hud.Refresh(run, 1 / 60f);
            Assert.That(hud.Q("board-left"), Is.Not.Null); Assert.That(hud.Q("board-right"), Is.Not.Null);
            Assert.That(hud.Q("seed-tag").style.backgroundImage.value.texture, Is.Not.Null, "the seed is not on its tag");
            Assert.That(hud.SeedShown, Is.EqualTo(run.rootSeed));
            Assert.That(hud.DevVisible, Is.False, "the developer line is hidden until F10");
        }

        [Test]
        public void TheWardensPlateIsUpOnlyWhileTheWardenLives()
        {
            var hud = new LedgerHud();
            var run = Run();
            hud.Refresh(run, 1 / 60f);
            var plate = hud.Q("boss-plate");
            Assert.That(plate.style.display.value, Is.EqualTo(DisplayStyle.None));
            var w = Spawn.Body(run, BodyKind.Boss, Balance.MidX, Balance.RoomTop + 120);
            run.enemies.Add(w);
            hud.Refresh(run, 1 / 60f);
            Assert.That(plate.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            w.hp = 0;
            hud.Refresh(run, 1 / 60f);
            Assert.That(plate.style.display.value, Is.EqualTo(DisplayStyle.None), "a dead Warden keeps no bar");
        }

        [Test]
        public void TheHudUpdateAllocatesNothingOnceWarm()
        {
            var hud = new LedgerHud();
            var run = Run();
            Items.Give(run, "tin_cup");
            Items.Give(run, "iron_ribs");
            run.enemies.Add(Spawn.Body(run, BodyKind.Boss, Balance.MidX, Balance.RoomTop + 120));
            var p = run.player;
            void Frame(int i)
            {
                // values that move every frame in a fight: cooldowns, momentum, the blink recharge, the Warden's health
                p.cooldown = i % 20; p.altCooldown = i % 33; p.momentum = (i % 50) / 50.0;
                run.blinkCharges = 1; p.blinkRegen = i % 100;
                run.enemies[0].hp = run.enemies[0].maxHp * (1 - (i % 90) / 100.0);
                p.hp = i % 40 < 20 ? 6 : 1;   // in and out of the last-heart pulse
                hud.Refresh(run, 1 / 60f);
            }
            for (int i = 0; i < 60; i++) Frame(i);   // warm: the seed tag stamped, every branch taken once
            int before = hud.Repaints;
            int n = 0;
            AllocMeter.AssertNone(() => Frame(n++), 100, "100 HUD updates");   // GetAllocatedBytesForCurrentThread read 0 under Unity's Mono: see AllocMeter
            Assert.That(hud.Repaints - before, Is.GreaterThan(50), "the fixture must actually move the drawn values");
        }

        [Test]
        public void TheInkRepaintsOnlyWhenADrawnValueChanges()
        {
            var hud = new LedgerHud();
            var run = Run();
            hud.Refresh(run, 1 / 60f);
            int n = hud.Repaints;
            for (int i = 0; i < 30; i++) hud.Refresh(run, 1 / 60f);
            Assert.That(hud.Repaints, Is.EqualTo(n), "a still frame repainted the ink");
            run.player.hp -= 1;
            hud.Refresh(run, 1 / 60f);
            Assert.That(hud.Repaints, Is.EqualTo(n + 1), "losing half a heart did not repaint");
            run.player.hasSilver = true;
            hud.Refresh(run, 1 / 60f);
            Assert.That(hud.Repaints, Is.EqualTo(n + 2), "picking up a key did not repaint");
            run.kills++;
            hud.Refresh(run, 1 / 60f);
            Assert.That(hud.Repaints, Is.EqualTo(n + 3), "a kill did not repaint the count");
            run.player.cooldown += 1e-6;
            hud.Refresh(run, 1 / 60f);
            Assert.That(hud.Repaints, Is.EqualTo(n + 3), "a cooldown change smaller than a pixel repainted");
        }

        [Test]
        public void TheLastHeartIsEverythingBetweenThePlayerAndDeath()
        {
            Player B(double hp, double regen, double armour) => new Player { hp = hp, maxHp = 8, regenHeart = regen, regenHeartMax = 2, armor = armour };
            Assert.That(LedgerHud.IsLow(B(2, 0, 0)), Is.True, "one red heart left");
            Assert.That(LedgerHud.IsLow(B(1, 0, 0)), Is.True, "half a heart left");
            Assert.That(LedgerHud.IsLow(B(2, 2, 0)), Is.False, "the regen heart drains first, so it is a second heart");
            Assert.That(LedgerHud.IsLow(B(1, 0, 2)), Is.False, "armour drains first too");
            Assert.That(LedgerHud.IsLow(B(0, 0, 0)), Is.False, "the dead do not pulse");
            Assert.That(LedgerHud.IsLow(B(3, 0, 0)), Is.False);
        }

        [Test]
        public void TheAccountIsTheJsSummaryRowsAndItsRecords()
        {
            var run = Run();
            run.floor = 3; run.kills = 37; run.dmgTaken = 9; run.shots = 120; run.hits = 88;
            run.ticks = Balance.TickHz * 75 + Balance.TickHz / 2; run.floorTicks = Balance.TickHz * 4;
            var o = new HudOverlays();
            o.Fill(run, new RunRecords.Result(3, 9, true, false));
            Assert.That(o.RecordValue(2), Is.EqualTo("none yet"), "no escapes yet");
            Assert.That(o.NewWinShown, Is.False);
            run.state = "won";
            o.Fill(run, new RunRecords.Result(14, 30, true, true, 2, 2465.5f, true));
            Assert.That(o.RecordValue(2), Is.EqualTo("2  (best 41:05.5)"));
            Assert.That(o.NewWinShown, "a quickest escape is not marked NEW");
            run.state = "playing";
            o.Fill(run, new RunRecords.Result(3, 9, true, false));
            Assert.That(HudOverlays.SummaryRows, Is.EqualTo(new[] { "Floor reached", "Time on this floor", "Time", "Rooms explored", "Enemies defeated", "Damage taken", "Accuracy", "Weapon", "Seed" }));
            Assert.That(o.SummaryValue(0), Is.EqualTo("3"));
            Assert.That(o.SummaryValue(1), Is.EqualTo("0:04.0"));
            Assert.That(o.SummaryValue(2), Is.EqualTo("1:15.5"));
            Assert.That(o.SummaryValue(3), Does.Match(@"^1 / \d+$"), "only the start room has been walked");
            Assert.That(o.SummaryValue(4), Is.EqualTo("37"));
            Assert.That(o.SummaryValue(5), Is.EqualTo("4.5 hearts"), "damage is counted in hearts: 2 HP a heart");
            Assert.That(o.SummaryValue(6), Is.EqualTo("73%  (88/120)"));
            Assert.That(o.SummaryValue(7), Is.EqualTo("Bolt"));
            Assert.That(o.SummaryValue(8), Is.EqualTo(Rng.Encode(7)));
            Assert.That(o.RecordValue(0), Is.EqualTo("3")); Assert.That(o.RecordValue(1), Is.EqualTo("9"));
            Assert.That(o.NewFloorShown, Is.True, "a new deepest floor is tagged NEW");
            Assert.That(o.NewRoomsShown, Is.False);
            o.Release();
        }

        [Test]
        public void ThePauseAndTheAccountOpenAndCloseWithTheGame()
        {
            var run = Run();
            var o = new HudOverlays();
            o.SetPause(true, run);
            Assert.That(o.PauseShown, Is.True);
            o.SetPause(false, run);
            Assert.That(o.PauseShown, Is.False);
            o.SetSummary(true, run);
            Assert.That(o.SummaryShown, Is.True);
            o.SetSummary(false, run);
            Assert.That(o.SummaryShown, Is.False);
            o.Release();
        }

        [Test]
        public void TheBannerFadesInHoldsAndFadesOut()
        {
            Assert.That(GameView.BannerAlpha(0), Is.EqualTo(0));
            Assert.That(GameView.BannerAlpha(3), Is.EqualTo(0), "it starts clear");
            Assert.That(GameView.BannerAlpha(2.875f), Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(GameView.BannerAlpha(1.5f), Is.EqualTo(1));
            Assert.That(GameView.BannerAlpha(0.3f), Is.EqualTo(0.5f).Within(1e-4));
            var run = Run();
            Assert.That(HudOverlays.WarningAlpha(run), Is.EqualTo(0), "no warning without its timer");
            run.bossWarnT = Balance.BossWarnTime / 2;
            Assert.That(HudOverlays.WarningAlpha(run), Is.EqualTo(1).Within(0.02f));
        }
    }
}
