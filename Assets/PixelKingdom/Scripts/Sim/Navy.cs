using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>
    /// Flotte (Handels- und Kriegsschiffe), Schiffsklassen, Seehandel, Truppentransport und Piraten;
    /// außerdem Garnisonsplätze (Kasernen) und Feldlager für Heere.
    /// </summary>
    public partial class Game
    {
        // ==================================================================
        //  Schiffe
        // ==================================================================
        public static readonly LArr ShipClassNames = new LArr("keine", "Kogge", "Holk", "Karavelle");
        static readonly int[] ShipCapacity = { 0, 30, 50, 75 };       // Soldaten je Kriegsschiff
        static readonly float[] ShipSpeed = { 1f, 1f, 1.25f, 1.5f };   // Tempo auf See
        static readonly float[] ShipTrade = { 0f, 1.6f, 2.5f, 3.5f };  // Gold/Tag je Handelsschiff (vor Handelsfaktor)
        static readonly float[] ShipCombat = { 0f, 4f, 7f, 11f };      // Kampfkraft je Kriegsschiff
        const int MerchantCarry = 10;                                  // Handelsschiffe tragen auch ein paar Leute

        /// <summary>Was die Schiffbauer wissen: 0 nichts, 1 Kogge, 2 Holk, 3 Karavelle.</summary>
        public int ShipTech => HasTech(Techs.Kompass) ? 3 : HasTech(Techs.Kielbau) ? 2 : HasTech(Techs.Schiffbau) ? 1 : (S.seafaring ? 1 : 0);

        /// <summary>Größe der besten Werft: Hafen allein = 1, mit Werft 2, mit großer Werft 3.</summary>
        public int YardLevel
        {
            get
            {
                int best = 0;
                foreach (var s in SettlementsOf(PlayerId))
                    if (s.buildings[(int)BType.Hafen] > 0) best = Math.Max(best, 1 + Math.Min(2, s.buildings[(int)BType.Werft]));
                return best;
            }
        }

        /// <summary>Die Klasse der Flotte: begrenzt durch Wissen UND Werft.</summary>
        public int ShipClass => Math.Min(ShipTech, YardLevel);
        public string ShipClassName => ShipClassNames[ShipClass];
        public int FleetSize => S.merchantShips + S.warShips;

        public int FleetMax
        {
            get
            {
                int m = 0;
                foreach (var s in SettlementsOf(PlayerId))
                    if (s.buildings[(int)BType.Hafen] > 0) m += 4 + 3 * s.buildings[(int)BType.Werft];
                return m;
            }
        }

        /// <summary>Wie viele Soldaten die Flotte auf einmal übers Meer bringt.</summary>
        public int TransportCapacity => S.warShips * ShipCapacity[ShipClass] + S.merchantShips * MerchantCarry;
        public float SeaSpeed => ShipSpeed[ShipClass];
        public float NavalPower => S.warShips * ShipCombat[ShipClass];

        /// <summary>Handelsfaktor: mehr Handelspartner und Häfen in Übersee bringen mehr ein.</summary>
        public float SeaTradeFactor
        {
            get
            {
                int partners = 0;
                foreach (var f in S.factions)
                    if (f.alive && !f.isPlayer && Rel(PlayerId, f.id).trade) partners++;
                int conts = SettlementsOf(PlayerId).Where(s => s.buildings[(int)BType.Hafen] > 0).Select(s => Map.cont[WorldMap.Idx(s.x, s.y)]).Distinct().Count();
                return 1f + 0.2f * Math.Min(4, partners) + 0.3f * Math.Max(0, conts - 1);
            }
        }

        public float SeaTradeIncome => S.merchantShips * ShipTrade[ShipClass] * SeaTradeFactor;
        public float FleetUpkeep => S.merchantShips * 0.25f + S.warShips * 0.6f;

        public float[] ShipCost(bool war)
        {
            float k = 1f + 0.35f * Math.Max(0, ShipClass - 1);
            return war ? Buildings.R(wood: 80 * k, iron: 15 * k, gold: 50 * k) : Buildings.R(wood: 60 * k, gold: 30 * k);
        }

        public bool CanBuildShip(SettlementData s, bool war, out string why)
        {
            why = null;
            var f = F(s.faction);
            if (!f.isPlayer) { why = "-"; return false; }
            if (s.buildings[(int)BType.Hafen] == 0) { why = Loc.T("Braucht einen Hafen"); return false; }
            if (ShipTech == 0) { why = Loc.T("Erforscht zuerst 'Schiffbau' (Wissen)"); return false; }
            int queued = 0;
            foreach (var x in SettlementsOf(PlayerId)) foreach (var q in x.queue) if (q.kind == 2) queued++;
            if (FleetSize + queued >= FleetMax) { why = Loc.F("Die Häfen sind voll ({0} Schiffe) – baut eine Werft", FleetMax); return false; }
            if (s.queue.Count >= 6) { why = Loc.T("Bauschlange voll"); return false; }
            if (!CanAfford(f, ShipCost(war))) { why = Loc.T("Nicht genug Ressourcen"); return false; }
            return true;
        }

        public bool QueueShip(SettlementData s, bool war)
        {
            if (!CanBuildShip(s, war, out _)) return false;
            Pay(F(s.faction), ShipCost(war));
            float days = 6 + 2 * ShipClass;
            s.queue.Add(new QueueItem { kind = 2, type = war ? 1 : 0, daysLeft = days, total = days });
            Hint("first_ship", Loc.T("Euer erstes Schiff wird gebaut! Handelsschiffe bringen jeden Tag Gold – mehr mit Handelspartnern und Häfen in Übersee. Kriegsschiffe tragen eure Heere übers Meer, schützen vor Piraten und beschießen Küstenstädte bei Belagerungen."));
            return true;
        }

        void ShipFinished(SettlementData s, QueueItem q)
        {
            if (q.type == 1) S.warShips++; else S.merchantShips++;
            Notify(Loc.F("Ein neues {0} ({1}) läuft in {2} vom Stapel.", q.type == 1 ? Loc.T("Kriegsschiff") : Loc.T("Handelsschiff"), ShipClassName, s.name), 1, false);
        }

        /// <summary>Täglich: Seehandel, Unterhalt der Flotte, Piraten.</summary>
        void NavyDay()
        {
            var p = Player;
            if (p == null || !p.alive) return;
            if (S.seafaring && !S.story.Has("hint_islands"))
                foreach (var s in S.settlements)
                    if (Map.cont[WorldMap.Idx(s.x, s.y)] >= 4 && Map.disc[WorldMap.Idx(s.x, s.y)])
                    {
                        Hint("islands", Loc.F("Land in Sicht: {0}! Die Herrscher der großen Inseln kennen weder den Kaiser noch euch. Handelt mit ihnen, verbündet euch – oder bringt ein Heer mit genug Kriegsschiffen hinüber.", WorldMap.ContinentNames[Map.cont[WorldMap.Idx(s.x, s.y)]]));
                        break;
                    }
            if (FleetSize > 0)
            {
                var delta = Delta(p.id);
                Add(p, (int)Res.Gold, SeaTradeIncome - FleetUpkeep, delta);
            }
            if (Offline || InPeace || S.merchantShips == 0 || S.day < S.nextPirateDay) return;
            // Kriegsschiffe schrecken Piraten ab
            float chance = 0.02f * S.merchantShips / (1f + S.warShips * 0.8f);
            if (Rng.NextDouble() > chance) return;
            S.nextPirateDay = S.day + 50 + Rng.Next(40);
            float pirates = 12 + S.day / 40f;
            QueueEvent(new PendingEvent { id = "pirates", v = 30 + S.merchantShips * 15, c = (int)pirates });
        }

        bool PiratesView(PendingEvent e, EventView v)
        {
            v.Title = Loc.T("Piraten!"); v.Icon = "skull";
            v.Text = Loc.F("Schwarze Segel am Horizont: Piraten haben eines eurer Handelsschiffe gestellt. Sie verlangen {0} Gold Lösegeld für Schiff und Mannschaft.", (int)e.v);
            v.Opt(Loc.F("Lösegeld zahlen ({0} Gold)", (int)e.v), Player.res[(int)Res.Gold] >= e.v);
            float odds = NavalPower / (NavalPower + e.c);
            v.Opt(S.warShips > 0 ? Loc.F("Kriegsschiffe schicken (Aussicht {0} %)", (int)(odds * 100)) : Loc.T("Kriegsschiffe schicken (ihr habt keine)"), S.warShips > 0);
            v.Opt(Loc.T("Das Schiff verloren geben"));
            return true;
        }

        void PiratesResolve(PendingEvent e, int option)
        {
            var p = Player;
            if (option == 0 && p.res[(int)Res.Gold] >= e.v) { p.res[(int)Res.Gold] -= e.v; return; }
            if (option == 1 && S.warShips > 0)
            {
                float odds = NavalPower / (NavalPower + e.c);
                if (Rng.NextDouble() < odds)
                {
                    int loot = 60 + Rng.Next(120);
                    p.res[(int)Res.Gold] += loot; p.prestige += 8;
                    Notify(Loc.F("Seesieg! Die Piraten sind versenkt, ihre Beute gehört euch: {0} Gold.", loot), 1);
                }
                else
                {
                    S.warShips = Math.Max(0, S.warShips - 1); S.merchantShips = Math.Max(0, S.merchantShips - 1);
                    Notify(Loc.T("Die Piraten waren stärker: ein Kriegsschiff und ein Handelsschiff sind verloren."), 2);
                }
                return;
            }
            S.merchantShips = Math.Max(0, S.merchantShips - 1);
            Notify(Loc.T("Ein Handelsschiff ist an die Piraten verloren."), 2);
        }

        /// <summary>Führt der Weg übers Meer? Dann braucht das Heer genug Schiffe.</summary>
        public bool CheckTransport(ArmyData a, List<int> path, out string why)
        {
            why = null;
            if (a.faction != S.playerFaction || path == null) return true;
            bool sea = false;
            foreach (int i in path) if (Map.IsSea(i)) { sea = true; break; }
            if (!sea) return true;
            int need = a.units.Sum() + a.settlers;
            int cap = TransportCapacity;
            if (cap == 0) { why = Loc.T("Ihr habt keine Schiffe – baut im Hafen Kriegs- oder Handelsschiffe."); return false; }
            if (need > cap) { why = Loc.F("Zu wenige Schiffe: Platz für {0} Mann, das Heer hat {1}. Teilt das Heer oder baut mehr Kriegsschiffe.", cap, need); return false; }
            return true;
        }

        /// <summary>Kriegsschiffe beschießen Küstenstädte: weniger Verteidigungsbonus.</summary>
        public float NavalSiegeBonus(ArmyData a, SettlementData st)
        {
            if (a.faction != S.playerFaction || S.warShips == 0) return 0f;
            bool coastal = false;
            for (int dy = -2; dy <= 2 && !coastal; dy++)
                for (int dx = -2; dx <= 2 && !coastal; dx++)
                    if (WorldMap.In(st.x + dx, st.y + dy) && Map.IsSea(WorldMap.Idx(st.x + dx, st.y + dy))) coastal = true;
            return coastal ? Math.Min(0.35f, NavalPower * 0.006f) : 0f;
        }

        // ==================================================================
        //  Garnisonen: Kasernen bieten Platz für Soldaten
        // ==================================================================
        public int GarrisonCount(SettlementData s) => s.garrison.Sum();

        public int GarrisonCap(SettlementData s)
        {
            int cap = 15 + 40 * s.buildings[(int)BType.Kaserne] + 20 * s.buildings[(int)BType.Archerie] + 15 * s.buildings[(int)BType.Stall] + 30 * s.buildings[(int)BType.Burg];
            if (s.isCapitalOfEmperor) cap += 400;
            if (s.grand > 0) cap += 900;
            var f = F(s.faction);
            if (f != null && !f.isPlayer) cap += (int)(s.pop * 0.5f);   // KI-Städte: großzügiger (Bürgerwehr)
            return cap;
        }

        public int GarrisonQueued(SettlementData s)
        {
            int n = 0;
            foreach (var q in s.queue) if (q.kind == 1) n += q.count;
            return n;
        }

        public int GarrisonFree(SettlementData s) => Math.Max(0, GarrisonCap(s) - GarrisonCount(s) - GarrisonQueued(s));

        // ==================================================================
        //  Feldlager
        // ==================================================================
        public const int CampCapacity = 60;

        /// <summary>Steht dieses Heer in einem eigenen Feldlager? Gibt die Position zurück.</summary>
        public bool InCamp(ArmyData a, out int cx, out int cy)
        {
            cx = cy = -1;
            foreach (var s in SettlementsOf(a.faction))
                foreach (var b in s.placed)
                    if (b.type == (int)BType.Feldlager && Math.Abs(b.x + 0.5f - a.x) <= 1.3f && Math.Abs(b.y + 0.5f - a.y) <= 1.3f) { cx = b.x; cy = b.y; return true; }
            return false;
        }

        /// <summary>Verteidigungsbonus im Feldlager (voll bis 60 Mann, darüber anteilig).</summary>
        public float CampBonus(ArmyData a)
        {
            if (!InCamp(a, out _, out _)) return 0f;
            int n = Math.Max(1, a.units.Sum());
            return 0.3f * Math.Min(1f, CampCapacity / (float)n);
        }

        public bool IsOwnCamp(int fid, int x, int y)
        {
            foreach (var s in SettlementsOf(fid))
                foreach (var b in s.placed)
                    if (b.type == (int)BType.Feldlager && b.x == x && b.y == y) return true;
            return false;
        }

        /// <summary>Täglich: Heere in Feldlagern und eigenen Städten erholen sich; Feldlager halten Ausschau.</summary>
        void CampsDay()
        {
            foreach (var a in S.armies)
            {
                if (a.path != null && a.pathIndex < a.path.Count) continue;
                if (InCamp(a, out _, out _)) a.morale = Math.Min(1.1f, a.morale + 0.06f);
                else if (SettlementAtTile((int)a.x, (int)a.y, a.faction)) a.morale = Math.Min(1.05f, a.morale + 0.03f);
            }
            if (S.day % 5 == 0)
                foreach (var s in SettlementsOf(PlayerId))
                    foreach (var b in s.placed)
                        if (b.type == (int)BType.Feldlager) Reveal(b.x, b.y, 7);
        }

        bool SettlementAtTile(int x, int y, int fid)
        {
            foreach (var s in SettlementsOf(fid)) if (Math.Abs(s.x - x) <= 2 && Math.Abs(s.y - y) <= 2) return true;
            return false;
        }
    }
}
