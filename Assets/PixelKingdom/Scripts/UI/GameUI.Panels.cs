using System.Linq;
using UnityEngine;

namespace PixelKingdom
{
    public partial class GameUI
    {
        readonly int[] armySel = new int[Units.Count];
        int armySelFor = -1;

        void DrawSelectionPanel()
        {
            var s = G.St(gm.SelSettlement);
            if (s != null)
            {
                if (s.faction == G.PlayerId) DrawOwnSettlement(s);
                else DrawForeignSettlement(s);
                return;
            }
            var a = G.A(gm.SelArmy);
            if (a != null) { DrawArmy(a); return; }
            var poi = G.Poi(gm.SelPoi);
            if (poi != null) { DrawPoi(poi); return; }
            var bs = G.St(gm.SelBuildingSet);
            if (bs != null && bs.faction == G.PlayerId) DrawBuilding(bs);
        }

        // ------------------------------------------------------------------
        //  Ein einzelnes Gebäude: Ertrag, Probleme, Abreißen, Verlegen
        // ------------------------------------------------------------------
        bool confirmDemolish;

        void DrawBuilding(SettlementData s)
        {
            var b = s.placed.Find(q => q.x == gm.SelBuildingX && q.y == gm.SelBuildingY);
            if (b == null) { gm.SelBuildingSet = -1; return; }
            var t = (BType)b.type; var d = Buildings.Get(t);
            var r = BottomPanel(400, 84);
            Panel(r);
            if (CloseButton(r)) { gm.SelBuildingSet = -1; confirmDemolish = false; }
            string spr = t == BType.Wohnhaus ? "b_Wohnhaus0" : "b_" + t;
            SpriteLib.DrawUI(new Rect(r.x + 6, r.y + 6, 44, 40), spr);
            float x = r.x + 56, y = r.y + 7;
            Text(x, y, "__" + d.Name + "__", Palette.UIText);
            Text(x + PixelFont.Width(d.Name) + 8, y, "- " + s.name, Palette.UITextDim);
            y += 12;
            float site = G.SiteBonus(t, b.x, b.y), way = G.DistanceFactor(s, b.x, b.y);
            string fn = Game.SiteFactorName(t);
            if (fn != null || way < 0.999f) Text(x, y, Loc.F("Ertrag hier: {0} ({1})", Game.Rating(site * way), Game.Diff(site * way)), Palette.UIGood);
            else Text(x, y, d.Desc.Length > 60 ? d.Desc.Substring(0, 58) + "." : d.Desc, Palette.UITextDim);
            y += 11;
            foreach (var p in G.Problems)
                if (Mathf.Abs(p.X - (b.x + 0.5f)) < 0.1f && Mathf.Abs(p.Y - (b.y + 1.5f)) < 0.1f)
                {
                    Text(x, y, Game.ProblemText(p).Split('\n')[0].Replace("__", ""), Palette.UIBad);
                    y += 11; break;
                }
            float bx = r.xMax - 8 - 2 * 104, by = r.yMax - 19;
            var refund = G.DemolishRefund(t); var moveCost = G.RelocateCost(t);
            if (Button(new Rect(bx, by, 100, 14), Loc.T("Verlegen"), G.CanAfford(G.Player, moveCost), Loc.T("__Verlegen__\nAn einen neuen Platz versetzen.\n__Kosten__: ") + CostLong(moveCost, 1), "arrow"))
            { confirmDemolish = false; gm.BeginMove(); }
            if (Button(new Rect(bx + 104, by, 100, 14), confirmDemolish ? Loc.T("Sicher?") : Loc.T("Abreißen"), true, Loc.T("__Abreißen__\nDas Gebäude verschwindet.\n__Zurück__: ") + CostLong(refund, 1), "close", confirmDemolish))
            {
                if (!confirmDemolish) confirmDemolish = true;
                else { confirmDemolish = false; G.Demolish(s, b.x, b.y); gm.SelBuildingSet = -1; }
            }
        }

        void DrawPoi(PoiData p)
        {
            var r = BottomPanel(380, 84);
            Panel(r);
            if (CloseButton(r)) gm.SelPoi = -1;
            SpriteLib.DrawUI(new Rect(r.x + 6, r.y + 6, 44, 40), Game.PoiSprite(p));
            float x = r.x + 56, y = r.y + 7;
            Text(x, y, "__" + Game.PoiName(p) + "__"); y += 13;
            string desc = p.type >= 3 ? Game.PoiDescription(p) : p.type == 2
                ? Loc.T("Gaukler, Händler und Kesselflicker ohne festen Wohnsitz. Ein Heer oder Späher kann sie besuchen: handeln, sie zum Bleiben einladen – oder vertreiben. Bald ziehen sie weiter.")
                : p.type == 0
                ? (p.done ? Loc.T("Hier wurde schon alles durchsucht.") : Loc.T("Alte Mauern aus vergessener Zeit. Ein Heer oder Späher kann sie erkunden – Gold, Waffen, Relikte oder alte Karten warten. Manchmal auch eine Falle."))
                : Loc.F("Räuber plündern von hier aus die Gegend und machen den Leuten Angst. Hebt das Lager mit einem Heer aus! __Wachen__: ~{0} Mann.", (Mathf.Max(1, Mathf.RoundToInt(p.guards.Sum() / 5f) * 5)));
            Wrapped(new Rect(x, y, r.xMax - x - 8, 40), desc, Palette.UITextDim);
            if ((p.type == 0 || p.type == Game.PoiTablet || p.type == Game.PoiHoard) && p.done) return;
            var nearest = G.ArmiesOf(G.PlayerId).Where(a => a.units.Sum() > 0).OrderBy(a => (a.x - p.x) * (a.x - p.x) + (a.y - p.y) * (a.y - p.y)).FirstOrDefault();
            string label = p.type == 1 || p.type == Game.PoiHoard ? Loc.T("Nächstes Heer angreifen lassen") : Loc.T("Nächstes Heer schicken");
            if (Button(new Rect(r.xMax - 168, r.yMax - 19, 160, 14), label, nearest != null, nearest != null ? Loc.T("Schickt ") + nearest.name : Loc.T("Ihr habt kein Heer im Feld. Stellt eines in einer Siedlung auf (Reiter 'Truppen')."), "army"))
                gm.SendToPoi(nearest, p);
        }

