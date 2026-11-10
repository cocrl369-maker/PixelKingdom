using System.Collections.Generic;
using UnityEngine;

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

        static bool guiTouchDown;

        static void ReadRealTouches()
        {
            if (NewSys.HasTouchscreen) { NewSys.ReadTouches(touches); if (!NewSys.Failed) return; touches.Clear(); }
            if (LegacyOk)
            {
                try
                {
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        var t = Input.GetTouch(i);
                        int ph = t.phase == TouchPhase.Began ? 0 : (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) ? 2 : 1;
                        touches.Add(new PKTouch { Id = t.fingerId, Pos = t.position, Phase = ph });
                    }
                    return;
                }
                catch (System.InvalidOperationException) { LegacyFailed(); }
            }
            // Letzter Ausweg auf Geräten mit Touch: Unity meldet den ersten Finger als GUI-Mausereignis.
            if (!Application.isMobilePlatform) return;
            bool down = Gui.Button(0, 0);
            if (down || guiTouchDown)
            {
                int ph = down && !guiTouchDown ? 0 : !down ? 2 : 1;
                touches.Add(new PKTouch { Id = 1, Pos = Gui.Pos, Phase = ph });
            }
            guiTouchDown = down;
        }
    }
}
