using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>Ein Erfolg: Name, Beschreibung und Bedingung.</summary>
    public class AchievementDef
    {
        public string Id, Name, Desc, Icon;
        public Func<Game, bool> Check;
    }

    /// <summary>Statistik (Verlauf über die Zeit) und Erfolge.</summary>
    public partial class Game
    {
        public static readonly AchievementDef[] Achievements =
        {
            A("houses", "Grundstein", "Habt 5 Wohnhäuser.", "house", g => g.CountBuildings(BType.Wohnhaus) >= 5),
            A("township", "Bürgersinn", "Steigt zur Städtegemeinde auf.", "crown", g => g.Player.rank >= 1),
            A("county", "Graf und Herr", "Steigt zur Grafschaft auf.", "crown", g => g.Player.rank >= 2),
            A("kingdom", "Krone des Volkes", "Steigt zum Königreich auf.", "crown", g => g.Player.rank >= 4),
            A("pop500", "Volksreich", "500 Bewohner in eurem Reich.", "people", g => g.TotalPop(g.PlayerId) >= 500),
            A("pop2000", "Großreich", "2000 Bewohner in eurem Reich.", "people", g => g.TotalPop(g.PlayerId) >= 2000),
            A("settle5", "Siedlerherz", "Herrscht über 5 Siedlungen.", "cart", g => g.CountSettlements(g.PlayerId) >= 5),
            A("win1", "Erster Sieg", "Gewinnt eine Schlacht.", "sword", g => g.S.battlesWon >= 1),
            A("win20", "Feldherr", "Gewinnt 20 Schlachten.", "sword", g => g.S.battlesWon >= 20),
            A("camps", "Räuberschreck", "Hebt 5 Räuberlager aus.", "skull", g => g.S.pois.Count(p => p.type == 1 && p.done) >= 5),
            A("tech1", "Gelehrt", "Erforscht euer erstes Wissen.", "book", g => g.S.techs.Count >= 1),
            A("techall", "Universalgelehrt", "Erforscht alles Wissen.", "book", g => g.S.techs.Count >= Techs.Count),
            A("allies3", "Diplomat", "Habt 3 Verbündete zugleich.", "handshake", g => g.AlliesOf(g.PlayerId).Count() >= 3),
            A("gold5000", "Schatzkammer", "Habt 5000 Gold auf einmal.", "gold", g => g.Player.res[(int)Res.Gold] >= 5000),
            A("fleet", "Admiral", "Habt 5 Kriegsschiffe.", "anchor", g => g.S.warShips >= 5),
            A("island", "Land in Sicht", "Besitzt eine Siedlung auf einer der großen Inseln.", "anchor", g => g.SettlementsOf(g.PlayerId).Any(s => g.Map.cont[WorldMap.Idx(s.x, s.y)] >= 4)),
            A("riddles", "Rätselmeister", "Löst drei Rätsel der Steintafeln.", "book", g => g.S.runesSolved >= 3),
            A("dragon", "Drachentöter", "Erobert den Drachenhort.", "fire", g => g.S.story.Has("dragon_crown")),
            A("stars", "Sternenkind", "Löst die Rätsel der Smaragdinsel und betretet den Sternenschrein.", "sun", g => g.S.story.Has("star_blessing")),
            A("relics", "Reliquiensammler", "Besitzt 3 Reliquien.", "chapel", g => g.S.relics >= 3),
            A("tourney", "Lanzenbrecher", "Richtet ein Ritterturnier aus.", "flag", g => g.S.lastTournamentDay > 0),
            A("emperor", "Tyrannensturz", "Stürzt Kaiser Malrik.", "eagle", g => g.S.story.emperorDefeated),
        };

        static AchievementDef A(string id, string name, string desc, string icon, Func<Game, bool> check) =>
            new AchievementDef { Id = id, Name = name, Desc = desc, Icon = icon, Check = check };

        public bool HasAchievement(string id) => S.achievements.Contains(id);

        void ProgressDay()
        {
            var p = Player;
            if (p == null || !p.alive) return;
            // Verlauf alle 10 Tage festhalten (bei langen Spielen wird ausgedünnt)
            if (S.day % 10 == 0)
            {
                S.statDay.Add(S.day); S.statPop.Add((int)TotalPop(p.id)); S.statGold.Add((int)p.res[(int)Res.Gold]);
                S.statSoldiers.Add(TotalSoldiers(p.id)); S.statPrestige.Add((int)p.prestige);
                if (S.statDay.Count > 240)
                    foreach (var l in new[] { S.statDay, S.statPop, S.statGold, S.statSoldiers, S.statPrestige })
                        for (int i = l.Count - 2; i >= 1; i -= 2) l.RemoveAt(i);
            }
            if (S.day % 2 != 0) return;
            foreach (var a in Achievements)
            {
                if (S.achievements.Contains(a.Id)) continue;
                bool ok;
                try { ok = a.Check(this); } catch { ok = false; }
                if (!ok) continue;
                S.achievements.Add(a.Id);
                p.prestige += 5;
                Notify(Loc.F("Erfolg: {0}! (+5 Ansehen)", Loc.T(a.Name)), 1);
                Hint("achievement", Loc.T("Euer erster Erfolg! Alle Erfolge findet ihr in der Reich-Übersicht (R) unter 'Erfolge'. Jeder bringt ein wenig Ansehen."));
            }
        }
    }
}
