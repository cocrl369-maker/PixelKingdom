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
        int hitFrame = -1;
        readonly List<Vector2> groupStack = new List<Vector2>();
        Vector2 groupOffset;

        float DragThresholdVirtual => Mathf.Max(5f, (Screen.dpi > 0 ? Screen.dpi : 160f) * 0.06f / Mathf.Max(1, S));

        void UpdatePointer()
        {
            clickPending = false; clickUsed = false; dragDelta = Vector2.zero;
            wheel = PKInput.UsingTouch ? 0f : PKInput.Scroll;
            bool pressed, released, down; Vector2 sp;
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
                if (ptrDown && !dragging && !multiTouch) { clickPending = true; clickPos = ptr; }
                ptrDown = false;
            }
            if (!down && !released) ptrDown = false;
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
