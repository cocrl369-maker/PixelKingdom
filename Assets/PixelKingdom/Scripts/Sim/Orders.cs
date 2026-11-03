using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>Ein möglicher Befehl für ein Heer an einem Zielort (für das Befehlsmenü).</summary>
    public class OrderOption
    {
        public string Label, Tip, Icon;
        public ArmyMission Mission;
        public int Target = -1, X, Y;
        public bool Enabled = true, NeedsWar;
        public int Kind;   // 0 marschieren, 1 erobern, 2 überfallen, 3 einrücken, 4 Ort besuchen, 5 gründen
    }

    /// <summary>
    /// Befehlsmenü: Heer wählen, Ziel antippen – dann zeigt das Spiel Weg, Marschdauer und alle möglichen
    /// Befehle zur Auswahl. Erst die Auswahl gibt den Befehl. So geht nichts versehentlich los.
    /// </summary>
    public partial class Game
    {
        /// <summary>Weg und Marschdauer (in Tagen) für ein Heer zu einem Ort. Null, wenn unerreichbar.</summary>
        public List<int> PreviewPath(ArmyData a, int tx, int ty, out float days)
        {
            days = 0;
            var path = Pathfinder.Find(Map, (int)Math.Floor(a.x), (int)Math.Floor(a.y), tx, ty, CanSail(a.faction));
            if (path == null) return null;
            float cost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                float c = Map.MoveCost(path[i], true);
                if (c == float.MaxValue) c = 1f;
                int dx = WorldMap.X(path[i]) - WorldMap.X(path[i - 1]), dy = WorldMap.Y(path[i]) - WorldMap.Y(path[i - 1]);
                cost += c * (dx != 0 && dy != 0 ? 1.414f : 1f);
            }
            days = cost / Math.Max(0.1f, ArmySpeed(a));
            return path;
        }

        public List<OrderOption> OrderOptions(ArmyData a, int tx, int ty, SettlementData target, PoiData poi)
        {
            var list = new List<OrderOption>();
            if (a.settlers > 0 && a.units.Sum() == 0)
            {
                bool can = CanFoundAt(a.faction, tx, ty, out string why);
                list.Add(new OrderOption { Kind = 5, Label = Loc.T("Hier eine Siedlung gründen"), Icon = "cart", Mission = ArmyMission.Found, X = tx, Y = ty, Enabled = can, Tip = can ? Loc.T("Die Siedler ziehen hin und gründen einen neuen Ort.") : why });
                list.Add(new OrderOption { Kind = 0, Label = Loc.T("Nur hinziehen"), Icon = "arrow", Mission = ArmyMission.Move, X = tx, Y = ty });
                return list;
            }
            if (target != null)
            {
                if (target.faction == a.faction)
                {
                    list.Add(new OrderOption { Kind = 3, Label = Loc.F("In {0} einrücken", (target.name)), Icon = "house", Mission = ArmyMission.Return, Target = target.id, X = target.x, Y = target.y, Tip = Loc.T("Die Truppen gehen in die Garnison und verteidigen die Stadt.") });
                    list.Add(new OrderOption { Kind = 0, Label = Loc.T("Vor der Stadt lagern"), Icon = "flag", Mission = ArmyMission.Move, X = tx, Y = ty, Tip = Loc.T("Das Heer bleibt als eigenes Heer stehen.") });
                    return list;
                }
                var f = F(target.faction);
                bool war = AtWar(a.faction, target.faction);
                float mine = Strength(a.units), theirs = DefenseStrength(target);
                string odds = mine > theirs * 1.4f ? Loc.T("gute Aussichten") : mine > theirs ? "knapp" : Loc.T("das Heer ist zu schwach");
                string warNote = war ? "" : Loc.F(" Dafür erklärt ihr {0} den Krieg.", (f.name));
                list.Add(new OrderOption { Kind = 1, Label = Loc.F("{0} erobern", (target.name)), Icon = "sword", Mission = ArmyMission.Attack, Target = target.id, X = target.x, Y = target.y, NeedsWar = !war,
                    Tip = Loc.F("Die Stadt einnehmen. Stärke {0} gegen ~{1} ({2}).{3}", ((int)mine), ((int)theirs), (odds), (warNote)) });
                list.Add(new OrderOption { Kind = 2, Label = Loc.F("{0} überfallen", (target.name)), Icon = "fire", Mission = ArmyMission.Raid, Target = target.id, X = target.x, Y = target.y, NeedsWar = !war,
                    Tip = Loc.F("Nur plündern und heimkehren – Mauern zählen halb.{0}", (warNote)) });
                list.Add(new OrderOption { Kind = 0, Label = Loc.T("Nur in die Nähe ziehen"), Icon = "arrow", Mission = ArmyMission.Move, X = tx, Y = ty, Tip = Loc.T("Marschieren ohne Angriff.") });
                return list;
            }
            if (poi != null)
            {
                string l = poi.type == 0 ? (poi.done ? Loc.T("Zur Ruine ziehen") : Loc.T("Ruine erkunden")) : poi.type == 1 ? Loc.T("Räuberlager angreifen") : Loc.T("Fahrendes Volk besuchen");
                string tip = poi.type == 1 ? Loc.F("Kampf gegen etwa {0} Räuber. Beute: Gold und Waffen.", (Math.Max(1, (int)Math.Round(poi.guards.Sum() / 5f) * 5))) : poi.type == 0 ? Loc.T("Gold, Waffen, Relikte oder Karten – manchmal eine Falle.") : Loc.T("Handeln oder sie einladen, zu bleiben.");
                list.Add(new OrderOption { Kind = 4, Label = l, Icon = poi.type == 1 ? "sword" : "eye", Mission = ArmyMission.Poi, Target = poi.id, X = poi.x, Y = poi.y, Tip = tip });
                list.Add(new OrderOption { Kind = 0, Label = Loc.T("Nur hinziehen"), Icon = "arrow", Mission = ArmyMission.Move, X = tx, Y = ty });
                return list;
            }
            list.Add(new OrderOption { Kind = 0, Label = Loc.T("Hierher marschieren"), Icon = "arrow", Mission = ArmyMission.Move, X = tx, Y = ty, Tip = Loc.T("Das Heer zieht an diesen Ort und wartet dort.") });
            return list;
        }
    }
}
