using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    public class Objective
    {
        public string Text; public float Cur, Target;
        public bool Done => Cur >= Target;
        public Objective(string t, float c, float target) { Text = t; Cur = Math.Min(c, target); Target = target; }
    }

    public class Chapter
    {
        public string Title, Subtitle;
        public Func<Game, List<DialogLine>> Intro, Outro;
        public Func<Game, List<Objective>> Objectives;
        public Action<Game> Reward;
        public string RewardText;
    }

    /// <summary>
    /// Die Geschichte: Vom kleinen Dorf zum Sturz des Kaisers Malrik – und danach hinaus in die Welt.
    /// </summary>
    public partial class Game
    {
        const string Alda = "Alda, Chronistin";
        const string Bero = "Hauptmann Bero";
        const string Malrik = "Kaiser Malrik";

        static List<DialogLine> L(params (string who, string portrait, string text)[] lines) => lines.Select(l => new DialogLine(l.who, l.portrait, l.text)).ToList();

        public static readonly Chapter[] Chapters =
        {
            new Chapter
            {
                Title = "Kapitel 1", Subtitle = "Ein bescheidener Anfang",
                Intro = g => L((Alda, "advisor", "Willkommen, Vorsteher. Dies ist euer Dorf – ein paar Hütten, ein Acker, viel Hoffnung."),
                               (Alda, "advisor", "Die Leute brauchen Holz, Nahrung und Dächer über dem Kopf. Wählt unten euer Dorf und baut."),
                               (Alda, "advisor", "Tipp: Rechte Maustaste oder WASD bewegt die Karte, das Mausrad zoomt. Leertaste pausiert.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Baut einen Holzfäller", g.CountBuildings(BType.Holzfaeller), 1),
                    new Objective("Habt 2 Bauernhöfe oder Fischerhütten", g.CountBuildings(BType.Bauernhof) + g.CountBuildings(BType.Fischerhuette), 2),
                    new Objective("Habt 4 Wohnhäuser", g.CountBuildings(BType.Wohnhaus), 4),
                },
                Outro = g => L((Alda, "advisor", "Das Dorf wächst. Rauch steigt aus den Kaminen, Kinder spielen am Bach."),
                               (Alda, "advisor", "Doch ein Dorf allein ist schwach. Wir müssen größer werden – eine echte Gemeinde.")),
                Reward = g => { g.Player.res[(int)Res.Gold] += 60; g.Player.res[(int)Res.Wood] += 40; }, RewardText = "+60 Gold, +40 Holz",
            },
            new Chapter
            {
                Title = "Kapitel 2", Subtitle = "Vom Dorf zur Gemeinde",
                Intro = g => L((Alda, "advisor", "Um eine Städtegemeinde zu werden, braucht ihr mehr Bewohner und etwas Ansehen."),
                               (Alda, "advisor", "Zufriedene Bürger bekommen mehr Kinder. Eine Taverne und ein Brunnen helfen. Achtet auf die Steuern!"),
                               (Alda, "advisor", "Öffnet das Reich-Menü (R), sobald ihr bereit seid aufzusteigen.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Erreicht 45 Bewohner", g.TotalPop(g.PlayerId), 45),
                    new Objective("Steigt zur Städtegemeinde auf", g.Player.rank, 1),
                },
                Outro = g => L((Alda, "advisor", "Eine Gemeinde! Die Händler kommen nun gern zu uns."),
                               (Bero, "knight", "Herr, Reisende berichten von anderen Herren hinter den Wäldern. Und von den Steuereintreibern des Kaisers."),
                               (Alda, "advisor", "Hier sind zwei Späher. Schickt sie hinaus – wir müssen wissen, wer unsere Nachbarn sind.")),
                Reward = g => { var c = g.Capital(g.Player); if (c != null) c.garrison[(int)UType.Spaeher] += 2; g.Player.res[(int)Res.Gold] += 50; },
                RewardText = "2 Späher, +50 Gold",
            },
            new Chapter
            {
                Title = "Kapitel 3", Subtitle = "Fremde Banner",
                Intro = g => L((Bero, "knight", "Die Welt ist größer, als wir dachten. Fürsten, freie Dörfer, Räuber – jeder will etwas."),
                               (Alda, "advisor", "Mit Gold und Worten gewinnt man Freunde. Mit Schwertern gewinnt man Land. Beides hat seinen Preis."),
                               (Alda, "advisor", "Wählt eine Armee aus und klickt mit Rechtsklick auf ein Ziel. Siedler gründen neue Orte.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Lernt 3 Herrscher kennen", g.S.factions.Count(f => f.knownToPlayer != 0), 3),
                    new Objective("Herrscht über 2 Siedlungen", g.CountSettlements(g.PlayerId), 2),
                    new Objective("Schließt einen Vertrag (Handel, Pakt, Bündnis)", g.Player.relations.Any(r => r.trade || r.status >= (int)DipStatus.Pact) ? 1 : 0, 1),
                },
                Outro = g => L((Alda, "advisor", "Eine Botschaft, heimlich überbracht. Sie stammt von Flüchtlingen aus dem Osten:"),
                               ("Brief", "scroll", "'Kaiser Malrik nimmt uns alles. Die Ernte, die Söhne, das Saatgut. Wer sich wehrt, hängt am Galgen von Valdoran.'"),
                               (Alda, "advisor", "Valdoran... die Hauptstadt des Kaisers. Irgendwann wird er auch zu uns kommen.")),
                Reward = g => { g.Player.prestige += 25; g.RevealEmperorCapital(); }, RewardText = "+25 Ansehen, Lage von Valdoran",
            },
            new Chapter
            {
                Title = "Kapitel 4", Subtitle = "Das Joch des Kaisers",
                Intro = g => L((Alda, "advisor", "Der Kaiser presst die Fürsten aus, und die Fürsten pressen ihr Volk. Manche sind ihm treu, andere hassen ihn."),
                               (Bero, "knight", "Wir brauchen Mauern und eine Burg. Und Männer, die kämpfen können."),
                               (Alda, "advisor", "Werdet eine Grafschaft. Baut eine Burg. Zeigt dem Land, dass es einen anderen Weg gibt.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Steigt zur Grafschaft auf", g.Player.rank, 2),
                    new Objective("Baut eine Burg", g.CountBuildings(BType.Burg), 1),
                    new Objective("Gewinnt 2 Schlachten", g.S.battlesWon, 2),
                },
                Outro = g => L((Alda, "advisor", "Euer Name wird in den Tavernen geflüstert. Die Unterdrückten schöpfen Hoffnung."),
                               (Malrik, "emperor", "Ein Graf, der sich für einen König hält? Ich habe größere Narren am Galgen gesehen."),
                               (Alda, "advisor", "Er hat uns bemerkt. Wir müssen Verbündete finden, bevor er zuschlägt.")),
                Reward = g => { g.Player.prestige += 40; g.Player.res[(int)Res.Gold] += 150; }, RewardText = "+40 Ansehen, +150 Gold",
            },
            new Chapter
            {
                Title = "Kapitel 5", Subtitle = "Der Bund der Freien",
                Intro = g => L((Alda, "advisor", "Allein können wir Malrik nicht besiegen. Sucht Verbündete unter jenen, die den Kaiser fürchten – oder macht sie zu einem Teil eures Reiches."),
                               (Alda, "advisor", "Achtung: Kaisertreue Fürsten werden euch dafür hassen. Wählt eure Freunde klug."),
                               (Bero, "knight", "Und wir brauchen ein richtiges Heer. Ritter. Speere. Bogenschützen.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Steigt zum Fürstentum auf", g.Player.rank, 3),
                    new Objective("2 Verbündete ODER 6 Siedlungen", Math.Max(g.AlliesOf(g.PlayerId).Count() / 2f, g.CountSettlements(g.PlayerId) / 6f), 1),
                    new Objective("Unterhaltet 60 Soldaten", g.TotalSoldiers(g.PlayerId), 60),
                },
                Outro = g => L((Malrik, "emperor", "Genug! Ihr habt meinen Zehnt verweigert, meine Vasallen verführt und Waffen geschmiedet."),
                               (Malrik, "emperor", "Hiermit erkläre ich euch zum Verräter am Reich. Meine Legionen werden euer Land in Asche legen."),
                               (Bero, "knight", "Dann ist es Krieg. Für das Volk!")),
                Reward = g => { g.StartEmperorWar(); g.Player.prestige += 30; }, RewardText = "Der Kaiser erklärt den Krieg!",
            },
            new Chapter
            {
                Title = "Kapitel 6", Subtitle = "Krone des Volkes",
                Intro = g => L((Alda, "advisor", "Die kaiserlichen Städte stöhnen unter seinen Steuern. Befreit sie – ihre Bürger werden euch zujubeln."),
                               (Alda, "advisor", "Manchmal erheben sich Städte von selbst, wenn man ihnen Gold für Waffen schickt."),
                               (Alda, "advisor", "Und das Volk braucht eine Krone, hinter der es sich sammeln kann: Werdet König.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Steigt zum Königreich auf", g.Player.rank, 4),
                    new Objective("Befreit 2 kaiserliche Städte", g.S.story.flags.Count(f => f == "liberated"), 2),
                },
                Outro = g => L((Alda, "advisor", "Ein König, vom Volk getragen. Malrik hat nur noch Valdoran und seine Garde."),
                               (Malrik, "emperor", "Kommt nur. Valdorans Mauern sind noch nie gefallen.")),
                Reward = g => { g.Player.prestige += 50; g.Player.res[(int)Res.Weapons] += 60; }, RewardText = "+50 Ansehen, +60 Waffen",
            },
            new Chapter
            {
                Title = "Kapitel 7", Subtitle = "Sturm auf Valdoran",
                Intro = g => L((Bero, "knight", "Valdoran. Doppelte Mauern, eine Burg, die kaiserliche Garde. Ohne Rammen kommen wir nicht hinein."),
                               (Alda, "advisor", "Baut eine Belagerungswerkstatt. Sammelt euer größtes Heer. Und dann – beendet die Tyrannei.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Erobert Valdoran", g.S.story.emperorDefeated ? 1 : 0, 1),
                },
                Outro = g => L((Alda, "advisor", "Valdoran ist gefallen! Malrik ist geflohen, seine Garde hat die Waffen gestreckt."),
                               (Alda, "advisor", "Auf den Straßen wird getanzt. Die Kornspeicher des Kaisers werden an das Volk verteilt."),
                               (Bero, "knight", "Kapitäne aus dem Hafen berichten von Ländern jenseits des Meeres. Nordlande. Südreich. Ein Inselbund."),
                               (Alda, "advisor", "Die Meere sind nun frei. Baut einen Hafen – und die Welt steht euch offen.")),
                Reward = g => { g.S.seafaring = true; g.Player.prestige += 100; g.Player.res[(int)Res.Gold] += 600; }, RewardText = "Seefahrt freigeschaltet, +600 Gold",
            },
            new Chapter
            {
                Title = "Kapitel 8", Subtitle = "Neue Ufer",
                Intro = g => L((Alda, "advisor", "Mit einem Hafen können eure Heere und Siedler über das Meer fahren. Wählt eine Armee und zielt aufs Wasser."),
                               (Alda, "advisor", "Die Herrscher dort kennen euch nicht. Handelt, verbündet euch – oder erobert.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Baut einen Hafen", g.CountBuildings(BType.Hafen), 1),
                    new Objective("Entdeckt einen anderen Kontinent", g.ContinentsSeen(), 1),
                    new Objective("Siedlung oder Verbündeter in Übersee", g.OverseasFootholds(), 1),
                },
                Outro = g => L((Alda, "advisor", "Euer Banner weht nun jenseits des Meeres. Die Chronik eures Reiches füllt Bände."),
                               (Alda, "advisor", "Eine letzte Aufgabe bleibt: Eint die Kontinente – durch Bündnisse oder Eroberung.")),
                Reward = g => { g.Player.prestige += 80; }, RewardText = "+80 Ansehen",
            },
            new Chapter
            {
                Title = "Kapitel 9", Subtitle = "Herrscher der Welt",
                Intro = g => L((Alda, "advisor", "Ein Kontinent gilt als geeint, wenn jedes Reich dort euch gehört oder euer Verbündeter ist.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Mittland geeint", g.ContinentUnited(0), 1),
                    new Objective("Nordlande geeint", g.ContinentUnited(1), 1),
                    new Objective("Südreich geeint", g.ContinentUnited(2), 1),
                    new Objective("Inselbund geeint", g.ContinentUnited(3), 1),
                },
                Outro = g => L((Alda, "advisor", "Es ist vollbracht. Von einem Dorf mit zwei Hütten zum Herrscher der Welt."),
                               (Alda, "advisor", "Ich lege die Feder nieder. Doch euer Reich lebt weiter – regiert es weise.")),
                Reward = g => { g.Player.prestige += 500; }, RewardText = "Sieg!",
            },
        };

        public Chapter CurrentChapter => S.story.chapter < Chapters.Length ? Chapters[S.story.chapter] : null;
        public bool StoryFinished => S.story.chapter >= Chapters.Length;

        void InitStory()
        {
            S.story.chapter = 0;
            S.story.chapterIntroShown = false;
        }

        /// <summary>Zeigt das Kapitel-Intro sofort (z.B. direkt nach Spielstart).</summary>
        public void StoryTick() => StoryDay();

        void StoryDay()
        {
            var ch = CurrentChapter;
            if (ch == null) return;
            if (!S.story.chapterIntroShown)
            {
                S.story.chapterIntroShown = true;
                Say(ch.Title, "book", ch.Subtitle);
                foreach (var l in ch.Intro(this)) Say(l.Speaker, l.Portrait, l.Text);
            }
            var objs = ch.Objectives(this);
            if (objs.All(o => o.Done))
            {
                foreach (var l in ch.Outro(this)) Say(l.Speaker, l.Portrait, l.Text);
                ch.Reward?.Invoke(this);
                Notify($"{ch.Title} abgeschlossen: {ch.Subtitle}. Belohnung: {ch.RewardText}", 4);
                S.story.chapter++;
                S.story.chapterIntroShown = false;
            }
        }

        public List<Objective> CurrentObjectives() => CurrentChapter?.Objectives(this) ?? new List<Objective>();

        // ------------------------------------------------------------------
        public int CountBuildings(BType t) { int n = 0; foreach (var s in SettlementsOf(PlayerId)) n += s.buildings[(int)t]; return n; }

        public int TotalSoldiers(int fid)
        {
            int n = 0;
            foreach (var s in SettlementsOf(fid)) for (int u = 0; u < Units.Count; u++) if (u != (int)UType.Spaeher) n += s.garrison[u];
            foreach (var a in ArmiesOf(fid)) for (int u = 0; u < Units.Count; u++) if (u != (int)UType.Spaeher) n += a.units[u];
            return n;
        }

        public int ContinentsSeen()
        {
            for (int i = 0; i < Map.disc.Length; i += 3) if (Map.disc[i] && Map.cont[i] != 0 && Map.IsLand(i)) return 1;
            return 0;
        }

        public int OverseasFootholds()
        {
            if (SettlementsOf(PlayerId).Any(s => Map.cont[WorldMap.Idx(s.x, s.y)] != 0)) return 1;
            if (AlliesOf(PlayerId).Any(f => f.continent != 0)) return 1;
            return 0;
        }

        public int ContinentUnited(int cont)
        {
            foreach (var f in S.factions)
            {
                if (!f.alive || f.isPlayer) continue;
                if (!SettlementsOf(f.id).Any(s => Map.cont[WorldMap.Idx(s.x, s.y)] == cont)) continue;
                if (Status(PlayerId, f.id) != DipStatus.Alliance) return 0;
            }
            return 1;
        }

        public void RevealEmperorCapital()
        {
            var emp = Emperor;
            if (emp == null) return;
            foreach (var s in SettlementsOf(emp.id)) if (s.isCapitalOfEmperor) Reveal(s.x, s.y, 5);
            MeetFaction(emp.id);
        }

        public void StartEmperorWar()
        {
            var emp = Emperor;
            if (emp == null || !emp.alive || S.story.Has("emperor_war")) return;
            S.story.Set("emperor_war");
            S.story.emperorWarDay = S.day;
            if (Status(emp.id, PlayerId) != DipStatus.War) DeclareWar(emp.id, PlayerId);
            foreach (var f in S.factions)
            {
                if (!f.alive || f.isPlayer || f.isEmperor || f.continent != 0 || f.isFree) continue;
                if (f.loyalty > 0.7f && Status(f.id, PlayerId) != DipStatus.Alliance)
                {
                    if (Status(f.id, PlayerId) != DipStatus.War) { SetStatus(f.id, PlayerId, DipStatus.War); Notify($"{f.name} hält zum Kaiser und erklärt euch den Krieg!", 3); }
                }
                else if (f.loyalty < 0.35f)
                {
                    ModRel(f.id, PlayerId, 20);
                    if (Status(f.id, emp.id) == DipStatus.Pact) SetStatus(f.id, emp.id, DipStatus.Neutral);
                }
            }
        }

        // ------------------------------------------------------------------
        //  Story-Hooks
        // ------------------------------------------------------------------
        void OnFactionMet(FactionData f)
        {
            if (f.isEmperor && !S.story.Has("met_emperor"))
            {
                S.story.Set("met_emperor");
                Say(Alda, "advisor", "Das schwarze Banner mit dem eisernen Adler – das Kaiserreich Valdor. Seid vorsichtig.");
            }
            else if (!S.story.Has("met_first"))
            {
                S.story.Set("met_first");
                Say(Alda, "advisor", $"Wir sind {f.leader} begegnet ({f.name}). Öffnet die Diplomatie (D), um mit ihnen zu verhandeln.");
            }
        }

        void OnBuildingFinished(SettlementData s, BType t)
        {
            if (!F(s.faction).isPlayer) return;
            if (t == BType.Hafen && S.seafaring && !S.story.Has("first_harbor"))
            {
                S.story.Set("first_harbor");
                Say(Alda, "advisor", "Der Hafen ist fertig! Eure Armeen können nun über das Meer segeln.");
            }
        }

        void OnRankUp(int rank)
        {
            var rd = Ranks.Get(rank);
            Say(Alda, "advisor", $"Es ist vollbracht: Ihr seid nun {rd.Ruler} – {rd.Name}. {rd.Desc}");
            if (rank == (int)Rank.Kaiserreich) Say(Alda, "advisor", "Ein Kaiser – doch einer, der für sein Volk regiert. Möge die Geschichte gnädig urteilen.");
        }

        void OnSettlementFounded(SettlementData s) { }

        void OnBattle(FactionData att, FactionData def, bool attackerWon) { }

        void OnConquered(SettlementData s, FactionData oldF, FactionData newF)
        {
            if (!newF.isPlayer) return;
            if (oldF.isEmperor)
            {
                S.story.flags.Add("liberated");
                if (s.isCapitalOfEmperor && !S.story.emperorDefeated)
                {
                    S.story.emperorDefeated = true;
                    s.isCapitalOfEmperor = false;
                    s.happiness = 70;
                    // Das Kaiserreich zerfällt
                    foreach (var st in SettlementsOf(oldF.id).ToList())
                    {
                        var free = new FactionData
                        {
                            id = NewId(), name = "Freie Stadt " + st.name, leader = "Bürgerrat", isFree = true, continent = 0, culture = 0,
                            color = 11, aggression = 0.1f, honor = 0.7f, ambition = 0.1f, loyalty = 0, res = new float[] { 100, 60, 40, 10, 80, 10 },
                        };
                        AddFaction(free); InitRelationsFor(free);
                        ModRel(free.id, PlayerId, 40);
                        free.knownToPlayer = 1;
                        TransferSettlement(st, free.id);
                    }
                    foreach (var a in ArmiesOf(oldF.id).ToList()) RemoveArmy(a);
                    oldF.alive = false;
                    Notify("DER KAISER IST GESTÜRZT!", 4);
                    S.story.Set("emperor_defeated");
                }
            }
        }

        void OnAlliance(FactionData f) { }
        void OnAnnexed(FactionData f) { }
    }
}
