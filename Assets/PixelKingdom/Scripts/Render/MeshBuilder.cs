using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelKingdom
{
    /// <summary>
    /// Baut flach schattierte Low-Poly-Meshes. Das Licht wird direkt in die Vertexfarben "gebacken",
    /// dadurch funktioniert der Shader in jeder Render-Pipeline und sieht ruhig & grafisch aus.
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>(4096);
        public readonly List<Color32> C = new List<Color32>(4096);
        public readonly List<int> T = new List<int>(8192);

        public static readonly Vector3 Sun = new Vector3(-0.45f, 0.8f, -0.4f).normalized;
        Matrix4x4 m = Matrix4x4.identity;
        bool hasM;

        public void Clear() { V.Clear(); C.Clear(); T.Clear(); ResetTransform(); }

        public void SetTransform(Vector3 pos, float rotY = 0f, float scale = 1f)
        {
            m = Matrix4x4.TRS(pos, Quaternion.Euler(0, rotY, 0), Vector3.one * scale);
            hasM = true;
        }

        public void ResetTransform() { m = Matrix4x4.identity; hasM = false; }

        Vector3 P(Vector3 p) => hasM ? m.MultiplyPoint3x4(p) : p;

        public static Color Shade(Color col, Vector3 n)
        {
            float d = Mathf.Max(0f, Vector3.Dot(n, Sun));
            float s = 0.7f + 0.36f * d + 0.04f * n.y;
            return new Color(col.r * s, col.g * s, col.b * s, 1f);
        }

        /// <summary>Dreieck im Uhrzeigersinn (von außen gesehen).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col, bool shade = true)
        {
            a = P(a); b = P(b); c = P(c);
            var n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            Color32 cc = shade ? (Color32)Shade(col, n) : (Color32)col;
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            C.Add(cc); C.Add(cc); C.Add(cc);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        /// <summary>Nach oben zeigende Fläche – Reihenfolge der Ecken egal (wird bei Bedarf gedreht).</summary>
        public void QuadUp(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, bool shade = true)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.y < 0) Quad(a, d, c, b, col, shade); else Quad(a, b, c, d, col, shade);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, bool shade = true)
        {
            Tri(a, b, c, col, shade);
            Tri(a, c, d, col, shade);
        }

        /// <summary>Quader; Mittelpunkt unten (Boden) bei 'b'.</summary>
        public void Box(Vector3 b, float sx, float sy, float sz, Color col, bool bottom = false)
        {
            float x0 = b.x - sx / 2, x1 = b.x + sx / 2, z0 = b.z - sz / 2, z1 = b.z + sz / 2, y0 = b.y, y1 = b.y + sy;
            var p000 = new Vector3(x0, y0, z0); var p100 = new Vector3(x1, y0, z0);
            var p010 = new Vector3(x0, y1, z0); var p110 = new Vector3(x1, y1, z0);
            var p001 = new Vector3(x0, y0, z1); var p101 = new Vector3(x1, y0, z1);
            var p011 = new Vector3(x0, y1, z1); var p111 = new Vector3(x1, y1, z1);
            Quad(p010, p011, p111, p110, col);          // oben
            Quad(p000, p010, p110, p100, col);          // vorne (-z)
            Quad(p101, p111, p011, p001, col);          // hinten (+z)
            Quad(p001, p011, p010, p000, col);          // links (-x)
            Quad(p100, p110, p111, p101, col);          // rechts (+x)
            if (bottom) Quad(p000, p100, p101, p001, col);
        }

        /// <summary>Satteldach entlang der X-Achse. 'b' = Mitte der Traufe.</summary>
        public void GableRoof(Vector3 b, float sx, float sz, float h, Color col, float overhang = 0.03f)
        {
            float x0 = b.x - sx / 2 - overhang, x1 = b.x + sx / 2 + overhang;
            float z0 = b.z - sz / 2 - overhang, z1 = b.z + sz / 2 + overhang;
            float y = b.y, top = b.y + h;
            var a0 = new Vector3(x0, y, z0); var a1 = new Vector3(x1, y, z0);
            var b0 = new Vector3(x0, y, z1); var b1 = new Vector3(x1, y, z1);
            var r0 = new Vector3(x0, top, b.z); var r1 = new Vector3(x1, top, b.z);
            Quad(a0, r0, r1, a1, col);  // vordere Dachfläche
            Quad(b1, r1, r0, b0, col);  // hintere Dachfläche
            Tri(a0, b0, r0, col);       // Giebel links
            Tri(a1, r1, b1, col);       // Giebel rechts
        }

        /// <summary>Pyramide (z.B. Turmdach). 'b' = Mitte der Basis.</summary>
        public void Pyramid(Vector3 b, float sx, float sz, float h, Color col)
        {
            float x0 = b.x - sx / 2, x1 = b.x + sx / 2, z0 = b.z - sz / 2, z1 = b.z + sz / 2;
            var top = new Vector3(b.x, b.y + h, b.z);
            var p0 = new Vector3(x0, b.y, z0); var p1 = new Vector3(x1, b.y, z0);
            var p2 = new Vector3(x1, b.y, z1); var p3 = new Vector3(x0, b.y, z1);
            Tri(p0, top, p1, col); Tri(p1, top, p2, col); Tri(p2, top, p3, col); Tri(p3, top, p0, col);
        }

        public void Cone(Vector3 b, float r, float h, int sides, Color col, float rot = 0f)
        {
            var top = new Vector3(b.x, b.y + h, b.z);
            for (int i = 0; i < sides; i++)
            {
                float a0 = rot + i * Mathf.PI * 2 / sides, a1 = rot + (i + 1) * Mathf.PI * 2 / sides;
                var p0 = b + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r);
                var p1 = b + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                Tri(p1, p0, top, col);
            }
        }

        public void Cylinder(Vector3 b, float r, float h, int sides, Color col, bool cap = true)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                var o0 = new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r);
                var o1 = new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                var p0 = b + o0; var p1 = b + o1;
                var q0 = p0 + Vector3.up * h; var q1 = p1 + Vector3.up * h;
                Quad(p0, q0, q1, p1, col);
                if (cap) Tri(b + Vector3.up * h, q1, q0, col);
            }
        }

        /// <summary>Kleine Kugel (Oktaeder-artig, flach) – für Baumkronen.</summary>
        public void Blob(Vector3 c, float r, float h, Color col, float rot = 0f)
        {
            int sides = 5;
            var top = c + Vector3.up * h * 0.6f;
            var bot = c - Vector3.up * h * 0.4f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = rot + i * Mathf.PI * 2 / sides, a1 = rot + (i + 1) * Mathf.PI * 2 / sides;
                var p0 = c + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r);
                var p1 = c + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                Tri(p1, p0, top, col);
                Tri(p0, p1, bot, col);
            }
        }

        public Mesh ToMesh(Mesh mesh = null)
        {
            if (mesh == null) mesh = new Mesh();
            mesh.Clear();
            mesh.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(V);
            mesh.SetColors(C);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
