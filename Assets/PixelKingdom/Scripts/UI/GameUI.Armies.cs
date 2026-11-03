using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Heerführung: alle eigenen Heere und Garnisonen auf einen Blick, Ziele aus einer Liste wählen,
    /// Heere teilen, vereinen, Truppen mit der Garnison tauschen.
    /// </summary>
    public partial class GameUI
    {
        int armyWinSel = -1;
        float scrollArmyList, scrollTargets;

        void DrawArmies()
        {
            var c = WindowFrame(Loc.T("Heerführung"), 560, 340, false);
            var mine = G.ArmiesOf(G.PlayerId).ToList();
            var a = G.A(armyWinSel);
            if (a == null || a.faction != G.PlayerId) { a = G.A(gm.SelArmy); if (a != null && a.faction != G.PlayerId) a = null; }
            if (a == null && mine.Count > 0) a = mine[0];
            armyWinSel = a != null ? a.id : -1;

            // ---- Liste links: Heere und Garnisonen --------------------------------
            var list = new Rect(c.x, c.y, 170, c.height);
            Fill(list, Palette.UIPanelDark);
            var rows = new List<(string text, string sub, int armyId, int settlementId)>();
            foreach (var ar in mine) rows.Add((ar.name, Loc.F("{0} Mann - {1}", (ar.units.Sum() + ar.settlers), (MissionShort(ar))), ar.id, -1));
            rows.Add((Loc.T("__Garnisonen__"), null, -1, -1));
            foreach (var s in G.SettlementsOf(G.PlayerId))
            {
                int n = s.garrison.Sum();
                rows.Add((s.name, n == 0 ? Loc.T("keine Truppen") : Loc.F("{0} Soldaten", (n)), -1, s.id));
            }
            float rowH = 22;
            scrollArmyList = Scroll(list, scrollArmyList, rows.Count * rowH - list.height + 4);
            BeginGroup(list);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rr = new Rect(2, 2 + i * rowH - scrollArmyList, list.width - 4, rowH - 2);
                if (rr.yMax < 0 || rr.y > list.height) continue;
                if (row.sub == null) { Text(rr.x + 4, rr.y + 8, row.text, Palette.UISand); continue; }
                bool sel = row.armyId >= 0 && row.armyId == armyWinSel;
                Fill(rr, sel ? Palette.UISand : Hover(rr) ? Palette.UIPanelLight : Palette.UIPanel);
                var fg = sel ? Palette.UIPanelDark : Palette.UIText;
                Icon(new Rect(rr.x + 3, rr.y + 4, 12, 12), row.armyId >= 0 ? "army" : "house", fg);
                string nm = row.text.Length > 22 ? row.text.Substring(0, 21) + "." : row.text;
                Text(rr.x + 18, rr.y + 2, nm, fg);
                Text(rr.x + 18, rr.y + 11, row.sub, sel ? Palette.UIPanelDark : Palette.UITextDim);
                if (Clicked(rr))
                {
                    if (row.armyId >= 0) { armyWinSel = row.armyId; var ar = G.A(row.armyId); if (ar != null) gm.SelectArmy(ar, true); }
                    else { var st = G.St(row.settlementId); if (st != null) { gm.SelectSettlement(st); SettlementTab = 2; Open = Window.None; } }
                }
            }
            EndGroup();
            rects.Add(list);

            float x = c.x + 180, y = c.y, w = c.xMax - x;
            if (a == null)
            {
                Wrapped(new Rect(x, y, w, 120), Loc.T("Ihr habt kein Heer im Feld.\n\nSo stellt ihr eins auf: Siedlung wählen, Reiter 'Truppen' öffnen, Soldaten ausbilden und unter 'Heer aufstellen' auswählen, dann 'Ausrücken'. Danach könnt ihr es hier oder direkt auf der Karte befehligen."));
                return;
            }

            // ---- Kopf: Zusammensetzung, Zustand, aktueller Befehl -------------------
            Text(x, y, "__" + a.name + "__");
            y += 12;
            var near = G.NearestSettlementOf(G.PlayerId, a.x, a.y);
            string where = G.Map.BiomeName(WorldMap.Idx(Mathf.Clamp((int)a.x, 0, WorldMap.W - 1), Mathf.Clamp((int)a.y, 0, WorldMap.H - 1)));
            var under = G.SettlementUnder(a);
            Text(x, y, under != null ? Loc.F("__Standort__: in {0}", (under.name)) : Loc.F("__Standort__: {0}", (where)) + (near != null ? Loc.F(", {0} Felder von {1}", (Mathf.RoundToInt(Game.Dist(near.x, near.y, (int)a.x, (int)a.y))), (near.name)) : ""), Palette.UITextDim);
            y += 12;
            float ux = x;
            for (int u = 0; u < Units.Count; u++)
            {
                if (a.units[u] == 0) continue;
                Icon(new Rect(ux, y, 12, 12), Units.Get(u).Icon, Palette.UIText);
                Text(ux + 14, y + 2, a.units[u].ToString());
                Tip(new Rect(ux, y, 40, 12), Units.Get(u).Plural);
                ux += 42;
            }
            if (a.settlers > 0) { Icon(new Rect(ux, y, 12, 12), "cart", Palette.UIText); Text(ux + 14, y + 2, a.settlers + Loc.T(" Siedler")); }
            y += 15;
            Text(x, y, Loc.F("__Stärke__: {0}   __Moral__: {1}%   __Tempo__: {2:0.#} Felder/Tag", ((int)Game.Strength(a.units)), ((int)(a.morale * 100)), (G.ArmySpeed(a))), Palette.UITextDim);
            y += 12;
            Text(x, y, Loc.T("__Befehl__: ") + MissionLong(a), Palette.UIGood);
            y += 15;

            // ---- Ziele -----------------------------------------------------------------
            Text(x, y, Loc.T("__Ziel wählen__ (nächste Orte):"), Palette.UISand);
            if (Button(new Rect(x + w - 128, y - 3, 128, 14), Loc.T("Ziel auf Karte"), true, PKInput.UsingTouch ? Loc.T("Danach auf die Karte tippen.") : Loc.T("Danach auf die Karte klicken (oder immer: Rechtsklick)."), "eye"))
            {
                gm.SelectArmy(a); gm.CurrentMode = GameManager.Mode.TargetArmy; Open = Window.None;
            }
            y += 13;
            var targets = G.Settlements.Where(s => G.IsDiscovered(s.x, s.y) && s != under)
                                       .OrderBy(s => Game.Dist(s.x, s.y, (int)a.x, (int)a.y)).Take(30).ToList();
            float listH = 92;
            var tl = new Rect(x, y, w, listH);
            Fill(tl, Palette.UIPanelDark);
            scrollTargets = Scroll(tl, scrollTargets, targets.Count * 15 - listH + 2);
            BeginGroup(tl);
            for (int i = 0; i < targets.Count; i++)
            {
                var s = targets[i];
                var rr = new Rect(2, 1 + i * 15 - scrollTargets, tl.width - 4, 14);
                if (rr.yMax < 0 || rr.y > tl.height) continue;
                var f = G.F(s.faction);
                bool own = s.faction == G.PlayerId;
                bool war = !own && G.AtWar(G.PlayerId, s.faction);
                string tag = own ? Loc.T("eigen") : war ? Loc.T("KRIEG") : G.Status(G.PlayerId, s.faction) == DipStatus.Alliance ? Loc.T("verbündet") : Loc.T("neutral");
                Fill(new Rect(rr.x, rr.y + 3, 6, 8), Palette.FactionColor(f.color));
                string nm = s.name.Length > 13 ? s.name.Substring(0, 12) + "." : s.name;
                Text(rr.x + 9, rr.y + 3, nm, Palette.UIText);
                Text(rr.x + 82, rr.y + 3, tag, war ? Palette.UIGood : Palette.UITextDim);
                float days = Game.Dist(s.x, s.y, (int)a.x, (int)a.y) * 1.3f / Mathf.Max(0.5f, G.ArmySpeed(a));
                Text(rr.x + 132, rr.y + 3, $"~{Mathf.CeilToInt(days)} T", Palette.UITextDim);
                float bx = rr.xMax - 2;
                if (own)
                {
                    bx -= 58;
                    if (Button(new Rect(bx, rr.y, 58, 13), Loc.T("Einrücken"), true, Loc.T("Hinmarschieren und in die Garnison aufnehmen"))) Order(a, s, ArmyMission.Return);
                }
                else
                {
                    bx -= 50;
                    if (Button(new Rect(bx, rr.y, 50, 13), Loc.T("Überfall"), true, Loc.T("Plündern und heimkehren"))) OrderOrAsk(a, s, true);
                    bx -= 50;
                    if (Button(new Rect(bx, rr.y, 48, 13), Loc.T("Erobern"), true, Loc.T("Die Stadt angreifen und einnehmen"))) OrderOrAsk(a, s, false);
                }
                bx -= 30;
                if (Button(new Rect(bx, rr.y, 28, 13), Loc.T("Hin"), true, Loc.T("Nur hinmarschieren, nicht angreifen"))) Order(a, s, ArmyMission.Move);
            }
            EndGroup();
            rects.Add(tl);
            y += listH + 5;

            // ---- Verwaltung ----------------------------------------------------------------
            float bw = (w - 8) / 5f;
            if (Button(new Rect(x, y, bw, 15), Loc.T("Heim"), true, Loc.T("Zur nächsten eigenen Siedlung, Truppen gehen in die Garnison"), "house")) G.SendHome(a);
            if (Button(new Rect(x + (bw + 2), y, bw, 15), Loc.T("Erkunden"), a.units.Sum() > 0, Loc.T("Zieht selbstständig umher und deckt die Karte auf"), "eye")) G.ExploreNext(a);
            if (Button(new Rect(x + (bw + 2) * 2, y, bw, 15), Loc.T("Teilen"), a.units.Sum() > 1, Loc.T("Die Hälfte jeder Truppenart bildet ein eigenes Heer"))) { var b = G.SplitArmy(a); if (b != null) { armyWinSel = b.id; Toast(Loc.T("Heer geteilt."), 0); } }
            var other = mine.FirstOrDefault(o => o.id != a.id && Mathf.Abs(o.x - a.x) < 1.6f && Mathf.Abs(o.y - a.y) < 1.6f);
            if (Button(new Rect(x + (bw + 2) * 3, y, bw, 15), Loc.T("Vereinen"), other != null, other != null ? Loc.T("Mit ") + other.name + " zusammenlegen" : Loc.T("Kein anderes Heer in der Nähe"))) G.Merge(a, other);
            if (Button(new Rect(x + (bw + 2) * 4, y, bw, 15), Loc.T("Auflösen"), true, Loc.T("Soldaten kehren als Bürger heim"), null, false, 1, true)) { G.Disband(a); armyWinSel = -1; gm.SelArmy = -1; return; }
            y += 19;

            // ---- Truppentausch mit der Garnison --------------------------------------------
            if (under != null && y + 14 < c.yMax)
            {
                Text(x, y + 2, Loc.F("__Tausch mit {0}__ (Heer / Garnison):", (under.name)), Palette.UISand);
                y += 13;
                float tx = x;
                int step = PKInput.Key(PKKey.Shift) ? 10 : 1;
                for (int u = 0; u < Units.Count; u++)
                {
                    if (a.units[u] == 0 && under.garrison[u] == 0) continue;
                    if (tx + 92 > c.xMax) { tx = x; y += 14; }
                    if (y + 13 > c.yMax) break;
                    Icon(new Rect(tx, y, 12, 12), Units.Get(u).Icon, Palette.UIText);
                    if (Button(new Rect(tx + 13, y, 11, 12), "<", under.garrison[u] > 0, Loc.T("Aus der Garnison ins Heer (Shift: 10)"))) G.ExchangeTroops(a, under, u, step);
                    Text(tx + 26, y + 2, $"{a.units[u]}/{under.garrison[u]}");
                    if (Button(new Rect(tx + 76, y, 11, 12), ">", a.units[u] > 0, Loc.T("Aus dem Heer in die Garnison (Shift: 10)"))) G.ExchangeTroops(a, under, u, -step);
                    tx += 92;
                }
            }
        }

        void Order(ArmyData a, SettlementData s, ArmyMission m)
        {
            if (!G.OrderMove(a, s.x, s.y, m, s.id, out string why)) Toast(why, 2);
            else Toast(m == ArmyMission.Return ? Loc.F("{0} rückt in {1} ein.", (a.name), (s.name)) : Loc.F("{0} marschiert nach {1}.", (a.name), (s.name)), 0);
        }

        void OrderOrAsk(ArmyData a, SettlementData s, bool raid)
        {
            if (!G.AtWar(G.PlayerId, s.faction)) { AskWar(G.F(s.faction), a, s, raid); return; }
            gm.OrderAttack(a, s, raid);
        }

        string MissionShort(ArmyData a)
        {
            switch ((ArmyMission)a.mission)
            {
                case ArmyMission.Attack: return Loc.T("greift an");
                case ArmyMission.Raid: return Loc.T("Überfall");
                case ArmyMission.Return: return Loc.T("kehrt heim");
                case ArmyMission.Found: return Loc.T("siedelt");
                case ArmyMission.Explore: return Loc.T("erkundet");
                case ArmyMission.Move: return Loc.T("marschiert");
                default: return Loc.T("wartet");
            }
        }

        string MissionLong(ArmyData a)
        {
            var t = G.St(a.target);
            string s;
            switch ((ArmyMission)a.mission)
            {
                case ArmyMission.Attack: s = Loc.T("Erobern: ") + (t?.name ?? "?"); break;
                case ArmyMission.Raid: s = Loc.T("Überfall auf ") + (t?.name ?? "?"); break;
                case ArmyMission.Return: s = Loc.T("Einrücken in ") + (t?.name ?? "?"); break;
                case ArmyMission.Found: s = Loc.T("Neue Siedlung gründen"); break;
                case ArmyMission.Explore: s = Loc.T("Erkundet die Gegend"); break;
                case ArmyMission.Move: s = Loc.T("Marschiert") + (t != null ? " nach " + t.name : ""); break;
                default: return Loc.T("Keiner – wartet auf Befehle");
            }
            if (a.path.Count > 0 && a.pathIndex < a.path.Count)
            {
                float remaining = 0;
                for (int k = a.pathIndex; k < a.path.Count; k++) remaining += G.Map.MoveCost(a.path[k], true);
                s += Loc.F(" (noch ~{0} Tage)", (Mathf.CeilToInt(remaining / Mathf.Max(0.1f, G.ArmySpeed(a)))));
            }
            return s;
        }
    }
}
