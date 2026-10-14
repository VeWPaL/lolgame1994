using NUnit.Framework;
using UnityEngine;

namespace Depths.Unity.Tests
{
    public class ShakeTests
    {
        // the largest offset a kick gives over its first half second
        static float Peak(ShakeLevel level, ScreenShake.Kick kick, float weight = 1)
        {
            var s = new ScreenShake { Level = level };
            s.Add(kick, 1, weight);
            float peak = 0;
            for (int f = 0; f < 30; f++)
            {
                s.Step(1 / 60f);
                var (x, y, _) = s.Offset();
                peak = Mathf.Max(peak, Mathf.Sqrt(x * x + y * y));
            }
            return peak;
        }

        // the steady shake of a gun held down at its cadence for two seconds, in px (its largest offset in the last second)
        static float Held(ShakeLevel level, double cooldownS)
        {
            var s = new ScreenShake { Level = level };
            float peak = 0, next = 0;
            for (int f = 0; f < 120; f++)
            {
                if (f / 60f >= next) { s.Add(ScreenShake.Kick.Shot, 1, ScreenShake.ShotWeight(cooldownS)); next += (float)cooldownS; }
                s.Step(1 / 60f);
                var (x, y, _) = s.Offset();
                if (f >= 60) peak = Mathf.Max(peak, Mathf.Sqrt(x * x + y * y));
            }
            return peak;
        }

        [Test]
        public void OffNeverShakes()
        {
            foreach (ScreenShake.Kick k in System.Enum.GetValues(typeof(ScreenShake.Kick)))
                Assert.That(Peak(ShakeLevel.Off, k), Is.EqualTo(0), k.ToString());
        }

        [Test]
        public void EachLevelShakesHarderThanTheOneBelow()
        {
            float med = Peak(ShakeLevel.Medium, ScreenShake.Kick.Thud), high = Peak(ShakeLevel.High, ScreenShake.Kick.Thud), mad = Peak(ShakeLevel.Insanity, ScreenShake.Kick.Thud);
            Assert.That(med, Is.GreaterThan(3f), "Medium feels a Burrower land (several px)");
            Assert.That(high, Is.GreaterThan(med));
            Assert.That(mad, Is.GreaterThan(high * 2), "Insanity is silly");
        }

        [Test]
        public void MediumKeepsItForTheBigMomentsAndHighAddsTheGuns()
        {
            Assert.That(Peak(ShakeLevel.Medium, ScreenShake.Kick.Shot), Is.EqualTo(0), "Isaac does not shake for a tear");
            Assert.That(Peak(ShakeLevel.Medium, ScreenShake.Kick.Kill), Is.EqualTo(0));
            float bolt = Peak(ShakeLevel.High, ScreenShake.Kick.Shot, ScreenShake.ShotWeight(Weapons.All[0].CooldownS));
            Assert.That(bolt, Is.GreaterThan(1f), "Gungeon kicks with a gun: a Bolt shot moves the screen a visible pixel or more");
            Assert.That(bolt, Is.LessThan(Peak(ShakeLevel.High, ScreenShake.Kick.Thud) / 2), "but a shot is a nudge next to a landing");
            Assert.That(Held(ShakeLevel.High, Weapons.All[2].CooldownS), Is.LessThan(3f), "and the Beam's stream is a buzz, not an earthquake");
            Assert.That(Held(ShakeLevel.Insanity, Weapons.All[2].CooldownS), Is.GreaterThan(6f), "unless it is Insanity");
            Assert.That(new ScreenShake { Level = ShakeLevel.Insanity }.HudToo, "Insanity takes the ledger with it");
        }

        [Test]
        public void TheShakeSettles()
        {
            var s = new ScreenShake { Level = ShakeLevel.Insanity };
            for (int i = 0; i < 6; i++) s.Add(ScreenShake.Kick.Thud);
            var before = s.Offset();
            Assert.That(s.Offset(), Is.EqualTo(before), "unstepped (paused), the shake does not move");
            for (int f = 0; f < 60; f++) s.Step(1 / 60f);
            Assert.That(s.Trauma01, Is.EqualTo(0), "a second after the biggest pile-up, the screen is still");
            Assert.That(s.Offset(), Is.EqualTo((0f, 0f, 0f)));
        }

        [Test]
        public void TheSettingIsMediumUntilChosenAndReadsTheOldSwitch()
        {
            var store = new MemoryStore();
            var g = new GameplaySettings(store);
            Assert.That(g.Shake, Is.EqualTo(ShakeLevel.Medium));
            g.Shake = ShakeLevel.Insanity;
            Assert.That(new GameplaySettings(store).Shake, Is.EqualTo(ShakeLevel.Insanity));
            store.S[GameplaySettings.ShakeKey] = "on";
            Assert.That(g.Shake, Is.EqualTo(ShakeLevel.Medium), "the old on/off switch's on");
            store.S[GameplaySettings.ShakeKey] = "off";
            Assert.That(g.Shake, Is.EqualTo(ShakeLevel.Off));
        }
    }
}
