using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Farben der Oberfläche (Holz & Pergament, Retro) und Auswahl der Kartenpalette.</summary>
    public static class Palette
    {
        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // Oberfläche
        public static readonly Color UIPanel = H("#6B4430");
        public static readonly Color UIPanelDark = H("#3E2618");
        public static readonly Color UIPanelLight = H("#8A5A3C");
        public static readonly Color UIText = H("#F7E9C9");
        public static readonly Color UITextDim = H("#D9BF94");
        public static readonly Color UISand = H("#EAD7AE");
        public static readonly Color UIGood = H("#F2C94C");
        public static readonly Color UIBad = H("#9E2B25");
        public static readonly Color UIShadow = new Color(0.08f, 0.04f, 0.02f, 0.45f);

        static readonly Color[] factions =
        {
            H("#D9433A"), H("#3E6CC0"), H("#3E9A4E"), H("#E2B23A"), H("#8A55B8"), H("#2FA39A"), H("#E07A2C"), H("#6E7A8C"),
            H("#B0306A"), H("#5AB0E8"), H("#9AAE2E"), H("#F2F0EA"), H("#4A4A4A"), H("#1E1A22"),
        };

        static int current;
        public static int Index => current;
        public static ArtPalette Art => ArtPalette.All[current];
        public static string Name => Art.Name;
        public static void Set(int i) { current = ((i % ArtPalette.All.Length) + ArtPalette.All.Length) % ArtPalette.All.Length; }

        public static Color FactionColor(int idx) => factions[((idx % factions.Length) + factions.Length) % factions.Length];
        public static Color ToColor(Px p) => new Color32(p.r, p.g, p.b, p.a);
    }
}
