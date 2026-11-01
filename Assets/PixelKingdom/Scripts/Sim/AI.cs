using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>KI der Fürsten, freien Dörfer und des Kaisers. Jede Fraktion hat eigene Interessen.</summary>
    public partial class Game
    {
        void AIEconomyOnly(FactionData f)
        {
            Recruit(f);
        }

        void AIThink(FactionData f)
        {
            if (CountSettlements(f.id) == 0) return;
            Recruit(f);

            // Interessen: begehrte Siedlung wählen
            if ((S.day + f.id) % 30 == 0 && !f.isFree) PickClaim(f);

            // Expansion
            if (!f.isFree && (S.day + f.id * 7) % 40 == 0) TryExpand(f);

            // Krieg & Frieden
            if ((S.day + f.id) % 9 == 0) ConsiderWar(f);
            if ((S.day + f.id) % 6 == 0) ConsiderPeace(f);
            if ((S.day + f.id) % 4 == 0) Campaign(f);

            // Untätige Armeen heimschicken
            foreach (var a in ArmiesOf(f.id).ToList())
                if (a.mission == (int)ArmyMission.Idle) SendHome(a);

            // Angebote an den Spieler
            if (f.knownToPlayer != 0 && (S.day + f.id * 3) % 25 == 0) ProposeToPlayer(f);
        }

        /// <summary>Ein junges Reich mit nur einer Siedlung kann nicht ausgelöscht werden.</summary>
        public bool PlayerProtected => Player.rank < 2 && CountSettlements(PlayerId) <= 1;

        void Recruit(FactionData f)
        {
            bool war = AtWarCount(f.id) > 0;
            foreach (var s in SettlementsOf(f.id))
            {
                if (s.queue.Count >= 3) continue;
                float desired = s.pop * 0.8f * (0.5f + f.aggression) * (f.isEmperor ? 1.5f : 1f) * (war ? 1.4f : 1f) * (f.isFree ? 0.4f : 1f);
                if (Strength(s.garrison) >= desired) continue;
                if (f.res[(int)Res.Food] < s.pop * 0.5f || f.res[(int)Res.Gold] < 20) continue;
                // bestes verfügbares
                UType best = UType.Miliz;
                foreach (var u in new[] { UType.Ritter, UType.Speer, UType.Bogen })
                {
                    var d = Units.Get(u);
                    if (d.Needs != BType.None && s.buildings[(int)d.Needs] == 0) continue;
                    if (f.rank < d.MinRank) continue;
                    if (!CanAfford(f, d.Cost, 3)) continue;
                    best = u;
                    if (Rng.NextDouble() < 0.5) break;
                }
                int count = Math.Max(1, Math.Min(6, (int)(s.pop / 25)));
                while (count > 0 && !CanTrain(s, best, count, out _)) count--;
                if (count > 0) QueueTrain(s, best, count);
            }
        }

        void PickClaim(FactionData f)
        {
            SettlementData best = null; float bestScore = float.MinValue;
            foreach (var mine in SettlementsOf(f.id))
                foreach (var o in S.settlements)
                {
                    if (o.faction == f.id) continue;
                    var of = F(o.faction);
                    if (of.isEmperor && f.loyalty > 0.4f) continue;
                    float d = Dist(mine.x, mine.y, o.x, o.y);
                    if (d > 32) continue;
                    float score = o.pop * 0.5f - d * 2f - DefenseStrength(o) * 0.1f + (of.isFree ? 10 : 0) - RelValue(f.id, o.faction) * 0.3f;
                    if (score > bestScore) { bestScore = score; best = o; }
                }
            int old = f.claim;
            f.claim = best != null && f.ambition > 0.25f ? best.id : -1;
            if (f.claim != old && best != null && best.faction == PlayerId && f.knownToPlayer != 0)
                QueueEvent(new PendingEvent { id = "claim", a = f.id, b = best.id });
        }

        void TryExpand(FactionData f)
        {
            if (CountSettlements(f.id) >= 2 + (int)(f.ambition * 4) + (f.isEmperor ? 4 : 0)) return;
            if (S.armies.Any(a => a.faction == f.id && a.settlers > 0)) return;
            var src = SettlementsOf(f.id).OrderByDescending(s => s.pop).FirstOrDefault();
            if (src == null || src.pop < 60) return;
            for (int t = 0; t < 30; t++)
            {
                double ang = Rng.NextDouble() * Math.PI * 2; int r = 9 + Rng.Next(11);
                int tx = src.x + (int)(Math.Cos(ang) * r), ty = src.y + (int)(Math.Sin(ang) * r);
                if (!CanFoundAt(f.id, tx, ty, out _)) continue;
                if (Map.cont[WorldMap.Idx(tx, ty)] != Map.cont[WorldMap.Idx(src.x, src.y)]) continue;
                f.res[(int)Res.Food] += 20; // KI-Vorteil beim Siedeln
                if (CanSendSettlersAI(src, f)) { SendSettlers(src, tx, ty, out _); return; }
                return;
            }
        }

        bool CanSendSettlersAI(SettlementData s, FactionData f) => s.pop >= 34 && CanAfford(f, SettlerCost);

        void ConsiderWar(FactionData f)
        {
            if (f.isFree || AtWarCount(f.id) >= 2 || S.day < 40) return;
            var emp = Emperor;
            float myStr = MilitaryStrength(f.id);
            FactionData target = null; float bestScore = 0;
            foreach (var o in S.factions)
            {
                if (!o.alive || o.id == f.id || !IsNeighbour(f.id, o.id)) continue;
                var st = Status(f.id, o.id);
                if (st == DipStatus.War || st == DipStatus.Alliance || st == DipStatus.Truce) continue;
                if (st == DipStatus.Pact && f.honor > 0.35f) continue;
                if (o.isPlayer)
                {
                    if (InPeace) continue;
                    // Schonfrist für neue Spieler
                    if (o.rank < 1 || S.day < 80) continue;
                    if (f.isEmperor && !S.story.Has("emperor_war")) continue;
                }
                if (f.isEmperor && o.loyalty > 0.4f && !o.isPlayer) continue;
                if (o.isEmperor && f.loyalty > 0.3f) continue;
                if (o.isEmperor && f.loyalty <= 0.3f && Emperor != null && MilitaryStrength(o.id) > myStr * 0.8f) continue;
                float rel = RelValue(f.id, o.id);
                var claim = St(f.claim);
                bool claimed = claim != null && claim.faction == o.id;
                if (rel > -20 && !claimed) continue;
                float theirStr = MilitaryStrength(o.id) + AlliesOf(o.id).Sum(x => MilitaryStrength(x.id) * 0.5f) + 1;
                float ratio = myStr / theirStr;
                float need = 1.3f + (1f - f.aggression) * 0.8f;
                if (ratio < need) continue;
                float score = ratio - rel * 0.02f + (claimed ? 1 : 0);
                if (score > bestScore) { bestScore = score; target = o; }
            }
            if (target == null) return;
            if (Rng.NextDouble() > 0.25 + f.aggression * 0.45) return;
            DeclareWar(f.id, target.id);
        }

        void ConsiderPeace(FactionData f)
        {
            foreach (var e in EnemiesOf(f.id).ToList())
            {
                if (f.isEmperor && e.isPlayer && S.story.Has("emperor_war")) continue;
                if (e.isEmperor && f.isPlayer) continue;
                var rel = Rel(f.id, e.id);
                if (S.day - rel.since < 25) continue;
                float tired = 25 + f.aggression * 30;
                float ratio = (MilitaryStrength(f.id) + 1) / (MilitaryStrength(e.id) + 1);
                if (f.warWeariness < tired && ratio > 0.6f) continue;
                if (e.isPlayer)
                {
                    QueueEvent(new PendingEvent { id = "peace_offer", a = f.id });
                }
                else if (e.warWeariness > 15 || ratio < 0.5f)
                {
                    MakePeace(f.id, e.id);
                }
            }
        }

        void Campaign(FactionData f)
        {
            if (AtWarCount(f.id) == 0) return;
            int maxArmies = f.isEmperor ? 4 : 2;
            if (ArmiesOf(f.id).Count(a => a.mission == (int)ArmyMission.Attack || a.mission == (int)ArmyMission.Raid) >= maxArmies) return;
            // stärkste Garnison
            SettlementData src = null; float best = 0;
            foreach (var s in SettlementsOf(f.id))
            {
                float st = Strength(s.garrison);
                if (st > best) { best = st; src = s; }
            }
            if (src == null || best < 30) return;
            float avail = best * 0.6f;
            // Ziel
            SettlementData target = null; float bestScore = float.MinValue;
            foreach (var o in S.settlements)
            {
                if (o.faction == f.id || !AtWar(f.id, o.faction)) continue;
                if (o.isCapitalOfEmperor) continue;
                if (o.faction == PlayerId && InPeace) continue; // Friedenszeit
                if (S.day - o.lastBattleDay < 25) continue; // Atempause nach einem Kampf
                float d = Dist(src.x, src.y, o.x, o.y);
                if (d > 68) continue;
                if (Map.cont[WorldMap.Idx(o.x, o.y)] != Map.cont[WorldMap.Idx(src.x, src.y)]) continue;
                float def = DefenseStrength(o);
                if (def > avail * 0.9f && !(f.aggression > 0.7f && def < avail * 1.6f)) continue;
                float score = -d - def * 0.05f + (o.id == f.claim ? 25 : 0) + (F(o.faction).isPlayer ? 5 : 0);
                if (score > bestScore) { bestScore = score; target = o; }
            }
            if (target == null) return;
            var units = new int[Units.Count];
            for (int u = 0; u < Units.Count; u++) units[u] = (int)(src.garrison[u] * 0.6f);
            units[(int)UType.Spaeher] = 0;
            var a = FormArmy(src, units);
            if (a == null) return;
            a.name = (f.isEmperor ? "Kaiserliches Heer" : "Heer von " + f.name);
            bool raid = DefenseStrength(target) > Strength(a.units) * 0.8f;
            // Anfängerschutz: die letzte Siedlung eines kleinen Spielerreichs wird nur geplündert
            if (F(target.faction).isPlayer && PlayerProtected) raid = true;
            if (!OrderMove(a, target.x, target.y, raid ? ArmyMission.Raid : ArmyMission.Attack, target.id, out _))
            {
                MergeIntoGarrison(a, src);
                return;
            }
            if (F(target.faction).isPlayer)
                Notify($"Späher melden: {a.name} ({a.units.Sum()} Mann) marschiert auf {target.name}!", 3);
        }

        void ProposeToPlayer(FactionData f)
        {
            var p = Player;
            var rel = Rel(f.id, p.id);
            var st = (DipStatus)rel.status;
            double r = Rng.NextDouble();
            if (st == DipStatus.War) return;
            if (rel.value > 45 && st != DipStatus.Alliance && r < 0.35)
                QueueEvent(new PendingEvent { id = "alliance_offer", a = f.id });
            else if (rel.value > 10 && !rel.trade && f.greed > 0.4f && r < 0.4 && HasMarket(p.id))
                QueueEvent(new PendingEvent { id = "trade_offer", a = f.id });
            else if (!InPeace && rel.value < -10 && f.aggression > 0.55f && !f.isFree && MilitaryStrength(f.id) > MilitaryStrength(p.id) * 2.2f && p.rank >= 1 && r < 0.4)
                QueueEvent(new PendingEvent { id = "tribute_demand", a = f.id, v = (float)Math.Round(30 + TotalPop(p.id) * 0.3f) });
            else if (rel.value > 25)
            {
                var enemy = EnemiesOf(f.id).FirstOrDefault(e => !e.isPlayer && !AtWar(p.id, e.id) && e.knownToPlayer != 0);
                if (enemy != null && r < 0.5) QueueEvent(new PendingEvent { id = "help_request", a = f.id, b = enemy.id });
                else if (st == DipStatus.Alliance && r < 0.15 && !f.isFree) QueueEvent(new PendingEvent { id = "marriage", a = f.id });
            }
        }
    }
}
