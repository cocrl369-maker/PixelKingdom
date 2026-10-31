using System;

namespace PixelKingdom
{
    // ------------------------------------------------------------------
    //  Ressourcen
    // ------------------------------------------------------------------
    public enum Res { Food = 0, Wood = 1, Stone = 2, Iron = 3, Gold = 4, Weapons = 5 }

    public static class ResInfo
    {
        public const int Count = 6;
        public static readonly string[] Names = { "Nahrung", "Holz", "Stein", "Eisen", "Gold", "Waffen" };
        public static readonly string[] Icons = { "food", "wood", "stone", "iron", "gold", "weapons" };
        // Grundpreis am Markt (in Gold)
        public static readonly float[] BasePrice = { 0.8f, 1.0f, 1.4f, 3.0f, 1f, 5.0f };
        public static string Name(int i) => Names[i];
    }

    // ------------------------------------------------------------------
    //  Rangstufen des Reiches
    // ------------------------------------------------------------------
    public enum Rank { Dorf = 0, Gemeinde = 1, Grafschaft = 2, Fuerstentum = 3, Koenigreich = 4, Kaiserreich = 5 }

    public class RankDef
    {
        public string Name, Ruler, Desc;
        public int Pop, Settlements, MaxSettlements;
        public float Prestige, GoldCost;
        public BType RequiredBuilding = BType.None;
        public int StoryChapter; // erforderliches Story-Kapitel (abgeschlossen)
        public float ProductionBonus;
    }

    public static class Ranks
    {
        public const int Count = 6;
        public static readonly RankDef[] All =
        {
            new RankDef { Name = "Dorf", Ruler = "Dorfvorsteher", Desc = "Ein kleines Dorf am Rande der Welt.",
                Pop = 0, Settlements = 1, MaxSettlements = 1, Prestige = 0, GoldCost = 0, ProductionBonus = 0f },
            new RankDef { Name = "Städtegemeinde", Ruler = "Bürgermeister", Desc = "Mehrere Höfe, ein Markt, erste Bürger.",
                Pop = 45, Settlements = 1, MaxSettlements = 3, Prestige = 12, GoldCost = 60, ProductionBonus = 0.05f },
            new RankDef { Name = "Grafschaft", Ruler = "Graf", Desc = "Ein befestigtes Land mit mehreren Orten.",
                Pop = 140, Settlements = 2, MaxSettlements = 5, Prestige = 60, GoldCost = 200, RequiredBuilding = BType.Mauer, ProductionBonus = 0.10f },
            new RankDef { Name = "Fürstentum", Ruler = "Fürst", Desc = "Burgen, Ritter und ein eigener Hof.",
                Pop = 320, Settlements = 4, MaxSettlements = 8, Prestige = 160, GoldCost = 500, RequiredBuilding = BType.Burg, StoryChapter = 3, ProductionBonus = 0.15f },
            new RankDef { Name = "Königreich", Ruler = "König", Desc = "Eine Krone, viele Vasallen.",
                Pop = 650, Settlements = 7, MaxSettlements = 14, Prestige = 380, GoldCost = 1200, RequiredBuilding = BType.Kathedrale, StoryChapter = 4, ProductionBonus = 0.22f },
            new RankDef { Name = "Kaiserreich", Ruler = "Kaiser", Desc = "Der Tyrann ist gestürzt. Die Welt steht offen.",
                Pop = 1000, Settlements = 10, MaxSettlements = 99, Prestige = 700, GoldCost = 2500, StoryChapter = 7, ProductionBonus = 0.30f },
        };
        public static RankDef Get(int r) => All[Math.Max(0, Math.Min(Count - 1, r))];
    }

    // ------------------------------------------------------------------
    //  Gebäude
    // ------------------------------------------------------------------
    public enum BType
    {
        None = -1,
        Wohnhaus, Bauernhof, Fischerhuette, Holzfaeller, Steinbruch, Mine, Lagerhaus, Markt, Taverne,
        Kapelle, Brunnen, Schmiede, Kaserne, Archerie, Stall, Mauer, Burg, Hafen, Kathedrale, Werkstatt,
    }

    public enum TerrainNeed { None, Coast, Forest, Hills, Mountain, Fertile }

