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
        public void EveryHeartTheBodyPageCanHoldSitsOnItsPaper()
        {
            var paper = new Rect(HudLayout.LeftX + HudLayout.PaperPad, HudLayout.LecternTop + HudLayout.PaperPad,
                                 HudLayout.LecternW - HudLayout.PaperPad * 2, HudLayout.LecternH - HudLayout.PaperPad * 2);
            foreach (HeartKind k in Enum.GetValues(typeof(HeartKind)))
                for (int i = 0; i < HudLayout.HeartCapacity(k); i++)
                {
                    var tl = HudLayout.HeartTopLeft(k, i);
                    var r = new Rect(tl.x, tl.y, HudLayout.HeartW, HudLayout.HeartH);
                    Assert.That(paper.Contains(r.min) && paper.Contains(r.max), k + " heart " + i + " at " + r + " falls off the page " + paper);
                    Assert.That(tl.x, Is.GreaterThan(HudLayout.MarginX + 3), "hearts are written right of the red margin");
                }
            Assert.That(HudLayout.HeartTopLeft(HeartKind.Red, 1).x - HudLayout.HeartTopLeft(HeartKind.Red, 0).x, Is.EqualTo(HudLayout.HeartStep));
            Assert.That(HudLayout.HeartTopLeft(HeartKind.Red, HudLayout.HeartsPerLine).y, Is.GreaterThan(HudLayout.HeartTopLeft(HeartKind.Red, 0).y), "the seventh red heart wraps");
            Assert.That(HudLayout.HeartTopLeft(HeartKind.Regen, 0).y, Is.GreaterThan(HudLayout.HeartTopLeft(HeartKind.Red, HudLayout.HeartsPerLine).y), "mending is written under blood");
            Assert.That(HudLayout.HeartTopLeft(HeartKind.Armour, 0).y, Is.GreaterThan(HudLayout.HeartTopLeft(HeartKind.Regen, 0).y), "mail is written under mending");
        }

        [Test]
        public void TheRowsOfThePageDoNotRunIntoEachOther()
        {
            for (int i = 1; i < HudLayout.LeftRows.Length; i++)
                Assert.That(HudLayout.LeftRows[i] - HudLayout.LeftRows[i - 1], Is.GreaterThanOrEqualTo(28), "row " + HudLayout.LeftLabels[i]);
            Assert.That(HudLayout.RowBottom(HudLayout.LeftRows.Length - 1), Is.LessThan(HudLayout.LecternTop + HudLayout.LecternH - HudLayout.PaperPad));
            Assert.That(HudLayout.Map.yMax, Is.LessThan(HudLayout.KitHeaderY), "the map and the kit's heading");
            Assert.That(HudLayout.Slot(2).xMax, Is.LessThanOrEqualTo(HudLayout.RightX + HudLayout.LecternW - HudLayout.PaperPad), "the third slot is on the page");
            Assert.That(HudLayout.CarryY + HudLayout.CarryStep * 2, Is.LessThan(HudLayout.LecternTop + HudLayout.LecternH - HudLayout.PaperPad));
        }

        [Test]
        public void TheLobesTheCheckSamplesAreInsideTheHeartSprite()
        {
            // heartRows row 2 is "#######": both sampled cells are heart, clear of the highlight on row 0
            var tl = new Vector2(100, 50);
            Vector2 l = HudLayout.LeftLobe(tl) - tl, r = HudLayout.RightLobe(tl) - tl;
            int px = HudLayout.HeartPx;
            Assert.That(Sprites.HeartRows[(int)((l.y - 2) / px)][(int)((l.x - 2) / px)], Is.EqualTo('#'));
            Assert.That(Sprites.HeartRows[(int)((r.y - 2) / px)][(int)((r.x - 2) / px)], Is.EqualTo('#'));
            Assert.That((int)((l.x - 2) / px), Is.LessThan(3.5f), "the left lobe is lit by a half heart");
            Assert.That((int)((r.x - 2) / px), Is.GreaterThan(3.5f), "the right lobe is not");
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

        static IEnumerable<string> Texts(VisualElement root) => root.Query<Label>().ToList().Select(l => l.text);

        [Test]
        public void ThePagesNameEveryLineTheJsHudCarries()
        {
            var hud = new LedgerHud();
            var run = Run();
            Items.Give(run, "tin_cup");
            hud.Refresh(run, 1 / 60f);
            var texts = Texts(hud).ToList();
            foreach (var line in new[] { "Blood", "Mending", "Mail", "Keys", "Blink", "Momentum", "Depth", "Slain", "Seed" })
                Assert.That(texts, Does.Contain(line), "the body page has no line for " + line);
            Assert.That(texts, Does.Contain("The Shallows"), "the depth line names the area");
            Assert.That(texts, Does.Contain(Weapons.All[run.player.weaponIdx].Name), "the weapon slot is captioned");
            Assert.That(texts, Does.Contain("Blast"), "the right-click slot is captioned");
            Assert.That(texts, Does.Contain("Tin Cup"), "the held item is named");
            Assert.That(hud.Q("stand-left"), Is.Not.Null); Assert.That(hud.Q("stand-right"), Is.Not.Null);
            Assert.That(hud.DevVisible, Is.False, "the developer line is hidden until F3");
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
            for (int i = 0; i < 60; i++) Frame(i);   // warm: labels written, every branch taken once
            int before = hud.Repaints;
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) Frame(i);
            long spent = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Assert.That(hud.Repaints - before, Is.GreaterThan(50), "the fixture must actually move the drawn values");
            Assert.That(spent, Is.EqualTo(0), "100 HUD updates allocated " + spent + " bytes");
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
            run.player.cooldown += 1e-6;
            hud.Refresh(run, 1 / 60f);
            Assert.That(hud.Repaints, Is.EqualTo(n + 2), "a cooldown change smaller than a pixel repainted");
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
        public void TheAccountIsTheJsSummaryRows()
        {
            var run = Run();
            run.floor = 3; run.kills = 37; run.dmgTaken = 9; run.shots = 120; run.hits = 88;
            run.ticks = Balance.TickHz * 75 + Balance.TickHz / 2; run.floorTicks = Balance.TickHz * 4;
            var o = new HudOverlays();
            o.Fill(run);
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

        [Test]
        public void TheBannerStrikesTheFloorInTheAreasAccent()
        {
            var img = HudOverlays.BannerArt(5);
            var accent = Hex.C(AreaPalette.For(Area.Area2).Accent);
            int hits = img.Px.Count(c => c.r == accent.r && c.g == accent.g && c.b == accent.b);
            Assert.That(hits, Is.GreaterThan(200), "FLOOR 5 is not struck in the Kiln Works' accent");
        }

        [Test]
        public void TheStandsAreBakedAtTheirLayoutSize()
        {
            foreach (bool left in new[] { true, false })
            {
                var img = LedgerArt.Stand(left);
                var at = HudLayout.Stand(left);
                Assert.That(img.W, Is.EqualTo((int)at.width)); Assert.That(img.H, Is.EqualTo((int)at.height));
                // the page is paper (light) in the middle of the plate, and the plate's rim is dark wood
                int px = (left ? HudLayout.LeftX : HudLayout.RightX) - (int)at.x, py = HudLayout.LecternTop - (int)at.y;
                var mid = img.Get(px + HudLayout.LecternW - 30, py + HudLayout.LecternH - 60);
                Assert.That(mid.r + mid.g + mid.b, Is.GreaterThan(450), "no paper where the page should be: " + mid);
                var rim = img.Get(px + 0, py + HudLayout.LecternH / 2);
                Assert.That(rim.r + rim.g + rim.b, Is.LessThan(150), "no dark rim on the plate: " + rim);
            }
        }
    }
}
