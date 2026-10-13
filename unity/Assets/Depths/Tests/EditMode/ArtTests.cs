using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Depths.Unity.Art;

namespace Depths.Unity.Tests
{
    /// <summary>The bakes against the JS rules they port (src/10-art.js), pixel by pixel.</summary>
    public class ArtTests
    {
        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        [Test]
        public void ASpriteIsItsRowsAtItsPixelSizeInsideAnOutlinePad()
        {
            // bakeSprite: pad 2, each '#' cell px x px in its palette colour, a 1px dark outline proud of it
            string[] rows = { ".a", "b." };
            var pal = new Dictionary<char, Color32> { ['a'] = Hex.C("#ff0000"), ['b'] = Hex.C("#00ff00") };
            var img = Sprites.Bake(rows, pal, 3);
            Assert.That(img.W, Is.EqualTo(2 * 3 + 4)); Assert.That(img.H, Is.EqualTo(2 * 3 + 4));
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    Assert.That(Same(img.Get(2 + 3 + x, 2 + y), pal['a']), "cell a at " + x + "," + y);
                    Assert.That(Same(img.Get(2 + x, 2 + 3 + y), pal['b']), "cell b at " + x + "," + y);
                }
            Assert.That(Same(img.Get(4, 1), Sprites.Outline), "the outline sits one pixel above cell a");
            Assert.That(img.Get(0, 0).a, Is.EqualTo(0), "the pad corner is clear");
            Assert.That(Same(img.Get(4, 4), Sprites.Outline), "where the two outlines cross, an empty cell is outline");
            Assert.That(img.Get(3, 3).a, Is.EqualTo(0), "a pixel beyond both outlines stays clear");
        }

        [Test]
        public void AFlashBakeIsTheSilhouetteAloneInTheFlashColour()
        {
            var normal = Sprites.Bake(Sprites.Frame(Sprites.PlayerUpper, 0), Sprites.PlayerPal, 2);
            var flash = Sprites.Bake(Sprites.Frame(Sprites.PlayerUpper, 0), Sprites.PlayerPal, 2, SpriteSet.Flash);
            int lit = 0;
            for (int i = 0; i < normal.Px.Length; i++)
            {
                Assert.That(normal.Px[i].a > 0, Is.EqualTo(flash.Px[i].a > 0), "same shape at pixel " + i);
                if (flash.Px[i].a > 0) { Assert.That(Same(flash.Px[i], SpriteSet.Flash)); lit++; }
            }
            Assert.That(lit, Is.GreaterThan(200), "the player silhouette is a body, not a dot");
        }

        [Test]
        public void ThePlayerIsMirroredLeftToRight()
        {
            // the rows are written as a left half and mirrored, so the bake is symmetric (legs aside)
            var rows = Sprites.PlayerUpper;
            foreach (var r in rows) Assert.That(new string(r.Reverse().ToArray()), Is.EqualTo(r));
            Assert.That(rows[0].Length, Is.EqualTo(12));
        }

        [Test]
        public void EveryPaletteCoversEveryCellItIsDrawnWith()
        {
            void Covers(string[] upper, Dictionary<char, Color32> pal, string what)
            {
                for (int f = 0; f < 4; f++)
                    foreach (var row in Sprites.Frame(upper, f))
                        foreach (char c in row)
                            if (c != '.') Assert.That(pal.ContainsKey(c), what + " has no colour for '" + c + "'");
            }
            Covers(Sprites.PlayerUpper, Sprites.PlayerPal, "player");
            foreach (BodyKind k in System.Enum.GetValues(typeof(BodyKind)))
                for (int f = 0; f < 4; f++)
                {
                    var (rows, pal) = SpriteSet.BodyArt(k, f);
                    foreach (var row in rows)
                    {
                        Assert.That(row.Length, Is.EqualTo(rows[0].Length), k + " rows are one width");
                        foreach (char c in row) if (c != '.') Assert.That(pal.ContainsKey(c), k + " has no colour for '" + c + "'");
                    }
                }
        }

