namespace PixelKingdom
{
    /// <summary>Farbpaletten für Karte und Natur – wechseln automatisch mit der Jahreszeit (Frühling/Sommer, Herbst, Winter).</summary>
    public class ArtPalette
    {
        public string Name;
        public Px Grass, Fertile, ForestFloor, PineFloor, Hills, Mountain, Snow, Desert, Steppe, Beach;
        public Px Deep, Water, Shallow, Foam, River, Bank, Cliff, CliffDark;
        public Px Leaf, LeafLight, LeafDark, Pine, PineDark, PineLight, Trunk;
        public Px Rock, RockLight, RockDark;
        public Px MountA, MountB, MountC, MountD, CapGrey, CapSnow; // Gebirge: Mitte, Schatten, Licht, tiefster Schatten, Schneekappe
        public Px Sky, ParchA, ParchB, Ink;
        public Px Flower1, Flower2;

        static Px H(string s) => Px.Hex(s);

        public static readonly ArtPalette[] All =
        {
            new ArtPalette
            {
                Name = "Retro",
                Grass = H("#6FAE48"), Fertile = H("#86BD52"), ForestFloor = H("#4C8A3C"), PineFloor = H("#4B7A48"),
                Hills = H("#8DAE55"), Mountain = H("#5D8794"), Snow = H("#EDF2F6"), Desert = H("#E3C47E"), Steppe = H("#B9B465"), Beach = H("#EAD8A0"),
                Deep = H("#2F66A8"), Water = H("#3D80C4"), Shallow = H("#58A8D8"), Foam = H("#D6EEF8"), River = H("#4B97D4"), Bank = H("#7D6A47"),
                Cliff = H("#8A7258"), CliffDark = H("#5E4A38"),
                Leaf = H("#3E8C3A"), LeafLight = H("#6CBA4C"), LeafDark = H("#286128"), Pine = H("#2F6F4A"), PineDark = H("#1F4F36"), PineLight = H("#4C9161"), Trunk = H("#6E4628"),
                Rock = H("#A39B8E"), RockLight = H("#C9C2B5"), RockDark = H("#6F675C"),
                MountA = H("#2F7A92"), MountB = H("#1F4F60"), MountC = H("#4F9DB4"), MountD = H("#163844"), CapGrey = H("#A8B4BA"), CapSnow = H("#F4F8FA"),
                Sky = H("#2E4E7A"), ParchA = H("#E9DAB4"), ParchB = H("#DDCB9E"), Ink = H("#7A5A3A"),
                Flower1 = H("#F2E26A"), Flower2 = H("#F4F1EA"),
            },
            new ArtPalette
            {
                Name = "Herbst",
                Grass = H("#9AA64E"), Fertile = H("#B0B357"), ForestFloor = H("#7E7A3A"), PineFloor = H("#5E7346"),
                Hills = H("#A9A15A"), Mountain = H("#6A7F86"), Snow = H("#EEF0F2"), Desert = H("#E3C47E"), Steppe = H("#C8B463"), Beach = H("#EAD8A0"),
                Deep = H("#2E5E92"), Water = H("#3A76AE"), Shallow = H("#5A9CC4"), Foam = H("#D6EAF2"), River = H("#4A8CC0"), Bank = H("#7D6447"),
                Cliff = H("#8A7258"), CliffDark = H("#5E4A38"),
                Leaf = H("#D0782C"), LeafLight = H("#EFA444"), LeafDark = H("#9C4A22"), Pine = H("#3A6646"), PineDark = H("#264A34"), PineLight = H("#55855E"), Trunk = H("#5E3A22"),
                Rock = H("#A39B8E"), RockLight = H("#C9C2B5"), RockDark = H("#6F675C"),
                MountA = H("#4F6F7E"), MountB = H("#34505E"), MountC = H("#6E8E9C"), MountD = H("#22383F"), CapGrey = H("#A8B0B4"), CapSnow = H("#F0F3F5"),
                Sky = H("#3A3550"), ParchA = H("#E9DAB4"), ParchB = H("#DDCB9E"), Ink = H("#7A5A3A"),
                Flower1 = H("#E86A3A"), Flower2 = H("#F2D25A"),
            },
            new ArtPalette
            {
                Name = "Winter",
                Grass = H("#E2EAEE"), Fertile = H("#DAE4E9"), ForestFloor = H("#C8D5DC"), PineFloor = H("#C0CED5"),
                Hills = H("#D2DDE3"), Mountain = H("#B8C5CD"), Snow = H("#F6FAFC"), Desert = H("#E6D4A8"), Steppe = H("#D6DCD0"), Beach = H("#E0DAC8"),
                Deep = H("#2A5578"), Water = H("#3A6E94"), Shallow = H("#8FBBD2"), Foam = H("#F2F8FB"), River = H("#A6CBE0"), Bank = H("#8A9AA4"),
                Cliff = H("#7E8C94"), CliffDark = H("#56636B"),
                Leaf = H("#4E6A5C"), LeafLight = H("#EEF4F7"), LeafDark = H("#34493E"), Pine = H("#2F5A48"), PineDark = H("#1F4034"), PineLight = H("#E8F0F4"), Trunk = H("#5A4030"),
                Rock = H("#A8B0B6"), RockLight = H("#D0D8DC"), RockDark = H("#6E787E"),
                MountA = H("#6E93A6"), MountB = H("#4C6E80"), MountC = H("#96B8C8"), MountD = H("#33505E"), CapGrey = H("#C4CED4"), CapSnow = H("#FAFCFD"),
                Sky = H("#3A4A66"), ParchA = H("#E9DAB4"), ParchB = H("#DDCB9E"), Ink = H("#7A5A3A"),
                Flower1 = H("#FFFFFF"), Flower2 = H("#E2EAEE"),
            },
        };
    }
}
