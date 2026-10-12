using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Depths.Unity.Tests
{
    public class TickClockTests
    {
        [Test]
        public void TheGameRunsAtSixtyHertz()
        {
            Assert.That(Balance.GameHz, Is.EqualTo(60));
            Assert.That(Balance.TickHz, Is.EqualTo(Balance.GameHz), "Unity steps the core at TickHz; nothing in the player may change it");
        }

        // a 0-tick frame followed by a 2-tick one is a visible stutter. Pins Unity's float frame times
        // for whole divisors of 60; a display slightly off 60 Hz still double-steps now and then.
        [Test]
        public void AWholeDivisorFrameRateStepsTheSameTicksEveryFrame()
        {
            foreach (var (fps, per) in new[] { (60, 1), (30, 2), (20, 3) })
            {
                var c = new TickClock();
                for (int f = 0; f < 60 * 60; f++)
                    Assert.That(c.Advance(1f / fps, false), Is.EqualTo(per), fps + " fps, frame " + f);
            }
        }

        [Test]
        public void SixtyTicksASecondAtAnyFrameRate()
        {
            foreach (double fps in new[] { 30, 50, 59.94, 75, 120, 144, 165, 240, 360 })
            {
                var c = new TickClock();
                float dt = (float)(1 / fps);
                double seconds = 0;
                for (int f = 0; f < (int)(fps * 60); f++) { c.Advance(dt, false); seconds += dt; }
                Assert.That(c.Ticks, Is.EqualTo(seconds * 60).Within(1.0), fps + " fps");
            }
        }

        [Test]
        public void JitteryFramesKeepTheRate()
        {
            var totals = new double[3];
            for (int seed = 0; seed < totals.Length; seed++)
            {
                var rng = new System.Random(1000 + seed);   // seeded per iteration; the three runs must differ
                var c = new TickClock();
                double seconds = 0;
                for (int f = 0; f < 20000; f++)
                {
                    float dt = (float)(0.004 + rng.NextDouble() * 0.03);   // 4-34 ms, a bad vsync day
                    c.Advance(dt, false);
                    seconds += dt;
                }
                totals[seed] = seconds;
                Assert.That(c.Ticks, Is.EqualTo(seconds * 60).Within(1.0), "seed " + seed);
            }
            Assert.That(totals.Distinct().Count(), Is.EqualTo(totals.Length), "the seeded runs are identical");
        }

        [Test]
        public void APauseStepsNothingAndDoesNotCatchUp()
        {
            var c = new TickClock();
            for (int f = 0; f < 30; f++) c.Advance(1f / 60, false);
            long before = c.Ticks;
            for (int f = 0; f < 120; f++) Assert.That(c.Advance(1f / 60, true), Is.EqualTo(0));
            Assert.That(c.Ticks, Is.EqualTo(before));
            Assert.That(c.Advance(1f / 60, false), Is.EqualTo(1), "unpausing replays the paused time");
        }

        [Test]
        public void AHitchIsCutToAQuarterSecond()
        {
            var c = new TickClock();
            Assert.That(c.Advance(2.0, false), Is.EqualTo(15), "a 2 s frame fast-forwards the game");
            Assert.That(c.Advance(0, false), Is.EqualTo(0));
            Assert.That(c.Advance(-1, false), Is.EqualTo(0), "a negative frame steps backwards");
            Assert.That(c.Advance(double.NaN, false), Is.EqualTo(0));
            Assert.That(c.Advance(double.PositiveInfinity, false), Is.EqualTo(15), "an infinite frame is a hitch too");
            Assert.That(c.Advance(1f / 60, false), Is.EqualTo(1), "a bad frame time stops the clock for good");
            Assert.That(c.Ticks, Is.EqualTo(31));
        }
    }

    public class HeartRowTests
    {
        static Player Body(double hp, double max, double regen, double regenMax, double armour) =>
            new Player { hp = hp, maxHp = max, regenHeart = regen, regenHeartMax = regenMax, armor = armour };

        static string Row(Player p) =>
            string.Join(" ", HeartRow.Layout(p, Vector2.zero).Select(h => h.Kind + ":" + h.Fill));

        [Test]
        public void OneHpIsHalfAHeartInTheOrderRedRegenArmour()
        {
            Assert.That(Row(Body(5, 6, 1, 2, 3)),
                Is.EqualTo("Red:Full Red:Full Red:Half Regen:Half Armour:Full Armour:Half"));
        }

        [Test]
        public void TheStartingBodyIsThreeRedAndAFullRegenHeart()
        {
            var run = new RunState(new Rng(7));
            run.Start(7);   // the body a run starts with, not a hand-made one
            Assert.That(Row(run.player), Is.EqualTo("Red:Full Red:Full Red:Full Regen:Full"));
        }

        [Test]
        public void EmptyHeartsStayAndArmourHasNoEmptySlots()
        {
            Assert.That(Row(Body(0, 6, 0, 2, 0)), Is.EqualTo("Red:Empty Red:Empty Red:Empty Regen:Empty"));
            Assert.That(Row(Body(-3, 6, -1, 2, -2)), Is.EqualTo("Red:Empty Red:Empty Red:Empty Regen:Empty"), "negative HP draws as empty");
            Assert.That(Row(Body(2, 6, 0, 0, 0)), Is.EqualTo("Red:Full Red:Empty Red:Empty"), "no regen heart, no regen slot");
        }

        [Test]
        public void AnOddMaxRoundsUpAndFractionsRoundToTheNearestPoint()
        {
            Assert.That(Row(Body(7, 7, 0, 0, 0)), Is.EqualTo("Red:Full Red:Full Red:Full Red:Half"));
            Assert.That(Row(Body(6, 6, 0, 0, 0.4)), Is.EqualTo("Red:Full Red:Full Red:Full"), "0.4 armour is not half a heart");
            Assert.That(Row(Body(6, 6, 0, 0, 1.6)), Is.EqualTo("Red:Full Red:Full Red:Full Armour:Full"));
        }

        [Test]
        public void HeartsSitOneStepApartFromTheFirst()
        {
            var first = new Vector2(250, -29);
            var row = HeartRow.Layout(Body(5, 6, 1, 2, 3), first);
            for (int i = 0; i < row.Count; i++)
                Assert.That(row[i].Centre, Is.EqualTo(first + new Vector2(i * HeartRow.Step, 0)), "heart " + i);
        }

        [Test]
        public void TheRowReusesItsList()
        {
            var list = HeartRow.Layout(Body(6, 6, 2, 2, 4), Vector2.zero);
            Assert.That(list.Count, Is.EqualTo(6));
            Assert.That(HeartRow.Layout(Body(1, 2, 0, 0, 0), Vector2.zero, list), Is.SameAs(list));
            Assert.That(list.Count, Is.EqualTo(1), "a shorter row keeps the old hearts");
        }
    }
}
