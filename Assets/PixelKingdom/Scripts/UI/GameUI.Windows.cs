using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelKingdom
{
    public partial class GameUI
    {
        Rect WindowFrame(string title, float w, float h, bool modal, bool closable = true)
        {
            w = Mathf.Min(w, VW - 16); h = Mathf.Min(h, VH - 16);
            if (modal) Modal();
            var r = new Rect(Mathf.Round((VW - w) / 2), Mathf.Round((VH - h) / 2), Mathf.Round(w), Mathf.Round(h));
            Panel(r);
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, 16), Palette.UIPanelDark);
            Text(r.x + 8, r.y + 5, title);
            if (closable && Button(new Rect(r.xMax - 16, r.y + 3, 13, 12), "", true, "Schließen (Esc)", "close")) Open = Window.None;
            return new Rect(r.x + 8, r.y + 22, r.width - 16, r.height - 28);
        }

        // ==================================================================
        //  Diplomatie
        // ==================================================================
        static string Traits(FactionData f)
        {
            var t = new List<string>();
            if (f.isEmperor) return "Tyrannisch, gierig, gnadenlos";
            if (f.isRebel) t.Add("Aufständisch");
            else if (f.isFree) t.Add("Freies Dorf");
            if (f.aggression > 0.66f) t.Add("Kriegerisch"); else if (f.aggression < 0.3f) t.Add("Friedfertig");
            if (f.greed > 0.66f) t.Add("Habgierig");
            if (f.honor > 0.66f) t.Add("Ehrenhaft"); else if (f.honor < 0.3f) t.Add("Verschlagen");
            if (f.ambition > 0.66f) t.Add("Ehrgeizig");
            if (f.continent == 0 && !f.isFree)
            {
                if (f.loyalty > 0.6f) t.Add("Kaisertreu"); else if (f.loyalty < 0.35f) t.Add("Kaiserfeind");
            }
            return t.Count == 0 ? "Unauffällig" : string.Join(", ", t);
        }

        void DrawDiplomacy()
        {
            var c = WindowFrame("Diplomatie", 520, 330, false);
            var known = G.Factions.Where(f => f.alive && !f.isPlayer && f.knownToPlayer != 0).ToList();
            if (known.Count == 0)
            {
                Wrapped(new Rect(c.x, c.y + 10, c.width, 60), "Ihr kennt noch keine anderen Herrscher. Schickt Späher oder Heere hinaus, um die Welt zu erkunden.");
                return;
            }
            if (dipSel < 0 || !known.Any(f => f.id == dipSel)) dipSel = known[0].id;
            // Liste
            var list = new Rect(c.x, c.y, 170, c.height);
            Fill(list, Palette.UIPanelDark);
            float rowH = 22;
            scrollDip = Scroll(list, scrollDip, known.Count * rowH - list.height);
            GUI.BeginGroup(list);
            for (int i = 0; i < known.Count; i++)
            {
                var f = known[i];
                var rr = new Rect(2, 2 + i * rowH - scrollDip, list.width - 4, rowH - 2);
                if (rr.yMax < 0 || rr.y > list.height) continue;
                bool sel = f.id == dipSel;
                Fill(rr, sel ? Palette.UISand : Hover(rr) ? Palette.UIPanelLight : Palette.UIPanel);
                Fill(new Rect(rr.x + 3, rr.y + 3, 6, 14), Palette.FactionColor(f.color));
                var fg = sel ? Palette.UIPanelDark : Palette.UIText;
                string nm = f.name.Length > 24 ? f.name.Substring(0, 23) + "." : f.name;
                Text(rr.x + 13, rr.y + 2, nm, fg);
                var st = G.Status(G.PlayerId, f.id);
                float rv = G.RelValue(G.PlayerId, f.id);
                Text(rr.x + 13, rr.y + 11, $"{DipInfo.Names[(int)st]}  {(rv >= 0 ? "+" : "")}{(int)rv}", sel ? Palette.UIPanelDark : Palette.UITextDim);
                if (Clicked(rr)) { dipSel = f.id; dipMessage = null; }
            }
            GUI.EndGroup();
            rects.Add(list);

            // Details
            var d = G.F(dipSel);
            float x = c.x + 180, y = c.y, w = c.xMax - x;
            Fill(new Rect(x, y + 1, 8, 8), Palette.FactionColor(d.color));
            Text(x + 12, y + 1, d.name);
            y += 12;
            Text(x, y, d.leader + "  -  " + Ranks.Get(d.rank).Name, Palette.UITextDim); y += 11;
            Text(x, y, "Wesen: " + Traits(d), Palette.UITextDim); y += 13;
            float relv = G.RelValue(G.PlayerId, d.id);
            Text(x, y, $"Beziehung: {RelWord(relv)} ({(int)relv})");
            Bar(new Rect(x + 150, y + 2, w - 150, 4), (relv + 100) / 200f, relv >= 0 ? Palette.UIText : Palette.UIBad);
            y += 11;
            var rel = G.Rel(G.PlayerId, d.id);
            string status = DipInfo.Names[rel.status] + (rel.trade ? ", Handel" : "");
            if (rel.status == (int)DipStatus.Truce) status += $" ({30 - (G.S.day - rel.since)} Tage)";
            Text(x, y, "Status: " + status); y += 11;
            float ratio = (G.MilitaryStrength(d.id) + 1) / (G.MilitaryStrength(G.PlayerId) + 1);
            string str = ratio > 2 ? "weit überlegen" : ratio > 1.25f ? "stärker" : ratio > 0.8f ? "ebenbürtig" : ratio > 0.5f ? "schwächer" : "weit unterlegen";
            Text(x, y, $"Militär: {str}  -  {G.CountSettlements(d.id)} Siedlungen, ~{Mathf.RoundToInt(G.TotalPop(d.id) / 10f) * 10} Einw."); y += 13;

            // Interessenkonflikte
            var lines = new List<string>();
            var claim = G.St(d.claim);
            if (claim != null && claim.faction == G.PlayerId) lines.Add($"Beansprucht eure Stadt {claim.name}!");
            else if (claim != null) lines.Add($"Begehrt {claim.name} ({G.F(claim.faction)?.name})");
            if (G.IsNeighbour(d.id, G.PlayerId)) lines.Add("Grenzt an euer Reich (Reibereien)");
            var wars = G.EnemiesOf(d.id).Where(e => e.knownToPlayer != 0 || e.isPlayer).Select(e => e.isPlayer ? "euch" : e.name).ToList();
            if (wars.Count > 0) lines.Add("Im Krieg mit: " + string.Join(", ", wars));
            var allies = G.AlliesOf(d.id).Where(e => e.knownToPlayer != 0 || e.isPlayer).Select(e => e.isPlayer ? "euch" : e.name).ToList();
            if (allies.Count > 0) lines.Add("Verbündet mit: " + string.Join(", ", allies));
            var emp = G.Emperor;
            if (emp != null && emp.alive && d.id != emp.id && d.continent == 0 && G.Status(d.id, emp.id) == DipStatus.Pact) lines.Add("Vasall des Kaisers");
            foreach (var l in lines.Take(4)) { y += Wrapped(new Rect(x, y, w, 30), "- " + l, Palette.UISand); }
            y += 4;

            // Aktionen
            (DipAction act, string label, string icon)[] acts =
            {
                (DipAction.Gift, "Geschenk", "gold"),
                (DipAction.Trade, "Handel", "market"),
                (DipAction.Pact, "Nichtangriff", "dove"),
                (DipAction.Alliance, "Bündnis", "handshake"),
                (DipAction.Annex, "Anschluss", "crown"),
                (DipAction.Tribute, "Tribut", "fist"),
                (DipAction.Peace, "Frieden", "dove"),
                (DipAction.War, "Krieg", "sword"),
            };
            float bw = (w - 6) / 4f;
            float by = c.yMax - 52;
            for (int i = 0; i < acts.Length; i++)
            {
                var ev = G.Evaluate(acts[i].act, d);
                var br = new Rect(x + (i % 4) * (bw + 2), by + (i / 4) * 18, bw, 16);
                string tip = "__" + acts[i].label + "__\n" + ActionDesc(acts[i].act, d, ev);
                if (!ev.Possible) tip += "\n__Nicht möglich__: " + ev.Blocker;
                else
                {
                    if (acts[i].act != DipAction.Gift && acts[i].act != DipAction.War) tip += $"\n__Aussicht__: {ev.Chance}";
                    if (ev.Reasons.Count > 0) tip += "\n__Gründe__:\n" + string.Join("\n", ev.Reasons);
                }
                if (Button(br, acts[i].label, ev.Possible, tip, null, false, 1, acts[i].act != DipAction.War))
                {
                    if (acts[i].act == DipAction.War) { warTarget = d; warArmy = null; warSettlement = null; Open = Window.ConfirmWar; }
                    else dipMessage = G.Perform(acts[i].act, d);
                }
            }
            if (dipMessage != null) Wrapped(new Rect(x, by - 22, w, 20), dipMessage, Palette.UIGood);
        }

        string ActionDesc(DipAction a, FactionData f, DipEval ev)
        {
            switch (a)
            {
                case DipAction.Gift: return $"{(int)ev.Cost} Gold schenken. Verbessert die Beziehung.";
                case DipAction.Trade: return "Handelsabkommen: beide Seiten erhalten täglich Gold.";
                case DipAction.Pact: return "Versprechen, einander nicht anzugreifen. Bruch kostet Ansehen bei allen.";
                case DipAction.Alliance: return $"Bündnis gegen Mitgift ({(int)ev.Cost} Gold). Verbündete kämpfen gemeinsam und teilen ihre Karten.";
                case DipAction.Annex: return $"Bietet {(int)ev.Cost} Gold, damit sie sich eurem Reich anschließen. Alle Siedlungen werden eure.";
                case DipAction.Tribute: return $"Fordert ~{(int)(-ev.Cost)} Gold. Klappt nur, wenn ihr viel stärker seid.";
                case DipAction.Peace: return "Bietet Frieden an (30 Tage Waffenruhe).";
                case DipAction.War: return "Erklärt den Krieg. Verbündete des Gegners können beitreten.";
            }
            return "";
        }

        void DrawConfirmWar()
        {
            if (warTarget == null) { Open = Window.None; return; }
            var c = WindowFrame("Krieg erklären?", 300, 120, true);
            var st = G.Status(G.PlayerId, warTarget.id);
            string text = $"Ihr seid nicht im Krieg mit {warTarget.name}. Wollt ihr {warTarget.leader} den Krieg erklären?";
            if (st == DipStatus.Pact) text += " Ihr brecht damit euren Nichtangriffspakt – alle werden euch misstrauen.";
            if (st == DipStatus.Alliance) text += " Ihr verratet euren Verbündeten!";
            if (st == DipStatus.Truce) text = $"Es herrscht noch Waffenruhe mit {warTarget.name}. Ihr könnt jetzt nicht angreifen.";
            Wrapped(new Rect(c.x, c.y, c.width, 60), text);
            bool can = st != DipStatus.Truce;
            if (Button(new Rect(c.x, c.yMax - 16, 120, 16), "Ja, Krieg!", can, null, "sword"))
            {
                G.DeclareWar(G.PlayerId, warTarget.id);
                if (warArmy != null && warSettlement != null && G.A(warArmy.id) != null) gm.OrderAttack(warArmy, warSettlement, warRaid);
                Open = warArmy == null ? Window.Diplomacy : Window.None;
                warTarget = null;
            }
            if (Button(new Rect(c.xMax - 120, c.yMax - 16, 120, 16), "Lieber nicht")) { Open = warArmy == null ? Window.Diplomacy : Window.None; warTarget = null; }
        }

        // ==================================================================
        //  Reich
        // ==================================================================
        void DrawRealm()
        {
            var p = G.Player;
            var c = WindowFrame("Euer Reich: " + p.name, 520, 330, false);
            string[] tabs = { "Rang", "Erlasse & Feste", "Siedlungen" };
            for (int t = 0; t < tabs.Length; t++)
                if (Button(new Rect(c.x + t * 104, c.y, 100, 15), tabs[t], true, null, null, realmTab == t)) realmTab = t;
            c = new Rect(c.x, c.y + 21, c.width, c.height - 21);
            if (realmTab == 1) { DrawEdicts(c); return; }
            if (realmTab == 2) { DrawRealmSettlements(c); return; }
            // Rangleiter
            float x = c.x, y = c.y;
            for (int i = 0; i < Ranks.Count; i++)
            {
                var rd = Ranks.Get(i);
                var rr = new Rect(x + i * (c.width / Ranks.Count), y, c.width / Ranks.Count - 3, 26);
                bool cur = i == p.rank, done = i < p.rank;
                Fill(rr, cur ? Palette.UISand : done ? Palette.UIPanelLight : Palette.UIPanelDark);
                var fg = cur ? Palette.UIPanelDark : done ? Palette.UIText : Palette.UITextDim;
                string nm = rd.Name.Length > 13 ? rd.Name.Substring(0, 12) + "." : rd.Name;
                Text(rr.x + (rr.width - PixelFont.Width(nm)) / 2, rr.y + 4, nm, fg);
                Text(rr.x + (rr.width - PixelFont.Width(rd.Ruler)) / 2, rr.y + 14, rd.Ruler.Length > 13 ? "" : rd.Ruler, fg);
                Tip(rr, $"__{rd.Name}__\n{rd.Desc}\n__Herrscher__: {rd.Ruler}\n__Max. Siedlungen__: {rd.MaxSettlements}\n__Produktionsbonus__: +{Mathf.RoundToInt(rd.ProductionBonus * 100)}%");
            }
            y += 34;
            // Nächster Rang
            if (p.rank < Ranks.Count - 1)
            {
                var next = Ranks.Get(p.rank + 1);
                Text(x, y, $"Nächster Rang: {next.Name}", Palette.UISand); y += 12;
                var missing = new List<string>();
                bool ok = G.CanRankUp(p, missing);
                // alle Bedingungen auflisten
                var all = new List<(string, bool)>();
                float pop = G.TotalPop(p.id);
                all.Add(($"Bevölkerung {(int)pop}/{next.Pop}", pop >= next.Pop));
                all.Add(($"Siedlungen {G.CountSettlements(p.id)}/{next.Settlements}", G.CountSettlements(p.id) >= next.Settlements));
                all.Add(($"Ansehen {(int)p.prestige}/{(int)next.Prestige}", p.prestige >= next.Prestige));
                all.Add(($"Gold {(int)p.res[(int)Res.Gold]}/{(int)next.GoldCost}", p.res[(int)Res.Gold] >= next.GoldCost));
                if (next.RequiredBuilding != BType.None) all.Add(("Gebäude: " + Buildings.Get(next.RequiredBuilding).Name, G.SettlementsOf(p.id).Any(s => s.buildings[(int)next.RequiredBuilding] > 0)));
                if (next.StoryChapter > 0) all.Add(($"Geschichte: Kapitel {next.StoryChapter + 1}", G.S.story.chapter >= next.StoryChapter));
                for (int i = 0; i < all.Count; i++)
                {
                    float ix = x + (i % 2) * 180, iy = y + (i / 2) * 12;
                    var box = new Rect(ix, iy, 9, 9);
                    Fill(box, Palette.UIPanelDark);
                    if (all[i].Item2) Fill(new Rect(ix + 2, iy + 2, 5, 5), Palette.UIGood);
                    Text(ix + 13, iy + 1, all[i].Item1, all[i].Item2 ? Palette.UITextDim : Palette.UIText);
                }
                y += ((all.Count + 1) / 2) * 12 + 4;
                if (Button(new Rect(x, y, 170, 16), "Aufsteigen!", ok, ok ? $"Kostet {(int)next.GoldCost} Gold" : "Bedingungen noch nicht erfüllt", "crown", false, 1, true)) G.RankUp();
                y += 24;
            }
            else { Text(x, y, "Ihr habt den höchsten Rang erreicht.", Palette.UISand); y += 16; }

            // Statistik
            Text(x, y, "Statistik", Palette.UITextDim); y += 11;
            string[] stats =
            {
                $"Bevölkerung: {(int)G.TotalPop(p.id)}",
                $"Soldaten: {G.TotalSoldiers(p.id)}",
                $"Ansehen: {(int)p.prestige}",
                $"Siege/Niederlagen: {G.S.battlesWon}/{G.S.battlesLost}",
                $"Verbündete: {G.AlliesOf(p.id).Count()}",
                $"Kriege: {G.AtWarCount(p.id)}",
            };
            for (int i = 0; i < stats.Length; i++) Text(x + (i % 3) * 120, y + (i / 3) * 11, stats[i]);
        }

        int realmTab;

        void DrawRealmSettlements(Rect c)
        {
            var p = G.Player;
            float x = c.x, y = c.y;
            Text(x, y, "Klick auf eine Siedlung springt dorthin.", Palette.UITextDim); y += 12;
            var list = new Rect(x, y, c.width, c.yMax - y);
            Fill(list, Palette.UIPanelDark);
            var sets = G.SettlementsOf(p.id).ToList();
            scrollRealm = Scroll(list, scrollRealm, sets.Count * 12 - list.height + 4);
            GUI.BeginGroup(list);
            for (int i = 0; i < sets.Count; i++)
            {
                var s = sets[i];
                var rr = new Rect(2, 2 + i * 12 - scrollRealm, list.width - 4, 11);
                if (rr.yMax < 0 || rr.y > list.height) continue;
                if (Hover(rr)) Fill(rr, Palette.UIPanel);
                Text(rr.x + 2, rr.y + 2, s.name);
                Text(rr.x + 130, rr.y + 2, $"Einw. {(int)s.pop}");
                Text(rr.x + 200, rr.y + 2, $"Zufr. {(int)s.happiness}", s.happiness < 30 ? Palette.UIGood : Palette.UIText);
                Text(rr.x + 270, rr.y + 2, s.queue.Count > 0 ? "baut..." : "", Palette.UITextDim);
                Text(rr.x + 330, rr.y + 2, s.auto ? "Verwalter" : "", Palette.UITextDim);
                if (Clicked(rr)) { gm.SelectSettlement(s); Open = Window.None; }
            }
            GUI.EndGroup();
            rects.Add(list);
        }

        void DrawEdicts(Rect c)
        {
            float x = c.x, y = c.y;
            Text(x, y, $"__Erlasse__ ({G.S.edicts.Count}/{G.MaxEdicts} aktiv) – Gesetze, die euer Reich prägen. Verkünden kostet {(int)Edicts.ChangeCost} Gold.", Palette.UISand);
            y += 14;
            for (int e = 0; e < Edicts.Count; e++)
            {
                bool on = G.S.edicts.Contains(e);
                bool can = G.CanToggleEdict(e, out string why);
                var row = new Rect(x, y, c.width, 26);
                Fill(row, on ? Palette.UIPanelLight : Palette.UIPanelDark);
                if (Button(new Rect(x + 4, y + 6, 74, 14), on ? "Aufheben" : "Verkünden", on || can, on ? "Erlass aufheben (kostenlos)" : (can ? "Erlass verkünden" : why), on ? "close" : "scroll", on))
                    G.ToggleEdict(e);
                Text(x + 86, y + 3, "__" + Edicts.Names[e] + "__" + (on ? "  (aktiv)" : ""), on ? Palette.UIGood : Palette.UIText);
                var lines = PixelFont.Wrap(Edicts.Desc[e], (int)c.width - 92);
                Text(x + 86, y + 14, lines[0] + (lines.Count > 1 ? " ..." : ""), Palette.UITextDim);
                Tip(new Rect(x + 86, y, c.width - 86, 26), "__" + Edicts.Names[e] + "__\n" + Edicts.Desc[e] + (G.Player.rank < Edicts.MinRank[e] ? "\n__Ab Rang__: " + Ranks.Get(Edicts.MinRank[e]).Name : ""));
                y += 28;
            }
            y += 6;
            // Ritterturnier
            Fill(new Rect(x, y, c.width, 1), Palette.UIPanelDark);
            y += 6;
            Text(x, y, "__Ritterturnier__", Palette.UISand);
            Wrapped(new Rect(x, y + 12, c.width - 180, 40), $"Lanzen, Banner und Gäste aus dem ganzen Land: +25 Ansehen, +10 Zufriedenheit, bessere Beziehungen, 2 Ritter. Kostet {(int)Game.TournamentCost} Gold, alle 60 Tage.", Palette.UITextDim);
            bool ok = G.CanTournament(out string twhy);
            if (Button(new Rect(c.xMax - 170, y + 12, 170, 18), "Turnier ausrichten", ok, ok ? "Das Turnier beginnt sofort." : twhy, "flag", false, 1, true)) G.HoldTournament();
        }

        // ==================================================================
        //  Chronik
        // ==================================================================
        void DrawLog()
        {
            var c = WindowFrame("Chronik", 460, 300, false);
            var log = G.S.log;
            var list = new Rect(c.x, c.y, c.width, c.height);
            var lines = new List<(string text, int kind, bool head)>();
            int lastDay = -1;
            for (int i = log.Count - 1; i >= 0 && lines.Count < 300; i--)
            {
                var e = log[i];
                if (e.day != lastDay) { lines.Add(($"Tag {e.day + 1}", 0, true)); lastDay = e.day; }
                foreach (var l in PixelFont.Wrap(e.text, (int)c.width - 20)) lines.Add((l, e.kind, false));
            }
            scrollLog = Scroll(list, scrollLog, lines.Count * 10 - list.height);
            GUI.BeginGroup(list);
            for (int i = 0; i < lines.Count; i++)
            {
                float y = i * 10 - scrollLog;
                if (y < -10 || y > list.height) continue;
                var l = lines[i];
                var col = l.head ? Palette.UITextDim : l.kind == 1 ? Palette.UIGood : l.kind == 2 || l.kind == 3 ? Palette.UISand : l.kind == 4 ? Palette.UIGood : Palette.UIText;
                Text(l.head ? 0 : 10, y, l.text, col);
            }
            GUI.EndGroup();
            if (lines.Count == 0) Text(c.x, c.y, "Noch ist nichts geschehen.", Palette.UITextDim);
        }

        // ==================================================================
        //  Hilfe
        // ==================================================================
        void DrawHelp()
        {
            var c = WindowFrame("Hilfe & Steuerung", 470, 320, false);
            string text =
                "KARTE: WASD/Pfeile oder rechte Maustaste ziehen zum Bewegen. Mausrad (oder Q/E) zoomt in Pixelstufen.\n" +
                "AUSWAHL: Linksklick auf Siedlung oder Heer. Rechtsklick gibt einem Heer ein Ziel. Shift+Rechtsklick auf eine feindliche Stadt: Überfall statt Eroberung.\n" +
                "TABLET: Tippen wählt aus. Ist ein Heer gewählt, gibt Tippen ihm ein Ziel (Tippen auf das Heer selbst wählt ab). Ein Finger verschiebt die Karte, zwei Finger zoomen. Lange drücken zeigt Geländeinfos oder befiehlt einen Überfall. Wichtige Aktionen (Bauen, Diplomatie) bestätigt ihr durch zweimaliges Tippen. Zurück-Taste = Esc. Am PC simuliert F9 die Touch-Steuerung.\n" +
                "HEERE: Truppen in einer Siedlung ausbilden (Reiter 'Truppen'), dann 'Heer aufstellen' und 'Ausrücken'. Heer anklicken: Am Mauszeiger steht, was ein Rechtsklick bewirkt (marschieren, einrücken, erobern). Die Heerführung (M) listet alle Heere, Ziele mit Entfernung, Teilen, Vereinen und Truppentausch mit der Garnison.\n" +
                "STORY: Ihr bestimmt das Tempo. Nach jedem Kapitel gibt es eine Friedenszeit. Der Druck des Kaiserreichs steigt langsam – zahlt den Zehnt, um ihn zu senken, oder erhebt euch, wenn ihr bereit seid.\n" +
                "ZEIT: Leertaste Pause, 1/2/3 Tempo. Tab: nächste Siedlung, H: Hauptstadt.\n" +
                "FENSTER: R Reich, M Heerführung, G Diplomatie, L Chronik, F1 Hilfe, P Farbpalette, F5 Speichern.\n\n" +
                "WIRTSCHAFT: Gebäude brauchen Arbeiter (60% der Bewohner). Bauernhöfe brauchen fruchtbares Land, Steinbrüche Hügel. Holzfäller fällen sichtbar Bäume – der Wald wächst nur langsam nach. Eisen- und Goldminen brauchen Erzvorkommen (rostrote bzw. funkelnde Felsen) im Stadtgebiet. Im Winter wächst wenig – legt Vorräte an!\n" +
                "ZUFRIEDENHEIT: Steuern und Hunger senken sie, Tavernen, Kapellen und Feste heben sie. Unter 25 wandern Leute ab, unter 15 drohen Aufstände.\n" +
                "MILITÄR: Rekruten kommen aus der Bevölkerung. Speere schlagen Ritter, Ritter schlagen Bogenschützen, Bogenschützen schlagen Speere. Rammen brechen Mauern.\n" +
                "DIPLOMATIE: Jeder Herrscher hat eigene Interessen: Gebietsansprüche, Treue zum Kaiser, Gier, Ehre. Fahrt mit der Maus über eine Aktion, um die Aussichten zu sehen.\n" +
                "GELEGENTLICH SPIELEN: Das Spiel speichert automatisch. Während ihr weg seid, laufen Wirtschaft und Bau weiter (bis zu 100 Tage). Schaltet den Verwalter ein, damit eure Städte selbst bauen.";
            if (!gm.InMenu && Button(new Rect(c.xMax - 150, c.yMax - 15, 150, 15), "Einführung zeigen", true, null, "book")) StartTutorial();
            c.height -= 18;
            Wrapped(c, text);
        }

        // ==================================================================
        //  Menü
        // ==================================================================
        void DrawMenu()
        {
            var c = WindowFrame("Menü", 230, 252, true);
            float y = c.y, bh = 16, gap = 4;
            if (Button(new Rect(c.x, y, c.width, bh), "Weiterspielen", true, null, "play")) Open = Window.None; y += bh + gap;
            if (Button(new Rect(c.x, y, c.width, bh), "Speichern (F5)", true, null, "save")) gm.SaveNow(); y += bh + gap + 4;
            if (Button(new Rect(c.x, y, c.width, bh), "Musik: " + (AudioManager.MusicOn ? "An" : "Aus"), true, null, "horn", AudioManager.MusicOn)) AudioManager.MusicOn = !AudioManager.MusicOn; y += bh + gap;
            if (Button(new Rect(c.x, y, c.width, bh), "Klänge: " + (AudioManager.SoundOn ? "An" : "Aus"), true, null, "check", AudioManager.SoundOn)) AudioManager.SoundOn = !AudioManager.SoundOn; y += bh + gap;
            if (Button(new Rect(c.x, y, c.width, bh), "Tipps von Aldo: " + (Game.HintsEnabled ? "An" : "Aus"), true, "Meister Aldo erklärt Neues, sobald es zum ersten Mal vorkommt.", "book", Game.HintsEnabled))
            { Game.HintsEnabled = !Game.HintsEnabled; PlayerPrefs.SetInt("pk_hints", Game.HintsEnabled ? 1 : 0); }
            y += bh + gap;
            if (Button(new Rect(c.x, y, c.width, bh), "Farben: " + Palette.Name, true, null, "sun")) gm.CyclePalette(); y += bh + gap;
            int extra = PlayerPrefs.GetInt("pk_uiscale", 0);
            Text(c.x, y + 4, "Größe der Oberfläche");
            if (Button(new Rect(c.xMax - 66, y, 30, bh), "-", S > 1)) PlayerPrefs.SetInt("pk_uiscale", extra - 1);
            if (Button(new Rect(c.xMax - 32, y, 30, bh), "+", S < 8)) PlayerPrefs.SetInt("pk_uiscale", extra + 1);
            y += bh + gap + 4;
            if (Button(new Rect(c.x, y, c.width / 2 - 2, bh), "Hilfe")) Open = Window.Help;
            if (Button(new Rect(c.x + c.width / 2 + 2, y, c.width / 2 - 2, bh), "Hauptmenü")) { Open = Window.None; gm.BackToMenu(); }
            y += bh + gap;
            if (Button(new Rect(c.x, y, c.width, bh), "Beenden")) { gm.SaveNow(); Application.Quit(); }
        }

        // ==================================================================
        //  Ereignisse, Dialoge, Berichte
        // ==================================================================
        void DrawEvent()
        {
            var e = G.CurrentEvent;
            var v = G.Describe(e);
            float textH = PixelFont.Wrap(v.Text, 300).Count * 10;
            var c = WindowFrame(v.Title, 380, 70 + textH + v.Options.Count * 20, true, false);
            IconTile(new Rect(c.x, c.y, 40, 40), v.Icon);
            Wrapped(new Rect(c.x + 48, c.y, c.width - 48, textH + 10), v.Text);
            float y = Mathf.Max(c.y + 46, c.y + textH + 10);
            for (int i = 0; i < v.Options.Count; i++)
            {
                if (Button(new Rect(c.x, y, c.width, 16), v.Options[i], v.Enabled[i])) { G.ResolveEvent(i); break; }
                y += 20;
            }
        }

        void DrawDialog()
        {
            var d = G.Dialog.Peek();
            Modal();
            float w = Mathf.Min(460, VW - 20);
            float textH = PixelFont.Wrap(d.Text, (int)w - 70).Count * 10;
            float h = Mathf.Max(64, textH + 34);
            var r = new Rect(Mathf.Round((VW - w) / 2), Mathf.Round(VH - h - 60), w, h);
            Panel(r, Palette.UISand);
            IconTile(new Rect(r.x + 8, r.y + 8, 44, 44), d.Portrait, Palette.UIPanel);
            Text(r.x + 60, r.y + 8, d.Speaker, Palette.UIPanelDark);
            Fill(new Rect(r.x + 60, r.y + 18, PixelFont.Width(d.Speaker), 1), Palette.UIPanelDark);
            PixelFont.DrawWrapped(new Rect(r.x + 60, r.y + 23, w - 70, textH), d.Text, Palette.UIPanelDark);
            string more = G.Dialog.Count > 1 ? "Weiter >" : "OK";
            if (Button(new Rect(r.xMax - 62, r.yMax - 18, 56, 14), more) || Clicked(r)) G.Dialog.Dequeue();
        }

        void DrawReport()
        {
            var rp = report;
            var c = WindowFrame(rp.Title, 400, 200, true, false);
            bool playerWon = (rp.PlayerAttacker && rp.AttackerWon) || (!rp.PlayerAttacker && !rp.AttackerWon);
            Text(c.x, c.y, playerWon ? "SIEG!" : "NIEDERLAGE", playerWon ? Palette.UIGood : Palette.UISand, 2);
            Wrapped(new Rect(c.x + 100, c.y, c.width - 100, 20), rp.Outcome);
            float y = c.y + 26;
            void Side(float x, string title, int[] start, int[] loss, int levy, int levyLoss)
            {
                Text(x, y, title.Length > 26 ? title.Substring(0, 25) + "." : title, Palette.UISand);
                float yy = y + 12;
                for (int u = 0; u < Units.Count; u++)
                {
                    if (start[u] == 0) continue;
                    Icon(new Rect(x, yy, 12, 12), Units.Get(u).Icon, Palette.UIText);
                    Text(x + 15, yy + 2, $"{start[u]}  -{loss[u]}");
                    yy += 13;
                }
                if (levy > 0) { Icon(new Rect(x, yy, 12, 12), "militia", Palette.UITextDim); Text(x + 15, yy + 2, $"Aufgebot {levy}  -{levyLoss}", Palette.UITextDim); }
            }
            Side(c.x, "Angreifer: " + rp.Attacker, rp.AttStart, rp.AttLoss, 0, 0);
            Side(c.x + c.width / 2, "Verteidiger: " + rp.Defender, rp.DefStart, rp.DefLoss, rp.Levy, rp.LevyLoss);
            if (rp.DefBonus > 0.01f) Text(c.x + c.width / 2, c.yMax - 30, $"Mauern/Gelände: +{Mathf.RoundToInt(rp.DefBonus * 100)}%", Palette.UITextDim);
            if (Button(new Rect(c.xMax - 80, c.yMax - 16, 80, 16), "OK")) report = null;
        }

        void DrawOffline()
        {
            var c = WindowFrame("Willkommen zurück!", 320, 180, true, false);
            Wrapped(new Rect(c.x, c.y, c.width, 20), "Während eurer Abwesenheit hat euer Volk weitergearbeitet:", Palette.UISand);
            Wrapped(new Rect(c.x, c.y + 22, c.width, c.height - 40), offlineReport);
            if (Button(new Rect(c.xMax - 80, c.yMax - 16, 80, 16), "Weiter")) offlineReport = null;
        }

        void DrawGameOver()
        {
            var c = WindowFrame("Euer Reich ist gefallen", 300, 120, true, false);
            Wrapped(new Rect(c.x, c.y, c.width, 50), "Eure letzte Siedlung ist verloren. Doch Geschichten enden nie ganz – beginnt neu und lernt aus der Chronik.");
            if (Button(new Rect(c.x, c.yMax - 16, 130, 16), "Neues Spiel")) { SaveSystem.Delete(); gm.NewGame(); }
            if (Button(new Rect(c.xMax - 130, c.yMax - 16, 130, 16), "Hauptmenü")) { SaveSystem.Delete(); gm.GameOver = false; gm.BackToMenu(); }
        }

        // ==================================================================
        //  Hauptmenü
        // ==================================================================
        bool confirmNew;
        void DrawMainMenu()
        {
            modalThisFrame = true;
            int ts = VW > 520 ? 5 : VW > 380 ? 4 : 3;
            string title = "PIXEL KINGDOM";
            float tw = PixelFont.Width(title) * ts;
            float ty = Mathf.Round(VH * 0.12f);
            var plate = new Rect(Mathf.Round((VW - tw) / 2 - 20), ty - 14, Mathf.Round(tw + 40), 9 * ts + 46);
            Panel(plate);
            Fill(new Rect(plate.x + 3, plate.y + 3, plate.width - 6, 1), Palette.UIGood);
            Fill(new Rect(plate.x + 3, plate.yMax - 4, plate.width - 6, 1), Palette.UIGood);
            PixelFont.DrawShadow(Mathf.Round((VW - tw) / 2), ty, title, Palette.UIGood, Palette.UIPanelDark, ts);
            Centered(ty + 9 * ts + 12, "Vom kleinen Dorf zum Königreich", Palette.UISand);

            bool hasSave = SaveSystem.Exists;
            int bs = VW > 400 ? 2 : 1;
            float bw = Mathf.Min(VW - 40, bs == 2 ? 280 : 200), bh = bs == 2 ? 30 : 22, gap = 8;
            int n = hasSave ? 5 : 4;
            float bx = Mathf.Round((VW - bw) / 2), by = Mathf.Round(Mathf.Max(plate.yMax + 16, VH * 0.42f));
            var box = new Rect(bx - 14, by - 14, bw + 28, n * (bh + gap) - gap + 28);
            Panel(box);
            if (hasSave)
            {
                if (Button(new Rect(bx, by, bw, bh), "Fortsetzen", true, "Weiterspielen. Die Zeit seit dem letzten Spiel wird nachgeholt.", "play", false, bs)) gm.ContinueGame();
                by += bh + gap;
            }
            if (Button(new Rect(bx, by, bw, bh), confirmNew ? "Wirklich neu?" : "Neues Spiel", true, confirmNew ? "Der alte Spielstand wird überschrieben." : null, "flag", confirmNew, bs))
            {
                if (hasSave && !confirmNew) confirmNew = true;
                else { confirmNew = false; gm.NewGame(); }
            }
            by += bh + gap;
            if (Button(new Rect(bx, by, bw, bh), "Farben: " + Palette.Name, true, "Farbpalette der Karte wechseln", "sun", false, bs)) gm.CyclePalette();
            by += bh + gap;
            if (Button(new Rect(bx, by, bw, bh), "Hilfe", true, null, "scroll", false, bs)) { Open = Open == Window.Help ? Window.None : Window.Help; }
            by += bh + gap;
            if (Button(new Rect(bx, by, bw, bh), "Beenden", true, null, "close", false, bs)) Application.Quit();
            string foot = "Ein Städtebauspiel im Mittelalter. Speichert automatisch.";
            var fr = new Rect(4, VH - 16, PixelFont.Width(foot) + 10, 13);
            Fill(fr, new Color(Palette.UIPanelDark.r, Palette.UIPanelDark.g, Palette.UIPanelDark.b, 0.85f));
            Text(fr.x + 5, fr.y + 3, foot, Palette.UITextDim);
        }
    }
}
