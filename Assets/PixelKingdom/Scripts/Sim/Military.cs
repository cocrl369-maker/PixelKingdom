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
            if (s.buildings[(int)BType.Mauer] > 0 && HasTech(F(s.faction), Techs.Steinmetzkunst)) b += 0.1f;
            return b;
        }

        public int Levy(SettlementData s) => (int)(s.pop * 0.06f * (EdictOn(F(s.faction), Edicts.Wehrpflicht) ? 1.6f : 1f));

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
            return sp * (0.8f + 0.2f * a.morale) * (a.faction == S.playerFaction && HasTech(Techs.Kartografie) ? 1.1f : 1f);
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
                name = settlers > 0 ? Loc.T("Siedler aus ") + s.name : (units[(int)UType.Spaeher] == total ? Loc.T("Späher aus ") + s.name : Loc.T("Heer aus ") + s.name),
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
                why = S.seafaring || a.faction != S.playerFaction ? Loc.T("Kein Weg dorthin.") : Loc.T("Kein Weg dorthin (über das Meer braucht ihr Schiffe).");
                return false;
            }
            if (!CheckTransport(a, path, out why))
            {
                // Zu wenige Schiffe: gibt es einen Landweg?
                var land = Pathfinder.Find(Map, sx, sy, tx, ty, false);
                if (land == null) return false;
                path = land; why = null;
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

        /// <summary>Teilt ein Heer: die Hälfte jeder Truppenart bildet ein neues Heer.</summary>
        public ArmyData SplitArmy(ArmyData a)
        {
            int total = 0;
            var half = new int[Units.Count];
            for (int u = 0; u < Units.Count; u++) { half[u] = a.units[u] / 2; total += half[u]; }
            if (total == 0) return null;
            var b = new ArmyData { id = NewId(), faction = a.faction, x = a.x + 0.3f, y = a.y - 0.3f, home = a.home, morale = a.morale, name = a.name + Loc.T(" (geteilt)") };
            for (int u = 0; u < Units.Count; u++) { b.units[u] = half[u]; a.units[u] -= half[u]; }
            AddArmy(b);
            return b;
        }

        /// <summary>Eigene Siedlung, in der das Heer gerade steht (oder null).</summary>
        public SettlementData SettlementUnder(ArmyData a)
        {
            foreach (var s in SettlementsOf(a.faction))
                if (Math.Abs(s.x + 0.5f - a.x) <= 1.6f && Math.Abs(s.y + 0.5f - a.y) <= 1.6f) return s;
            return null;
        }

        /// <summary>Truppen zwischen Heer und Garnison verschieben (n > 0: aus der Garnison ins Heer).</summary>
        public void ExchangeTroops(ArmyData a, SettlementData s, int unit, int n)
        {
            if (n > 0) { n = Math.Min(n, s.garrison[unit]); s.garrison[unit] -= n; a.units[unit] += n; }
            else { n = Math.Min(Math.Min(-n, a.units[unit]), F(s.faction).isPlayer ? GarrisonFree(s) : int.MaxValue); a.units[unit] -= n; s.garrison[unit] += n; }
        }

        /// <summary>Was würde ein Befehl an diesen Ort bewirken? (Vorschau für die Oberfläche)</summary>
        public string OrderPreview(ArmyData a, int tx, int ty, SettlementData target, bool raid)
        {
            if (a.settlers > 0 && a.units.Sum() == 0)
                return CanFoundAt(a.faction, tx, ty, out string why) ? Loc.T("Hier eine Siedlung gründen") : Loc.T("Gründen nicht möglich: ") + why;
            var poi = PoiNear(tx + 0.5f, ty + 0.5f, 0.9f);
            if (poi != null && target == null && G_IsDisc(poi.x, poi.y))
                return poi.type == 0 ? (poi.done ? Loc.T("Ruine (bereits durchsucht)") : Loc.T("Ruine erkunden")) : poi.type == 1 ? Loc.T("Räuberlager angreifen") : Loc.T("Fahrendes Volk besuchen");
            if (target != null)
            {
                if (target.faction == a.faction) return Loc.T("In ") + target.name + Loc.T(" einrücken (Garnison)");
                var f = F(target.faction);
                if (!AtWar(a.faction, target.faction)) return (raid ? Loc.T("Überfall auf ") : Loc.T("Angriff auf ")) + target.name + Loc.T(" – erst Krieg erklären!");
                return (raid ? Loc.T("Überfall auf ") : Loc.T("Erobern: ")) + target.name + " (" + f.name + ")";
            }
            if (!WorldMap.In(tx, ty)) return null;
            int ti = WorldMap.Idx(tx, ty);
            if (Map.biome[ti] == Biome.Mountain || Map.biome[ti] == Biome.Snow) return Loc.T("Gebirge – unpassierbar");
            if (Map.MoveCost(ti, CanSail(a.faction)) == float.MaxValue) return Loc.T("Dort kann das Heer nicht hin");
            return Loc.T("Hierher marschieren");
        }

        bool G_IsDisc(int x, int y) => IsDiscovered(x, y);

        public bool CanSendSettlers(SettlementData s, out string why)
        {
            var f = F(s.faction);
            why = null;
            int pending = S.armies.Count(a => a.faction == f.id && a.settlers > 0);
            if (CountSettlements(f.id) + pending >= Ranks.Get(f.rank).MaxSettlements) { why = Loc.F("Euer Rang erlaubt nur {0} Siedlungen", (Ranks.Get(f.rank).MaxSettlements)); return false; }
            if (s.pop < 34) { why = Loc.T("Mind. 34 Bewohner nötig"); return false; }
            if (!CanAfford(f, SettlerCost)) { why = Loc.T("Kosten: 40 Nahrung, 50 Holz, 30 Gold"); return false; }
            return true;
        }

        public static readonly float[] SettlerCost = { 40, 50, 0, 0, 30, 0 };

        public bool CanFoundAt(int fid, int x, int y, out string why)
        {
            why = null;
            if (!WorldMap.In(x, y)) { why = Loc.T("Außerhalb der Karte"); return false; }
            int i = WorldMap.Idx(x, y);
            if (fid == S.playerFaction && !Map.disc[i]) { why = Loc.T("Unerforschtes Gebiet"); return false; }
            if (!Map.Buildable(i)) { why = Loc.T("Hier kann man nicht siedeln"); return false; }
            if (Map.owner[i] >= 0 && Map.owner[i] != fid) { why = Loc.T("Gebiet gehört bereits jemandem"); return false; }
            foreach (var s in S.settlements) if (Dist(s.x, s.y, x, y) < 8f) { why = Loc.T("Zu nah an ") + s.name; return false; }
            return true;
        }

        public ArmyData SendSettlers(SettlementData s, int tx, int ty, out string why)
        {
            if (!CanSendSettlers(s, out why)) return null;
            if (!CanFoundAt(s.faction, tx, ty, out why)) return null;
            var f = F(s.faction);
            var a = new ArmyData { id = NewId(), faction = s.faction, x = s.x + 0.5f, y = s.y + 0.5f, home = s.id, settlers = 12, name = Loc.T("Siedler aus ") + s.name };
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
                if (f.isPlayer) Notify(Loc.T("Gründung gescheitert: ") + why, 2);
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
            if (f.isPlayer) { Reveal(x, y, 9); Notify(Loc.F("{0} wurde gegründet!", (st.name)), 1); f.prestige += 10; }
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
                    if (a.faction == S.playerFaction && Map.IsSea(next)) cost /= SeaSpeed * 1.6f;   // Schiffe sind schneller als Fußtruppen, bessere noch mehr
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
            if (a.faction == S.playerFaction && HasTech(Techs.Kartografie)) v += 2;
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
                            if (f.isPlayer) Notify(Loc.F("Euer Heer steht vor {0}, aber ihr seid nicht im Krieg mit {1}.", (st.name), (F(st.faction).name)), 0);
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
                case ArmyMission.Poi:
                    {
                        var poi = Poi(a.target);
                        if (poi != null) VisitPoi(a, poi); else a.mission = (int)ArmyMission.Idle;
                        return;
                    }
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
            st.pop += a.settlers; a.settlers = 0;
            int free = F(st.faction).isPlayer ? Math.Max(0, GarrisonCap(st) - GarrisonCount(st)) : int.MaxValue;
            for (int u = 0; u < Units.Count && free > 0; u++)
            {
                int n = Math.Min(free, a.units[u]);
                st.garrison[u] += n; a.units[u] -= n; free -= n;
            }
            if (a.units.Sum() <= 0) { RemoveArmy(a); return; }
            // Kein Platz mehr: der Rest lagert als Heer vor der Stadt
            a.mission = (int)ArmyMission.Idle;
            if (F(a.faction).isPlayer) Notify(Loc.F("Die Garnison von {0} ist voll – {1} Mann lagern vor der Stadt. Baut eine Kaserne oder ein Feldlager.", st.name, a.units.Sum()), 2);
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
            if (a.faction == S.playerFaction) Notify(Loc.F("{0}: Hier gibt es nichts Neues mehr zu entdecken.", (a.name)), 0);
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
                Title = raid ? Loc.T("Überfall auf ") + st.name : Loc.T("Belagerung von ") + st.name, Place = st.name,
                Attacker = af.name, Defender = df.name, PlayerAttacker = af.isPlayer,
            };
            Array.Copy(a.units, rep.AttStart, Units.Count);
            Array.Copy(st.garrison, rep.DefStart, Units.Count);
            int levy = Levy(st);
            rep.Levy = levy;

            float siege = 0;
            for (int u = 0; u < Units.Count; u++) siege += a.units[u] * Units.Get(u).Siege;
            float bonus = Math.Max(0, DefenseBonus(st) - siege - NavalSiegeBonus(a, st));
            if (raid) bonus *= 0.5f; // Überfälle zielen auf das Umland
            rep.DefBonus = bonus;

            var att = ToF(a.units);
            var def = ToF(st.garrison);
            def[(int)UType.Miliz] += levy;
            bool won = Fight(att, def, bonus, a.morale * CombatMult(a.faction), CombatMult(st.faction));
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
                    rep.Outcome = Loc.T("Das Umland wurde geplündert: ") + LootText(loot);
                    ModRel(af.id, df.id, -10);
                    SendHome(a);
                }
                else
                {
                    rep.Outcome = st.name + Loc.T(" wurde erobert!");
                    Conquer(a, st, df);
                }
            }
            else
            {
                df.prestige += 6;
                a.morale = Math.Max(0.5f, a.morale - 0.2f);
                rep.Outcome = Loc.T("Der Angriff wurde abgewehrt.");
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
            return parts.Count == 0 ? Loc.T("nichts") : string.Join(", ", parts);
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
                Notify(Loc.F("Die Bürger von {0} jubeln: Befreit vom Kaiser!", (st.name)), 1);
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
                Title = Loc.T("Feldschlacht"), Place = Map.BiomeName(WorldMap.Idx((int)a.x, (int)a.y)), Attacker = af.name, Defender = bf.name,
                PlayerAttacker = af.isPlayer,
            };
            Array.Copy(a.units, rep.AttStart, Units.Count);
            Array.Copy(b.units, rep.DefStart, Units.Count);
            var bi = Map.biome[WorldMap.Idx((int)b.x, (int)b.y)];
            float terrain = (bi == Biome.Hills || bi == Biome.Forest || bi == Biome.Pine ? 0.15f : 0f) + CampBonus(b);
            var fa = ToF(a.units); var fb = ToF(b.units);
            // Siedler kämpfen nicht
            bool won = Fight(fa, fb, terrain, a.morale * CombatMult(a.faction), b.morale * CombatMult(b.faction));
            var la = Round(fa); var lb = Round(fb);
            for (int u = 0; u < Units.Count; u++) { rep.AttLoss[u] = a.units[u] - la[u]; a.units[u] = la[u]; rep.DefLoss[u] = b.units[u] - lb[u]; b.units[u] = lb[u]; }
            rep.AttackerWon = won;
            var loser = won ? b : a; var winner = won ? a : b;
            F(winner.faction).prestige += 6;
            winner.morale = Math.Min(1.3f, winner.morale + 0.1f);
            loser.morale = Math.Max(0.4f, loser.morale - 0.25f);
            if (loser.settlers > 0 && loser.units.Sum() == 0) loser.settlers = 0; // Siedler zerstreut
            F(loser.faction).warWeariness += 4;
            rep.Outcome = Loc.F("{0} siegt.", won ? af.name : bf.name);
            if (loser.units.Sum() + loser.settlers <= 0) RemoveArmy(loser); else SendHome(loser);
            if (winner.units.Sum() + winner.settlers <= 0) RemoveArmy(winner);
            if (af.isPlayer || bf.isPlayer)
            {
                bool pw = F(winner.faction).isPlayer;
                if (pw) S.battlesWon++; else S.battlesLost++;
                if (!Offline) Reports.Enqueue(rep);
                Notify(Loc.T("Feldschlacht: ") + rep.Outcome, pw ? 1 : 2);
            }
            OnBattle(af, bf, won);
        }
    }
}
