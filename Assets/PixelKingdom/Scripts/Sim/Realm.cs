using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>Ruinen, Räuberlager, Erlasse und Ritterturniere.</summary>
    public partial class Game
    {
        public int PoisVersion;
        public IReadOnlyList<PoiData> Pois => S.pois;
        public PoiData Poi(int id) { foreach (var p in S.pois) if (p.id == id) return p; return null; }

        public static string PoiName(PoiData p) => p.type == 0 ? (p.done ? "Erkundete Ruine" : "Alte Ruine") : p.type == 1 ? "Räuberlager" : "Fahrendes Volk";

        public PoiData PoiNear(float x, float y, float r)
        {
            PoiData best = null; float bd = r * r;
            foreach (var p in S.pois)
            {
                if (p.type == 1 && p.done) continue;
                float d = (p.x + 0.5f - x) * (p.x + 0.5f - x) + (p.y + 0.5f - y) * (p.y + 0.5f - y);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------
        //  Verteilung auf der Karte (beim Spielstart)
        // ------------------------------------------------------------------
        public void PlacePois()
        {
            var r = new Random(S.seed * 3 + 77);
            var cap = Capital(Player);
            bool Ok(int x, int y, float minSet)
            {
                if (!WorldMap.In(x, y)) return false;
                int i = WorldMap.Idx(x, y);
                if (!Map.Buildable(i) || Map.deposit[i] != 0) return false;
                foreach (var s in S.settlements) if (Dist(s.x, s.y, x, y) < minSet) return false;
                foreach (var p in S.pois) if (Dist(p.x, p.y, x, y) < 9) return false;
                return true;
            }
            int ruins = 0, camps = 0;
            for (int t = 0; t < 6000 && (ruins < 34 || camps < 22); t++)
            {
                int x = r.Next(6, WorldMap.W - 6), y = r.Next(6, WorldMap.H - 6);
                bool camp = camps < 22 && r.NextDouble() < 0.45;
                if (!camp && ruins >= 34) continue;
                if (!Ok(x, y, camp ? 9 : 7)) continue;
                var b = Map.biome[WorldMap.Idx(x, y)];
                if (camp && !(b == Biome.Forest || b == Biome.Pine || b == Biome.Hills || b == Biome.Steppe) && r.NextDouble() < 0.7) continue;
                var p = new PoiData { id = NewId(), type = camp ? 1 : 0, x = x, y = y, seed = r.Next() };
                if (camp)
                {
                    float d = cap != null ? Dist(cap.x, cap.y, x, y) : 100;
                    float k = Math.Min(2.5f, 0.6f + d / 70f); // nahe Lager sind schwächer
                    p.guards[(int)UType.Miliz] = (int)(8 * k) + r.Next(4);
                    p.guards[(int)UType.Speer] = (int)(3 * k) + r.Next(3);
                    p.guards[(int)UType.Bogen] = (int)(2 * k) + r.Next(3);
                    p.nextRaid = 160 + r.Next(80);
                    camps++;
                }
                else ruins++;
                S.pois.Add(p);
                Map.trees[WorldMap.Idx(x, y)] = 0; Map.baseTrees[WorldMap.Idx(x, y)] = 0;
            }
            PoisVersion++;
        }

        // ------------------------------------------------------------------
        //  Ein Heer erreicht eine Ruine oder ein Lager
        // ------------------------------------------------------------------
        void VisitPoi(ArmyData a, PoiData p)
        {
            var f = F(a.faction);
            a.mission = (int)ArmyMission.Idle;
            if (p.type == 2) { VisitNomads(a, p); return; }
            if (p.type == 0)
            {
                if (p.done) { if (f.isPlayer) Notify("Die Ruine wurde bereits durchsucht.", 0); return; }
                p.done = true; PoisVersion++;
                if (!f.isPlayer) return;
                var rr = new Random(p.seed);
                double roll = rr.NextDouble();
                string msg;
                if (roll < 0.38) { int g = 80 + rr.Next(140); f.res[(int)Res.Gold] += g; msg = $"In den Ruinen lag ein vergessener Schatz: {g} Gold!"; }
                else if (roll < 0.58) { f.res[(int)Res.Iron] += 25; f.res[(int)Res.Weapons] += 15; msg = "Eine alte Waffenkammer: 25 Eisen und 15 Waffen."; }
                else if (roll < 0.76) { f.prestige += 20; foreach (var s in SettlementsOf(f.id)) s.happiness = Math.Min(100, s.happiness + 6); S.relics++; msg = "Ein heiliges Relikt! Das Volk pilgert herbei (+20 Ansehen, +Zufriedenheit). In einem Kloster aufbewahrt, bringt es dauerhaft Pilger und Gold."; }
                else if (roll < 0.9) { Reveal(p.x, p.y, 28); msg = "Eine alte Landkarte zeigt die Umgebung."; }
                else
                {
                    for (int u = 0; u < Units.Count; u++) a.units[u] -= a.units[u] / 5;
                    msg = "Eine Falle! Einsturz in den Gewölben – einige Männer sind verloren.";
                }
                Notify("Ruine: " + msg, 1);
                Say(Aldo, "advisor", msg);
                return;
            }
            // Räuberlager
            if (p.done) return;
            var rep = new BattleReport { Title = "Angriff auf ein Räuberlager", Place = "Räuberlager", Attacker = f.name, Defender = "Räuber", PlayerAttacker = f.isPlayer };
            Array.Copy(a.units, rep.AttStart, Units.Count);
            Array.Copy(p.guards, rep.DefStart, Units.Count);
            var att = ToF(a.units); var def = ToF(p.guards);
            bool won = Fight(att, def, 0.15f, a.morale * CombatMult(a.faction), 1f);
            var la = Round(att); var ld = Round(def);
            for (int u = 0; u < Units.Count; u++) { rep.AttLoss[u] = a.units[u] - la[u]; a.units[u] = la[u]; rep.DefLoss[u] = p.guards[u] - ld[u]; p.guards[u] = ld[u]; }
            rep.AttackerWon = won;
            if (won)
            {
                p.done = true; PoisVersion++;
                int loot = 100 + new Random(p.seed).Next(160);
                f.res[(int)Res.Gold] += loot; f.res[(int)Res.Weapons] += 10; f.prestige += 15;
                rep.Outcome = $"Das Lager ist ausgehoben! Beute: {loot} Gold und 10 Waffen.";
                foreach (var s in S.settlements) if (Dist(s.x, s.y, p.x, p.y) < 14) s.happiness = Math.Min(100, s.happiness + 8);
                if (f.isPlayer) S.battlesWon++;
            }
            else
            {
                rep.Outcome = "Die Räuber haben den Angriff abgewehrt.";
                if (f.isPlayer) S.battlesLost++;
            }
            if (a.units.Sum() > 0) SendHome(a); else RemoveArmy(a);
            if (f.isPlayer) { if (!Offline) Reports.Enqueue(rep); Notify("Räuberlager: " + rep.Outcome, won ? 1 : 2); }
        }

        /// <summary>Räuberlager plündern die Umgebung und werden langsam stärker.</summary>
        void PoisDay()
        {
            foreach (var p in S.pois)
            {
                if (p.type != 1 || p.done) continue;
                if (S.day % 10 == 0 && p.guards[(int)UType.Miliz] < 30) p.guards[(int)UType.Miliz]++;
                if (S.day < p.nextRaid || Offline) continue;
                p.nextRaid = S.day + 45 + Rng.Next(30);
                SettlementData target = null; float bd = 13f;
                foreach (var s in SettlementsOf(PlayerId)) { float d = Dist(s.x, s.y, p.x, p.y); if (d < bd) { bd = d; target = s; } }
                if (target == null || InPeace) continue;
                if (WatchtowerNear(p.x, p.y, 12) && Rng.NextDouble() < 0.5)
                {
                    Notify($"Die Wächter auf dem Wachturm sahen die Räuber kommen – {target.name} war gewarnt, der Überfall blieb aus.", 1);
                    continue;
                }
                QueueEvent(new PendingEvent { id = "camp_raid", a = target.id, b = p.id, v = (float)Math.Round(30 + Player.res[(int)Res.Gold] * 0.1f) });
            }
        }

        /// <summary>Räuberplage: Siedlungen in der Nähe eines Lagers sind unzufriedener.</summary>
        public bool NearBanditCamp(SettlementData s)
        {
            foreach (var p in S.pois) if (p.type == 1 && !p.done && Dist(p.x, p.y, s.x, s.y) < 10) return true;
            return false;
        }

        // ------------------------------------------------------------------
        //  Erlasse
        // ------------------------------------------------------------------
        public bool EdictOn(FactionData f, int e) => f != null && f.isPlayer && S.edicts.Contains(e);
        public int MaxEdicts => Player.rank;

        public bool CanToggleEdict(int e, out string why)
        {
            why = null;
            if (S.edicts.Contains(e)) return true;
            if (Player.rank < Edicts.MinRank[e]) { why = "Benötigt Rang: " + Ranks.Get(Edicts.MinRank[e]).Name; return false; }
            if (S.edicts.Count >= MaxEdicts) { why = MaxEdicts == 0 ? "Erst ab Städtegemeinde" : $"Euer Rang erlaubt nur {MaxEdicts} Erlass(e)"; return false; }
            if (Player.res[(int)Res.Gold] < Edicts.ChangeCost) { why = $"Verkünden kostet {(int)Edicts.ChangeCost} Gold"; return false; }
            return true;
        }

        public void ToggleEdict(int e)
        {
            if (S.edicts.Contains(e)) { S.edicts.Remove(e); Notify($"Erlass aufgehoben: {Edicts.Names[e]}", 0); return; }
            if (!CanToggleEdict(e, out _)) return;
            Player.res[(int)Res.Gold] -= Edicts.ChangeCost;
            S.edicts.Add(e);
            Notify($"Erlass verkündet: {Edicts.Names[e]}", 1);
        }

        // ------------------------------------------------------------------
        //  Ritterturnier
        // ------------------------------------------------------------------
        public const float TournamentCost = 150f;
        public int TournamentCooldown => Math.Max(0, S.lastTournamentDay + 60 - S.day);

        public bool CanTournament(out string why)
        {
            why = null;
            if (CountBuildings(BType.Turnierplatz) == 0) { why = "Ihr braucht einen Turnierplatz"; return false; }
            if (TournamentCooldown > 0) { why = $"Das nächste Turnier ist in {TournamentCooldown} Tagen möglich"; return false; }
            if (Player.res[(int)Res.Gold] < TournamentCost) { why = $"Ein Turnier kostet {(int)TournamentCost} Gold"; return false; }
            return true;
        }

        public void HoldTournament()
        {
            if (!CanTournament(out _)) return;
            var p = Player;
            p.res[(int)Res.Gold] -= TournamentCost;
            S.lastTournamentDay = S.day;
            p.prestige += 25;
            foreach (var s in SettlementsOf(p.id)) s.happiness = Math.Min(100, s.happiness + 10);
            var guests = S.factions.Where(f => f.alive && !f.isPlayer && f.knownToPlayer != 0 && !AtWar(p.id, f.id)).ToList();
            foreach (var f in guests) ModRel(p.id, f.id, 8);
            var cap = Capital(p);
            if (cap != null) cap.garrison[(int)UType.Ritter] += 2;
            string winner = guests.Count > 0 ? "ein Ritter aus " + guests[Rng.Next(guests.Count)].name : "einer eurer eigenen Ritter";
            Notify($"Ritterturnier! Sieger: {winner}. +25 Ansehen, {guests.Count} Gäste sind euch nun gewogener.", 1);
            Say(Aldo, "advisor", $"Was für ein Turnier! Lanzen splitterten, das Volk jubelte – und am Ende siegte {winner}. Zwei fahrende Ritter bleiben in euren Diensten.");
        }
    }
}
