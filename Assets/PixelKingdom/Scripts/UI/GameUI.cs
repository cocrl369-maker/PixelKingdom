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
        public enum Window { None, Menu, Diplomacy, Realm, Log, Help, ConfirmWar, Armies }

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
                return IsOverUI(PKInput.MousePos);
            }
        }

        /// <summary>Liegt ein Bildschirmpunkt (Maus/Finger) auf einem UI-Element?</summary>
        public bool IsOverUI(Vector2 screen)
        {
            if (gm == null || gm.InMenu || modalLastFrame) return true;
            var v = ScreenToVirtual(screen);
            foreach (var r in lastRects) if (r.Contains(v)) return true;
            return false;
        }

        Rect safe;
        Vector2 ScreenToVirtual(Vector2 sp) => new Vector2((sp.x - safe.x) / S, (safe.yMax - sp.y) / S);

        // ---- Touch: Info-Kärtchen statt Maus-Tooltips, Bestätigung durch zweites Tippen ----
        string pinTip; float pinTime; string armed; float armedTime;

        void PinTip(string text) { if (!string.IsNullOrEmpty(text)) { pinTip = text; pinTime = Time.unscaledTime; } }

        /// <summary>Auf Touch-Geräten: erstes Tippen zeigt die Info, zweites Tippen führt aus.</summary>
        bool TouchConfirm(string id, string tip)
        {
            if (!PKInput.UsingTouch) return true;
            if (armed == id && Time.unscaledTime - armedTime < 5f) { armed = null; pinTip = null; return true; }
            armed = id; armedTime = Time.unscaledTime;
            PinTip((tip ?? "") + "\n> __Nochmal tippen zum Bestätigen__");
            return false;
        }

        public void ShowTileInfo(int x, int y)
        {
            int ti = WorldMap.Idx(x, y);
            if (!G.Map.disc[ti]) { PinTip("Unerforschtes Gebiet"); return; }
            string info = "__" + G.Map.BiomeName(ti) + "__";
            string dep = WorldMap.DepositName(G.Map.deposit[ti]);
            if (dep != null) info += "\n__Vorkommen__: " + dep;
            if (G.Map.trees[ti] > 0) info += $"\n{G.Map.trees[ti]} Bäume";
            else if (G.Map.baseTrees[ti] > 0) info += "\nabgeholzt (wächst nach)";
            int o = G.Map.owner[ti];
            if (o >= 0) { var of = G.F(o); if (of != null) info += "\n" + (of.isPlayer ? "Euer Land" : of.name); }
            PinTip(info);
        }

        public void ShowOfflineReport(string report) { if (!string.IsNullOrEmpty(report)) offlineReport = report; }

        void DrawPinnedTip()
        {
            if (pinTip == null) return;
            if (Time.unscaledTime - pinTime > 6f) { pinTip = null; armed = null; return; }
            var lines = PixelFont.Wrap(pinTip, (int)Mathf.Min(260, VW - 30));
            float w = lines.Max(l => PixelFont.Width(l)) + 14, h = lines.Count * PixelFont.LineH + 10;
            var r = new Rect(Mathf.Round((VW - w) / 2), 64, w, h);
            rects.Add(r);
            Fill(new Rect(r.x + 2, r.y + 2, w, h), Palette.UIShadow);
            Fill(r, Palette.UIPanelDark);
            Fill(new Rect(r.x + 1, r.y + 1, w - 2, h - 2), Palette.UISand);
            float ly = r.y + 6;
            foreach (var l in lines) { Text(r.x + 7, ly, l, l.StartsWith(">") ? Palette.UIBad : Palette.UIPanelDark); ly += PixelFont.LineH; }
            if (Clicked(r)) { pinTip = null; armed = null; }
        }

        public bool IsBlocking => gm.InMenu || gm.GameOver || TutorialActive || (G != null && (G.Dialog.Count > 0 || G.CurrentEvent != null)) || report != null || offlineReport != null
                                   || Open == Window.Menu || Open == Window.ConfirmWar;
        public bool KeyboardCaptured => false;

        public void Toast(string text, int kind = 0) => toasts.Add(new Toast_ { text = text, kind = kind, t = Time.unscaledTime });

        public void OnGameStarted(string offline)
        {
            offlineReport = offline;
            toasts.Clear(); Open = Window.None; report = null; dipSel = -1; tutStep = -1; pinTip = null;
            minimapTimer = 0;
        }

        public void OpenWindow(Window w) { Open = w; dipMessage = null; WindowHint(w); }
        public void ToggleWindow(Window w) { Open = Open == w ? Window.None : w; dipMessage = null; WindowHint(Open); }

        void WindowHint(Window w)
        {
            if (G == null) return;
            switch (w)
            {
                case Window.Armies: G.Hint("win_armies", "Die Heerführung: Links stehen eure Heere und Garnisonen, rechts die Ziele in der Nähe. 'Erobern' nimmt eine Stadt ein, 'Überfall' plündert nur, 'Hin' marschiert ohne Angriff. Steht ein Heer in einer eigenen Stadt, tauscht ihr unten Truppen mit der Garnison."); break;
                case Window.Diplomacy: G.Hint("win_diplo", "Die Diplomatie: Links die Herrscher, die ihr kennt, rechts ihr Wesen und was sie wollen. Fahrt über eine Aktion – dort steht, wie gut eure Aussichten sind und warum."); break;
                case Window.Realm: G.Hint("win_realm", "Die Reich-Übersicht hat drei Reiter: 'Rang' zeigt, was für den nächsten Aufstieg fehlt. 'Erlasse & Feste' für Gesetze und Turniere. 'Siedlungen' listet alle eure Orte."); break;
            }
        }

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
            if (G.Dialog.Count > 0 && !TutorialActive && offlineReport == null && report == null && PKInput.KeyDown(PKKey.Space)) G.Dialog.Dequeue();
        }

        // ------------------------------------------------------------------
        //  Zeichnen
        // ------------------------------------------------------------------
        void OnGUI()
        {
            if (gm == null || G == null) return;
            int extra = PlayerPrefs.GetInt("pk_uiscale", 0);
            safe = Screen.safeArea;
            if (safe.width < 100 || safe.height < 100) safe = new Rect(0, 0, Screen.width, Screen.height);
            float hh = safe.height;
            int baseS = Application.isMobilePlatform || PKInput.TouchSim
                ? Mathf.Max(1, Mathf.FloorToInt(hh / 360f))                    // Tablet/Handy: große, fingerfreundliche Elemente
                : (hh < 700 ? 1 : Mathf.Max(2, Mathf.FloorToInt(hh / 500f)));
            S = Mathf.Clamp(baseS + extra, 1, 8);
            VW = safe.width / S; VH = safe.height / S;
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0), Quaternion.identity, new Vector3(S, S, 1));
            if (Event.current.type == EventType.Repaint) { rects.Clear(); modalThisFrame = false; tooltip = null; marks.Clear(); }
            modalNow = false;

            // Eingaben gehen zuerst an die oberste Ebene (Fenster, Dialoge, Ereignisse).
            // Nur wenn dort nichts getroffen wurde, erreicht der Klick die Leisten und Panels darunter.
            var et = Event.current.type;
            bool input = et == EventType.MouseDown || et == EventType.MouseUp || et == EventType.MouseDrag || et == EventType.ScrollWheel;
            if (input)
            {
                DrawTopLayer();
                bool overTop = modalNow;
                foreach (var r in lastTopRects) if (r.Contains(Event.current.mousePosition)) { overTop = true; break; }
                if (Event.current.type != EventType.Used && !overTop) DrawBackgroundLayer();
                FinishFrame();
                return;
            }
            DrawBackgroundLayer();
            int topStart = rects.Count;
            DrawTopLayer();
            if (Event.current.type == EventType.Repaint) lastTopRects = rects.GetRange(topStart, rects.Count - topStart);
            FinishFrame();
        }

        List<Rect> lastTopRects = new List<Rect>();
        bool modalNow;

        void DrawBackgroundLayer()
        {
            if (gm.InMenu) { DrawMainMenu(); return; }
            DrawWorldLabels();
            DrawTopBar();
            DrawSideButtons();
            DrawMinimap();
            DrawQuest();
            DrawToasts();
            DrawSelectionPanel();
            DrawModeHint();
            if (gm.Paused && !IsBlocking) Centered(VH * 0.18f, "- PAUSE -", Palette.UIPanelDark, 2);
        }

        /// <summary>Oberste Ebene: Fenster, Dialoge, Ereignisse, Einführung, Info-Kärtchen.</summary>
        void DrawTopLayer()
        {
            if (gm.InMenu)
            {
                if (Open == Window.Help) DrawHelp();
                DrawPinnedTip();
                return;
            }
            switch (Open)
            {
                case Window.Diplomacy: DrawDiplomacy(); break;
                case Window.Armies: DrawArmies(); break;
                case Window.Realm: DrawRealm(); break;
                case Window.Log: DrawLog(); break;
                case Window.Help: DrawHelp(); break;
                case Window.Menu: DrawMenu(); break;
                case Window.ConfirmWar: DrawConfirmWar(); break;
            }

            if (gm.GameOver) DrawGameOver();
            else if (offlineReport != null) DrawOffline();
            else if (report != null) DrawReport();
            else if (TutorialActive) DrawTutorial();
            else if (G.Dialog.Count > 0) DrawDialog();
            else if (G.CurrentEvent != null) DrawEvent();
            DrawPinnedTip();
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
            modalThisFrame = true; modalNow = true;
            rects.Add(new Rect(0, 0, VW, VH));
            Fill(new Rect(0, 0, VW, VH), new Color(0.2f, 0.08f, 0.04f, 0.35f));
        }

        static void Text(float x, float y, string s, Color? c = null, int scale = 1) => PixelFont.Draw(Mathf.Round(x), Mathf.Round(y), s, c ?? Palette.UIText, scale);
        static float Wrapped(Rect r, string s, Color? c = null, TextAnchor a = TextAnchor.UpperLeft) => PixelFont.DrawWrapped(r, s, c ?? Palette.UIText, 1, a);
        void Centered(float y, string s, Color c, int scale = 1) => Text((VW - PixelFont.Width(s) * scale) / 2f, y, s, c, scale);

        static bool Hover(Rect r) => r.Contains(Event.current.mousePosition);

        void Tip(Rect r, string text)
        {
            if (text == null) return;
            if (PKInput.UsingTouch)
            {
                var e = Event.current;
                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition)) PinTip(text);
                return;
            }
            if (Hover(r)) { tooltip = text; tipPos = Event.current.mousePosition; }
        }

        bool Clicked(Rect r, int button = 0)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == button && r.Contains(e.mousePosition)) { e.Use(); AudioManager.Click(); return true; }
            return false;
        }

        bool Button(Rect r, string label, bool enabled = true, string tip = null, string icon = null, bool selected = false, int ts = 1, bool confirm = false)
        {
            rects.Add(r);
            bool hov = Hover(r) && enabled;
            var bg = !enabled ? Palette.UIPanelLight : selected ? Palette.UISand : hov ? Palette.UIPanelLight : Palette.UIPanel;
            var fg = !enabled ? Palette.UITextDim : selected ? Palette.UIPanelDark : Palette.UIText;
            Fill(r, Palette.UIPanelDark);
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), bg);
            if (enabled && !selected)
            {
                Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, 1), new Color(1, 1, 1, hov ? 0.25f : 0.14f));     // Lichtkante
                Fill(new Rect(r.x + 1, r.yMax - 2, r.width - 2, 1), new Color(0, 0, 0, 0.25f));             // Schattenkante
            }
            float lw = PixelFont.Width(label) * ts;
            float tx = r.x + (r.width - lw) / 2f;
            if (icon != null)
            {
                float isz = ts > 1 ? 16 : 12;
                float total = isz + (label.Length > 0 ? 4 + lw : 0);
                float ix = r.x + (r.width - total) / 2f;
                Icon(new Rect(Mathf.Round(ix), Mathf.Round(r.y + (r.height - isz) / 2f), isz, isz), icon, fg);
                tx = ix + isz + 4;
            }
            if (label.Length > 0) Text(tx, r.y + (r.height - 8 * ts) / 2f + ts, label, fg, ts);
            if (PKInput.UsingTouch)
            {
                if (!Clicked(r)) return false;
                if (!enabled) { PinTip(tip); return false; }
                if (confirm) return TouchConfirm(label + r.x + "," + r.y, tip);
                return true;
            }
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
            string key = ColorKey(icon);
            if (key != null) { SpriteLib.DrawUI(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), key, null, true, true); return; }
            float isz = r.width >= 26 ? 24 : 12;
            PixelIcons.Draw(new Rect(Mathf.Round(r.x + (r.width - isz) / 2), Mathf.Round(r.y + (r.height - isz) / 2), isz, isz), icon, Palette.UIText);
        }

        static string ColorKey(string icon)
        {
            if (icon == null) return null;
            if (SpriteLib.Has("i_" + icon)) return "i_" + icon;
            if (SpriteLib.Has(icon)) return icon;
            return null;
        }

        /// <summary>Icon: farbig, wenn es in den Platz passt, sonst die schlichte Variante.</summary>
        static void Icon(Rect r, string name, Color tint)
        {
            string key = ColorKey(name);
            var sp = key != null ? SpriteLib.Get(key) : null;
            if (sp != null && sp.W <= r.width + 4 && sp.H <= r.height + 4)
            {
                var rr = new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4);
                if (tint.a < 0.99f) SpriteLib.DrawUI(rr, key, new Color(1, 1, 1, tint.a)); else SpriteLib.DrawUI(rr, key);
                return;
            }
            if (PixelIcons.Has(name)) { PixelIcons.Draw(r, name, tint); return; }
            if (key != null) SpriteLib.DrawUI(r, key);
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
            Mark("rank", left);
            Panel(left);
            Icon(new Rect(9, 9, 12, 12), "crown", Palette.UIText);
            Text(25, 8, Ranks.Get(p.rank).Name, Palette.UIText);
            Text(25, 19, $"Tag {G.S.day + 1}", Palette.UITextDim);
            Text(70, 19, Calendar.SeasonNames[(int)G.Season] + " J" + Calendar.Year(G.S.day), Palette.UITextDim);
            Tip(left, $"__{p.name}__\n__Datum__: {G.DateString()}\n__Ansehen__: {(int)p.prestige}\nKlick: Reich-Übersicht (R)");
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
                Mark("resources", new Rect(r.x - 2, r.y, r.width + 4, 33));
                rects.Add(new Rect(r.x, r.y, r.width, 32));
                IconTile(r, icons[i]);
                string val; string tip; Color col = Palette.UIText; float frac = -1;
                if (i < ResInfo.Count)
                {
                    val = Num(p.res[i]);
                    tip = $"__{ResInfo.Name(i)}__\n__Vorrat__: {(int)p.res[i]}" + (i != (int)Res.Gold ? $" / {(int)cap}" : "") + $"\n__Pro Tag__: {Signed(d[i])}";
                    if (i != (int)Res.Gold) frac = p.res[i] / cap;
                    if (d[i] < -0.05f) col = Palette.UIGood;
                    if (i == (int)Res.Food && d[i] < 0) tip += "\n__Achtung__: Nahrung nimmt ab!";
                }
                else if (i == 6) { val = Num(pop); tip = $"__Bevölkerung__: {(int)pop}\n__Soldaten__: {G.TotalSoldiers(p.id)}"; }
                else if (i == 7) { val = ((int)happy).ToString(); tip = "__Zufriedenheit__\nDurchschnitt aller Siedlungen. Unter 25 drohen Abwanderung und Aufstände."; frac = happy / 100f; }
                else { val = Num(p.prestige); tip = "__Ansehen__\nNötig für Rangaufstiege und Diplomatie."; }
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
            Mark("speed", new Rect(bx, 4, VW - 4 - bx, 18));
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
                ("army", "Heerführung (M): Heere befehligen und verwalten", Window.Armies),
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
            Mark("side", new Rect(4, 40, 22, y + 20 - 40));
        }

        // ------------------------------------------------------------------
        //  Minikarte
        // ------------------------------------------------------------------
        void DrawMinimap()
        {
            float w = Mathf.Min(160, VW * 0.17f), h = w * WorldMap.H / WorldMap.W;
            var r = new Rect(4, VH - h - 4, w, h);
            if (Event.current.type == EventType.Repaint)
            {
                minimapTimer -= Time.unscaledDeltaTime;
                if (minimap == null || minimapTimer <= 0) { UpdateMinimap(); minimapTimer = 1.5f; }
            }
            Panel(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Palette.UIPanelDark);
            Mark("map", new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4));
            if (Event.current.type == EventType.Repaint && minimap != null) GUI.DrawTexture(r, minimap);
            // Kamera-Position
            var t = gm.CamCtrl.Target;
            float cx = r.x + t.x / WorldMap.W * w, cy = r.yMax - t.y / WorldMap.H * h;
            var view = gm.CamCtrl.ViewSize;
            float vw = Mathf.Min(w, view.x / WorldMap.W * w), vh = Mathf.Min(h, view.y / WorldMap.H * h);
            var vr = new Rect(cx - vw / 2, cy - vh / 2, vw, vh);
            Fill(new Rect(vr.x, vr.y, vr.width, 1), Palette.UIText); Fill(new Rect(vr.x, vr.yMax, vr.width, 1), Palette.UIText);
            Fill(new Rect(vr.x, vr.y, 1, vr.height), Palette.UIText); Fill(new Rect(vr.xMax, vr.y, 1, vr.height + 1), Palette.UIText);
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && r.Contains(e.mousePosition))
            {
                float mx = (e.mousePosition.x - r.x) / w * WorldMap.W, mz = (r.yMax - e.mousePosition.y) / h * WorldMap.H;
                gm.CamCtrl.JumpTo(new Vector2(mx, mz));
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
            var a = Palette.Art;
            var map = G.Map;
            for (int i = 0; i < W * H; i++)
            {
                Px c;
                if (!map.disc[i]) c = (i / W + i) % 7 == 0 ? a.ParchB : a.ParchA;
                else if (map.river[i]) c = a.River;
                else
                {
                    switch (map.biome[i])
                    {
                        case Biome.Ocean: c = a.Deep; break;
                        case Biome.Shallow: c = a.Shallow; break;
                        case Biome.Lake: c = a.Water; break;
                        case Biome.Beach: c = a.Beach; break;
                        case Biome.Fertile: c = a.Fertile; break;
                        case Biome.Forest: c = a.Leaf; break;
                        case Biome.Pine: c = a.Pine; break;
                        case Biome.Hills: c = a.Hills; break;
                        case Biome.Mountain: c = a.Rock; break;
                        case Biome.Snow: c = a.Snow; break;
                        case Biome.Desert: c = a.Desert; break;
                        case Biome.Steppe: c = a.Steppe; break;
                        default: c = a.Grass; break;
                    }
                    int o = map.owner[i];
                    if (o >= 0) { var f = G.F(o); if (f != null) { var fc = (Color32)Palette.FactionColor(f.color); c = c.Lerp(new Px(fc.r, fc.g, fc.b), 0.5f); } }
                }
                minimapPx[i] = new Color32(c.r, c.g, c.b, 255);
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
        float questBottom = 60;

        void DrawQuest()
        {
            var ch = G.CurrentChapter;
            float w = Mathf.Min(200, VW * 0.3f);
            float x = VW - w - 4, y = 40;
            // Status-Zeilen: Friedenszeit und Druck des Kaiserreichs
            var emp = G.Emperor;
            bool showPressure = emp != null && emp.alive && !G.S.story.Has("emperor_war") && G.Player.rank >= 1;
            bool war = emp != null && emp.alive && G.S.story.Has("emperor_war");
            int statusLines = (G.InPeace ? 1 : 0) + (showPressure || war ? 1 : 0) + (G.CanChallengeEmperor ? 1 : 0);

            var objs = G.CurrentObjectives();
            var objLines = new List<List<string>>();
            foreach (var o in objs)
            {
                string t = o.Text;
                if (o.Target > 1 && !o.Done) t += $" ({(int)o.Cur}/{(int)o.Target})";
                objLines.Add(PixelFont.Wrap(t, (int)w - 26));
            }
            int nLines = 0; foreach (var l in objLines) nLines += l.Count;
            float h = ch == null ? 18 : questOpen ? 22 + nLines * 10 + objs.Count * 2 + 2 : 18;
            if (questOpen) h += statusLines * 13;
            var r = new Rect(x, y, w, h);
            Panel(r);
            Mark("quest", r);
            PixelIcons.Draw(new Rect(x + 5, y + 4, 12, 12), "book", Palette.UIText);
            string head = ch == null ? "Freies Spiel – die Welt gehört euch" : ch.Title + ": " + ch.Subtitle;
            if (PixelFont.Width(head) > w - 26) head = ch != null ? ch.Subtitle : head;
            Text(x + 21, y + 6, head, Palette.UIText);
            if (Clicked(new Rect(x, y, w, 16))) questOpen = !questOpen;
            questBottom = r.yMax;
            if (!questOpen) return;
            float oy = y + 20;
            for (int k = 0; k < objs.Count; k++)
            {
                var o = objs[k];
                var box = new Rect(x + 6, oy, 9, 9);
                Fill(box, Palette.UIPanelDark);
                if (o.Done) Fill(new Rect(box.x + 2, box.y + 2, 5, 5), Palette.UIGood);
                foreach (var line in objLines[k]) { Text(x + 19, oy + 1, line, o.Done ? Palette.UITextDim : Palette.UIText); oy += 10; }
                oy += 2;
            }
            Tip(new Rect(x, y, w, oy - y), "__Aufgaben__\nDas aktuelle Kapitel der Geschichte. Ziele werden automatisch geprüft. Ihr bestimmt das Tempo – nichts davon läuft euch weg. Klick auf den Titel klappt ein/aus.");
            if (G.InPeace)
            {
                Icon(new Rect(x + 5, oy, 12, 12), "dove", Palette.UIGood);
                Text(x + 21, oy + 2, $"Friedenszeit: noch {G.PeaceDaysLeft} Tage", Palette.UIGood);
                Tip(new Rect(x, oy, w, 12), "__Friedenszeit__\nNiemand greift euch an. Zeit, das Dorf auszubauen. Nach jedem Kapitel beginnt eine neue Friedenszeit.");
                oy += 13;
            }
            if (showPressure)
            {
                float pr = G.S.story.pressure;
                Icon(new Rect(x + 5, oy, 12, 12), "eagle", Palette.UIText);
                Text(x + 21, oy + 2, "Druck", Palette.UIText);
                Bar(new Rect(x + 52, oy + 4, w - 60, 5), pr / 100f, pr >= 80 ? Palette.UIBad : pr >= 50 ? Palette.UIGood : Palette.UISand);
                Tip(new Rect(x, oy, w, 12), $"__Druck des Kaiserreichs__: {(int)pr}/100\nSteigt langsam mit eurem Wachstum und wenn ihr dem Kaiser trotzt. Sinkt, wenn ihr den Zehnt zahlt oder Geschenke schickt.\n__Ab 50__: Strafexpeditionen\n__Bei 100__: Der Kaiser erklärt den Krieg (ab Kapitel {Game.ChapterUprising + 1})");
                oy += 13;
            }
            else if (war)
            {
                Icon(new Rect(x + 5, oy, 12, 12), "sword", Palette.UIGood);
                Text(x + 21, oy + 2, "Krieg gegen den Kaiser!", Palette.UIGood);
                oy += 13;
            }
            if (G.CanChallengeEmperor)
            {
                if (Button(new Rect(x + 5, oy - 1, w - 10, 13), "Gegen den Kaiser erheben", true,
                    "__Aufstand__\nIhr entscheidet, wann der Krieg gegen Kaiser Malrik beginnt. Kaisertreue Fürsten werden sich gegen euch stellen, Kaiserfeinde werden euch mögen.", "sword", false, 1, true))
                    G.ChallengeEmperor();
                oy += 13;
            }
            questBottom = Mathf.Max(r.yMax, oy);
        }

        // ------------------------------------------------------------------
        //  Meldungen
        // ------------------------------------------------------------------
        void DrawToasts()
        {
            float w = 190;
            float y = questBottom + 6;
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
            v = ScreenToVirtual(sp);
            return v.x > -50 && v.y > -20 && v.x < VW + 50 && v.y < VH + 20;
        }

        void DrawWorldLabels()
        {
            if (Event.current.type != EventType.Repaint) return;
            float zoom = gm.CamCtrl.Zoom;
            foreach (var s in G.Settlements)
            {
                if (!G.IsDiscovered(s.x, s.y)) continue;
                var f = G.F(s.faction);
                bool mine = f != null && f.isPlayer;
                if (zoom < 1f && !mine && s.pop < 100) continue;
                var wp = gm.Entities.SettlementWorldPos(s) + Vector3.up * (zoom < 1f ? 0.5f : 2.2f);
                if (!WorldToVirtual(wp, out var v)) continue;
                string label = s.name;
                float w = PixelFont.Width(label) + 14;
                var r = new Rect(Mathf.Round(v.x - w / 2), Mathf.Round(v.y - 12), w, 11);
                var fc = f != null ? Palette.FactionColor(f.color) : Color.white;
                Fill(r, new Color(Palette.UIPanelDark.r, Palette.UIPanelDark.g, Palette.UIPanelDark.b, 0.8f));
                Fill(new Rect(r.x + 2, r.y + 2, 7, 7), fc);
                Text(r.x + 11, r.y + 2, label, Palette.UIText);
                if (zoom >= 2f)
                {
                    string sub = ((int)s.pop).ToString();
                    if (f != null && !mine)
                    {
                        var st = G.Status(G.PlayerId, f.id);
                        if (st == DipStatus.War) sub += " KRIEG";
                        else if (st == DipStatus.Alliance) sub += " verbündet";
                    }
                    var subR = new Rect(Mathf.Round(v.x - PixelFont.Width(sub) / 2f) - 2, r.yMax, PixelFont.Width(sub) + 4, 10);
                    Fill(subR, new Color(0, 0, 0, 0.35f));
                    Text(subR.x + 2, subR.y + 1, sub, mine ? Palette.UIText : Palette.UIGood);
                }
                if (s.id == gm.SelSettlement) { float bs = 3.4f * 16f * zoom / S; var cpos = gm.Entities.SettlementWorldPos(s); if (WorldToVirtual(cpos, out var cv)) Brackets(new Rect(cv.x - bs / 2, cv.y - bs * 0.6f, bs, bs), 5, Palette.UIText); }
            }
            foreach (var a in G.Armies)
            {
                if (!gm.Entities.ArmyVisible(a.id)) continue;
                var wp = gm.Entities.ArmyWorldPos(a) + Vector3.up * 1.3f;
                if (!WorldToVirtual(wp, out var v)) continue;
                string t = (a.units.Sum() + a.settlers).ToString();
                var f = G.F(a.faction);
                var fc = f != null ? Palette.FactionColor(f.color) : Color.white;
                float w = PixelFont.Width(t) + 6;
                var r = new Rect(Mathf.Round(v.x - w / 2), Mathf.Round(v.y - 10), w, 10);
                Fill(r, f != null && f.isPlayer ? Palette.UIPanel : (G.AtWar(G.PlayerId, a.faction) ? Palette.UIBad : Palette.UIPanelDark));
                Fill(new Rect(r.x, r.yMax - 1, r.width, 1), fc);
                Text(r.x + 3, r.y + 1, t, Palette.UIText);
                if (a.id == gm.SelArmy) { float bs = 1.6f * 16f * zoom / S; Brackets(new Rect(v.x - bs / 2, v.y + 4, bs, bs), 4, Palette.UIText); }
            }
            // Ruinen und Lager: kleine Beschriftung, Auswahl-Klammern
            foreach (var poi in G.Pois)
            {
                if ((poi.type == 1 && poi.done) || !G.IsDiscovered(poi.x, poi.y)) continue;
                if (!WorldToVirtual(new Vector3(poi.x + 0.5f, poi.y + 0.5f, 0), out var pv)) continue;
                if (poi.id == gm.SelPoi) { float bs = 2.4f * 16f * zoom / S; Brackets(new Rect(pv.x - bs / 2, pv.y - bs * 0.7f, bs, bs), 4, Palette.UIText); }
                if (zoom >= 2f)
                {
                    string t = Game.PoiName(poi);
                    var lr = new Rect(Mathf.Round(pv.x - PixelFont.Width(t) / 2f - 3), Mathf.Round(pv.y + 8), PixelFont.Width(t) + 6, 10);
                    Fill(lr, new Color(0, 0, 0, 0.4f));
                    Text(lr.x + 3, lr.y + 1, t, poi.type == 1 ? Palette.UIGood : Palette.UIText);
                }
            }
            // Mauszeiger-Kachel
            if (gm.HoverValid && (PKInput.UsingTouch || !MouseOverUI))
            {
                var c = new Vector3(gm.HoverX + 0.5f, gm.HoverY + 0.5f, 0);
                if (WorldToVirtual(c, out var v))
                {
                    float sz = 16f * zoom / S;
                    Brackets(new Rect(v.x - sz / 2, v.y - sz / 2, sz, sz), 2, gm.CurrentMode == GameManager.Mode.PlaceSettlers ? Palette.UISand : Palette.UIText);
                }
                // Befehlsvorschau am Mauszeiger (gewähltes eigenes Heer)
                var selA = G.A(gm.SelArmy);
                if (selA != null && selA.faction == G.PlayerId && (!PKInput.UsingTouch || gm.CurrentMode == GameManager.Mode.TargetArmy) && WorldToVirtual(c, out var pv))
                {
                    var ts = gm.SettlementNear(gm.HoverPoint.x, gm.HoverPoint.y, 1.2f);
                    if (ts != null && !G.IsDiscovered(ts.x, ts.y)) ts = null;
                    string prev = G.OrderPreview(selA, gm.HoverX, gm.HoverY, ts, PKInput.Key(PKKey.Shift) || gm.RaidMode);
                    if (prev != null)
                    {
                        string lbl = (PKInput.UsingTouch ? "Tippen: " : gm.CurrentMode == GameManager.Mode.TargetArmy ? "Klick: " : "Rechtsklick: ") + prev;
                        var pr = new Rect(Mathf.Round(pv.x + 10), Mathf.Round(pv.y + 8), PixelFont.Width(lbl) + 8, 12);
                        if (pr.xMax > VW - 4) pr.x = VW - 4 - pr.width;
                        Fill(pr, new Color(Palette.UIPanelDark.r, Palette.UIPanelDark.g, Palette.UIPanelDark.b, 0.9f));
                        Text(pr.x + 4, pr.y + 2, lbl, Palette.UIGood);
                    }
                }
                // Geländeinfo unten links über der Minikarte
                int ti = WorldMap.Idx(gm.HoverX, gm.HoverY);
                if (G.Map.disc[ti])
                {
                    string info = G.Map.BiomeName(ti);
                    string dep = WorldMap.DepositName(G.Map.deposit[ti]);
                    if (dep != null) info += " - " + dep;
                    if (G.Map.trees[ti] > 0) info += $" - {G.Map.trees[ti]} Bäume";
                    else if (G.Map.baseTrees[ti] > 0) info += " - abgeholzt";
                    int o = G.Map.owner[ti];
                    if (o >= 0) { var of = G.F(o); if (of != null) info += " - " + (of.isPlayer ? "euer Land" : of.name); }
                    float mw = Mathf.Min(160, VW * 0.17f), mh = mw * WorldMap.H / WorldMap.W;
                    var ir = new Rect(4, VH - mh - 22, PixelFont.Width(info) + 10, 13);
                    Fill(ir, new Color(Palette.UIPanelDark.r, Palette.UIPanelDark.g, Palette.UIPanelDark.b, 0.9f));
                    Text(ir.x + 5, ir.y + 3, info, Palette.UIText);
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
            if (gm.CurrentMode == GameManager.Mode.TargetArmy)
            {
                hint = (PKInput.UsingTouch ? "Tippt" : "Klickt") + " auf das Ziel für das Heer: Gelände, eigene Stadt oder Feind  (" + (PKInput.UsingTouch ? "Zurück" : "Esc") + ": abbrechen)";
            }
            else if (gm.CurrentMode == GameManager.Mode.PlaceSettlers)
            {
                hint = PKInput.UsingTouch ? "Tippt auf den Ort für die neue Siedlung  (Zurück: abbrechen)" : "Linksklick: Ort für die neue Siedlung wählen  (Esc: abbrechen)";
                if (gm.HoverValid && !G.CanFoundAt(G.PlayerId, gm.HoverX, gm.HoverY, out string why)) hint += "\n" + why;
            }
            else
            {
                var a = G.A(gm.SelArmy);
                if (a != null && a.faction == G.PlayerId && a.path.Count == 0)
                    hint = PKInput.UsingTouch
                        ? (a.settlers > 0 && a.units.Sum() == 0 ? "Tippt auf den Ort zum Siedeln" : "Tippt auf ein Ziel. Lange drücken auf eine Stadt: Überfall")
                        : (a.settlers > 0 && a.units.Sum() == 0 ? "Rechtsklick: Ort zum Siedeln wählen" : "Rechtsklick: Ziel – Gelände, eigene Stadt oder Feind (Shift: Überfall)");
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