        Rect BottomPanel(float wantW, float h)
        {
            float minimapW = Mathf.Min(160, VW * 0.17f) + 10;
            float w = Mathf.Min(wantW, VW - minimapW - 8);
            float x = Mathf.Max(minimapW, (VW - w) / 2f);
            if (x + w > VW - 4) x = VW - 4 - w;
            return new Rect(Mathf.Round(x), Mathf.Round(VH - h - 4), Mathf.Round(w), h);
        }

        bool CloseButton(Rect panel)
        {
            return Button(new Rect(panel.xMax - 16, panel.y + 3, 13, 12), "", true, Loc.T("Schließen"), "close");
        }

        // ==================================================================
        //  Eigene Siedlung
        // ==================================================================
        void DrawOwnSettlement(SettlementData s)
        {
            var r = BottomPanel(580, 136);
            string[] tabs = { Loc.T("Übersicht"), Loc.T("Bauen"), Loc.T("Truppen"), Loc.T("Markt"), Loc.T("Hafen") };
            // Reiter erscheinen erst, wenn es sie braucht: Markt mit Markt/Hafen, Hafen nur in Hafenstädten
            var shown = new System.Collections.Generic.List<int> { 0, 1, 2 };
            if (G.HasMarket(G.PlayerId)) shown.Add(3);
            if (s.buildings[(int)BType.Hafen] > 0) shown.Add(4);
            if (!shown.Contains(SettlementTab)) SettlementTab = 0;
            for (int k = 0; k < shown.Count; k++)
            {
                int i = shown[k];
                var tr = new Rect(r.x + 4 + k * 66, r.y - 15, 64, 16);
                if (Button(tr, tabs[i], true, i == 1 ? Loc.T("Gebäude errichten (B)") : null, null, SettlementTab == i))
                {
                    SettlementTab = i;
                    if (i == 2) G.Hint("tab_troops", Loc.T("Truppen: Oben bildet ihr Soldaten aus – sie kommen aus der Bevölkerung. Rechts stellt ihr aus der Garnison ein Heer zusammen und lasst es mit 'Ausrücken' losziehen. Speere schlagen Ritter, Ritter schlagen Bogenschützen, Bogenschützen schlagen Speere."));
                    if (i == 4) G.Hint("tab_harbor", Loc.T("Der Hafen: Hier baut ihr Handelsschiffe (Gold durch Seehandel) und Kriegsschiffe (tragen Heere übers Meer, schützen vor Piraten). Wie groß und schnell eure Schiffe sind, hängt vom Wissen der Schiffbauer und von der Größe eurer Werft ab."));
                    if (i == 3) G.Hint("tab_market", Loc.T("Der Markt: Tauscht Waren gegen Gold und umgekehrt. Ihr braucht dafür einen Markt oder einen Hafen."));
                }
            }
            Panel(r);
            Mark("panel", new Rect(r.x, r.y - 15, r.width, r.height + 15));
            var f = G.F(s.faction);
            Text(r.x + 8, r.y + 6, s.name, Palette.UIText);
            string sub = G.Capital(f) == s ? Loc.T("Hauptstadt") : Loc.T("Siedlung");
            Text(r.x + 14 + PixelFont.Width(s.name), r.y + 6, "- " + sub + " - " + G.Map.BiomeName(WorldMap.Idx(s.x, s.y)), Palette.UITextDim);
            if (CloseButton(r)) gm.SelSettlement = -1;
            var content = new Rect(r.x + 6, r.y + 20, r.width - 12, r.height - 24);
            switch (SettlementTab)
            {
                case 0: TabOverview(s, content); break;
                case 1: TabBuild(s, content); break;
                case 2: TabTroops(s, content); break;
                case 3: TabMarket(content); break;
                case 4: TabHarbor(s, content); break;
            }
        }

