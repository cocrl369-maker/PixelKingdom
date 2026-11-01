using System.Collections.Generic;
using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelKingdom
{
    public struct PKTouch
    {
        public int Id;
        public Vector2 Pos;
        public int Phase; // 0 = Beginn, 1 = gehalten/bewegt, 2 = Ende
    }

    /// <summary>
    /// Touch-Eingabe für Android – und eine Touch-Simulation mit der Maus (F9) zum Testen am PC.
    /// </summary>
    public static partial class PKInput
    {
        static readonly List<PKTouch> touches = new List<PKTouch>();
        static int polledFrame = -1;
        static float lastTouchTime = -100f;
        static bool simDown;

        /// <summary>Am PC: Maus verhält sich wie ein Finger (zum Testen der Android-Steuerung).</summary>
        public static bool TouchSim;

        /// <summary>True auf Android/iOS, in der Touch-Simulation oder wenn kürzlich ein Touchscreen benutzt wurde.</summary>
        public static bool UsingTouch => Application.isMobilePlatform || TouchSim || Time.unscaledTime - lastTouchTime < 5f;

        public static int TouchCount { get { Poll(); return touches.Count; } }
        public static PKTouch GetTouch(int i) { Poll(); return touches[i]; }

        static void Poll()
        {
            if (polledFrame == Time.frameCount) return;
            polledFrame = Time.frameCount;
            touches.Clear();

            if (TouchSim && !Application.isMobilePlatform)
            {
                bool down = Mouse(0);
                if (down || simDown)
                {
                    int ph = down && !simDown ? 0 : !down ? 2 : 1;
                    touches.Add(new PKTouch { Id = 1, Pos = MousePos, Phase = ph });
                }
                simDown = down;
                return;
            }
            ReadRealTouches();
            if (touches.Count > 0) lastTouchTime = Time.unscaledTime;
        }

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        static void ReadRealTouches()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                int ph = t.phase == TouchPhase.Began ? 0 : (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) ? 2 : 1;
                touches.Add(new PKTouch { Id = t.fingerId, Pos = t.position, Phase = ph });
            }
        }
#else
        static void ReadRealTouches()
        {
            var ts = Touchscreen.current;
            if (ts == null) return;
            foreach (var tc in ts.touches)
            {
                bool pressed = tc.press.isPressed, began = tc.press.wasPressedThisFrame, ended = tc.press.wasReleasedThisFrame;
                if (!pressed && !ended) continue;
                touches.Add(new PKTouch { Id = tc.touchId.ReadValue(), Pos = tc.position.ReadValue(), Phase = began ? 0 : ended ? 2 : 1 });
            }
        }
#endif
    }
}
