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
            string sn = s != null ? s.name : Loc.T("eurem Dorf");
            switch (e.id)
            {
                case "border": BorderView(e, v); break;
                case "nomads": NomadsView(e, v); break;
                case "harvest":
                    v.Title = Loc.T("Reiche Ernte"); v.Icon = "food";
                    v.Text = Loc.F("Die Felder um {0} tragen in diesem Jahr außergewöhnlich gut. Die Scheunen sind voll.", (sn));
                    v.Opt(Loc.T("Wunderbar! (+60 Nahrung)"));
                    v.Opt(Loc.T("Ein Erntefest feiern (+15 Zufriedenheit, +20 Nahrung)"));
                    break;
                case "merchant":
                    v.Title = Loc.T("Fahrender Händler"); v.Icon = "market";
                    v.Text = Loc.T("Ein Händler mit schwer beladenen Wagen bietet Eisen und Waffen feil – zu fairen Preisen.");
                    v.Opt(Loc.T("30 Eisen für 60 Gold"), gold >= 60);
                    v.Opt(Loc.T("10 Waffen für 50 Gold"), gold >= 50);
                    v.Opt(Loc.T("Kein Interesse"));
                    break;
                case "refugees":
                    v.Title = Loc.T("Flüchtlinge aus dem Kaiserreich"); v.Icon = "people";
                    v.Text = Loc.T("Abgemagerte Familien stehen vor den Toren. Sie fliehen vor den Steuereintreibern des Kaisers. 'Bitte, Herr, gebt uns Obdach!'");
                    v.Opt(Loc.T("Aufnehmen (+12 Bewohner, -30 Nahrung, Kaiser verärgert)"));
                    v.Opt(Loc.T("Mit Proviant weiterschicken (-15 Nahrung, +Ansehen)"));
                    v.Opt(Loc.T("Abweisen"));
                    break;
                case "bandits":
                    v.Title = Loc.T("Räuberbande"); v.Icon = "sword";
                    v.Text = Loc.F("Eine Räuberbande lauert an der Straße nach {0} und überfällt Bauern. Ihr Anführer verlangt Wegzoll.", (sn));
                    v.Opt(Loc.T("40 Gold zahlen"), gold >= 40);
                    v.Opt(Loc.T("Die Garnison schicken (Kampf)"));
                    v.Opt(Loc.T("Nichts tun (Händler bleiben weg)"));
                    break;
                case "plague":
                    v.Title = Loc.T("Fieber in ") + sn; v.Icon = "skull";
                    v.Text = Loc.T("Ein Fieber breitet sich aus. Das Wasser ist schlecht. Ein Heiler aus dem Kloster bietet Hilfe an.");
                    v.Opt(Loc.T("Heiler bezahlen (50 Gold)"), gold >= 50);
                    v.Opt(Loc.T("Beten und hoffen"));
                    break;
                case "festival":
                    v.Title = Loc.T("Johannisfeuer"); v.Icon = "smile";
                    v.Text = Loc.T("Das Volk möchte ein großes Fest mit Musik, Gauklern und Bier feiern.");
                    v.Opt(Loc.T("Fest ausrichten (-40 Gold, -40 Nahrung, +Zufriedenheit)"), gold >= 40 && p.res[0] >= 40);
                    v.Opt(Loc.T("Lieber sparen"));
                    break;
                case "traveler":
                    v.Title = Loc.T("Ein Kartograph"); v.Icon = "map";
                    v.Text = Loc.T("Ein reisender Kartograph bietet euch seine Karten der Umgebung an.");
                    v.Opt(Loc.T("Karten kaufen (30 Gold)"), gold >= 30);
                    v.Opt(Loc.T("Ablehnen"));
                    break;
                case "fire":
                    v.Title = Loc.T("Feuer!"); v.Icon = "fire";
                    v.Text = Loc.F("In {0} ist ein Lagerschuppen in Brand geraten.", (sn));
                    v.Opt(Loc.T("Löschen! (verliert 25% Holz)"));
                    break;
                case "knight":
                    v.Title = Loc.T("Fahrende Ritter"); v.Icon = "knight";
                    v.Text = Loc.T("Drei fahrende Ritter bieten ihre Dienste an – gegen ordentlichen Sold.");
                    v.Opt(Loc.T("Anheuern (90 Gold)"), gold >= 90 && s != null);
                    v.Opt(Loc.T("Ablehnen"));
                    break;
                case "treasure":
                    v.Title = Loc.T("Silberader"); v.Icon = "gold";
                    v.Text = Loc.T("Die Bergleute sind auf eine Silberader gestoßen!");
                    v.Opt(Loc.T("Hervorragend (+120 Gold)"));
                    break;
                case "unrest":
                    v.Title = Loc.T("Murren in ") + sn; v.Icon = "angry";
                    v.Text = Loc.T("Die Bürger sind unzufrieden. Auf dem Marktplatz wird gegen euch gehetzt.");
                    v.Opt(Loc.T("Brot verteilen (-50 Nahrung, +Zufriedenheit)"), p.res[0] >= 50);
                    v.Opt(Loc.T("Steuern für eine Weile senken"));
                    v.Opt(Loc.T("Rädelsführer einsperren (-Zufriedenheit, +Ordnung)"));
                    break;
                case "drought":
                    v.Title = Loc.T("Dürre"); v.Icon = "sun";
                    v.Text = Loc.T("Ein heißer Sommer. Die Brunnen sinken, die Ernte verdorrt.");
                    v.Opt(Loc.T("Vorräte rationieren (-40 Nahrung)"));
                    break;
                case "wolves":
                    v.Title = Loc.T("Wölfe im Winter"); v.Icon = "wolf";
                    v.Text = Loc.T("Hungrige Wölfe reißen Vieh am Waldrand.");
                    v.Opt(Loc.T("Jäger aussenden (-15 Gold)"), gold >= 15);
                    v.Opt(Loc.T("Vieh in die Ställe (-20 Nahrung)"));
                    break;
                case "tax":
                    v.Title = Loc.T("Der kaiserliche Zehnt"); v.Icon = "crown";
                    v.Text = Loc.F("Ein Steuereintreiber in schwarzem Samt verliest ein Dekret: 'Seine Majestät Kaiser Malrik fordert den Zehnt: {0} Gold.'", ((int)e.v));
                    v.Opt(Loc.F("Zahlen ({0} Gold)", ((int)e.v)), gold >= e.v);
                    v.Opt(Loc.T("Verweigern (Kaiser erzürnt, Volk jubelt)"));
                    break;
                case "camp_raid":
                    v.Title = Loc.T("Räuber!"); v.Icon = "skull";
                    v.Text = Loc.F("Räuber aus dem Lager in den Wäldern überfallen die Höfe um {0}. Ihr Anführer lässt ausrichten: '{1} Gold, und wir ziehen weiter.' Solange das Lager steht, kommen sie wieder – hebt es mit einem Heer aus!", (sn), ((int)e.v));
                    v.Opt(Loc.F("Zahlen ({0} Gold)", ((int)e.v)), gold >= e.v);
                    v.Opt(Loc.T("Plündern lassen (Nahrung und Holz gehen verloren)"));
                    v.Opt(Loc.T("Die Garnison schicken (Kampf)"), s != null && s.garrison.Sum() > 0);
                    break;
                case "punish":
                    v.Title = Loc.T("Kaiserliche Strafexpedition"); v.Icon = "eagle";
                    v.Text = Loc.F("Reiter mit dem eisernen Adler stehen vor {0}: 'Seine Majestät ist unzufrieden mit euch. Zahlt {1} Gold Buße – oder wir nehmen uns, was wir finden.'", (sn), ((int)e.v));
                    v.Opt(Loc.F("Buße zahlen ({0} Gold, senkt den Druck)", ((int)e.v)), gold >= e.v);
                    v.Opt(Loc.T("Plündern lassen (Nahrung und Holz gehen verloren)"));
                    v.Opt(Loc.T("Mit der Garnison vertreiben (riskant, erhöht den Druck)"), s != null && s.garrison.Sum() > 0);
                    break;
                case "uprising":
                    v.Title = Loc.T("Aufstand in ") + sn; v.Icon = "fist";
                    v.Text = Loc.F("Bürger aus {0} schicken einen Boten: 'Wir haben genug vom Kaiser! Schickt uns Gold für Waffen, und wir öffnen euch die Tore.'", (sn));
                    v.Opt(Loc.T("Den Aufstand finanzieren (120 Gold)"), gold >= 120);
                    v.Opt(Loc.T("Zu riskant"));
                    break;
                case "claim":
                    v.Title = Loc.T("Gebietsanspruch"); v.Icon = "scroll";
                    v.Text = Loc.F("{0} von {1} erhebt Anspruch auf {2}: 'Diese Ländereien gehörten einst meinen Ahnen!'", (f?.leader), (f?.name), (St(e.b)?.name));
                    v.Opt(Loc.T("Mit Geschenken beschwichtigen (60 Gold)"), gold >= 60);
                    v.Opt(Loc.T("Zurückweisen (Beziehung sinkt)"));
                    break;
                case "peace_offer":
                    v.Title = Loc.T("Friedensangebot"); v.Icon = "dove";
                    v.Text = Loc.F("Ein Gesandter von {0} überbringt ein Angebot: Frieden und 30 Tage Waffenruhe.", (f?.name));
                    v.Opt(Loc.T("Frieden annehmen"));
                    v.Opt(Loc.T("Weiterkämpfen"));
                    break;
                case "alliance_offer":
                    v.Title = Loc.T("Bündnisangebot"); v.Icon = "handshake";
                    v.Text = Loc.F("{0} schlägt ein Bündnis vor: gegenseitiger Beistand in Kriegszeiten.", (f?.leader));
                    v.Opt(Loc.T("Bündnis eingehen"));
                    v.Opt(Loc.T("Höflich ablehnen"));
                    break;
                case "trade_offer":
                    v.Title = Loc.T("Handelsangebot"); v.Icon = "market";
                    v.Text = Loc.F("Händler von {0} möchten mit euren Märkten handeln.", (f?.name));
                    v.Opt(Loc.T("Handelsabkommen schließen"));
                    v.Opt(Loc.T("Ablehnen"));
                    break;
                case "tribute_demand":
                    v.Title = Loc.T("Tributforderung"); v.Icon = "sword";
                    v.Text = Loc.F("{0} droht: 'Zahlt {1} Gold, oder wir holen es uns mit dem Schwert.'", (f?.leader), ((int)e.v));
                    v.Opt(Loc.F("Zahlen ({0} Gold)", ((int)e.v)), gold >= e.v);
                    v.Opt(Loc.T("Niemals! (Krieg droht)"));
                    break;
                case "help_request":
                    v.Title = Loc.T("Hilferuf"); v.Icon = "horn";
                    v.Text = Loc.F("{0} bittet um Beistand im Krieg gegen {1}. Ein Interessenkonflikt: Helft ihr, macht ihr euch Feinde.", (f?.leader), (F(e.b)?.name));
                    v.Opt(Loc.F("Krieg gegen {0} erklären", (F(e.b)?.name)));
                    v.Opt(Loc.T("Gold statt Soldaten schicken (40 Gold)"), gold >= 40);
                    v.Opt(Loc.T("Neutral bleiben"));
                    break;
                case "marriage":
                    v.Title = Loc.T("Eine Hochzeit"); v.Icon = "heart";
                    v.Text = Loc.F("{0} schlägt eine Heirat zwischen euren Häusern vor. Eine Mitgift wäre inbegriffen.", (f?.leader));
                    v.Opt(Loc.T("Annehmen (+Beziehung, +Gold, +Ansehen)"));
                    v.Opt(Loc.T("Ablehnen"));
                    break;
                default:
                    v.Title = "?"; v.Text = e.id; v.Opt(Loc.T("OK"));
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
                        if (str > 35 || Rng.NextDouble() < str / 40f) { p.prestige += 6; res[4] += 25; Notify(Loc.T("Die Räuber wurden vertrieben! Ihre Beute gehört euch."), 1); }
                        else { for (int u = 0; u < Units.Count; u++) s.garrison[u] = s.garrison[u] * 2 / 3; s.pop -= 3; Notify(Loc.T("Eure Männer wurden zurückgeschlagen."), 2); }
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
                        Notify(Loc.T("Ihr habt dem Kaiser die Stirn geboten. Das Volk spricht von euch – und Valdoran auch."), 1);
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
                                p.prestige += 5; Notify(Loc.T("Die Räuber wurden in die Wälder zurückgejagt."), 1);
                            }
                            else { for (int u = 0; u < Units.Count; u++) s.garrison[u] = s.garrison[u] * 3 / 4; res[0] *= 0.85f; Notify(Loc.T("Die Garnison wurde zurückgeschlagen."), 2); }
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
                            Notify(Loc.F("Die kaiserlichen Reiter wurden vor {0} zurückgeschlagen!", (s.name)), 1);
                        }
                        else
                        {
                            for (int u = 0; u < Units.Count; u++) s.garrison[u] = s.garrison[u] * 3 / 4;
                            res[0] *= 0.7f; res[1] *= 0.8f; s.happiness -= 10;
                            Notify(Loc.F("Die Strafexpedition hat {0} geplündert.", (s.name)), 2);
                        }
                    }
                    else { res[0] *= 0.75f; res[1] *= 0.8f; s.happiness -= 12; p.prestige += 3; ChangePressure(-5); Notify(Loc.F("Die Strafexpedition hat {0} geplündert.", (s.name)), 2); }
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
                            Notify(Loc.F("{0} hat sich erhoben und schließt sich euch an!", (s.name)), 1);
                            OnConquered(s, Emperor, p);
                        }
                        else Notify(Loc.F("Der Aufstand in {0} wurde blutig niedergeschlagen.", (s.name)), 2);
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
                    if (option == 0) { SetStatus(p.id, f.id, DipStatus.Alliance); ModRel(p.id, f.id, 10); OnAlliance(f); Notify(Loc.F("Bündnis mit {0}!", (f.name)), 1); }
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
