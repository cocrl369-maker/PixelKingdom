using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Kurze Einführung beim ersten Spielstart: hebt nacheinander die Bereiche der Oberfläche hervor
    /// und erklärt sie in ein, zwei Sätzen. Jederzeit über die Hilfe erneut aufrufbar.
    /// </summary>
    public partial class GameUI
    {
        int tutStep = -1;
        readonly Dictionary<string, Rect> marks = new Dictionary<string, Rect>();

        public bool TutorialActive => tutStep >= 0;

        public void StartTutorial()
        {
            tutStep = 0;
            Open = Window.None;
            pinTip = null;
        }

        /// <summary>Merkt sich, wo ein Bereich der Oberfläche gezeichnet wurde (für die Einführung).</summary>
        void Mark(string key, Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            marks[key] = marks.TryGetValue(key, out var old) ? Rect.MinMaxRect(Mathf.Min(old.xMin, r.xMin), Mathf.Min(old.yMin, r.yMin), Mathf.Max(old.xMax, r.xMax), Mathf.Max(old.yMax, r.yMax)) : r;
        }

        struct TutStep { public string Key, Title, Text; public TutStep(string k, string t, string x) { Key = k; Title = t; Text = x; } }

        TutStep[] Steps()
        {
            bool touch = PKInput.UsingTouch;
            string hover = touch ? "Tippt darauf" : "Fahrt mit der Maus darüber";
            string click = touch ? "Tippen" : "Ein Klick";
            return new[]
            {
                new TutStep(null, "Willkommen in Pixel Kingdom!", "Eine kurze Einführung in die Oberfläche – in 8 Schritten. Ihr könnt sie jederzeit überspringen und später in der Hilfe erneut ansehen."),
                new TutStep("resources", "Vorräte", $"Nahrung, Holz, Stein, Eisen, Gold, Waffen, dazu Bewohner, Zufriedenheit und Ansehen. Der kleine Balken zeigt, wie voll das Lager ist. {hover}, um zu sehen, wie viel pro Tag dazukommt."),
                new TutStep("rank", "Rang & Datum", $"Euer Rang (Dorf, Städtegemeinde, ... Königreich) und der aktuelle Tag. {click} öffnet die Reich-Übersicht: Dort seht ihr, was für den nächsten Aufstieg fehlt."),
                new TutStep("speed", "Zeit & Menü", "Pause und Tempo (1x, 2x, 5x). Das Zahnrad öffnet das Menü: Speichern, Farben, UI-Größe, Hauptmenü."),
                new TutStep("side", "Schnellzugriff", "Von oben nach unten: Reich, Heerführung (eure Heere befehligen), Diplomatie (andere Herrscher), Chronik (was geschah), Hilfe, zur Hauptstadt springen, Farbpalette."),
                new TutStep("quest", "Aufgaben", "Die Ziele des aktuellen Kapitels. Ihr bestimmt das Tempo. Darunter: die Friedenszeit (niemand greift euch an) und später der Druck des Kaiserreichs."),
                new TutStep("map", "Weltkarte", $"{click} springt an diese Stelle. Unerforschtes Land ist Pergament – schickt Späher los, um es zu entdecken."),
                new TutStep("panel", "Euer Dorf", "Hier verwaltet ihr eine Siedlung. Übersicht: Bewohner, Zufriedenheit, Steuern, Rationen und der Verwalter, der automatisch baut. Dazu die Reiter Bauen, Truppen und Markt."),
                new TutStep("panel", "Bauen", $"Jede Karte ist ein Gebäude. Darunter stehen die Kosten (N = Nahrung, H = Holz, S = Stein, E = Eisen, G = Gold). Graue Karten sind noch gesperrt – {hover.ToLower()}, um zu sehen warum."),
                new TutStep(null, "Los geht's!", "Baut zuerst einen Holzfäller, Bauernhöfe und Wohnhäuser. Die Hilfe (F1) erklärt alles Weitere. Viel Erfolg!"),
            };
        }

        void DrawTutorial()
        {
            var steps = Steps();
            if (tutStep >= steps.Length) { EndTutorial(); return; }
            var st = steps[tutStep];

            // Für die Dorf-Schritte das eigene Dorf öffnen
            if (st.Key == "panel")
            {
                var cap = G.Capital(G.Player);
                if (cap != null && gm.SelSettlement != cap.id) gm.SelectSettlement(cap);
                SettlementTab = st.Title == "Bauen" ? 1 : 0;
            }

            modalThisFrame = true; modalNow = true;
            rects.Add(new Rect(0, 0, VW, VH));
            var dim = new Color(0.08f, 0.04f, 0.02f, 0.6f);
            bool has = st.Key != null && marks.TryGetValue(st.Key, out _);
            Rect hl = has ? marks[st.Key] : new Rect(VW / 2, VH / 2, 0, 0);
            if (has)
            {
                hl = new Rect(hl.x - 3, hl.y - 3, hl.width + 6, hl.height + 6);
                // Abdunkeln mit Aussparung
                Fill(new Rect(0, 0, VW, hl.y), dim);
                Fill(new Rect(0, hl.yMax, VW, VH - hl.yMax), dim);
                Fill(new Rect(0, hl.y, hl.x, hl.height), dim);
                Fill(new Rect(hl.xMax, hl.y, VW - hl.xMax, hl.height), dim);
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);
                var c = new Color(Palette.UIGood.r, Palette.UIGood.g, Palette.UIGood.b, pulse);
                Brackets(hl, 6, c);
                Fill(new Rect(hl.x, hl.y, hl.width, 1), c); Fill(new Rect(hl.x, hl.yMax - 1, hl.width, 1), c);
                Fill(new Rect(hl.x, hl.y, 1, hl.height), c); Fill(new Rect(hl.xMax - 1, hl.y, 1, hl.height), c);
            }
            else Fill(new Rect(0, 0, VW, VH), dim);

            // Erklärkasten neben dem markierten Bereich
            float w = Mathf.Min(270, VW - 20);
            var lines = PixelFont.Wrap(st.Text, (int)w - 16);
            float h = 30 + lines.Count * PixelFont.LineH + 26;
            float x, y;
            if (!has) { x = (VW - w) / 2; y = (VH - h) / 2; }
            else
            {
                x = Mathf.Clamp(hl.center.x - w / 2, 6, VW - w - 6);
                y = hl.yMax + 8;
                if (y + h > VH - 6) y = hl.y - h - 8;
                if (y < 6) { y = Mathf.Clamp(hl.center.y - h / 2, 6, VH - h - 6); x = hl.xMax + 8 + w > VW ? Mathf.Max(6, hl.x - w - 8) : hl.xMax + 8; }
            }
            var box = new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(w), Mathf.Round(h));
            Panel(box, Palette.UISand);
            Text(box.x + 8, box.y + 7, "__" + st.Title + "__", Palette.UIPanelDark);
            Text(box.xMax - 8 - PixelFont.Width($"{tutStep + 1}/{steps.Length}"), box.y + 7, $"{tutStep + 1}/{steps.Length}", Palette.UIPanelLight);
            float ly = box.y + 22;
            foreach (var l in lines) { Text(box.x + 8, ly, l, Palette.UIPanelDark); ly += PixelFont.LineH; }

            float by = box.yMax - 20;
            bool last = tutStep == steps.Length - 1;
            if (Button(new Rect(box.xMax - 78, by, 70, 15), last ? "Spielen!" : "Weiter", true, null, last ? "play" : "arrow")) { tutStep++; if (tutStep >= steps.Length) EndTutorial(); }
            if (tutStep > 0 && Button(new Rect(box.xMax - 152, by, 70, 15), "Zurück")) tutStep--;
            if (!last && Button(new Rect(box.x + 8, by, 80, 15), "Überspringen")) EndTutorial();
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Space))
            {
                tutStep++; if (tutStep >= steps.Length) EndTutorial();
                Event.current.Use();
            }
        }

        void EndTutorial()
        {
            tutStep = -1;
            SettlementTab = 0;
            G.S.story.Set("tutorial_done");
        }
    }
}
