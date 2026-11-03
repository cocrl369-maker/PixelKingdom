using System;
using System.Collections.Generic;

namespace PixelKingdom
{
    /// <summary>
    /// Sprache des Spiels. Texte stehen im Code auf Deutsch; Loc.T / Loc.F schlagen die Übersetzung nach.
    /// Fehlt eine Übersetzung, bleibt der deutsche Text stehen.
    /// </summary>
    public static partial class Loc
    {
        public enum Lang { De = 0, En = 1 }
        public static Lang Current = Lang.De;
        public static bool English => Current == Lang.En;
        public static event Action Changed;

        static Dictionary<string, string> en;
        static Dictionary<string, string> En
        {
            get
            {
                if (en == null)
                {
                    en = new Dictionary<string, string>(4096);
                    FillEnglish(en);
                }
                return en;
            }
        }

        public static void Set(Lang l)
        {
            if (Current == l) return;
            Current = l;
            Changed?.Invoke();
        }

        /// <summary>Für Tests: sammelt Texte ohne Übersetzung.</summary>
        public static HashSet<string> Missing;

        /// <summary>Übersetzt einen festen Text.</summary>
        public static string T(string de)
        {
            if (Current == Lang.De || de == null) return de;
            if (En.TryGetValue(de, out var s)) return s;
            Missing?.Add(de);
            return de;
        }

        /// <summary>Übersetzt einen Text mit Platzhaltern {0}, {1}, ... und setzt die Werte ein.</summary>
        public static string F(string de, params object[] args)
        {
            string fmt = de;
            if (Current != Lang.De) { if (En.TryGetValue(de, out var s)) fmt = s; else Missing?.Add(de); }
            try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, args); }
            catch (FormatException) { return string.Format(System.Globalization.CultureInfo.InvariantCulture, de, args); }
        }

        /// <summary>Ist dieser Text übersetzt? (für Tests)</summary>
        public static bool Has(string de) => En.ContainsKey(de);

        static partial void FillEnglish(Dictionary<string, string> d);
    }

    /// <summary>Feste Textliste, die bei jedem Zugriff übersetzt wird (z.B. Namen von Jahreszeiten).</summary>
    public sealed class LArr
    {
        readonly string[] de;
        public LArr(params string[] de) { this.de = de; }
        public string this[int i] => Loc.T(de[i]);
        public int Length => de.Length;
        public string Raw(int i) => de[i];
    }
}
