using System;
using System.Collections.Generic;

namespace Depths.Playtest
{
    // Field names match the JS bot's JSON so the two outputs read alike. HP is in game HP (2 = 1 heart).

    public sealed class FloorRecord
    {
        public int floor, ticks, rooms, cleared, bossTicks;
        public double dmg, healed, regen;   // regen: HP the regenerating heart refilled on this floor
        public bool bossKilled;        // set on the actual descent (the JS set it while walking to the exit)
        public double hpIn, maxHpIn;   // hearts: red + regenerating, as the JS bot's hp
        public double? hpOut;          // after the step that left the floor (or at the run's end)
        public double armorIn;
        public double? armorOut;
    }

    public sealed class RunResult
    {
        public uint seed;
        public string profile = "";
        public int floor = 1, ticks;
        public string end = "timeout";   // death, timeout, stuck, error
        public object? cause;            // death: [source, hp]; stuck: what the room held
        public List<FloorRecord> floors = new List<FloorRecord>();
        public SortedDictionary<string, double> dmgBySource =
            new SortedDictionary<string, double>(StringComparer.Ordinal);
        public double healed, regenHealed;
        public SortedDictionary<string, double> healBy = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public int hits, shots, kills, blinks, dodgeBlinks, secrets;
        public int qPresses, actives;   // Q pressed when low; actives: presses that raised HP (the JS rule)
        public int maskedHits;          // ticks where a same-layer heal hid (part of) a hit from the HP deltas
        public double maskedHp;
        public List<string> items = new List<string>();
        public string weapon = "";
        public List<string> errors = new List<string>();
    }

    public sealed class Batch
    {
        public string label = "";
        public string? commit;
        public string brunch = "";
        public int tickHz;
        public double minutes;
        public List<uint> seeds = new List<uint>();
        public List<string> profiles = new List<string>();
        public List<RunResult> runs = new List<RunResult>();
    }
}
