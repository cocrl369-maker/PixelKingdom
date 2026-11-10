using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    public enum DipAction { Gift, Trade, Pact, Alliance, Annex, War, Peace, Tribute }

    public class DipEval
    {
        public float Score;            // >= 0 -> Annahme
        public float Cost;             // Goldkosten
        public bool Possible = true;
        public string Blocker;
        public List<string> Reasons = new List<string>();
        public string Chance => !Possible ? Loc.T("unmöglich") : Score >= 25 ? Loc.T("sicher") : Score >= 0 ? Loc.T("wahrscheinlich") : Score >= -20 ? Loc.T("fraglich") : Loc.T("keine Chance");
    }

    public partial class Game
    {
        // ==================================================================
        //  Beziehungen
        // ==================================================================
        public RelationData Rel(int a, int b)
        {
            var fa = F(a);
            foreach (var r in fa.relations) if (r.other == b) return r;
            var nr = new RelationData { other = b };
            fa.relations.Add(nr);
            return nr;
        }

        public float RelValue(int a, int b) => a == b ? 100 : Rel(a, b).value;
        public DipStatus Status(int a, int b) => a == b ? DipStatus.Alliance : (DipStatus)Rel(a, b).status;
        public bool AtWar(int a, int b) => a != b && Rel(a, b).status == (int)DipStatus.War;

        public void ModRel(int a, int b, float d)
        {
            if (a == b || F(a) == null || F(b) == null) return;
            var r1 = Rel(a, b); var r2 = Rel(b, a);
            r1.value = Clamp(r1.value + d, -100, 100);
            r2.value = r1.value;
        }

        public void SetStatus(int a, int b, DipStatus st)
        {
            if (a == b) return;
            var r1 = Rel(a, b); var r2 = Rel(b, a);
            r1.status = r2.status = (int)st;
            r1.since = r2.since = S.day;
            if (st == DipStatus.War) { r1.trade = r2.trade = false; }
        }

        public void SetTrade(int a, int b, bool on) { Rel(a, b).trade = on; Rel(b, a).trade = on; }

        public int AtWarCount(int fid)
        {
            int n = 0;
            foreach (var r in F(fid).relations) if (r.status == (int)DipStatus.War && F(r.other) != null && F(r.other).alive) n++;
            return n;
        }

        public IEnumerable<FactionData> EnemiesOf(int fid)
        {
            foreach (var r in F(fid).relations)
                if (r.status == (int)DipStatus.War) { var o = F(r.other); if (o != null && o.alive) yield return o; }
        }

        public IEnumerable<FactionData> AlliesOf(int fid)
        {
            foreach (var r in F(fid).relations)
                if (r.status == (int)DipStatus.Alliance) { var o = F(r.other); if (o != null && o.alive) yield return o; }
        }

        public void InitRelations()
        {
            foreach (var f in S.factions) InitRelationsFor(f);
        }

        public void InitRelationsFor(FactionData f)
        {
            var emp = Emperor;
            foreach (var o in S.factions)
            {
                if (o.id == f.id) continue;
                var r = Rel(f.id, o.id);
                if (r.value != 0 || r.status != 0) continue;
                float v = (float)Rng.NextDouble() * 30 - 10;
                if (f.culture != o.culture) v -= 10;
                if (emp != null && (f.id == emp.id || o.id == emp.id))
                {
                    var other = f.id == emp.id ? o : f;
                    v = other.isPlayer ? 0 : (other.loyalty - 0.5f) * 80f;
                    if (other.continent == 0 && !other.isPlayer && other.loyalty > 0.45f) { r.status = (int)DipStatus.Pact; Rel(o.id, f.id).status = (int)DipStatus.Pact; }
                }
                r.value = v;
                var back = Rel(o.id, f.id);
                back.value = v;
            }
        }

        // ==================================================================
        //  Täglicher Wandel: Interessenkonflikte, Handel, gemeinsame Feinde
        // ==================================================================
        void DiplomacyDay()
        {
            var emp = Emperor;
            bool playerVsEmperor = emp != null && emp.alive && AtWar(PlayerId, emp.id);
            var alive = S.factions.Where(f => f.alive).ToArray();
            // Nähe-Cache: haben zwei Fraktionen gemeinsame Grenzen?
            if (S.day % 5 == 0) RefreshNeighbours();

            for (int i = 0; i < alive.Length; i++)
                for (int j = i + 1; j < alive.Length; j++)
                {
                    var a = alive[i]; var b = alive[j];
                    var r = Rel(a.id, b.id);
                    float d = -r.value * 0.004f; // langsames Vergessen
                    if (r.trade) d += 0.05f;
                    if (r.status == (int)DipStatus.Alliance) d += 0.04f;
                    if (r.status == (int)DipStatus.War) d -= 0.05f;
                    if (IsNeighbour(a.id, b.id)) d -= 0.02f * (0.5f + Math.Max(a.ambition, b.ambition));
                    // Gebietsansprüche
                    var ca = St(a.claim); if (ca != null && ca.faction == b.id) d -= 0.05f * (0.5f + a.ambition);
                    var cb = St(b.claim); if (cb != null && cb.faction == a.id) d -= 0.05f * (0.5f + b.ambition);
                    // Haltung zum Kaiser
                    if (emp != null && emp.alive && a.continent == 0 && b.continent == 0 && !a.isEmperor && !b.isEmperor)
                    {
                        float la = a.isPlayer ? (playerVsEmperor ? 0 : 0.5f) : a.loyalty;
                        float lb = b.isPlayer ? (playerVsEmperor ? 0 : 0.5f) : b.loyalty;
                        d -= Math.Abs(la - lb) * 0.03f;
                    }
                    // Neid auf den Spieler
                    if (a.isPlayer && b.ambition > 0.6f && a.rank > b.rank) d -= 0.01f * (a.rank - b.rank);
                    if (b.isPlayer && a.ambition > 0.6f && b.rank > a.rank) d -= 0.01f * (b.rank - a.rank);
                    if (d != 0) ModRel(a.id, b.id, d);

                    // Waffenruhe läuft aus
                    if (r.status == (int)DipStatus.Truce && S.day - r.since > 30) SetStatus(a.id, b.id, DipStatus.Neutral);
                }

            // gemeinsame Feinde verbinden (seltener berechnen)
            if (S.day % 5 == 0)
                foreach (var f in alive)
                {
                    var enemies = EnemiesOf(f.id).Select(e => e.id).ToList();
                    if (enemies.Count == 0) continue;
                    foreach (var o in alive)
                    {
                        if (o.id == f.id) continue;
                        foreach (int e in enemies) if (e != o.id && AtWar(o.id, e)) { ModRel(f.id, o.id, 0.15f); break; }
                    }
                }
        }

        readonly HashSet<long> neighbours = new HashSet<long>();
        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        public bool IsNeighbour(int a, int b) => neighbours.Contains(Key(a, b));

        void RefreshNeighbours()
        {
            neighbours.Clear();
            var list = S.settlements;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    var s1 = list[i]; var s2 = list[j];
                    if (s1.faction == s2.faction) continue;
                    if (Dist(s1.x, s1.y, s2.x, s2.y) < 32) neighbours.Add(Key(s1.faction, s2.faction));
                }
        }

        // ==================================================================
        //  Diplomatische Aktionen des Spielers
        // ==================================================================
        public float GiftAmount(FactionData f) => (float)Math.Round(40 + TotalPop(f.id) * 0.4f);
        public float AllianceCost(FactionData f) => (float)Math.Round(80 + MilitaryStrength(f.id) * 0.25f + TotalPop(f.id) * 0.6f);
        public float AnnexCost(FactionData f) => (float)Math.Round(150 + TotalPop(f.id) * 4.5f + MilitaryStrength(f.id) * 0.8f);

        public DipEval Evaluate(DipAction act, FactionData f)
        {
            var p = Player;
            var e = new DipEval();
            var rel = Rel(p.id, f.id);
            var st = (DipStatus)rel.status;
            float myStr = MilitaryStrength(p.id) + 1, theirStr = MilitaryStrength(f.id) + 1;
            float ratio = myStr / theirStr;

            void R(string text, float v) { e.Score += v; if (Math.Abs(v) >= 1) e.Reasons.Add((v > 0 ? "+" : "") + (int)Math.Round(v) + " " + text); }
            if (f.xenophobic && act != DipAction.War)
            {
                e.Possible = false;
                e.Blocker = Loc.T("Das Eiserne Reich empfängt keine Gesandten – es tötet sie. Diese Insel lässt sich nur erobern.");
                return e;
            }
            R(Loc.T("Beziehung"), rel.value * 0.6f);

            switch (act)
            {
                case DipAction.Gift:
                    e.Cost = GiftAmount(f);
                    e.Score = 100; e.Reasons.Clear(); e.Reasons.Add(Loc.T("Geschenke werden immer angenommen"));
                    break;
                case DipAction.Trade:
                    if (st == DipStatus.War) { e.Possible = false; e.Blocker = Loc.T("Ihr seid im Krieg"); break; }
                    if (rel.trade) { e.Possible = false; e.Blocker = Loc.T("Besteht bereits"); break; }
                    if (!HasMarket(p.id)) { e.Possible = false; e.Blocker = Loc.T("Ihr braucht einen Markt"); break; }
                    R(Loc.T("Händlergeist"), f.greed * 20);
                    R(Loc.T("Grundhaltung"), 5);
                    break;
                case DipAction.Pact:
                    if (st == DipStatus.War) { e.Possible = false; e.Blocker = Loc.T("Zuerst Frieden schließen"); break; }
                    if (st >= DipStatus.Pact) { e.Possible = false; e.Blocker = Loc.T("Besteht bereits"); break; }
                    R(Loc.T("Eure Stärke"), Clamp((ratio - 1f) * 10, -10, 15));
                    R(Loc.T("Angriffslust"), -f.aggression * 15);
                    R(Loc.T("Ehre"), f.honor * 8);
                    R(Loc.T("Grundhaltung"), -5);
                    break;
                case DipAction.Alliance:
                    if (st == DipStatus.War) { e.Possible = false; e.Blocker = Loc.T("Zuerst Frieden schließen"); break; }
                    if (st == DipStatus.Alliance) { e.Possible = false; e.Blocker = Loc.T("Besteht bereits"); break; }
                    e.Cost = AllianceCost(f);
                    R(Loc.T("Mitgift (") + (int)e.Cost + Loc.T(" Gold)"), 25);
                    R(Loc.T("Bündnisse sind ernst"), -40);
                    R(Loc.T("Ehre"), f.honor * 10);
                    R(Loc.T("Eure Stärke"), Clamp((ratio - 1f) * 8, -15, 15));
                    if (EnemiesOf(p.id).Any(x => AtWar(f.id, x.id))) R(Loc.T("Gemeinsamer Feind"), 20);
                    var emp = Emperor;
                    if (emp != null && emp.alive && AtWar(p.id, emp.id) && !f.isEmperor) R(Loc.T("Treue zum Kaiser"), -f.loyalty * 50 + 15);
                    if (f.ambition > 0.6f && p.rank > f.rank) R(Loc.T("Neid"), -(p.rank - f.rank) * 6);
                    break;
                case DipAction.Annex:
                    if (st == DipStatus.War) { e.Possible = false; e.Blocker = Loc.T("Nicht im Krieg"); break; }
                    if (f.isEmperor) { e.Possible = false; e.Blocker = Loc.T("Der Kaiser beugt sich niemandem"); break; }
                    if (p.rank <= f.rank && !f.isFree) { e.Possible = false; e.Blocker = Loc.T("Euer Rang muss höher sein"); break; }
                    if (CountSettlements(p.id) + CountSettlements(f.id) > Ranks.Get(p.rank).MaxSettlements + 2) { e.Possible = false; e.Blocker = Loc.T("Zu viele Siedlungen für euren Rang"); break; }
                    e.Cost = AnnexCost(f);
                    R(Loc.T("Großzügiges Angebot"), 20);
                    R(Loc.T("Unabhängigkeit"), f.isFree ? -15 : -45);
                    R(Loc.T("Euer Ansehen"), Clamp((p.prestige - f.prestige) / 20f, -15, 25));
                    R(Loc.T("Machtverhältnis"), Clamp((ratio - 1.5f) * 10, -20, 25));
                    R(Loc.T("Ehrgeiz"), -f.ambition * 20);
                    if (st == DipStatus.Alliance) R(Loc.T("Verbündete"), 15);
                    break;
                case DipAction.War:
                    if (st == DipStatus.War) { e.Possible = false; e.Blocker = Loc.T("Bereits im Krieg"); break; }
                    if (st == DipStatus.Truce) { e.Possible = false; e.Blocker = Loc.T("Waffenruhe noch ") + (30 - (S.day - rel.since)) + Loc.T(" Tage"); break; }
                    e.Score = 100; e.Reasons.Clear();
                    if (st == DipStatus.Pact) e.Reasons.Add(Loc.T("Vertragsbruch: alle sind empört (-15)"));
                    if (st == DipStatus.Alliance) e.Reasons.Add(Loc.T("Verrat am Verbündeten: alle sind entsetzt (-30)"));
                    foreach (var a in AlliesOf(f.id)) if (!a.isPlayer) e.Reasons.Add(Loc.T("Verbündeter tritt bei: ") + a.name);
                    break;
                case DipAction.Peace:
                    if (st != DipStatus.War) { e.Possible = false; e.Blocker = Loc.T("Kein Krieg"); break; }
                    if (f.isEmperor && S.story.chapter >= 5) { e.Possible = false; e.Blocker = Loc.T("Der Kaiser will euren Kopf"); break; }
                    R(Loc.T("Kriegsmüdigkeit"), f.warWeariness * 0.8f);
                    R(Loc.T("Machtverhältnis"), Clamp((ratio - 1f) * 20, -30, 30));
                    R(Loc.T("Angriffslust"), -f.aggression * 20);
                    R(Loc.T("Kriegsdauer"), Math.Min(15, (S.day - rel.since) * 0.2f));
                    var cl = St(f.claim); if (cl != null && cl.faction == p.id) R(Loc.T("Gebietsanspruch"), -15);
                    break;
                case DipAction.Tribute:
                    if (st == DipStatus.War || st == DipStatus.Alliance) { e.Possible = false; e.Blocker = st == DipStatus.War ? Loc.T("Im Krieg") : Loc.T("Verbündete"); break; }
                    e.Cost = -(float)Math.Round(Math.Min(f.res[(int)Res.Gold] * 0.4f, 30 + TotalPop(f.id) * 0.5f));
                    R(Loc.T("Machtverhältnis"), Clamp((ratio - 2f) * 20, -40, 30));
                    R(Loc.T("Stolz"), -f.honor * 15 - f.aggression * 15);
                    break;
            }
            if (e.Possible && e.Cost > 0 && p.res[(int)Res.Gold] < e.Cost) { e.Possible = false; e.Blocker = Loc.T("Nicht genug Gold (") + (int)e.Cost + ")"; }
            return e;
        }

        public string Perform(DipAction act, FactionData f)
        {
            var p = Player;
            var e = Evaluate(act, f);
            if (!e.Possible) return e.Blocker;
            bool yes = e.Score >= 0 || (e.Score > -20 && Rng.NextDouble() < (e.Score + 20) / 40f);
            switch (act)
            {
                case DipAction.Gift:
                    p.res[(int)Res.Gold] -= e.Cost;
                    f.res[(int)Res.Gold] += e.Cost;
                    ModRel(p.id, f.id, 8 + f.greed * 10);
                    if (f.isEmperor) { ChangePressure(-12); return Loc.T("Der Kaiser nimmt das Geschenk an. Der Druck aus Valdoran lässt etwas nach."); }
                    return Loc.F("{0} nimmt das Geschenk dankend an.", (f.leader));
                case DipAction.Trade:
                    if (!yes) { ModRel(p.id, f.id, -2); return Loc.F("{0} lehnt ab: 'Wir brauchen eure Waren nicht.'", (f.leader)); }
                    SetTrade(p.id, f.id, true); ModRel(p.id, f.id, 5);
                    return Loc.T("Handelsabkommen geschlossen! (+Gold pro Tag)");
                case DipAction.Pact:
                    if (!yes) { ModRel(p.id, f.id, -3); return Loc.F("{0} lehnt ab: 'Wir trauen euch nicht.'", (f.leader)); }
                    SetStatus(p.id, f.id, DipStatus.Pact); ModRel(p.id, f.id, 8);
                    return Loc.T("Nichtangriffspakt geschlossen.");
                case DipAction.Alliance:
                    if (!yes) { ModRel(p.id, f.id, -4); return Loc.F("{0} lehnt ab: 'Ein Bündnis? Noch nicht.'", (f.leader)); }
                    p.res[(int)Res.Gold] -= e.Cost; f.res[(int)Res.Gold] += e.Cost;
                    SetStatus(p.id, f.id, DipStatus.Alliance); ModRel(p.id, f.id, 15);
                    foreach (var s in SettlementsOf(f.id)) Reveal(s.x, s.y, Radius(s) + 2);
                    OnAlliance(f);
                    return Loc.F("Bündnis mit {0} besiegelt!", (f.name));
                case DipAction.Annex:
                    if (!yes) { ModRel(p.id, f.id, -8); return Loc.F("{0} ist empört: 'Wir sind nicht käuflich!'", (f.leader)); }
                    p.res[(int)Res.Gold] -= e.Cost;
                    AnnexFaction(f);
                    return Loc.F("{0} schließt sich eurem Reich an!", (f.name));
                case DipAction.War:
                    DeclareWar(p.id, f.id);
                    return Loc.F("Ihr habt {0} den Krieg erklärt!", (f.name));
                case DipAction.Peace:
                    if (!yes) return Loc.F("{0}: 'Niemals! Wir kämpfen weiter.'", (f.leader));
                    MakePeace(p.id, f.id);
                    return Loc.F("Frieden mit {0}. 30 Tage Waffenruhe.", (f.name));
                case DipAction.Tribute:
                    if (!yes) { ModRel(p.id, f.id, -15); return Loc.F("{0}: 'Holt es euch doch!'", (f.leader)); }
                    float g = -e.Cost;
                    f.res[(int)Res.Gold] -= g; p.res[(int)Res.Gold] += g; ModRel(p.id, f.id, -20);
                    return Loc.F("{0} zahlt zähneknirschend {1} Gold.", (f.leader), ((int)g));
            }
            return "";
        }

        public void AnnexFaction(FactionData f)
        {
            var p = Player;
            foreach (var s in SettlementsOf(f.id).ToList())
            {
                TransferSettlement(s, p.id);
                s.originalFaction = p.id; // friedlich übernommen: kein Aufstandsrisiko
                s.happiness = Math.Max(s.happiness, 50);
            }
            foreach (var a in ArmiesOf(f.id).ToList()) { a.faction = p.id; SendHome(a); }
            for (int r = 0; r < ResInfo.Count; r++) p.res[r] += f.res[r];
            f.alive = false;
            p.prestige += 20;
            Notify(Loc.F("{0} ist nun Teil eures Reiches.", (f.name)), 1);
            OnAnnexed(f);
        }

        public void DeclareWar(int a, int b)
        {
            var fa = F(a); var fb = F(b);
            var prev = Status(a, b);
            if (prev == DipStatus.War) return;
            if (prev == DipStatus.Pact || prev == DipStatus.Alliance)
            {
                float pen = prev == DipStatus.Alliance ? 30 : 15;
                foreach (var o in S.factions) if (o.alive && o.id != a) ModRel(a, o.id, -pen * (0.4f + o.honor * 0.6f));
            }
            SetStatus(a, b, DipStatus.War);
            ModRel(a, b, -30);
            fa.warWeariness = Math.Max(0, fa.warWeariness - 5);
            // Verbündete des Angegriffenen treten bei
            foreach (var ally in AlliesOf(b).ToList())
            {
                if (ally.id == a || Status(ally.id, a) == DipStatus.War) continue;
                if (RelValue(ally.id, b) > 20 || ally.honor > 0.5f)
                {
                    SetStatus(ally.id, a, DipStatus.War);
                    if (ally.isPlayer) Notify(Loc.F("Bündnisfall! Ihr steht nun mit {0} im Krieg gegen {1}.", (fb.name), (fa.name)), 3);
                    else if (fa.isPlayer || fb.isPlayer) Notify(Loc.F("{0} tritt an der Seite von {1} in den Krieg ein.", (ally.name), (fb.name)), 3);
                }
            }
            if (fa.isPlayer || fb.isPlayer) Notify(Loc.F("KRIEG: {0} gegen {1}!", (fa.name), (fb.name)), 3);
            // Wer dem Kaiser selbst den Krieg erklärt, hat sich erhoben
            if (fa.isPlayer && fb.isEmperor && !S.story.Has("emperor_war")) StartEmperorWar();
            else if (fa.knownToPlayer != 0 || fb.knownToPlayer != 0) Notify(Loc.F("{0} erklärt {1} den Krieg.", (fa.name), (fb.name)), 0);
        }

        public void MakePeace(int a, int b)
        {
            SetStatus(a, b, DipStatus.Truce);
            ModRel(a, b, 10);
            F(a).warWeariness *= 0.5f; F(b).warWeariness *= 0.5f;
            // Truppen kehren heim
            foreach (var ar in S.armies.ToList())
            {
                if ((ar.faction == a || ar.faction == b) && (ar.mission == (int)ArmyMission.Attack || ar.mission == (int)ArmyMission.Raid))
                {
                    var t = St(ar.target);
                    if (t != null && (t.faction == a || t.faction == b) && t.faction != ar.faction) SendHome(ar);
                }
            }
            if (F(a).isPlayer || F(b).isPlayer) Notify(Loc.F("Frieden zwischen {0} und {1}.", (F(a).name), (F(b).name)), 1);
        }
    }
}
