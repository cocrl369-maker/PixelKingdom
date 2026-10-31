using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    public class BattleReport
    {
        public string Title, Place, Attacker, Defender, Outcome;
        public int[] AttStart = new int[Units.Count], AttLoss = new int[Units.Count];
        public int[] DefStart = new int[Units.Count], DefLoss = new int[Units.Count];
        public int Levy, LevyLoss;
        public float DefBonus;
        public bool AttackerWon, PlayerAttacker;
    }

    public partial class Game
    {
        public readonly Queue<BattleReport> Reports = new Queue<BattleReport>();

        public bool CanSail(int fid) => fid == S.playerFaction && S.seafaring && SettlementsOf(fid).Any(s => s.buildings[(int)BType.Hafen] > 0);

        public static float Strength(int[] u)
        {
            float s = 0;
            for (int i = 0; i < Units.Count; i++) { var d = Units.Get(i); s += u[i] * (d.Atk + d.Def) * d.Hp / 10f; }
            return s;
        }

        public float DefenseBonus(SettlementData s)
        {
            float b = 0;
            for (int t = 0; t < Buildings.Count; t++) b += Buildings.Get(t).Defense * s.buildings[t];
            var bi = Map.biome[WorldMap.Idx(s.x, s.y)];
            if (bi == Biome.Hills) b += 0.15f;
            if (s.isCapitalOfEmperor) b += 0.3f;
            return b;
        }

        public int Levy(SettlementData s) => (int)(s.pop * 0.06f);

        public float DefenseStrength(SettlementData s)
        {
            float st = Strength(s.garrison) + Levy(s) * Strength1(UType.Miliz);
            return st * (1f + DefenseBonus(s));
        }

        static float Strength1(UType t) { var d = Units.Get(t); return (d.Atk + d.Def) * d.Hp / 10f; }

        public float MilitaryStrength(int fid)
        {
            float s = 0;
            foreach (var st in SettlementsOf(fid)) s += Strength(st.garrison) + Levy(st) * Strength1(UType.Miliz) * 0.5f;
            foreach (var a in ArmiesOf(fid)) s += Strength(a.units);
            return s;
        }

        public float ArmySpeed(ArmyData a)
        {
            float sp = float.MaxValue;
            for (int u = 0; u < Units.Count; u++) if (a.units[u] > 0) sp = Math.Min(sp, Units.Get(u).Speed);
            if (a.settlers > 0) sp = Math.Min(sp, 2.5f);
            if (sp == float.MaxValue) sp = 3f;
            return sp * (0.8f + 0.2f * a.morale);
        }

        public int ArmySize(ArmyData a) => a.units.Sum() + a.settlers;

        // ==================================================================
        //  Armeen aufstellen & befehligen
        // ==================================================================
        public ArmyData FormArmy(SettlementData s, int[] units, int settlers = 0)
        {
            int total = settlers;
            for (int u = 0; u < Units.Count; u++)
            {
                units[u] = Math.Max(0, Math.Min(units[u], s.garrison[u]));
                total += units[u];
            }
            if (total == 0) return null;
            var a = new ArmyData
            {
                id = NewId(), faction = s.faction, x = s.x + 0.5f, y = s.y + 0.5f, home = s.id, settlers = settlers,
                name = settlers > 0 ? "Siedler aus " + s.name : (units[(int)UType.Spaeher] == total ? "Späher aus " + s.name : "Heer aus " + s.name),
            };
            for (int u = 0; u < Units.Count; u++) { a.units[u] = units[u]; s.garrison[u] -= units[u]; }
            AddArmy(a);
            return a;
        }

        public bool OrderMove(ArmyData a, int tx, int ty, ArmyMission mission, int target, out string why)
        {
            why = null;
            int sx = (int)Math.Floor(a.x), sy = (int)Math.Floor(a.y);
            var path = Pathfinder.Find(Map, sx, sy, tx, ty, CanSail(a.faction));
            if (path == null)
            {
                why = S.seafaring || a.faction != S.playerFaction ? "Kein Weg dorthin." : "Kein Weg dorthin (über das Meer braucht ihr Schiffe).";
                return false;
            }
            a.path = path; a.pathIndex = 1; a.mission = (int)mission; a.target = target; a.targetX = tx; a.targetY = ty;
            if (path.Count <= 1) Arrive(a);
            return true;
        }

        public void Disband(ArmyData a)
        {
            var home = NearestSettlementOf(a.faction, a.x, a.y);
            if (home != null)
            {
                if (Dist(home.x, home.y, (int)a.x, (int)a.y) <= 2)
                    for (int u = 0; u < Units.Count; u++) home.garrison[u] += a.units[u];
                else home.pop += a.units.Sum() * 0.8f; // heimkehrende Soldaten werden wieder Bürger
                home.pop += a.settlers;
            }
            RemoveArmy(a);
        }

        public void Merge(ArmyData into, ArmyData other)
        {
            for (int u = 0; u < Units.Count; u++) into.units[u] += other.units[u];
            into.settlers += other.settlers;
            into.morale = (into.morale + other.morale) / 2f;
            RemoveArmy(other);
        }

        public bool CanSendSettlers(SettlementData s, out string why)
        {
            var f = F(s.faction);
            why = null;
            int pending = S.armies.Count(a => a.faction == f.id && a.settlers > 0);
            if (CountSettlements(f.id) + pending >= Ranks.Get(f.rank).MaxSettlements) { why = $"Euer Rang erlaubt nur {Ranks.Get(f.rank).MaxSettlements} Siedlungen"; return false; }
            if (s.pop < 34) { why = "Mind. 34 Bewohner nötig"; return false; }
            if (!CanAfford(f, SettlerCost)) { why = "Kosten: 40 Nahrung, 50 Holz, 30 Gold"; return false; }
            return true;
        }

        public static readonly float[] SettlerCost = { 40, 50, 0, 0, 30, 0 };

        public bool CanFoundAt(int fid, int x, int y, out string why)
        {
            why = null;
            if (!WorldMap.In(x, y)) { why = "Außerhalb der Karte"; return false; }
            int i = WorldMap.Idx(x, y);
            if (fid == S.playerFaction && !Map.disc[i]) { why = "Unerforschtes Gebiet"; return false; }
            if (!Map.Buildable(i)) { why = "Hier kann man nicht siedeln"; return false; }
            if (Map.owner[i] >= 0 && Map.owner[i] != fid) { why = "Gebiet gehört bereits jemandem"; return false; }
            foreach (var s in S.settlements) if (Dist(s.x, s.y, x, y) < 6.5f) { why = "Zu nah an " + s.name; return false; }
            return true;
        }

        public ArmyData SendSettlers(SettlementData s, int tx, int ty, out string why)
        {
            if (!CanSendSettlers(s, out why)) return null;
            if (!CanFoundAt(s.faction, tx, ty, out why)) return null;
            var f = F(s.faction);
            var a = new ArmyData { id = NewId(), faction = s.faction, x = s.x + 0.5f, y = s.y + 0.5f, home = s.id, settlers = 12, name = "Siedler aus " + s.name };
            AddArmy(a);
            if (!OrderMove(a, tx, ty, ArmyMission.Found, -1, out why)) { RemoveArmy(a); return null; }
            Pay(f, SettlerCost);
            s.pop -= 12;
            return a;
        }

        void FoundSettlement(ArmyData a)
        {
            int x = (int)Math.Floor(a.x), y = (int)Math.Floor(a.y);
            var f = F(a.faction);
            if (!CanFoundAt(a.faction, x, y, out string why))
            {
                if (f.isPlayer) Notify("Gründung gescheitert: " + why, 2);
                a.mission = (int)ArmyMission.Idle;
                return;
            }
            var used = new HashSet<string>(S.settlements.Select(s => s.name));
            var st = new SettlementData
            {
                id = NewId(), faction = a.faction, x = x, y = y, pop = a.settlers, founded = S.day, originalFaction = a.faction,
                name = NameGen.Place(Rng, Map.cont[WorldMap.Idx(x, y)] == 0 ? f.culture : Map.cont[WorldMap.Idx(x, y)], used), happiness = 60,
            };
            st.buildings[(int)BType.Wohnhaus] = 1;
            for (int u = 0; u < Units.Count; u++) st.garrison[u] = a.units[u];
            AddSettlement(st);
            RemoveArmy(a);
            RecomputeTerritory();
            if (f.isPlayer) { Reveal(x, y, 9); Notify($"{st.name} wurde gegründet!", 1); f.prestige += 10; }
            OnSettlementFounded(st);
        }

        // ==================================================================
        //  Bewegung
        // ==================================================================
        void UpdateArmies(float dt)
        {
            if (S.armies.Count == 0) return;
            var list = S.armies.ToArray();
            foreach (var a in list)
            {
                if (!arm.ContainsKey(a.id)) continue;
                if (a.units.Sum() + a.settlers <= 0) { RemoveArmy(a); continue; }
                if (a.path == null || a.pathIndex >= a.path.Count) continue;
                float budget = ArmySpeed(a) * dt;
                while (budget > 0 && a.pathIndex < a.path.Count)
                {
                    int next = a.path[a.pathIndex];
                    float tx = WorldMap.X(next) + 0.5f, ty = WorldMap.Y(next) + 0.5f;
                    float cost = Map.MoveCost(next, true);
                    if (cost == float.MaxValue) cost = 1f;
                    float dx = tx - a.x, dy = ty - a.y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float can = budget / cost;
                    if (can >= dist)
                    {
                        a.x = tx; a.y = ty; budget -= dist * cost; a.pathIndex++;
                        a.atSea = Map.IsSea(next);
                        if (a.faction == S.playerFaction) Reveal(WorldMap.X(next), WorldMap.Y(next), ArmyVision(a));
                    }
                    else
                    {
                        a.x += dx / dist * can; a.y += dy / dist * can; budget = 0;
                    }
                }
                if (a.pathIndex >= a.path.Count) Arrive(a);
            }
            // Feldschlachten
            var arr = S.armies.ToArray();
            for (int i = 0; i < arr.Length; i++)
                for (int j = i + 1; j < arr.Length; j++)
                {
                    var a = arr[i]; var b = arr[j];
                    if (!arm.ContainsKey(a.id) || !arm.ContainsKey(b.id) || a.faction == b.faction) continue;
                    if (Math.Abs(a.x - b.x) > 0.9f || Math.Abs(a.y - b.y) > 0.9f) continue;
                    if (!AtWar(a.faction, b.faction)) continue;
                    FieldBattle(a, b);
                }
        }

        int ArmyVision(ArmyData a)
        {
            int v = 3;
            for (int u = 0; u < Units.Count; u++) if (a.units[u] > 0) v = Math.Max(v, Units.Get(u).Vision);
            return a.atSea ? v + 2 : v;
        }

        void Arrive(ArmyData a)
        {
            var m = (ArmyMission)a.mission;
            a.path.Clear(); a.pathIndex = 0;
            var f = F(a.faction);
            switch (m)
            {
                case ArmyMission.Found:
                    FoundSettlement(a);
                    return;
                case ArmyMission.Attack:
                case ArmyMission.Raid:
                    {
                        var st = St(a.target);
                        a.mission = (int)ArmyMission.Idle;
                        if (st == null) return;
                        if (st.faction == a.faction) { MergeIntoGarrison(a, st); return; }
                        if (!AtWar(a.faction, st.faction))
                        {
                            if (f.isPlayer) Notify($"Euer Heer steht vor {st.name}, aber ihr seid nicht im Krieg mit {F(st.faction).name}.", 0);
                            return;
                        }
                        bool raid = m == ArmyMission.Raid || (F(st.faction).isPlayer && PlayerProtected)
                                    || (st.isCapitalOfEmperor && !f.isPlayer); // Valdoran fällt nur durch den Spieler
                        SiegeBattle(a, st, raid);
                        return;
                    }
                case ArmyMission.Return:
                    {
                        var st = St(a.target);
                        if (st != null && st.faction == a.faction) { MergeIntoGarrison(a, st); return; }
                        a.mission = (int)ArmyMission.Idle;
                        return;
                    }
                case ArmyMission.Explore:
                    ExploreNext(a);
                    return;
                default:
                    {
                        a.mission = (int)ArmyMission.Idle;
                        // Steht das Heer in einer eigenen Siedlung? Dann bleibt es dort als Armee stehen.
                        return;
                    }
            }
        }

        void MergeIntoGarrison(ArmyData a, SettlementData st)
        {
            for (int u = 0; u < Units.Count; u++) st.garrison[u] += a.units[u];
            st.pop += a.settlers;
            RemoveArmy(a);
        }

        public void ExploreNext(ArmyData a)
        {
            int ax = (int)a.x, ay = (int)a.y;
            bool sail = CanSail(a.faction);
            for (int t = 0; t < 40; t++)
            {
                double ang = Rng.NextDouble() * Math.PI * 2;
                int r = 10 + Rng.Next(20);
                int tx = ax + (int)(Math.Cos(ang) * r), ty = ay + (int)(Math.Sin(ang) * r);
                if (!WorldMap.In(tx, ty)) continue;
                int i = WorldMap.Idx(tx, ty);
                if (Map.disc[i] && t < 30) continue;
                if (Map.MoveCost(i, sail) == float.MaxValue) continue;
                if (OrderMove(a, tx, ty, ArmyMission.Explore, -1, out _)) return;
            }
            a.mission = (int)ArmyMission.Idle;
            if (a.faction == S.playerFaction) Notify($"{a.name}: Hier gibt es nichts Neues mehr zu entdecken.", 0);
        }

        public void SendHome(ArmyData a)
        {
            var home = St(a.home);
            if (home == null || home.faction != a.faction) home = NearestSettlementOf(a.faction, a.x, a.y);
            if (home == null) { a.mission = (int)ArmyMission.Idle; return; }
            a.home = home.id;
            if (!OrderMove(a, home.x, home.y, ArmyMission.Return, home.id, out _)) a.mission = (int)ArmyMission.Idle;
        }

        // ==================================================================
        //  Kampf
        // ==================================================================
        /// <summary>Rundenbasierte Schlacht mit Konter-System. Ändert die Arrays (verbleibende Truppen).</summary>
        bool Fight(float[] att, float[] def, float defBonus, float attMorale, float defMorale)
        {
            float startA = StrengthF(att), startD = StrengthF(def) * (1 + defBonus);
            if (startD <= 0.01f) return true;
            if (startA <= 0.01f) return false;
            for (int round = 0; round < 6; round++)
            {
                float dmgToD = Damage(att, def, attMorale, round) * Rand(0.85f, 1.15f);
                float dmgToA = Damage(def, att, defMorale, round) * Rand(0.85f, 1.15f);
                ApplyDamage(def, dmgToD, 1f + defBonus);
                ApplyDamage(att, dmgToA, 1f);
                float a = StrengthF(att), d = StrengthF(def) * (1 + defBonus);
                if (a <= 0.01f || d <= 0.01f) break;
                if (a < startA * 0.3f || d < startD * 0.25f) break; // Flucht
            }
            float ra = StrengthF(att) / startA, rd = StrengthF(def) * (1 + defBonus) / startD;
            return ra > rd * 1.05f;
        }

        float Rand(float a, float b) => a + (float)Rng.NextDouble() * (b - a);

        static float StrengthF(float[] u)
        {
            float s = 0;
            for (int i = 0; i < Units.Count; i++) { var d = Units.Get(i); s += u[i] * (d.Atk + d.Def) * d.Hp / 10f; }
            return s;
        }

        static float Damage(float[] from, float[] to, float morale, int round)
        {
            float total = 0, enemy = 0;
            for (int j = 0; j < Units.Count; j++) enemy += to[j];
            if (enemy <= 0) return 0;
            for (int i = 0; i < Units.Count; i++)
            {
                if (from[i] <= 0) continue;
                var d = Units.Get(i);
                float mult = 1f;
                if (d.StrongVs >= 0) mult += (d.StrongMult - 1f) * (to[d.StrongVs] / enemy);
                if (d.Ranged && round == 0) mult *= 1.5f;
                total += from[i] * d.Atk * mult;
            }
            return total * morale * 0.45f;
        }

        static void ApplyDamage(float[] u, float dmg, float toughMult)
        {
            float pool = 0;
            for (int i = 0; i < Units.Count; i++) { var d = Units.Get(i); pool += u[i] * d.Hp * (1 + d.Def * 0.08f) * toughMult; }
            if (pool <= 0) return;
            float frac = Math.Min(1f, dmg / pool);
            for (int i = 0; i < Units.Count; i++) u[i] = Math.Max(0, u[i] * (1f - frac));
        }

        int[] Round(float[] f)
        {
            var r = new int[f.Length];
            for (int i = 0; i < f.Length; i++)
            {
                int b = (int)Math.Floor(f[i]);
                r[i] = b + (Rng.NextDouble() < f[i] - b ? 1 : 0);
            }
            return r;
        }

        static float[] ToF(int[] u) { var f = new float[u.Length]; for (int i = 0; i < u.Length; i++) f[i] = u[i]; return f; }

        void SiegeBattle(ArmyData a, SettlementData st, bool raid)
        {
            var af = F(a.faction); var df = F(st.faction);
            var rep = new BattleReport
            {
                Title = raid ? "Überfall auf " + st.name : "Belagerung von " + st.name, Place = st.name,
                Attacker = af.name, Defender = df.name, PlayerAttacker = af.isPlayer,
            };
            Array.Copy(a.units, rep.AttStart, Units.Count);
            Array.Copy(st.garrison, rep.DefStart, Units.Count);
            int levy = Levy(st);
            rep.Levy = levy;

            float siege = 0;
            for (int u = 0; u < Units.Count; u++) siege += a.units[u] * Units.Get(u).Siege;
            float bonus = Math.Max(0, DefenseBonus(st) - siege);
            if (raid) bonus *= 0.5f; // Überfälle zielen auf das Umland
            rep.DefBonus = bonus;

            var att = ToF(a.units);
            var def = ToF(st.garrison);
            def[(int)UType.Miliz] += levy;
            bool won = Fight(att, def, bonus, a.morale, 1f);
            var attLeft = Round(att); var defLeft = Round(def);
            for (int u = 0; u < Units.Count; u++) { rep.AttLoss[u] = a.units[u] - attLeft[u]; a.units[u] = attLeft[u]; }
            int milLeft = defLeft[(int)UType.Miliz];
            int garMil = st.garrison[(int)UType.Miliz];
            // Verluste zuerst beim Aufgebot
            int levyLeft = Math.Max(0, Math.Min(levy, milLeft - garMil));
            rep.LevyLoss = levy - levyLeft;
            st.pop = Math.Max(5, st.pop - rep.LevyLoss);
            defLeft[(int)UType.Miliz] = Math.Max(0, milLeft - levyLeft);
            for (int u = 0; u < Units.Count; u++) { rep.DefLoss[u] = Math.Max(0, st.garrison[u] - defLeft[u]); st.garrison[u] = Math.Min(st.garrison[u], defLeft[u]); }

            st.lastBattleDay = S.day;
            rep.AttackerWon = won;
            af.warWeariness += won ? 1 : 5; df.warWeariness += won ? 5 : 1;

            if (won)
            {
                af.prestige += raid ? 4 : 12;
                a.morale = Math.Min(1.3f, a.morale + 0.1f);
                if (raid)
                {
                    int carry = a.units.Sum() * 8;
                    var loot = new float[ResInfo.Count];
                    for (int r = 0; r < ResInfo.Count; r++)
                    {
                        float take = Math.Min(df.res[r] * 0.2f, carry);
                        df.res[r] -= take; af.res[r] += take; loot[r] = take;
                    }
                    st.happiness -= 15; st.pop *= 0.95f;
                    rep.Outcome = "Das Umland wurde geplündert: " + LootText(loot);
                    ModRel(af.id, df.id, -10);
                    SendHome(a);
                }
                else
                {
                    rep.Outcome = st.name + " wurde erobert!";
                    Conquer(a, st, df);
                }
            }
            else
            {
                df.prestige += 6;
                a.morale = Math.Max(0.5f, a.morale - 0.2f);
                rep.Outcome = "Der Angriff wurde abgewehrt.";
                if (a.units.Sum() > 0) SendHome(a); else RemoveArmy(a);
            }
            if (af.isPlayer || df.isPlayer)
            {
                bool playerWon = (af.isPlayer && won) || (df.isPlayer && !won);
                if (playerWon) S.battlesWon++; else S.battlesLost++;
                if (!Offline) Reports.Enqueue(rep);
                Notify(rep.Title + ": " + rep.Outcome, playerWon ? 1 : 2);
            }
            OnBattle(af, df, won);
        }

        static string LootText(float[] loot)
        {
            var parts = new List<string>();
            for (int r = 0; r < ResInfo.Count; r++) if (loot[r] >= 1) parts.Add((int)loot[r] + " " + ResInfo.Name(r));
            return parts.Count == 0 ? "nichts" : string.Join(", ", parts);
        }

        void Conquer(ArmyData a, SettlementData st, FactionData oldF)
        {
            var newF = F(a.faction);
            bool liberated = oldF.isEmperor && newF.isPlayer;
            TransferSettlement(st, a.faction);
            for (int u = 0; u < Units.Count; u++) st.garrison[u] = a.units[u];
            RemoveArmy(a);
            st.pop *= 0.88f;
            if (st.buildings[(int)BType.Mauer] > 0) st.buildings[(int)BType.Mauer]--;
            if (liberated)
            {
                st.happiness = 55; st.taxRate = 0.08f;
                Notify($"Die Bürger von {st.name} jubeln: Befreit vom Kaiser!", 1);
            }
            else
            {
                st.happiness = 25; st.conqueredDay = S.day;
            }
            newF.prestige += 10;
            OnConquered(st, oldF, newF);
        }

        void FieldBattle(ArmyData a, ArmyData b)
        {
            var af = F(a.faction); var bf = F(b.faction);
            var rep = new BattleReport
            {
                Title = "Feldschlacht", Place = Map.BiomeName(WorldMap.Idx((int)a.x, (int)a.y)), Attacker = af.name, Defender = bf.name,
                PlayerAttacker = af.isPlayer,
            };
            Array.Copy(a.units, rep.AttStart, Units.Count);
            Array.Copy(b.units, rep.DefStart, Units.Count);
            var bi = Map.biome[WorldMap.Idx((int)b.x, (int)b.y)];
            float terrain = bi == Biome.Hills || bi == Biome.Forest || bi == Biome.Pine ? 0.15f : 0f;
            var fa = ToF(a.units); var fb = ToF(b.units);
            // Siedler kämpfen nicht
            bool won = Fight(fa, fb, terrain, a.morale, b.morale);
            var la = Round(fa); var lb = Round(fb);
            for (int u = 0; u < Units.Count; u++) { rep.AttLoss[u] = a.units[u] - la[u]; a.units[u] = la[u]; rep.DefLoss[u] = b.units[u] - lb[u]; b.units[u] = lb[u]; }
            rep.AttackerWon = won;
            var loser = won ? b : a; var winner = won ? a : b;
            F(winner.faction).prestige += 6;
            winner.morale = Math.Min(1.3f, winner.morale + 0.1f);
            loser.morale = Math.Max(0.4f, loser.morale - 0.25f);
            if (loser.settlers > 0 && loser.units.Sum() == 0) loser.settlers = 0; // Siedler zerstreut
            F(loser.faction).warWeariness += 4;
            rep.Outcome = (won ? af.name : bf.name) + " siegt.";
            if (loser.units.Sum() + loser.settlers <= 0) RemoveArmy(loser); else SendHome(loser);
            if (winner.units.Sum() + winner.settlers <= 0) RemoveArmy(winner);
            if (af.isPlayer || bf.isPlayer)
            {
                bool pw = F(winner.faction).isPlayer;
                if (pw) S.battlesWon++; else S.battlesLost++;
                if (!Offline) Reports.Enqueue(rep);
                Notify("Feldschlacht: " + rep.Outcome, pw ? 1 : 2);
            }
            OnBattle(af, bf, won);
        }
    }
}
