using System.Collections.Generic;

namespace Depths
{
    /// <summary>The region bosses' kit (docs/bosses.md, sections 3 and 8): each run draws its bosses per area from its own
    /// stream, health is flat per area, and every hit a region boss lands is booked to its attack (RunState.bossBy).</summary>
    public static class Bosses
    {
        /// <summary>Whether the region bosses hold floors 1-13 (tuned, the floor boss on, and the kit on); else the Gatekeeper.</summary>
        public static bool On => Balance.Tuned && Balance.TuneFloorBoss && Balance.TuneBosses;

        /// <summary>The rosters as data, by area (the Shallows, the Kiln Works, the Flooded Galleries, floor 13): ids, in the
        /// design's order. A boss not built yet falls back to the Gatekeeper (Built).</summary>
        public static readonly string[][] Rosters =
        {
            new[] { "pike", "tide", "hermit", "chorus", "mudmouth", "wader" },
            new[] { "foreman", "bellows", "potter", "colossus", "crucibles", "overseer" },
            new[] { "curator", "saint", "undertow", "lamplighter", "choir", "bishop" },
            new[] { "warden", "twins", "bellringer" },
        };

        /// <summary>The bar's names, by the same ids.</summary>
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["pike"] = "THE OLD PIKE", ["tide"] = "MOTHER TIDE", ["hermit"] = "THE HERMIT", ["chorus"] = "THE FROG CHORUS",
            ["mudmouth"] = Mudmouth.Name, ["wader"] = "THE GREY WADER",
            ["foreman"] = "THE FOREMAN", ["bellows"] = "THE BELLOWS", ["potter"] = "THE POTTER", ["colossus"] = "THE SLAG COLOSSUS",
            ["crucibles"] = "THE TWIN CRUCIBLES", ["overseer"] = "THE OVERSEER",
            ["curator"] = "THE CURATOR", ["saint"] = "THE GLASS SAINT", ["undertow"] = "THE UNDERTOW", ["lamplighter"] = "THE LAMPLIGHTER",
            ["choir"] = "THE CHOIR OF ECHOES", ["bishop"] = "THE LEECH BISHOP",
            ["warden"] = "THE WARDEN, RETURNED", ["twins"] = "THE TWIN GATEKEEPERS", ["bellringer"] = "THE BELL-RINGER",
        };

        /// <summary>The bosses built so far, by id, and the body each spawns as. Everything else is the Gatekeeper.</summary>
        public static readonly Dictionary<string, BodyKind> Built = new Dictionary<string, BodyKind> { ["mudmouth"] = BodyKind.Mudmouth };

        public const int PerArea = 4, LastFloor = 13;
        /// <summary>Flat health by area (docs/bosses.md section 2): the Shallows, the Kiln Works, the Galleries, floor 13's miniboss.</summary>
        public static readonly double[] AreaHp = { 1750, 2100, 2150, 1150 };
        const uint StreamSalt = 0xB055E5u;

        /// <summary>The bot's and the tests' hook: a boss id every draw of its own area becomes (null: the draw stands).</summary>
        public static string? Force;

        /// <summary>The run's draw: floors 1-12 take four of their area's six each, shuffled, never one twice; floor 13 one of
        /// its three. Index by floor (0 and 14 are null). Its own stream from the root seed, so no other roll moves.</summary>
        public static string?[] Draw(uint root)
        {
            var rng = new Rng(root ^ StreamSalt);
            var drawn = new string?[Balance.FinalFloor + 1];
            for (int a = 0; a < 3; a++)
            {
                var bag = (string[])Rosters[a].Clone();
                for (int i = bag.Length - 1; i > 0; i--) { int j = (int)(rng.Run() * (i + 1)); (bag[i], bag[j]) = (bag[j], bag[i]); }
                for (int k = 0; k < PerArea; k++) drawn[a * PerArea + k + 1] = bag[k];
            }
            var last = Rosters[3];
            drawn[LastFloor] = last[(int)(rng.Run() * last.Length) % last.Length];
            return drawn;
        }

        /// <summary>The id drawn for this floor (Force applied), or null off floors 1-13.</summary>
        public static string? IdFor(RunState run)
        {
            int f = run.floor;
            if (run.bosses == null || f < 1 || f > LastFloor) return null;
            if (Force != null && System.Array.IndexOf(Rosters[AreaIndex(f)], Force) >= 0) return Force;
            return run.bosses[f];
        }

        static int AreaIndex(int floor) => floor >= LastFloor ? 3 : (floor - 1) / PerArea;

        /// <summary>The region boss this floor's boss room holds, or null: the kit off, the final floor, or a boss not built yet.</summary>
        public static BodyKind? KindFor(RunState run)
        {
            if (!On || run.finalFloor) return null;
            var id = IdFor(run);
            return id != null && Built.TryGetValue(id, out var k) ? k : (BodyKind?)null;
        }

        /// <summary>Whether this kind is a region boss (one of the rosters, built).</summary>
        public static bool IsRegion(BodyKind k) => k == BodyKind.Mudmouth;

        /// <summary>A region boss's id, for the books; null for any other kind.</summary>
        public static string? IdOf(BodyKind k) => k == BodyKind.Mudmouth ? "mudmouth" : null;

        /// <summary>The bar's name for any boss kind.</summary>
        public static string NameOf(BodyKind k) => k == BodyKind.SunkenKing ? FinalBoss.Name : k == BodyKind.Gatekeeper ? FloorBoss.Name
            : IdOf(k) is string id ? Names[id] : "THE WARDEN";

        /// <summary>Flat health for a region boss on this floor: its area's, times the boss's own scale.</summary>
        public static double Health(int floor, double scale) => AreaHp[AreaIndex(System.Math.Max(1, System.Math.Min(LastFloor, floor)))] * scale;

        /// <summary>A hit being dealt now books to `run.hitBook` (a region boss's "boss:attack"), as FinalBoss books to finalBy.</summary>
        public static void Book(RunState run, double amount)
        {
            if (run.hitBook == null) return;
            run.bossBy.TryGetValue(run.hitBook, out var was);
            run.bossBy[run.hitBook] = was + amount;
        }

        /// <summary>The region boss is down: its death beat (the exit waits for it), its brood and shells go with it (TickRoom).</summary>
        public static void Fall(RunState run, Enemy e, int beat)
        {
            run.finaleT = run.finaleMax = beat;
            run.finaleX = e.x; run.finaleY = e.y; run.finaleKind = e.kind;
        }
    }
}
