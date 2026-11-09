using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>
    /// Mythisches: alte Steintafeln mit Rätseln, Heilige Quellen und der verborgene Drachenhort,
    /// dessen Lage erst drei gelöste Rätsel verraten. Dazu seltsame Ereignisse (Irrlichter, Komet).
    /// POI-Typen: 3 Steintafel, 4 Drachenhort, 5 Heilige Quelle.
    /// </summary>
    public partial class Game
    {
        public const int PoiTablet = 3, PoiHoard = 4, PoiSpring = 5;
        public const int RunesForHoard = 3;

        // Rätsel: Frage, richtige Antwort, zwei falsche
        static readonly string[][] Riddles =
        {
            new[] { "Je mehr du davon machst, desto mehr lässt du hinter dir. Was ist es?", "Schritte", "Lieder", "Brote" },
            new[] { "Ich habe Städte, doch keine Häuser, Wälder, doch keine Bäume, Flüsse, doch kein Wasser. Was bin ich?", "Eine Landkarte", "Ein Traum", "Ein Grab" },
            new[] { "Ich spreche ohne Mund und antworte, ohne zu hören. Die Berge sind mein Zuhause. Was bin ich?", "Das Echo", "Der Wind", "Ein Geist" },
            new[] { "Was wird umso größer, je mehr man davon wegnimmt?", "Ein Loch", "Ein Berg", "Ein Feuer" },
            new[] { "Am Morgen geht es auf vier Beinen, am Mittag auf zwei, am Abend auf drei. Was ist es?", "Der Mensch", "Das Pferd", "Der Drache" },
            new[] { "Ich bin leichter als eine Feder, doch selbst der stärkste Mann hält mich nicht lange fest. Was bin ich?", "Der Atem", "Ein Gedanke", "Der Rauch" },
            new[] { "Ich fresse alles, was man mir gibt – doch gibst du mir zu trinken, so sterbe ich. Was bin ich?", "Das Feuer", "Die Zeit", "Der Wolf" },
        };

        public static string PoiActionLabel(PoiData p) =>
            p.type == PoiTablet ? (p.done ? Loc.T("Zur Steintafel ziehen") : Loc.T("Die Steintafel lesen")) : p.type == PoiHoard ? (p.done ? Loc.T("Zum Drachenhort ziehen") : Loc.T("Den Drachenhort stürmen")) : Loc.T("Aus der Heiligen Quelle trinken");

        public static string PoiActionTip(PoiData p) =>
            p.type == PoiTablet ? Loc.T("Auf der Tafel steht ein Rätsel. Wer drei löst, erfährt, wo der Drachenhort liegt.")
            : p.type == PoiHoard ? Loc.F("Steinerne Wächter, etwa {0} Mann stark. Ein riesiger Schatz wartet.", Math.Max(1, (int)Math.Round(p.guards.Sum() / 5f) * 5))
            : Loc.T("Volle Moral für das Heer und Freude im nächsten Ort.");

        public static string PoiDescription(PoiData p) =>
            p.type == PoiTablet ? (p.done ? Loc.T("Das Rätsel dieser Tafel ist gelöst. Ihre Runen sind erloschen.") : Loc.T("Ein mannshoher Stein mit glimmenden Runen. Ein Heer oder Späher kann das Rätsel lesen und lösen."))
            : p.type == PoiHoard ? (p.done ? Loc.T("Der Hort ist geplündert. Nur Drachenknochen bleiben.") : Loc.F("Der Hort von Vargrimm, dem letzten Drachen. Steinerne Wächter halten Wacht. __Wächter__: ~{0}", Math.Max(1, (int)Math.Round(p.guards.Sum() / 5f) * 5)))
            : Loc.T("Klares Wasser zwischen moosigen Steinen. Ein Heer, das hier trinkt, gewinnt volle Moral. Danach ruht die Quelle einige Wochen.");

        public static string PoiDisplayName(PoiData p)
        {
            switch (p.type)
            {
                case PoiTablet: return p.done ? Loc.T("Verstummte Steintafel") : Loc.T("Alte Steintafel");
                case PoiHoard: return p.done ? Loc.T("Geplünderter Drachenhort") : Loc.T("Drachenhort");
                case PoiSpring: return Loc.T("Heilige Quelle");
            }
            return null;
        }

        /// <summary>Beim Spielstart: Steintafeln und Quellen verteilen, die Lage des Drachenhorts festlegen.</summary>
        void PlaceMythPois(Random r)
        {
            bool Ok(int x, int y, float minSet)
            {
                if (!WorldMap.In(x, y)) return false;
                int i = WorldMap.Idx(x, y);
                if (!Map.Buildable(i) || Map.deposit[i] != 0 || Map.river[i]) return false;
                foreach (var s in S.settlements) if (Dist(s.x, s.y, x, y) < minSet) return false;
                foreach (var p in S.pois) if (Dist(p.x, p.y, x, y) < 8) return false;
                return true;
            }
            int[] tabletConts = { 0, 0, 1, 2, 4, 5, 4, 5 };
            int t = 0, springs = 0;
            for (int k = 0; k < 20000 && (t < tabletConts.Length || springs < 5); k++)
            {
                int x = r.Next(6, WorldMap.W - 6), y = r.Next(6, WorldMap.H - 6);
                if (!Ok(x, y, 7)) continue;
                int c = Map.cont[WorldMap.Idx(x, y)];
                if (t < tabletConts.Length && c == tabletConts[t])
                {
                    S.pois.Add(new PoiData { id = NewId(), type = PoiTablet, x = x, y = y, seed = t });
                    t++;
                }
                else if (springs < 5 && (Map.biome[WorldMap.Idx(x, y)] == Biome.Forest || Map.biome[WorldMap.Idx(x, y)] == Biome.Pine) && r.NextDouble() < 0.3)
                {
                    S.pois.Add(new PoiData { id = NewId(), type = PoiSpring, x = x, y = y, seed = r.Next() });
                    springs++;
                }
                else continue;
                Map.trees[WorldMap.Idx(x, y)] = 0; Map.baseTrees[WorldMap.Idx(x, y)] = 0;
            }
            // Drachenhort: an einem Berghang auf Eisenfels (sonst irgendwo am Gebirge)
            for (int pass = 0; pass < 2 && S.hoardX < 0; pass++)
                for (int k = 0; k < 20000; k++)
                {
                    int x = r.Next(6, WorldMap.W - 6), y = r.Next(6, WorldMap.H - 6);
                    if (pass == 0 && Map.cont[WorldMap.Idx(x, y)] != 4) continue;
                    if (!Ok(x, y, 10)) continue;
                    bool mountain = false;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + WorldMap.DX8[d], ny = y + WorldMap.DY8[d];
                        if (WorldMap.In(nx, ny) && (Map.biome[WorldMap.Idx(nx, ny)] == Biome.Mountain || Map.biome[WorldMap.Idx(nx, ny)] == Biome.Snow)) mountain = true;
                    }
                    if (!mountain) continue;
                    S.hoardX = x; S.hoardY = y;
                    break;
                }
        }

        // ------------------------------------------------------------------
        //  Besuche
        // ------------------------------------------------------------------
        bool VisitMyth(ArmyData a, PoiData p)
        {
            var f = F(a.faction);
            if (!f.isPlayer) return p.type == PoiTablet || p.type == PoiSpring || p.type == PoiHoard;
            switch (p.type)
            {
                case PoiTablet:
                    a.mission = (int)ArmyMission.Idle;
                    if (p.done) { Notify(Loc.T("Die Zeichen auf der Tafel sind erloschen. Ihr Geheimnis ist schon gelüftet."), 0); return true; }
                    if (S.day < p.nextRaid) { Notify(Loc.F("Die Runen schweigen noch. Kommt in {0} Tagen wieder.", p.nextRaid - S.day), 0); return true; }
                    QueueEvent(new PendingEvent { id = "riddle", b = p.id, c = a.id });
                    return true;
                case PoiSpring:
                    a.mission = (int)ArmyMission.Idle;
                    if (S.day < p.nextRaid) { Notify(Loc.T("Das Wasser der Quelle ruht. Erst in einigen Wochen schenkt sie wieder Segen."), 0); return true; }
                    p.nextRaid = S.day + 60;
                    a.morale = 1.3f;
                    var near = SettlementsOf(f.id).OrderBy(s => Dist(s.x, s.y, p.x, p.y)).FirstOrDefault();
                    if (near != null) near.happiness = Math.Min(100, near.happiness + 12);
                    f.prestige += 5;
                    string where = near != null ? near.name : Loc.T("eurem Land");
                    Notify(Loc.T("Heilige Quelle: Eure Soldaten trinken und fühlen sich wie neugeboren (volle Moral)."), 1);
                    Say(Aldo, "advisor", Loc.F("An der Quelle schimmert das Wasser wie Silber. Die Alten sagen, eine Nymphe wache darüber. Eure Männer kehren mit leuchtenden Augen zurück – und in {0} spricht man von einem guten Zeichen.", where));
                    return true;
                case PoiHoard:
                    return false;   // Kampf wie bei einem Räuberlager (in VisitPoi)
            }
            return false;
        }

        // ------------------------------------------------------------------
        //  Das Rätsel
        // ------------------------------------------------------------------
        int[] RiddleOrder(PoiData p)
        {
            // feste, aber je Tafel andere Reihenfolge der Antworten
            var order = new[] { 1, 2, 3 };
            int rot = (p.seed * 7 + p.x) % 3;
            for (int k = 0; k < rot; k++) { int t0 = order[0]; order[0] = order[1]; order[1] = order[2]; order[2] = t0; }
            return order;
        }

        bool RiddleView(PendingEvent e, EventView v)
        {
            var p = Poi(e.b);
            v.Title = Loc.T("Die alte Steintafel"); v.Icon = "book";
            if (p == null) { v.Text = Loc.T("Die Tafel ist verschwunden."); v.Opt(Loc.T("Gut so")); return false; }
            var rd = Riddles[p.seed % Riddles.Length];
            v.Text = Loc.T("Moos überwuchert einen mannshohen Stein. Darauf glimmen Runen, die eure Gelehrten mühsam entziffern:") + "\n\n'" + Loc.T(rd[0]) + "'\n\n"
                   + Loc.F("Gelöste Rätsel: {0}/{1}. Man erzählt, wer drei löst, erfährt, wo der letzte Drache seinen Hort verbarg.", Math.Min(S.runesSolved, RunesForHoard), RunesForHoard);
            foreach (int k in RiddleOrder(p)) v.Opt(Loc.T(rd[k]));
            v.Opt(Loc.T("Weitergehen, ohne zu antworten"));
            return true;
        }

        void RiddleResolve(PendingEvent e, int option)
        {
            var p = Poi(e.b);
            if (p == null || p.done || option > 2) return;
            var order = RiddleOrder(p);
            var f = Player;
            if (order[option] != 1)
            {
                p.nextRaid = S.day + 30;
                Notify(Loc.T("Falsch! Die Runen erlöschen. Vielleicht verraten sie in einem Monat erneut ihr Rätsel."), 2);
                return;
            }
            p.done = true; PoisVersion++;
            S.runesSolved++;
            string reward;
            switch (S.runesSolved % 4)
            {
                case 1: f.res[(int)Res.Gold] += 150; reward = Loc.T("Unter dem Stein liegt ein Beutel alter Münzen: 150 Gold."); break;
                case 2: S.relics++; reward = Loc.T("Im Sockel ruht ein heiliges Relikt. Bewahrt es in einem Kloster auf."); break;
                case 3: Reveal(p.x, p.y, 30); reward = Loc.T("Die Runen zeichnen eine Karte der Umgebung in eure Köpfe."); break;
                default: f.prestige += 25; foreach (var s in SettlementsOf(f.id)) s.happiness = Math.Min(100, s.happiness + 5); reward = Loc.T("Barden singen von eurer Weisheit (+25 Ansehen, +Zufriedenheit)."); break;
            }
            Notify(Loc.T("Richtig! ") + reward, 1);
            if (S.runesSolved == RunesForHoard) RevealHoard();
        }

        void RevealHoard()
        {
            if (S.hoardX < 0 || S.pois.Any(q => q.type == PoiHoard)) return;
            var h = new PoiData { id = NewId(), type = PoiHoard, x = S.hoardX, y = S.hoardY, seed = 1 };
            h.guards[(int)UType.Speer] = 40; h.guards[(int)UType.Bogen] = 30; h.guards[(int)UType.Ritter] = 25;
            S.pois.Add(h);
            PoisVersion++;
            Reveal(S.hoardX, S.hoardY, 6);
            RebuildOccupancy();
            Say(Aldo, "advisor", Loc.F("Drei Rätsel, drei Runen – und nun fügen sie sich zusammen: Auf {0}, an einem Berghang, liegt der Hort von Vargrimm, dem letzten Drachen. Er selbst ist lange tot, doch seine steinernen Wächter schlafen nicht. Bringt ein starkes Heer – und Schiffe.", WorldMap.ContinentNames[Map.cont[WorldMap.Idx(S.hoardX, S.hoardY)]]));
        }

        /// <summary>Beute aus dem Drachenhort (nach gewonnenem Kampf).</summary>
        void HoardLooted(FactionData f)
        {
            f.res[(int)Res.Gold] += 1500; f.res[(int)Res.Weapons] += 40; S.relics += 2; f.prestige += 80;
            S.story.Set("dragon_crown");
            Say(Aldo, "advisor", Loc.T("Gold, so weit das Auge reicht – und auf einem Felsen die Krone des Drachenkönigs. Wer sie trägt, dem folgt das Volk gern. Diese Geschichte wird man sich noch in hundert Jahren erzählen."));
        }

        // ------------------------------------------------------------------
        //  Seltsame Ereignisse
        // ------------------------------------------------------------------
        bool MythEventView(PendingEvent e, EventView v, string sn)
        {
            switch (e.id)
            {
                case "wisps":
                    v.Title = Loc.T("Irrlichter"); v.Icon = "sun";
                    v.Text = Loc.F("Nachts tanzen bläuliche Lichter über dem Moor bei {0}. Ein Hirtenjunge schwört, sie hätten ihn zu einem vergrabenen Schatz führen wollen. Die Alten warnen: Irrlichter führen in die Irre.", sn);
                    v.Opt(Loc.T("Den Lichtern folgen lassen (Wagnis)"));
                    v.Opt(Loc.T("Die Leute vom Moor fernhalten"));
                    return true;
                case "comet":
                    v.Title = Loc.T("Ein Schweifstern"); v.Icon = "sun";
                    v.Text = Loc.T("Ein Komet mit feurigem Schweif steht am Himmel. Die einen sehen darin ein Zeichen des Glücks, die anderen ein böses Omen. Alle schauen auf euch.");
                    v.Opt(Loc.T("Ein Fest des Himmelszeichens ausrufen (-40 Gold, +Zufriedenheit)"), Player.res[(int)Res.Gold] >= 40);
                    v.Opt(Loc.T("Sterndeuter befragen (-20 Gold, Karte der Umgebung)"), Player.res[(int)Res.Gold] >= 20);
                    v.Opt(Loc.T("Nichts tun"));
                    return true;
            }
            return false;
        }

        bool MythEventResolve(PendingEvent e, int option, SettlementData s)
        {
            var p = Player;
            switch (e.id)
            {
                case "wisps":
                    if (option == 0)
                    {
                        if (Rng.NextDouble() < 0.5) { int g = 80 + Rng.Next(120); p.res[(int)Res.Gold] += g; Notify(Loc.F("Die Irrlichter führten zu einer alten Truhe: {0} Gold!", g), 1); }
                        else { if (s != null) s.pop = Math.Max(5, s.pop - 3); Notify(Loc.T("Die Irrlichter lockten drei Leute ins Moor. Man sah sie nie wieder."), 2); }
                    }
                    return true;
                case "comet":
                    if (option == 0 && p.res[(int)Res.Gold] >= 40) { p.res[(int)Res.Gold] -= 40; foreach (var x in SettlementsOf(p.id)) x.happiness = Math.Min(100, x.happiness + 10); }
                    else if (option == 1 && p.res[(int)Res.Gold] >= 20) { p.res[(int)Res.Gold] -= 20; var c = Capital(p); if (c != null) Reveal(c.x, c.y, 26); }
                    else if (option == 2) foreach (var x in SettlementsOf(p.id)) x.happiness = Math.Max(0, x.happiness - 3);
                    return true;
            }
            return false;
        }

        /// <summary>Täglich: Hinweise, wenn Tafeln oder Quellen entdeckt werden.</summary>
        void MythDay()
        {
            if (S.day % 3 != 0) return;
            foreach (var p in S.pois)
            {
                if (!Map.disc[WorldMap.Idx(p.x, p.y)]) continue;
                if (p.type == PoiTablet && !p.done) Hint("tablet", Loc.T("Eine alte Steintafel mit leuchtenden Runen! Schickt ein Heer oder einen Späher hin: Darauf steht ein Rätsel. Wer drei solcher Rätsel löst, soll erfahren, wo der letzte Drache seinen Hort verbarg."));
                if (p.type == PoiSpring) Hint("spring", Loc.T("Eine Heilige Quelle im Wald! Ein Heer, das dort trinkt, gewinnt volle Moral, und der nächste Ort freut sich über das gute Zeichen. Danach ruht die Quelle einige Wochen."));
            }
        }
    }
}