    public class BuildingDef
    {
        public BType Type;
        public string Name, Desc, Icon;
        public float[] Cost = new float[ResInfo.Count];
        public float[] Prod = new float[ResInfo.Count];   // pro Tag, voll besetzt
        public float[] Use = new float[ResInfo.Count];    // Verbrauch pro Tag
        public int Days = 3;
        public int Max = 99;           // max Anzahl pro Siedlung
        public int MinRank;
        public int Jobs;
        public int Housing;
        public float Happiness;
        public float Defense;
        public int Storage;
        public float Prestige;
        public TerrainNeed Need = TerrainNeed.None;
        public bool FoodBuilding;
    }

    public static class Buildings
    {
        public static readonly int Count = Enum.GetValues(typeof(BType)).Length - 1;
        public static readonly BuildingDef[] All;

        static float[] R(float food = 0, float wood = 0, float stone = 0, float iron = 0, float gold = 0, float weapons = 0)
            => new[] { food, wood, stone, iron, gold, weapons };

        static Buildings()
        {
            All = new BuildingDef[Count];
            void Add(BuildingDef d) { All[(int)d.Type] = d; }

            Add(new BuildingDef { Type = BType.Wohnhaus, Name = "Wohnhaus", Icon = "house", Desc = "Platz für 8 Bewohner.",
                Cost = R(wood: 24), Days = 2, Housing = 8, Max = 30 });
            Add(new BuildingDef { Type = BType.Bauernhof, Name = "Bauernhof", Icon = "farm", Desc = "Felder und Vieh. Braucht fruchtbares Land.",
                Cost = R(wood: 25), Days = 3, Jobs = 4, Prod = R(food: 4.2f), Need = TerrainNeed.Fertile, FoodBuilding = true, Max = 12 });
            Add(new BuildingDef { Type = BType.Fischerhuette, Name = "Fischerhütte", Icon = "fish", Desc = "Fisch aus Meer, See oder Fluss.",
                Cost = R(wood: 18), Days = 2, Jobs = 2, Prod = R(food: 3.0f), Need = TerrainNeed.Coast, FoodBuilding = true, Max = 6 });
            Add(new BuildingDef { Type = BType.Holzfaeller, Name = "Holzfäller", Icon = "axe", Desc = "Schlägt Holz im nahen Wald.",
                Cost = R(wood: 10, gold: 5), Days = 2, Jobs = 3, Prod = R(wood: 3.2f), Need = TerrainNeed.Forest, Max = 8 });
            Add(new BuildingDef { Type = BType.Steinbruch, Name = "Steinbruch", Icon = "pick", Desc = "Bricht Stein aus Hügeln und Bergen.",
                Cost = R(wood: 25, gold: 10), Days = 3, Jobs = 4, Prod = R(stone: 2.4f), Need = TerrainNeed.Hills, Max = 6 });
            Add(new BuildingDef { Type = BType.Mine, Name = "Eisenmine", Icon = "mine", Desc = "Fördert Eisenerz im Gebirge.",
                Cost = R(wood: 40, stone: 20, gold: 30), Days = 5, Jobs = 5, Prod = R(iron: 1.6f), Need = TerrainNeed.Mountain, MinRank = 1, Max = 5 });
            Add(new BuildingDef { Type = BType.Lagerhaus, Name = "Lagerhaus", Icon = "crate", Desc = "+250 Lagerplatz für Waren.",
                Cost = R(wood: 40, stone: 10), Days = 3, Storage = 250, Max = 6 });
            Add(new BuildingDef { Type = BType.Markt, Name = "Markt", Icon = "market", Desc = "Handel, Gold und gute Laune. Erlaubt Kauf/Verkauf.",
                Cost = R(wood: 50, stone: 20, gold: 20), Days = 4, Jobs = 3, Prod = R(gold: 3.0f), Happiness = 3, Prestige = 0.1f, MinRank = 1, Max = 2 });
            Add(new BuildingDef { Type = BType.Taverne, Name = "Taverne", Icon = "tavern", Desc = "Bier und Geschichten. Bewohner sind zufriedener.",
                Cost = R(wood: 35, stone: 5, gold: 15), Days = 3, Jobs = 2, Prod = R(gold: 1.0f), Use = R(food: 1.0f), Happiness = 7, Max = 3 });
            Add(new BuildingDef { Type = BType.Kapelle, Name = "Kapelle", Icon = "chapel", Desc = "Trost und Ansehen.",
                Cost = R(wood: 30, stone: 30, gold: 20), Days = 5, Jobs = 1, Happiness = 6, Prestige = 0.35f, Max = 2 });
            Add(new BuildingDef { Type = BType.Brunnen, Name = "Brunnen", Icon = "well", Desc = "Sauberes Wasser: Wachstum, Gesundheit.",
                Cost = R(wood: 10, stone: 25), Days = 2, Happiness = 3, Max = 3 });
            Add(new BuildingDef { Type = BType.Schmiede, Name = "Schmiede", Icon = "anvil", Desc = "Fertigt Waffen aus Eisen und Holz.",
                Cost = R(wood: 40, stone: 30, gold: 25), Days = 4, Jobs = 3, Prod = R(weapons: 1.2f), Use = R(wood: 0.8f, iron: 1.0f), MinRank = 1, Max = 4 });
            Add(new BuildingDef { Type = BType.Kaserne, Name = "Kaserne", Icon = "sword", Desc = "Bildet Speerträger aus.",
                Cost = R(wood: 50, stone: 30, gold: 30), Days = 4, Defense = 0.05f, Max = 1 });
            Add(new BuildingDef { Type = BType.Archerie, Name = "Archerie", Icon = "bow", Desc = "Bildet Bogenschützen aus.",
                Cost = R(wood: 60, stone: 10, gold: 25), Days = 4, Defense = 0.05f, Max = 1 });
            Add(new BuildingDef { Type = BType.Stall, Name = "Stall", Icon = "horse", Desc = "Bildet Ritter aus.",
                Cost = R(food: 40, wood: 70, stone: 20, gold: 60), Days = 5, MinRank = 2, Max = 1 });
            Add(new BuildingDef { Type = BType.Mauer, Name = "Palisade & Mauer", Icon = "wall", Desc = "+40% Verteidigung pro Ausbaustufe.",
                Cost = R(wood: 60, stone: 60), Days = 6, Defense = 0.4f, MinRank = 1, Max = 2 });
            Add(new BuildingDef { Type = BType.Burg, Name = "Burg", Icon = "castle", Desc = "+80% Verteidigung, viel Ansehen.",
                Cost = R(wood: 120, stone: 250, iron: 20, gold: 150), Days = 14, Defense = 0.8f, Prestige = 1.2f, Happiness = 2, MinRank = 2, Max = 1 });
            Add(new BuildingDef { Type = BType.Hafen, Name = "Hafen", Icon = "anchor", Desc = "Handel über das Meer. Später: Schiffe für Übersee.",
                Cost = R(wood: 120, stone: 40, gold: 60), Days = 8, Jobs = 4, Prod = R(gold: 3.5f, food: 1.0f), Need = TerrainNeed.Coast, MinRank = 2, Max = 1, Prestige = 0.2f });
            Add(new BuildingDef { Type = BType.Kathedrale, Name = "Kathedrale", Icon = "cathedral", Desc = "Ein Wunder aus Stein. Großes Ansehen.",
                Cost = R(wood: 150, stone: 400, iron: 30, gold: 300), Days = 24, Jobs = 2, Happiness = 14, Prestige = 3.0f, MinRank = 3, Max = 1 });
            Add(new BuildingDef { Type = BType.Werkstatt, Name = "Belagerungswerkstatt", Icon = "ram", Desc = "Baut Rammen gegen Mauern.",
                Cost = R(wood: 100, stone: 30, iron: 15, gold: 60), Days = 6, MinRank = 3, Max = 1 });
        }

