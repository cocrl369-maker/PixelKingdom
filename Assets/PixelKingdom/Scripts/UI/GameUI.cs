using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Pixel-UI im Stil des Vorbilds (Terrakotta & Sand), gezeichnet mit IMGUI und eigener Bitmapschrift.
    /// Alles wird in einem virtuellen Raster mit ganzzahliger Skalierung gezeichnet.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        public enum Window { None, Menu, Diplomacy, Realm, Log, Help, ConfirmWar }

        GameManager gm;
        Game G => gm.Game;

        int S = 2;                   // Pixelgröße
        float VW, VH;                // virtuelle Bildschirmgröße
        readonly List<Rect> rects = new List<Rect>();
        List<Rect> lastRects = new List<Rect>();
        bool modalThisFrame, modalLastFrame;
        string tooltip; Vector2 tipPos;
        public Window Open = Window.None;
        public int SettlementTab;

        struct Toast_ { public string text; public int kind; public float t; }
        readonly List<Toast_> toasts = new List<Toast_>();
        BattleReport report;
        string offlineReport;
        string dipMessage;
        int dipSel = -1;
        float scrollLog, scrollDip, scrollRealm, scrollCards;
        bool questOpen = true;

        // Krieg-Bestätigung
        FactionData warTarget; ArmyData warArmy; SettlementData warSettlement; bool warRaid;

        Texture2D minimap; float minimapTimer; Color32[] minimapPx;

        void Awake() { gm = GetComponent<GameManager>(); PixelFont.Init(); }

        // ------------------------------------------------------------------
        //  Zustand für den GameManager
        // ------------------------------------------------------------------
        public bool MouseOverUI
        {
            get
            {
                if (gm == null || gm.InMenu || modalLastFrame) return true;
                var mp = PKInput.MousePos;
                var v = new Vector2(mp.x / S, (Screen.height - mp.y) / S);
                foreach (var r in lastRects) if (r.Contains(v)) return true;
                return false;
            }
        }

        public bool IsBlocking => gm.InMenu || gm.GameOver || (G != null && (G.Dialog.Count > 0 || G.CurrentEvent != null)) || report != null || offlineReport != null
                                   || Open == Window.Menu || Open == Window.ConfirmWar;
        public bool KeyboardCaptured => false;

        public void Toast(string text, int kind = 0) => toasts.Add(new Toast_ { text = text, kind = kind, t = Time.unscaledTime });

        public void OnGameStarted(string offline)
        {
            offlineReport = offline;
            toasts.Clear(); Open = Window.None; report = null; dipSel = -1;
            minimapTimer = 0;
        }

        public void OpenWindow(Window w) { Open = w; dipMessage = null; }
        public void ToggleWindow(Window w) { Open = Open == w ? Window.None : w; dipMessage = null; }

        public bool CloseTopWindow()
        {
            if (Open != Window.None) { Open = Window.None; return true; }
            return false;
        }

        public void AskWar(FactionData f, ArmyData a, SettlementData s, bool raid)
        {
            warTarget = f; warArmy = a; warSettlement = s; warRaid = raid;
            Open = Window.ConfirmWar;
        }

        void Update()
        {
            if (G == null) return;
            while (G.Notices.Count > 0) { var n = G.Notices.Dequeue(); Toast(n.Text, n.Kind); }
            if (report == null && G.Reports.Count > 0) report = G.Reports.Dequeue();
            if (toasts.Count > 6) toasts.RemoveRange(0, toasts.Count - 6);
            toasts.RemoveAll(t => Time.unscaledTime - t.t > 7f);
            // Dialog weiterklicken mit Leertaste
            if (G.Dialog.Count > 0 && (PKInput.KeyDown(PKKey.Space))) G.Dialog.Dequeue();
        }

        // ------------------------------------------------------------------
        //  Zeichnen
        // ------------------------------------------------------------------
        void OnGUI()
        {
            if (gm == null || G == null) return;
            int extra = PlayerPrefs.GetInt("pk_uiscale", 0);
            S = Mathf.Clamp(Mathf.FloorToInt(Screen.height / 380f) + extra, 1, 8);
            VW = Screen.width / (float)S; VH = Screen.height / (float)S;
            GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1));
            if (Event.current.type == EventType.Layout) { }
            if (Event.current.type == EventType.Repaint) { rects.Clear(); modalThisFrame = false; tooltip = null; }

            if (gm.InMenu) { DrawMainMenu(); FinishFrame(); return; }

            DrawWorldLabels();
            DrawTopBar();
            DrawSideButtons();
            DrawMinimap();
            DrawQuest();
            DrawToasts();
            DrawSelectionPanel();
            DrawModeHint();

            switch (Open)
            {
                case Window.Diplomacy: DrawDiplomacy(); break;
                case Window.Realm: DrawRealm(); break;
                case Window.Log: DrawLog(); break;
                case Window.Help: DrawHelp(); break;
                case Window.Menu: DrawMenu(); break;
                case Window.ConfirmWar: DrawConfirmWar(); break;
            }

            if (gm.GameOver) DrawGameOver();
            else if (offlineReport != null) DrawOffline();
            else if (report != null) DrawReport();
            else if (G.Dialog.Count > 0) DrawDialog();
            else if (G.CurrentEvent != null) DrawEvent();

            if (gm.Paused && !IsBlocking) Centered(VH * 0.18f, "- PAUSE -", Palette.UIPanelDark, 2);
            FinishFrame();
        }

        void FinishFrame()
        {
            if (tooltip != null) DrawTooltip();
            if (Event.current.type == EventType.Repaint)
            {
                lastRects = new List<Rect>(rects);
                modalLastFrame = modalThisFrame;
            }
        }

        // ------------------------------------------------------------------
        //  Bausteine
        // ------------------------------------------------------------------
        static void Fill(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Panel(Rect r, Color? col = null, bool register = true)
        {
            if (register) rects.Add(r);
            Fill(new Rect(r.x + 2, r.y + 2, r.width, r.height), Palette.UIShadow);
            Fill(r, Palette.UIPanelDark);
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), col ?? Palette.UIPanel);
        }

        void Modal()
        {
            modalThisFrame = true;
            rects.Add(new Rect(0, 0, VW, VH));
            Fill(new Rect(0, 0, VW, VH), new Color(0.2f, 0.08f, 0.04f, 0.35f));
        }

        static void Text(float x, float y, string s, Color? c = null, int scale = 1) => PixelFont.Draw(Mathf.Round(x), Mathf.Round(y), s, c ?? Palette.UIText, scale);
        static float Wrapped(Rect r, string s, Color? c = null, TextAnchor a = TextAnchor.UpperLeft) => PixelFont.DrawWrapped(r, s, c ?? Palette.UIText, 1, a);
        void Centered(float y, string s, Color c, int scale = 1) => Text((VW - PixelFont.Width(s) * scale) / 2f, y, s, c, scale);

        static bool Hover(Rect r) => r.Contains(Event.current.mousePosition);

        void Tip(Rect r, string text) { if (text != null && Hover(r)) { tooltip = text; tipPos = Event.current.mousePosition; } }

        bool Clicked(Rect r, int button = 0)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == button && r.Contains(e.mousePosition)) { e.Use(); return true; }
            return false;
        }

        bool Button(Rect r, string label, bool enabled = true, string tip = null, string icon = null, bool selected = false)
        {
            rects.Add(r);
            bool hov = Hover(r) && enabled;
            var bg = !enabled ? Palette.UIPanelLight : selected ? Palette.UISand : hov ? Palette.UIPanelLight : Palette.UIPanelDark;
            var fg = !enabled ? Palette.UITextDim : selected ? Palette.UIPanelDark : Palette.UIText;
            Fill(r, Palette.UIPanelDark);
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), bg);
            if (enabled && !selected) Fill(new Rect(r.x + 1, r.yMax - 2, r.width - 2, 1), new Color(0, 0, 0, 0.18f));
            float tx = r.x + (r.width - PixelFont.Width(label)) / 2f;
            if (icon != null)
            {
                float total = 12 + (label.Length > 0 ? 3 + PixelFont.Width(label) : 0);
                float ix = r.x + (r.width - total) / 2f;
                PixelIcons.Draw(new Rect(Mathf.Round(ix), Mathf.Round(r.y + (r.height - 12) / 2f), 12, 12), icon, fg);
                tx = ix + 15;
            }
            if (label.Length > 0) Text(tx, r.y + (r.height - 8) / 2f + 1, label, fg);
            Tip(r, tip);
            return enabled && Clicked(r);
        }

        void Bar(Rect r, float t, Color fg, Color? bg = null)
        {
            Fill(r, bg ?? Palette.UIPanelDark);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), fg);
        }

        void IconTile(Rect r, string icon, Color? bg = null)
        {
            Fill(r, Palette.UIPanelDark);
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), bg ?? Palette.UIPanel);
            float isz = r.width >= 26 ? 24 : 12;
            PixelIcons.Draw(new Rect(Mathf.Round(r.x + (r.width - isz) / 2), Mathf.Round(r.y + (r.height - isz) / 2), isz, isz), icon, Palette.UIText);
        }

        void DrawTooltip()
        {
            var lines = PixelFont.Wrap(tooltip, 190);
            float w = lines.Max(l => PixelFont.Width(l)) + 10, h = lines.Count * PixelFont.LineH + 8;
            float x = tipPos.x + 10, y = tipPos.y + 12;
            if (x + w > VW - 2) x = VW - w - 2;
            if (y + h > VH - 2) y = tipPos.y - h - 4;
            var r = new Rect(x, y, w, h);
            Fill(new Rect(r.x + 2, r.y + 2, w, h), Palette.UIShadow);
            Fill(r, Palette.UIPanelDark);
            Fill(new Rect(r.x + 1, r.y + 1, w - 2, h - 2), Palette.UISand);
            float ly = y + 5;
            foreach (var l in lines) { Text(x + 5, ly, l, Palette.UIPanelDark); ly += PixelFont.LineH; }
        }

        static string Num(float v)
        {
            if (v >= 10000) return (v / 1000f).ToString("0") + "k";
            if (v >= 1000) return (v / 1000f).ToString("0.0") + "k";
            return ((int)v).ToString();
        }

        static string Signed(float v) => (v >= 0 ? "+" : "") + (Mathf.Abs(v) < 10 ? v.ToString("0.0") : ((int)v).ToString());

        float Scroll(Rect r, float value, float max)
        {
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && r.Contains(e.mousePosition)) { value += e.delta.y * 6; e.Use(); }
            return Mathf.Clamp(value, 0, Mathf.Max(0, max));
        }

        // ------------------------------------------------------------------
        //  HUD: Obere Leiste
        // ------------------------------------------------------------------
        void DrawTopBar()
        {
            var p = G.Player;
            var d = G.Delta(p.id);
            // Links: Rang & Datum (wie im Vorbild: Krone + Zahl, "Day")
            var left = new Rect(4, 4, 150, 30);
            Panel(left);
            PixelIcons.Draw(new Rect(9, 9, 12, 12), "crown", Palette.UIText);
            Text(25, 8, Ranks.Get(p.rank).Name, Palette.UIText);
            Text(25, 19, $"Tag {G.S.day + 1}", Palette.UITextDim);
            Text(70, 19, Calendar.SeasonNames[(int)G.Season] + " J" + Calendar.Year(G.S.day), Palette.UITextDim);
            Tip(left, $"{p.name}\n{G.DateString()}\nAnsehen: {(int)p.prestige}\nKlick: Reich-Übersicht (R)");
            if (Clicked(left)) ToggleWindow(Window.Realm);

            // Mitte: Kacheln
            string[] icons = { "food", "wood", "stone", "iron", "gold", "weapons", "people", "smile", "prestige" };
            int n = icons.Length;
            float tw = 30, gap = 4;
            float total = n * tw + (n - 1) * gap;
            float x0 = Mathf.Max(left.xMax + 8, (VW - total) / 2f);
            float cap = G.Storage(p.id);
            float pop = G.TotalPop(p.id);
            float happy = G.SettlementsOf(p.id).Any() ? G.SettlementsOf(p.id).Average(s => s.happiness) : 0;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x0 + i * (tw + gap), 4, tw, 22);
                rects.Add(new Rect(r.x, r.y, r.width, 32));
                IconTile(r, icons[i]);
                string val; string tip; Color col = Palette.UIText; float frac = -1;
                if (i < ResInfo.Count)
                {
                    val = Num(p.res[i]);
                    tip = $"{ResInfo.Name(i)}: {(int)p.res[i]}" + (i != (int)Res.Gold ? $" / {(int)cap}" : "") + $"\nPro Tag: {Signed(d[i])}";
                    if (i != (int)Res.Gold) frac = p.res[i] / cap;
                    if (d[i] < -0.05f) col = Palette.UIGood;
                    if (i == (int)Res.Food && d[i] < 0) tip += "\nAchtung: Nahrung nimmt ab!";
                }
                else if (i == 6) { val = Num(pop); tip = $"Bevölkerung: {(int)pop}\nSoldaten: {G.TotalSoldiers(p.id)}"; }
                else if (i == 7) { val = ((int)happy).ToString(); tip = "Durchschnittliche Zufriedenheit. Unter 25 drohen Abwanderung und Aufstände."; frac = happy / 100f; }
                else { val = Num(p.prestige); tip = "Ansehen: nötig für Rangaufstiege und Diplomatie."; }
                // Balken unter dem Icon (wie im Vorbild)
                if (frac >= 0) Bar(new Rect(r.x + 3, r.yMax - 4, r.width - 6, 2), frac, Palette.UIText, Palette.UIPanelDark);
                var lr = new Rect(r.x - 2, r.yMax + 1, r.width + 4, 10);
                Fill(lr, new Color(Palette.UIPanelDark.r, Palette.UIPanelDark.g, Palette.UIPanelDark.b, 0.85f));
                Text(lr.x + (lr.width - PixelFont.Width(val)) / 2, lr.y + 1, val, col);
                if (i < ResInfo.Count && i != (int)Res.Gold && p.res[i] >= cap - 1) Fill(new Rect(r.xMax - 4, r.y + 1, 3, 3), Palette.UIGood);
                Tip(new Rect(r.x, r.y, r.width, 32), tip);
            }

            // Rechts: Geschwindigkeit & Menü
            float bx = VW - 4 - 22;
            if (Button(new Rect(bx, 4, 22, 18), "", true, "Menü (Esc)", "gear")) OpenWindow(Window.Menu);
            bx -= 26;
            for (int s = 3; s >= 1; s--)
            {
                bool sel = !gm.Paused && gm.Speed == s;
                if (Button(new Rect(bx, 4, 22, 18), s == 1 ? "1x" : s == 2 ? "2x" : "5x", true, $"Geschwindigkeit ({s})", null, sel)) { gm.Speed = s; gm.Paused = false; }
                bx -= 24;
            }
            if (Button(new Rect(bx, 4, 22, 18), "", true, "Pause (Leertaste)", gm.Paused ? "play" : "pause", gm.Paused)) gm.Paused = !gm.Paused;
        }

        // ------------------------------------------------------------------
        //  Seitenleiste links
        // ------------------------------------------------------------------
        void DrawSideButtons()
        {
            float y = 40;
            (string icon, string tip, Window w)[] items =
            {
                ("crown", "Reich & Rang (R)", Window.Realm),
                ("handshake", "Diplomatie (G)", Window.Diplomacy),
                ("book", "Chronik (L)", Window.Log),
                ("scroll", "Hilfe (F1)", Window.Help),
            };
            foreach (var it in items)
            {
                if (Button(new Rect(4, y, 22, 20), "", true, it.tip, it.icon, Open == it.w)) ToggleWindow(it.w);
                y += 23;
            }
            if (Button(new Rect(4, y, 22, 20), "", true, "Zur Hauptstadt (H)", "house")) gm.FocusHome();
            y += 23;
            if (Button(new Rect(4, y, 22, 20), "", true, "Farbpalette wechseln (P): " + Palette.Current.Name, "sun")) gm.CyclePalette();
        }

        // ------------------------------------------------------------------
        //  Minikarte
        // ------------------------------------------------------------------
        void DrawMinimap()
        {
            float w = Mathf.Min(160, VW * 0.22f), h = w * WorldMap.H / WorldMap.W;
            var r = new Rect(4, VH - h - 4, w, h);
            if (Event.current.type == EventType.Repaint)
            {
                minimapTimer -= Time.unscaledDeltaTime;
                if (minimap == null || minimapTimer <= 0) { UpdateMinimap(); minimapTimer = 1.5f; }
            }
            Panel(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Palette.UIPanelDark);
            if (Event.current.type == EventType.Repaint && minimap != null) GUI.DrawTexture(r, minimap);
            // Kamera-Position
            var t = gm.CamCtrl.Target;
            float cx = r.x + t.x / WorldMap.W * w, cy = r.yMax - t.z / WorldMap.H * h;
            float vs = gm.CamCtrl.Distance / WorldMap.W * w * 0.8f;
            var vr = new Rect(cx - vs, cy - vs * 0.6f, vs * 2, vs * 1.2f);
            Fill(new Rect(vr.x, vr.y, vr.width, 1), Palette.UIText); Fill(new Rect(vr.x, vr.yMax, vr.width, 1), Palette.UIText);
            Fill(new Rect(vr.x, vr.y, 1, vr.height), Palette.UIText); Fill(new Rect(vr.xMax, vr.y, 1, vr.height + 1), Palette.UIText);
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && r.Contains(e.mousePosition))
            {
                float mx = (e.mousePosition.x - r.x) / w * WorldMap.W, mz = (r.yMax - e.mousePosition.y) / h * WorldMap.H;
                gm.CamCtrl.JumpTo(new Vector3(mx, gm.CamCtrl.Target.y, mz));
                e.Use();
            }
            Tip(r, "Weltkarte – klicken zum Springen");
        }

        void UpdateMinimap()
        {
            int W = WorldMap.W, H = WorldMap.H;
            if (minimap == null)
            {
                minimap = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                minimapPx = new Color32[W * H];
            }
            var pal = Palette.Current;
            var map = G.Map;
            for (int i = 0; i < W * H; i++)
            {
                Color c;
                if (!map.disc[i]) c = map.IsSea(i) ? pal.FogSea : pal.Fog;
                else
                {
                    c = map.river[i] ? pal.River : pal.BiomeColor(map.biome[i]);
                    if (map.biome[i] == Biome.Forest || map.biome[i] == Biome.Pine) c = Color.Lerp(c, pal.Tree, 0.5f);
                    int o = map.owner[i];
                    if (o >= 0) { var f = G.F(o); if (f != null) c = Color.Lerp(c, pal.FactionColor(f.color), 0.55f); }
                }
                minimapPx[i] = c;
            }
            foreach (var s in G.Settlements)
            {
                if (!G.IsDiscovered(s.x, s.y)) continue;
                var f = G.F(s.faction);
                Color32 sc = f != null && f.isPlayer ? new Color32(255, 255, 255, 255) : new Color32(40, 20, 15, 255);
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    if (WorldMap.In(s.x + dx, s.y + dy)) minimapPx[WorldMap.Idx(s.x + dx, s.y + dy)] = sc;
            }
            minimap.SetPixels32(minimapPx);
            minimap.Apply(false);
        }

        // ------------------------------------------------------------------
        //  Quest-Tracker
        // ------------------------------------------------------------------
        void DrawQuest()
        {
            var ch = G.CurrentChapter;
            float w = 172;
            float x = VW - w - 4, y = 40;
            if (ch == null)
            {
                var r0 = new Rect(x, y, w, 20);
                Panel(r0);
                Text(x + 6, y + 6, "Freies Spiel – die Welt gehört euch", Palette.UIText);
                return;
            }
            var objs = G.CurrentObjectives();
            float h = questOpen ? 30 + objs.Count * 12 + 2 : 18;
            var r = new Rect(x, y, w, h);
            Panel(r);
            PixelIcons.Draw(new Rect(x + 5, y + 4, 12, 12), "book", Palette.UIText);
            Text(x + 21, y + 6, ch.Title + ": " + ch.Subtitle, Palette.UIText);
            if (Clicked(new Rect(x, y, w, 16))) questOpen = !questOpen;
            if (!questOpen) return;
            float oy = y + 20;
            foreach (var o in objs)
            {
                var box = new Rect(x + 6, oy, 9, 9);
                Fill(box, Palette.UIPanelDark);
                if (o.Done) Fill(new Rect(box.x + 2, box.y + 2, 5, 5), Palette.UIGood);
                string t = o.Text;
                if (o.Target > 1 && !o.Done) t += $" ({(int)o.Cur}/{(int)o.Target})";
                Text(x + 19, oy + 1, t, o.Done ? Palette.UITextDim : Palette.UIText);
                oy += 12;
            }
            Tip(r, "Aktuelles Kapitel der Geschichte. Ziele werden automatisch geprüft. Klick auf den Titel klappt ein/aus.");
        }

        // ------------------------------------------------------------------
        //  Meldungen
        // ------------------------------------------------------------------
        void DrawToasts()
        {
            float w = 190;
            float y = 40 + (G.CurrentChapter != null && questOpen ? 30 + G.CurrentObjectives().Count * 12 + 8 : 26);
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float age = Time.unscaledTime - t.t;
                float a = Mathf.Clamp01((7f - age) / 1f);
                var lines = PixelFont.Wrap(t.text, (int)w - 22);
                float h = lines.Count * 10 + 6;
                var r = new Rect(VW - w - 4, y, w, h);
                var bg = t.kind == 2 || t.kind == 3 ? Palette.UIPanelDark : t.kind == 4 ? Palette.UISand : Palette.UIPanel;
                Fill(r, new Color(bg.r, bg.g, bg.b, 0.9f * a));
                var fg = t.kind == 4 ? Palette.UIPanelDark : Palette.UIText;
                string icon = t.kind == 1 ? "check" : t.kind == 2 ? "skull" : t.kind == 3 ? "sword" : t.kind == 4 ? "book" : "scroll";
                PixelIcons.Draw(new Rect(r.x + 3, r.y + 3, 12, 12), icon, new Color(fg.r, fg.g, fg.b, a));
                float ly = r.y + 4;
                foreach (var l in lines) { Text(r.x + 18, ly, l, new Color(fg.r, fg.g, fg.b, a)); ly += 10; }
                y += h + 2;
                if (y > VH * 0.6f) break;
            }
        }

        // ------------------------------------------------------------------
        //  Beschriftungen in der Welt
        // ------------------------------------------------------------------
        bool WorldToVirtual(Vector3 w, out Vector2 v)
        {
            var cam = gm.CamCtrl.Cam;
            var sp = cam.WorldToScreenPoint(w);
            v = new Vector2(sp.x / S, (Screen.height - sp.y) / S);
            return sp.z > 0 && v.x > -50 && v.y > -20 && v.x < VW + 50 && v.y < VH + 20;
        }

        void DrawWorldLabels()
        {
            if (Event.current.type != EventType.Repaint) return;
            var pal = Palette.Current;
            float dist = gm.CamCtrl.Distance;
            foreach (var s in G.Settlements)
            {
                if (!G.IsDiscovered(s.x, s.y)) continue;
                var f = G.F(s.faction);
                bool mine = f != null && f.isPlayer;
                if (dist > 80 && !mine && s.pop < 100) continue;
                var wp = gm.Entities.SettlementWorldPos(s) + Vector3.up * 1.4f;
                if (!WorldToVirtual(wp, out var v)) continue;
                string label = s.name;
                float w = PixelFont.Width(label) + 14;
                var r = new Rect(Mathf.Round(v.x - w / 2), Mathf.Round(v.y - 12), w, 11);
                var fc = f != null ? pal.FactionColor(f.color) : Color.white;
                Fill(r, new Color(Palette.UIPanelDark.r, Palette.UIPanelDark.g, Palette.UIPanelDark.b, 0.8f));
                Fill(new Rect(r.x + 2, r.y + 2, 7, 7), fc);
                Text(r.x + 11, r.y + 2, label, Palette.UIText);
                if (dist < 45)
                {
                    string sub = ((int)s.pop).ToString();
                    if (f != null && !mine)
                    {
                        var st = G.Status(G.PlayerId, f.id);
                        if (st == DipStatus.War) sub += " KRIEG";
                        else if (st == DipStatus.Alliance) sub += " verbündet";
                    }
                    Text(Mathf.Round(v.x - PixelFont.Width(sub) / 2f), r.yMax + 1, sub, mine ? Palette.UIPanelDark : Palette.UIBad);
                }
                if (s.id == gm.SelSettlement) Brackets(new Rect(v.x - 22, v.y - 2, 44, 36));
            }
            foreach (var a in G.Armies)
            {
                if (!gm.Entities.ArmyVisible(a.id)) continue;
                var wp = gm.Entities.ArmyWorldPos(a) + Vector3.up * 0.6f;
                if (!WorldToVirtual(wp, out var v)) continue;
                string t = (a.units.Sum() + a.settlers).ToString();
                var f = G.F(a.faction);
                var fc = f != null ? pal.FactionColor(f.color) : Color.white;
                float w = PixelFont.Width(t) + 6;
                var r = new Rect(Mathf.Round(v.x - w / 2), Mathf.Round(v.y - 10), w, 10);
                Fill(r, f != null && f.isPlayer ? Palette.UIPanel : (G.AtWar(G.PlayerId, a.faction) ? Palette.UIBad : Palette.UIPanelDark));
                Fill(new Rect(r.x, r.yMax - 1, r.width, 1), fc);
                Text(r.x + 3, r.y + 1, t, Palette.UIText);
                if (a.id == gm.SelArmy) Brackets(new Rect(v.x - 14, v.y - 14, 28, 28));
            }
            // Mauszeiger-Kachel
            if (gm.HoverValid && !MouseOverUI)
            {
                var c = new Vector3(gm.HoverX + 0.5f, gm.Terrain.GroundY(gm.HoverX + 0.5f, gm.HoverY + 0.5f), gm.HoverY + 0.5f);
                if (WorldToVirtual(c, out var v))
                {
                    float sz = Mathf.Clamp(260f / gm.CamCtrl.Distance, 6, 30);
                    Brackets(new Rect(v.x - sz / 2, v.y - sz / 2, sz, sz), 2, gm.CurrentMode == GameManager.Mode.PlaceSettlers ? Palette.UISand : Palette.UIPanelDark);
                }
            }
        }

        /// <summary>Eck-Klammern wie im Vorbild um die Auswahl.</summary>
        void Brackets(Rect r, int len = 4, Color? col = null)
        {
            var c = col ?? Palette.UIPanelDark;
            float t = 1f;
            Fill(new Rect(r.x, r.y, len, t), c); Fill(new Rect(r.x, r.y, t, len), c);
            Fill(new Rect(r.xMax - len, r.y, len, t), c); Fill(new Rect(r.xMax - t, r.y, t, len), c);
            Fill(new Rect(r.x, r.yMax - t, len, t), c); Fill(new Rect(r.x, r.yMax - len, t, len), c);
            Fill(new Rect(r.xMax - len, r.yMax - t, len, t), c); Fill(new Rect(r.xMax - t, r.yMax - len, t, len), c);
        }

        void DrawModeHint()
        {
            string hint = null;
            if (gm.CurrentMode == GameManager.Mode.PlaceSettlers)
            {
                hint = "Linksklick: Ort für die neue Siedlung wählen  (Esc: abbrechen)";
                if (gm.HoverValid && !G.CanFoundAt(G.PlayerId, gm.HoverX, gm.HoverY, out string why)) hint += "\n" + why;
            }
            else
            {
                var a = G.A(gm.SelArmy);
                if (a != null && a.faction == G.PlayerId && a.path.Count == 0)
                    hint = a.settlers > 0 && a.units.Sum() == 0 ? "Rechtsklick: Ort zum Siedeln wählen" : "Rechtsklick: Ziel – Gelände, eigene Stadt oder Feind (Shift: Überfall)";
            }
            if (hint == null) return;
            var lines = hint.Split('\n');
            float w = lines.Max(l => PixelFont.Width(l)) + 12;
            var r = new Rect((VW - w) / 2, 42, w, lines.Length * 10 + 6);
            Panel(r, Palette.UISand);
            for (int i = 0; i < lines.Length; i++) Text(r.x + 6, r.y + 4 + i * 10, lines[i], Palette.UIPanelDark);
        }
    }
}
