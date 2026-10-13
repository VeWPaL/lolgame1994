using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Depths.Unity.Hud
{
    /// <summary>
    /// The ledger's hand: a book face read from the OS at runtime (no font files in the repo). Georgia,
    /// then the other Windows book faces; if none can be loaded the label keeps Unity's default, so a
    /// missing font costs the look, never the text.
    /// </summary>
    public static class LedgerFonts
    {
        static readonly string[] Families = { "Georgia", "Palatino Linotype", "Book Antiqua", "Constantia", "Cambria", "Times New Roman" };
        static readonly string Printable = BuildPrintable();
        static string BuildPrintable() { var c = new char[96]; for (int i = 0; i < 95; i++) c[i] = (char)(32 + i); c[95] = '·'; return new string(c); }
        static bool _tried;
        static FontDefinition? _regular, _italic, _bold;

        public static FontDefinition? Regular { get { Load(); return _regular; } }
        public static FontDefinition? Italic { get { Load(); return _italic ?? _regular; } }
        public static FontDefinition? Bold { get { Load(); return _bold ?? _regular; } }

        static void Load()
        {
            if (_tried) return;
            _tried = true;
            foreach (var f in Families)
            {
                var r = Make(f, "Regular");
                if (r == null) continue;
                _regular = r; _italic = Make(f, "Italic"); _bold = Make(f, "Bold");
                return;
            }
        }

        static FontDefinition? Make(string family, string style)
        {
            try
            {
                var fa = FontAsset.CreateFontAsset(family, style, 90);
                if (fa == null) return null;
                fa.hideFlags = HideFlags.DontSave;
                // rasterise every printable ASCII glyph now: a glyph made mid-fight (a new kill count) is a hitch
                fa.TryAddCharacters(Printable, out _);
                return FontDefinition.FromSDFFont(fa);
            }
            catch (Exception) { }   // a family the OS does not have; try the next
            return null;
        }

        /// <summary>Sets the face on an element (it inherits down to every label inside).</summary>
        public static void Use(VisualElement e, FontDefinition? f)
        {
            if (f.HasValue) e.style.unityFontDefinition = f.Value;
        }

        /// <summary>A label in the ledger's ink: the face, a size, a colour, no Unity padding.</summary>
        public static Label Ink(string text, int size, Color color, FontDefinition? face = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            Use(l, face ?? Regular);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityTextAlign = align;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            return l;
        }
    }
}
