using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PixelKingdom
{
    public enum PKKey { W, A, S, D, Q, E, Up, Down, Left, Right, Space, Escape, Tab, Alpha1, Alpha2, Alpha3, B, R, L, H, P, G, M, F1, F5, F9, Shift, Delete, Plus, Minus }

    /// <summary>
    /// Kapselt die Eingabe – ohne feste Abhängigkeit von einem Paket. Zur Laufzeit wird erkannt, was es gibt:
    /// das neue Input System (Paket "com.unity.inputsystem", in Unity 6 Standard) wird über Reflection angesprochen,
    /// sonst der alte Input Manager. So kompiliert der Ordner in JEDEM Unity-Projekt, egal welche
    /// Einstellung bei "Active Input Handling" gewählt ist.
    /// </summary>
    public static partial class PKInput
    {
        // ------------------------------------------------------------------
        //  Drei Quellen, in dieser Reihenfolge – bei JEDER Abfrage neu entschieden, nichts wird dauerhaft gemerkt:
        //  1. neues Input System (falls Paket vorhanden und ein Gerät da ist)
        //  2. alter Input Manager (falls in "Active Input Handling" erlaubt)
        //  3. Unitys GUI-Ereignisse (funktionieren immer, egal welche Projekteinstellung)
        //  So kann ein frisches Unity-Projekt nie "taub" sein.
        // ------------------------------------------------------------------
        static int legacyState = -1;      // -1 ungeprüft, 0 abgeschaltet, 1 nutzbar
        static bool warned;

        static bool LegacyOk
        {
            get
            {
                if (legacyState < 0)
                {
                    try { Input.GetKey(KeyCode.Space); var _ = Input.mousePosition; legacyState = 1; }
                    catch (InvalidOperationException) { legacyState = 0; }
                    catch (Exception) { legacyState = 0; }
                }
                return legacyState == 1;
            }
        }

        static void LegacyFailed()
        {
            legacyState = 0;
            if (warned) return;
            warned = true;
            Debug.Log("Pixel Kingdom: Alter Input Manager ist abgeschaltet – es wird das neue Input System bzw. Unitys GUI-Ereignisse benutzt.");
        }

        /// <summary>Welche Quelle gerade für Maus/Tastatur benutzt wird (nur zur Anzeige/Fehlersuche).</summary>
        public static string Backend => NewSys.HasMouse ? "InputSystem" : LegacyOk ? "InputManager" : "GUI";

        static KeyCode Map(PKKey k)
        {
            switch (k)
            {
                case PKKey.W: return KeyCode.W;
                case PKKey.A: return KeyCode.A;
                case PKKey.S: return KeyCode.S;
                case PKKey.D: return KeyCode.D;
                case PKKey.Q: return KeyCode.Q;
                case PKKey.E: return KeyCode.E;
                case PKKey.Up: return KeyCode.UpArrow;
                case PKKey.Down: return KeyCode.DownArrow;
                case PKKey.Left: return KeyCode.LeftArrow;
                case PKKey.Right: return KeyCode.RightArrow;
                case PKKey.Space: return KeyCode.Space;
                case PKKey.Escape: return KeyCode.Escape;
                case PKKey.Tab: return KeyCode.Tab;
                case PKKey.Alpha1: return KeyCode.Alpha1;
                case PKKey.Alpha2: return KeyCode.Alpha2;
                case PKKey.Alpha3: return KeyCode.Alpha3;
                case PKKey.B: return KeyCode.B;
                case PKKey.R: return KeyCode.R;
                case PKKey.L: return KeyCode.L;
                case PKKey.H: return KeyCode.H;
                case PKKey.P: return KeyCode.P;
                case PKKey.G: return KeyCode.G;
                case PKKey.M: return KeyCode.M;
                case PKKey.F1: return KeyCode.F1;
                case PKKey.F5: return KeyCode.F5;
                case PKKey.F9: return KeyCode.F9;
                case PKKey.Shift: return KeyCode.LeftShift;
                case PKKey.Delete: return KeyCode.Delete;
                case PKKey.Plus: return KeyCode.KeypadPlus;
                case PKKey.Minus: return KeyCode.KeypadMinus;
            }
            return KeyCode.None;
        }

        // Namen der Tasten im neuen Input System (Enum UnityEngine.InputSystem.Key)
        static string NewKeyName(PKKey k)
        {
            switch (k)
            {
                case PKKey.Up: return "UpArrow";
                case PKKey.Down: return "DownArrow";
                case PKKey.Left: return "LeftArrow";
                case PKKey.Right: return "RightArrow";
                case PKKey.Alpha1: return "Digit1";
                case PKKey.Alpha2: return "Digit2";
                case PKKey.Alpha3: return "Digit3";
                case PKKey.Shift: return "LeftShift";
                case PKKey.Plus: return "NumpadPlus";
                case PKKey.Minus: return "NumpadMinus";
            }
            return k.ToString();
        }

        static bool Legacy(Func<bool> f)
        {
            if (!LegacyOk) return false;
            try { return f(); } catch (InvalidOperationException) { LegacyFailed(); return false; }
        }

        public static bool Key(PKKey k)
        {
            if (NewSys.HasKeyboard) return NewSys.Key(NewKeyName(k), 0) || (k == PKKey.Shift && NewSys.Key("RightShift", 0));
            if (LegacyOk) return Legacy(() => Input.GetKey(Map(k)) || (k == PKKey.Shift && Input.GetKey(KeyCode.RightShift)));
            return Gui.KeyHeld(Map(k)) || (k == PKKey.Shift && Gui.KeyHeld(KeyCode.RightShift));
        }

        public static bool KeyDown(PKKey k)
        {
            if (NewSys.HasKeyboard) return NewSys.Key(NewKeyName(k), 1);
            if (LegacyOk) return Legacy(() => Input.GetKeyDown(Map(k)));
            return Gui.KeyPressed(Map(k));
        }

        public static Vector2 MousePos
        {
            get
            {
                if (NewSys.HasMouse && NewSys.MousePos(out var p)) return p;
                if (LegacyOk) { try { return Input.mousePosition; } catch (InvalidOperationException) { LegacyFailed(); } }
                return Gui.Pos;
            }
        }

        /// <summary>mode: 0 gehalten, 1 gerade gedrückt, 2 gerade losgelassen</summary>
        static bool MouseQuery(int b, int mode)
        {
            if (NewSys.HasMouse) return NewSys.MouseButton(b, mode);
            if (LegacyOk) return Legacy(() => mode == 0 ? Input.GetMouseButton(b) : mode == 1 ? Input.GetMouseButtonDown(b) : Input.GetMouseButtonUp(b));
            return Gui.Button(b, mode);
        }

        public static bool MouseDown(int b) => MouseQuery(b, 1);
        public static bool MouseUp(int b) => MouseQuery(b, 2);
        public static bool Mouse(int b) => MouseQuery(b, 0);

        public static float Scroll
        {
            get
            {
                if (NewSys.HasMouse) return NewSys.ScrollY() / 120f;
                if (LegacyOk) { try { return Input.mouseScrollDelta.y; } catch (InvalidOperationException) { LegacyFailed(); } }
                return Gui.ScrollY;
            }
        }

        /// <summary>
        /// Muss am Anfang von OnGUI aufgerufen werden (vor dem Setzen von GUI.matrix).
        /// Sammelt Unitys GUI-Ereignisse als letzte Eingabequelle.
        /// </summary>
        public static void FeedGuiEvent(Event e) => Gui.Feed(e);

        // ------------------------------------------------------------------
        /// <summary>
        /// Eingabe aus Unitys GUI-Ereignissen. Ereignisse aus OnGUI von Bild N gelten in Bild N+1
        /// (Update läuft vor OnGUI) – so sehen alle Abfragen eines Bildes denselben Zustand.
        /// </summary>
        static class Gui
        {
            static readonly bool[] held = new bool[3];
            static readonly bool[] pendDown = new bool[3], pendUp = new bool[3];
            static readonly bool[] down = new bool[3], up = new bool[3];
            static readonly HashSet<KeyCode> keysHeld = new HashSet<KeyCode>();
            static readonly HashSet<KeyCode> pendKeys = new HashSet<KeyCode>(), keysPressed = new HashSet<KeyCode>();
            static float pendScroll, scroll;
            static Vector2 pos;
            static int latched = -1;

            static void Latch()
            {
                if (latched == Time.frameCount) return;
                latched = Time.frameCount;
                for (int i = 0; i < 3; i++) { down[i] = pendDown[i]; up[i] = pendUp[i]; pendDown[i] = pendUp[i] = false; }
                keysPressed.Clear(); foreach (var k in pendKeys) keysPressed.Add(k); pendKeys.Clear();
                scroll = pendScroll; pendScroll = 0f;
            }

            public static Vector2 Pos => pos;
            public static float ScrollY { get { Latch(); return scroll; } }
            public static bool KeyHeld(KeyCode k) => k != KeyCode.None && keysHeld.Contains(k);
            public static bool KeyPressed(KeyCode k) { Latch(); return k != KeyCode.None && keysPressed.Contains(k); }

            public static bool Button(int b, int mode)
            {
                if (b < 0 || b > 2) return false;
                Latch();
                return mode == 0 ? held[b] || down[b] : mode == 1 ? down[b] : up[b];
            }

            public static void Feed(Event e)
            {
                if (e == null) return;
                // Ohne gesetzte GUI.matrix ist mousePosition in Bildschirmpunkten (oben links = 0,0)
                var mp = e.mousePosition;
                if (e.isMouse || e.type == EventType.ScrollWheel || e.type == EventType.Repaint)
                    pos = new Vector2(mp.x, Screen.height - mp.y);
                int b = Mathf.Clamp(e.button, 0, 2);
                switch (e.type)
                {
                    case EventType.MouseDown: if (!held[b]) pendDown[b] = true; held[b] = true; break;
                    case EventType.MouseUp: if (held[b]) pendUp[b] = true; held[b] = false; break;
                    case EventType.ScrollWheel: pendScroll += -e.delta.y / 3f; break;
                    case EventType.KeyDown:
                        if (e.keyCode != KeyCode.None) { if (!keysHeld.Contains(e.keyCode)) pendKeys.Add(e.keyCode); keysHeld.Add(e.keyCode); }
                        break;
                    case EventType.KeyUp: keysHeld.Remove(e.keyCode); break;
                }
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) lastGuiInput = Time.unscaledTime;
            }

            public static float lastGuiInput = -100f;
        }

        // ------------------------------------------------------------------
        /// <summary>Zugriff auf das neue Input System per Reflection (kein Kompilier-Bezug zum Paket nötig).</summary>
        static class NewSys
        {
            static Type tKeyboard, tMouse, tTouchscreen, tKey;
            static readonly Dictionary<string, PropertyInfo> props = new Dictionary<string, PropertyInfo>();
            static readonly Dictionary<Type, MethodInfo> readValue = new Dictionary<Type, MethodInfo>();
            static readonly Dictionary<string, object> keyEnum = new Dictionary<string, object>();
            static PropertyInfo keyboardIndexer;

            static Type Find(string name)
            {
                var t = Type.GetType(name + ", Unity.InputSystem");
                if (t != null) return t;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(name);
                    if (t != null) return t;
                }
                return null;
            }

            static float nextTypeSearch;
            static bool typesOk;

            /// <summary>Sind die Typen des Pakets vorhanden? Wird bei Misserfolg alle paar Sekunden neu gesucht.</summary>
            static bool Types
            {
                get
                {
                    if (typesOk) return true;
                    if (Time.unscaledTime < nextTypeSearch) return false;
                    nextTypeSearch = Time.unscaledTime + 3f;
                    try
                    {
                        tKeyboard = Find("UnityEngine.InputSystem.Keyboard");
                        tMouse = Find("UnityEngine.InputSystem.Mouse");
                        tTouchscreen = Find("UnityEngine.InputSystem.Touchscreen");
                        tKey = Find("UnityEngine.InputSystem.Key");
                        typesOk = tKeyboard != null && tMouse != null && tTouchscreen != null && tKey != null;
                        if (typesOk) keyboardIndexer = tKeyboard.GetProperty("Item", new[] { tKey });
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Pixel Kingdom: Neues Input System nicht nutzbar: " + e.Message);
                        typesOk = false;
                    }
                    return typesOk;
                }
            }

            static bool failed;   // Reflection hat versagt (z.B. geänderte API) → dieses Backend nicht mehr benutzen
            static void Fail(Exception e)
            {
                if (failed) return;
                failed = true;
                Debug.LogWarning("Pixel Kingdom: Neues Input System per Reflection nicht lesbar, nutze Ersatz: " + e.Message);
            }

            static object SafeCurrent(Type t)
            {
                if (failed || !Types || t == null) return null;
                try { return Current(t); } catch (Exception e) { Fail(e); return null; }
            }

            /// <summary>Gibt es (gerade) eine Maus im neuen Input System?</summary>
            public static bool HasMouse => SafeCurrent(tMouse) != null;
            public static bool HasKeyboard => SafeCurrent(tKeyboard) != null && keyboardIndexer != null;
            public static bool HasTouchscreen => SafeCurrent(tTouchscreen) != null;

            static object Current(Type t)
            {
                var p = t.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                return p?.GetValue(null);
            }

            static object P(object o, string name)
            {
                if (o == null) return null;
                var t = o.GetType();
                string key = t.FullName + "." + name;
                if (!props.TryGetValue(key, out var pi))
                {
                    pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    props[key] = pi;
                }
                return pi?.GetValue(o);
            }

            static object Read(object control)
            {
                if (control == null) return null;
                var t = control.GetType();
                if (!readValue.TryGetValue(t, out var mi))
                {
                    mi = t.GetMethod("ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    readValue[t] = mi;
                }
                return mi?.Invoke(control, null);
            }

            static bool Button(object button, int mode)
            {
                if (button == null) return false;
                string n = mode == 0 ? "isPressed" : mode == 1 ? "wasPressedThisFrame" : "wasReleasedThisFrame";
                return P(button, n) is bool b && b;
            }

            /// <summary>mode: 0 gehalten, 1 gerade gedrückt</summary>
            public static bool Key(string name, int mode)
            {
                try
                {
                    var kb = Current(tKeyboard);
                    if (kb == null || keyboardIndexer == null) return false;
                    if (!keyEnum.TryGetValue(name, out var kv))
                    {
                        try { kv = Enum.Parse(tKey, name); } catch { kv = null; }
                        keyEnum[name] = kv;
                    }
                    if (kv == null) return false;
                    return Button(keyboardIndexer.GetValue(kb, new[] { kv }), mode);
                }
                catch { return false; }
            }

            public static bool MousePos(out Vector2 p)
            {
                p = Vector2.zero;
                try
                {
                    var v = Read(P(Current(tMouse), "position"));
                    if (v is Vector2 q) { p = q; return true; }
                    Fail(new Exception("Mouse.position.ReadValue lieferte nichts"));
                    return false;
                }
                catch (Exception e) { Fail(e); return false; }
            }

            /// <summary>mode: 0 gehalten, 1 gedrückt, 2 losgelassen</summary>
            public static bool MouseButton(int b, int mode)
            {
                try
                {
                    var m = Current(tMouse);
                    return Button(P(m, b == 0 ? "leftButton" : b == 1 ? "rightButton" : "middleButton"), mode);
                }
                catch { return false; }
            }

            public static float ScrollY()
            {
                try
                {
                    var v = Read(P(Current(tMouse), "scroll"));
                    return v is Vector2 p ? p.y : 0f;
                }
                catch { return 0f; }
            }

            public static bool Failed => failed;

            public static void ReadTouches(List<PKTouch> list)
            {
                try
                {
                    var ts = Current(tTouchscreen);
                    if (!(P(ts, "touches") is IEnumerable touchesEnum)) return;
                    foreach (var tc in touchesEnum)
                    {
                        var press = P(tc, "press");
                        bool pressed = Button(press, 0), began = Button(press, 1), ended = Button(press, 2);
                        if (!pressed && !ended) continue;
                        var pos = Read(P(tc, "position"));
                        var id = Read(P(tc, "touchId"));
                        list.Add(new PKTouch
                        {
                            Id = id is int i ? i : 0,
                            Pos = pos is Vector2 v ? v : Vector2.zero,
                            Phase = began ? 0 : ended ? 2 : 1,
                        });
                    }
                }
                catch (Exception e) { Fail(e); }
            }
        }
    }
}
