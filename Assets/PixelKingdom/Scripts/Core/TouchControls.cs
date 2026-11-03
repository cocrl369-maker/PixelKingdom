using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Gesten für Android:
    ///  - Tippen: auswählen bzw. (mit gewähltem Heer) Befehl geben
    ///  - Ziehen mit einem Finger: Karte verschieben
    ///  - Zwei Finger: zoomen (Pinch) und verschieben
    ///  - Lange halten: Geländeinfo bzw. Überfall-Befehl
    /// </summary>
    public class TouchControls
    {
        readonly GameManager gm;
        int primaryId = -1;
        Vector2 startPos, lastPos;
        float startTime;
        bool moved, longFired, startedOnUI;
        bool pinching;
        float pinchBase;
        Vector2 lastMid;

        public TouchControls(GameManager gm) { this.gm = gm; }

        float DragThreshold => Mathf.Max(12f, (Screen.dpi > 0 ? Screen.dpi : 160f) * 0.07f);

        public void Update(bool worldInput)
        {
            int n = PKInput.TouchCount;
            if (n >= 2)
            {
                var a = PKInput.GetTouch(0); var b = PKInput.GetTouch(1);
                float d = Vector2.Distance(a.Pos, b.Pos);
                Vector2 mid = (a.Pos + b.Pos) * 0.5f;
                if (!pinching)
                {
                    pinching = true; pinchBase = Mathf.Max(1f, d); lastMid = mid;
                    primaryId = -1; // kein Tippen mehr
                    return;
                }
                if (!worldInput) { lastMid = mid; return; }
                gm.CamCtrl.Pan(mid - lastMid);
                lastMid = mid;
                float ratio = d / pinchBase;
                if (ratio > 1.45f) { gm.CamCtrl.ZoomStep(1, mid); pinchBase = d; }
                else if (ratio < 0.69f) { gm.CamCtrl.ZoomStep(-1, mid); pinchBase = d; }
                return;
            }
            if (pinching)
            {
                pinching = false;
                // übrig gebliebener Finger verschiebt nur noch, tippt nicht
                if (n == 1) { var t0 = PKInput.GetTouch(0); primaryId = t0.Id; lastPos = t0.Pos; moved = true; startedOnUI = false; longFired = true; }
                return;
            }
            if (n == 0) return;

            var t = PKInput.GetTouch(0);
            if (t.Phase == 0)
            {
                primaryId = t.Id; startPos = lastPos = t.Pos; startTime = Time.unscaledTime;
                moved = false; longFired = false;
                startedOnUI = !worldInput || gm.UI.IsOverUI(t.Pos);
                return;
            }
            if (t.Id != primaryId) return;
            // Hat die Oberfläche (Knopf, Fenster, Dialog) diesen Druck angenommen, bleibt die Karte unberührt
            if (gm.UI.LastUIPress >= startTime - 0.02f || !worldInput) startedOnUI = true;
            if (!startedOnUI)
            {
                if (!moved && (t.Pos - startPos).magnitude > DragThreshold) moved = true;
                if (moved) gm.CamCtrl.Pan(t.Pos - lastPos);
                if (!moved && !longFired && Time.unscaledTime - startTime > 0.55f) { longFired = true; gm.OnLongPress(t.Pos); }
            }
            lastPos = t.Pos;
            if (t.Phase == 2)
            {
                if (!startedOnUI && !moved && !longFired && !gm.UI.IsBlocking && !gm.UI.IsOverUI(t.Pos)) gm.OnTap(t.Pos);
                primaryId = -1;
            }
        }
    }
}
