using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelKingdom
{
    public enum PKKey { W, A, S, D, Q, E, Up, Down, Left, Right, Space, Escape, Tab, Alpha1, Alpha2, Alpha3, B, R, L, H, P, G, F1, F5, F9, Shift, Delete, Plus, Minus }

    /// <summary>
    /// Kapselt die Eingabe, damit das Spiel sowohl mit dem alten Input Manager
    /// als auch mit dem neuen Input System (Unity 6 Standard) läuft.
    /// </summary>
    public static partial class PKInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
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

        public static bool Key(PKKey k) => Input.GetKey(Map(k)) || (k == PKKey.Shift && Input.GetKey(KeyCode.RightShift));
        public static bool KeyDown(PKKey k) => Input.GetKeyDown(Map(k));
        public static Vector2 MousePos => Input.mousePosition;
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b);
        public static bool MouseUp(int b) => Input.GetMouseButtonUp(b);
        public static bool Mouse(int b) => Input.GetMouseButton(b);
        public static float Scroll => Input.mouseScrollDelta.y;
#else
        static UnityEngine.InputSystem.Controls.KeyControl Map(PKKey k)
        {
            var kb = Keyboard.current;
            if (kb == null) return null;
            switch (k)
            {
                case PKKey.W: return kb.wKey;
                case PKKey.A: return kb.aKey;
                case PKKey.S: return kb.sKey;
                case PKKey.D: return kb.dKey;
                case PKKey.Q: return kb.qKey;
                case PKKey.E: return kb.eKey;
                case PKKey.Up: return kb.upArrowKey;
                case PKKey.Down: return kb.downArrowKey;
                case PKKey.Left: return kb.leftArrowKey;
                case PKKey.Right: return kb.rightArrowKey;
                case PKKey.Space: return kb.spaceKey;
                case PKKey.Escape: return kb.escapeKey;
                case PKKey.Tab: return kb.tabKey;
                case PKKey.Alpha1: return kb.digit1Key;
                case PKKey.Alpha2: return kb.digit2Key;
                case PKKey.Alpha3: return kb.digit3Key;
                case PKKey.B: return kb.bKey;
                case PKKey.R: return kb.rKey;
                case PKKey.L: return kb.lKey;
                case PKKey.H: return kb.hKey;
                case PKKey.P: return kb.pKey;
                case PKKey.G: return kb.gKey;
                case PKKey.F1: return kb.f1Key;
                case PKKey.F5: return kb.f5Key;
                case PKKey.F9: return kb.f9Key;
                case PKKey.Shift: return kb.leftShiftKey;
                case PKKey.Delete: return kb.deleteKey;
                case PKKey.Plus: return kb.numpadPlusKey;
                case PKKey.Minus: return kb.numpadMinusKey;
            }
            return null;
        }

        public static bool Key(PKKey k) { var c = Map(k); return c != null && (c.isPressed || (k == PKKey.Shift && Keyboard.current.rightShiftKey.isPressed)); }
        public static bool KeyDown(PKKey k) { var c = Map(k); return c != null && c.wasPressedThisFrame; }
        public static Vector2 MousePos => UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
        static UnityEngine.InputSystem.Controls.ButtonControl Btn(int b)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) return null;
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
        }
        public static bool MouseDown(int b) { var c = Btn(b); return c != null && c.wasPressedThisFrame; }
        public static bool MouseUp(int b) { var c = Btn(b); return c != null && c.wasReleasedThisFrame; }
        public static bool Mouse(int b) { var c = Btn(b); return c != null && c.isPressed; }
        public static float Scroll => UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y / 120f : 0f;
#endif
    }
}