        public static BuildingDef Get(BType t) => All[(int)t];
        public static BuildingDef Get(int t) => All[t];
    }

    // ------------------------------------------------------------------
    //  Einheiten
    // ------------------------------------------------------------------
    public enum UType { Miliz = 0, Speer = 1, Bogen = 2, Ritter = 3, Ramme = 4, Spaeher = 5 }

    public class UnitDef
    {
        public UType Type;
        public string Name, Plural, Icon, Desc;
        public float Atk, Def, Hp, Speed;
        public float[] Cost = new float[ResInfo.Count];
        public float UpkeepGold, UpkeepFood;
        public int Days;
        public BType Needs = BType.None;
        public int MinRank;
        public int StrongVs = -1;
        public float StrongMult = 1f;
        public bool Ranged;
        public float Siege;
        public int Vision = 4;
    }

    public static class Units
    {
        public const int Count = 6;
        public static readonly UnitDef[] All =
        {
            new UnitDef { Type = UType.Miliz, Name = "Miliz", Plural = "Milizen", Icon = "militia", Desc = "Bauern mit Mistgabeln. Billig, schwach.",
                Atk = 2, Def = 1, Hp = 10, Speed = 3f, Cost = new float[]{ 4, 0, 0, 0, 2, 0 }, UpkeepGold = 0.03f, UpkeepFood = 0.12f, Days = 1 },
            new UnitDef { Type = UType.Speer, Name = "Speerträger", Plural = "Speerträger", Icon = "spear", Desc = "Solide Linie. Stark gegen Ritter.",
                Atk = 4, Def = 5, Hp = 14, Speed = 3f, Cost = new float[]{ 5, 2, 0, 0, 6, 1 }, UpkeepGold = 0.08f, UpkeepFood = 0.15f, Days = 2,
                Needs = BType.Kaserne, StrongVs = (int)UType.Ritter, StrongMult = 2.2f },
            new UnitDef { Type = UType.Bogen, Name = "Bogenschütze", Plural = "Bogenschützen", Icon = "bow", Desc = "Schießt zuerst. Stark gegen Speerträger.",
                Atk = 5, Def = 2, Hp = 10, Speed = 3f, Cost = new float[]{ 5, 5, 0, 0, 8, 1 }, UpkeepGold = 0.08f, UpkeepFood = 0.15f, Days = 2,
                Needs = BType.Archerie, StrongVs = (int)UType.Speer, StrongMult = 1.6f, Ranged = true },
            new UnitDef { Type = UType.Ritter, Name = "Ritter", Plural = "Ritter", Icon = "knight", Desc = "Schwere Reiterei. Zerschlägt Bogenschützen.",
                Atk = 9, Def = 7, Hp = 24, Speed = 5f, Cost = new float[]{ 10, 0, 0, 2, 25, 3 }, UpkeepGold = 0.25f, UpkeepFood = 0.3f, Days = 4,
                Needs = BType.Stall, MinRank = 2, StrongVs = (int)UType.Bogen, StrongMult = 2.2f },
            new UnitDef { Type = UType.Ramme, Name = "Ramme", Plural = "Rammen", Icon = "ram", Desc = "Bricht Mauern. Im Feld nutzlos.",
                Atk = 1, Def = 4, Hp = 22, Speed = 2f, Cost = new float[]{ 0, 30, 0, 4, 15, 0 }, UpkeepGold = 0.1f, UpkeepFood = 0.1f, Days = 4,
                Needs = BType.Werkstatt, MinRank = 3, Siege = 0.3f },
            new UnitDef { Type = UType.Spaeher, Name = "Späher", Plural = "Späher", Icon = "eye", Desc = "Schnell, sieht weit. Erkundet die Welt.",
                Atk = 1, Def = 1, Hp = 6, Speed = 7f, Cost = new float[]{ 3, 0, 0, 0, 4, 0 }, UpkeepGold = 0.04f, UpkeepFood = 0.08f, Days = 1, Vision = 9 },
        };
        public static UnitDef Get(int t) => All[t];
        public static UnitDef Get(UType t) => All[(int)t];
    }

    // ------------------------------------------------------------------
    //  Diplomatie
    // ------------------------------------------------------------------
    public enum DipStatus { Neutral = 0, War = 1, Truce = 2, Pact = 3, Alliance = 4 }

    public static class DipInfo
    {
        public static readonly string[] Names = { "Neutral", "Krieg", "Waffenruhe", "Nichtangriff", "Bündnis" };
    }

    public enum ArmyMission { Idle = 0, Move = 1, Attack = 2, Raid = 3, Return = 4, Found = 5, Explore = 6 }

    public enum Season { Fruehling, Sommer, Herbst, Winter }

    public static class Calendar
    {
        public const int DaysPerSeason = 30;
        public const int DaysPerYear = DaysPerSeason * 4;
        public static readonly string[] SeasonNames = { "Frühling", "Sommer", "Herbst", "Winter" };
        public static readonly float[] FoodMod = { 0.9f, 1.25f, 1.1f, 0.45f };
        public static Season SeasonOf(int day) => (Season)((day / DaysPerSeason) % 4);
        public static int Year(int day) => 1 + day / DaysPerYear;
        public static int DayOfSeason(int day) => 1 + day % DaysPerSeason;
    }
}
