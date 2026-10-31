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

        public static int Width(string s)
        {
            Init();
            int w = 0, best = 0;
            foreach (var c in s)
            {
                if (c == '\n') { best = Mathf.Max(best, w); w = 0; continue; }
                w += CharWidth(c);
            }
            return Mathf.Max(best, w) - (s.Length > 0 ? 1 : 0);
        }

        /// <summary>Zeichnet eine Zeile ohne Umbruch. Skalierung immer ganzzahlig.</summary>
        public static void Draw(float x, float y, string s, Color col, int scale = 1)
        {
            if (string.IsNullOrEmpty(s) || Event.current.type != EventType.Repaint) return;
            Init();
            var old = GUI.color;
            GUI.color = col;
            float cx = x;
            float cellU = 1f / count;
            foreach (var raw in s)
            {
                if (raw == '\n') { cx = x; y += LineH * scale; continue; }
                if (raw == ' ') { cx += 3 * scale; continue; }
                char c = Normalize(raw);
                if (!glyphs.TryGetValue(c, out var g) && !glyphs.TryGetValue(char.ToUpperInvariant(c), out g)) { cx += 4 * scale; continue; }
                var uv = new Rect(g.index * cellU, 0, cellU, 1);
                GUI.DrawTextureWithTexCoords(new Rect(cx, y, CellW * scale, CellH * scale), atlas, uv);
                cx += (g.width + 1) * scale;
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
            return result;
        }
    }
}