        [Test]
        public void AHeartFillsFromTheLeftInSevenColumns()
        {
            // heartRows: a column is lit when its index is under fill * 7; the highlight is row 0, column 1
            Assert.That(Sprites.HeartRowsFor(1)[1], Is.EqualTo("RRRRRRR"));
            Assert.That(Sprites.HeartRowsFor(0)[1], Is.EqualTo("EEEEEEE"));
            Assert.That(Sprites.HeartRowsFor(0.5f)[1], Is.EqualTo("RRRREEE"), "half a heart is 3.5 columns: four lit");
            Assert.That(Sprites.HeartRowsFor(1)[0], Is.EqualTo(".HR.RR."));
            Assert.That(Sprites.HeartRowsFor(0)[0], Is.EqualTo(".GE.EE."));
            Assert.That(Sprites.HeartStep(0.5f), Is.EqualTo(8));
            Assert.That(Sprites.HeartStep(2), Is.EqualTo(Sprites.HeartSteps), "a fill over one clamps");
        }

        [Test]
        public void TheGunIconsCarryTheirGunsColour()
        {
            for (int i = 0; i < Weapons.All.Length; i++)
            {
                var icon = Sprites.Icon(i.ToString(), Weapons.All[i].Color);
                var want = Hex.C(Weapons.All[i].Color);
                bool found = icon.Px.Any(p => p.r == want.r && p.g == want.g && p.b == want.b && p.a == 255);
                if (i == 3) found = true;   // the Voidball is drawn as a hole in fixed greens; its colour is only the glow
                Assert.That(found, Weapons.All[i].Name + " icon has its own colour somewhere solid");
                Assert.That(icon.W, Is.EqualTo(34));
            }
        }

        [Test]
        public void AnItemTileStampsItsTwoLetterMarkInDarkInk()
        {
            var tile = Sprites.ItemTile("tin_cup", 26);
            var face = Hex.C(ItemArt.Color("tin_cup"));
            int ink = tile.Px.Count(p => p.r < face.r - 40 && p.g < face.g - 40);
            Assert.That(ink, Is.GreaterThan(40), "the mark is stamped in the tile");
            Assert.That(Same(tile.Get(0, 0), Hex.C("#0b0e14")) || tile.Get(0, 0).r < 20, "a dark rim at the corner");
            Assert.That(ItemArt.Of("no_such_item").glyph, Is.EqualTo("NO"), "an unknown item still gets a mark");
        }

        [Test]
        public void ThePixelFontMeasuresWhatItStamps()
        {
            Assert.That(PixelFont.Width("AB", 2), Is.EqualTo((2 * 4 - 1) * 2));
            var g = new Img(40, 20);
            PixelFont.Stamp(g, "HI", 20, 10, 2, new Color32(255, 255, 255, 255));
            int minX = 99, maxX = -1;
            for (int y = 0; y < g.H; y++) for (int x = 0; x < g.W; x++) if (g.Get(x, y).a > 0) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
            Assert.That(maxX - minX + 1, Is.EqualTo(PixelFont.Width("HI", 2)));
            Assert.That((minX + maxX + 1) / 2f, Is.EqualTo(20).Within(1), "centred on the point it was given");
        }

        [Test]
        public void AWoodPlateHasADarkRimALitBevelAndBrassRivets()
        {
            var p = Materials.WoodPlate(60, 40);
            Assert.That(Same(p.Get(0, 20), Materials.WoodDark), "rim");
            Assert.That(Same(p.Get(30, 2), Materials.WoodLit), "lit top bevel");
            Assert.That(Same(p.Get(30, 36), Materials.WoodShade), "shaded bottom bevel");
            Assert.That(Same(p.Get(5, 5), Materials.Brass) && Same(p.Get(53, 33), Materials.Brass), "a rivet top-left and bottom-right");
            Assert.That(p.Get(30, 20).a, Is.EqualTo(255), "grain fills the face");
        }

