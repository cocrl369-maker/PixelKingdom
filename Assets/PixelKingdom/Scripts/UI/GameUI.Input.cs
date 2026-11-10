using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Eigene Zeiger-Verarbeitung für die Oberfläche. Finger und Maus werden direkt gelesen
    /// (nicht über Unitys IMGUI-Mausereignisse, die auf Android mit veralteter Position ankommen können).
    /// Ein Klick zählt beim Loslassen, wenn nicht gewischt wurde. Wischen scrollt Listen und die Gebäudeleiste.
    /// Pro Bild läuft zuerst ein Treffer-Durchlauf (oberste Ebene zuerst), danach wird gezeichnet.
    /// </summary>
    public partial class GameUI
    {
        Vector2 ptr, ptrPrev, pressStart;     // virtuelle Koordinaten (ganzer Bildschirm)
        bool ptrDown, dragging, multiTouch;
        bool clickPending, clickUsed;
        Vector2 clickPos;
        Vector2 dragDelta;
        float wheel;
        bool hitPass;                         // true während des Treffer-Durchlaufs
        float lastDeviceInput = -100f;        // wann zuletzt Maus/Finger direkt gelesen wurden
        bool clickFromGui; float clickTime;   // Ersatz-Klick aus Unitys GUI-Ereignissen
        int hitFrame = -1;
        int pointerSeq, hitSeq = -1;          // Treffer-Durchlauf genau einmal nach jeder Zeiger-Aktualisierung
        // Am PC: Maus direkt aus Unitys GUI-Ereignissen (gleiche Koordinaten wie die gezeichneten Knöpfe)
        Vector2 gPtr, gPressPos, gReleasePos;
        bool gDown, gPendPress, gPendRelease, gSeen, gDragged;
        public int DbgDowns, DbgUps, DbgClicks, DbgHits; public Vector2 DbgLast;
        float gWheel;

        /// <summary>Am PC mit Maus: Oberfläche nutzt die GUI-Ereignisse (sobald welche angekommen sind).</summary>
        bool DesktopGuiPointer => !Application.isMobilePlatform && !PKInput.TouchSim && !PKInput.UsingTouch && gSeen;

        /// <summary>Wird in OnGUI nach dem Setzen von GUI.matrix aufgerufen – Positionen sind dann virtuelle Koordinaten.</summary>
        void CaptureGuiPointer(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    gSeen = true;
                    if (e.button == 0) { gPendPress = true; gPressPos = e.mousePosition; gDown = true; gDragged = false; DbgDowns++; }
                    gPtr = e.mousePosition; break;
                case EventType.MouseUp:
                    gSeen = true;
                    if (e.button == 0)
                    {
                        // Klick = Loslassen nahe der Druckstelle. Bewertet nur mit den beiden Klick-Ereignissen selbst.
                        if ((e.mousePosition - gPressPos).magnitude > DragThresholdVirtual * 2f) gDragged = true;
                        gPendRelease = true; gReleasePos = e.mousePosition; gDown = false; DbgUps++; DbgLast = e.mousePosition;
                    }
                    gPtr = e.mousePosition; break;
                case EventType.MouseDrag:
                    gPtr = e.mousePosition;
                    if (gDown && (gPtr - gPressPos).magnitude > DragThresholdVirtual) gDragged = true;
                    break;
                case EventType.MouseMove: case EventType.Repaint:
                    if (!gDown) gPtr = e.mousePosition;   // nur fürs Hervorheben; während gedrückt zählen nur echte Mausereignisse
                    break;
                case EventType.ScrollWheel:
                    gSeen = true; gWheel += -e.delta.y / 3f; gPtr = e.mousePosition; break;
            }
        }
        readonly List<Vector2> groupStack = new List<Vector2>();
        Vector2 groupOffset;

        float DragThresholdVirtual => Mathf.Max(5f, (Screen.dpi > 0 ? Screen.dpi : 160f) * 0.06f / Mathf.Max(1, S));

        void UpdatePointer()
        {
            // Ein Klick, den der Treffer-Durchlauf noch nicht gesehen hat, bleibt erhalten – unabhängig davon,
            // in welcher Reihenfolge Unity Update und OnGUI aufruft.
            bool unseen = clickPending && hitSeq != pointerSeq && Time.unscaledTime - clickTime < 0.5f;
            pointerSeq++;
            bool keepGuiClick = unseen || (clickPending && clickFromGui && Time.unscaledTime - clickTime < 0.3f);
            if (!keepGuiClick) { clickPending = false; clickFromGui = false; }
            clickUsed = false; dragDelta = Vector2.zero;
            wheel = PKInput.UsingTouch ? 0f : PKInput.Scroll;
            bool pressed, released, down; Vector2 sp;
            if (DesktopGuiPointer)
            {
                // Ereignisse aus OnGUI des letzten Bildes übernehmen
                pressed = gPendPress; released = gPendRelease; down = gDown || pressed;
                gPendPress = gPendRelease = false;
                if (gWheel != 0f) wheel = gWheel;
                gWheel = 0f;
                multiTouch = false;
                lastDeviceInput = Time.unscaledTime;
                ptrPrev = ptr;
                if (pressed)
                {
                    ptr = gPressPos; ptrPrev = ptr; pressStart = ptr; dragging = false; ptrDown = true;
                    if (IsOverVirtual(ptr) || IsBlocking) LastUIPress = Time.unscaledTime;
                }
                ptr = released ? gReleasePos : gPtr;
                dragging = gDragged;
                if (ptrDown && dragging) dragDelta = ptr - ptrPrev;
                if (released)
                {
                    if (!gDragged) { clickPending = true; clickPos = gReleasePos; clickTime = Time.unscaledTime; DbgClicks++; }
                    ptrDown = false;
                }
                if (!down && !released) ptrDown = false;
                return;
            }
            if (PKInput.UsingTouch)
            {
                int n = PKInput.TouchCount;
                if (n >= 2) multiTouch = true;
                if (n > 0)
                {
                    var t = PKInput.GetTouch(0);
                    sp = t.Pos; pressed = t.Phase == 0; released = t.Phase == 2; down = !released;
                }
                else { ptrDown = false; multiTouch = false; return; }
            }
            else
            {
                sp = PKInput.MousePos;
                pressed = PKInput.MouseDown(0); released = PKInput.MouseUp(0); down = PKInput.Mouse(0);
                multiTouch = false;
            }
            if (pressed || released || down) lastDeviceInput = Time.unscaledTime;
            if (keepGuiClick) return;
            ptrPrev = ptr;
            ptr = ScreenToVirtual(sp);
            if (pressed)
            {
                ptrPrev = ptr; pressStart = ptr; dragging = false; ptrDown = true;
                if (!(PKInput.UsingTouch && PKInput.TouchCount >= 2)) multiTouch = false;
                if (IsOverVirtual(ptr) || IsBlocking) LastUIPress = Time.unscaledTime;
            }
            if (ptrDown && !dragging && (ptr - pressStart).magnitude > DragThresholdVirtual) dragging = true;
            if (ptrDown && dragging) dragDelta = ptr - ptrPrev;
            if (released)
            {
                if (ptrDown && !dragging && !multiTouch) { clickPending = true; clickPos = ptr; clickTime = Time.unscaledTime; }
                ptrDown = false;
            }
            if (!down && !released) ptrDown = false;
        }

        /// <summary>
        /// Sicherheitsnetz: Liefert das Eingabesystem gar nichts (z.B. falsche Einstellung "Active Input Handling"),
        /// werden Unitys eigene GUI-Mausereignisse als Klick genommen.
        /// </summary>
        void GuiEventFallback(Event e)
        {
            if (Time.unscaledTime - lastDeviceInput < 1f) return;   // Eingabesystem funktioniert – nichts tun
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                ptr = pressStart = e.mousePosition;
                if (IsOverVirtual(ptr) || IsBlocking) LastUIPress = Time.unscaledTime;
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                ptr = e.mousePosition;
                clickPending = true; clickFromGui = true; clickPos = ptr; clickTime = Time.unscaledTime;
            }
            else if (e.type == EventType.ScrollWheel) wheel = -e.delta.y / 3f;
        }

        /// <summary>Zeigerposition im aktuellen Gruppen-Koordinatensystem.</summary>
        Vector2 Local(Vector2 v) => v - groupOffset;

        void BeginGroup(Rect r)
        {
            groupStack.Add(groupOffset);
            groupOffset += r.position;
            GUI.BeginGroup(r);
        }

        void EndGroup()
        {
            GUI.EndGroup();
            if (groupStack.Count > 0) { groupOffset = groupStack[groupStack.Count - 1]; groupStack.RemoveAt(groupStack.Count - 1); }
            else groupOffset = Vector2.zero;
        }

        /// <summary>Wurde dieses Rechteck angeklickt/angetippt? (nur im Treffer-Durchlauf, jeder Klick zählt einmal)</summary>
        bool Clicked(Rect r, int button = 0)
        {
            if (!hitPass || !clickPending || clickUsed || button != 0) return false;
            if (!r.Contains(Local(clickPos))) return false;
            clickUsed = true;
            DbgHits++;
            LastUIPress = Time.unscaledTime;
            AudioManager.Click();
            return true;
        }

        /// <summary>Tippen auf ein Rechteck, ohne den Klick zu verbrauchen (für Info-Kärtchen).</summary>
        bool TappedPassive(Rect r) => hitPass && clickPending && !clickUsed && r.Contains(Local(clickPos));

        bool Hover(Rect r) => (!PKInput.UsingTouch || ptrDown) && r.Contains(Local(ptr));

        /// <summary>Liste scrollen: Mausrad oder Wischen (in der Liste begonnen).</summary>
        float Scroll(Rect r, float value, float max, bool horizontal = false)
        {
            if (hitPass)
            {
                if (wheel != 0 && r.Contains(Local(ptr))) { value -= wheel * 18f; wheel = 0; }
                if (dragging && ptrDown && r.Contains(Local(pressStart))) value -= horizontal ? dragDelta.x : dragDelta.y;
            }
            return Mathf.Clamp(value, 0, Mathf.Max(0, max));
        }

        /// <summary>Wird gerade innerhalb dieses Rechtecks gezogen? (z.B. Minikarte)</summary>
        bool HeldIn(Rect r) => hitPass && ptrDown && r.Contains(Local(pressStart)) && r.Contains(Local(ptr));
    }
}
