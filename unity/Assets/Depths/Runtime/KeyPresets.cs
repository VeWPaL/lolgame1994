using System.Collections.Generic;

namespace Depths.Unity
{
    public enum KeyPreset { Auto, Qwerty, Azerty, Qwertz, Dvorak }

    /// <summary>
    /// Layout presets for the keys whose letter differs between layouts.
    ///
    /// The Input System binds <c>&lt;Keyboard&gt;/w</c> to a key's POSITION, so the default (Auto)
    /// already puts movement under the left hand on every layout: ZQSD on AZERTY, ,AOE on Dvorak.
    /// The letter presets bind by the character a key types instead (<c>&lt;Keyboard&gt;/#(Z)</c>),
    /// for players who would rather press the letters they read. Dvorak has no natural letter set,
    /// so its preset is the positional one, named for what the keys say.
    /// </summary>
    public static class KeyPresets
    {
        /// <summary>The rebindable keyboard slots, as "Action" or "Action/compositePart".</summary>
        public static readonly string[] Slots =
            { "Move/up", "Move/left", "Move/down", "Move/right", "Active", "Blink", "Pause", "Sheet", "Controls" };

        static string Pos(string key) => "<Keyboard>/" + key;
        static string Chr(char c) => "<Keyboard>/#(" + c + ")";

        public static IReadOnlyDictionary<string, string> PathsFor(KeyPreset p)
        {
            var m = new Dictionary<string, string>
            {
                ["Move/up"] = Pos("w"), ["Move/left"] = Pos("a"), ["Move/down"] = Pos("s"), ["Move/right"] = Pos("d"),
                ["Active"] = Pos("q"), ["Blink"] = Pos("leftShift"), ["Pause"] = Pos("escape"),
                ["Sheet"] = Pos("tab"), ["Controls"] = Pos("h"),
            };
            switch (p)
            {
                case KeyPreset.Qwerty:
                case KeyPreset.Qwertz:   // W A S D Q H are the same letters on both
                    m["Move/up"] = Chr('W'); m["Move/left"] = Chr('A'); m["Move/down"] = Chr('S'); m["Move/right"] = Chr('D');
                    m["Active"] = Chr('Q'); m["Controls"] = Chr('H');
                    break;
                case KeyPreset.Azerty:
                    m["Move/up"] = Chr('Z'); m["Move/left"] = Chr('Q'); m["Move/down"] = Chr('S'); m["Move/right"] = Chr('D');
                    m["Active"] = Chr('A'); m["Controls"] = Chr('H');
                    break;
            }
            return m;
        }

        public static string Label(KeyPreset p) => p switch
        {
            KeyPreset.Auto => "Auto (by key position)",
            KeyPreset.Dvorak => "Dvorak (by position: , A O E)",
            _ => p.ToString().ToUpperInvariant() + " (by letter)",
        };
    }
}
