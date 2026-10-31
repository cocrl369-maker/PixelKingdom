using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Zeichnet die Welt in Chunks: Low-Poly-Höhenfeld, Wasser, Flüsse, Bäume, Grenzen und Nebel des Krieges.
    /// Unerforschtes Land bleibt flaches "Pergament" – beim Erkunden wächst die 3D-Welt daraus hervor.
    /// </summary>
    public class TerrainRenderer : MonoBehaviour
    {
        public const int ChunkSize = 32;
        Game game;
        Material mat;
        MeshFilter[] filters;
        Mesh[] meshes;
        int cw, ch;
        readonly HashSet<int> dirty = new HashSet<int>();
        readonly MeshBuilder mb = new MeshBuilder();
        readonly HashSet<int> clearTiles = new HashSet<int>();
        float[] tileH; // Höhe pro Kachel für die Ecken-Mittelung

        public void Init(Game g, Material material)
        {
            game = g; mat = material;
            foreach (Transform c in transform) Destroy(c.gameObject);
            cw = (WorldMap.W + ChunkSize - 1) / ChunkSize;
            ch = (WorldMap.H + ChunkSize - 1) / ChunkSize;
            filters = new MeshFilter[cw * ch];
            meshes = new Mesh[cw * ch];
            var map = g.Map;
            tileH = new float[WorldMap.W * WorldMap.H];
            for (int i = 0; i < tileH.Length; i++)
            {
                if (map.IsSea(i)) tileH[i] = -0.7f;
                else if (map.biome[i] == Biome.Lake) tileH[i] = WorldMap.LandY(map.h[i]) - 0.4f;
                else if (map.river[i]) tileH[i] = WorldMap.LandY(map.h[i]) - 0.4f;
                else tileH[i] = WorldMap.LandY(map.h[i]);
            }
            for (int cy = 0; cy < ch; cy++)
                for (int cx = 0; cx < cw; cx++)
                {
                    var go = new GameObject($"Chunk {cx},{cy}");
                    go.transform.SetParent(transform, false);
                    var mf = go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    int idx = cy * cw + cx;
                    filters[idx] = mf;
                    meshes[idx] = new Mesh { name = go.name };
                    mf.sharedMesh = meshes[idx];
                    dirty.Add(idx);
                }
            RefreshClearTiles();
        }

        public void MarkAllDirty()
        {
            if (filters == null) return;
            for (int i = 0; i < filters.Length; i++) dirty.Add(i);
        }

        public int PendingChunks => dirty.Count;

        /// <summary>Baut alle ausstehenden Chunks sofort (z.B. beim Laden).</summary>
        public void BuildAll()
        {
            ConsumeGameDirty();
            foreach (var i in dirty) BuildChunk(i);
            dirty.Clear();
        }

        void Update()
        {
            if (game == null) return;
            ConsumeGameDirty();
            int budget = 3;
            if (dirty.Count > 0)
            {
                var list = new List<int>(dirty);
                foreach (var i in list)
                {
                    if (budget-- <= 0) break;
                    BuildChunk(i);
                    dirty.Remove(i);
                }
            }
        }

        void ConsumeGameDirty()
        {
            if (game.SettlementsDirty || game.DirtyTiles.Count > 0) RefreshClearTiles();
            if (game.DirtyTiles.Count == 0) return;
            foreach (int t in game.DirtyTiles)
            {
                int x = WorldMap.X(t), y = WorldMap.Y(t);
                // Nachbar-Chunks mitnehmen (Ecken/Grenzen liegen auf Kanten)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (!WorldMap.In(nx, ny)) continue;
                        dirty.Add((ny / ChunkSize) * cw + nx / ChunkSize);
                    }
            }
            game.DirtyTiles.Clear();
        }

        void RefreshClearTiles()
        {
            clearTiles.Clear();
            foreach (var s in game.Settlements)
                for (int y = s.y - 3; y <= s.y + 3; y++)
                    for (int x = s.x - 3; x <= s.x + 3; x++)
                        if (WorldMap.In(x, y) && (x - s.x) * (x - s.x) + (y - s.y) * (y - s.y) <= 7) clearTiles.Add(WorldMap.Idx(x, y));
        }

        bool Disc(int x, int y) => WorldMap.In(x, y) && game.Map.disc[WorldMap.Idx(x, y)];

        public float CornerY(int cx, int cy)
        {
            float sum = 0; int n = 0; bool anyLand = false, anyDisc = false;
            for (int dy = -1; dy <= 0; dy++)
                for (int dx = -1; dx <= 0; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!WorldMap.In(x, y)) continue;
                    int i = WorldMap.Idx(x, y);
                    sum += tileH[i]; n++;
                    if (!game.Map.IsSea(i)) anyLand = true;
                    if (game.Map.disc[i]) anyDisc = true;
                }
            if (!anyDisc) return 0f;
            float v = n > 0 ? sum / n : 0f;
            if (anyLand) v = Mathf.Max(v, 0.03f);
            return v;
        }

        /// <summary>Höhe der gezeichneten Oberfläche an einer Weltposition (bilinear über die Ecken).</summary>
        public float GroundY(float wx, float wz)
        {
            int x = Mathf.FloorToInt(wx), y = Mathf.FloorToInt(wz);
            if (!WorldMap.In(x, y)) return 0f;
            float fx = wx - x, fy = wz - y;
            float h = Mathf.Lerp(Mathf.Lerp(CornerY(x, y), CornerY(x + 1, y), fx), Mathf.Lerp(CornerY(x, y + 1), CornerY(x + 1, y + 1), fx), fy);
            int i = WorldMap.Idx(x, y);
            if (game.Map.IsSea(i) && game.Map.disc[i]) h = Mathf.Max(h, 0f);
            return h;
        }

        public static float Hash(int x, int y, int k)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + k * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }

        static Color Vary(Color c, float t, float amt = 0.035f)
        {
            float f = 1f + (t - 0.5f) * 2f * amt;
            return new Color(c.r * f, c.g * f, c.b * f, 1f);
        }

        void BuildChunk(int idx)
        {
            var pal = Palette.Current;
            var map = game.Map;
            int cx0 = (idx % cw) * ChunkSize, cy0 = (idx / cw) * ChunkSize;
            int cx1 = Mathf.Min(WorldMap.W, cx0 + ChunkSize), cy1 = Mathf.Min(WorldMap.H, cy0 + ChunkSize);
            mb.Clear();

            // Ecken-Höhen des Chunks vorberechnen
            int cwid = cx1 - cx0 + 1;
            var corners = new float[(cx1 - cx0 + 1) * (cy1 - cy0 + 1)];
            for (int y = cy0; y <= cy1; y++)
                for (int x = cx0; x <= cx1; x++)
                    corners[(y - cy0) * cwid + (x - cx0)] = CornerY(x, y);
            float CY(int x, int y) => corners[(y - cy0) * cwid + (x - cx0)];

            var factions = game.Factions;
            for (int y = cy0; y < cy1; y++)
                for (int x = cx0; x < cx1; x++)
                {
                    int i = WorldMap.Idx(x, y);
                    bool disc = map.disc[i];
                    float h00 = CY(x, y), h10 = CY(x + 1, y), h01 = CY(x, y + 1), h11 = CY(x + 1, y + 1);
                    var p00 = new Vector3(x, h00, y); var p10 = new Vector3(x + 1, h10, y);
                    var p01 = new Vector3(x, h01, y + 1); var p11 = new Vector3(x + 1, h11, y + 1);
                    float rnd = Hash(x, y, 0);

                    if (!disc)
                    {
                        var fc = Vary(map.IsSea(i) ? pal.FogSea : pal.Fog, rnd, 0.015f);
                        TileQuad(p00, p10, p01, p11, fc, x, y);
                        continue;
                    }

                    bool sea = map.IsSea(i);
                    bool fresh = map.biome[i] == Biome.Lake || map.river[i];
                    if (sea)
                    {
                        var wc = map.biome[i] == Biome.Shallow ? pal.Shallow : pal.Ocean;
                        wc = Vary(wc, rnd, 0.015f);
                        mb.Quad(new Vector3(x, 0, y), new Vector3(x, 0, y + 1), new Vector3(x + 1, 0, y + 1), new Vector3(x + 1, 0, y), wc, false);
                        continue;
                    }
                    if (fresh)
                    {
                        float wl = Mathf.Min(Mathf.Min(h00, h10), Mathf.Min(h01, h11)) + 0.02f;
                        var wc = Vary(pal.River, rnd, 0.015f);
                        mb.Quad(new Vector3(x, wl, y), new Vector3(x, wl, y + 1), new Vector3(x + 1, wl, y + 1), new Vector3(x + 1, wl, y), wc, false);
                        var bank = pal.Bank;
                        Wall(p00, p01, wl, bank); Wall(p01, p11, wl, bank); Wall(p11, p10, wl, bank); Wall(p10, p00, wl, bank);
                        continue;
                    }

                    var col = Vary(pal.BiomeColor(map.biome[i]), rnd);
                    // Gebiets-Tönung
                    int own = map.owner[i];
                    Color fcol = Color.white;
                    if (own >= 0)
                    {
                        var f = game.F(own);
                        if (f != null) { fcol = pal.FactionColor(f.color); col = Color.Lerp(col, fcol, 0.14f); }
                    }
                    TileQuad(p00, p10, p01, p11, col, x, y);

                    // Grenzlinien
                    if (own >= 0)
                    {
                        Vector3 c = (p00 + p10 + p01 + p11) * 0.25f;
                        if (Owner(x - 1, y) != own) Border(p00, p01, c, fcol);
                        if (Owner(x + 1, y) != own) Border(p10, p11, c, fcol);
                        if (Owner(x, y - 1) != own) Border(p00, p10, c, fcol);
                        if (Owner(x, y + 1) != own) Border(p01, p11, c, fcol);
                    }

                    // Bäume
                    int nTrees = map.trees[i];
                    if (nTrees > 0 && !clearTiles.Contains(i))
                    {
                        bool pine = map.biome[i] == Biome.Pine || map.biome[i] == Biome.Snow;
                        for (int k = 0; k < nTrees; k++)
                        {
                            float tx = 0.2f + Hash(x, y, k * 2 + 1) * 0.6f, tz = 0.2f + Hash(x, y, k * 2 + 2) * 0.6f;
                            float hy = Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
                            var pos = new Vector3(x + tx, hy, y + tz);
                            float s = 0.75f + Hash(x, y, k + 9) * 0.5f;
                            if (pine) Pine(pos, s, pal);
                            else Tree(pos, s, pal, Hash(x, y, k + 20));
                        }
                    }
                    // Steine im Gebirge
                    if (map.biome[i] == Biome.Mountain && rnd < 0.25f)
                    {
                        float hy = (h00 + h10 + h01 + h11) * 0.25f;
                        mb.Pyramid(new Vector3(x + 0.5f, hy - 0.02f, y + 0.5f), 0.3f, 0.26f, 0.22f, Vary(pal.Mountain, 0.2f, 0.1f));
                    }
                }

            mb.ToMesh(meshes[idx]);
        }

        int Owner(int x, int y) => WorldMap.In(x, y) ? game.Map.owner[WorldMap.Idx(x, y)] : -1;

        void TileQuad(Vector3 p00, Vector3 p10, Vector3 p01, Vector3 p11, Color col, int x, int y)
        {
            // Diagonale wechselnd für ein natürlicheres Low-Poly-Muster
            if (((x + y) & 1) == 0)
            {
                mb.Tri(p00, p01, p11, col);
                mb.Tri(p00, p11, p10, col);
            }
            else
            {
                mb.Tri(p00, p01, p10, col);
                mb.Tri(p01, p11, p10, col);
            }
        }

        void Wall(Vector3 a, Vector3 b, float wl, Color col)
        {
            if (a.y <= wl + 0.001f && b.y <= wl + 0.001f) return;
            var a2 = new Vector3(a.x, wl, a.z); var b2 = new Vector3(b.x, wl, b.z);
            // nach innen (zur Kachelmitte) zeigend – von der Wasserseite aus sichtbar
            mb.Quad(a, b, b2, a2, col);
            mb.Quad(a, a2, b2, b, col);
        }

        void Border(Vector3 a, Vector3 b, Vector3 center, Color col)
        {
            const float w = 0.13f;
            var ai = Vector3.Lerp(a, center, w * 2f); var bi = Vector3.Lerp(b, center, w * 2f);
            var up = Vector3.up * 0.025f;
            mb.QuadUp(a + up, b + up, bi + up, ai + up, col, false);
        }

        void Tree(Vector3 p, float s, Palette pal, float v)
        {
            mb.SetTransform(p, v * 360f, s);
            mb.Cylinder(Vector3.zero, 0.025f, 0.12f, 3, pal.Trunk, false);
            mb.Blob(new Vector3(0, 0.22f, 0), 0.13f, 0.26f, Vary(pal.Tree, v, 0.06f), v * 3f);
            mb.ResetTransform();
        }

        void Pine(Vector3 p, float s, Palette pal)
        {
            mb.SetTransform(p, 0, s);
            mb.Cone(new Vector3(0, 0.04f, 0), 0.14f, 0.24f, 5, pal.PineTree);
            mb.Cone(new Vector3(0, 0.17f, 0), 0.10f, 0.22f, 5, pal.PineTree, 0.6f);
            mb.ResetTransform();
        }
    }
}