        void TabOverview(SettlementData s, Rect c)
        {
            float x = c.x + 2, y = c.y + 2;
            int housing = G.Housing(s);
            // Bevölkerung
            Icon(new Rect(x, y, 12, 12), "people", Palette.UIText);
            Text(x + 16, y + 2, Loc.F("Bewohner {0} / {1}", ((int)s.pop), (housing)));
            Bar(new Rect(x + 16, y + 12, 140, 2), s.pop / housing, Palette.UIText);
            Tip(new Rect(x, y, 160, 14), Loc.T("__Bewohner__\nWohnhäuser schaffen Platz. Wachstum braucht Nahrung und Zufriedenheit."));
            y += 18;
            // Zufriedenheit
            Icon(new Rect(x, y, 12, 12), s.happiness >= 40 ? "smile" : "angry", Palette.UIText);
            Text(x + 16, y + 2, Loc.F("Zufriedenheit {0}", ((int)s.happiness)));
            Bar(new Rect(x + 16, y + 12, 140, 2), s.happiness / 100f, s.happiness < 25 ? Palette.UIBad : Palette.UIText);
            var reasons = new System.Collections.Generic.List<string>();
            float target = G.HappinessTarget(s, reasons);
            Tip(new Rect(x, y, 160, 14), Loc.F("__Zufriedenheit__\n__Zielwert__: {0}\n__Einflüsse__:\n", ((int)target)) + string.Join("\n", reasons));
            y += 18;
            // Arbeit
            int jobs = G.Jobs(s);
            Text(x, y + 2, Loc.F("Arbeiter {0} / Stellen {1}", ((int)(s.pop * 0.6f)), (jobs)), Palette.UITextDim);
            Tip(new Rect(x, y, 160, 12), Loc.T("__Arbeit__\n60% der Bewohner arbeiten. Nahrungsbetriebe werden zuerst besetzt. Fehlen Arbeiter, sinkt die Produktion."));
            y += 14;
            // Steuern
            Text(x, y + 3, Loc.T("Steuern"));
            if (Button(new Rect(x + 50, y, 14, 13), "-", s.taxRate > 0.001f)) s.taxRate = Mathf.Max(0, s.taxRate - 0.05f);
            Text(x + 69, y + 3, $"{Mathf.RoundToInt(s.taxRate * 100)}%");
            if (Button(new Rect(x + 92, y, 14, 13), "+", s.taxRate < 0.349f)) s.taxRate = Mathf.Min(0.35f, s.taxRate + 0.05f);
            Tip(new Rect(x, y, 110, 13), Loc.T("__Steuern__\nMehr Steuern = mehr Gold, aber weniger Zufriedenheit."));
            y += 16;
            Text(x, y + 3, Loc.T("Rationen"));
            string[] rn = { Loc.T("Knapp"), Loc.T("Normal"), Loc.T("Üppig") };
            for (int i = 0; i < 3; i++)
                if (Button(new Rect(x + 50 + i * 38, y, 36, 13), rn[i], true, i == 0 ? Loc.T("-25% Verbrauch, unzufriedener") : i == 2 ? Loc.T("+30% Verbrauch, zufriedener, mehr Wachstum") : null, null, s.rations == i)) s.rations = i;
            y += 16;
            if (Button(new Rect(x, y, 164, 13), s.auto ? Loc.T("Verwalter: AN") : Loc.T("Verwalter: AUS"), true,
                Loc.T("Ein Verwalter baut automatisch, was die Siedlung braucht. Ideal, wenn ihr nur ab und zu spielt."), null, s.auto)) s.auto = !s.auto;

            // Bauschlange
            float qx = c.x + 178, qy = c.y + 2;
            Text(qx, qy + 2, Loc.T("In Arbeit"), Palette.UITextDim);
            qy += 13;
            if (s.queue.Count == 0) Text(qx, qy + 2, Loc.T("Nichts. Baut etwas im Reiter 'Bauen'."), Palette.UITextDim);
            for (int i = 0; i < s.queue.Count && i < 6; i++)
            {
                var q = s.queue[i];
                string name = q.kind == 0 ? Buildings.Get(q.type).Name : q.kind == 2 ? (q.type == 1 ? Loc.T("Kriegsschiff") : Loc.T("Handelsschiff")) : q.count + " " + Units.Get(q.type).Plural;
                string icon = q.kind == 0 ? Buildings.Get(q.type).Icon : q.kind == 2 ? "anchor" : Units.Get(q.type).Icon;
                Icon(new Rect(qx, qy, 12, 12), icon, Palette.UIText);
                Text(qx + 15, qy + 1, name);
                if (i == 0)
                {
                    Bar(new Rect(qx + 15, qy + 10, 108, 2), 1f - q.daysLeft / Mathf.Max(0.01f, q.total), Palette.UIGood);
                    int sc = G.SpeedUpCost(s);
                    if (Button(new Rect(qx + 126, qy, 12, 11), "", G.Player.res[(int)Res.Gold] >= sc, Loc.F("__Beschleunigen__\nMehr Arbeiter und Fuhrwerke: morgen fertig.\n__Kosten__: {0} Gold", sc), "fast", false, 1, true)) G.SpeedUp(s);
                }
                else Text(qx + 130, qy + 1, "...", Palette.UITextDim);
                if (Button(new Rect(qx + 140, qy, 12, 11), "", true, Loc.T("Abbrechen (80% zurück)"), "close")) { G.CancelQueue(s, i); break; }
                qy += 15;
            }

            // Garnison & Aktionen
            float gx = c.x + 344, gy = c.y + 2;
            if (gx + 150 > c.xMax) return;
            Text(gx, gy + 2, Loc.F("Garnison {0}/{1}", G.GarrisonCount(s), G.GarrisonCap(s)), G.GarrisonFree(s) == 0 ? Palette.UIGood : Palette.UITextDim);
            Tip(new Rect(gx, gy, 120, 11), Loc.T("__Garnison__\nSo viele Soldaten haben in der Stadt Platz. Jede Kaserne bietet 40 Plätze, Archerie, Stall und Burg weitere. Was keinen Platz hat, lagert als Heer vor der Stadt – oder in einem Feldlager."));
            gy += 13;
            int shown = 0;
            for (int u = 0; u < Units.Count; u++)
            {
                if (s.garrison[u] == 0) continue;
                float ux = gx + (shown % 3) * 46, uy = gy + (shown / 3) * 14;
                Icon(new Rect(ux, uy, 12, 12), Units.Get(u).Icon, Palette.UIText);
                Text(ux + 14, uy + 2, s.garrison[u].ToString());
                Tip(new Rect(ux, uy, 44, 12), Units.Get(u).Plural);
                shown++;
            }
            if (shown == 0) Text(gx, gy + 2, Loc.T("Keine Truppen"), Palette.UITextDim);
            gy += 30;
            float def = G.DefenseStrength(s);
            Text(gx, gy, Loc.F("Verteidigung {0}", ((int)def)), Palette.UIText);
            Tip(new Rect(gx, gy, 120, 10), Loc.F("__Verteidigung__\n__Truppen__: Garnison + Aufgebot ({0} Bürger)\n__Bonus__: +{1}% durch Mauern/Gelände", (G.Levy(s)), (Mathf.RoundToInt(G.DefenseBonus(s) * 100))));
            gy += 14;
            if (Button(new Rect(gx, gy, 150, 14), Loc.T("Heer aufstellen"), s.garrison.Sum() > 0, Loc.T("Truppen aus der Garnison ins Feld schicken"), "army")) SettlementTab = 2;
            gy += 17;
            bool canSettle = G.CanSendSettlers(s, out string why);
            if (Button(new Rect(gx, gy, 150, 14), Loc.T("Siedler aussenden"), canSettle,
                canSettle ? Loc.T("12 Bewohner gründen eine neue Siedlung. Kosten: 40 Nahrung, 50 Holz, 30 Gold.") : why, "cart"))
            {
                gm.CurrentMode = GameManager.Mode.PlaceSettlers; gm.ModeSource = s.id;
            }
        }

