using System.Linq;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>
    /// Einstiegspunkt. Startet automatisch in jeder Szene (keine Einrichtung nötig):
    /// einfach Unity öffnen und auf Play drücken.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null || FindObjectOfType<GameManager>() != null) return;
            var go = new GameObject("PixelKingdom");
            go.AddComponent<GameManager>();
        }

        public Game Game;
        public TerrainRenderer Terrain;
        public EntityRenderer Entities;
        public CameraController CamCtrl;
        public GameUI UI;
        public Material Mat;

        public bool InMenu = true;
        public bool Paused;
        public int Speed = 1;
        public static readonly int[] Speeds = { 1, 2, 5 };
        public const float SecondsPerDay = 5f;

        public int SelSettlement = -1, SelArmy = -1;
        public enum Mode { None, PlaceSettlers }
        public Mode CurrentMode;
        public int ModeSource = -1;

        public bool HoverValid;
        public Vector3 HoverPoint;
        public int HoverX, HoverY;
        public bool GameOver;

        float autosave = 90f;
        bool isDemo;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Palette.Set(PlayerPrefs.GetInt("pk_palette", 0));

            var shader = Resources.Load<Shader>("PKFlat");
            if (shader == null) shader = Shader.Find("PixelKingdom/Flat");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Mat = new Material(shader) { name = "PixelKingdomFlat" };

            var cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                cam = cgo.AddComponent<Camera>();
            }
            foreach (var l in FindObjectsOfType<Light>()) l.enabled = false; // Licht ist "gebacken"
            RenderSettings.fog = false;
            CamCtrl = cam.GetComponent<CameraController>();
            if (CamCtrl == null) CamCtrl = cam.gameObject.AddComponent<CameraController>();
            CamCtrl.Init(cam);

            Terrain = new GameObject("Terrain").AddComponent<TerrainRenderer>();
            Terrain.transform.SetParent(transform, false);
            Entities = new GameObject("Entities").AddComponent<EntityRenderer>();
            Entities.transform.SetParent(transform, false);
            UI = gameObject.AddComponent<GameUI>();
            CamCtrl.GroundAt = (x, z) => Terrain.GroundY(x, z);
        }

        void Start()
        {
            // Hintergrund für das Hauptmenü: der letzte Spielstand oder eine Demo-Welt
            var saved = SaveSystem.Load();
            if (saved != null) LoadInto(Game.FromState(saved), false);
            else ShowDemoWorld();
            InMenu = true;
            CamCtrl.AutoRotate = 4f;
        }

        void ShowDemoWorld()
        {
            var g = Game.NewGame(Random.Range(1, 999999));
            for (int i = 0; i < g.Map.disc.Length; i++) g.Map.disc[i] = true;
            g.Headless = true;
            isDemo = true;
            LoadInto(g, false);
            var cap = g.Capital(g.Player);
            CamCtrl.JumpTo(new Vector3(cap.x, 0, cap.y), 60f);
        }

        void LoadInto(Game g, bool focus)
        {
            Game = g;
            Palette.Set(PlayerPrefs.GetInt("pk_palette", g.S.palette));
            Terrain.Init(g, Mat);
            Terrain.BuildAll();
            Entities.Init(g, Terrain, Mat);
            SelSettlement = SelArmy = -1;
            CurrentMode = Mode.None;
            GameOver = false;
            var cap = g.Capital(g.Player);
            if (cap != null) CamCtrl.JumpTo(new Vector3(cap.x + 0.5f, 0, cap.y + 0.5f), focus ? 22f : 40f);
        }

        // ------------------------------------------------------------------
        //  Menüaktionen
        // ------------------------------------------------------------------
        public void NewGame()
        {
            // Die Welt aus dem Menü-Hintergrund wird zur eigenen Welt
            int seed = isDemo && Game != null ? Game.S.seed : Random.Range(1, 999999);
            var g = Game.NewGame(seed);
            isDemo = false;
            LoadInto(g, true);
            InMenu = false; Paused = false; Speed = 1;
            CamCtrl.AutoRotate = 0;
            SaveSystem.Save(g);
            UI.OnGameStarted(null);
            g.StoryTick();
        }

        public void ContinueGame()
        {
            var saved = SaveSystem.Load();
            if (saved == null) { NewGame(); return; }
            int offline = SaveSystem.OfflineDays(saved);
            var g = Game.FromState(saved);
            string report = offline > 0 ? g.SimulateOffline(offline) : null;
            isDemo = false;
            LoadInto(g, true);
            InMenu = false; Paused = false;
            CamCtrl.AutoRotate = 0;
            UI.OnGameStarted(report);
        }

        public void SaveNow()
        {
            if (Game == null || isDemo || InMenu && !HasRunningGame) return;
            if (SaveSystem.Save(Game)) UI.Toast("Spiel gespeichert.", 1);
        }

        public bool HasRunningGame => Game != null && !isDemo;

        public void BackToMenu()
        {
            if (HasRunningGame) SaveSystem.Save(Game);
            InMenu = true;
            CamCtrl.AutoRotate = 4f;
        }

        public void CyclePalette()
        {
            Palette.Set(Palette.Index + 1);
            PlayerPrefs.SetInt("pk_palette", Palette.Index);
            Terrain.MarkAllDirty();
            Entities.ForceRefresh();
        }

        // ------------------------------------------------------------------
        //  Schleife
        // ------------------------------------------------------------------
        void Update()
        {
            if (Game == null) return;
            bool blocking = UI.IsBlocking;
            CamCtrl.InputEnabled = !UI.KeyboardCaptured;
            CamCtrl.BlockMouse = UI.MouseOverUI || InMenu;

            if (!InMenu && !GameOver)
            {
                if (!Paused && !blocking)
                {
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    Game.Advance(dt * Speeds[Speed - 1 < 0 ? 0 : Mathf.Min(Speed - 1, 2)] / SecondsPerDay);
                }
                HandleKeys();
                HandleMouse();
                autosave -= Time.unscaledDeltaTime;
                if (autosave <= 0) { autosave = 90f; SaveSystem.Save(Game); }
                if (Game.CountSettlements(Game.PlayerId) == 0) GameOver = true;
                if (Game.A(SelArmy) == null) SelArmy = -1;
                var ss = Game.St(SelSettlement);
                if (ss == null) SelSettlement = -1;
            }
            Entities.SelectedArmy = SelArmy;
        }

        void HandleKeys()
        {
            if (UI.KeyboardCaptured) return;
            if (UI.IsBlocking)
            {
                if (PKInput.KeyDown(PKKey.Escape)) UI.CloseTopWindow();
                return;
            }
            if (PKInput.KeyDown(PKKey.Space)) Paused = !Paused;
            if (PKInput.KeyDown(PKKey.Alpha1)) { Speed = 1; Paused = false; }
            if (PKInput.KeyDown(PKKey.Alpha2)) { Speed = 2; Paused = false; }
            if (PKInput.KeyDown(PKKey.Alpha3)) { Speed = 3; Paused = false; }
            if (PKInput.KeyDown(PKKey.F5)) SaveNow();
            if (PKInput.KeyDown(PKKey.P)) CyclePalette();
            if (PKInput.KeyDown(PKKey.H)) FocusHome();
            if (PKInput.KeyDown(PKKey.Tab)) CycleSettlement();
            if (PKInput.KeyDown(PKKey.Escape))
            {
                if (UI.CloseTopWindow()) { }
                else if (CurrentMode != Mode.None) CurrentMode = Mode.None;
                else if (SelArmy >= 0 || SelSettlement >= 0) { SelArmy = -1; SelSettlement = -1; }
                else UI.OpenWindow(GameUI.Window.Menu);
            }
            if (PKInput.KeyDown(PKKey.G)) UI.ToggleWindow(GameUI.Window.Diplomacy);
            if (PKInput.KeyDown(PKKey.R)) UI.ToggleWindow(GameUI.Window.Realm);
            if (PKInput.KeyDown(PKKey.L)) UI.ToggleWindow(GameUI.Window.Log);
            if (PKInput.KeyDown(PKKey.F1)) UI.ToggleWindow(GameUI.Window.Help);
            if (PKInput.KeyDown(PKKey.B) && SelSettlement >= 0) UI.SettlementTab = 1;
            if (PKInput.KeyDown(PKKey.Delete) && SelArmy >= 0) { var a = Game.A(SelArmy); if (a != null && a.faction == Game.PlayerId) Game.Disband(a); }
        }

        public void FocusHome()
        {
            var cap = Game.Capital(Game.Player);
            if (cap != null) { SelectSettlement(cap); }
        }

        void CycleSettlement()
        {
            var list = Game.SettlementsOf(Game.PlayerId).ToList();
            if (list.Count == 0) return;
            int idx = list.FindIndex(s => s.id == SelSettlement);
            SelectSettlement(list[(idx + 1) % list.Count]);
        }

        public void SelectSettlement(SettlementData s, bool focus = true)
        {
            SelSettlement = s.id; SelArmy = -1;
            if (focus) CamCtrl.FocusOn(new Vector3(s.x + 0.5f, 0, s.y + 0.5f));
        }

        public void SelectArmy(ArmyData a, bool focus = false)
        {
            SelArmy = a.id; SelSettlement = -1;
            if (focus) CamCtrl.FocusOn(new Vector3(a.x, 0, a.y));
        }

        Vector2 downPos;
        void HandleMouse()
        {
            HoverValid = false;
            if (UI.MouseOverUI || UI.IsBlocking) return;
            var mp = PKInput.MousePos;
            if (CamCtrl.PickGround(mp, out var hit))
            {
                HoverValid = true; HoverPoint = hit;
                HoverX = Mathf.FloorToInt(hit.x); HoverY = Mathf.FloorToInt(hit.z);
            }
            if (PKInput.MouseDown(0)) downPos = mp;
            if (PKInput.MouseUp(0) && (mp - downPos).sqrMagnitude < 100 && HoverValid) LeftClick();
            if (PKInput.MouseUp(1) && !CamCtrl.DraggedThisPress && HoverValid) RightClick();
        }

        void LeftClick()
        {
            var g = Game;
            if (CurrentMode == Mode.PlaceSettlers)
            {
                var src = g.St(ModeSource);
                CurrentMode = Mode.None;
                if (src == null) return;
                var a = g.SendSettlers(src, HoverX, HoverY, out string why);
                if (a == null) UI.Toast("Nicht möglich: " + why, 2);
                else { UI.Toast("Siedler brechen auf!", 1); SelectArmy(a); }
                return;
            }
            // Armee unter der Maus?
            ArmyData best = null; float bd = 0.8f * 0.8f;
            foreach (var a in g.Armies)
            {
                if (!Entities.ArmyVisible(a.id)) continue;
                float d = (a.x - HoverPoint.x) * (a.x - HoverPoint.x) + (a.y - HoverPoint.z) * (a.y - HoverPoint.z);
                if (a.faction == g.PlayerId) d *= 0.6f;
                if (d < bd) { bd = d; best = a; }
            }
            if (best != null) { SelectArmy(best); return; }
            var s = SettlementNear(HoverPoint.x, HoverPoint.z, 1.4f);
            if (s != null && g.IsDiscovered(s.x, s.y)) { SelectSettlement(s, false); return; }
            SelArmy = -1; SelSettlement = -1;
        }

        public SettlementData SettlementNear(float x, float z, float r)
        {
            SettlementData best = null; float bd = r * r;
            foreach (var s in Game.Settlements)
            {
                float d = (s.x + 0.5f - x) * (s.x + 0.5f - x) + (s.y + 0.5f - z) * (s.y + 0.5f - z);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        void RightClick()
        {
            var g = Game;
            var a = g.A(SelArmy);
            if (a == null || a.faction != g.PlayerId) return;
            var s = SettlementNear(HoverPoint.x, HoverPoint.z, 1.2f);
            if (a.settlers > 0 && a.units.Sum() == 0)
            {
                if (!g.OrderMove(a, HoverX, HoverY, ArmyMission.Found, -1, out string w)) UI.Toast(w, 2);
                else UI.Toast("Siedler ziehen dorthin und gründen einen neuen Ort.", 0);
                return;
            }
            if (s != null && g.IsDiscovered(s.x, s.y))
            {
                if (s.faction == g.PlayerId)
                {
                    if (!g.OrderMove(a, s.x, s.y, ArmyMission.Return, s.id, out string w)) UI.Toast(w, 2);
                    return;
                }
                bool raid = PKInput.Key(PKKey.Shift);
                if (!g.AtWar(g.PlayerId, s.faction))
                {
                    UI.AskWar(g.F(s.faction), a, s, raid);
                    return;
                }
                OrderAttack(a, s, raid);
                return;
            }
            if (!g.OrderMove(a, HoverX, HoverY, ArmyMission.Move, -1, out string why)) UI.Toast(why, 2);
        }

        public void OrderAttack(ArmyData a, SettlementData s, bool raid)
        {
            if (!Game.OrderMove(a, s.x, s.y, raid ? ArmyMission.Raid : ArmyMission.Attack, s.id, out string why)) UI.Toast(why, 2);
            else UI.Toast(raid ? $"Überfall auf {s.name} befohlen." : $"Angriff auf {s.name} befohlen.", 3);
        }

        void OnApplicationQuit()
        {
            if (HasRunningGame) SaveSystem.Save(Game);
            PlayerPrefs.Save();
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && HasRunningGame) SaveSystem.Save(Game);
        }
    }
}
