using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    public partial class Game
    {
        // ==================================================================
        //  Kennzahlen
        // ==================================================================
        public int Housing(SettlementData s) => 12 + s.buildings[(int)BType.Wohnhaus] * Buildings.Get(BType.Wohnhaus).Housing
                                                + (s.buildings[(int)BType.Burg] > 0 ? 20 : 0) + (s.isCapitalOfEmperor ? 300 : 0);

        public int Jobs(SettlementData s)
        {
            int j = 0;
            for (int b = 0; b < Buildings.Count; b++) j += s.buildings[b] * Buildings.Get(b).Jobs;
            return j;
        }

        public float Storage(int fid)
        {
            float st = 0;
            foreach (var s in SettlementsOf(fid)) st += 150 + s.buildings[(int)BType.Lagerhaus] * Buildings.Get(BType.Lagerhaus).Storage;
            return Math.Max(150, st);
        }

        public float TerrainBonus(SettlementData s, TerrainNeed need)
        {
            var t = Terrain(s);
            switch (need)
            {
                case TerrainNeed.Fertile: return Clamp(0.35f + t[0] / 22f, 0.35f, 1.35f);
                case TerrainNeed.Forest: return Clamp(0.2f + t[1] / 22f, 0.2f, 1.35f);
                case TerrainNeed.Hills: return Clamp(0.4f + t[2] / 8f + t[8] * 0.15f, 0.4f, 1.5f);
                case TerrainNeed.Iron: return Clamp(0.6f + t[6] * 0.25f, 0.6f, 1.6f);
                case TerrainNeed.Gold: return Clamp(0.6f + t[7] * 0.3f, 0.6f, 1.6f);
                case TerrainNeed.Mountain: return Clamp(0.4f + t[3] / 4f, 0.4f, 1.3f);
                case TerrainNeed.Coast: return Clamp(0.5f + t[5] / 14f, 0.5f, 1.3f);
            }
            return 1f;
        }

        public bool TerrainOk(SettlementData s, TerrainNeed need, out string why)
        {
            var t = Terrain(s);
            why = null;
            switch (need)
            {
                case TerrainNeed.Fertile: if (t[0] < 3) why = Loc.T("Kein fruchtbares Land in der Nähe"); break;
                case TerrainNeed.Forest: if (t[1] < 2) why = Loc.T("Keine Bäume in der Nähe (Wald abgeholzt?)"); break;
                case TerrainNeed.Iron: if (t[6] < 1) why = Loc.T("Kein Eisenvorkommen im Gebiet"); break;
                case TerrainNeed.Gold: if (t[7] < 1) why = Loc.T("Kein Goldvorkommen im Gebiet"); break;
                case TerrainNeed.Hills: if (t[2] < 1) why = Loc.T("Keine Hügel oder Berge in der Nähe"); break;
                case TerrainNeed.Mountain: if (t[3] < 1) why = Loc.T("Kein Gebirge in der Nähe"); break;
                case TerrainNeed.Coast: if (t[4] < 1) why = Loc.T("Kein Wasser in der Nähe"); break;
            }
            return why == null;
        }

        public float HappinessTarget(SettlementData s, List<string> reasons = null)
        {
            var f = F(s.faction);
            float h = 50;
            void R(string txt, float v) { h += v; if (reasons != null && Math.Abs(v) >= 0.5f) reasons.Add((v > 0 ? "+" : "") + ((int)Math.Round(v)) + " " + txt); }

            float build = 0;
            for (int b = 0; b < Buildings.Count; b++)
            {
                var d = Buildings.Get(b);
                if (d.Happiness <= 0 || s.buildings[b] == 0) continue;
                // abnehmender Nutzen, skaliert mit Größe
                float eff = d.Happiness * (float)Math.Sqrt(s.buildings[b]) * Clamp(60f / Math.Max(30f, s.pop), 0.4f, 1.2f);
                build += eff;
            }
            R(Loc.T("Gebäude"), build);
            R(Loc.T("Steuern"), -s.taxRate * 140f + 7f);
            if (HasTech(f, Techs.Kraeuterkunde)) R(Loc.T("Kräuterkunde"), 4);
            R(Loc.T("Rationen"), (s.rations - 1) * 9f);
            int housing = Housing(s);
            if (s.pop > housing) R(Loc.T("Überfüllung"), -Math.Min(20, (s.pop - housing) * 1.5f));
            if (f != null && f.res[(int)Res.Food] <= 1) R(Loc.T("Hunger"), -25);
            if (S.day - s.conqueredDay < 120) R(Loc.T("Kürzlich erobert"), -25f * (1f - (S.day - s.conqueredDay) / 120f));
            if (S.day - s.lastBattleDay < 20) R(Loc.T("Kampfhandlungen"), -10);
            if (NearBanditCamp(s)) R(Loc.T("Räuberplage"), -6);
            if (f != null)
            {
                R(Loc.T("Rang"), f.rank * 1.5f);
                if (f.warWeariness > 10) R(Loc.T("Kriegsmüdigkeit"), -Math.Min(15, f.warWeariness / 4f));
                int maxS = Ranks.Get(f.rank).MaxSettlements;
                int over = CountSettlements(f.id) - maxS;
                if (f.isPlayer && over > 0) R(Loc.T("Verwaltungslast"), -over * 6);
                if (f.isEmperor) R(Loc.T("Kaiserliche Garde"), 12);
                if (f.isPlayer && S.story.emperorDefeated) R(Loc.T("Befreiung vom Kaiser"), 8);
                if (EdictOn(f, Edicts.Wehrpflicht)) R(Loc.T("Wehrpflicht"), -4);
                if (EdictOn(f, Edicts.FreieBauern)) R(Loc.T("Freie Bauern"), 6);
                if (Season == Season.Winter) R(Loc.T("Winter"), -3);
            }
            return Clamp(h, 0, 100);
        }

        // ==================================================================
        //  Tageslauf einer Siedlung
        // ==================================================================
        void SettlementDay(SettlementData s)
        {
            var f = F(s.faction);
            if (f == null) return;
            var res = f.res;
            var delta = Delta(f.id);
            float rankBonus = Ranks.Get(f.rank).ProductionBonus;
            float happyMod = 0.6f + s.happiness / 125f; // 0.6 .. 1.4
            float seasonFood = Calendar.FoodMod[(int)Season] * (EdictOn(f, Edicts.Erntedank) ? 1.1f : 1f);

            // Arbeitskräfte: Nahrung zuerst
            float workers = s.pop * 0.6f;
            int foodJobs = 0, otherJobs = 0;
            for (int b = 0; b < Buildings.Count; b++)
            {
                var d = Buildings.Get(b);
                if (d.FoodBuilding) foodJobs += s.buildings[b] * d.Jobs; else otherJobs += s.buildings[b] * d.Jobs;
            }
            float foodStaff = foodJobs > 0 ? Math.Min(1f, workers / foodJobs) : 1f;
            float rest = Math.Max(0, workers - foodJobs);
            float otherStaff = otherJobs > 0 ? Math.Min(1f, rest / otherJobs) : 1f;

            for (int b = 0; b < Buildings.Count; b++)
            {
                int n = s.buildings[b];
                if (n == 0) continue;
                var d = Buildings.Get(b);
                float staff = d.Jobs == 0 ? 1f : (d.FoodBuilding ? foodStaff : otherStaff);
                // Standort & Entfernung jedes einzelnen Gebäudes zählen (statt pauschalem Geländebonus)
                float terr = NeedsSite(d.Type) ? SiteSum(s, d.Type) / n : (d.Need == TerrainNeed.None ? 1f : TerrainBonus(s, d.Need));
                // Verbrauch prüfen (z.B. Schmiede braucht Eisen)
                float useFactor = 1f;
                for (int r = 0; r < ResInfo.Count; r++)
                    if (d.Use[r] > 0) useFactor = Math.Min(useFactor, res[r] / (d.Use[r] * n * staff + 0.001f));
                useFactor = Clamp(useFactor, 0, 1);
                float eff = n * staff * terr * happyMod * (1f + rankBonus) * useFactor;
                if (d.Type == BType.Bauernhof) eff *= 1f + 0.2f * Math.Min(2, s.buildings[(int)BType.Windmuehle]);
                eff *= TechProd(f, d.Type);
                if (eff <= 0) continue;
                for (int r = 0; r < ResInfo.Count; r++)
                {
                    float p = d.Prod[r] * eff * (r == (int)Res.Food && d.Type != BType.Taverne ? seasonFood : 1f);
                    if (r == (int)Res.Food && d.Type == BType.Fischerhuette && Season == Season.Winter) p *= 1.6f; // Fisch auch im Winter
                    float u = d.Use[r] * n * staff * useFactor;
                    Add(f, r, p - u, delta);
                }
                f.prestige += d.Prestige * n * 0.5f;
            }

            // Grundversorgung: Sammeln & Kleingärten
            Add(f, (int)Res.Food, s.pop * 0.05f * seasonFood, delta);
            Add(f, (int)Res.Wood, 1.0f, delta);
            Add(f, (int)Res.Stone, 0.25f, delta); // Feldsteine sammeln

            // Steuern
            float tax = s.pop * s.taxRate * 0.45f * (0.7f + s.happiness / 200f);
            if (f.isEmperor) tax *= 1.6f;
            if (EdictOn(f, Edicts.Marktrecht) && HasMarket(f.id)) tax *= 1.15f;
            if (EdictOn(f, Edicts.FreieBauern)) tax *= 0.8f;
            if (HasTech(f, Techs.Handelsgilde)) tax *= 1.08f;
            if (EdictOn(f, Edicts.Erntedank)) Add(f, (int)Res.Gold, -1f, delta);
            if (EdictOn(f, Edicts.Pilgerwege))
            {
                f.prestige += 0.5f * (s.buildings[(int)BType.Kapelle] + s.buildings[(int)BType.Kloster] + s.buildings[(int)BType.Kathedrale]);
                Add(f, (int)Res.Food, -0.5f, delta);
            }
            Add(f, (int)Res.Gold, tax, delta);

            // Verbrauch
            float ration = s.rations == 0 ? 0.75f : s.rations == 2 ? 1.3f : 1f;
            Add(f, (int)Res.Food, -s.pop * 0.1f * ration, delta);
            int soldiers = 0;
            for (int u = 0; u < Units.Count; u++)
            {
                if (s.garrison[u] == 0) continue;
                var ud = Units.Get(u);
                soldiers += s.garrison[u];
                Add(f, (int)Res.Food, -ud.UpkeepFood * s.garrison[u], delta);
                Add(f, (int)Res.Gold, -ud.UpkeepGold * s.garrison[u], delta);
            }

            // Wachstum
            int housing = Housing(s);
            bool starving = res[(int)Res.Food] <= 0.5f;
            if (starving)
            {
                s.pop = Math.Max(5, s.pop * 0.975f);
            }
            else if (s.pop < housing)
            {
                float wells = 1f + Math.Min(3, s.buildings[(int)BType.Brunnen]) * 0.15f + (HasTech(f, Techs.Kraeuterkunde) ? 0.15f : 0f);
                float g = s.pop * 0.011f * (s.happiness / 55f) * wells * (s.rations == 2 ? 1.2f : s.rations == 0 ? 0.7f : 1f);
                if (s.happiness < 25) g = -s.pop * 0.004f; // Abwanderung
                s.pop = Math.Min(housing, s.pop + g + 0.05f);
            }
            else
            {
                s.pop = Math.Max(5, s.pop - (s.pop - housing) * 0.05f);
            }
            if (!s.isCapitalOfEmperor && s.buildings[(int)BType.Brunnen] == 0 && !HasTech(f, Techs.Kraeuterkunde) && s.pop > 60 && Rng.NextDouble() < 0.002 && !Offline)
            {
                s.pop *= 0.9f;
                if (f.isPlayer) Notify(Loc.F("Krankheit in {0}! Ein Brunnen hätte geholfen.", (s.name)), 2);
            }

            // Zufriedenheit nähert sich dem Zielwert
            float target = HappinessTarget(s);
            s.happiness += (target - s.happiness) * 0.12f;

            // Unruhen
            if (s.happiness < 15 && !Offline && Rng.NextDouble() < 0.02) Revolt(s);

            if (PlacedMismatch(s)) EnsurePlaced(s);

            // Holzfällerei: Bäume werden sichtbar gefällt und wachsen langsam nach
            Forestry(s);

            // Bauschlange
            ProcessQueue(s, f);

            // KI und Verwalter bauen automatisch
            if ((s.auto && f.isPlayer) || (!f.isPlayer && (S.day + s.id) % 4 == 0)) AutoManage(s, f);
        }

        void Forestry(SettlementData s)
        {
            int r = Radius(s) + 1;
            bool changed = false;
            // Jeder Holzfäller schlägt Bäume rund um seine Hütte
            foreach (var b in s.placed)
            {
                if (b.type != (int)BType.Holzfaeller || Rng.NextDouble() > 0.12) continue;
                for (int t = 0; t < 6; t++)
                {
                    int x = b.x + Rng.Next(-2, 3), y = b.y + Rng.Next(-2, 3);
                    if (!WorldMap.In(x, y)) continue;
                    int i = WorldMap.Idx(x, y);
                    if (Map.trees[i] == 0 || (Map.ownerSettlement[i] != s.id && Map.ownerSettlement[i] != -1)) continue;
                    Map.trees[i]--; DirtyTrees.Add(i); changed = true;
                    break;
                }
            }
            // Nachwachsen (Förster pflanzen nach)
            for (int k = 0; k < 6; k++)
            {
                int x = s.x + Rng.Next(-r - 2, r + 3), y = s.y + Rng.Next(-r - 2, r + 3);
                if (k >= 3 && s.placed.Count > 0)
                {
                    var b = s.placed[Rng.Next(s.placed.Count)];
                    if (b.type == (int)BType.Holzfaeller) { x = b.x + Rng.Next(-3, 4); y = b.y + Rng.Next(-3, 4); }
                }
                if (!WorldMap.In(x, y)) continue;
                int i = WorldMap.Idx(x, y);
                if (Map.trees[i] < Map.baseTrees[i] && Rng.NextDouble() < 0.35) { Map.trees[i]++; DirtyTrees.Add(i); changed = true; }
            }
            if (changed) terrainCounts.Remove(s.id);
        }

        void Add(FactionData f, int r, float v, float[] delta)
        {
            f.res[r] += v;
            delta[r] += v;
        }

        void FactionDay(FactionData f)
        {
            var delta = Delta(f.id);
            // Armeen im Feld: Unterhalt (1,5-fach Nahrung)
            foreach (var a in S.armies)
            {
                if (a.faction != f.id) continue;
                for (int u = 0; u < Units.Count; u++)
                {
                    if (a.units[u] == 0) continue;
                    var ud = Units.Get(u);
                    Add(f, (int)Res.Food, -ud.UpkeepFood * a.units[u] * 1.5f, delta);
                    Add(f, (int)Res.Gold, -ud.UpkeepGold * a.units[u], delta);
                }
                Add(f, (int)Res.Food, -a.settlers * 0.1f, delta);
            }
            // Handelsabkommen
            foreach (var rel in f.relations)
                if (rel.trade && F(rel.other) != null && F(rel.other).alive) Add(f, (int)Res.Gold, 1.5f + f.rank * 0.5f, delta);

            // Lagergrenzen
            float cap = Storage(f.id);
            for (int r = 0; r < ResInfo.Count; r++)
            {
                if (r == (int)Res.Gold) continue;
                if (f.res[r] > cap) { delta[r] -= f.res[r] - cap; f.res[r] = cap; }
            }
            // Hunger & Schulden
            if (f.res[(int)Res.Food] < 0) f.res[(int)Res.Food] = 0;
            if (f.res[(int)Res.Gold] < 0)
            {
                // Sold nicht bezahlt -> Desertion
                f.res[(int)Res.Gold] = 0;
                if (!Offline || f.isPlayer) Desert(f, 0.03f);
                if (f.isPlayer && !Offline && S.day % 5 == 0) Notify(Loc.T("Die Kassen sind leer! Soldaten desertieren."), 2);
            }
            for (int r = 0; r < ResInfo.Count; r++) if (f.res[r] < 0) f.res[r] = 0;

            // KI-Hofhaltung: Reichtum über Bedarf wird verprasst (verhindert endloses Horten)
            if (!f.isPlayer)
            {
                float limit = 300 + TotalPop(f.id) * 3f;
                if (f.res[(int)Res.Gold] > limit) f.res[(int)Res.Gold] -= (f.res[(int)Res.Gold] - limit) * 0.05f;
            }

            // Ansehen: Grundwert pro Tag nach Größe
            f.prestige += 0.02f * (float)Math.Sqrt(TotalPop(f.id));
            f.warWeariness = Math.Max(0, f.warWeariness - (AtWarCount(f.id) == 0 ? 1.5f : -0.15f));

            if (f.isPlayer && S.day % 5 == 0) CheckRankHint(f);
        }

        void Desert(FactionData f, float share)
        {
            foreach (var s in SettlementsOf(f.id))
                for (int u = 0; u < Units.Count; u++) s.garrison[u] -= (int)Math.Ceiling(s.garrison[u] * share);
            foreach (var a in ArmiesOf(f.id))
                for (int u = 0; u < Units.Count; u++) a.units[u] -= (int)Math.Ceiling(a.units[u] * share);
        }

        void Revolt(SettlementData s)
        {
            if (s.isCapitalOfEmperor) return;
            var f = F(s.faction);
            bool conquered = s.originalFaction != s.faction && s.originalFaction >= 0;
            int soldiers = s.garrison.Sum();
            if (soldiers > s.pop * 0.15f)
            {
                s.pop *= 0.95f;
                s.happiness += 8;
                if (f.isPlayer) Notify(Loc.F("Unruhen in {0} wurden von der Garnison niedergeschlagen.", (s.name)), 2);
                return;
            }
            // Siedlung löst sich los
            var orig = F(s.originalFaction);
            FactionData target;
            if (conquered && orig != null && orig.alive) target = orig;
            else
            {
                target = new FactionData
                {
                    id = NewId(), name = Loc.T("Aufständische von ") + s.name, leader = Loc.T("Volkstribun ") + NameGen.Leader(Rng, 0).Split(' ')[1],
                    isRebel = true, isFree = true, continent = Map.cont[WorldMap.Idx(s.x, s.y)], culture = f.culture, color = 12,
                    aggression = 0.3f, honor = 0.6f, ambition = 0.2f, loyalty = 0f, res = new float[] { 60, 40, 10, 0, 20, 0 },
                };
                AddFaction(target);
                InitRelationsFor(target);
                SetStatus(target.id, f.id, DipStatus.War);
            }
            string oldName = f.name;
            TransferSettlement(s, target.id);
            s.happiness = 50;
            Notify(Loc.F("Aufstand! {0} hat sich von {1} losgesagt.", (s.name), (oldName)), f.isPlayer ? 2 : 3);
        }

        public void TransferSettlement(SettlementData s, int newFaction)
        {
            int old = s.faction;
            s.faction = newFaction;
            s.queue.Clear();
            s.auto = false;
            RebuildOccupancy();
            SettlementsDirty = true;
            var oldF = F(old);
            if (oldF != null && oldF.capital == s.id) oldF.capital = -1;
            var nf = F(newFaction);
            if (nf != null && nf.capital < 0) nf.capital = s.id;
            RecomputeTerritory();
            if (nf != null && nf.isPlayer) Reveal(s.x, s.y, Radius(s) + 5);
        }

        // ==================================================================
        //  Bauen & Ausbilden
        // ==================================================================
        public bool CanAfford(FactionData f, float[] cost, float mult = 1f)
        {
            for (int r = 0; r < ResInfo.Count; r++) if (f.res[r] + 0.001f < cost[r] * mult) return false;
            return true;
        }

        public void Pay(FactionData f, float[] cost, float mult = 1f)
        {
            for (int r = 0; r < ResInfo.Count; r++) f.res[r] -= cost[r] * mult;
        }

        public int CountWithQueue(SettlementData s, BType t)
        {
            int n = s.buildings[(int)t];
            foreach (var q in s.queue) if (q.kind == 0 && q.type == (int)t) n++;
            return n;
        }

        public float BuildCostMult(SettlementData s, BType t)
        {
            // Jedes weitere Gebäude gleicher Art wird etwas teurer
            int n = CountWithQueue(s, t);
            return 1f + n * 0.12f;
        }

        public bool CanBuild(SettlementData s, BType t, out string why, bool checkCost = true)
        {
            var f = F(s.faction); var d = Buildings.Get(t);
            why = null;
            if (f.rank < d.MinRank) { why = Loc.T("Benötigt Rang: ") + Ranks.Get(d.MinRank).Name; return false; }
            if (CountWithQueue(s, t) >= d.Max) { why = Loc.T("Maximum erreicht"); return false; }
            if (!f.isPlayer && d.Need != TerrainNeed.None && !TerrainOk(s, d.Need, out why)) return false;
            if (t == BType.Burg && s.buildings[(int)BType.Mauer] == 0) { why = Loc.T("Benötigt erst eine Mauer"); return false; }
            if (t == BType.Kathedrale && s.buildings[(int)BType.Kapelle] == 0) { why = Loc.T("Benötigt erst eine Kapelle"); return false; }
            if (s.queue.Count >= 6) { why = Loc.T("Bauschlange voll"); return false; }
            if (checkCost && !CanAfford(f, d.Cost, BuildCostMult(s, t))) { why = Loc.T("Nicht genug Ressourcen"); return false; }
            return true;
        }

        /// <summary>Gibt einen Bauauftrag. Ohne Bauplatz (x &lt; 0) wird der beste freie Platz gesucht.</summary>
        public bool QueueBuild(SettlementData s, BType t, int x = -1, int y = -1)
        {
            if (!CanBuild(s, t, out _)) return false;
            if (NeedsSite(t))
            {
                if (x < 0) { if (!FindSite(s, t, out x, out y)) return false; }
                else if (!CanPlace(s, t, x, y, out _)) return false;
            }
            else x = y = -1;
            var f = F(s.faction); var d = Buildings.Get(t);
            Pay(f, d.Cost, BuildCostMult(s, t));
            float days = d.Days * (1f + CountWithQueue(s, t) * 0.08f);
            if (x >= 0 && f.isPlayer && Dist(s.x, s.y, x, y) > TownRadius(s)) days *= 1f + (Dist(s.x, s.y, x, y) - TownRadius(s)) * 0.04f; // lange Wege
            s.queue.Add(new QueueItem { kind = 0, type = (int)t, daysLeft = days, total = days, x = x, y = y });
            if (x >= 0) { if (tileBuilding == null) RebuildOccupancy(); tileBuilding[WorldMap.Idx(x, y)] = s.id; }
            return true;
        }

        public bool CanTrain(SettlementData s, UType u, int count, out string why)
        {
            var f = F(s.faction); var d = Units.Get(u);
            why = null;
            if (f.rank < d.MinRank) { why = Loc.T("Benötigt Rang: ") + Ranks.Get(d.MinRank).Name; return false; }
            if (d.Needs != BType.None && s.buildings[(int)d.Needs] == 0) { why = Loc.T("Benötigt ") + Buildings.Get(d.Needs).Name; return false; }
            if (s.queue.Count >= 6) { why = Loc.T("Warteschlange voll"); return false; }
            if (s.pop - count < 8) { why = Loc.T("Zu wenige Bewohner"); return false; }
            if (!CanAfford(f, d.Cost, count)) { why = Loc.T("Nicht genug Ressourcen"); return false; }
            return true;
        }

        public bool QueueTrain(SettlementData s, UType u, int count)
        {
            if (!CanTrain(s, u, count, out _)) return false;
            var f = F(s.faction); var d = Units.Get(u);
            Pay(f, d.Cost, count);
            s.pop -= count; // Rekruten kommen aus der Bevölkerung
            float days = d.Days + count * 0.25f;
            s.queue.Add(new QueueItem { kind = 1, type = (int)u, count = count, daysLeft = days, total = days });
            return true;
        }

        public void CancelQueue(SettlementData s, int index)
        {
            if (index < 0 || index >= s.queue.Count) return;
            var q = s.queue[index];
            var f = F(s.faction);
            if (q.kind == 0)
            {
                var d = Buildings.Get(q.type);
                s.queue.RemoveAt(index);
                float mult = BuildCostMult(s, (BType)q.type);
                for (int r = 0; r < ResInfo.Count; r++) f.res[r] += d.Cost[r] * mult * 0.8f;
                if (q.x >= 0) RebuildOccupancy();
            }
            else
            {
                var d = Units.Get(q.type);
                s.queue.RemoveAt(index);
                for (int r = 0; r < ResInfo.Count; r++) f.res[r] += d.Cost[r] * q.count * 0.8f;
                s.pop += q.count;
            }
        }

        void ProcessQueue(SettlementData s, FactionData f)
        {
            if (s.queue.Count == 0) return;
            var q = s.queue[0];
            float speed = 1f + Math.Min(1.5f, s.pop / 150f);
            if (q.kind == 1) speed = 1f + (s.buildings[(int)BType.Kaserne] > 0 ? 0.3f : 0f);
            q.daysLeft -= speed;
            if (q.daysLeft > 0) return;
            s.queue.RemoveAt(0);
            if (q.kind == 0)
            {
                s.buildings[q.type]++;
                if (q.x >= 0) s.placed.Add(new BuildingPos(q.type, q.x, q.y));
                else EnsurePlaced(s);
                SettlementsDirty = true;
                terrainCounts.Remove(s.id);
                f.prestige += 1f;
                if (f.isPlayer) Notify(Loc.F("{0} in {1} fertiggestellt.", (Buildings.Get(q.type).Name), (s.name)), 1, false);
                OnBuildingFinished(s, (BType)q.type);
            }
            else
            {
                s.garrison[q.type] += q.count;
                if (f.isPlayer) Notify(Loc.F("{0} {1} in {2} ausgebildet.", (q.count), (Units.Get(q.type).Plural), (s.name)), 1, false);
            }
        }

        // ==================================================================
        //  Automatische Verwaltung (Verwalter des Spielers & KI)
        // ==================================================================
        void AutoManage(SettlementData s, FactionData f)
        {
            if (s.queue.Count >= 2) return;
            var t = ChooseBuilding(s, f);
            if (t != BType.None) QueueBuild(s, t);
            // Steuern anpassen
            if (!f.isPlayer)
            {
                float desired = f.isEmperor ? 0.24f : 0.08f + f.greed * 0.08f;
                if (s.happiness < 35) desired -= 0.05f;
                s.taxRate = Clamp(desired, 0.02f, 0.3f);
                s.rations = f.res[(int)Res.Food] < s.pop ? 0 : 1;
            }
        }

        public BType ChooseBuilding(SettlementData s, FactionData f)
        {
            var d = Delta(f.id);
            int housing = Housing(s);
            float food = f.res[(int)Res.Food];
            bool Try(BType b) => CanBuild(s, b, out _);

            if (d[(int)Res.Food] < s.pop * 0.03f || food < s.pop * 0.6f)
            {
                if (Try(BType.Bauernhof) && TerrainBonus(s, TerrainNeed.Fertile) >= TerrainBonus(s, TerrainNeed.Coast) * 0.9f) return BType.Bauernhof;
                if (Try(BType.Fischerhuette)) return BType.Fischerhuette;
                if (Try(BType.Bauernhof)) return BType.Bauernhof;
            }
            if (s.buildings[(int)BType.Holzfaeller] == 0 && Try(BType.Holzfaeller)) return BType.Holzfaeller;
            if (s.pop > housing - 5 && Try(BType.Wohnhaus)) return BType.Wohnhaus;
            if (f.res[(int)Res.Wood] < 60 && s.buildings[(int)BType.Holzfaeller] < 3 && Try(BType.Holzfaeller)) return BType.Holzfaeller;
            if (s.happiness < 50)
            {
                if (Try(BType.Taverne)) return BType.Taverne;
                if (Try(BType.Brunnen)) return BType.Brunnen;
                if (Try(BType.Kapelle)) return BType.Kapelle;
            }
            if (s.buildings[(int)BType.Steinbruch] == 0 && Try(BType.Steinbruch)) return BType.Steinbruch;
            if (s.buildings[(int)BType.Brunnen] == 0 && s.pop > 40 && Try(BType.Brunnen)) return BType.Brunnen;
            if (s.buildings[(int)BType.Markt] == 0 && Try(BType.Markt)) return BType.Markt;
            if (f.res[(int)Res.Wood] > Storage(f.id) * 0.85f && Try(BType.Lagerhaus)) return BType.Lagerhaus;
            if (!f.isPlayer || s.auto)
            {
                if (s.buildings[(int)BType.Kaserne] == 0 && s.pop > 50 && Try(BType.Kaserne)) return BType.Kaserne;
                if (s.buildings[(int)BType.Mine] == 0 && Try(BType.Mine)) return BType.Mine;
                if (s.buildings[(int)BType.Goldmine] == 0 && Try(BType.Goldmine)) return BType.Goldmine;
                if (s.buildings[(int)BType.Schmiede] == 0 && s.buildings[(int)BType.Mine] > 0 && Try(BType.Schmiede)) return BType.Schmiede;
                if (s.buildings[(int)BType.Windmuehle] == 0 && s.buildings[(int)BType.Bauernhof] >= 3 && Try(BType.Windmuehle)) return BType.Windmuehle;
                if (s.buildings[(int)BType.Mauer] == 0 && s.pop > 70 && Try(BType.Mauer)) return BType.Mauer;
                if (s.buildings[(int)BType.Archerie] == 0 && s.pop > 80 && Try(BType.Archerie)) return BType.Archerie;
                if (s.buildings[(int)BType.Kapelle] == 0 && s.pop > 60 && Try(BType.Kapelle)) return BType.Kapelle;
            }
            if (s.pop > housing - 10 && Try(BType.Wohnhaus)) return BType.Wohnhaus;
            if (d[(int)Res.Food] < s.pop * 0.08f && Try(BType.Bauernhof)) return BType.Bauernhof;
            if (d[(int)Res.Gold] < 2 && Try(BType.Markt)) return BType.Markt;
            return BType.None;
        }

        /// <summary>Gibt einer KI-Siedlung beim Spielstart passende Gebäude & Truppen.</summary>
        public void SeedSettlement(SettlementData s)
        {
            var f = F(s.faction);
            // Grundausstattung nach Größe
            int houses = Math.Max(1, (int)((s.pop - 12) / 8f) + 1);
            s.buildings[(int)BType.Wohnhaus] = houses;
            var t = Terrain(s);
            int foodNeed = (int)Math.Ceiling(s.pop * 0.1f / 3.2f);
            if (t[0] >= 3) s.buildings[(int)BType.Bauernhof] = Math.Min(10, foodNeed);
            else if (t[4] >= 1) s.buildings[(int)BType.Fischerhuette] = Math.Min(6, foodNeed + 1);
            else s.buildings[(int)BType.Bauernhof] = Math.Min(8, foodNeed + 1);
            if (t[1] >= 2) s.buildings[(int)BType.Holzfaeller] = 1 + (int)(s.pop / 80);
            if (t[2] >= 1) s.buildings[(int)BType.Steinbruch] = s.pop > 50 ? 1 : 0;
            if (s.pop > 45) { s.buildings[(int)BType.Markt] = 1; s.buildings[(int)BType.Taverne] = 1; s.buildings[(int)BType.Brunnen] = 1; }
            if (s.pop > 70) { s.buildings[(int)BType.Kapelle] = 1; s.buildings[(int)BType.Kaserne] = 1; s.buildings[(int)BType.Mauer] = 1; }
            if (s.pop > 100) { s.buildings[(int)BType.Archerie] = 1; s.buildings[(int)BType.Lagerhaus] = 1; if (t[6] >= 1) { s.buildings[(int)BType.Mine] = 1; s.buildings[(int)BType.Schmiede] = 1; } }
            if (s.pop > 140) { s.buildings[(int)BType.Stall] = 1; s.buildings[(int)BType.Mauer] = 2; }
            if (s.pop > 200) { s.buildings[(int)BType.Burg] = 1; s.buildings[(int)BType.Kapelle] = 2; s.buildings[(int)BType.Lagerhaus] = 2; }
            // Garnison
            float mil = s.pop * (0.06f + f.aggression * 0.06f) * (f.isEmperor ? 1.6f : 1f) * (f.isFree ? 0.6f : 1f);
            s.garrison[(int)UType.Miliz] = (int)(mil * 0.4f) + 2;
            if (s.buildings[(int)BType.Kaserne] > 0) s.garrison[(int)UType.Speer] = (int)(mil * 0.35f);
            if (s.buildings[(int)BType.Archerie] > 0) s.garrison[(int)UType.Bogen] = (int)(mil * 0.25f);
            if (s.buildings[(int)BType.Stall] > 0) s.garrison[(int)UType.Ritter] = (int)(mil * 0.12f);
            s.happiness = f.isEmperor ? 35 : 55;
            s.taxRate = f.isEmperor ? 0.24f : 0.1f;
        }

        // ==================================================================
        //  Markt
        // ==================================================================
        public bool HasMarket(int fid)
        {
            foreach (var s in SettlementsOf(fid)) if (s.buildings[(int)BType.Markt] > 0 || s.buildings[(int)BType.Hafen] > 0) return true;
            return false;
        }

        public float Price(int r, bool buy)
        {
            float p = ResInfo.BasePrice[r] * (1f + S.priceDrift[r]);
            if (Season == Season.Winter && r == (int)Res.Food) p *= 1.4f;
            bool guild = HasTech(Techs.Handelsgilde);
            return buy ? p * (guild ? 1.2f : 1.3f) : p * (guild ? 0.8f : 0.7f);
        }

        public bool Buy(int r, int amount)
        {
            var f = Player;
            float cost = Price(r, true) * amount;
            if (r == (int)Res.Gold || !HasMarket(f.id) || f.res[(int)Res.Gold] < cost) return false;
            f.res[(int)Res.Gold] -= cost; f.res[r] += amount;
            S.priceDrift[r] = Math.Min(0.8f, S.priceDrift[r] + amount * 0.0015f);
            return true;
        }

        public bool Sell(int r, int amount)
        {
            var f = Player;
            if (r == (int)Res.Gold || !HasMarket(f.id) || f.res[r] < amount) return false;
            f.res[r] -= amount; f.res[(int)Res.Gold] += Price(r, false) * amount;
            S.priceDrift[r] = Math.Max(-0.5f, S.priceDrift[r] - amount * 0.0015f);
            return true;
        }

        void MarketDrift()
        {
            for (int r = 0; r < ResInfo.Count; r++)
                S.priceDrift[r] = S.priceDrift[r] * 0.97f + ((float)Rng.NextDouble() - 0.5f) * 0.02f;
        }

        // ==================================================================
        //  Rang
        // ==================================================================
        public bool CanRankUp(FactionData f, List<string> missing)
        {
            if (f.rank >= Ranks.Count - 1) { missing?.Add(Loc.T("Höchster Rang erreicht")); return false; }
            var next = Ranks.Get(f.rank + 1);
            bool ok = true;
            void Need(bool cond, string text) { if (!cond) { ok = false; missing?.Add(text); } }
            float pop = TotalPop(f.id);
            Need(pop >= next.Pop, Loc.F("Bevölkerung {0}/{1}", ((int)pop), (next.Pop)));
            Need(CountSettlements(f.id) >= next.Settlements, Loc.F("Siedlungen {0}/{1}", (CountSettlements(f.id)), (next.Settlements)));
            Need(f.prestige >= next.Prestige, Loc.F("Ansehen {0}/{1}", ((int)f.prestige), ((int)next.Prestige)));
            Need(f.res[(int)Res.Gold] >= next.GoldCost, Loc.F("Gold {0}/{1}", ((int)f.res[(int)Res.Gold]), ((int)next.GoldCost)));
            if (next.RequiredBuilding != BType.None)
            {
                bool has = SettlementsOf(f.id).Any(s => s.buildings[(int)next.RequiredBuilding] > 0);
                Need(has, Loc.T("Gebäude: ") + Buildings.Get(next.RequiredBuilding).Name);
            }
            if (f.isPlayer && next.StoryChapter > 0) Need(S.story.chapter >= next.StoryChapter, Loc.T("Geschichte: Kapitel ") + (next.StoryChapter + 1) + " erreichen");
            return ok;
        }

        public bool RankUp()
        {
            var f = Player;
            if (!CanRankUp(f, null)) return false;
            f.res[(int)Res.Gold] -= Ranks.Get(f.rank + 1).GoldCost;
            f.rank++;
            var rd = Ranks.Get(f.rank);
            var cap = Capital(f);
            f.name = (f.rank >= 2 ? rd.Name + " " : rd.Name + " ") + (cap != null ? cap.name : "");
            if (f.rank == (int)Rank.Gemeinde) f.name = Loc.T("Gemeinde ") + (cap != null ? cap.name : "");
            Notify(Loc.F("Aufstieg! Euer Reich ist nun: {0}. Ihr seid {1}.", (rd.Name), (rd.Ruler)), 1);
            foreach (var s in SettlementsOf(f.id)) s.happiness += 10;
            OnRankUp(f.rank);
            return true;
        }

        int lastRankHint = -1;
        void CheckRankHint(FactionData f)
        {
            if (f.rank != lastRankHint && CanRankUp(f, null))
            {
                lastRankHint = f.rank;
                Notify(Loc.T("Ihr könnt im Rang aufsteigen! (Reich-Menü)"), 1, false);
            }
        }

        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    }
}
