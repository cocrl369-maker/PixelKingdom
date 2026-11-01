namespace PixelKingdom
{
    /// <summary>Farbpaletten für Karte und Natur. Gebäude behalten ihre eigenen Farben.</summary>
    public class ArtPalette
    {
        public string Name;
        public Px Grass, Fertile, ForestFloor, PineFloor, Hills, Mountain, Snow, Desert, Steppe, Beach;
        public Px Deep, Water, Shallow, Foam, River, Bank, Cliff, CliffDark;
        public Px Leaf, LeafLight, LeafDark, Pine, PineDark, PineLight, Trunk;
        public Px Rock, RockLight, RockDark;
        public Px Sky, ParchA, ParchB, Ink;
        public Px Flower1, Flower2;

        static Px H(string s) => Px.Hex(s);

        public static readonly ArtPalette[] All =
        {
            new ArtPalette
            {
                Name = "Retro",
                Grass = H("#6FAE48"), Fertile = H("#86BD52"), ForestFloor = H("#4C8A3C"), PineFloor = H("#4B7A48"),
                Hills = H("#8DAE55"), Mountain = H("#9C9282"), Snow = H("#EDF2F6"), Desert = H("#E3C47E"), Steppe = H("#B9B465"), Beach = H("#EAD8A0"),
                Deep = H("#2F66A8"), Water = H("#3D80C4"), Shallow = H("#58A8D8"), Foam = H("#D6EEF8"), River = H("#4B97D4"), Bank = H("#7D6A47"),
                Cliff = H("#8A7258"), CliffDark = H("#5E4A38"),
                Leaf = H("#3E8C3A"), LeafLight = H("#6CBA4C"), LeafDark = H("#286128"), Pine = H("#2F6F4A"), PineDark = H("#1F4F36"), PineLight = H("#4C9161"), Trunk = H("#6E4628"),
                Rock = H("#A39B8E"), RockLight = H("#C9C2B5"), RockDark = H("#6F675C"),
                Sky = H("#2E4E7A"), ParchA = H("#E9DAB4"), ParchB = H("#DDCB9E"), Ink = H("#7A5A3A"),
                Flower1 = H("#F2E26A"), Flower2 = H("#F4F1EA"),
            },
            new ArtPalette
            {
                Name = "Herbst",
                Grass = H("#9AA64E"), Fertile = H("#B0B357"), ForestFloor = H("#7E7A3A"), PineFloor = H("#5E7346"),
                Hills = H("#A9A15A"), Mountain = H("#9A8C7C"), Snow = H("#EEF0F2"), Desert = H("#E3C47E"), Steppe = H("#C8B463"), Beach = H("#EAD8A0"),
                Deep = H("#2E5E92"), Water = H("#3A76AE"), Shallow = H("#5A9CC4"), Foam = H("#D6EAF2"), River = H("#4A8CC0"), Bank = H("#7D6447"),
                Cliff = H("#8A7258"), CliffDark = H("#5E4A38"),
                Leaf = H("#D0782C"), LeafLight = H("#EFA444"), LeafDark = H("#9C4A22"), Pine = H("#3A6646"), PineDark = H("#264A34"), PineLight = H("#55855E"), Trunk = H("#5E3A22"),
                Rock = H("#A39B8E"), RockLight = H("#C9C2B5"), RockDark = H("#6F675C"),
                Sky = H("#3A3550"), ParchA = H("#E9DAB4"), ParchB = H("#DDCB9E"), Ink = H("#7A5A3A"),
                Flower1 = H("#E86A3A"), Flower2 = H("#F2D25A"),
            },
            new ArtPalette
            {
                Name = "Terrakotta",
                Grass = H("#C0704F"), Fertile = H("#C97A57"), ForestFloor = H("#A85C44"), PineFloor = H("#A05640"),
                Hills = H("#CC8462"), Mountain = H("#D69A7C"), Snow = H("#F3E2C2"), Desert = H("#DDA07A"), Steppe = H("#C8805E"), Beach = H("#E3B48E"),
                Deep = H("#D9BC92"), Water = H("#E6CDA6"), Shallow = H("#EFDBB9"), Foam = H("#F8EBD2"), River = H("#EFDBB9"), Bank = H("#9C4D39"),
                Cliff = H("#9C4D39"), CliffDark = H("#7A3A2A"),
                Leaf = H("#8E4433"), LeafLight = H("#A85A44"), LeafDark = H("#6E3324"), Pine = H("#7E3B2C"), PineDark = H("#5E2A1E"), PineLight = H("#984C38"), Trunk = H("#5A271C"),
                Rock = H("#E0B79A"), RockLight = H("#F0D3B8"), RockDark = H("#B07A62"),
                Sky = H("#B05A44"), ParchA = H("#E3C59D"), ParchB = H("#D8B88E"), Ink = H("#8E4433"),
                Flower1 = H("#F3DFBC"), Flower2 = H("#E3C59D"),
            },
        };
    }
}
