using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Diorama-Kamera: schwenken, drehen, zoomen. Nebel passt sich dem Zoom an.</summary>
    public class CameraController : MonoBehaviour
    {
        public Camera Cam;
        public Vector3 Target = new Vector3(100, 0, 100);
        public float Distance = 26f, Yaw = 20f, Pitch = 52f;
        float targetDistance = 26f, targetYaw = 20f;
        Vector3 goal;
        bool hasGoal;
        Vector2 lastMouse;
        bool dragging;
        public bool InputEnabled = true;
        public bool BlockMouse;   // Maus über UI
        public System.Func<float, float, float> GroundAt;
        public float AutoRotate;  // für das Hauptmenü

        public const float MinDist = 5f, MaxDist = 140f;

        public void Init(Camera cam)
        {
            Cam = cam;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            targetDistance = Distance;
            targetYaw = Yaw;
        }

        public void FocusOn(Vector3 p, float dist = -1)
        {
            goal = p; hasGoal = true;
            if (dist > 0) targetDistance = dist;
        }

        public void JumpTo(Vector3 p, float dist = -1)
        {
            Target = p; hasGoal = false;
            if (dist > 0) { targetDistance = dist; Distance = dist; }
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            float dt = Time.unscaledDeltaTime;
            if (InputEnabled) HandleInput(dt);
            if (AutoRotate != 0) targetYaw += AutoRotate * dt;

            if (hasGoal)
            {
                Target = Vector3.Lerp(Target, goal, 1f - Mathf.Exp(-6f * dt));
                if ((Target - goal).sqrMagnitude < 0.01f) hasGoal = false;
            }
            Target.x = Mathf.Clamp(Target.x, 0, WorldMap.W);
            Target.z = Mathf.Clamp(Target.z, 0, WorldMap.H);
            float gy = GroundAt != null ? GroundAt(Target.x, Target.z) : 0f;
            Target.y = Mathf.Lerp(Target.y, Mathf.Max(0, gy), 1f - Mathf.Exp(-4f * dt));

            Distance = Mathf.Lerp(Distance, targetDistance, 1f - Mathf.Exp(-10f * dt));
            Yaw = Mathf.LerpAngle(Yaw, targetYaw, 1f - Mathf.Exp(-10f * dt));
            // Beim Herauszoomen steiler (Kartenansicht), beim Heranzoomen flacher (Diorama)
            float t = Mathf.InverseLerp(MinDist, MaxDist, Distance);
            Pitch = Mathf.Lerp(38f, 68f, Mathf.Sqrt(t));

            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            Cam.transform.position = Target - rot * Vector3.forward * Distance;
            Cam.transform.rotation = rot;

            var pal = Palette.Current;
            Cam.backgroundColor = pal.Sky;
            Shader.SetGlobalColor("_PKFogColor", pal.Sky);
            Shader.SetGlobalFloat("_PKFogStart", Distance * 1.3f + 10f);
            Shader.SetGlobalFloat("_PKFogEnd", Distance * 3.2f + 40f);
        }

        void HandleInput(float dt)
        {
            float pan = Distance * 1.1f * dt;
            var fwd = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
            Vector3 move = Vector3.zero;
            if (PKInput.Key(PKKey.W) || PKInput.Key(PKKey.Up)) move += fwd;
            if (PKInput.Key(PKKey.S) || PKInput.Key(PKKey.Down)) move -= fwd;
            if (PKInput.Key(PKKey.D) || PKInput.Key(PKKey.Right)) move += right;
            if (PKInput.Key(PKKey.A) || PKInput.Key(PKKey.Left)) move -= right;
            if (move != Vector3.zero) { Target += move.normalized * pan * (PKInput.Key(PKKey.Shift) ? 2.5f : 1f); hasGoal = false; }
            if (PKInput.Key(PKKey.Q)) targetYaw += 90f * dt;
            if (PKInput.Key(PKKey.E)) targetYaw -= 90f * dt;

            var mp = PKInput.MousePos;
            if (!BlockMouse)
            {
                float sc = PKInput.Scroll;
                if (Mathf.Abs(sc) > 0.01f) targetDistance = Mathf.Clamp(targetDistance * Mathf.Pow(0.88f, Mathf.Sign(sc) * Mathf.Min(3f, Mathf.Abs(sc))), MinDist, MaxDist);
                if (PKInput.MouseDown(1) || PKInput.MouseDown(2)) { dragging = true; lastMouse = mp; }
            }
            if (PKInput.Key(PKKey.Plus)) targetDistance = Mathf.Max(MinDist, targetDistance * (1f - dt * 1.5f));
            if (PKInput.Key(PKKey.Minus)) targetDistance = Mathf.Min(MaxDist, targetDistance * (1f + dt * 1.5f));
            if (dragging)
            {
                if (!PKInput.Mouse(1) && !PKInput.Mouse(2)) dragging = false;
                else
                {
                    var d = mp - lastMouse;
                    lastMouse = mp;
                    if (PKInput.Mouse(2))
                        targetYaw += d.x * 0.25f;
                    else
                    {
                        float k = Distance * 0.0016f * (1080f / Mathf.Max(400, Screen.height));
                        Target -= (right * d.x + fwd * d.y) * k;
                        hasGoal = false;
                        if (d.sqrMagnitude > 4) DraggedThisPress = true;
                    }
                }
            }
            if (PKInput.MouseDown(1)) DraggedThisPress = false;
        }

        /// <summary>True, wenn die rechte Maustaste zum Verschieben benutzt wurde (dann kein Befehl).</summary>
        public bool DraggedThisPress;

        /// <summary>Strahl von der Maus auf das Gelände.</summary>
        public bool PickGround(Vector2 screen, out Vector3 hit)
        {
            hit = Vector3.zero;
            if (Cam == null || GroundAt == null) return false;
            var ray = Cam.ScreenPointToRay(screen);
            float step = 0.25f;
            float prevT = 0;
            for (float t = 0; t < 700; t += step)
            {
                var p = ray.origin + ray.direction * t;
                if (p.y < -2) { hit = p; return InMap(p); }
                if (p.x >= -5 && p.z >= -5 && p.x <= WorldMap.W + 5 && p.z <= WorldMap.H + 5)
                {
                    float g = GroundAt(p.x, p.z);
                    if (p.y <= g)
                    {
                        // verfeinern
                        float a = prevT, b = t;
                        for (int k = 0; k < 8; k++)
                        {
                            float m = (a + b) * 0.5f;
                            var q = ray.origin + ray.direction * m;
                            if (q.y <= GroundAt(q.x, q.z)) b = m; else a = m;
                        }
                        hit = ray.origin + ray.direction * b;
                        return InMap(hit);
                    }
                }
                prevT = t;
                step = Mathf.Min(1.0f, 0.15f + t * 0.004f);
            }
            return false;
        }

        static bool InMap(Vector3 p) => p.x >= 0 && p.z >= 0 && p.x < WorldMap.W && p.z < WorldMap.H;
    }
}
