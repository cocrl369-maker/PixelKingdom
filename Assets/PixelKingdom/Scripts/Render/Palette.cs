using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Farbpaletten. Bewusst wenige, warme Farben – nett und minimalistisch.</summary>
    public class Palette
    {
        public string Name;
        public Color Sky, Fog, FogSea, Ocean, Shallow, River, Beach, Grass, Fertile, Forest, Pine, Hills, Mountain, Snow, Desert, Steppe, Bank;
        public Color Tree, PineTree, Trunk, Wall, Roof, RoofAlt, Wood, Stone, Field, Dark, Plaza;
        public Color[] Factions;

        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // UI ist in allen Paletten gleich (Terrakotta & Sand – wie auf dem Vorbild)
        public static readonly Color UIPanel = H("#B05A44");
        public static readonly Color UIPanelDark = H("#8E4433");
        public static readonly Color UIPanelLight = H("#C46F57");
        public static readonly Color UIText = H("#F3DFBC");
        public static readonly Color UITextDim = H("#E2BE98");
        public static readonly Color UISand = H("#E3C59D");
        public static readonly Color UIGood = H("#F7EDA0");
        public static readonly Color UIBad = H("#4A1A10");
        public static readonly Color UIShadow = new Color(0.25f, 0.1f, 0.06f, 0.35f);

        public static readonly Palette[] All =
        {
            new Palette
            {
                Name = "Terrakotta",
                Sky = H("#E3C59D"), Fog = H("#DDBD93"), FogSea = H("#D8B78C"),
                Ocean = H("#E9D0A8"), Shallow = H("#EFDBB9"), River = H("#F2E0BF"), Beach = H("#D99A73"),
                Grass = H("#B6604A"), Fertile = H("#BE6A50"), Forest = H("#AA5A44"), Pine = H("#A25440"), Hills = H("#C57A5E"),
                Mountain = H("#CF8C70"), Snow = H("#F3DFBC"), Desert = H("#D38D66"), Steppe = H("#C27053"), Bank = H("#9C4D39"),
                Tree = H("#8E4433"), PineTree = H("#7E3B2C"), Trunk = H("#6E3324"), Wall = H("#F0D9B4"), Roof = H("#8E4433"), RoofAlt = H("#6F3426"),
                Wood = H("#9A5638"), Stone = H("#E6C9A2"), Field = H("#E3B783"), Dark = H("#5A271C"), Plaza = H("#D7A57E"),
                Factions = new[] { H("#F6E7C8"), H("#3E5C76"), H("#5B7F4A"), H("#D9A93E"), H("#6C4E8C"), H("#2F8277"), H("#C9762D"), H("#596273"),
                                   H("#8A2F4E"), H("#4A7DB0"), H("#8B8F2E"), H("#EDEDED"), H("#3A3A3A"), H("#1D1A1A") },
            },
            new Palette
            {
                Name = "Pastell",
                Sky = H("#F1E6CF"), Fog = H("#E4D8BF"), FogSea = H("#DCD2BC"),
                Ocean = H("#7FA9BE"), Shallow = H("#9CC3CF"), River = H("#8EC0D6"), Beach = H("#EAD7A4"),
                Grass = H("#A9C47F"), Fertile = H("#BCD48A"), Forest = H("#94B571"), Pine = H("#87A776"), Hills = H("#C2B083"),
                Mountain = H("#A79A8F"), Snow = H("#F7F4EE"), Desert = H("#E8CB8C"), Steppe = H("#CFC58F"), Bank = H("#9AA56E"),
                Tree = H("#5E8C55"), PineTree = H("#4B7357"), Trunk = H("#7A5A44"), Wall = H("#F3E8D2"), Roof = H("#B5584A"), RoofAlt = H("#8D4A3E"),
                Wood = H("#9C7350"), Stone = H("#C9C2B8"), Field = H("#E6CF7A"), Dark = H("#4A3B33"), Plaza = H("#DCCBA6"),
                Factions = new[] { H("#C2412F"), H("#3E5C96"), H("#2F7A4A"), H("#D9A93E"), H("#6C4E9C"), H("#2F8A87"), H("#E0832D"), H("#596273"),
                                   H("#9A2F5E"), H("#4A9DD0"), H("#8B9F2E"), H("#F5F5F5"), H("#3A3A3A"), H("#1D1A1A") },
            },
            new Palette
            {
                Name = "Abenddämmerung",
                Sky = H("#2E3148"), Fog = H("#3A3D58"), FogSea = H("#34374F"),
                Ocean = H("#3F5A7A"), Shallow = H("#4F7092"), River = H("#5E86AA"), Beach = H("#C9A97D"),
                Grass = H("#6E7F5B"), Fertile = H("#7C8C60"), Forest = H("#5F7352"), Pine = H("#566A53"), Hills = H("#8A7C68"),
                Mountain = H("#7D7584"), Snow = H("#D9DCEB"), Desert = H("#B79A72"), Steppe = H("#8C8A66"), Bank = H("#556147"),
                Tree = H("#3F5A44"), PineTree = H("#34503F"), Trunk = H("#4A3A32"), Wall = H("#E8D3B0"), Roof = H("#A04A3C"), RoofAlt = H("#7A3A30"),
                Wood = H("#7A5A40"), Stone = H("#A8A2A8"), Field = H("#C9B26A"), Dark = H("#2A2422"), Plaza = H("#9C8A70"),
                Factions = new[] { H("#F2B544"), H("#6F9AD6"), H("#6FBF73"), H("#E8D06A"), H("#A884D6"), H("#4FC2B5"), H("#F08A4B"), H("#9AA3B5"),
                                   H("#D65A86"), H("#7CC4F2"), H("#C9D45A"), H("#F5F5F5"), H("#777777"), H("#111111") },
            },
        };

        static int current;
        public static Palette Current => All[current];
        public static int Index => current;
        public static void Set(int i) { current = ((i % All.Length) + All.Length) % All.Length; }

        public Color FactionColor(int idx) => Factions[((idx % Factions.Length) + Factions.Length) % Factions.Length];

        public Color BiomeColor(Biome b)
        {
            switch (b)
            {
                case Biome.Ocean: return Ocean;
                case Biome.Shallow: return Shallow;
                case Biome.Lake: return River;
                case Biome.Beach: return Beach;
                case Biome.Grass: return Grass;
                case Biome.Fertile: return Fertile;
                case Biome.Forest: return Forest;
                case Biome.Pine: return Pine;
                case Biome.Hills: return Hills;
                case Biome.Mountain: return Mountain;
                case Biome.Snow: return Snow;
                case Biome.Desert: return Desert;
                case Biome.Steppe: return Steppe;
            }
            return Grass;
        }
    }
}
