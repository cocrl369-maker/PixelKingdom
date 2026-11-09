using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    public enum ProblemKind { NoWorkers, NoTrees, NoInput, Hunger, Unhappy, NoHousing, StorageFull, NoWell, Bandits, ForeignLand }

    /// <summary>Ein Problem an einem Ort (über einem Gebäude oder einem Ortszentrum).</summary>
    public struct Problem
    {
        public float X, Y;
        public ProblemKind Kind;
        public int Settlement;
        public string Detail;
    }

    /// <summary>
    /// Problem-Symbole: Läuft etwas nicht gut, erscheint über dem betroffenen Gebäude ein Zeichen
    /// (keine Arbeiter, kein Holz, Hunger, ...). Wird täglich für die Orte des Spielers neu ermittelt.
    /// </summary>
    public partial class Game
    {
        public readonly List<Problem> Problems = new List<Problem>();
        public int ProblemsVersion;
        readonly Dictionary<int, float[]> staffInfo = new Dictionary<int, float[]>();   // je Siedlung: [Typ*2] Personal, [Typ*2+1] Rohstoffe

        void RecordStaff(int sid, int type, float staff, float use)
        {
            if (!staffInfo.TryGetValue(sid, out var a)) staffInfo[sid] = a = new float[Buildings.Count * 2];
            a[type * 2] = staff; a[type * 2 + 1] = use;
        }

        public static string ProblemIcon(ProblemKind k)
        {
            switch (k)
            {
                case ProblemKind.NoWorkers: return "people";
                case ProblemKind.NoTrees: return "axe";
                case ProblemKind.NoInput: return "crate";
                case ProblemKind.Hunger: return "food";
                case ProblemKind.Unhappy: return "angry";
                case ProblemKind.NoHousing: return "house";
                case ProblemKind.StorageFull: return "crate";
                case ProblemKind.NoWell: return "well";
                case ProblemKind.Bandits: return "skull";
                case ProblemKind.ForeignLand: return "flag";
            }
            return "scroll";
        }

        public static string ProblemText(Problem p)
        {
            switch (p.Kind)
            {
                case ProblemKind.NoWorkers: return Loc.T("__Keine Arbeiter__\nEs fehlen Leute, das Gebäude arbeitet nur teilweise. Mehr Wohnhäuser bauen oder weniger Betriebe.");
                case ProblemKind.NoTrees: return Loc.T("__Kaum noch Bäume__\nDer Wald rund um den Holzfäller ist abgeholzt. Neuen Holzfäller im Wald bauen oder warten, bis er nachwächst.");
                case ProblemKind.NoInput: return Loc.F("__Rohstoff fehlt__\nDem Gebäude fehlt {0}. Mehr davon herstellen oder am Markt kaufen.", p.Detail);
                case ProblemKind.Hunger: return Loc.T("__Hunger__\nDie Speicher sind leer! Bauernhöfe oder Fischerhütten bauen, Rationen kürzen oder Nahrung kaufen.");
                case ProblemKind.Unhappy: return Loc.T("__Unzufriedenheit__\nDie Leute murren. Steuern senken, Taverne, Brunnen oder Kapelle bauen. Unter 15 drohen Aufstände.");
                case ProblemKind.NoHousing: return Loc.T("__Kein Wohnraum__\nDer Ort ist voll und kann nicht wachsen. Wohnhäuser bauen.");
                case ProblemKind.StorageFull: return Loc.F("__Lager voll__\n{0} kann nicht mehr eingelagert werden. Lagerhaus bauen oder am Markt verkaufen.", p.Detail);
                case ProblemKind.NoWell: return Loc.T("__Kein Brunnen__\nOhne sauberes Wasser drohen Krankheiten. Einen Brunnen bauen.");
                case ProblemKind.Bandits: return Loc.T("__Räuber in der Nähe__\nEin Räuberlager bedroht den Ort. Mit einem Heer ausheben.");
                case ProblemKind.ForeignLand: return Loc.T("__In fremdem Gebiet__\nEin Nachbar ist über diesen Außenposten gewachsen – er bringt kaum noch etwas.");
            }
            return "";
        }

        /// <summary>Täglich: Probleme der Orte des Spielers sammeln.</summary>
        void ComputeProblems()
        {
            Problems.Clear();
            var p = Player;
            if (p == null || !p.alive) return;
            var delta = Delta(p.id);
            float cap = Storage(p.id);
            var counts = new Dictionary<(int, ProblemKind), int>();
            void Add(SettlementData s, float x, float y, ProblemKind k, string detail = null, int max = 4)
            {
                var key = (s.id, k);
                counts.TryGetValue(key, out int n);
                if (n >= max) return;
                counts[key] = n + 1;
                Problems.Add(new Problem { X = x, Y = y, Kind = k, Settlement = s.id, Detail = detail });
            }
            var cap0 = Capital(p);
            foreach (var s in SettlementsOf(p.id))
            {
                float cx = s.x + 0.5f, cy = s.y + 2.2f;   // über dem Ortszentrum
                float off = 0;
                void Center(ProblemKind k, string d = null) { Add(s, cx + off, cy, k, d, 1); off += 0.9f; }
                if (p.res[(int)Res.Food] <= 1f && delta[(int)Res.Food] < 0) Center(ProblemKind.Hunger);
                if (s.happiness < 30) Center(ProblemKind.Unhappy);
                if (s.pop >= Housing(s) - 0.5f) Center(ProblemKind.NoHousing);
                if (s.buildings[(int)BType.Brunnen] == 0 && s.pop > 60 && !HasTech(p, Techs.Kraeuterkunde)) Center(ProblemKind.NoWell);
                if (NearBanditCamp(s)) Center(ProblemKind.Bandits);
                if (s == cap0)
                    for (int r = 0; r < ResInfo.Count; r++)
                        if (r != (int)Res.Gold && p.res[r] >= cap - 1 && delta[r] > 0) { Center(ProblemKind.StorageFull, ResInfo.Name(r)); break; }

                staffInfo.TryGetValue(s.id, out var info);
                foreach (var b in s.placed)
                {
                    var d = Buildings.Get(b.type);
                    float bx = b.x + 0.5f, by = b.y + 1.5f;
                    if (info != null && d.Jobs > 0 && info[b.type * 2] < 0.6f) { Add(s, bx, by, ProblemKind.NoWorkers); continue; }
                    if (info != null && info[b.type * 2 + 1] < 0.6f)
                    {
                        string need = null;
                        for (int r = 0; r < ResInfo.Count; r++) if (d.Use[r] > 0 && p.res[r] < d.Use[r] * 2) { need = ResInfo.Name(r); break; }
                        if (need != null) { Add(s, bx, by, ProblemKind.NoInput, need); continue; }
                    }
                    if (b.type == (int)BType.Holzfaeller && SiteBonus(BType.Holzfaeller, b.x, b.y) < 0.45f) { Add(s, bx, by, ProblemKind.NoTrees); continue; }
                    int own = Map.owner[WorldMap.Idx(b.x, b.y)];
                    if (own >= 0 && own != p.id && IsRemote((BType)b.type)) Add(s, bx, by, ProblemKind.ForeignLand);
                }
            }
            ProblemsVersion++;
        }
    }
}
