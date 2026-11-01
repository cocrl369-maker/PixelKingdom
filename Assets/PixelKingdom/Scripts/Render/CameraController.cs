using UnityEngine;

namespace PixelKingdom
{
    /// <summary>2D-Kartenkamera (orthografisch) mit pixelgenauen Zoomstufen.</summary>
    public class CameraController : MonoBehaviour
    {
        public Camera Cam;
        public Vector2 Target = new Vector2(100, 100);
        Vector2 goal; bool hasGoal;
        public static readonly float[] Zooms = { 0.25f, 0.5f, 1f, 2f, 3f, 4f };
        public int ZoomIndex = 3;
        public float Zoom => Zooms[ZoomIndex];
        public bool InputEnabled = true;
        public bool BlockMouse;
        public Vector2 AutoPan;
        public bool DraggedThisPress;
        Vector2 lastMouse; bool dragging;

        public void Init(Camera cam)
        {
            Cam = cam;
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.rotation = Quaternion.identity;
        }

        public void FocusOn(Vector2 p) { goal = p; hasGoal = true; }
        public void JumpTo(Vector2 p) { Target = p; hasGoal = false; }
        public void SetZoom(int idx) { ZoomIndex = Mathf.Clamp(idx, 0, Zooms.Length - 1); }

        /// <summary>Sichtbare Breite in Kacheln (für Minikarte).</summary>
        public Vector2 ViewSize => new Vector2(Screen.width, Screen.height) / (16f * Zoom);

        public Vector2 ScreenToWorld(Vector2 screen)
        {
            if (Cam == null) return Vector2.zero;
            var w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            return new Vector2(w.x, w.y);
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            float dt = Time.unscaledDeltaTime;
            if (InputEnabled) HandleInput(dt);
            if (AutoPan != Vector2.zero) Target += AutoPan * dt;
            if (hasGoal)
            {
                Target = Vector2.Lerp(Target, goal, 1f - Mathf.Exp(-7f * dt));
                if ((Target - goal).sqrMagnitude < 0.001f) hasGoal = false;
            }
            Target.x = Mathf.Clamp(Target.x, 0, WorldMap.W);
            Target.y = Mathf.Clamp(Target.y, 0, WorldMap.H);

            float ppu = 16f * Zoom; // Bildschirmpixel pro Kachel
            Cam.orthographicSize = Screen.height / (2f * ppu);
            var pos = Target;
            if (Zoom >= 1f && !hasGoal && AutoPan == Vector2.zero)
            {
                // auf das Bildschirm-Pixelraster einrasten -> scharfe Pixel ohne Flimmern
                pos.x = Mathf.Round(pos.x * ppu) / ppu;
                pos.y = Mathf.Round(pos.y * ppu) / ppu;
                if (Screen.width % 2 == 1) pos.x += 0.5f / ppu;
                if (Screen.height % 2 == 1) pos.y += 0.5f / ppu;
            }
            Cam.transform.position = new Vector3(pos.x, pos.y, -50f);
            Cam.backgroundColor = Palette.ToColor(Palette.Art.Deep);
        }

        void HandleInput(float dt)
        {
            float speed = 700f / (16f * Zoom) * dt * (PKInput.Key(PKKey.Shift) ? 2.5f : 1f);
            Vector2 move = Vector2.zero;
            if (PKInput.Key(PKKey.W) || PKInput.Key(PKKey.Up)) move.y += 1;
            if (PKInput.Key(PKKey.S) || PKInput.Key(PKKey.Down)) move.y -= 1;
            if (PKInput.Key(PKKey.D) || PKInput.Key(PKKey.Right)) move.x += 1;
            if (PKInput.Key(PKKey.A) || PKInput.Key(PKKey.Left)) move.x -= 1;
            if (move != Vector2.zero) { Target += move.normalized * speed; hasGoal = false; }
            if (PKInput.KeyDown(PKKey.Plus) || PKInput.KeyDown(PKKey.E)) ZoomAt(1, new Vector2(Screen.width / 2f, Screen.height / 2f));
            if (PKInput.KeyDown(PKKey.Minus) || PKInput.KeyDown(PKKey.Q)) ZoomAt(-1, new Vector2(Screen.width / 2f, Screen.height / 2f));

            var mp = PKInput.MousePos;
            if (!BlockMouse)
            {
                float sc = PKInput.Scroll;
                if (Mathf.Abs(sc) > 0.01f) ZoomAt(sc > 0 ? 1 : -1, mp);
                if (PKInput.MouseDown(1) || PKInput.MouseDown(2)) { dragging = true; lastMouse = mp; DraggedThisPress = false; }
            }
            if (dragging)
            {
                if (!PKInput.Mouse(1) && !PKInput.Mouse(2)) dragging = false;
                else
                {
                    var d = mp - lastMouse;
                    lastMouse = mp;
                    if (d.sqrMagnitude > 0)
                    {
                        Target -= d / (16f * Zoom);
                        hasGoal = false;
                        if (d.sqrMagnitude > 4) DraggedThisPress = true;
                    }
                }
            }
        }

        /// <summary>Zoomt um eine Stufe, der Punkt unter der Maus bleibt stehen.</summary>
        void ZoomAt(int dir, Vector2 screen)
        {
            int ni = Mathf.Clamp(ZoomIndex + dir, 0, Zooms.Length - 1);
            if (ni == ZoomIndex) return;
            var before = ScreenToWorld(screen);
            ZoomIndex = ni;
            Cam.orthographicSize = Screen.height / (2f * 16f * Zoom);
            Cam.transform.position = new Vector3(Target.x, Target.y, -50f);
            var after = ScreenToWorld(screen);
            Target += before - after;
            hasGoal = false;
        }
    }
}