        void TabBuild(SettlementData s, Rect c)
        {
            var f = G.Player;
            float cardW = 50, cardH = 50, gap = 8;
            // Nur freigeschaltete Gebäude zeigen – der Anfang bleibt übersichtlich
            var list = new System.Collections.Generic.List<int>();
            var next = new System.Collections.Generic.List<string>();
            for (int b = 0; b < Buildings.Count; b++)
            {
                var bd = Buildings.Get(b);
                if (bd.MinRank <= f.rank) list.Add(b);
                else if (bd.MinRank == f.rank + 1) next.Add(bd.Name);
            }
            int count = list.Count;
            float totalW = count * (cardW + gap) + (next.Count > 0 ? 120 : 0);
            scrollCards = Scroll(c, scrollCards, totalW - c.width + 20, true);   // Mausrad oder seitlich wischen
            // Pfeile
            if (scrollCards > 0 && Button(new Rect(c.x, c.y + 30, 10, 24), "<")) scrollCards = Mathf.Max(0, scrollCards - 120);
            if (scrollCards < totalW - c.width + 20 && Button(new Rect(c.xMax - 10, c.y + 30, 10, 24), ">")) scrollCards += 120;
            var clip = new Rect(c.x + 12, c.y, c.width - 24, c.height);
            BeginGroup(clip);
            if (next.Count > 0)
            {
                float nx = count * (cardW + gap) - scrollCards;
                Text(nx + 2, 16, Loc.T("Ab Rang"), Palette.UITextDim);
                Text(nx + 2, 26, Ranks.Get(f.rank + 1).Name + ":", Palette.UITextDim);
                for (int k = 0; k < next.Count && k < 5; k++) Text(nx + 6, 38 + k * 9, next[k], Palette.UITextDim);
            }
            for (int li = 0; li < count; li++)
            {
                int b = list[li];
                var d = Buildings.Get(b);
                float x = li * (cardW + gap) - scrollCards;
                if (x + cardW < 0 || x > clip.width) continue;
                bool ok = G.CanBuild(s, d.Type, out string why);
                bool locked = !ok && why != Loc.T("Nicht genug Ressourcen");
                int have = s.buildings[b];
                int queued = G.CountWithQueue(s, d.Type) - have;
                // Name über der Karte (wie im Vorbild)
                string nm = d.Name.Length > 11 ? d.Name.Substring(0, 10) + "." : d.Name;
                Text(x + (cardW - PixelFont.Width(nm)) / 2, 2, nm, locked ? Palette.UITextDim : Palette.UIText);
                var card = new Rect(x, 13, cardW, cardH);
                bool hov = Hover(card);
                Fill(new Rect(card.x + 2, card.y + 2, card.width, card.height), Palette.UIShadow);
                Fill(card, locked ? Palette.UIPanelLight : Palette.UISand);
                Fill(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), locked ? Palette.UIPanelLight : (hov && ok ? Palette.UIText : Palette.UISand));
                Fill(new Rect(card.x + 3, card.y + 3, card.width - 6, card.height - 6), locked ? Palette.UIPanel : Palette.UISand);
                var ic = locked ? Palette.UIPanelLight : Palette.UIPanelDark;
                string spr = d.Type == BType.Wohnhaus ? "b_Wohnhaus0" : "b_" + d.Type;
                if (!SpriteLib.DrawUI(new Rect(card.x + 3, card.y + 9, card.width - 6, card.height - 11), spr, locked ? new Color(1, 1, 1, 0.45f) : (Color?)null))
                    PixelIcons.Draw(new Rect(card.x + (cardW - 24) / 2, card.y + 14, 24, 24), d.Icon, ic);
                Text(card.x + 5, card.y + 5, have.ToString() + (queued > 0 ? "+" + queued : ""), ic);
                if (!ok && !locked) Fill(new Rect(card.xMax - 8, card.y + 5, 3, 3), Palette.UIBad);
                // Kosten unter der Karte
                float mult = G.BuildCostMult(s, d.Type);
                string cost = CostText(d.Cost, mult);
                Text(x + (cardW - PixelFont.Width(cost)) / 2, card.yMax + 4, cost, CanAfford(d.Cost, mult) ? Palette.UIText : Palette.UIGood);
                string tip = Loc.F("__{0}__\n{1}\n__Kosten__: {2}\n__Bauzeit__: ~{3} Tage", (d.Name), (d.Desc), (CostLong(d.Cost, mult)), (d.Days));
                if (d.Jobs > 0) tip += Loc.F("\n__Arbeiter__: {0}", (d.Jobs));
                if (BuildingEffects(d) != null) tip += Loc.T("\n__Wirkung__: ") + BuildingEffects(d);
                if (Game.NeedsSite(d.Type)) tip += Game.IsRemote(d.Type) ? Loc.T("\n__Bauplatz__: frei wählbar, auch außerhalb der Stadt") : Loc.T("\n__Bauplatz__: in der Stadt");
                if (!ok) tip += Loc.T("\n__Gesperrt__: ") + why;
                var cardScreen = new Rect(clip.x + card.x, clip.y + card.y, card.width, card.height);
                if (hov && !PKInput.UsingTouch) { tooltip = tip; tipPos = ptr; }
                if (Clicked(card))
                {
                    if (!ok) PinTip(tip);
                    else if (Game.NeedsSite(d.Type)) gm.BeginPlace(s, d.Type);
                    else if (TouchConfirm("bau" + b, tip) && G.QueueBuild(s, d.Type)) Toast(Loc.F("{0} wird gebaut.", (d.Name)), 0);
                }
            }
            EndGroup();
        }

