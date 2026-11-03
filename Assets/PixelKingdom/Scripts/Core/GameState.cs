using System;
using System.Collections.Generic;

namespace PixelKingdom
{
    // Alle Klassen hier sind per JsonUtility serialisierbar (keine Dictionaries, keine Polymorphie).

    [Serializable]
    public class GameState
    {
        public const int CurrentVersion = 4;   // 4 = Gebäude mit Bauplatz, Wissen, Fahrendes Volk
        public int version = CurrentVersion;
        public int seed;
        public int day;
        public float dayProgress;
        public int speed = 1;
        public int playerFaction;
        public int nextId = 1;
        public long lastSaveTicks;
        public int palette;
        public bool seafaring;
        public int battlesWon;
        public int battlesLost;
        public int nextEventDay = 12;
        public int nextTaxDay = 40;
        public float[] priceDrift = new float[ResInfo.Count];
        public string discovered = "";
        public string trees = "";                       // Baumbestand (Abholzung)
        public List<int> extraDeposits = new List<int>(); // Paare: Kachel, Art

        public List<FactionData> factions = new List<FactionData>();
        public List<SettlementData> settlements = new List<SettlementData>();
        public List<ArmyData> armies = new List<ArmyData>();
        public List<LogEntry> log = new List<LogEntry>();
        public List<PendingEvent> events = new List<PendingEvent>();
        public List<PoiData> pois = new List<PoiData>();
        public List<int> edicts = new List<int>();
        public int lastTournamentDay = -999;
        public List<int> techs = new List<int>();
        public int researching = -1;
        public float researchLeft;
        public int relics;
        public StoryData story = new StoryData();
    }

    [Serializable]
    public class FactionData
    {
        public int id;
        public string name;
        public string leader;
        public int color;
        public bool isPlayer;
        public bool isEmperor;
        public bool isFree;       // freies Dorf ohne Fürst
        public bool isRebel;
        public bool alive = true;
        public int continent;
        public int culture;       // 0 = Heimat, 1 = Nordlande, 2 = Südreich, 3 = Inselbund
        public int rank;
        public float prestige;
        public float[] res = new float[ResInfo.Count];
        public float aggression, greed, honor, ambition, loyalty; // 0..1
        public float warWeariness;
        public int capital = -1;
        public int claim = -1;     // begehrte Siedlung (Interessenkonflikt)
        public int knownToPlayer;  // 0/1
        public int lastAIDay;
        public List<RelationData> relations = new List<RelationData>();
    }

    [Serializable]
    public class RelationData
    {
        public int other;
        public float value;
        public int status;   // DipStatus
        public int since;    // Tag der letzten Änderung
        public bool trade;
    }

    [Serializable]
    public class SettlementData
    {
        public int id;
        public string name;
        public int faction;
        public int x, y;
        public float pop = 20;
        public float happiness = 55;
        public int[] buildings = new int[Buildings.Count];
        public int[] garrison = new int[Units.Count];
        public List<QueueItem> queue = new List<QueueItem>();
        public List<BuildingPos> placed = new List<BuildingPos>();   // wo jedes Gebäude steht
        public float taxRate = 0.1f;
        public int rations = 1;
        public bool auto;
        public int founded;
        public int conqueredDay = -999;
        public int lastBattleDay = -999;
        public int originalFaction = -1;
        public bool isCapitalOfEmperor;
    }

    [Serializable]
    public class QueueItem
    {
        public int kind;   // 0 = Gebäude, 1 = Einheiten
        public int type;
        public int count = 1;
        public float daysLeft;
        public float total;
        public int x = -1, y = -1;   // Bauplatz (Gebäude)
    }

    [Serializable]
    public class BuildingPos
    {
        public int type, x, y;
        public BuildingPos() { }
        public BuildingPos(int t, int x, int y) { type = t; this.x = x; this.y = y; }
    }

    [Serializable]
    public class ArmyData
    {
        public int id;
        public int faction;
        public string name;
        public float x, y;
        public int[] units = new int[Units.Count];
        public int settlers;
        public List<int> path = new List<int>();
        public int pathIndex;
        public int mission;      // ArmyMission
        public int target = -1;  // Siedlungs-Id
        public int targetX = -1, targetY = -1;
        public float morale = 1f;
        public int home = -1;
        public bool atSea;
    }

    /// <summary>Ort auf der Karte: 0 = Ruine, 1 = Räuberlager.</summary>
    [Serializable]
    public class PoiData
    {
        public int id, type, x, y, seed;
        public bool done;
        public int[] guards = new int[Units.Count];
        public int nextRaid;
    }

    [Serializable]
    public class LogEntry
    {
        public int day;
        public string text;
        public int kind; // 0 info, 1 gut, 2 schlecht, 3 krieg, 4 story
    }

    [Serializable]
    public class PendingEvent
    {
        public string id;
        public int a = -1, b = -1, c = -1;
        public float v;
    }

    [Serializable]
    public class StoryData
    {
        public int chapter;
        public bool chapterIntroShown;
        public bool emperorDefeated;
        public int emperorWarDay = -1;
        public float pressure;          // Druck des Kaiserreichs 0..100
        public int peaceUntil;          // bis zu diesem Tag greift niemand den Spieler an
        public int nextPunish;          // nächste mögliche Strafexpedition
        public int loyalistsId = -1;    // Fraktion "Malriks Getreue"
        public int exileId = -1;        // Siedlung "Malriks Exilfestung"
        public List<string> flags = new List<string>();
        public bool Has(string f) => flags.Contains(f);
        public void Set(string f) { if (!flags.Contains(f)) flags.Add(f); }
    }
}
