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
        public enum Window { None, Menu, Diplomacy, Realm, Log, Help, ConfirmWar, Armies, Saves, Attention }

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

        void Awake() { gm = GetComponent<GameManager>(); PixelFont.Init(); LoadLanguage(); }

        /// <summary>Sprache aus den Einstellungen laden (beim ersten Start: Systemsprache).</summary>
        public static void LoadLanguage()
        {
            int def = Application.systemLanguage == SystemLanguage.German ? 0 : 1;
            Loc.Set((Loc.Lang)PlayerPrefs.GetInt("pk_lang", def));
        }

        void SetLanguage(Loc.Lang l)
        {
            Loc.Set(l);
            PlayerPrefs.SetInt("pk_lang", (int)l);
            PlayerPrefs.Save();
        }

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

        /// <summary>Zeitpunkt, zu dem zuletzt ein Druck/Klick von der Oberfläche angenommen wurde.</summary>
        public float LastUIPress = -100f;

        bool IsOverVirtual(Vector2 v)
        {
            foreach (var r in lastRects) if (r.Contains(v)) return true;
            return false;
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
            PinTip((tip ?? "") + Loc.T("\n> __Nochmal tippen zum Bestätigen__"));
            return false;
        }

        public void ShowTileInfo(int x, int y)
        {
            int ti = WorldMap.Idx(x, y);
            if (!G.Map.disc[ti]) { PinTip(Loc.T("Unerforschtes Gebiet")); return; }
            string info = "__" + G.Map.BiomeName(ti) + "__";
            string dep = WorldMap.DepositName(G.Map.deposit[ti]);
            if (dep != null) info += Loc.T("\n__Vorkommen__: ") + dep;
            if (G.Map.trees[ti] > 0) info += Loc.F("\n{0} Bäume", (G.Map.trees[ti]));
            else if (G.Map.baseTrees[ti] > 0) info += Loc.T("\nabgeholzt (wächst nach)");
            int o = G.Map.owner[ti];
            if (o >= 0) { var of = G.F(o); if (of != null) info += "\n" + (of.isPlayer ? Loc.T("Euer Land") : of.name); }
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
                case Window.Armies: G.Hint("win_armies", Loc.T("Die Heerführung: Links stehen eure Heere und Garnisonen, rechts die Ziele in der Nähe. 'Erobern' nimmt eine Stadt ein, 'Überfall' plündert nur, 'Hin' marschiert ohne Angriff. Steht ein Heer in einer eigenen Stadt, tauscht ihr unten Truppen mit der Garnison.")); break;
                case Window.Diplomacy: G.Hint("win_diplo", Loc.T("Die Diplomatie: Links die Herrscher, die ihr kennt, rechts ihr Wesen und was sie wollen. Fahrt über eine Aktion – dort steht, wie gut eure Aussichten sind und warum.")); break;
                case Window.Realm: G.Hint("win_realm", Loc.T("Die Reich-Übersicht hat drei Reiter: 'Rang' zeigt, was für den nächsten Aufstieg fehlt. 'Erlasse & Feste' für Gesetze und Turniere. 'Siedlungen' listet alle eure Orte.")); break;
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
            UpdatePointer();
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
            PKInput.FeedGuiEvent(Event.current);   // letzte Eingabequelle – funktioniert in jedem Unity-Projekt
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

            // Unitys eigene Mausereignisse werden nicht benutzt (auf Android teils mit falscher Position).
            var et = Event.current.type;
            CaptureGuiPointer(Event.current);
            if (DesktopGuiPointer && (et == EventType.MouseDown || et == EventType.MouseUp || et == EventType.MouseDrag || et == EventType.ScrollWheel)) return;
            if (et == EventType.MouseDown || et == EventType.MouseUp || et == EventType.MouseDrag || et == EventType.ScrollWheel) { GuiEventFallback(Event.current); return; }

            // Einmal pro Bild: Treffer-Durchlauf. Die oberste Ebene (Fenster, Dialoge, Ereignisse) bekommt den
            // Klick zuerst; nur wenn dort nichts liegt, erreicht er die Leisten und Panels darunter.
            if (et == EventType.Layout && hitSeq != pointerSeq)
            {
                hitSeq = pointerSeq;
                hitFrame = Time.frameCount;
                hitPass = true;
                groupOffset = Vector2.zero; groupStack.Clear();
                int before = rects.Count;
                bool mt = modalThisFrame;
                DrawTopLayer();
                bool overTop = modalNow || clickUsed;
                Vector2 probe = clickPending ? clickPos : ptrDown ? pressStart : ptr;
                for (int k = before; k < rects.Count && !overTop; k++) if (rects[k].Contains(probe)) overTop = true;
                if (!overTop) DrawBackgroundLayer();
                if (clickPending && (clickUsed || overTop || IsOverVirtual(clickPos))) LastUIPress = Time.unscaledTime;
                if (rects.Count > before) rects.RemoveRange(before, rects.Count - before);
                modalThisFrame = mt; modalNow = false; tooltip = null;
                hitPass = false;
                clickPending = false;
                return;
            }
            groupOffset = Vector2.zero; groupStack.Clear();
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
            DrawProblemIcons();
            DrawTopBar();
            DrawSideButtons();
            DrawMinimap();
            DrawQuest();
            DrawToasts();
            DrawSelectionPanel();
            DrawModeHint();
            if (gm.Paused && !IsBlocking) Centered(VH * 0.18f, Loc.T("- PAUSE -"), Palette.UIPanelDark, 2);
        }

        /// <summary>Oberste Ebene: Fenster, Dialoge, Ereignisse, Einführung, Info-Kärtchen.</summary>
        void DrawTopLayer()
        {
            if (gm.InMenu)
            {
                if (Open == Window.Help) DrawHelp();
                if (Open == Window.Saves) DrawSaves();
                DrawPinnedTip();
                return;
            }
            switch (Open)
            {
                case Window.Diplomacy: DrawDiplomacy(); break;
                case Window.Armies: DrawArmies(); break;
                case Window.Attention: DrawAttention(); break;
                case Window.Realm: DrawRealm(); break;
                case Window.Log: DrawLog(); break;
                case Window.Help: DrawHelp(); break;
                case Window.Menu: DrawMenu(); break;
                case Window.ConfirmWar: DrawConfirmWar(); break;
            }

            if (gm.OrderMenu != null && !IsBlocking) DrawOrderMenu();

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

        void Tip(Rect r, string text)
        {
            if (text == null) return;
            if (PKInput.UsingTouch)
            {
                if (TappedPassive(r)) PinTip(text);
                return;
            }
            if (Hover(r)) { tooltip = text; tipPos = ptr; }
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
            Text(25, 19, Loc.F("Tag {0}", (G.S.day + 1)), Palette.UITextDim);
            Text(70, 19, Loc.F("{0} J{1}", Calendar.SeasonNames[(int)G.Season], Calendar.Year(G.S.day)), Palette.UITextDim);
            Tip(left, Loc.F("__{0}__\n__Datum__: {1}\n__Ansehen__: {2}\nKlick: Reich-Übersicht (R)", (p.name), (G.DateString()), ((int)p.prestige)));
            if (Clicked(left)) ToggleWindow(Window.Realm);

            // Mitte: Kacheln
            // Eisen und Waffen erst zeigen, wenn sie eine Rolle spielen
            bool showIron = p.res[(int)Res.Iron] > 0.5f || p.res[(int)Res.Weapons] > 0.5f || p.rank >= 2 || G.CountBuildings(BType.Mine) > 0;
            var idx = new List<int>();
            for (int k = 0; k < 9; k++) if (showIron || (k != 3 && k != 5)) idx.Add(k);
            string[] allIcons = { "food", "wood", "stone", "iron", "gold", "weapons", "people", "smile", "prestige" };
            int n = idx.Count;
            var icons = idx.Select(k => allIcons[k]).ToArray();
            float tw = 30, gap = 4;
            float total = n * tw + (n - 1) * gap;
            float x0 = Mathf.Max(left.xMax + 8, (VW - total) / 2f);
            float cap = G.Storage(p.id);
            float pop = G.TotalPop(p.id);
            float happy = G.SettlementsOf(p.id).Any() ? G.SettlementsOf(p.id).Average(s => s.happiness) : 0;
            for (int ii = 0; ii < n; ii++)
            {
                int i = idx[ii];
                var r = new Rect(x0 + ii * (tw + gap), 4, tw, 22);
                Mark("resources", new Rect(r.x - 2, r.y, r.width + 4, 33));
                rects.Add(new Rect(r.x, r.y, r.width, 32));
                IconTile(r, icons[ii]);
                string val; string tip; Color col = Palette.UIText; float frac = -1;
                if (i < ResInfo.Count)
                {
                    val = Num(p.res[i]);
                    tip = Loc.F("__{0}__\n__Vorrat__: {1}", (ResInfo.Name(i)), ((int)p.res[i])) + (i != (int)Res.Gold ? $" / {(int)cap}" : "") + Loc.F("\n__Pro Tag__: {0}", (Signed(d[i])));
                    if (i != (int)Res.Gold) frac = p.res[i] / cap;
                    if (d[i] < -0.05f) col = Palette.UIGood;
                    if (i == (int)Res.Food && d[i] < 0) tip += Loc.T("\n__Achtung__: Nahrung nimmt ab!");
                }
                else if (i == 6) { val = Num(pop); tip = Loc.F("__Bevölkerung__: {0}\n__Soldaten__: {1}", ((int)pop), (G.TotalSoldiers(p.id))); }
                else if (i == 7) { val = ((int)happy).ToString(); tip = Loc.T("__Zufriedenheit__\nDurchschnitt aller Siedlungen. Unter 25 drohen Abwanderung und Aufstände."); frac = happy / 100f; }
                else { val = Num(p.prestige); tip = Loc.T("__Ansehen__\nNötig für Rangaufstiege und Diplomatie."); }
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
            if (Button(new Rect(bx, 4, 22, 18), "", true, Loc.T("Menü (Esc)"), "gear")) OpenWindow(Window.Menu);
            bx -= 26;
            for (int s = 3; s >= 1; s--)
            {
                bool sel = !gm.Paused && gm.Speed == s;
                if (Button(new Rect(bx, 4, 22, 18), s == 1 ? "1x" : s == 2 ? "2x" : "5x", true, Loc.F("Geschwindigkeit ({0})", (s)), null, sel)) { gm.Speed = s; gm.Paused = false; }
                bx -= 24;
            }
            Mark("speed", new Rect(bx, 4, VW - 4 - bx, 18));
            if (Button(new Rect(bx, 4, 22, 18), "", true, Loc.T("Pause (Leertaste)"), gm.Paused ? "play" : "pause", gm.Paused)) gm.Paused = !gm.Paused;
        }

        // ------------------------------------------------------------------
        //  Seitenleiste links
        // ------------------------------------------------------------------
        void DrawSideButtons()
        {
            float y = 40;
            (string icon, string tip, Window w)[] items =
            {
                ("crown", Loc.T("Reich & Rang (R)"), Window.Realm),
                ("army", Loc.T("Heerführung (M): Heere befehligen und verwalten"), Window.Armies),
                ("handshake", Loc.T("Diplomatie (G)"), Window.Diplomacy),
                ("book", Loc.T("Chronik (L)"), Window.Log),
                ("scroll", Loc.T("Hilfe (F1)"), Window.Help),
            };
            bool armiesKnown = G.ArmiesOf(G.PlayerId).Any() || G.TotalSoldiers(G.PlayerId) > 0 || G.Player.rank >= 1;
            bool diploKnown = G.Factions.Any(f => !f.isPlayer && f.alive && f.knownToPlayer != 0);
            foreach (var it in items)
            {
                if (it.w == Window.Armies && !armiesKnown) continue;
                if (it.w == Window.Diplomacy && !diploKnown) continue;
                if (Button(new Rect(4, y, 22, 20), "", true, it.tip, it.icon, Open == it.w)) ToggleWindow(it.w);
                y += 23;
            }
            if (Button(new Rect(4, y, 22, 20), "", true, Loc.T("Zur Hauptstadt (H)"), "house")) gm.FocusHome();
            y += 23;
            // Was braucht Aufmerksamkeit? (nur wenn es Probleme gibt) – mit Zähler
            int pc = G.Problems.Count;
            if (pc > 0)
            {
                var pr = new Rect(4, y, 22, 20);
                if (Button(pr, "", true, Loc.T("Was braucht Aufmerksamkeit?"), "fire", Open == Window.Attention)) ToggleWindow(Window.Attention);
                string cnt = pc > 9 ? "9+" : pc.ToString();
                var br = new Rect(pr.xMax - 7, pr.y - 3, PixelFont.Width(cnt) + 4, 9);
                Fill(br, Palette.UIBad);
                Text(br.x + 2, br.y + 1, cnt, Color.white);
                y += 23;
            }
            Mark("side", new Rect(4, 40, 22, y - 3 - 40));
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
            if (HeldIn(r))
            {
                var mp = Local(ptr);
                float mx = (mp.x - r.x) / w * WorldMap.W, mz = (r.yMax - mp.y) / h * WorldMap.H;
                gm.CamCtrl.JumpTo(new Vector2(mx, mz));
                LastUIPress = Time.unscaledTime;
            }
            Tip(r, Loc.T("Weltkarte – klicken zum Springen"));
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
            string head = ch == null ? Loc.T("Freies Spiel – die Welt gehört euch") : ch.Title + ": " + ch.Subtitle;
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
            Tip(new Rect(x, y, w, oy - y), Loc.T("__Aufgaben__\nDas aktuelle Kapitel der Geschichte. Ziele werden automatisch geprüft. Ihr bestimmt das Tempo – nichts davon läuft euch weg. Klick auf den Titel klappt ein/aus."));
            if (G.InPeace)
            {
                Icon(new Rect(x + 5, oy, 12, 12), "dove", Palette.UIGood);
                Text(x + 21, oy + 2, Loc.F("Friedenszeit: noch {0} Tage", (G.PeaceDaysLeft)), Palette.UIGood);
                Tip(new Rect(x, oy, w, 12), Loc.T("__Friedenszeit__\nNiemand greift euch an. Zeit, das Dorf auszubauen. Nach jedem Kapitel beginnt eine neue Friedenszeit."));
                oy += 13;
            }
            if (showPressure)
            {
                float pr = G.S.story.pressure;
                Icon(new Rect(x + 5, oy, 12, 12), "eagle", Palette.UIText);
                Text(x + 21, oy + 2, Loc.T("Druck"), Palette.UIText);
                Bar(new Rect(x + 52, oy + 4, w - 60, 5), pr / 100f, pr >= 80 ? Palette.UIBad : pr >= 50 ? Palette.UIGood : Palette.UISand);
                Tip(new Rect(x, oy, w, 12), Loc.F("__Druck des Kaiserreichs__: {0}/100\nSteigt langsam mit eurem Wachstum und wenn ihr dem Kaiser trotzt. Sinkt, wenn ihr den Zehnt zahlt oder Geschenke schickt.\n__Ab 50__: Strafexpeditionen\n__Bei 100__: Der Kaiser erklärt den Krieg (ab Kapitel {1})", ((int)pr), (Game.ChapterUprising + 1)));
                oy += 13;
            }
            else if (war)
            {
                Icon(new Rect(x + 5, oy, 12, 12), "sword", Palette.UIGood);
                Text(x + 21, oy + 2, Loc.T("Krieg gegen den Kaiser!"), Palette.UIGood);
                oy += 13;
            }
            if (G.CanChallengeEmperor)
            {
                if (Button(new Rect(x + 5, oy - 1, w - 10, 13), Loc.T("Gegen den Kaiser erheben"), true,
                    Loc.T("__Aufstand__\nIhr entscheidet, wann der Krieg gegen Kaiser Malrik beginnt. Kaisertreue Fürsten werden sich gegen euch stellen, Kaiserfeinde werden euch mögen."), "sword", false, 1, true))
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
                        if (st == DipStatus.War) sub += Loc.T(" KRIEG");
                        else if (st == DipStatus.Alliance) sub += Loc.T(" verbündet");
                    }
                    var subR = new Rect(Mathf.Round(v.x - PixelFont.Width(sub) / 2f) - 2, r.yMax, PixelFont.Width(sub) + 4, 10);
                    Fill(subR, new Color(0, 0, 0, 0.35f));
                    Text(subR.x + 2, subR.y + 1, sub, mine ? Palette.UIText : Palette.UIGood);
                }
                if (s.id == gm.SelSettlement) { float bs = 3.4f * 16f * zoom / S; var cpos = gm.Entities.SettlementWorldPos(s); if (WorldToVirtual(cpos, out var cv)) Brackets(new Rect(cv.x - bs / 2, cv.y - bs * 0.6f, bs, bs), 5, Palette.UIText); }
            }
            // Heere: keine Zahl mehr – die Größe sieht man an der Formation. Nur der Auswahlrahmen.
            foreach (var a in G.Armies)
            {
                if (a.id != gm.SelArmy || !gm.Entities.ArmyVisible(a.id)) continue;
                if (!WorldToVirtual(gm.Entities.ArmyWorldPos(a), out var v)) continue;
                float bs = Game.ArmyRadius(a) * 2f * 16f * zoom / S;
                Brackets(new Rect(v.x - bs / 2, v.y - bs / 2, bs, bs), 4, Palette.UIText);
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
            if (gm.CurrentMode == GameManager.Mode.PlaceBuilding || gm.CurrentMode == GameManager.Mode.MoveBuilding) DrawPlacement(zoom);
            if (gm.OrderPath != null) DrawOrderPath(zoom);
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
                        string lbl = (PKInput.UsingTouch ? Loc.T("Tippen: ") : gm.CurrentMode == GameManager.Mode.TargetArmy ? Loc.T("Klick: ") : Loc.T("Rechtsklick: ")) + prev;
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
                    if (G.Map.trees[ti] > 0) info += Loc.F(" - {0} Bäume", (G.Map.trees[ti]));
                    else if (G.Map.baseTrees[ti] > 0) info += " - abgeholzt";
                    int o = G.Map.owner[ti];
                    if (o >= 0) { var of = G.F(o); if (of != null) info += " - " + (of.isPlayer ? Loc.T("euer Land") : of.name); }
                    float mw = Mathf.Min(160, VW * 0.17f), mh = mw * WorldMap.H / WorldMap.W;
                    var ir = new Rect(4, VH - mh - 22, PixelFont.Width(info) + 10, 13);
                    Fill(ir, new Color(Palette.UIPanelDark.r, Palette.UIPanelDark.g, Palette.UIPanelDark.b, 0.9f));
                    Text(ir.x + 5, ir.y + 3, info, Palette.UIText);
                }
            }
        }

        // ---- Bauplatz-Wahl: gültige Felder hervorheben, Standortgüte anzeigen ----
        readonly List<(int x, int y, float q)> placeCells = new List<(int x, int y, float q)>();
        int placeKey = int.MinValue; float placeTime;

        void DrawPlacement(float zoom)
        {
            var s = G.St(gm.ModeSource);
            if (s == null) return;
            var t = gm.PlaceType;
            int key = s.id * 977 + (int)t * 31 + G.S.day * 7 + s.queue.Count;
            if (key != placeKey || Time.unscaledTime - placeTime > 1f)
            {
                placeKey = key; placeTime = Time.unscaledTime; placeCells.Clear();
                int r = Game.MaxRemote;
                for (int y = s.y - r; y <= s.y + r; y++)
                    for (int x = s.x - r; x <= s.x + r; x++)
                        if (G.CanPlace(s, t, x, y, out _)) placeCells.Add((x, y, G.SiteBonus(t, x, y) * G.DistanceFactor(s, x, y)));
            }
            float sz = 16f * zoom / S;
            foreach (var c in placeCells)
            {
                if (!WorldToVirtual(new Vector3(c.x + 0.5f, c.y + 0.5f, 0), out var v)) continue;
                float a = Mathf.Clamp(0.12f + (c.q - 0.4f) * 0.3f, 0.1f, 0.45f);
                var col = c.q >= 1f ? new Color(0.45f, 0.95f, 0.35f, a) : new Color(0.95f, 0.85f, 0.3f, a);
                Fill(new Rect(Mathf.Round(v.x - sz / 2 + 1), Mathf.Round(v.y - sz / 2 + 1), Mathf.Max(1, sz - 2), Mathf.Max(1, sz - 2)), col);
            }
            // Info am Mauszeiger
            if (!gm.HoverValid || !WorldToVirtual(new Vector3(gm.HoverX + 0.5f, gm.HoverY + 0.5f, 0), out var hv)) return;
            // Angaben immer im Vergleich zu einem normalen Gebäude: "+12 %" = 12 % mehr Ertrag als üblich
            var lines = new List<string>(); Color lc;
            if (G.CanPlace(s, t, gm.HoverX, gm.HoverY, out string why))
            {
                float site = G.SiteBonus(t, gm.HoverX, gm.HoverY), way = G.DistanceFactor(s, gm.HoverX, gm.HoverY);
                string fn = Game.SiteFactorName(t);
                bool prod = fn != null || way < 0.999f;
                if (prod)
                {
                    lines.Add(Loc.F("Ertrag hier: {0} ({1})", (Game.Rating(site * way)), (Game.Diff(site * way))));
                    var parts = new List<string>();
                    if (fn != null) parts.Add($"{fn}: {Game.Diff(site)}");
                    if (way < 0.999f) parts.Add(Loc.F("langer Weg: {0}", (Game.Diff(way))));
                    lines.Add(string.Join(", ", parts));
                }
                else lines.Add(Loc.T("Guter Bauplatz"));
                if (G.Map.owner[WorldMap.Idx(gm.HoverX, gm.HoverY)] < 0) lines.Add(Loc.T("Niemandsland - Nachbarn könnten Ärger machen"));
                lc = Palette.UIGood;
            }
            else { lines.Add(why); lc = Palette.UIBad; }
            float lw = 0; foreach (var l in lines) lw = Mathf.Max(lw, PixelFont.Width(l));
            var pr = new Rect(Mathf.Round(hv.x + 10), Mathf.Round(hv.y + 8), lw + 8, lines.Count * 10 + 3);
            if (pr.xMax > VW - 4) pr.x = VW - 4 - pr.width;
            if (pr.yMax > VH - 4) pr.y = hv.y - pr.height - 8;
            Fill(pr, new Color(Palette.UIPanelDark.r, Palette.UIPanelDark.g, Palette.UIPanelDark.b, 0.9f));
            for (int i = 0; i < lines.Count; i++) Text(pr.x + 4, pr.y + 2 + i * 10, lines[i], i == 0 ? lc : Palette.UIText);
        }

        // ---- Problem-Symbole über Gebäuden ----
        public static bool ProblemsOn
        {
            get => PlayerPrefs.GetInt("pk_problems", 1) == 1;
            set => PlayerPrefs.SetInt("pk_problems", value ? 1 : 0);
        }

        void DrawProblemIcons()
        {
            if (!ProblemsOn || gm.InMenu || G.Problems.Count == 0) return;
            float zoom = gm.CamCtrl.Zoom;
            if (zoom < 1f) return;
            float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 3f) * 1.5f);
            foreach (var p in G.Problems)
            {
                if (!WorldToVirtual(new Vector3(p.X, p.Y, 0), out var v)) continue;
                var r = new Rect(Mathf.Round(v.x - 8), Mathf.Round(v.y - 16) + bob, 16, 16);
                bool severe = p.Kind == ProblemKind.Hunger || p.Kind == ProblemKind.Unhappy || p.Kind == ProblemKind.Bandits;
                // Sprechblase: Rand, Farbe, kleiner Zipfel nach unten, Symbol
                Fill(new Rect(r.x + 1, r.y + 1, r.width, r.height), Palette.UIShadow);
                Fill(r, Palette.UIPanelDark);
                Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), severe ? new Color(0.78f, 0.22f, 0.18f) : new Color(0.93f, 0.62f, 0.18f));
                Fill(new Rect(r.center.x - 2, r.yMax, 4, 1), Palette.UIPanelDark);
                Fill(new Rect(r.center.x - 1, r.yMax + 1, 2, 1), Palette.UIPanelDark);
                Icon(new Rect(r.x + 2, r.y + 2, 12, 12), Game.ProblemIcon(p.Kind), Color.white);
                // Erklärung: am PC beim Überfahren, am Tablet beim Antippen (die Karte darunter reagiert trotzdem)
                if (!IsOverVirtual(Local(ptr)) && Hover(r) && !PKInput.UsingTouch) { tooltip = Game.ProblemText(p); tipPos = ptr; }
                if (TappedPassive(r) && !IsOverVirtual(clickPos)) PinTip(Game.ProblemText(p));
            }
        }

        // ---- Befehlsmenü für Heere ----
        void DrawOrderPath(float zoom)
        {
            var path = gm.OrderPath;
            float dot = Mathf.Max(2, Mathf.Round(zoom * 2));
            var col = new Color(Palette.UIGood.r, Palette.UIGood.g, Palette.UIGood.b, 0.95f);
            for (int i = 1; i < path.Count; i++)
            {
                if (!WorldToVirtual(new Vector3(WorldMap.X(path[i]) + 0.5f, WorldMap.Y(path[i]) + 0.5f, 0), out var v)) continue;
                Fill(new Rect(Mathf.Round(v.x - dot / 2) + 1, Mathf.Round(v.y - dot / 2) + 1, dot, dot), Palette.UIShadow);
                Fill(new Rect(Mathf.Round(v.x - dot / 2), Mathf.Round(v.y - dot / 2), dot, dot), col);
            }
            if (WorldToVirtual(new Vector3(gm.OrderX + 0.5f, gm.OrderY + 0.5f, 0), out var tv))
            {
                float sz = 16f * zoom / S;
                Brackets(new Rect(tv.x - sz / 2, tv.y - sz / 2, sz, sz), 3, Palette.UIGood);
            }
        }

        void DrawOrderMenu()
        {
            var a = G.A(gm.OrderArmy);
            if (a == null) { gm.CloseOrderMenu(); return; }
            var opts = gm.OrderMenu;
            float w = Mathf.Min(230, VW - 16);
            float rowH = 27;
            float h = 32 + opts.Count * rowH + 18;
            Vector2 anchor;
            if (!WorldToVirtual(new Vector3(gm.OrderX + 0.5f, gm.OrderY + 0.5f, 0), out anchor)) anchor = new Vector2(VW / 2, VH / 2);
            float x = anchor.x + 14, y = anchor.y - h / 2;
            if (x + w > VW - 6) x = anchor.x - w - 14;
            x = Mathf.Clamp(x, 6, VW - w - 6); y = Mathf.Clamp(y, 40, VH - h - 6);
            var r = new Rect(Mathf.Round(x), Mathf.Round(y), w, h);
            Panel(r);
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, 26), Palette.UIPanelDark);
            string title = gm.OrderTarget ?? "";
            if (PixelFont.Width(title) > w - 30) title = title.Substring(0, Mathf.Max(4, title.Length * (int)(w - 30) / Mathf.Max(1, PixelFont.Width(title)) - 1)) + ".";
            Text(r.x + 6, r.y + 4, title, Palette.UIText);
            int d = Mathf.Max(1, Mathf.CeilToInt(gm.OrderDays));
            Text(r.x + 6, r.y + 15, Loc.F("{0}: Marsch ~{1} {2}", (a.name), (d), ((d == 1 ? Loc.T("Tag") : Loc.T("Tage")))), Palette.UITextDim);
            if (Button(new Rect(r.xMax - 16, r.y + 3, 13, 12), "", true, Loc.T("Abbrechen"), "close")) { gm.CloseOrderMenu(); return; }
            float oy = r.y + 32;
            foreach (var o in opts)
            {
                if (Button(new Rect(r.x + 6, oy, w - 12, 14), o.Label, o.Enabled, o.Tip, o.Icon)) { gm.ExecuteOrder(o); return; }
                if (!string.IsNullOrEmpty(o.Tip))
                {
                    var tl = PixelFont.Wrap(o.Tip, (int)w - 14);
                    Text(r.x + 8, oy + 16, tl[0] + (tl.Count > 1 ? " ..." : ""), Palette.UITextDim);
                }
                oy += rowH;
            }
            if (Button(new Rect(r.x + 6, r.yMax - 17, w - 12, 13), Loc.T("Abbrechen"))) gm.CloseOrderMenu();
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
                hint = (PKInput.UsingTouch ? Loc.T("Tippt") : Loc.T("Klickt")) + Loc.T(" auf das Ziel für das Heer: Gelände, eigene Stadt oder Feind  (") + (PKInput.UsingTouch ? Loc.T("Zurück") : Loc.T("Esc")) + Loc.T(": abbrechen)");
            }
            else if (gm.CurrentMode == GameManager.Mode.PlaceBuilding)
            {
                var d = Buildings.Get(gm.PlaceType);
                hint = (PKInput.UsingTouch ? Loc.T("Tippt zweimal auf ein Feld für: ") : Loc.T("Linksklick auf ein Feld für: ")) + d.Name
                     + (PKInput.UsingTouch ? Loc.T("  (Zurück: abbrechen)") : Loc.T("  (Esc/Rechtsklick: abbrechen, Shift: mehrere)"))
                     + Loc.T("\nGrün = mehr Ertrag als üblich, Gelb = weniger. Die Angabe vergleicht mit einem normalen ") + d.Name + ".";
            }
            else if (gm.CurrentMode == GameManager.Mode.MoveBuilding)
            {
                hint = (PKInput.UsingTouch ? Loc.T("Tippt zweimal auf den neuen Platz für: ") : Loc.T("Linksklick auf den neuen Platz für: ")) + Buildings.Get(gm.PlaceType).Name
                     + (PKInput.UsingTouch ? Loc.T("  (Zurück: abbrechen)") : Loc.T("  (Esc/Rechtsklick: abbrechen)"));
            }
            else if (gm.CurrentMode == GameManager.Mode.PlaceSettlers)
            {
                hint = PKInput.UsingTouch ? Loc.T("Tippt auf den Ort für die neue Siedlung  (Zurück: abbrechen)") : Loc.T("Linksklick: Ort für die neue Siedlung wählen  (Esc: abbrechen)");
                if (gm.HoverValid && !G.CanFoundAt(G.PlayerId, gm.HoverX, gm.HoverY, out string why)) hint += "\n" + why;
            }
            else
            {
                var a = G.A(gm.SelArmy);
                if (a != null && a.faction == G.PlayerId && a.path.Count == 0)
                    hint = PKInput.UsingTouch
                        ? (a.settlers > 0 && a.units.Sum() == 0 ? Loc.T("Tippt auf den Ort zum Siedeln") : Loc.T("Tippt auf ein Ziel. Lange drücken auf eine Stadt: Überfall"))
                        : (a.settlers > 0 && a.units.Sum() == 0 ? Loc.T("Rechtsklick: Ort zum Siedeln wählen") : Loc.T("Rechtsklick: Ziel – Gelände, eigene Stadt oder Feind (Shift: Überfall)"));
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
