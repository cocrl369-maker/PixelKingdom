using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelKingdom
{
    /// <summary>Sammelt Sprite-Quads zu einem Mesh. 1 Welteinheit = 1 Kachel = 16 Texel.</summary>
    public class QuadBatch
    {
        public const float PPU = 16f;
        readonly List<Vector3> v = new List<Vector3>(4096);
        readonly List<Vector2> uv = new List<Vector2>(4096);
        readonly List<Color32> col = new List<Color32>(4096);
        readonly List<int> tri = new List<int>(6144);

        public int Count => v.Count / 4;
        public void Clear() { v.Clear(); uv.Clear(); col.Clear(); tri.Clear(); }

        /// <summary>Tiefe: weiter nördlich = weiter hinten.</summary>
        public static float DepthOf(float worldY) => 1f + worldY * 0.04f;

        public void Add(SpriteInfo s, float wx, float wy, float z, Color32 c, bool flip = false)
        {
            if (s == null) return;
            int pvx = flip ? s.W - s.PivotX : s.PivotX;
            float x0 = Mathf.Round(wx * PPU - pvx) / PPU, y0 = Mathf.Round(wy * PPU - s.PivotY) / PPU;
            float x1 = x0 + s.W / PPU, y1 = y0 + s.H / PPU;
            int i = v.Count;
            v.Add(new Vector3(x0, y0, z)); v.Add(new Vector3(x0, y1, z)); v.Add(new Vector3(x1, y1, z)); v.Add(new Vector3(x1, y0, z));
            var r = s.UV;
            if (!flip) { uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMin, r.yMax)); uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMax, r.yMin)); }
            else { uv.Add(new Vector2(r.xMax, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMin, r.yMax)); uv.Add(new Vector2(r.xMin, r.yMin)); }
            col.Add(c); col.Add(c); col.Add(c); col.Add(c);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        /// <summary>Einfarbiges Rechteck (nutzt das weiße Pixel im Atlas).</summary>
        public void Rect(SpriteInfo white, float x0, float y0, float x1, float y1, float z, Color32 c)
        {
            int i = v.Count;
            v.Add(new Vector3(x0, y0, z)); v.Add(new Vector3(x0, y1, z)); v.Add(new Vector3(x1, y1, z)); v.Add(new Vector3(x1, y0, z));
            var m = white.UV.center;
            uv.Add(m); uv.Add(m); uv.Add(m); uv.Add(m);
            col.Add(c); col.Add(c); col.Add(c); col.Add(c);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        public Mesh ToMesh(Mesh m)
        {
            if (m == null) m = new Mesh();
            m.Clear();
            m.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetColors(col);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        public static GameObject MakeObject(string name, Transform parent, Material mat, out Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }
    }
}