        [Test]
        public void TheMaterialsAreTheSameEveryTimeTheyAreBaked()
        {
            var a = Materials.PaperTex(50, 30); var b = Materials.PaperTex(50, 30);
            Assert.That(a.Px.SequenceEqual(b.Px), "paper is seeded");
            var area = AreaPalette.For(Area.Area2);
            var c1 = Materials.CaveTile(area, new Rng(7)); var c2 = Materials.CaveTile(area, new Rng(7)); var c3 = Materials.CaveTile(area, new Rng(8));
            Assert.That(c1.Px.SequenceEqual(c2.Px), "one seed, one floor");
            Assert.That(c1.Px.SequenceEqual(c3.Px), Is.False, "another seed, another floor");
        }

        [Test]
        public void TheRoomToneIsTheTypeTintWashedByTheArea()
        {
            // areaFloorTint: mixHex(ROOM_BG[type], floorTint, wash); Area1 has no wash, so it is the bare tint
            Assert.That(Same(AreaPalette.For(Area.Area1).RoomTone(RoomKind.Boss), Hex.C("#2a1414")));
            var a3 = AreaPalette.For(Area.Area3);
            Assert.That(Same(a3.RoomTone(RoomKind.Normal), Hex.Mix(Hex.C("#191b22"), Hex.C("#1e3c34"), 0.20f)));
        }

        [Test]
        public void TheAtlasPacksEverySpriteWithoutOverlap()
        {
            var items = new List<(string, Img)>();
            var rng = new System.Random(3);
            for (int i = 0; i < 120; i++) items.Add(("s" + i, new Img(4 + rng.Next(40), 4 + rng.Next(40))));
            var at = Atlas.Pack(items, 512);
            Assert.That(at, Is.Not.Null);
            var rects = at.Values.ToList();
            for (int i = 0; i < rects.Count; i++)
            {
                Assert.That(rects[i].xMin >= 0 && rects[i].yMin >= 0 && rects[i].xMax <= 512 && rects[i].yMax <= 512);
                for (int j = i + 1; j < rects.Count; j++) Assert.That(rects[i].Overlaps(rects[j]), Is.False, i + " and " + j);
            }
            Assert.That(Atlas.Pack(items, 64), Is.Null, "too small a sheet says so rather than overlapping");
        }

