using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Verkehr als reine Darstellung: Fuhrwerke pendeln über die Straßen des Spielers, Handelsschiffe
    /// zwischen den Häfen, und die Seefahrer-Völker der großen Inseln haben ihre eigenen Flotten vor der Küste.
    /// Die Routen werden nach und nach berechnet (höchstens eine Wegsuche pro Bild).
    /// </summary>
    public class TrafficRenderer : MonoBehaviour
    {
        Game game;
        Mesh mesh, shadow;
        readonly QuadBatch qb = new QuadBatch(), sb = new QuadBatch();
        List<List<int>> carts = new List<List<int>>();
        readonly List<(List<int> path, int faction, string sprite)> ships = new List<(List<int>, int, string)>();
        readonly Queue<(int sx, int sy, int tx, int ty, int faction, string sprite)> pending = new Queue<(int, int, int, int, int, string)>();
        int roadsVersion = -1, shipsKey = -1;
        float rebuildTimer;

        public void Init(Game g, Material sprite, Material shadowMat)
        {
            game = g;
            foreach (Transform c in transform) Destroy(c.gameObject);
            QuadBatch.MakeObject("Verkehr", transform, sprite, out mesh);
            QuadBatch.MakeObject("Verkehr Schatten", transform, shadowMat, out shadow);
            carts.Clear(); ships.Clear(); pending.Clear();
            roadsVersion = -1; shipsKey = -1; rebuildTimer = 0;
        }

        void Update()
        {
            if (game == null || SpriteLib.Atlas == null || mesh == null) return;
            if (roadsVersion != game.RoadsVersion) { roadsVersion = game.RoadsVersion; carts = game.RoadRoutes(); if (carts.Count > 16) carts = carts.GetRange(carts.Count - 16, 16); }
            rebuildTimer -= Time.unscaledDeltaTime;
            if (rebuildTimer <= 0) { rebuildTimer = 20f; PlanShips(); }
            if (pending.Count > 0)
            {
                var p = pending.Dequeue();
                var path = Pathfinder.Find(game.Map, p.sx, p.sy, p.tx, p.ty, true, 120000);
                if (path != null && path.Count > 6) ships.Add((path, p.faction, p.sprite));
            }
            Draw();
        }

        /// <summary>Welche Schiffe fahren wohin? (Spieler: Handelsschiffe; Saelaner: eigene Flotten)</summary>
        void PlanShips()
        {
            int key = game.S.merchantShips * 1000 + game.Settlements.Count;
            foreach (var f in game.Factions) if (f.alive) key = key * 31 + f.id;
            if (key == shipsKey) return;
            shipsKey = key;
            ships.Clear(); pending.Clear();
            var rnd = new System.Random(game.S.seed + game.S.day / 30);
            // Häfen des Spielers -> Küstenorte von Handelspartnern oder eigene andere Häfen
            var myPorts = new List<SettlementData>();
            foreach (var s in game.SettlementsOf(game.PlayerId)) if (s.buildings[(int)BType.Hafen] > 0) myPorts.Add(s);
            var dests = new List<SettlementData>();
            foreach (var s in game.Settlements)
            {
                if (!Coastal(s)) continue;
                var f = game.F(s.faction);
                if (f == null) continue;
                if (f.isPlayer ? s.buildings[(int)BType.Hafen] > 0 : game.Rel(game.PlayerId, f.id).trade) dests.Add(s);
            }
            int n = Mathf.Min(6, game.S.merchantShips);
            for (int k = 0; k < n && myPorts.Count > 0 && dests.Count > 0; k++)
            {
                var a = myPorts[k % myPorts.Count]; var b = dests[rnd.Next(dests.Count)];
                if (a == b) continue;
                pending.Enqueue((a.x, a.y, b.x, b.y, game.PlayerId, game.ShipClass >= 3 ? "u_ship3" : game.ShipClass == 2 ? "u_ship2" : "u_ship"));
            }
            // Seefahrer-Völker: je Reich ein bis zwei Schiffe zwischen ihren Küstenorten oder zu Nachbarn
            foreach (var f in game.Factions)
            {
                if (!f.alive || !game.IsSeafarer(f)) continue;
                var own = new List<SettlementData>();
                foreach (var s in game.SettlementsOf(f.id)) if (Coastal(s)) own.Add(s);
                if (own.Count == 0) continue;
                for (int k = 0; k < 2; k++)
                {
                    var a = own[rnd.Next(own.Count)];
                    SettlementData b = null;
                    for (int t = 0; t < 10 && (b == null || b == a); t++)
                    {
                        var cand = game.Settlements[rnd.Next(game.Settlements.Count)];
                        if (Coastal(cand) && game.Map.cont[WorldMap.Idx(cand.x, cand.y)] == f.continent) b = cand;
                    }
                    if (b != null && b != a) pending.Enqueue((a.x, a.y, b.x, b.y, f.id, "u_ship2"));
                }
            }
        }

        bool Coastal(SettlementData s)
        {
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                    if (WorldMap.In(s.x + dx, s.y + dy) && game.Map.IsSea(WorldMap.Idx(s.x + dx, s.y + dy))) return true;
            return false;
        }

        static Vector2 Along(List<int> path, float t, out bool flip)
        {
            // t in [0,1): hin und zurück
            float u = t < 0.5f ? t * 2f : 2f - t * 2f;
            float f = u * (path.Count - 1);
            int i0 = Mathf.Clamp(Mathf.FloorToInt(f), 0, path.Count - 1), i1 = Mathf.Min(path.Count - 1, i0 + 1);
            float fr = f - i0;
            var a = new Vector2(WorldMap.X(path[i0]) + 0.5f, WorldMap.Y(path[i0]) + 0.5f);
            var b = new Vector2(WorldMap.X(path[i1]) + 0.5f, WorldMap.Y(path[i1]) + 0.5f);
            flip = (b.x - a.x) * (t < 0.5f ? 1 : -1) < 0;
            return Vector2.Lerp(a, b, fr);
        }

        void Draw()
        {
            qb.Clear(); sb.Clear();
            float time = Time.time;
            var white = new Color32(255, 255, 255, 255);
            var cart = SpriteLib.Get("u_cart");
            if (cart != null)
                for (int k = 0; k < carts.Count; k++)
                {
                    var path = carts[k];
                    float period = Mathf.Max(20f, path.Count * 2.4f);
                    var p = Along(path, ((time + k * 7.3f) / period) % 1f, out bool flip);
                    int ti = WorldMap.Idx((int)p.x, (int)p.y);
                    if (!game.Map.disc[ti] || game.Map.IsSea(ti)) continue;
                    qb.Add(cart, p.x, p.y - 0.3f, QuadBatch.DepthOf(p.y - 0.3f), white, flip);
                    if (cart.Shadow != null) sb.Add(cart.Shadow, p.x, p.y - 0.3f, ObjectRenderer.ShadowZ, white);
                }
            foreach (var sh in ships)
            {
                var spr = SpriteLib.Get(sh.sprite);
                if (spr == null) continue;
                float period = Mathf.Max(30f, sh.path.Count * 1.2f);
                var p = Along(sh.path, ((time + sh.path.Count * 3.1f) / period) % 1f, out bool flip);
                int ti = WorldMap.Idx((int)p.x, (int)p.y);
                if (!game.Map.disc[ti] || !game.Map.IsSea(ti)) continue;   // nur auf See und im erkundeten Gebiet
                var f = game.F(sh.faction);
                Color32 fc = f != null ? (Color32)Palette.FactionColor(f.color) : white;
                qb.Add(spr, p.x, p.y - 0.2f, QuadBatch.DepthOf(p.y - 0.2f), white, flip);
                if (spr.Overlay != null) qb.Add(spr.Overlay, p.x, p.y - 0.2f, QuadBatch.DepthOf(p.y - 0.2f) - 0.002f, fc, flip);
            }
            qb.ToMesh(mesh); sb.ToMesh(shadow);
        }
    }
}
