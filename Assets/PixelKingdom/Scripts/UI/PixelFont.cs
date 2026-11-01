using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Eigene Pixel-Bitmapschrift (5x8, proportional, mit Umlauten).
    /// Wird im GUI-Raum mit ganzzahliger Skalierung gezeichnet und bleibt dadurch knackscharf.
    /// </summary>
    public static class PixelFont
    {
        public const int CellW = 6, CellH = 9, LineH = 10;
        static Texture2D atlas;
        static readonly Dictionary<char, (int index, int width)> glyphs = new Dictionary<char, (int, int)>();
        static int count;

        static readonly string[] Data =
        {
            "A .###. #...# #...# ##### #...# #...# #...#",
            "B ####. #...# #...# ####. #...# #...# ####.",
            "C .###. #...# #.... #.... #.... #...# .###.",
            "D ###.. #..#. #...# #...# #...# #..#. ###..",
            "E ##### #.... #.... ####. #.... #.... #####",
            "F ##### #.... #.... ####. #.... #.... #....",
            "G .###. #...# #.... #.### #...# #...# .####",
            "H #...# #...# #...# ##### #...# #...# #...#",
            "I ### .#. .#. .#. .#. .#. ###",
            "J ..### ...#. ...#. ...#. #..#. #..#. .##..",
            "K #...# #..#. #.#.. ##... #.#.. #..#. #...#",
            "L #.... #.... #.... #.... #.... #.... #####",
            "M #...# ##.## #.#.# #.#.# #...# #...# #...#",
            "N #...# ##..# #.#.# #..## #...# #...# #...#",
            "O .###. #...# #...# #...# #...# #...# .###.",
            "P ####. #...# #...# ####. #.... #.... #....",
            "Q .###. #...# #...# #...# #.#.# #..#. .##.#",
            "R ####. #...# #...# ####. #.#.. #..#. #...#",
            "S .#### #.... #.... .###. ....# ....# ####.",
            "T ##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..",
            "U #...# #...# #...# #...# #...# #...# .###.",
            "V #...# #...# #...# #...# #...# .#.#. ..#..",
            "W #...# #...# #...# #.#.# #.#.# #.#.# .#.#.",
            "X #...# #...# .#.#. ..#.. .#.#. #...# #...#",
            "Y #...# #...# .#.#. ..#.. ..#.. ..#.. ..#..",
            "Z ##### ....# ...#. ..#.. .#... #.... #####",
            "a ..... ..... .###. ....# .#### #...# .####",
            "b #.... #.... ####. #...# #...# #...# ####.",
            "c ..... ..... .###. #.... #.... #.... .###.",
            "d ....# ....# .#### #...# #...# #...# .####",
            "e ..... ..... .###. #...# ##### #.... .###.",
            "f ..## .#.. ###. .#.. .#.. .#.. .#..",
            "g ..... ..... .#### #...# #...# .#### ....# .###.",
            "h #.... #.... ####. #...# #...# #...# #...#",
            "i # . # # # # #",
            "j ..# ... ..# ..# ..# ..# ..# ##.",
            "k #... #... #..# #.#. ##.. #.#. #..#",
            "l #. #. #. #. #. #. .#",
            "m ..... ..... ##.#. #.#.# #.#.# #.#.# #.#.#",
            "n ..... ..... ####. #...# #...# #...# #...#",
            "o ..... ..... .###. #...# #...# #...# .###.",
            "p ..... ..... ####. #...# #...# ####. #.... #....",
            "q ..... ..... .#### #...# #...# .#### ....# ....#",
            "r .... .... #.## ##.. #... #... #...",
            "s ..... ..... .#### #.... .###. ....# ####.",
            "t .#.. .#.. #### .#.. .#.. .#.. ..##",
            "u ..... ..... #...# #...# #...# #...# .####",
            "v ..... ..... #...# #...# #...# .#.#. ..#..",
            "w ..... ..... #...# #...# #.#.# #.#.# .#.#.",
            "x ..... ..... #...# .#.#. ..#.. .#.#. #...#",
            "y ..... ..... #...# #...# #...# .#### ....# .###.",
            "z ..... ..... ##### ...#. ..#.. .#... #####",
            "Ä .#.#. ..... .###. #...# ##### #...# #...#",
            "Ö .#.#. ..... .###. #...# #...# #...# .###.",
            "Ü .#.#. ..... #...# #...# #...# #...# .###.",
            "ä .#.#. ..... .###. ....# .#### #...# .####",
            "ö .#.#. ..... .###. #...# #...# #...# .###.",
            "ü .#.#. ..... #...# #...# #...# #...# .####",
            "ß .##.. #..#. #..#. #.#.. #..#. #...# #.##.",
            "0 .###. #...# #..## #.#.# ##..# #...# .###.",
            "1 .#. ##. .#. .#. .#. .#. ###",
            "2 .###. #...# ....# ...#. ..#.. .#... #####",
            "3 ####. ....# ....# .###. ....# ....# ####.",
            "4 ...#. ..##. .#.#. #..#. ##### ...#. ...#.",
            "5 ##### #.... ####. ....# ....# #...# .###.",
            "6 .###. #.... #.... ####. #...# #...# .###.",
            "7 ##### ....# ...#. ..#.. .#... .#... .#...",
            "8 .###. #...# #...# .###. #...# #...# .###.",
            "9 .###. #...# #...# .#### ....# ....# .###.",
            ". . . . . . . #",
            ", .. .. .. .. .. .# .# #.",
            ": . . # . . # .",
            "; .. .. .# .. .. .# #.",
            "! # # # # # . #",
            "? .###. #...# ....# ..##. ..#.. ..... ..#..",
            "- ... ... ... ### ... ... ...",
            "+ ... ... .#. ### .#. ... ...",
            "/ ..# ..# .#. .#. .#. #.. #..",
            "( .# #. #. #. #. #. .#",
            ") #. .# .# .# .# .# #.",
            "% ##... ##..# ...#. ..#.. .#... #..## ...##",
            "' # # . . . . .",
            "\" #.# #.# ... ... ... ... ...",
            "= ... ... ### ... ### ... ...",
            "< ... ..# .#. #.. .#. ..# ...",
            "> ... #.. .#. ..# .#. #.. ...",
            "* ... #.# .#. #.# ... ... ...",
            "_ ..... ..... ..... ..... ..... ..... ..... #####",
            "[ ## #. #. #. #. #. ##",
            "] ## .# .# .# .# .# ##",
            "| # # # # # # # #",
            "# .#.#. ##### .#.#. .#.#. ##### .#.#. .....",
            "° .#. #.# .#. ... ... ... ...",
            "@ .###. #...# #.### #.#.# #.### #.... .###.",
        };

        public static void Init()
        {
            if (atlas != null) return;
            count = Data.Length;
            atlas = new Texture2D(count * CellW, CellH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelFont"
            };
            var px = new Color32[count * CellW * CellH];
            for (int g = 0; g < count; g++)
            {
                string line = Data[g];
                char ch = line[0];
                var rows = line.Substring(2).Split(' ');
                int width = 0;
                for (int r = 0; r < rows.Length && r < CellH; r++)
                {
                    string row = rows[r];
                    for (int c = 0; c < row.Length && c < CellW; c++)
                    {
                        if (row[c] != '#') continue;
                        width = Mathf.Max(width, c + 1);
                        int ty = CellH - 1 - r; // Textur-Y von unten
                        px[ty * count * CellW + g * CellW + c] = new Color32(255, 255, 255, 255);
                    }
                    width = Mathf.Max(width, row.Length);
                }
                glyphs[ch] = (g, Mathf.Max(1, width));
            }
            atlas.SetPixels32(px);
            atlas.Apply(false, false);
        }

        static char Normalize(char c)
        {
            switch (c)
            {
                case '„': case '“': case '”': return '"';
                case '‘': case '’': return '\'';
                case '–': case '—': return '-';
                case 'é': case 'è': case 'ê': return 'e';
                case 'á': case 'à': return 'a';
                case 'ø': return 'ö';
                case 'Ø': return 'Ö';
                case '&': return '+';
            }
            return c;
        }

        public static int CharWidth(char c)
        {
            if (c == ' ') return 3;
            c = Normalize(c);
            if (glyphs.TryGetValue(c, out var g)) return g.width + 1;
            if (glyphs.TryGetValue(char.ToUpperInvariant(c), out g)) return g.width + 1;
            return 4;
        }

        /// <summary>"__" schaltet die Unterstreichung um (wird nicht gezeichnet).</summary>
        static bool IsMark(string s, int i) => s[i] == '_' && i + 1 < s.Length && s[i + 1] == '_';

        public static int Width(string s)
        {
            Init();
            int w = 0, best = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (IsMark(s, i)) { i++; continue; }
                if (c == '\n') { best = Mathf.Max(best, w); w = 0; continue; }
                w += CharWidth(c);
            }
            return Mathf.Max(best, w) - (s.Length > 0 ? 1 : 0);
        }

        public static string StripMarks(string s) => s.Replace("__", "");

        /// <summary>Zeichnet eine Zeile ohne Umbruch. Skalierung immer ganzzahlig.</summary>
        public static void Draw(float x, float y, string s, Color col, int scale = 1)
        {
            if (string.IsNullOrEmpty(s) || Event.current.type != EventType.Repaint) return;
            Init();
            var old = GUI.color;
            GUI.color = col;
            float cx = x;
            float cellU = 1f / count;
            bool ul = false;
            for (int i = 0; i < s.Length; i++)
            {
                char raw = s[i];
                if (IsMark(s, i)) { i++; ul = !ul; continue; }
                if (raw == '\n') { cx = x; y += LineH * scale; continue; }
                float adv;
                if (raw == ' ') adv = 3 * scale;
                else
                {
                    char c = Normalize(raw);
                    if (!glyphs.TryGetValue(c, out var g) && !glyphs.TryGetValue(char.ToUpperInvariant(c), out g)) adv = 4 * scale;
                    else
                    {
                        var uv = new Rect(g.index * cellU, 0, cellU, 1);
                        GUI.DrawTextureWithTexCoords(new Rect(cx, y, CellW * scale, CellH * scale), atlas, uv);
                        adv = (g.width + 1) * scale;
                    }
                }
                // Unterstreichung (ohne das Leerzeichen hinter dem letzten Wort)
                if (ul && !(raw == ' ' && (i + 2 < s.Length && IsMark(s, i + 1))))
                    GUI.DrawTexture(new Rect(cx, y + 8 * scale, (i + 1 < s.Length && !IsMark(s, i + 1)) ? adv : adv - scale, scale), Texture2D.whiteTexture);
                cx += adv;
            }
            GUI.color = old;
        }

        public static void DrawShadow(float x, float y, string s, Color col, Color shadow, int scale = 1)
        {
            Draw(x + scale, y + scale, s, shadow, scale);
            Draw(x, y, s, col, scale);
        }

        /// <summary>Text in ein Rechteck mit Zeilenumbruch. Gibt die benötigte Höhe zurück.</summary>
        public static float DrawWrapped(Rect r, string text, Color col, int scale = 1, TextAnchor align = TextAnchor.UpperLeft, bool draw = true)
        {
            var lines = Wrap(text, Mathf.Max(10, (int)(r.width / scale)));
            float h = lines.Count * LineH * scale;
            float y = r.y;
            if (align == TextAnchor.MiddleCenter || align == TextAnchor.MiddleLeft) y = r.y + (r.height - h) / 2f + 1;
            foreach (var l in lines)
            {
                float x = r.x;
                if (align == TextAnchor.UpperCenter || align == TextAnchor.MiddleCenter) x = r.x + (r.width - Width(l) * scale) / 2f;
                else if (align == TextAnchor.UpperRight || align == TextAnchor.MiddleRight) x = r.xMax - Width(l) * scale;
                if (draw) Draw(Mathf.Round(x), Mathf.Round(y), l, col, scale);
                y += LineH * scale;
            }
            return h;
        }

        public static List<string> Wrap(string text, int maxW)
        {
            Init();
            var result = new List<string>();
            foreach (var para in (text ?? "").Split('\n'))
            {
                var words = para.Split(' ');
                string cur = "";
                foreach (var w in words)
                {
                    string test = cur.Length == 0 ? w : cur + " " + w;
                    if (Width(test) > maxW && cur.Length > 0) { result.Add(cur); cur = w; }
                    else cur = test;
                }
                result.Add(cur);
            }
            // Unterstreichung über Zeilenumbrüche fortsetzen
            bool open = false;
            for (int i = 0; i < result.Count; i++)
            {
                string l = result[i];
                if (open) l = "__" + l;
                int marks = 0;
                for (int k = 0; k + 1 < l.Length; k++) if (l[k] == '_' && l[k + 1] == '_') { marks++; k++; }
                open = marks % 2 == 1;
                if (open) l += "__";
                result[i] = l;
            }
            return result;
        }
    }
}
