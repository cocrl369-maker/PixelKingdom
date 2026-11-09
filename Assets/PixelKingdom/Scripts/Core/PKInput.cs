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
        static int backend = -1;          // -1 unbekannt, 0 alter Input Manager, 1 neues Input System
        static bool legacyBroken;

        /// <summary>Wird das neue Input System benutzt?</summary>
        public static bool UseNewSystem
        {
            get
            {
                if (backend < 0) backend = NewSys.Available ? 1 : 0;
                return backend == 1;
            }
        }

        static void LegacyFailed()
        {
            if (legacyBroken) return;
            legacyBroken = true;
            if (NewSys.Available) backend = 1;
            Debug.LogWarning("Pixel Kingdom: Der alte Input Manager ist abgeschaltet. Bitte unter Project Settings > Player > Active Input Handling 'Input System Package (New)' oder 'Both' wählen und das Paket 'Input System' installieren.");
        }

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
            if (legacyBroken) return false;
            try { return f(); } catch (InvalidOperationException) { LegacyFailed(); return false; }
        }

        public static bool Key(PKKey k)
        {
            if (UseNewSystem) return NewSys.Key(NewKeyName(k), 0) || (k == PKKey.Shift && NewSys.Key("RightShift", 0));
            return Legacy(() => Input.GetKey(Map(k)) || (k == PKKey.Shift && Input.GetKey(KeyCode.RightShift)));
        }

        public static bool KeyDown(PKKey k)
        {
            if (UseNewSystem) return NewSys.Key(NewKeyName(k), 1);
            return Legacy(() => Input.GetKeyDown(Map(k)));
        }

        public static Vector2 MousePos
        {
            get
            {
                if (UseNewSystem) return NewSys.MousePos();
                if (legacyBroken) return Vector2.zero;
                try { return Input.mousePosition; } catch (InvalidOperationException) { LegacyFailed(); return Vector2.zero; }
            }
        }

        public static bool MouseDown(int b) => UseNewSystem ? NewSys.MouseButton(b, 1) : Legacy(() => Input.GetMouseButtonDown(b));
        public static bool MouseUp(int b) => UseNewSystem ? NewSys.MouseButton(b, 2) : Legacy(() => Input.GetMouseButtonUp(b));
        public static bool Mouse(int b) => UseNewSystem ? NewSys.MouseButton(b, 0) : Legacy(() => Input.GetMouseButton(b));

        public static float Scroll
        {
            get
            {
                if (UseNewSystem) return NewSys.ScrollY() / 120f;
                if (legacyBroken) return 0f;
                try { return Input.mouseScrollDelta.y; } catch (InvalidOperationException) { LegacyFailed(); return 0f; }
            }
        }

        // ------------------------------------------------------------------
        /// <summary>Zugriff auf das neue Input System per Reflection (kein Kompilier-Bezug zum Paket nötig).</summary>
        static class NewSys
        {
            static bool? available;
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

            public static bool Available
            {
                get
                {
                    if (available.HasValue) return available.Value;
                    try
                    {
                        tKeyboard = Find("UnityEngine.InputSystem.Keyboard");
                        tMouse = Find("UnityEngine.InputSystem.Mouse");
                        tTouchscreen = Find("UnityEngine.InputSystem.Touchscreen");
                        tKey = Find("UnityEngine.InputSystem.Key");
                        bool ok = tKeyboard != null && tMouse != null && tTouchscreen != null && tKey != null;
                        // Ist das neue System auch wirklich aktiv? (Bei "Input Manager (Old)" gibt es keine Geräte.)
                        if (ok) ok = Current(tMouse) != null || Current(tTouchscreen) != null || Current(tKeyboard) != null;
                        if (ok) keyboardIndexer = tKeyboard.GetProperty("Item", new[] { tKey });
                        available = ok;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Pixel Kingdom: Neues Input System nicht nutzbar: " + e.Message);
                        available = false;
                    }
                    return available.Value;
                }
            }

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

            public static Vector2 MousePos()
            {
                try
                {
                    var m = Current(tMouse);
                    var v = Read(P(m, "position"));
                    return v is Vector2 p ? p : Vector2.zero;
                }
                catch { return Vector2.zero; }
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
                catch { }
            }
        }
    }
}
