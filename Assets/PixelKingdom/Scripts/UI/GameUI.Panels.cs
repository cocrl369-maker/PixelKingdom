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
            if (poi != null) DrawPoi(poi);
        }

        void DrawPoi(PoiData p)
        {
            var r = BottomPanel(380, 84);
            Panel(r);
            if (CloseButton(r)) gm.SelPoi = -1;
            SpriteLib.DrawUI(new Rect(r.x + 6, r.y + 6, 44, 40), Game.PoiSprite(p));
            float x = r.x + 56, y = r.y + 7;
            Text(x, y, "__" + Game.PoiName(p) + "__"); y += 13;
            string desc = p.type == 2
                ? "Gaukler, Händler und Kesselflicker ohne festen Wohnsitz. Ein Heer oder Späher kann sie besuchen: handeln, sie zum Bleiben einladen – oder vertreiben. Bald ziehen sie weiter."
                : p.type == 0
                ? (p.done ? "Hier wurde schon alles durchsucht." : "Alte Mauern aus vergessener Zeit. Ein Heer oder Späher kann sie erkunden – Gold, Waffen, Relikte oder alte Karten warten. Manchmal auch eine Falle.")
                : $"Räuber plündern von hier aus die Gegend und machen den Leuten Angst. Hebt das Lager mit einem Heer aus! __Wachen__: ~{Mathf.Max(1, Mathf.RoundToInt(p.guards.Sum() / 5f) * 5)} Mann.";
            Wrapped(new Rect(x, y, r.xMax - x - 8, 40), desc, Palette.UITextDim);
            if (p.type == 0 && p.done) return;
            var nearest = G.ArmiesOf(G.PlayerId).Where(a => a.units.Sum() > 0).OrderBy(a => (a.x - p.x) * (a.x - p.x) + (a.y - p.y) * (a.y - p.y)).FirstOrDefault();
            string label = p.type == 1 ? "Nächstes Heer angreifen lassen" : "Nächstes Heer schicken";
            if (Button(new Rect(r.xMax - 168, r.yMax - 19, 160, 14), label, nearest != null, nearest != null ? "Schickt " + nearest.name : "Ihr habt kein Heer im Feld. Stellt eines in einer Siedlung auf (Reiter 'Truppen').", "army"))
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
            return Button(new Rect(panel.xMax - 16, panel.y + 3, 13, 12), "", true, "Schließen", "close");
        }

        // ==================================================================
        //  Eigene Siedlung
        // ==================================================================
        void DrawOwnSettlement(SettlementData s)
        {
            var r = BottomPanel(580, 136);
            string[] tabs = { "Übersicht", "Bauen", "Truppen", "Markt" };
            int tabCount = G.HasMarket(G.PlayerId) ? 4 : 3;
            if (SettlementTab >= tabCount) SettlementTab = 0;
            for (int i = 0; i < tabCount; i++)
            {
                var tr = new Rect(r.x + 4 + i * 66, r.y - 15, 64, 16);
                if (Button(tr, tabs[i], true, i == 1 ? "Gebäude errichten (B)" : null, null, SettlementTab == i))
                {
                    SettlementTab = i;
                    if (i == 2) G.Hint("tab_troops", "Truppen: Oben bildet ihr Soldaten aus – sie kommen aus der Bevölkerung. Rechts stellt ihr aus der Garnison ein Heer zusammen und lasst es mit 'Ausrücken' losziehen. Speere schlagen Ritter, Ritter schlagen Bogenschützen, Bogenschützen schlagen Speere.");
                    if (i == 3) G.Hint("tab_market", "Der Markt: Tauscht Waren gegen Gold und umgekehrt. Ihr braucht dafür einen Markt oder einen Hafen.");
                }
            }
            Panel(r);
            Mark("panel", new Rect(r.x, r.y - 15, r.width, r.height + 15));
            var f = G.F(s.faction);
            Text(r.x + 8, r.y + 6, s.name, Palette.UIText);
            string sub = G.Capital(f) == s ? "Hauptstadt" : "Siedlung";
            Text(r.x + 14 + PixelFont.Width(s.name), r.y + 6, "- " + sub + " - " + G.Map.BiomeName(WorldMap.Idx(s.x, s.y)), Palette.UITextDim);
            if (CloseButton(r)) gm.SelSettlement = -1;
            var content = new Rect(r.x + 6, r.y + 20, r.width - 12, r.height - 24);
            switch (SettlementTab)
            {
                case 0: TabOverview(s, content); break;
                case 1: TabBuild(s, content); break;
                case 2: TabTroops(s, content); break;
                case 3: TabMarket(content); break;
            }
        }

        void TabOverview(SettlementData s, Rect c)
        {
            float x = c.x + 2, y = c.y + 2;
            int housing = G.Housing(s);
            // Bevölkerung
            Icon(new Rect(x, y, 12, 12), "people", Palette.UIText);
            Text(x + 16, y + 2, $"Bewohner {(int)s.pop} / {housing}");
            Bar(new Rect(x + 16, y + 12, 140, 2), s.pop / housing, Palette.UIText);
            Tip(new Rect(x, y, 160, 14), "__Bewohner__\nWohnhäuser schaffen Platz. Wachstum braucht Nahrung und Zufriedenheit.");
            y += 18;
            // Zufriedenheit
            Icon(new Rect(x, y, 12, 12), s.happiness >= 40 ? "smile" : "angry", Palette.UIText);
            Text(x + 16, y + 2, $"Zufriedenheit {(int)s.happiness}");
            Bar(new Rect(x + 16, y + 12, 140, 2), s.happiness / 100f, s.happiness < 25 ? Palette.UIBad : Palette.UIText);
            var reasons = new System.Collections.Generic.List<string>();
            float target = G.HappinessTarget(s, reasons);
            Tip(new Rect(x, y, 160, 14), $"__Zufriedenheit__\n__Zielwert__: {(int)target}\n__Einflüsse__:\n" + string.Join("\n", reasons));
            y += 18;
            // Arbeit
            int jobs = G.Jobs(s);
            Text(x, y + 2, $"Arbeiter {(int)(s.pop * 0.6f)} / Stellen {jobs}", Palette.UITextDim);
            Tip(new Rect(x, y, 160, 12), "__Arbeit__\n60% der Bewohner arbeiten. Nahrungsbetriebe werden zuerst besetzt. Fehlen Arbeiter, sinkt die Produktion.");
            y += 14;
            // Steuern
            Text(x, y + 3, "Steuern");
            if (Button(new Rect(x + 50, y, 14, 13), "-", s.taxRate > 0.001f)) s.taxRate = Mathf.Max(0, s.taxRate - 0.05f);
            Text(x + 69, y + 3, $"{Mathf.RoundToInt(s.taxRate * 100)}%");
            if (Button(new Rect(x + 92, y, 14, 13), "+", s.taxRate < 0.349f)) s.taxRate = Mathf.Min(0.35f, s.taxRate + 0.05f);
            Tip(new Rect(x, y, 110, 13), "__Steuern__\nMehr Steuern = mehr Gold, aber weniger Zufriedenheit.");
            y += 16;
            Text(x, y + 3, "Rationen");
            string[] rn = { "Knapp", "Normal", "Üppig" };
            for (int i = 0; i < 3; i++)
                if (Button(new Rect(x + 50 + i * 38, y, 36, 13), rn[i], true, i == 0 ? "-25% Verbrauch, unzufriedener" : i == 2 ? "+30% Verbrauch, zufriedener, mehr Wachstum" : null, null, s.rations == i)) s.rations = i;
            y += 16;
            if (Button(new Rect(x, y, 164, 13), s.auto ? "Verwalter: AN" : "Verwalter: AUS", true,
                "Ein Verwalter baut automatisch, was die Siedlung braucht. Ideal, wenn ihr nur ab und zu spielt.", null, s.auto)) s.auto = !s.auto;

            // Bauschlange
            float qx = c.x + 178, qy = c.y + 2;
            Text(qx, qy + 2, "In Arbeit", Palette.UITextDim);
            qy += 13;
            if (s.queue.Count == 0) Text(qx, qy + 2, "Nichts. Baut etwas im Reiter 'Bauen'.", Palette.UITextDim);
            for (int i = 0; i < s.queue.Count && i < 6; i++)
            {
                var q = s.queue[i];
                string name = q.kind == 0 ? Buildings.Get(q.type).Name : q.count + " " + Units.Get(q.type).Plural;
                string icon = q.kind == 0 ? Buildings.Get(q.type).Icon : Units.Get(q.type).Icon;
                Icon(new Rect(qx, qy, 12, 12), icon, Palette.UIText);
                Text(qx + 15, qy + 1, name);
                if (i == 0) Bar(new Rect(qx + 15, qy + 10, 110, 2), 1f - q.daysLeft / Mathf.Max(0.01f, q.total), Palette.UIGood);
                else Text(qx + 130, qy + 1, "...", Palette.UITextDim);
                if (Button(new Rect(qx + 140, qy, 12, 11), "", true, "Abbrechen (80% zurück)", "close")) { G.CancelQueue(s, i); break; }
                qy += 15;
            }

            // Garnison & Aktionen
            float gx = c.x + 344, gy = c.y + 2;
            if (gx + 150 > c.xMax) return;
            Text(gx, gy + 2, "Garnison", Palette.UITextDim);
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
            if (shown == 0) Text(gx, gy + 2, "Keine Truppen", Palette.UITextDim);
            gy += 30;
            float def = G.DefenseStrength(s);
            Text(gx, gy, $"Verteidigung {(int)def}", Palette.UIText);
            Tip(new Rect(gx, gy, 120, 10), $"__Verteidigung__\n__Truppen__: Garnison + Aufgebot ({G.Levy(s)} Bürger)\n__Bonus__: +{Mathf.RoundToInt(G.DefenseBonus(s) * 100)}% durch Mauern/Gelände");
            gy += 14;
            if (Button(new Rect(gx, gy, 150, 14), "Heer aufstellen", s.garrison.Sum() > 0, "Truppen aus der Garnison ins Feld schicken", "army")) SettlementTab = 2;
            gy += 17;
            bool canSettle = G.CanSendSettlers(s, out string why);
            if (Button(new Rect(gx, gy, 150, 14), "Siedler aussenden", canSettle,
                canSettle ? "12 Bewohner gründen eine neue Siedlung. Kosten: 40 Nahrung, 50 Holz, 30 Gold." : why, "cart"))
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
            scrollCards = Scroll(c, scrollCards, totalW - c.width + 20);
            // Pfeile
            if (scrollCards > 0 && Button(new Rect(c.x, c.y + 30, 10, 24), "<")) scrollCards = Mathf.Max(0, scrollCards - 120);
            if (scrollCards < totalW - c.width + 20 && Button(new Rect(c.xMax - 10, c.y + 30, 10, 24), ">")) scrollCards += 120;
            var clip = new Rect(c.x + 12, c.y, c.width - 24, c.height);
            GUI.BeginGroup(clip);
            if (next.Count > 0)
            {
                float nx = count * (cardW + gap) - scrollCards;
                Text(nx + 2, 16, "Ab Rang", Palette.UITextDim);
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
                bool locked = !ok && why != "Nicht genug Ressourcen";
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
                string tip = $"__{d.Name}__\n{d.Desc}\n__Kosten__: {CostLong(d.Cost, mult)}\n__Bauzeit__: ~{d.Days} Tage";
                if (d.Jobs > 0) tip += $"\n__Arbeiter__: {d.Jobs}";
                if (BuildingEffects(d) != null) tip += "\n__Wirkung__: " + BuildingEffects(d);
                if (Game.NeedsSite(d.Type)) tip += Game.IsRemote(d.Type) ? "\n__Bauplatz__: frei wählbar, auch außerhalb der Stadt" : "\n__Bauplatz__: in der Stadt";
                if (!ok) tip += "\n__Gesperrt__: " + why;
                var cardScreen = new Rect(clip.x + card.x, clip.y + card.y, card.width, card.height);
                if (hov && !PKInput.UsingTouch) { tooltip = tip; tipPos = Event.current.mousePosition + new Vector2(clip.x, clip.y); }
                if (Clicked(card))
                {
                    if (!ok) PinTip(tip);
                    else if (Game.NeedsSite(d.Type)) gm.BeginPlace(s, d.Type);
                    else if (TouchConfirm("bau" + b, tip) && G.QueueBuild(s, d.Type)) Toast($"{d.Name} wird gebaut.", 0);
                }
            }
            GUI.EndGroup();
        }

        static string BuildingEffects(BuildingDef d)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int r = 0; r < ResInfo.Count; r++) if (d.Prod[r] > 0) parts.Add($"+{d.Prod[r]:0.#} {ResInfo.Name(r)}/Tag");
            for (int r = 0; r < ResInfo.Count; r++) if (d.Use[r] > 0) parts.Add($"-{d.Use[r]:0.#} {ResInfo.Name(r)}/Tag");
            if (d.Housing > 0) parts.Add($"+{d.Housing} Wohnplätze");
            if (d.Happiness > 0) parts.Add($"+{d.Happiness:0} Zufriedenheit");
            if (d.Defense > 0) parts.Add($"+{Mathf.RoundToInt(d.Defense * 100)}% Verteidigung");
            if (d.Storage > 0) parts.Add($"+{d.Storage} Lager");
            if (d.Prestige > 0) parts.Add("Ansehen");
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        bool CanAfford(float[] cost, float mult) => G.CanAfford(G.Player, cost, mult);

        static readonly string[] ResShort = { "N", "H", "S", "E", "G", "W" };
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
            return parts.Count == 0 ? "nichts" : string.Join(", ", parts);
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
                bool locked = !ok1 && why != "Nicht genug Ressourcen" && why != "Zu wenige Bewohner" && why != "Warteschlange voll";
                var rowR = new Rect(x, y, 300, 15);
                Icon(new Rect(x, y + 1, 12, 12), d.Icon, locked ? Palette.UITextDim : Palette.UIText);
                Text(x + 15, y + 3, d.Name, locked ? Palette.UITextDim : Palette.UIText);
                Text(x + 92, y + 3, "x" + s.garrison[u], Palette.UIText);
                Text(x + 120, y + 3, $"A{d.Atk} V{d.Def} L{d.Hp}", Palette.UITextDim);
                Text(x + 182, y + 3, CostText(d.Cost, 1), CanAfford(d.Cost, 1) ? Palette.UIText : Palette.UIGood);
                string tip = $"__{d.Name}__\n{d.Desc}\n__Werte__: Angriff {d.Atk}, Verteidigung {d.Def}, Leben {d.Hp}, Tempo {d.Speed}\n__Kosten__: {CostLong(d.Cost, 1)} + 1 Bewohner\n__Ausbildung__: {d.Days} Tage\n__Sold__: {d.UpkeepGold:0.00} Gold, {d.UpkeepFood:0.00} Nahrung pro Tag";
                if (d.StrongVs >= 0) tip += $"\n__Stark gegen__: {Units.Get(d.StrongVs).Plural}";
                if (d.Needs != BType.None) tip += $"\n__Benötigt__: {Buildings.Get(d.Needs).Name}";
                if (!ok1) tip += "\n__Gesperrt__: " + why;
                Tip(new Rect(x, y, 230, 14), tip);
                if (Button(new Rect(x + 236, y, 24, 13), "+1", ok1)) G.QueueTrain(s, (UType)u, 1);
                bool ok5 = G.CanTrain(s, (UType)u, 5, out _);
                if (Button(new Rect(x + 262, y, 24, 13), "+5", ok5)) G.QueueTrain(s, (UType)u, 5);
                y += 15;
            }
            if (hidden.Count > 0 && y < c.yMax - 10) Text(x, y + 3, "Weitere Truppen mit: " + string.Join(", ", hidden), Palette.UITextDim);

            // Heer aufstellen
            float ax = c.x + 300, ay = c.y + 1;
            if (ax + 160 > c.xMax) ax = c.xMax - 160;
            Fill(new Rect(ax - 6, c.y, 1, c.height), Palette.UIPanelDark);
            Text(ax, ay + 2, "Heer aufstellen", Palette.UITextDim);
            ay += 12;
            for (int u = 0; u < Units.Count; u++)
            {
                if (s.garrison[u] == 0) { armySel[u] = 0; continue; }
                armySel[u] = Mathf.Clamp(armySel[u], 0, s.garrison[u]);
                Icon(new Rect(ax, ay, 12, 12), Units.Get(u).Icon, Palette.UIText);
                if (Button(new Rect(ax + 16, ay, 12, 12), "-", armySel[u] > 0)) armySel[u] = Mathf.Max(0, armySel[u] - (PKInput.Key(PKKey.Shift) ? 10 : 1));
                Text(ax + 32, ay + 2, armySel[u] + "/" + s.garrison[u]);
                if (Button(new Rect(ax + 74, ay, 12, 12), "+", armySel[u] < s.garrison[u])) armySel[u] = Mathf.Min(s.garrison[u], armySel[u] + (PKInput.Key(PKKey.Shift) ? 10 : 1));
                if (Button(new Rect(ax + 90, ay, 26, 12), "alle")) armySel[u] = s.garrison[u];
                ay += 13;
            }
            int total = armySel.Sum();
            float by = c.yMax - 15;
            if (Button(new Rect(ax, by, 76, 14), "Ausrücken", total > 0, PKInput.UsingTouch ? "Das Heer erscheint vor der Stadt. Dann auf ein Ziel tippen." : "Das Heer erscheint vor der Stadt. Dann Rechtsklick auf ein Ziel."))
            {
                var a = G.FormArmy(s, (int[])armySel.Clone());
                System.Array.Clear(armySel, 0, armySel.Length);
                if (a != null) gm.SelectArmy(a);
            }
            if (Button(new Rect(ax + 80, by, 76, 14), "Erkunden", s.garrison[(int)UType.Spaeher] > 0, "Schickt einen Späher, der selbstständig die Umgebung erkundet.", "eye"))
            {
                var sel = new int[Units.Count]; sel[(int)UType.Spaeher] = 1;
                var a = G.FormArmy(s, sel);
                if (a != null) { G.ExploreNext(a); Toast("Ein Späher zieht los.", 0); }
            }
        }

        void TabMarket(Rect c)
        {
            if (!G.HasMarket(G.PlayerId))
            {
                Wrapped(new Rect(c.x + 4, c.y + 10, c.width - 8, 40), "Ihr habt noch keinen Markt. Baut einen Markt (ab Städtegemeinde) oder einen Hafen, um Waren zu kaufen und zu verkaufen.");
                return;
            }
            float y = c.y + 1;
            Text(c.x + 4, y + 2, "Ware", Palette.UITextDim); Text(c.x + 84, y + 2, "Lager", Palette.UITextDim);
            Text(c.x + 124, y + 2, "Kauf", Palette.UITextDim); Text(c.x + 160, y + 2, "Verkauf", Palette.UITextDim);
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
                    if (Button(new Rect(bx, y, 46, 12), "Kauf " + amt, p.res[(int)Res.Gold] >= G.Price(r, true) * amt)) G.Buy(r, amt);
                    bx += 48;
                    if (Button(new Rect(bx, y, 50, 12), "Verk. " + amt, p.res[r] >= amt)) G.Sell(r, amt);
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
            Text(r.x + 8, y, $"Herrscher: {f.leader}"); y += 11;
            Text(r.x + 8, y, $"Beziehung: {RelWord(rel)} ({(int)rel})  -  {DipInfo.Names[(int)st]}"); y += 11;
            Text(r.x + 8, y, $"Einwohner: ~{Mathf.RoundToInt(s.pop / 5f) * 5}"); y += 11;
            float def = G.DefenseStrength(s);
            float mine = G.ArmiesOf(G.PlayerId).Select(a => Game.Strength(a.units)).DefaultIfEmpty(0).Max();
            string cmp = mine <= 0 ? "Ihr habt kein Heer im Feld" : def > mine * 1.3f ? "stärker als euer größtes Heer" : def > mine * 0.8f ? "etwa so stark wie euer größtes Heer" : "schwächer als euer größtes Heer";
            Text(r.x + 8, y, $"Verteidigung: {cmp}"); y += 11;
            string walls = s.buildings[(int)BType.Burg] > 0 ? "Burg & Mauern" : s.buildings[(int)BType.Mauer] > 0 ? "Mauern" : "unbefestigt";
            Text(r.x + 8, y, $"Befestigung: {walls}", Palette.UITextDim);
            if (Button(new Rect(r.xMax - 108, r.yMax - 20, 100, 14), "Diplomatie", true, null, "handshake"))
            {
                dipSel = f.id; OpenWindow(Window.Diplomacy);
            }
            Wrapped(new Rect(r.xMax - 150, r.y + 22, 140, 50), PKInput.UsingTouch ? "Angriff: Heer wählen und auf die Stadt tippen." : "Angriff: Heer wählen und mit Rechtsklick auf die Stadt klicken.", Palette.UITextDim);
        }

        static string RelWord(float rel) => rel > 60 ? "Freundschaft" : rel > 25 ? "Wohlwollend" : rel > -25 ? "Neutral" : rel > -60 ? "Misstrauisch" : "Feindselig";

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
            if (mine && Button(new Rect(r.xMax - 104, r.y + 3, 84, 13), "Befehle (M)", true, "Ziel aus einer Liste wählen, Heer teilen, Truppen tauschen", "army")) { armyWinSel = a.id; OpenWindow(Window.Armies); }
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
                Text(x + 14, y + 2, a.settlers + " Siedler");
            }
            y += 16;
            string mission = mine ? "__Befehl__: " + MissionLong(a) : MissionShort(a);
            if (mine && a.mission == (int)ArmyMission.Idle)
                mission = PKInput.UsingTouch ? "__Befehl__: keiner – tippt auf ein Ziel oder 'Befehle'" : "__Befehl__: keiner – Rechtsklick auf ein Ziel oder 'Befehle'";
            Text(r.x + 8, y, mission, mine ? Palette.UIGood : Palette.UITextDim);
            Text(r.x + 8, y + 11, $"Moral {(int)(a.morale * 100)}%   Stärke {(int)Game.Strength(a.units)}", Palette.UITextDim);
            if (!mine) return;
            float bx = r.xMax - 8 - 4 * 62;
            float by = r.yMax - 18;
            if (Button(new Rect(bx, by, 60, 14), "Erkunden", a.units.Sum() > 0, "Zieht selbstständig umher und deckt die Karte auf.", "eye")) G.ExploreNext(a);
            if (Button(new Rect(bx + 62, by, 60, 14), "Heim", true, "Zurück in die nächste eigene Siedlung (Truppen gehen in die Garnison).", "house")) G.SendHome(a);
            var other = G.ArmiesOf(G.PlayerId).FirstOrDefault(o => o.id != a.id && Mathf.Abs(o.x - a.x) < 1.5f && Mathf.Abs(o.y - a.y) < 1.5f);
            if (Button(new Rect(bx + 124, by, 60, 14), "Vereinen", other != null, "Mit einem eigenen Heer in der Nähe zusammenlegen.")) G.Merge(a, other);
            if (Button(new Rect(bx + 186, by, 60, 14), "Auflösen", true, "Soldaten kehren als Bürger heim (Entf).", null, false, 1, true)) { G.Disband(a); gm.SelArmy = -1; }
            if (a.units.Sum() > 0 && Button(new Rect(bx + 124, by - 17, 122, 14), gm.RaidMode ? "Bei Angriff: Überfall" : "Bei Angriff: Erobern", true,
                "Erobern: die Stadt einnehmen. Überfall: plündern und heimkehren (am PC auch mit Shift).", "sword", gm.RaidMode)) gm.RaidMode = !gm.RaidMode;
            if (a.settlers > 0 && a.units.Sum() == 0 && a.path.Count == 0)
            {
                int tx = Mathf.FloorToInt(a.x), ty = Mathf.FloorToInt(a.y);
                bool can = G.CanFoundAt(G.PlayerId, tx, ty, out string why);
                if (Button(new Rect(bx, by - 17, 122, 14), "Hier gründen", can, can ? "Neue Siedlung an diesem Ort." : why))
                    G.OrderMove(a, tx, ty, ArmyMission.Found, -1, out _);
            }
        }
    }
}
