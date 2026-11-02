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
        public Action<Game> Reward, OnStart;
        public string RewardText;
    }

    /// <summary>
    /// Die Geschichte: Vom kleinen Dorf zum Sturz des Kaisers Malrik – und danach hinaus in die Welt.
    /// </summary>
    public partial class Game
    {
        const string Aldo = "Meister Aldo, Chronist";
        const string Bero = "Hauptmann Bero";
        const string Malrik = "Kaiser Malrik";

        static List<DialogLine> L(params (string who, string portrait, string text)[] lines) => lines.Select(l => new DialogLine(l.who, l.portrait, l.text)).ToList();

        const string Gilda = "Meisterin Gilda";
        const string Kael = "General Kael";

        public static readonly Chapter[] Chapters =
        {
            // ---------------------------------------------------------------- 1
            new Chapter
            {
                Title = "Kapitel 1", Subtitle = "Ein bescheidener Anfang",
                Intro = g => L((Aldo, "advisor", "Willkommen, Vorsteher. Dies ist euer Dorf – ein paar Hütten, ein Acker, viel Hoffnung."),
                               (Aldo, "advisor", "Die Leute brauchen Holz, Nahrung und Dächer über dem Kopf. Wählt euer Dorf und öffnet den Reiter 'Bauen'."),
                               (Aldo, "advisor", "Niemand bedroht uns hier draußen. Lasst euch Zeit – ein starkes Dorf ist das beste Fundament.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Baut einen Holzfäller", g.CountBuildings(BType.Holzfaeller), 1),
                    new Objective("Habt 2 Bauernhöfe oder Fischerhütten", g.CountBuildings(BType.Bauernhof) + g.CountBuildings(BType.Fischerhuette), 2),
                    new Objective("Habt 4 Wohnhäuser", g.CountBuildings(BType.Wohnhaus), 4),
                },
                Outro = g => L((Aldo, "advisor", "Das Dorf wächst. Rauch steigt aus den Kaminen, Kinder spielen am Bach.")),
                Reward = g => { g.Player.res[(int)Res.Gold] += 60; g.Player.res[(int)Res.Wood] += 40; }, RewardText = "+60 Gold, +40 Holz",
            },
            // ---------------------------------------------------------------- 2
            new Chapter
            {
                Title = "Kapitel 2", Subtitle = "Vom Dorf zur Gemeinde",
                Intro = g => L((Aldo, "advisor", "Um eine Städtegemeinde zu werden, braucht ihr mehr Bewohner und etwas Ansehen."),
                               (Aldo, "advisor", "Zufriedene Bürger bekommen mehr Kinder. Eine Taverne und ein Brunnen helfen. Achtet auf die Steuern!"),
                               (Aldo, "advisor", "Öffnet die Reich-Übersicht (R), sobald ihr bereit seid aufzusteigen.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Erreicht 45 Bewohner", g.TotalPop(g.PlayerId), 45),
                    new Objective("Steigt zur Städtegemeinde auf", g.Player.rank, 1),
                },
                Outro = g => L((Aldo, "advisor", "Eine Gemeinde! Die Händler kommen nun gern zu uns."),
                               (Aldo, "advisor", "Doch die Alten warnen: Die Winter in diesem Tal sind lang und hart.")),
                Reward = g => { g.Player.res[(int)Res.Gold] += 50; }, RewardText = "+50 Gold",
            },
            // ---------------------------------------------------------------- 3 (neu)
            new Chapter
            {
                Title = "Kapitel 3", Subtitle = "Vorräte für den Winter",
                Intro = g => L((Aldo, "advisor", "Im Winter tragen die Felder kaum. Wer nicht vorsorgt, hungert."),
                               (Aldo, "advisor", "Baut ein Lagerhaus, füllt die Speicher und sorgt für sauberes Wasser. Eine ruhige Zeit – nutzt sie.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Baut ein Lagerhaus", g.CountBuildings(BType.Lagerhaus), 1),
                    new Objective("Baut einen Brunnen", g.CountBuildings(BType.Brunnen), 1),
                    new Objective("Lagert 200 Nahrung", g.Player.res[(int)Res.Food], 200),
                },
                Outro = g => L((Aldo, "advisor", "Die Speicher sind voll. Diesen Winter wird niemand hungern."),
                               (Bero, "knight", "Herr, Reisende berichten von anderen Herren hinter den Wäldern. Und von den Steuereintreibern des Kaisers."),
                               (Aldo, "advisor", "Hier sind zwei Späher. Schickt sie hinaus – wir müssen wissen, wer unsere Nachbarn sind.")),
                Reward = g => { var c = g.Capital(g.Player); if (c != null) c.garrison[(int)UType.Spaeher] += 2; g.Player.prestige += 10; },
                RewardText = "2 Späher, +10 Ansehen",
            },
            // ---------------------------------------------------------------- 4
            new Chapter
            {
                Title = "Kapitel 4", Subtitle = "Fremde Banner",
                Intro = g => L((Bero, "knight", "Die Welt ist größer, als wir dachten. Fürsten, freie Dörfer, Räuber – jeder will etwas."),
                               (Aldo, "advisor", "Mit Gold und Worten gewinnt man Freunde. Mit Schwertern gewinnt man Land. Beides hat seinen Preis."),
                               (Aldo, "advisor", "Öffnet die Heerführung (Taste M), um Späher und Heere zu befehligen. Siedler gründen neue Orte.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Lernt 3 Herrscher kennen", g.S.factions.Count(f => f.knownToPlayer != 0), 3),
                    new Objective("Herrscht über 2 Siedlungen", g.CountSettlements(g.PlayerId), 2),
                    new Objective("Schließt einen Vertrag (Handel, Pakt, Bündnis)", g.Player.relations.Any(r => r.trade || r.status >= (int)DipStatus.Pact) ? 1 : 0, 1),
                },
                Outro = g => L((Aldo, "advisor", "Eine Botschaft, heimlich überbracht. Sie stammt von Flüchtlingen aus dem Osten:"),
                               ("Brief", "scroll", "'Kaiser Malrik nimmt uns alles. Die Ernte, die Söhne, das Saatgut. Wer sich wehrt, hängt am Galgen von Valdoran.'"),
                               (Aldo, "advisor", "Valdoran... die Hauptstadt des Kaisers. Noch sind wir ihm zu klein, um ihn zu kümmern.")),
                Reward = g => { g.Player.prestige += 25; g.RevealEmperorCapital(); }, RewardText = "+25 Ansehen, Lage von Valdoran",
            },
            // ---------------------------------------------------------------- 5 (neu)
            new Chapter
            {
                Title = "Kapitel 5", Subtitle = "Gilden und Handwerk",
                Intro = g => L((Gilda, "advisor", "Gilda, Meisterin der Schmiedegilde. Ich habe gehört, euer Ort hat Erz in den Hügeln?"),
                               (Gilda, "advisor", "Gebt mir eine Mine, eine Schmiede und einen Markt – und ich mache aus euren Bauern ein Volk von Handwerkern."),
                               (Aldo, "advisor", "Eisen und Waffen werden wir brauchen. Früher oder später.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Baut eine Eisenmine", g.CountBuildings(BType.Mine), 1),
                    new Objective("Baut eine Schmiede", g.CountBuildings(BType.Schmiede), 1),
                    new Objective("Baut einen Markt", g.CountBuildings(BType.Markt), 1),
                    new Objective("Lagert 20 Waffen", g.Player.res[(int)Res.Weapons], 20),
                },
                Outro = g => L((Gilda, "advisor", "Hört ihr das? Hämmer und Ambosse. Das ist der Klang eines Volkes, das sich nicht mehr ducken muss."),
                               (Aldo, "advisor", "Die kaiserlichen Steuereintreiber kommen inzwischen öfter. Sie fragen nach euch.")),
                Reward = g => { g.Player.res[(int)Res.Gold] += 120; g.Player.res[(int)Res.Iron] += 30; }, RewardText = "+120 Gold, +30 Eisen",
            },
            // ---------------------------------------------------------------- 6
            new Chapter
            {
                Title = "Kapitel 6", Subtitle = "Das Joch des Kaisers",
                Intro = g => L((Aldo, "advisor", "Der Kaiser presst die Fürsten aus, und die Fürsten pressen ihr Volk. Manche sind ihm treu, andere hassen ihn."),
                               (Aldo, "advisor", "Achtet auf den 'Druck des Kaiserreichs' bei den Aufgaben. Zahlt ihr den Zehnt, sinkt er. Trotzt ihr ihm, steigt er."),
                               (Bero, "knight", "Wir brauchen Mauern und eine Burg. Und Männer, die kämpfen können.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Steigt zur Grafschaft auf", g.Player.rank, 2),
                    new Objective("Baut eine Burg", g.CountBuildings(BType.Burg), 1),
                    new Objective("Gewinnt 2 Schlachten", g.S.battlesWon, 2),
                },
                Outro = g => L((Aldo, "advisor", "Euer Name wird in den Tavernen geflüstert. Die Unterdrückten schöpfen Hoffnung."),
                               (Malrik, "emperor", "Ein Graf, der sich für einen König hält? Ich habe größere Narren am Galgen gesehen.")),
                Reward = g => { g.Player.prestige += 40; g.Player.res[(int)Res.Gold] += 150; }, RewardText = "+40 Ansehen, +150 Gold",
            },
            // ---------------------------------------------------------------- 7
            new Chapter
            {
                Title = "Kapitel 7", Subtitle = "Der Bund der Freien",
                Intro = g => L((Aldo, "advisor", "Allein können wir Malrik nicht besiegen. Sucht Verbündete unter jenen, die den Kaiser fürchten – oder macht sie zu einem Teil eures Reiches."),
                               (Bero, "knight", "Und wir brauchen ein richtiges Heer. Ritter. Speere. Bogenschützen."),
                               (Aldo, "advisor", "Wann ihr euch erhebt, entscheidet ihr – in den Aufgaben oder der Reich-Übersicht. Doch wartet ihr zu lange, schlägt der Kaiser von selbst zu.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Steigt zum Fürstentum auf", g.Player.rank, 3),
                    new Objective("2 Verbündete ODER 6 Siedlungen", Math.Max(g.AlliesOf(g.PlayerId).Count() / 2f, g.CountSettlements(g.PlayerId) / 6f), 1),
                    new Objective("Unterhaltet 60 Soldaten", g.TotalSoldiers(g.PlayerId), 60),
                    new Objective("Erhebt euch gegen den Kaiser", g.S.story.Has("emperor_war") || g.S.story.emperorDefeated ? 1 : 0, 1),
                },
                Outro = g => L((Bero, "knight", "Die Würfel sind gefallen. Für das Volk!"),
                               (Aldo, "advisor", "Die kaiserlichen Städte stöhnen unter den Steuern. Befreien wir sie, jubeln uns ihre Bürger zu.")),
                Reward = g => { g.Player.prestige += 30; }, RewardText = "+30 Ansehen",
            },
            // ---------------------------------------------------------------- 8
            new Chapter
            {
                Title = "Kapitel 8", Subtitle = "Krone des Volkes",
                Intro = g => L((Aldo, "advisor", "Befreit kaiserliche Städte. Manche erheben sich auch von selbst, wenn man ihnen Gold für Waffen schickt."),
                               (Aldo, "advisor", "Und das Volk braucht eine Krone, hinter der es sich sammeln kann: Werdet König.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Steigt zum Königreich auf", g.Player.rank, 4),
                    new Objective("Befreit 2 kaiserliche Städte", g.S.story.flags.Count(f => f == "liberated"), 2),
                },
                Outro = g => L((Aldo, "advisor", "Ein König, vom Volk getragen. Malrik hat nur noch Valdoran und seine Garde."),
                               (Malrik, "emperor", "Kommt nur. Valdorans Mauern sind noch nie gefallen.")),
                Reward = g => { g.Player.prestige += 50; g.Player.res[(int)Res.Weapons] += 60; }, RewardText = "+50 Ansehen, +60 Waffen",
            },
            // ---------------------------------------------------------------- 9
            new Chapter
            {
                Title = "Kapitel 9", Subtitle = "Sturm auf Valdoran",
                Intro = g => L((Bero, "knight", "Valdoran. Doppelte Mauern, eine Burg, die kaiserliche Garde. Ohne Rammen kommen wir nicht hinein."),
                               (Aldo, "advisor", "Baut eine Belagerungswerkstatt. Sammelt euer größtes Heer. Und dann – beendet die Tyrannei.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Erobert Valdoran", g.S.story.emperorDefeated ? 1 : 0, 1),
                },
                Outro = g => L((Aldo, "advisor", "Valdoran ist gefallen! Doch Malrik selbst ist entkommen – mit einem Schiff, im Schutz der Nacht."),
                               (Kael, "knight", "Ich bin Kael, einst General des Kaisers. Ein Teil der Garde hört noch auf mich... und auf Malriks Gold."),
                               (Kael, "knight", "Ihr habt eine Stadt erobert, kein Reich. Wir werden sehen, wem das Land wirklich folgt.")),
                Reward = g => { g.Player.prestige += 100; g.Player.res[(int)Res.Gold] += 600; }, RewardText = "+100 Ansehen, +600 Gold",
            },
            // ---------------------------------------------------------------- 10 (neu)
            new Chapter
            {
                Title = "Kapitel 10", Subtitle = "Der Thronstreit",
                Intro = g => L((Aldo, "advisor", "General Kael hält mit 'Malriks Getreuen' die letzten kaiserlichen Festungen. Ihr könnt sie bezwingen – oder mit Gold und Geduld Frieden schließen."),
                               (Aldo, "advisor", "Die freien Städte beobachten euch genau. Seid ihr ein neuer Tyrann – oder etwas Besseres?")),
                OnStart = g => g.SpawnLoyalists(),
                Objectives = g => new List<Objective>
                {
                    new Objective("Bezwingt Malriks Getreue oder schließt Frieden", g.LoyalistsResolved() ? 1 : 0, 1),
                    new Objective("Steigt zum Kaiserreich auf", g.Player.rank, 5),
                },
                Outro = g => L((Aldo, "advisor", "Ein Kaiser – doch einer, der für sein Volk regiert. Die Häfen sind wieder sicher."),
                               (Bero, "knight", "Kapitäne berichten von Ländern jenseits des Meeres. Nordlande. Südreich. Ein Inselbund.")),
                Reward = g => { g.S.seafaring = true; g.Player.prestige += 60; }, RewardText = "Seefahrt freigeschaltet, +60 Ansehen",
            },
            // ---------------------------------------------------------------- 11
            new Chapter
            {
                Title = "Kapitel 11", Subtitle = "Neue Ufer",
                Intro = g => L((Aldo, "advisor", "Mit einem Hafen können eure Heere und Siedler über das Meer fahren. Wählt eine Armee und zielt aufs Wasser."),
                               (Aldo, "advisor", "Die Herrscher dort kennen euch nicht. Handelt, verbündet euch – oder erobert.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Baut einen Hafen", g.CountBuildings(BType.Hafen), 1),
                    new Objective("Entdeckt einen anderen Kontinent", g.ContinentsSeen(), 1),
                    new Objective("Siedlung oder Verbündeter in Übersee", g.OverseasFootholds(), 1),
                },
                Outro = g => L((Aldo, "advisor", "Euer Banner weht nun jenseits des Meeres."),
                               ("Brief", "scroll", "'Malrik lebt. Er hat sich im Südreich eine Festung gekauft und wirbt Söldner an. Er will zurück.'")),
                Reward = g => { g.Player.prestige += 80; }, RewardText = "+80 Ansehen",
            },
            // ---------------------------------------------------------------- 12 (neu)
            new Chapter
            {
                Title = "Kapitel 12", Subtitle = "Der Kaiser im Exil",
                Intro = g => L((Malrik, "emperor", "Ihr dachtet, ihr wärt mich los? Gold kauft Mauern, und Mauern kaufen Zeit."),
                               (Aldo, "advisor", "Seine Exilfestung liegt im Südreich. Bringt ein Heer mit Rammen übers Meer – oder gewinnt die Emire als Verbündete.")),
                OnStart = g => g.SpawnExile(),
                Objectives = g => new List<Objective>
                {
                    new Objective("Erobert Malriks Exilfestung", g.ExileTaken() ? 1 : 0, 1),
                },
                Outro = g => L((Malrik, "emperor", "...So endet es also. Nicht durch einen Kaiser – durch ein Dorf."),
                               (Aldo, "advisor", "Malrik ist gefangen. Die Tyrannei ist endgültig vorbei.")),
                Reward = g => { g.Player.prestige += 150; g.Player.res[(int)Res.Gold] += 800; }, RewardText = "+150 Ansehen, +800 Gold",
            },
            // ---------------------------------------------------------------- 13
            new Chapter
            {
                Title = "Kapitel 13", Subtitle = "Herrscher der Welt",
                Intro = g => L((Aldo, "advisor", "Eine letzte Aufgabe: Eint die Kontinente. Ein Kontinent gilt als geeint, wenn jedes Reich dort euch gehört oder euer Verbündeter ist.")),
                Objectives = g => new List<Objective>
                {
                    new Objective("Mittland geeint", g.ContinentUnited(0), 1),
                    new Objective("Nordlande geeint", g.ContinentUnited(1), 1),
                    new Objective("Südreich geeint", g.ContinentUnited(2), 1),
                    new Objective("Inselbund geeint", g.ContinentUnited(3), 1),
                },
                Outro = g => L((Aldo, "advisor", "Es ist vollbracht. Von einem Dorf mit zwei Hütten zum Herrscher der Welt."),
                               (Aldo, "advisor", "Ich lege die Feder nieder. Doch euer Reich lebt weiter – regiert es weise.")),
                Reward = g => { g.Player.prestige += 500; }, RewardText = "Sieg!",
            },
        };

        /// <summary>Kapitel, ab dem der Spieler den Aufstand gegen den Kaiser selbst auslösen kann.</summary>
        public const int ChapterUprising = 6;

        public Chapter CurrentChapter => S.story.chapter < Chapters.Length ? Chapters[S.story.chapter] : null;
        public bool StoryFinished => S.story.chapter >= Chapters.Length;

        void InitStory()
        {
            S.story.chapter = 0;
            S.story.chapterIntroShown = false;
            S.story.peaceUntil = 150; // ruhiger Start
        }

        /// <summary>Zeigt das Kapitel-Intro sofort (z.B. direkt nach Spielstart).</summary>
        public void StoryTick() => StoryDay();

        // ==================================================================
        //  Ruhephasen und Druck des Kaiserreichs
        // ==================================================================
        /// <summary>Friedenszeit: Niemand greift den Spieler an (nach Spielstart und nach jedem Kapitel).</summary>
        public bool InPeace => S.day < S.story.peaceUntil;
        public int PeaceDaysLeft => Math.Max(0, S.story.peaceUntil - S.day);

        public bool CanChallengeEmperor => S.story.chapter >= ChapterUprising && !S.story.Has("emperor_war") && Emperor != null && Emperor.alive;

        /// <summary>Der Spieler erhebt sich aus eigenem Entschluss gegen den Kaiser.</summary>
        public void ChallengeEmperor()
        {
            if (!CanChallengeEmperor) return;
            Say(Bero, "knight", "Ihr habt es entschieden. Die Glocken läuten, die Banner werden gehisst.");
            Say(Malrik, "emperor", "So sei es. Ich werde euer Dorf in Asche legen und Salz in die Felder streuen.");
            S.story.pressure = 100;
            StartEmperorWar();
            Player.prestige += 20;
        }

        public void ChangePressure(float d)
        {
            if (S.story.Has("emperor_war")) return;
            S.story.pressure = Math.Max(0, Math.Min(100, S.story.pressure + d));
        }

        void PressureDay()
        {
            var emp = Emperor;
            if (emp == null || !emp.alive || S.story.Has("emperor_war")) return;
            var p = Player;
            if (p.rank < 1 || p.continent != 0) return;
            float rise = 0.025f + 0.025f * p.rank + CountSettlements(p.id) * 0.004f;
            if (InPeace) rise *= 0.3f;
            float cap = S.story.chapter >= ChapterUprising ? 100f : 65f;
            float before = S.story.pressure;
            S.story.pressure = Math.Min(cap, S.story.pressure + rise);
            float now = S.story.pressure;
            if (before < 40 && now >= 40) Say(Aldo, "advisor", "Die Spione des Kaisers zählen unsere Speicher. Der Druck aus Valdoran wächst.");
            if (before < 80 && now >= 80) Say(Malrik, "emperor", "Meine Geduld ist nicht unendlich. Beugt das Knie – oder ich beuge es euch.");
            if (now >= 100 && S.story.chapter >= ChapterUprising)
            {
                Say(Malrik, "emperor", "Genug! Ihr habt meinen Zehnt verweigert und meine Vasallen verführt. Ich erkläre euch zum Verräter am Reich!");
                StartEmperorWar();
                return;
            }
            // Strafexpedition (ohne Krieg): ab mittlerem Druck, nicht in Friedenszeiten
            if (now >= 50 && !InPeace && S.day >= S.story.nextPunish && !Offline)
            {
                S.story.nextPunish = S.day + 80;
                var target = SettlementsOf(p.id).OrderBy(s => SettlementsOf(emp.id).Select(e => Dist(e.x, e.y, s.x, s.y)).DefaultIfEmpty(999).Min()).FirstOrDefault();
                if (target != null) QueueEvent(new PendingEvent { id = "punish", a = target.id, v = (float)Math.Round(40 + p.res[(int)Res.Gold] * 0.15f) });
            }
        }

        // ==================================================================
        //  Hinweise: Meister Aldo erklärt Neues, sobald es zum ersten Mal vorkommt
        // ==================================================================
        public static bool HintsEnabled = true;

        public void Hint(string key, string text)
        {
            if (!HintsEnabled || Headless) return;
            string flag = "hint_" + key;
            if (S.story.Has(flag)) return;
            S.story.Set(flag);
            Say(Aldo, "advisor", text);
        }

        void HintsDay()
        {
            if (!HintsEnabled || Headless || Offline) return;
            var p = Player;
            var mine = SettlementsOf(p.id).ToList();
            if (mine.Count == 0) return;
            if (S.day > 3 && ArmiesOf(p.id).Any(a => a.units.Sum() > 0 && a.units[(int)UType.Spaeher] < a.units.Sum()))
                Hint("first_army", "Euer erstes Heer! Klickt es an und dann mit Rechtsklick auf ein Ziel (am Tablet: antippen). Am Mauszeiger steht, was passieren wird. Alle Heere findet ihr in der Heerführung (M).");
            if (Season == Season.Winter && S.day % Calendar.DaysPerSeason < 3)
                Hint("winter", "Der Winter ist da. Die Felder tragen kaum noch – jetzt zeigt sich, ob die Speicher voll sind. Fischer fangen auch im Winter.");
            if (p.res[(int)Res.Food] < TotalPop(p.id) * 0.3f && Delta(p.id)[(int)Res.Food] < 0)
                Hint("hunger", "Die Speicher leeren sich! Baut Bauernhöfe oder Fischerhütten, oder stellt die Rationen in der Dorf-Übersicht auf 'Knapp'.");
            var sad = mine.FirstOrDefault(s => s.happiness < 30);
            if (sad != null) Hint("unhappy", $"In {sad.name} murren die Leute. Senkt die Steuern, baut eine Taverne oder einen Brunnen. Fahrt über die Zufriedenheit, um die Gründe zu sehen.");
            if (p.rank >= 1) Hint("edicts", "Als Städtegemeinde dürft ihr Erlasse verkünden: in der Reich-Übersicht (R) unter 'Erlasse'. Jeder Rang erlaubt einen Erlass mehr – wählt, was zu eurem Reich passt.");
            if (mine.Count == 1 && mine[0].pop >= 34 && Ranks.Get(p.rank).MaxSettlements > 1)
                Hint("settlers", "Euer Dorf ist groß genug, um Siedler auszusenden. In der Dorf-Übersicht: 'Siedler aussenden' und dann einen Ort auf der Karte wählen – am besten mit Wald, Wasser und fruchtbarem Land.");
            if (CountBuildings(BType.Markt) > 0) Hint("market", "Der Markt ist fertig! Im Reiter 'Markt' eures Dorfes kauft und verkauft ihr Waren. Die Preise schwanken – kauft billig, verkauft teuer.");
            if (CountBuildings(BType.Bauernhof) >= 3) Hint("mill", "Drei Bauernhöfe! Eine Windmühle in derselben Siedlung steigert ihre Ernte um ein Fünftel.");
            if (CountBuildings(BType.Turnierplatz) > 0) Hint("tournament", "Der Turnierplatz steht. In der Reich-Übersicht (R) könnt ihr nun Ritterturniere ausrichten: Ansehen, gute Laune und Freunde unter den Fürsten.");
            if (S.story.pressure > 12) Hint("pressure", "Seht ihr die Leiste 'Druck' unter den Aufgaben? Sie zeigt, wie sehr der Kaiser euch beobachtet. Zahlt ihr den Zehnt, sinkt sie. Trotzt ihr ihm, steigt sie.");
            if (AtWarCount(p.id) > 0) Hint("war", "Krieg! Feindliche Heere sind rot markiert. Mauern, Garnisonen und das Bürgeraufgebot schützen eure Städte. Friedensangebote macht ihr in der Diplomatie (G).");
            if (S.day % 2 == 0)
                foreach (var poi in S.pois)
                {
                    if (!IsDiscovered(poi.x, poi.y)) continue;
                    if (poi.type == 0 && !poi.done) Hint("ruin", "Dort liegen alte Ruinen! Schickt ein Heer oder einen Späher hin: Wählt es aus und klickt mit Rechtsklick auf die Ruine. Vielleicht findet ihr Gold – oder Schlimmeres.");
                    if (poi.type == 1 && !poi.done) Hint("camp", "Ein Räuberlager! Solange es steht, überfallen die Räuber eure Höfe in der Nähe, und die Leute fürchten sich. Hebt es mit einem Heer aus – es gibt Beute.");
                }
        }

        void StoryDay()
        {
            PressureDay();
            HintsDay();
            var ch = CurrentChapter;
            if (ch == null) return;
            if (!S.story.chapterIntroShown)
            {
                S.story.chapterIntroShown = true;
                Say(ch.Title, "book", ch.Subtitle);
                foreach (var l in ch.Intro(this)) Say(l.Speaker, l.Portrait, l.Text);
                ch.OnStart?.Invoke(this);
            }
            var objs = ch.Objectives(this);
            if (objs.All(o => o.Done))
            {
                foreach (var l in ch.Outro(this)) Say(l.Speaker, l.Portrait, l.Text);
                ch.Reward?.Invoke(this);
                Notify($"{ch.Title} abgeschlossen: {ch.Subtitle}. Belohnung: {ch.RewardText}", 4);
                S.story.chapter++;
                S.story.chapterIntroShown = false;
                // Atempause nach jedem Kapitel – im Krieg gegen den Kaiser kürzer
                int rest = S.story.Has("emperor_war") && !S.story.emperorDefeated ? 20 : 45;
                S.story.peaceUntil = Math.Max(S.story.peaceUntil, S.day + rest);
                if (!Offline) Notify($"Friedenszeit: {rest} Tage lang wird euch niemand angreifen.", 1);
            }
        }

        // ==================================================================
        //  Malriks Getreue und das Exil
        // ==================================================================
        public void SpawnLoyalists()
        {
            if (S.story.loyalistsId >= 0) return;
            var cands = S.settlements.Where(st => { var f = F(st.faction); return f != null && f.isFree && st.originalFaction >= 0 && F(st.originalFaction) != null && F(st.originalFaction).isEmperor; })
                                     .OrderByDescending(st => st.pop).Take(2).ToList();
            var lo = new FactionData
            {
                id = NewId(), name = "Malriks Getreue", leader = "General Kael", continent = 0, culture = 0, color = 12,
                aggression = 0.8f, honor = 0.4f, ambition = 0.7f, loyalty = 1f, greed = 0.5f, rank = 3,
                res = new float[] { 400, 300, 200, 60, 500, 80 }, knownToPlayer = 1,
            };
            AddFaction(lo); InitRelationsFor(lo);
            S.story.loyalistsId = lo.id;
            foreach (var st in cands)
            {
                TransferSettlement(st, lo.id);
                st.originalFaction = lo.id;
                st.garrison[(int)UType.Speer] += 25; st.garrison[(int)UType.Bogen] += 15; st.garrison[(int)UType.Ritter] += 8;
                if (st.buildings[(int)BType.Mauer] == 0) st.buildings[(int)BType.Mauer] = 1;
            }
            if (cands.Count == 0) { lo.alive = false; return; }
            ModRel(lo.id, PlayerId, -60);
            SetStatus(lo.id, PlayerId, DipStatus.War);
            Notify("Malriks Getreue unter General Kael erklären euch den Krieg!", 3);
        }

        public bool LoyalistsResolved()
        {
            var lo = F(S.story.loyalistsId);
            if (lo == null || !lo.alive) return S.story.loyalistsId >= 0;
            return Status(PlayerId, lo.id) != DipStatus.War && S.day - Rel(PlayerId, lo.id).since > 5;
        }

        public void SpawnExile()
        {
            if (S.story.exileId >= 0) return;
            // freien Bauplatz im Südreich suchen
            int bx = -1, by = -1; float best = float.MinValue;
            for (int y = 4; y < WorldMap.H - 4; y += 2)
                for (int x = 4; x < WorldMap.W - 4; x += 2)
                {
                    int i = WorldMap.Idx(x, y);
                    if (Map.cont[i] != 2 || !Map.Buildable(i) || Map.owner[i] >= 0) continue;
                    if (S.settlements.Any(s => Dist(s.x, s.y, x, y) < 9)) continue;
                    float score = (Map.coast[i] ? 5 : 0) + Map.CountAround(x, y, 3, j => Map.biome[j] == Biome.Hills || Map.biome[j] == Biome.Mountain) - (float)Rng.NextDouble();
                    if (score > best) { best = score; bx = x; by = y; }
                }
            var ex = new FactionData
            {
                id = NewId(), name = "Malriks Exilhof", leader = "Malrik der Vertriebene", continent = 2, culture = 0, color = 13,
                aggression = 0.9f, honor = 0.1f, ambition = 1f, loyalty = 1f, greed = 1f, rank = 3,
                res = new float[] { 600, 400, 300, 80, 900, 120 }, knownToPlayer = 1,
            };
            AddFaction(ex); InitRelationsFor(ex);
            if (bx < 0) { ex.alive = false; S.story.exileId = 0; return; }
            var st = new SettlementData { id = NewId(), faction = ex.id, x = bx, y = by, pop = 160, name = "Schwarzfels", founded = S.day, originalFaction = ex.id, happiness = 45 };
            st.buildings[(int)BType.Wohnhaus] = 14; st.buildings[(int)BType.Mauer] = 2; st.buildings[(int)BType.Burg] = 1;
            st.buildings[(int)BType.Bauernhof] = 4; st.buildings[(int)BType.Kaserne] = 1; st.buildings[(int)BType.Archerie] = 1;
            st.garrison[(int)UType.Speer] = 45; st.garrison[(int)UType.Bogen] = 30; st.garrison[(int)UType.Ritter] = 15; st.garrison[(int)UType.Miliz] = 20;
            AddSettlement(st);
            ex.capital = st.id;
            S.story.exileId = st.id;
            RecomputeTerritory();
            Reveal(bx, by, 6);
            SetStatus(ex.id, PlayerId, DipStatus.War);
            ModRel(ex.id, PlayerId, -80);
        }

        public bool ExileTaken()
        {
            var st = St(S.story.exileId);
            if (S.story.exileId == 0) return true; // kein Platz gefunden
            return st != null && st.faction == PlayerId;
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
                Say(Aldo, "advisor", "Das schwarze Banner mit dem eisernen Adler – das Kaiserreich Valdor. Seid vorsichtig.");
            }
            else if (!S.story.Has("met_first"))
            {
                S.story.Set("met_first");
                Say(Aldo, "advisor", $"Wir sind {f.leader} begegnet ({f.name}). Öffnet die Diplomatie (D), um mit ihnen zu verhandeln.");
            }
        }

        void OnBuildingFinished(SettlementData s, BType t)
        {
            if (!F(s.faction).isPlayer) return;
            if (t == BType.Hafen && S.seafaring && !S.story.Has("first_harbor"))
            {
                S.story.Set("first_harbor");
                Say(Aldo, "advisor", "Der Hafen ist fertig! Eure Armeen können nun über das Meer segeln.");
            }
        }

        void OnRankUp(int rank)
        {
            var rd = Ranks.Get(rank);
            Say(Aldo, "advisor", $"Es ist vollbracht: Ihr seid nun {rd.Ruler} – {rd.Name}. {rd.Desc}");
            if (rank == (int)Rank.Kaiserreich) Say(Aldo, "advisor", "Ein Kaiser – doch einer, der für sein Volk regiert. Möge die Geschichte gnädig urteilen.");
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
