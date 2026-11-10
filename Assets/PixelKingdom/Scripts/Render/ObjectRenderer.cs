using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Natur-Objekte (Bäume, Stümpfe, Berge, Felsen, Bodenschätze) als Sprites mit Schatten, in Chunks.</summary>
    public class ObjectRenderer : MonoBehaviour
    {
        const int CS = GroundRenderer.CS;
        public const float ShadowZ = 19f;

        Game game;
        Material spriteMat, shadowMat;
        Mesh[] meshes, shadowMeshes;
        int cw, ch;
        readonly HashSet<int> dirty = new HashSet<int>();
        readonly QuadBatch qb = new QuadBatch(), sb = new QuadBatch();
        readonly List<Placed> tmp = new List<Placed>();
        HashSet<int> occupied = new HashSet<int>();

        public void Init(Game g, Material sprite, Material shadow)
        {
            game = g; spriteMat = sprite; shadowMat = shadow;
            foreach (Transform c in transform) Destroy(c.gameObject);
            cw = (WorldMap.W + CS - 1) / CS; ch = (WorldMap.H + CS - 1) / CS;
            meshes = new Mesh[cw * ch]; shadowMeshes = new Mesh[cw * ch];
            for (int i = 0; i < meshes.Length; i++)
            {
                QuadBatch.MakeObject("Natur " + i, transform, spriteMat, out meshes[i]);
                QuadBatch.MakeObject("Natur-Schatten " + i, transform, shadowMat, out shadowMeshes[i]);
                dirty.Add(i);
            }
        }

        public void MarkAll() { if (meshes != null) for (int i = 0; i < meshes.Length; i++) dirty.Add(i); }

        public void MarkTile(int tile)
        {
            int x = WorldMap.X(tile), y = WorldMap.Y(tile);
            dirty.Add((y / CS) * cw + x / CS);
        }

        /// <summary>Kacheln, auf denen Gebäude stehen (dort keine Bäume).</summary>
        public void SetOccupied(HashSet<int> occ)
        {
            foreach (int t in occ) if (!occupied.Contains(t)) MarkTile(t);
            foreach (int t in occupied) if (!occ.Contains(t)) MarkTile(t);
            occupied = new HashSet<int>(occ);
        }

        void Update()
        {
            if (game == null || SpriteLib.Atlas == null) return;
            spriteMat.mainTexture = SpriteLib.Atlas; shadowMat.mainTexture = SpriteLib.Atlas;
            int budget = 4;
            if (dirty.Count == 0) return;
            var list = new List<int>(dirty);
            foreach (int i in list)
            {
                if (budget-- <= 0) break;
                Build(i);
                dirty.Remove(i);
            }
        }

        public void BuildAll()
        {
            foreach (int i in dirty) Build(i);
            dirty.Clear();
        }

        void Build(int idx)
        {
            qb.Clear(); sb.Clear();
            int x0 = (idx % cw) * CS, y0 = (idx / cw) * CS;
            var white = new Color32(255, 255, 255, 255);
            for (int y = y0; y < Mathf.Min(WorldMap.H, y0 + CS); y++)
                for (int x = x0; x < Mathf.Min(WorldMap.W, x0 + CS); x++)
                {
                    int i = WorldMap.Idx(x, y);
                    if (occupied.Contains(i)) continue;
                    tmp.Clear();
                    ObjectPlacer.ForTile(game.Map, x, y, tmp);
                    foreach (var o in tmp)
                    {
                        var s = SpriteLib.Get(o.Name);
                        if (s == null) continue;
                        qb.Add(s, o.X, o.Y, o.Flat ? 19.5f : QuadBatch.DepthOf(o.Y), white, o.Flip);
                        if (s.Shadow != null) sb.Add(s.Shadow, o.X, o.Y, ShadowZ, white);
                    }
                }
            qb.ToMesh(meshes[idx]);
            sb.ToMesh(shadowMeshes[idx]);
        }
    }
}