        static string BuildingEffects(BuildingDef d)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int r = 0; r < ResInfo.Count; r++) if (d.Prod[r] > 0) parts.Add(Loc.F("+{0:0.#} {1}/Tag", (d.Prod[r]), (ResInfo.Name(r))));
            for (int r = 0; r < ResInfo.Count; r++) if (d.Use[r] > 0) parts.Add(Loc.F("-{0:0.#} {1}/Tag", (d.Use[r]), (ResInfo.Name(r))));
            if (d.Housing > 0) parts.Add(Loc.F("+{0} Wohnplätze", (d.Housing)));
            if (d.Happiness > 0) parts.Add(Loc.F("+{0:0} Zufriedenheit", (d.Happiness)));
            if (d.Defense > 0) parts.Add(Loc.F("+{0}% Verteidigung", (Mathf.RoundToInt(d.Defense * 100))));
            if (d.Storage > 0) parts.Add(Loc.F("+{0} Lager", (d.Storage)));
            if (d.Prestige > 0) parts.Add(Loc.T("Ansehen"));
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        bool CanAfford(float[] cost, float mult) => G.CanAfford(G.Player, cost, mult);

        static readonly LArr ResShort = new LArr("N", "H", "S", "E", "G", "W");
        static string CostText(float[] cost, float mult)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int r = 0; r < ResInfo.Count; r++) if (cost[r] > 0) parts.Add(ResShort[r] + Mathf.CeilToInt(cost[r] * mult));
            if (parts.Count > 2) return parts[0] + " " + parts[1] + "..";
            return string.Join(" ", parts);
        }
        static string CostLong(float[] cost, float mult)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int r = 0; r < ResInfo.Count; r++) if (cost[r] > 0) parts.Add(Mathf.CeilToInt(cost[r] * mult) + " " + ResInfo.Name(r));
            return parts.Count == 0 ? Loc.T("nichts") : string.Join(", ", parts);
        }

        // ------------------------------------------------------------------
        //  Hafen: Flotte, Schiffsklasse, Seehandel
        // ------------------------------------------------------------------
        void TabHarbor(SettlementData s, Rect c)
        {
            float x = c.x + 2, y = c.y + 1;
            int cls = G.ShipClass;
            Icon(new Rect(x, y, 12, 12), "anchor", Palette.UIText);
            Text(x + 16, y + 2, Loc.F("Flotte: {0} Handelsschiffe, {1} Kriegsschiffe (Platz für {2})", G.S.merchantShips, G.S.warShips, G.FleetMax));
            y += 14;
            Text(x + 16, y, Loc.F("Schiffsklasse: {0}", cls == 0 ? Loc.T("keine") : G.ShipClassName), Palette.UISand);
            string need;
            if (G.ShipTech == 0) need = Loc.T("Erforscht 'Schiffbau' (Reich > Wissen), um Schiffe zu bauen.");
            else if (cls < 3 && G.ShipTech <= cls) need = Loc.F("Nächste Klasse ({0}): Wissen der Schiffbauer erweitern ({1}).", Game.ShipClassNames[cls + 1], cls == 1 ? Loc.T("Kielbau") : Loc.T("Seekarten & Kompass"));
            else if (cls < 3) need = Loc.F("Nächste Klasse ({0}): größere Werft bauen.", Game.ShipClassNames[cls + 1]);
            else need = Loc.T("Die besten Schiffe der Welt.");
            Text(x + 16, y + 10, need, Palette.UITextDim);
            Tip(new Rect(x, y, 300, 20), Loc.F("__Schiffsklasse__\nKogge, Holk, Karavelle: größere Schiffe tragen mehr Soldaten, segeln schneller, verdienen mehr und kämpfen besser.\n__Wissen__: {0}  __Werft__: {1}\nDie Klasse ist das Kleinere von beidem.", Game.ShipClassNames[G.ShipTech], Game.ShipClassNames[Mathf.Min(3, G.YardLevel)]));
            y += 24;
            Text(x + 16, y, Loc.F("Seehandel: +{0:0.#} Gold/Tag   Unterhalt: -{1:0.#}", G.SeaTradeIncome, G.FleetUpkeep), Palette.UIText);
            Tip(new Rect(x, y, 300, 10), Loc.F("__Seehandel__\nJedes Handelsschiff bringt täglich Gold. Handelsabkommen und eigene Häfen auf anderen Kontinenten erhöhen den Ertrag (derzeit x{0:0.0}).", G.SeaTradeFactor));
            y += 11;
            Text(x + 16, y, Loc.F("Transport: bis {0} Mann pro Fahrt   Tempo auf See: x{1:0.0}", G.TransportCapacity, G.SeaSpeed), Palette.UIText);
            Tip(new Rect(x, y, 300, 10), Loc.T("__Transport__\nZieht ein Heer übers Meer, braucht es genug Schiffe: Kriegsschiffe tragen viele Soldaten, Handelsschiffe nur wenige. Kriegsschiffe beschießen außerdem Küstenstädte bei Belagerungen."));
            // Bauknöpfe
            float bx = c.xMax - 150, by = c.y + 4;
            foreach (bool war in new[] { false, true })
            {
                bool ok = G.CanBuildShip(s, war, out string why);
                string label = war ? Loc.T("Kriegsschiff bauen") : Loc.T("Handelsschiff bauen");
                string tip = (war ? Loc.T("__Kriegsschiff__\nTrägt Heere übers Meer, schützt vor Piraten, hilft bei Belagerungen.") : Loc.T("__Handelsschiff__\nBringt täglich Gold durch Seehandel."))
                             + Loc.T("\n__Kosten__: ") + CostLong(G.ShipCost(war), 1) + (ok ? "" : Loc.T("\n__Gesperrt__: ") + why);
                if (Button(new Rect(bx, by, 146, 15), label, ok, tip, war ? "sword" : "gold")) G.QueueShip(s, war);
                Text(bx + 4, by + 17, CostText(G.ShipCost(war), 1), CanAfford(G.ShipCost(war), 1) ? Palette.UITextDim : Palette.UIGood);
                by += 32;
            }
            if (s.buildings[(int)BType.Werft] == 0) Text(bx, by + 2, Loc.T("Tipp: Eine Werft erlaubt größere Schiffe."), Palette.UITextDim);
        }

        void TabTroops(SettlementData s, Rect c)
        {
            if (armySelFor != s.id) { System.Array.Clear(armySel, 0, armySel.Length); armySelFor = s.id; }
            float y = c.y + 1;
            float x = c.x + 2;
            var hidden = new System.Collections.Generic.List<string>();
            for (int u = 0; u < Units.Count; u++)
            {
                var d = Units.Get(u);
                if (d.MinRank > G.Player.rank || (d.Needs != BType.None && s.buildings[(int)d.Needs] == 0 && s.garrison[u] == 0))
                {
                    if (d.MinRank <= G.Player.rank) hidden.Add(Buildings.Get(d.Needs).Name);
                    continue;
                }
                bool ok1 = G.CanTrain(s, (UType)u, 1, out string why);
                bool locked = !ok1 && why != Loc.T("Nicht genug Ressourcen") && why != Loc.T("Zu wenige Bewohner") && why != Loc.T("Warteschlange voll");
                var rowR = new Rect(x, y, 300, 15);
                Icon(new Rect(x, y + 1, 12, 12), d.Icon, locked ? Palette.UITextDim : Palette.UIText);
                Text(x + 15, y + 3, d.Name, locked ? Palette.UITextDim : Palette.UIText);
                Text(x + 92, y + 3, "x" + s.garrison[u], Palette.UIText);
                Text(x + 120, y + 3, $"A{d.Atk} V{d.Def} L{d.Hp}", Palette.UITextDim);
                Text(x + 182, y + 3, CostText(d.Cost, 1), CanAfford(d.Cost, 1) ? Palette.UIText : Palette.UIGood);
                string tip = Loc.F("__{0}__\n{1}\n__Werte__: Angriff {2}, Verteidigung {3}, Leben {4}, Tempo {5}\n__Kosten__: {6} + 1 Bewohner\n__Ausbildung__: {7} Tage\n__Sold__: {8:0.00} Gold, {9:0.00} Nahrung pro Tag", (d.Name), (d.Desc), (d.Atk), (d.Def), (d.Hp), (d.Speed), (CostLong(d.Cost, 1)), (d.Days), (d.UpkeepGold), (d.UpkeepFood));
                if (d.StrongVs >= 0) tip += Loc.F("\n__Stark gegen__: {0}", (Units.Get(d.StrongVs).Plural));
                if (d.Needs != BType.None) tip += Loc.F("\n__Benötigt__: {0}", (Buildings.Get(d.Needs).Name));
                if (!ok1) tip += Loc.T("\n__Gesperrt__: ") + why;
                Tip(new Rect(x, y, 230, 14), tip);
                if (Button(new Rect(x + 236, y, 24, 13), "+1", ok1)) G.QueueTrain(s, (UType)u, 1);
                bool ok5 = G.CanTrain(s, (UType)u, 5, out _);
                if (Button(new Rect(x + 262, y, 24, 13), "+5", ok5)) G.QueueTrain(s, (UType)u, 5);
                y += 15;
            }
            if (hidden.Count > 0 && y < c.yMax - 20) { Text(x, y + 3, Loc.T("Weitere Truppen mit: ") + string.Join(", ", hidden), Palette.UITextDim); y += 11; }
            if (y < c.yMax - 10) Text(x, y + 3, Loc.F("Platz in der Garnison: {0} von {1} frei", G.GarrisonFree(s), G.GarrisonCap(s)), G.GarrisonFree(s) == 0 ? Palette.UIGood : Palette.UITextDim);
            if (G.Player.rank >= 1 && y < c.yMax - 10)
            {
                bool mok = G.CanHireMercs(s, out string mwhy);
                if (Button(new Rect(x + 190, y, 96, 13), Loc.F("Söldner ({0} G)", G.MercCost), mok,
                    Loc.F("__Söldner anheuern__\n{0} Speerträger, sofort bereit. Sie kosten nur Gold, keine Bewohner – werden aber jedes Mal teurer.", Game.MercCount) + (mok ? "" : Loc.T("\n__Gesperrt__: ") + mwhy), "sword", false, 1, true))
                    G.HireMercs(s);
            }

            // Heer aufstellen
            float ax = c.x + 300, ay = c.y + 1;
            if (ax + 160 > c.xMax) ax = c.xMax - 160;
            Fill(new Rect(ax - 6, c.y, 1, c.height), Palette.UIPanelDark);
            Text(ax, ay + 2, Loc.T("Heer aufstellen"), Palette.UITextDim);
            ay += 12;
            for (int u = 0; u < Units.Count; u++)
            {
                if (s.garrison[u] == 0) { armySel[u] = 0; continue; }
                armySel[u] = Mathf.Clamp(armySel[u], 0, s.garrison[u]);
                Icon(new Rect(ax, ay, 12, 12), Units.Get(u).Icon, Palette.UIText);
                if (Button(new Rect(ax + 16, ay, 12, 12), "-", armySel[u] > 0)) armySel[u] = Mathf.Max(0, armySel[u] - (PKInput.Key(PKKey.Shift) ? 10 : 1));
                Text(ax + 32, ay + 2, armySel[u] + "/" + s.garrison[u]);
                if (Button(new Rect(ax + 74, ay, 12, 12), "+", armySel[u] < s.garrison[u])) armySel[u] = Mathf.Min(s.garrison[u], armySel[u] + (PKInput.Key(PKKey.Shift) ? 10 : 1));
                if (Button(new Rect(ax + 90, ay, 26, 12), Loc.T("alle"))) armySel[u] = s.garrison[u];
                ay += 13;
            }
            int total = armySel.Sum();
            float by = c.yMax - 15;
            if (Button(new Rect(ax, by, 76, 14), Loc.T("Ausrücken"), total > 0, PKInput.UsingTouch ? Loc.T("Das Heer erscheint vor der Stadt. Dann auf ein Ziel tippen.") : Loc.T("Das Heer erscheint vor der Stadt. Dann Rechtsklick auf ein Ziel.")))
            {
                var a = G.FormArmy(s, (int[])armySel.Clone());
                System.Array.Clear(armySel, 0, armySel.Length);
                if (a != null) gm.SelectArmy(a);
            }
            if (Button(new Rect(ax + 80, by, 76, 14), Loc.T("Erkunden"), s.garrison[(int)UType.Spaeher] > 0, Loc.T("Schickt einen Späher, der selbstständig die Umgebung erkundet."), "eye"))
            {
                var sel = new int[Units.Count]; sel[(int)UType.Spaeher] = 1;
                var a = G.FormArmy(s, sel);
                if (a != null) { G.ExploreNext(a); Toast(Loc.T("Ein Späher zieht los."), 0); }
            }
        }

        void TabMarket(Rect c)
        {
            if (!G.HasMarket(G.PlayerId))
            {
                Wrapped(new Rect(c.x + 4, c.y + 10, c.width - 8, 40), Loc.T("Ihr habt noch keinen Markt. Baut einen Markt (ab Städtegemeinde) oder einen Hafen, um Waren zu kaufen und zu verkaufen."));
                return;
            }
            float y = c.y + 1;
            Text(c.x + 4, y + 2, Loc.T("Ware"), Palette.UITextDim); Text(c.x + 84, y + 2, Loc.T("Lager"), Palette.UITextDim);
            Text(c.x + 124, y + 2, Loc.T("Kauf"), Palette.UITextDim); Text(c.x + 160, y + 2, Loc.T("Verkauf"), Palette.UITextDim);
            y += 12;
            var p = G.Player;
            for (int r = 0; r < ResInfo.Count; r++)
            {
                if (r == (int)Res.Gold) continue;
                Icon(new Rect(c.x + 4, y, 12, 12), ResInfo.Icons[r], Palette.UIText);
                Text(c.x + 19, y + 2, ResInfo.Name(r));
                Text(c.x + 84, y + 2, ((int)p.res[r]).ToString());
                Text(c.x + 124, y + 2, G.Price(r, true).ToString("0.0"));
                Text(c.x + 160, y + 2, G.Price(r, false).ToString("0.0"));
                float bx = c.x + 206;
                foreach (int amt in new[] { 10, 50 })
                {
                    if (Button(new Rect(bx, y, 46, 12), Loc.T("Kauf ") + amt, p.res[(int)Res.Gold] >= G.Price(r, true) * amt)) G.Buy(r, amt);
                    bx += 48;
                    if (Button(new Rect(bx, y, 50, 12), Loc.T("Verk. ") + amt, p.res[r] >= amt)) G.Sell(r, amt);
                    bx += 52;
                }
                y += 14;
            }
        }

        // ==================================================================
        //  Fremde Siedlung
        // ==================================================================
        void DrawForeignSettlement(SettlementData s)
        {
            var r = BottomPanel(400, 96);
            Panel(r);
            if (CloseButton(r)) gm.SelSettlement = -1;
            var f = G.F(s.faction);
            Fill(new Rect(r.x + 8, r.y + 7, 8, 8), Palette.FactionColor(f.color));
            Text(r.x + 20, r.y + 7, s.name);
            Text(r.x + 26 + PixelFont.Width(s.name), r.y + 7, "- " + f.name, Palette.UITextDim);
            float y = r.y + 22;
            var st = G.Status(G.PlayerId, f.id);
            float rel = G.RelValue(G.PlayerId, f.id);
            Text(r.x + 8, y, Loc.F("Herrscher: {0}", (f.leader))); y += 11;
            Text(r.x + 8, y, Loc.F("Beziehung: {0} ({1})  -  {2}", (RelWord(rel)), ((int)rel), (DipInfo.Names[(int)st]))); y += 11;
            Text(r.x + 8, y, Loc.F("Einwohner: ~{0}", (Mathf.RoundToInt(s.pop / 5f) * 5))); y += 11;
            float def = G.DefenseStrength(s);
            float mine = G.ArmiesOf(G.PlayerId).Select(a => Game.Strength(a.units)).DefaultIfEmpty(0).Max();
            string cmp = mine <= 0 ? Loc.T("Ihr habt kein Heer im Feld") : def > mine * 1.3f ? Loc.T("stärker als euer größtes Heer") : def > mine * 0.8f ? Loc.T("etwa so stark wie euer größtes Heer") : Loc.T("schwächer als euer größtes Heer");
            Text(r.x + 8, y, Loc.F("Verteidigung: {0}", (cmp))); y += 11;
            string walls = s.buildings[(int)BType.Burg] > 0 ? Loc.T("Burg & Mauern") : s.buildings[(int)BType.Mauer] > 0 ? Loc.T("Mauern") : "unbefestigt";
            Text(r.x + 8, y, Loc.F("Befestigung: {0}", (walls)), Palette.UITextDim);
            if (Button(new Rect(r.xMax - 108, r.yMax - 20, 100, 14), Loc.T("Diplomatie"), true, null, "handshake"))
            {
                dipSel = f.id; OpenWindow(Window.Diplomacy);
            }
            Wrapped(new Rect(r.xMax - 150, r.y + 22, 140, 50), PKInput.UsingTouch ? Loc.T("Angriff: Heer wählen und auf die Stadt tippen.") : Loc.T("Angriff: Heer wählen und mit Rechtsklick auf die Stadt klicken."), Palette.UITextDim);
        }

        static string RelWord(float rel) => rel > 60 ? Loc.T("Freundschaft") : rel > 25 ? Loc.T("Wohlwollend") : rel > -25 ? Loc.T("Neutral") : rel > -60 ? Loc.T("Misstrauisch") : Loc.T("Feindselig");

        // ==================================================================
        //  Armee
        // ==================================================================
        void DrawArmy(ArmyData a)
        {
            var r = BottomPanel(400, 78);
            Panel(r);
            if (CloseButton(r)) gm.SelArmy = -1;
            var f = G.F(a.faction);
            bool mine = f.isPlayer;
            if (mine && Button(new Rect(r.xMax - 104, r.y + 3, 84, 13), Loc.T("Befehle (M)"), true, Loc.T("Ziel aus einer Liste wählen, Heer teilen, Truppen tauschen"), "army")) { armyWinSel = a.id; OpenWindow(Window.Armies); }
            Fill(new Rect(r.x + 8, r.y + 7, 8, 8), Palette.FactionColor(f.color));
            Text(r.x + 20, r.y + 7, a.name);
            if (!mine) Text(r.x + 26 + PixelFont.Width(a.name), r.y + 7, "- " + f.name, Palette.UITextDim);
            float x = r.x + 8, y = r.y + 22;
            for (int u = 0; u < Units.Count; u++)
            {
                if (a.units[u] == 0) continue;
                Icon(new Rect(x, y, 12, 12), Units.Get(u).Icon, Palette.UIText);
                string cnt = mine ? a.units[u].ToString() : "~" + Mathf.Max(1, Mathf.RoundToInt(a.units[u] / 5f) * 5);
                Text(x + 14, y + 2, cnt);
                Tip(new Rect(x, y, 40, 12), Units.Get(u).Plural);
                x += 44;
            }
            if (a.settlers > 0)
            {
                Icon(new Rect(x, y, 12, 12), "cart", Palette.UIText);
                Text(x + 14, y + 2, a.settlers + Loc.T(" Siedler"));
            }
            y += 16;
            string mission = mine ? Loc.T("__Befehl__: ") + MissionLong(a) : MissionShort(a);
            if (mine && a.mission == (int)ArmyMission.Idle)
                mission = PKInput.UsingTouch ? Loc.T("__Befehl__: keiner – tippt auf ein Ziel, dann Befehl wählen") : Loc.T("__Befehl__: keiner – Rechtsklick auf ein Ziel, dann Befehl wählen");
            Text(r.x + 8, y, mission, mine ? Palette.UIGood : Palette.UITextDim);
            Text(r.x + 8, y + 11, Loc.F("Moral {0}%   Stärke {1}", ((int)(a.morale * 100)), ((int)Game.Strength(a.units))), Palette.UITextDim);
            if (!mine) return;
            float bx = r.xMax - 8 - 4 * 62;
            float by = r.yMax - 18;
            if (Button(new Rect(bx, by, 60, 14), Loc.T("Erkunden"), a.units.Sum() > 0, Loc.T("Zieht selbstständig umher und deckt die Karte auf."), "eye")) G.ExploreNext(a);
            if (Button(new Rect(bx + 62, by, 60, 14), Loc.T("Heim"), true, Loc.T("Zurück in die nächste eigene Siedlung (Truppen gehen in die Garnison)."), "house")) G.SendHome(a);
            var other = G.ArmiesOf(G.PlayerId).FirstOrDefault(o => o.id != a.id && Mathf.Abs(o.x - a.x) < 1.5f && Mathf.Abs(o.y - a.y) < 1.5f);
            if (Button(new Rect(bx + 124, by, 60, 14), Loc.T("Vereinen"), other != null, Loc.T("Mit einem eigenen Heer in der Nähe zusammenlegen."))) G.Merge(a, other);
            if (Button(new Rect(bx + 186, by, 60, 14), Loc.T("Auflösen"), true, Loc.T("Soldaten kehren als Bürger heim (Entf)."), null, false, 1, true)) { G.Disband(a); gm.SelArmy = -1; }
            if (a.units.Sum() > 0 && Button(new Rect(bx + 124, by - 17, 122, 14), gm.RaidMode ? Loc.T("Bei Angriff: Überfall") : Loc.T("Bei Angriff: Erobern"), true,
                Loc.T("Erobern: die Stadt einnehmen. Überfall: plündern und heimkehren (am PC auch mit Shift)."), "sword", gm.RaidMode)) gm.RaidMode = !gm.RaidMode;
            if (a.settlers > 0 && a.units.Sum() == 0 && a.path.Count == 0)
            {
                int tx = Mathf.FloorToInt(a.x), ty = Mathf.FloorToInt(a.y);
                bool can = G.CanFoundAt(G.PlayerId, tx, ty, out string why);
                if (Button(new Rect(bx, by - 17, 122, 14), Loc.T("Hier gründen"), can, can ? Loc.T("Neue Siedlung an diesem Ort.") : why))
                    G.OrderMove(a, tx, ty, ArmyMission.Found, -1, out _);
            }
        }
    }
}
