using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    public class EventView
    {
        public string Title, Text, Icon;
        public List<string> Options = new List<string>();
        public List<bool> Enabled = new List<bool>();
        public void Opt(string text, bool enabled = true) { Options.Add(text); Enabled.Add(enabled); }
    }

    /// <summary>Zufallsereignisse und Angebote anderer Herrscher – jeweils mit Entscheidungen.</summary>
    public partial class Game
    {
        public PendingEvent CurrentEvent => S.events.Count > 0 ? S.events[0] : null;

        public void QueueEvent(PendingEvent e)
        {
            if (Offline || Headless && e.id != "test") { if (Headless) AutoResolve(e); return; }
            if (S.events.Count >= 5) return;
            if (S.events.Any(x => x.id == e.id && x.a == e.a)) return;
            S.events.Add(e);
        }

        void AutoResolve(PendingEvent e)
        {
            // In Tests: erste Option wählen
            S.events.Add(e);
            ResolveEvent(0);
        }

        void EventsDay()
        {
            var p = Player;
            var emp = Emperor;
            // Kaiserlicher Zehnt
            if (emp != null && emp.alive && p.rank >= 1 && !S.story.Has("emperor_war") && S.day >= S.nextTaxDay && p.continent == 0)
            {
                S.nextTaxDay = S.day + 45;
                float amount = (20 + p.res[(int)Res.Gold] * 0.15f + TotalPop(p.id) * 0.2f) * (1f + S.story.pressure / 100f);
                QueueEvent(new PendingEvent { id = "tax", a = emp.id, v = (float)Math.Round(amount) });
            }
            // Aufstände im Kaiserreich, sobald der Spieler gegen den Kaiser kämpft
            if (emp != null && emp.alive && S.story.Has("emperor_war") && Rng.NextDouble() < 0.025)
            {
                var cand = SettlementsOf(emp.id).Where(s => !s.isCapitalOfEmperor && s.garrison.Sum() < 25 &&
                    SettlementsOf(p.id).Any(ps => Dist(ps.x, ps.y, s.x, s.y) < 45)).OrderBy(s => s.happiness).FirstOrDefault();
                if (cand != null) QueueEvent(new PendingEvent { id = "uprising", a = cand.id });
            }

            if (S.day < S.nextEventDay) return;
            S.nextEventDay = S.day + 14 + Rng.Next(16);
            var pool = new List<string> { "harvest", "merchant", "bandits", "festival", "traveler", "fire", "knight" };
            var mySet = SettlementsOf(p.id).ToList();
            if (mySet.Any(s => s.buildings[(int)BType.Brunnen] == 0 && s.pop > 40)) pool.Add("plague");
            if (mySet.Any(s => s.buildings[(int)BType.Mine] > 0)) pool.Add("treasure");
            if (mySet.Any(s => s.happiness < 35)) pool.Add("unrest");
            if (emp != null && emp.alive && p.continent == 0) { pool.Add("refugees"); pool.Add("refugees"); }
            if (Season == Season.Sommer) pool.Add("drought");
            if (Season == Season.Winter) pool.Add("wolves");
            string id = pool[Rng.Next(pool.Count)];
            var target = mySet.Count > 0 ? mySet[Rng.Next(mySet.Count)] : null;
            if (id == "unrest") target = mySet.OrderBy(s => s.happiness).First();
            if (id == "plague") target = mySet.Where(s => s.buildings[(int)BType.Brunnen] == 0).OrderByDescending(s => s.pop).FirstOrDefault() ?? target;
            QueueEvent(new PendingEvent { id = id, a = target != null ? target.id : -1 });
        }

        public EventView Describe(PendingEvent e)
        {
            var v = new EventView { Icon = "scroll" };
            var p = Player;
            var s = St(e.a);
            var f = F(e.a);
            float gold = p.res[(int)Res.Gold];
            string sn = s != null ? s.name : "eurem Dorf";
            switch (e.id)
            {
                case "border": BorderView(e, v); break;
                case "nomads": NomadsView(e, v); break;
                case "harvest":
                    v.Title = "Reiche Ernte"; v.Icon = "food";
                    v.Text = $"Die Felder um {sn} tragen in diesem Jahr außergewöhnlich gut. Die Scheunen sind voll.";
                    v.Opt("Wunderbar! (+60 Nahrung)");
                    v.Opt("Ein Erntefest feiern (+15 Zufriedenheit, +20 Nahrung)");
                    break;
                case "merchant":
                    v.Title = "Fahrender Händler"; v.Icon = "market";
                    v.Text = "Ein Händler mit schwer beladenen Wagen bietet Eisen und Waffen feil – zu fairen Preisen.";
                    v.Opt("30 Eisen für 60 Gold", gold >= 60);
                    v.Opt("10 Waffen für 50 Gold", gold >= 50);
                    v.Opt("Kein Interesse");
                    break;
                case "refugees":
                    v.Title = "Flüchtlinge aus dem Kaiserreich"; v.Icon = "people";
                    v.Text = "Abgemagerte Familien stehen vor den Toren. Sie fliehen vor den Steuereintreibern des Kaisers. 'Bitte, Herr, gebt uns Obdach!'";
                    v.Opt("Aufnehmen (+12 Bewohner, -30 Nahrung, Kaiser verärgert)");
                    v.Opt("Mit Proviant weiterschicken (-15 Nahrung, +Ansehen)");
                    v.Opt("Abweisen");
                    break;
                case "bandits":
                    v.Title = "Räuberbande"; v.Icon = "sword";
                    v.Text = $"Eine Räuberbande lauert an der Straße nach {sn} und überfällt Bauern. Ihr Anführer verlangt Wegzoll.";
                    v.Opt("40 Gold zahlen", gold >= 40);
                    v.Opt("Die Garnison schicken (Kampf)");
                    v.Opt("Nichts tun (Händler bleiben weg)");
                    break;
                case "plague":
                    v.Title = "Fieber in " + sn; v.Icon = "skull";
                    v.Text = "Ein Fieber breitet sich aus. Das Wasser ist schlecht. Ein Heiler aus dem Kloster bietet Hilfe an.";
                    v.Opt("Heiler bezahlen (50 Gold)", gold >= 50);
                    v.Opt("Beten und hoffen");
                    break;
                case "festival":
                    v.Title = "Johannisfeuer"; v.Icon = "smile";
                    v.Text = "Das Volk möchte ein großes Fest mit Musik, Gauklern und Bier feiern.";
                    v.Opt("Fest ausrichten (-40 Gold, -40 Nahrung, +Zufriedenheit)", gold >= 40 && p.res[0] >= 40);
                    v.Opt("Lieber sparen");
                    break;
                case "traveler":
                    v.Title = "Ein Kartograph"; v.Icon = "map";
                    v.Text = "Ein reisender Kartograph bietet euch seine Karten der Umgebung an.";
                    v.Opt("Karten kaufen (30 Gold)", gold >= 30);
                    v.Opt("Ablehnen");
                    break;
                case "fire":
                    v.Title = "Feuer!"; v.Icon = "fire";
                    v.Text = $"In {sn} ist ein Lagerschuppen in Brand geraten.";
                    v.Opt("Löschen! (verliert 25% Holz)");
                    break;
                case "knight":
                    v.Title = "Fahrende Ritter"; v.Icon = "knight";
                    v.Text = "Drei fahrende Ritter bieten ihre Dienste an – gegen ordentlichen Sold.";
                    v.Opt("Anheuern (90 Gold)", gold >= 90 && s != null);
                    v.Opt("Ablehnen");
                    break;
                case "treasure":
                    v.Title = "Silberader"; v.Icon = "gold";
                    v.Text = "Die Bergleute sind auf eine Silberader gestoßen!";
                    v.Opt("Hervorragend (+120 Gold)");
                    break;
                case "unrest":
                    v.Title = "Murren in " + sn; v.Icon = "angry";
                    v.Text = "Die Bürger sind unzufrieden. Auf dem Marktplatz wird gegen euch gehetzt.";
                    v.Opt("Brot verteilen (-50 Nahrung, +Zufriedenheit)", p.res[0] >= 50);
                    v.Opt("Steuern für eine Weile senken");
                    v.Opt("Rädelsführer einsperren (-Zufriedenheit, +Ordnung)");
                    break;
                case "drought":
                    v.Title = "Dürre"; v.Icon = "sun";
                    v.Text = "Ein heißer Sommer. Die Brunnen sinken, die Ernte verdorrt.";
                    v.Opt("Vorräte rationieren (-40 Nahrung)");
                    break;
                case "wolves":
                    v.Title = "Wölfe im Winter"; v.Icon = "wolf";
                    v.Text = "Hungrige Wölfe reißen Vieh am Waldrand.";
                    v.Opt("Jäger aussenden (-15 Gold)", gold >= 15);
                    v.Opt("Vieh in die Ställe (-20 Nahrung)");
                    break;
                case "tax":
                    v.Title = "Der kaiserliche Zehnt"; v.Icon = "crown";
                    v.Text = $"Ein Steuereintreiber in schwarzem Samt verliest ein Dekret: 'Seine Majestät Kaiser Malrik fordert den Zehnt: {(int)e.v} Gold.'";
                    v.Opt($"Zahlen ({(int)e.v} Gold)", gold >= e.v);
                    v.Opt("Verweigern (Kaiser erzürnt, Volk jubelt)");
                    break;
                case "camp_raid":
                    v.Title = "Räuber!"; v.Icon = "skull";
                    v.Text = $"Räuber aus dem Lager in den Wäldern überfallen die Höfe um {sn}. Ihr Anführer lässt ausrichten: '{(int)e.v} Gold, und wir ziehen weiter.' Solange das Lager steht, kommen sie wieder – hebt es mit einem Heer aus!";
                    v.Opt($"Zahlen ({(int)e.v} Gold)", gold >= e.v);
                    v.Opt("Plündern lassen (Nahrung und Holz gehen verloren)");
                    v.Opt("Die Garnison schicken (Kampf)", s != null && s.garrison.Sum() > 0);
                    break;
                case "punish":
                    v.Title = "Kaiserliche Strafexpedition"; v.Icon = "eagle";
                    v.Text = $"Reiter mit dem eisernen Adler stehen vor {sn}: 'Seine Majestät ist unzufrieden mit euch. Zahlt {(int)e.v} Gold Buße – oder wir nehmen uns, was wir finden.'";
                    v.Opt($"Buße zahlen ({(int)e.v} Gold, senkt den Druck)", gold >= e.v);
                    v.Opt("Plündern lassen (Nahrung und Holz gehen verloren)");
                    v.Opt("Mit der Garnison vertreiben (riskant, erhöht den Druck)", s != null && s.garrison.Sum() > 0);
                    break;
                case "uprising":
                    v.Title = "Aufstand in " + sn; v.Icon = "fist";
                    v.Text = $"Bürger aus {sn} schicken einen Boten: 'Wir haben genug vom Kaiser! Schickt uns Gold für Waffen, und wir öffnen euch die Tore.'";
                    v.Opt("Den Aufstand finanzieren (120 Gold)", gold >= 120);
                    v.Opt("Zu riskant");
                    break;
                case "claim":
                    v.Title = "Gebietsanspruch"; v.Icon = "scroll";
                    v.Text = $"{f?.leader} von {f?.name} erhebt Anspruch auf {St(e.b)?.name}: 'Diese Ländereien gehörten einst meinen Ahnen!'";
                    v.Opt("Mit Geschenken beschwichtigen (60 Gold)", gold >= 60);
                    v.Opt("Zurückweisen (Beziehung sinkt)");
                    break;
                case "peace_offer":
                    v.Title = "Friedensangebot"; v.Icon = "dove";
                    v.Text = $"Ein Gesandter von {f?.name} überbringt ein Angebot: Frieden und 30 Tage Waffenruhe.";
                    v.Opt("Frieden annehmen");
                    v.Opt("Weiterkämpfen");
                    break;
                case "alliance_offer":
                    v.Title = "Bündnisangebot"; v.Icon = "handshake";
                    v.Text = $"{f?.leader} schlägt ein Bündnis vor: gegenseitiger Beistand in Kriegszeiten.";
                    v.Opt("Bündnis eingehen");
                    v.Opt("Höflich ablehnen");
                    break;
                case "trade_offer":
                    v.Title = "Handelsangebot"; v.Icon = "market";
                    v.Text = $"Händler von {f?.name} möchten mit euren Märkten handeln.";
                    v.Opt("Handelsabkommen schließen");
                    v.Opt("Ablehnen");
                    break;
                case "tribute_demand":
                    v.Title = "Tributforderung"; v.Icon = "sword";
                    v.Text = $"{f?.leader} droht: 'Zahlt {(int)e.v} Gold, oder wir holen es uns mit dem Schwert.'";
                    v.Opt($"Zahlen ({(int)e.v} Gold)", gold >= e.v);
                    v.Opt("Niemals! (Krieg droht)");
                    break;
                case "help_request":
                    v.Title = "Hilferuf"; v.Icon = "horn";
                    v.Text = $"{f?.leader} bittet um Beistand im Krieg gegen {F(e.b)?.name}. Ein Interessenkonflikt: Helft ihr, macht ihr euch Feinde.";
                    v.Opt($"Krieg gegen {F(e.b)?.name} erklären");
                    v.Opt("Gold statt Soldaten schicken (40 Gold)", gold >= 40);
                    v.Opt("Neutral bleiben");
                    break;
                case "marriage":
                    v.Title = "Eine Hochzeit"; v.Icon = "heart";
                    v.Text = $"{f?.leader} schlägt eine Heirat zwischen euren Häusern vor. Eine Mitgift wäre inbegriffen.";
                    v.Opt("Annehmen (+Beziehung, +Gold, +Ansehen)");
                    v.Opt("Ablehnen");
                    break;
                default:
                    v.Title = "?"; v.Text = e.id; v.Opt("OK");
                    break;
            }
            return v;
        }

        public void ResolveEvent(int option)
        {
            var e = CurrentEvent;
            if (e == null) return;
            S.events.RemoveAt(0);
            var p = Player;
            var s = St(e.a);
            var f = F(e.a);
            var res = p.res;
            void AllHappy(float v) { foreach (var x in SettlementsOf(p.id)) x.happiness = Math.Min(100, x.happiness + v); }
            switch (e.id)
            {
                case "border": BorderResolve(e, option); break;
                case "nomads": NomadsResolve(e, option); break;
                case "harvest":
                    if (option == 0) res[0] += 60; else { res[0] += 20; AllHappy(15); }
                    break;
                case "merchant":
                    if (option == 0 && res[4] >= 60) { res[4] -= 60; res[3] += 30; }
                    if (option == 1 && res[4] >= 50) { res[4] -= 50; res[5] += 10; }
                    break;
                case "refugees":
                    if (option == 0 && s != null) { s.pop += 12; res[0] = Math.Max(0, res[0] - 30); var emp = Emperor; if (emp != null) ModRel(p.id, emp.id, -6); p.prestige += 3; }
                    else if (option == 1) { res[0] = Math.Max(0, res[0] - 15); p.prestige += 5; }
                    else AllHappy(-3);
                    break;
                case "bandits":
                    if (option == 0 && res[4] >= 40) res[4] -= 40;
                    else if (option == 1 && s != null)
                    {
                        float str = Strength(s.garrison);
                        if (str > 35 || Rng.NextDouble() < str / 40f) { p.prestige += 6; res[4] += 25; Notify("Die Räuber wurden vertrieben! Ihre Beute gehört euch.", 1); }
                        else { for (int u = 0; u < Units.Count; u++) s.garrison[u] = s.garrison[u] * 2 / 3; s.pop -= 3; Notify("Eure Männer wurden zurückgeschlagen.", 2); }
                    }
                    else { res[4] = Math.Max(0, res[4] - 20); if (s != null) s.happiness -= 8; }
                    break;
                case "plague":
                    if (s == null) break;
                    if (option == 0 && res[4] >= 50) { res[4] -= 50; s.pop *= 0.97f; }
                    else { s.pop *= 0.85f; s.happiness -= 10; }
                    break;
                case "festival":
                    if (option == 0 && res[4] >= 40 && res[0] >= 40) { res[4] -= 40; res[0] -= 40; AllHappy(18); p.prestige += 4; }
                    break;
                case "traveler":
                    if (option == 0 && res[4] >= 30)
                    {
                        res[4] -= 30;
                        foreach (var x in SettlementsOf(p.id)) Reveal(x.x, x.y, 26);
                    }
                    break;
                case "fire":
                    res[1] *= 0.75f;
                    break;
                case "knight":
                    if (option == 0 && res[4] >= 90 && s != null) { res[4] -= 90; s.garrison[(int)UType.Ritter] += 3; }
                    break;
                case "treasure":
                    res[4] += 120;
                    break;
                case "unrest":
                    if (s == null) break;
                    if (option == 0 && res[0] >= 50) { res[0] -= 50; s.happiness += 20; }
                    else if (option == 1) { s.taxRate = Math.Max(0, s.taxRate - 0.05f); s.happiness += 10; }
                    else { s.happiness -= 5; s.pop -= 2; p.prestige -= 3; }
                    break;
                case "drought":
                    res[0] = Math.Max(0, res[0] - 40);
                    break;
                case "wolves":
                    if (option == 0 && res[4] >= 15) res[4] -= 15; else res[0] = Math.Max(0, res[0] - 20);
                    break;
                case "tax":
                    if (option == 0 && res[4] >= e.v) { res[4] -= e.v; ChangePressure(-15); if (f != null) { ModRel(p.id, f.id, 6); f.res[4] += e.v; } }
                    else
                    {
                        if (f != null) ModRel(p.id, f.id, -18);
                        AllHappy(6); p.prestige += 8;
                        ChangePressure(15);
                        S.story.Set("tax_refused");
                        Notify("Ihr habt dem Kaiser die Stirn geboten. Das Volk spricht von euch – und Valdoran auch.", 1);
                    }
                    break;
                case "camp_raid":
                    {
                        if (s == null) break;
                        var camp = Poi(e.b);
                        if (option == 0 && res[4] >= e.v) res[4] -= e.v;
                        else if (option == 2 && camp != null && !camp.done)
                        {
                            float mine = Strength(s.garrison), theirs = Strength(camp.guards);
                            if (mine > theirs * 0.8f || Rng.NextDouble() < mine / (theirs + 1f))
                            {
                                for (int u = 0; u < Units.Count; u++) camp.guards[u] = camp.guards[u] / 2;
                                p.prestige += 5; Notify("Die Räuber wurden in die Wälder zurückgejagt.", 1);
                            }
                            else { for (int u = 0; u < Units.Count; u++) s.garrison[u] = s.garrison[u] * 3 / 4; res[0] *= 0.85f; Notify("Die Garnison wurde zurückgeschlagen.", 2); }
                        }
                        else { res[0] *= 0.85f; res[1] *= 0.85f; s.happiness -= 6; }
                        break;
                    }
                case "punish":
                    if (s == null) break;
                    if (option == 0 && res[4] >= e.v) { res[4] -= e.v; ChangePressure(-20); }
                    else if (option == 2)
                    {
                        float str = Strength(s.garrison);
                        if (str > 60 || Rng.NextDouble() < str / 80f)
                        {
                            p.prestige += 12; ChangePressure(8); S.battlesWon++;
                            Notify($"Die kaiserlichen Reiter wurden vor {s.name} zurückgeschlagen!", 1);
                        }
                        else
                        {
                            for (int u = 0; u < Units.Count; u++) s.garrison[u] = s.garrison[u] * 3 / 4;
                            res[0] *= 0.7f; res[1] *= 0.8f; s.happiness -= 10;
                            Notify($"Die Strafexpedition hat {s.name} geplündert.", 2);
                        }
                    }
                    else { res[0] *= 0.75f; res[1] *= 0.8f; s.happiness -= 12; p.prestige += 3; ChangePressure(-5); Notify($"Die Strafexpedition hat {s.name} geplündert.", 2); }
                    break;
                case "uprising":
                    if (option == 0 && res[4] >= 120 && s != null && Emperor != null && s.faction == Emperor.id)
                    {
                        res[4] -= 120;
                        if (Rng.NextDouble() < 0.7)
                        {
                            TransferSettlement(s, p.id);
                            s.happiness = 60; s.originalFaction = p.id;
                            for (int u = 0; u < Units.Count; u++) s.garrison[u] = 0;
                            s.garrison[(int)UType.Miliz] = (int)(s.pop * 0.1f);
                            Notify($"{s.name} hat sich erhoben und schließt sich euch an!", 1);
                            OnConquered(s, Emperor, p);
                        }
                        else Notify($"Der Aufstand in {s.name} wurde blutig niedergeschlagen.", 2);
                    }
                    break;
                case "claim":
                    if (f == null) break;
                    if (option == 0 && res[4] >= 60) { res[4] -= 60; ModRel(p.id, f.id, 15); f.claim = -1; }
                    else ModRel(p.id, f.id, -12);
                    break;
                case "peace_offer":
                    if (f == null || !f.alive) break;
                    if (option == 0) MakePeace(p.id, f.id); else f.warWeariness *= 0.7f;
                    break;
                case "alliance_offer":
                    if (f == null || !f.alive) break;
                    if (option == 0) { SetStatus(p.id, f.id, DipStatus.Alliance); ModRel(p.id, f.id, 10); OnAlliance(f); Notify($"Bündnis mit {f.name}!", 1); }
                    else ModRel(p.id, f.id, -5);
                    break;
                case "trade_offer":
                    if (f == null || !f.alive) break;
                    if (option == 0) { SetTrade(p.id, f.id, true); ModRel(p.id, f.id, 5); } else ModRel(p.id, f.id, -2);
                    break;
                case "tribute_demand":
                    if (f == null || !f.alive) break;
                    if (option == 0 && res[4] >= e.v) { res[4] -= e.v; f.res[4] += e.v; ModRel(p.id, f.id, 5); }
                    else { ModRel(p.id, f.id, -20); if (Rng.NextDouble() < f.aggression) DeclareWar(f.id, p.id); }
                    break;
                case "help_request":
                    {
                        var enemy = F(e.b);
                        if (f == null || enemy == null || !enemy.alive) break;
                        if (option == 0) { DeclareWar(p.id, enemy.id); ModRel(p.id, f.id, 20); }
                        else if (option == 1 && res[4] >= 40) { res[4] -= 40; f.res[4] += 40; ModRel(p.id, f.id, 8); }
                        else ModRel(p.id, f.id, -8);
                        break;
                    }
                case "marriage":
                    if (f == null || !f.alive) break;
                    if (option == 0) { ModRel(p.id, f.id, 30); res[4] += 80; p.prestige += 15; AllHappy(5); }
                    else ModRel(p.id, f.id, -10);
                    break;
            }
        }
    }
}
