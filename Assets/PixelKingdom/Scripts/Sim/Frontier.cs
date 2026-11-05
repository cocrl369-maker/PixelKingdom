using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>
    /// Das Land zwischen den Dörfern: Außenposten im Niemandsland (mit Grenzstreit), fahrendes Volk,
    /// Wachtürme, Reliquien im Kloster und das Wissen (Forschung).
    /// </summary>
    public partial class Game
    {
        // ==================================================================
        //  Wissen (Forschung)
        // ==================================================================
        public bool HasTech(FactionData f, int t) => f != null && f.isPlayer && S.techs.Contains(t);
        public bool HasTech(int t) => S.techs.Contains(t);

        public bool CanResearch(int t, out string why)
        {
            var d = Techs.All[t]; var p = Player;
            why = null;
            if (S.techs.Contains(t)) { why = Loc.T("Bereits erforscht"); return false; }
            if (S.researching >= 0) { why = Loc.T("Die Gelehrten sind beschäftigt: ") + Techs.All[S.researching].Name; return false; }
            if (p.rank < d.MinRank) { why = Loc.T("Benötigt Rang: ") + Ranks.Get(d.MinRank).Name; return false; }
            if (d.Needs != BType.None && CountBuildings(d.Needs) == 0) { why = Loc.T("Benötigt: ") + Buildings.Get(d.Needs).Name; return false; }
            if (p.res[(int)Res.Gold] < d.Gold || p.res[(int)Res.Wood] < d.Wood || p.res[(int)Res.Stone] < d.Stone || p.res[(int)Res.Iron] < d.Iron) { why = Loc.T("Nicht genug Ressourcen"); return false; }
            return true;
        }

        public void StartResearch(int t)
        {
            if (!CanResearch(t, out _)) return;
            var d = Techs.All[t]; var p = Player;
            p.res[(int)Res.Gold] -= d.Gold; p.res[(int)Res.Wood] -= d.Wood; p.res[(int)Res.Stone] -= d.Stone; p.res[(int)Res.Iron] -= d.Iron;
            S.researching = t; S.researchLeft = d.Days;
            Notify(Loc.F("Die Gelehrten beginnen: {0}", (d.Name)), 0);
        }

        /// <summary>Klöster und Kapellen beschleunigen die Gelehrten.</summary>
        public float ResearchSpeed => 1f + Math.Min(2, CountBuildings(BType.Kloster)) * 0.35f + Math.Min(2, CountBuildings(BType.Kapelle)) * 0.1f;

        void ResearchDay()
        {
            if (S.researching < 0) return;
            S.researchLeft -= ResearchSpeed;
            if (S.researchLeft > 0) return;
            int t = S.researching;
            S.researching = -1;
            if (!S.techs.Contains(t)) S.techs.Add(t);
            if (t == Techs.Schiffbau && !S.seafaring)
            {
                S.seafaring = true;
                Say(Aldo, "advisor", Loc.T("Unsere Schiffbauer bauen jetzt hochseetaugliche Koggen! Im Hafen könnt ihr Handels- und Kriegsschiffe bauen. Und Kapitäne erzählen von zwei großen Inseln weit im Osten – noch hat sie kein Herrscher vom Festland betreten."));
            }
            Notify(Loc.F("Erforscht: {0}! {1}", (Techs.All[t].Name), (Techs.All[t].Desc)), 1);
            Hint("first_tech", Loc.T("Euer erstes Wissen! Was erforscht ist, wirkt für immer und überall in eurem Reich. Neue Kenntnisse werden mit höherem Rang und neuen Gebäuden möglich."));
        }

        /// <summary>Ertragsbonus durch Wissen für ein Gebäude.</summary>
        public float TechProd(FactionData f, BType t)
        {
            if (f == null || !f.isPlayer) return 1f;
            switch (t)
            {
                case BType.Holzfaeller: return HasTech(Techs.Doppelaxt) ? 1.25f : 1f;
                case BType.Bauernhof: return (HasTech(Techs.SchwererPflug) ? 1.2f : 1f) * (HasTech(Techs.Fruchtfolge) ? 1.15f : 1f);
                case BType.Steinbruch: return HasTech(Techs.Steinmetzkunst) ? 1.25f : 1f;
                case BType.Mine: case BType.Goldmine: return HasTech(Techs.Tiefbau) ? 1.3f : 1f;
                case BType.Schmiede: return HasTech(Techs.Schmiedekunst) ? 1.3f : 1f;
            }
            return 1f;
        }

        /// <summary>Kampfkraft-Faktor (Kettenhemden).</summary>
        public float CombatMult(int fid) => fid == S.playerFaction && HasTech(Techs.Kettenhemden) ? 1.12f : 1f;

        // ==================================================================
        //  Täglich: Grenzland
        // ==================================================================
        void FrontierDay()
        {
            var p = Player;
            if (p == null || !p.alive) return;
            ResearchDay();

            // Reliquien im Kloster: Pilger bringen Gold und Ansehen
            int housed = Math.Min(S.relics, 2 * CountBuildings(BType.Kloster));
            if (housed > 0) { p.res[(int)Res.Gold] += housed * 1.2f; p.prestige += housed * 0.15f; }

            // Wachtürme halten Ausschau
            if (S.day % 5 == 0)
                foreach (var s in SettlementsOf(p.id))
                    foreach (var b in s.placed)
                        if (b.type == (int)BType.Wachturm) Reveal(b.x, b.y, 8);

            if (Offline) return;
            BorderDisputes();
            NomadsDay();
        }

        /// <summary>Steht ein Wachturm in der Nähe dieses Ortes?</summary>
        public bool WatchtowerNear(int x, int y, float r)
        {
            foreach (var s in SettlementsOf(PlayerId))
                foreach (var b in s.placed)
                    if (b.type == (int)BType.Wachturm && Dist(b.x, b.y, x, y) <= r) return true;
            return false;
        }

        // ------------------------------------------------------------------
        //  Grenzstreit: Außenposten im Niemandsland stören die Nachbarn
        // ------------------------------------------------------------------
        void BorderDisputes()
        {
            if (S.day < S.nextBorderDay || InPeace) return;
            foreach (var (s, b) in PlayerOutposts().ToList())
            {
                if (Rng.NextDouble() > 0.01) continue;
                SettlementData near = null; float bd = 9f;
                foreach (var o in S.settlements)
                {
                    if (o.faction == PlayerId || o.isCapitalOfEmperor) continue;
                    var of = F(o.faction);
                    if (of == null || !of.alive || of.knownToPlayer == 0 || AtWar(PlayerId, of.id)) continue;
                    float d = Dist(o.x, o.y, b.x, b.y);
                    if (d < bd) { bd = d; near = o; }
                }
                if (near == null) continue;
                S.nextBorderDay = S.day + 40 + Rng.Next(30);
                QueueEvent(new PendingEvent { id = "border", a = near.id, b = b.x * 1000 + b.y, c = s.id, v = (float)Math.Round(25 + near.pop * 0.2f) });
                return;
            }
        }

        bool BorderView(PendingEvent e, EventView v)
        {
            var o = St(e.a); var s = St(e.c);
            if (o == null || s == null || F(o.faction) == null)
            {
                v.Title = Loc.T("Grenzstreit"); v.Icon = "handshake";
                v.Text = Loc.T("Der Streit um das Grenzland hat sich von selbst erledigt.");
                v.Opt(Loc.T("Gut so"));
                return false;
            }
            var of = F(o.faction);
            var bt = BuildingAt(s, e.b / 1000, e.b % 1000);
            string what = bt.HasValue ? Buildings.Get(bt.Value).Name : Loc.T("Außenposten");
            v.Title = Loc.T("Grenzstreit"); v.Icon = "handshake";
            v.Text = Loc.F("Ein Bote aus {0} ({1}) beschwert sich: Euer {2} stehe auf Land, das seit jeher ihre Hirten und Holzfäller nutzen. 'Zahlt für die Weiderechte – oder zieht ab!'", (o.name), (of.name), (what));
            v.Opt(Loc.F("Entschädigung zahlen ({0} Gold, sie sind zufrieden)", ((int)e.v)), Player.res[(int)Res.Gold] >= e.v);
            v.Opt(Loc.T("Auf eurem Recht bestehen (Beziehung sinkt, +Ansehen)"));
            v.Opt(Loc.F("Den {0} räumen (Beziehung steigt)", (what)), bt.HasValue);
            return true;
        }

        void BorderResolve(PendingEvent e, int option)
        {
            var o = St(e.a); var s = St(e.c);
            if (o == null || s == null) return;
            var p = Player;
            int x = e.b / 1000, y = e.b % 1000;
            if (option == 0 && p.res[(int)Res.Gold] >= e.v) { p.res[(int)Res.Gold] -= e.v; ModRel(p.id, o.faction, 6); }
            else if (option == 2 && BuildingAt(s, x, y).HasValue) { RemoveBuildingAt(s, x, y); ModRel(p.id, o.faction, 10); Notify(Loc.T("Der Außenposten wurde geräumt."), 0); }
            else { ModRel(p.id, o.faction, -12); p.prestige += 3; }
        }

        public BType? BuildingAt(SettlementData s, int x, int y)
        {
            foreach (var b in s.placed) if (b.x == x && b.y == y) return (BType)b.type;
            return null;
        }

        public void RemoveBuildingAt(SettlementData s, int x, int y)
        {
            int idx = s.placed.FindIndex(b => b.x == x && b.y == y);
            if (idx < 0) return;
            var t = s.placed[idx].type;
            s.placed.RemoveAt(idx);
            if (s.buildings[t] > 0) s.buildings[t]--;
            SettlementsDirty = true;
            terrainCounts.Remove(s.id);
            RebuildOccupancy();
        }

        // ------------------------------------------------------------------
        //  Fahrendes Volk: Gaukler, Kesselflicker und Händler ohne festen Wohnsitz
        // ------------------------------------------------------------------
        void NomadsDay()
        {
            bool moved = false;
            for (int k = S.pois.Count - 1; k >= 0; k--)
            {
                var n = S.pois[k];
                if (n.type != 2) continue;
                if (S.day >= n.nextRaid) { S.pois.RemoveAt(k); moved = true; continue; }   // ziehen weiter
                if ((S.day + n.id) % 6 != 0) continue;
                for (int t = 0; t < 8; t++)
                {
                    int x = n.x + Rng.Next(-2, 3), y = n.y + Rng.Next(-2, 3);
                    if (!NomadSpot(x, y)) continue;
                    n.x = x; n.y = y; moved = true; break;
                }
            }
            if (moved) PoisVersion++;

            if (S.day < S.nextNomadDay) return;
            S.nextNomadDay = S.day + 70 + Rng.Next(50);
            if (S.pois.Count(q => q.type == 2) >= 2) return;
            var mine = SettlementsOf(PlayerId).ToList();
            if (mine.Count == 0) return;
            var home = mine[Rng.Next(mine.Count)];
            for (int t = 0; t < 60; t++)
            {
                double ang = Rng.NextDouble() * Math.PI * 2; float d = 7 + (float)Rng.NextDouble() * 9;
                int x = home.x + (int)Math.Round(Math.Cos(ang) * d), y = home.y + (int)Math.Round(Math.Sin(ang) * d);
                if (!NomadSpot(x, y)) continue;
                S.pois.Add(new PoiData { id = NewId(), type = 2, x = x, y = y, seed = Rng.Next(), nextRaid = S.day + 50 + Rng.Next(30) });
                PoisVersion++;
                Reveal(x, y, 2);
                Notify(Loc.F("Fahrendes Volk lagert unweit von {0}.", (home.name)), 1);
                Hint("nomads", Loc.T("Fahrendes Volk! Gaukler, Händler und Kesselflicker ohne festen Wohnsitz ziehen durchs Niemandsland. Schickt ein Heer oder einen Späher hin: Man kann mit ihnen handeln – oder sie einladen, sich bei euch niederzulassen. Lange bleiben sie nicht."));
                return;
            }
        }

        bool NomadSpot(int x, int y)
        {
            if (!WorldMap.In(x, y)) return false;
            int i = WorldMap.Idx(x, y);
            if (!Map.Buildable(i) || Map.deposit[i] != 0 || Map.owner[i] >= 0) return false;
            if (BuildingOwnerAt(x, y) != -1) return false;
            foreach (var p in S.pois) if (p.type != 2 && p.x == x && p.y == y) return false;
            return true;
        }

        void VisitNomads(ArmyData a, PoiData p)
        {
            a.mission = (int)ArmyMission.Idle;
            var f = F(a.faction);
            if (!f.isPlayer) return;
            if (Dist((int)a.x, (int)a.y, p.x, p.y) > 3) { Notify(Loc.T("Das fahrende Volk ist schon weitergezogen."), 0); return; }
            SettlementData near = null; float bd = float.MaxValue;
            foreach (var s in SettlementsOf(f.id)) { float d = Dist(s.x, s.y, p.x, p.y); if (d < bd) { bd = d; near = s; } }
            QueueEvent(new PendingEvent { id = "nomads", a = near != null ? near.id : -1, b = p.id });
        }

        bool NomadsView(PendingEvent e, EventView v)
        {
            var s = St(e.a);
            v.Title = Loc.T("Fahrendes Volk"); v.Icon = "cart";
            v.Text = Loc.T("Bunte Planwagen, ein Feuer, Fiedelklänge. Die Älteste mustert eure Leute: 'Wir haben Salz, Felle und Geschichten aus fernen Ländern. Und wer weiß – vielleicht sind wir des Wanderns müde.'");
            v.Opt(Loc.T("Handeln (40 Gold: Waren und Kunde von fernen Orten)"), Player.res[(int)Res.Gold] >= 40);
            v.Opt(s != null ? Loc.F("Einladen, in {0} zu bleiben (+10 Bewohner, -25 Nahrung)", (s.name)) : Loc.T("Einladen, zu bleiben"), s != null);
            v.Opt(Loc.T("Sie in Frieden weiterziehen lassen"));
            v.Opt(Loc.T("Vertreiben (Ruhe im Land, aber die Leute murren)"));
            return true;
        }

        void NomadsResolve(PendingEvent e, int option)
        {
            var p = Player; var s = St(e.a);
            var n = Poi(e.b);
            if (option == 0 && p.res[(int)Res.Gold] >= 40)
            {
                p.res[(int)Res.Gold] -= 40;
                p.res[(int)Res.Food] += 35; p.res[(int)Res.Wood] += 25; p.res[(int)Res.Iron] += 8;
                if (n != null) Reveal(n.x, n.y, 22);
                Notify(Loc.T("Ihr handelt mit dem fahrenden Volk: Salz, Felle, Eisen – und eine Karte der Umgebung."), 1);
            }
            else if (option == 1 && s != null)
            {
                s.pop += 10; p.res[(int)Res.Food] = Math.Max(0, p.res[(int)Res.Food] - 25);
                foreach (var x in SettlementsOf(p.id)) x.happiness = Math.Min(100, x.happiness + 2);
                RemoveNomads(n);
                Notify(Loc.F("Das fahrende Volk lässt sich in {0} nieder.", (s.name)), 1);
            }
            else if (option == 3)
            {
                p.prestige += 1;
                foreach (var x in SettlementsOf(p.id)) x.happiness = Math.Max(0, x.happiness - 2);
                RemoveNomads(n);
            }
        }

        void RemoveNomads(PoiData n)
        {
            if (n == null) return;
            S.pois.Remove(n);
            PoisVersion++;
        }

        public static string PoiSprite(PoiData p) => p.type == 0 ? "p_ruin" : p.type == 1 ? "p_camp" : "p_nomads";
    }
}
