using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Zeichnet den Kartenboden als Pixel-Texturen in 32x32-Kachel-Chunks.
    /// Gemalt wird im Hintergrund (Threads), nur Chunks mit erforschtem Land.
    /// Nebel des Krieges und Gebietstönung kommen aus zwei kleinen globalen Texturen.
    /// </summary>
    public class GroundRenderer : MonoBehaviour
    {
        public const int CS = 32;
        public const float Z = 20f;

        class Chunk
        {
            public int cx, cy, tw, th;
            public MeshRenderer mr;
            public Texture2D tex;
            public bool hasLand, wanted;
            public int paintedPalette = -1;
            public Task<Px[]> task;
            public int taskPalette;
        }

        Game game;
        Material mat;
        GroundPainter painter;
        int painterPalette = -1;
        Chunk[] chunks;
        int cw, ch;
        Texture2D fogTex, ownerTex;
        Color32[] fogPx, ownerPx;
        MaterialPropertyBlock mpb;
        bool fogDirty = true;
        int running;

        public bool FogEnabled = true;

        public void Init(Game g, Material m)
        {
            game = g; mat = m;
            foreach (Transform c in transform) Destroy(c.gameObject);
            if (chunks != null) foreach (var c in chunks) if (c.tex != null) Destroy(c.tex);
            mpb = new MaterialPropertyBlock();
            cw = (WorldMap.W + CS - 1) / CS; ch = (WorldMap.H + CS - 1) / CS;
            chunks = new Chunk[cw * ch];
            painter = null; painterPalette = -1;
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    var c = new Chunk { cx = x, cy = y, tw = Mathf.Min(CS, WorldMap.W - x * CS), th = Mathf.Min(CS, WorldMap.H - y * CS) };
                    var go = new GameObject($"Boden {x},{y}");
                    go.transform.SetParent(transform, false);
                    float x0 = x * CS, y0 = y * CS, x1 = x0 + c.tw, y1 = y0 + c.th;
                    var mesh = new Mesh { name = go.name };
                    mesh.SetVertices(new List<Vector3> { new Vector3(x0, y0, Z), new Vector3(x0, y1, Z), new Vector3(x1, y1, Z), new Vector3(x1, y0, Z) });
                    mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
                    mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                    mesh.RecalculateBounds();
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    c.mr = go.AddComponent<MeshRenderer>();
                    c.mr.sharedMaterial = mat;
                    c.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    c.mr.receiveShadows = false;
                    mpb.SetTexture("_MainTex", Placeholder());
                    c.mr.SetPropertyBlock(mpb);
                    for (int ty = (int)y0; ty < y1 && !c.hasLand; ty++)
                        for (int tx = (int)x0; tx < x1; tx++) if (!g.Map.IsSea(WorldMap.Idx(tx, ty)) || g.Map.disc[WorldMap.Idx(tx, ty)]) { c.hasLand = true; break; }
                    chunks[y * cw + x] = c;
                }

            int W = WorldMap.W, H = WorldMap.H;
            if (fogTex == null)
            {
                fogTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "PKFog" };
                ownerTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PKOwner" };
                fogPx = new Color32[W * H]; ownerPx = new Color32[W * H];
            }
            Shader.SetGlobalTexture("_PKFogTex", fogTex);
            Shader.SetGlobalTexture("_PKOwnerTex", ownerTex);
            Shader.SetGlobalVector("_PKMapSize", new Vector4(W, H, 1f / W, 1f / H));
            ApplyPaletteGlobals();
            fogDirty = true;
            RefreshMaps();
        }

        Texture2D placeholder;
        Texture2D Placeholder()
        {
            if (placeholder == null) placeholder = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            placeholder.SetPixel(0, 0, Palette.ToColor(Palette.Art.Grass));
            placeholder.Apply();
            return placeholder;
        }

        public void ApplyPaletteGlobals()
        {
            var a = Palette.Art;
            Shader.SetGlobalColor("_PKParchA", Palette.ToColor(a.ParchA));
            Shader.SetGlobalColor("_PKParchB", Palette.ToColor(a.ParchB));
            Shader.SetGlobalColor("_PKInk", Palette.ToColor(a.Ink));
            Shader.SetGlobalFloat("_PKFogOn", FogEnabled ? 1f : 0f);
        }

        public void PaletteChanged()
        {
            painter = null; painterPalette = -1;
            ApplyPaletteGlobals();
            Placeholder();
        }

        /// <summary>Wie viele Chunks noch gemalt werden.</summary>
        public int Pending
        {
            get { int n = 0; if (chunks != null) foreach (var c in chunks) if (c.wanted && c.paintedPalette != Palette.Index) n++; return n; }
        }

        void RefreshMaps()
        {
            var map = game.Map;
            int N = WorldMap.W * WorldMap.H;
            for (int i = 0; i < N; i++)
            {
                bool d = map.disc[i];
                fogPx[i] = d ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
                int o = map.owner[i];
                if (o >= 0 && d)
                {
                    var f = game.F(o);
                    var col = f != null ? (Color32)Palette.FactionColor(f.color) : new Color32(255, 255, 255, 255);
                    col.a = 255;
                    ownerPx[i] = col;
                }
                else ownerPx[i] = new Color32(0, 0, 0, 0);
            }
            fogTex.SetPixels32(fogPx); fogTex.Apply(false);
            ownerTex.SetPixels32(ownerPx); ownerTex.Apply(false);
            // Welche Chunks werden gebraucht?
            foreach (var c in chunks)
            {
                if (c.wanted) continue;
                for (int ty = c.cy * CS; ty < c.cy * CS + c.th && !c.wanted; ty++)
                    for (int tx = c.cx * CS; tx < c.cx * CS + c.tw; tx++)
                        if (map.disc[WorldMap.Idx(tx, ty)]) { c.wanted = true; break; }
            }
            fogDirty = false;
        }

        public void MarkFogDirty() => fogDirty = true;

        void Update()
        {
            if (game == null) return;
            if (fogDirty) RefreshMaps();
            Shader.SetGlobalFloat("_PKFogOn", FogEnabled ? 1f : 0f);

            if (painter == null || painterPalette != Palette.Index)
            {
                painterPalette = Palette.Index;
                painter = new GroundPainter(game.Map, Palette.Art);
            }

            // Fertige Aufträge übernehmen
            foreach (var c in chunks)
            {
                if (c.task == null || !c.task.IsCompleted) continue;
                var t = c.task; c.task = null; running--;
                if (t.IsFaulted || t.Result == null) { Debug.LogWarning("PixelKingdom: Boden konnte nicht gemalt werden: " + t.Exception?.Message); continue; }
                if (c.taskPalette != Palette.Index) continue; // veraltet
                int pw = c.tw * GroundPainter.T, ph = c.th * GroundPainter.T;
                if (c.tex == null || c.tex.width != pw || c.tex.height != ph)
                {
                    if (c.tex != null) Destroy(c.tex);
                    c.tex = new Texture2D(pw, ph, TextureFormat.RGBA32, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = c.mr.name };
                }
                c.tex.SetPixelData(t.Result, 0);
                c.tex.Apply(true, false);
                c.paintedPalette = c.taskPalette;
                mpb.SetTexture("_MainTex", c.tex);
                c.mr.SetPropertyBlock(mpb);
            }

            // Neue Aufträge vergeben (nächstgelegene zuerst)
            int maxParallel = Mathf.Max(1, System.Environment.ProcessorCount - 1);
            if (running >= maxParallel) return;
            var cam = GameManager.I != null ? GameManager.I.CamCtrl : null;
            Vector2 focus = cam != null ? cam.Target : new Vector2(WorldMap.W / 2f, WorldMap.H / 2f);
            Chunk best = null; float bd = float.MaxValue;
            foreach (var c in chunks)
            {
                if (!c.wanted || c.task != null || c.paintedPalette == Palette.Index) continue;
                float dx = (c.cx + 0.5f) * CS - focus.x, dy = (c.cy + 0.5f) * CS - focus.y;
                float d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = c; }
            }
            if (best == null) return;
            var p = painter;
            int x0 = best.cx * CS, y0 = best.cy * CS, w = best.tw, h = best.th;
            best.taskPalette = Palette.Index;
            best.task = Task.Run(() =>
            {
                var px = new Px[w * GroundPainter.T * h * GroundPainter.T];
                p.Paint(x0, y0, w, h, px);
                return px;
            });
            running++;
        }

        /// <summary>Malt sofort (blockierend) alle benötigten Chunks in der Nähe – z.B. beim Laden.</summary>
        public void PaintNearNow(Vector2 center, int radiusChunks)
        {
            if (painter == null || painterPalette != Palette.Index) { painterPalette = Palette.Index; painter = new GroundPainter(game.Map, Palette.Art); }
            int ccx = (int)(center.x / CS), ccy = (int)(center.y / CS);
            var todo = new List<Chunk>();
            foreach (var c in chunks)
                if (c.wanted && c.task == null && c.paintedPalette != Palette.Index && Mathf.Abs(c.cx - ccx) <= radiusChunks && Mathf.Abs(c.cy - ccy) <= radiusChunks) todo.Add(c);
            var p = painter;
            foreach (var c in todo)
            {
                int x0 = c.cx * CS, y0 = c.cy * CS, w = c.tw, h = c.th;
                c.taskPalette = Palette.Index;
                c.task = Task.Run(() => { var px = new Px[w * GroundPainter.T * h * GroundPainter.T]; p.Paint(x0, y0, w, h, px); return px; });
                running++;
            }
            foreach (var c in todo) c.task.Wait();
            Update();
        }

        void OnDestroy()
        {
            if (chunks != null) foreach (var c in chunks) if (c.tex != null) Destroy(c.tex);
        }
    }
}