        [Test]
        public void TheGameAtlasHoldsEveryNamedSpriteAndReadsBackItsPixels()
        {
            var atlas = SpriteSet.Atlas;
            foreach (BodyKind k in System.Enum.GetValues(typeof(BodyKind)))
                for (int f = 0; f < 4; f++) { Assert.That(atlas.Has(SpriteSet.Body(k, f, false)), k + " " + f); Assert.That(atlas.Has(SpriteSet.Body(k, f, true))); }
            foreach (var id in ItemArt.Ids) Assert.That(atlas.Has(SpriteSet.Item(id, 26)), id);
            Assert.That(atlas.Has(SpriteSet.Item("unknown", 26)), "an unknown item falls back to the generic tile");
            for (int i = 0; i < Weapons.All.Length; i++) Assert.That(atlas.Has(SpriteSet.Mouse(true, Weapons.All[i].Color)));
            // the texture is bottom-up; the region must point at the image the right way up
            var reg = atlas[SpriteSet.Player(0, false)];
            var bake = Sprites.Bake(Sprites.Frame(Sprites.PlayerUpper, 0), Sprites.PlayerPal, 2);
            var tex = atlas.Texture;
            int x0 = Mathf.RoundToInt(reg.U0 * tex.width), yTop = Mathf.RoundToInt(reg.V1 * tex.height) - 1;
            for (int y = 0; y < bake.H; y += 3)
                for (int x = 0; x < bake.W; x += 3)
                    Assert.That(Same(tex.GetPixel(x0 + x, yTop - y), bake.Get(x, y)), "pixel " + x + "," + y);
            Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Point), "pixel art is never smoothed");
        }

        [Test]
        public void SpritesAreDrawnAtTheJsPixelSizes()
        {
            Assert.That(SpriteSet.Art(BodyKind.Brunch), Is.EqualTo(1));
            Assert.That(SpriteSet.Art(BodyKind.Lunger), Is.EqualTo(2));
            Assert.That(SpriteSet.Art(BodyKind.Shooter), Is.EqualTo(2));
            Assert.That(SpriteSet.Art(BodyKind.Gunner), Is.EqualTo(3));
            Assert.That(SpriteSet.Art(BodyKind.Boss), Is.EqualTo(4));
            Assert.That(SpriteSet.Atlas[SpriteSet.Body(BodyKind.Boss, 0, false)].W, Is.EqualTo(12 * 4 + 4));
        }
    }

    public class FxTrackerTests
    {
        static RunState Run()
        {
            var run = new RunState(new Rng(5));
            run.Start(5);
            run.state = "playing";
            return run;
        }

        [Test]
        public void ASpentBlinkLaysUpToSixPuffsOverTheDistance()
        {
            var run = Run();
            var fx = new FxTracker();
            fx.Update(run, 0.016f);
            run.player.x += 116;   // a full blink
            run.blinkCharges--;
            fx.Update(run, 0.016f);
            Assert.That(fx.Count, Is.EqualTo(5), "116px / 24 rounds up to 5 puffs");
            for (int i = 0; i < fx.Count; i++) Assert.That(fx[i].Mine, Is.True);
            fx.Update(run, FxTracker.DashTrail + 0.01f);
            Assert.That(fx.Count, Is.EqualTo(0), "the trail is gone after its life");
        }

        [Test]
        public void APausedFrameAgesNothing()
        {
            var run = Run();
            var fx = new FxTracker();
            fx.Update(run, 0.016f);
            run.blinkCharges--; run.player.x += 30;
            fx.Update(run, 0.016f);
            int n = fx.Count;
            for (int i = 0; i < 100; i++) fx.Update(run, 0);
            Assert.That(fx.Count, Is.EqualTo(n));
        }

        [Test]
        public void AnAltShellThatGoesOffLeavesItsRing()
        {
            var run = Run();
            var fx = new FxTracker();
            var shell = new Projectile { x = 300, y = 300, alt = true, mode = "hook", color = Sprites.HookColor };
            run.projectiles.Add(shell);
            fx.Update(run, 0.016f);
            fx.Update(run, 0.016f);
            Assert.That(fx.Count, Is.EqualTo(0), "a shell in flight is no ring");
            run.projectiles.Remove(shell);
            fx.Update(run, 0.016f);
            Assert.That(fx.Count, Is.EqualTo(1));
            Assert.That(fx[0].Ring && fx[0].Dir == -1 && fx[0].R == (float)Balance.HookAoe, "the hook's ring collapses, at the hook's reach");
        }

        [Test]
        public void AWarmFrameAllocatesNothing()
        {
            // the view's per-frame work outside the mesh: derived FX over a live fight, and the colour lookups a frame makes
            var run = Run();
            for (int i = 0; i < 6; i++) run.enemies.Add(Enemy.Of(i % 2 == 0 ? BodyKind.Lunger : BodyKind.Shooter, 200 + i * 60, 250));
            run.projectiles.Add(new Projectile { x = 300, y = 300, alt = true, mode = "blast", color = Sprites.BlastColor });
            var fx = new FxTracker();
            for (int i = 0; i < 10; i++) { fx.Update(run, 0.016f); Hex.Of("#ffb03a"); Hex.Of(Sprites.HookColor); }
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) { fx.Update(run, 0.016f); Hex.Of("#ffb03a"); Hex.Of(Sprites.HookColor); }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "bytes allocated over 200 warm frames");
        }

        [Test]
        public void ANewRunStartsWithNothingShowing()
        {
            var fx = new FxTracker();
            var a = Run();
            fx.Update(a, 0.016f);
            a.blinkCharges--; a.player.x += 60;
            fx.Update(a, 0.016f);
            Assert.That(fx.Count, Is.GreaterThan(0));
            fx.Update(Run(), 0.016f);
            Assert.That(fx.Count, Is.EqualTo(0));
        }
    }
}
