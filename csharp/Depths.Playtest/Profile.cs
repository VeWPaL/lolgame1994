using System;
using System.Collections.Generic;

namespace Depths.Playtest
{
    /// <summary>One stand-in player. The numbers are tools/playtest.js's, in ticks at 210 Hz.</summary>
    public sealed class Profile
    {
        public string Name = "";
        public int React;          // ticks between decisions, and before a seen shot can be dodged
        public double AimErr;      // radians of aim noise (scaled gaussian)
        public double Dodge;       // chance factor for a dodge blink
        public double BandMin, BandMax;
        public double Blast;       // chance to throw the alt into a crowd of 3+
        public double Heal;        // HP (red + regen, the JS bot's hp) at or under which Q is tried

        public static readonly IReadOnlyList<Profile> All = new[]
        {
            new Profile { Name = "novice", React = 55, AimErr = 0.14, Dodge = 0.2,
                          BandMin = 110, BandMax = 220, Blast = 0.25, Heal = 1 },
            new Profile { Name = "average", React = 38, AimErr = 0.07, Dodge = 0.55,
                          BandMin = 150, BandMax = 260, Blast = 0.55, Heal = 2 },
            new Profile { Name = "skilled", React = 24, AimErr = 0.03, Dodge = 0.9,
                          BandMin = 170, BandMax = 280, Blast = 0.85, Heal = 2 },
        };

        public static Profile Get(string name)
        {
            foreach (var p in All) if (p.Name == name) return p;
            throw new ArgumentException("no profile called " + name + " (novice, average, skilled)");
        }
    }

    /// <summary>The bot's own xorshift32, seeded exactly as the JS bot seeds it. Never the game's streams.</summary>
    public sealed class BotRng
    {
        uint _s;

        public BotRng(uint seed, string profile)
        {
            _s = (uint)((ulong)seed * 2654435761UL + (ulong)profile.Length * 977UL);
        }

        public double Next()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return _s / 4294967296.0;
        }

        public double Gauss()
        {
            double u = 0;
            for (int i = 0; i < 4; i++) u += Next();
            return (u - 2) * 0.87;
        }
    }
}
