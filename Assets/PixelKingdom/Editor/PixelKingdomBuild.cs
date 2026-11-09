using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelKingdom.EditorTools
{
    /// <summary>
    /// Menü "Pixel Kingdom" in Unity: Android-Einstellungen übernehmen, App-Icon erzeugen, APK/AAB bauen.
    /// </summary>
    public static class PixelKingdomBuild
    {
        const string ScenePath = "Assets/PixelKingdom/Scenes/Main.unity";
        const string IconPath = "Assets/PixelKingdom/Icon/AppIcon.png";
        const string PackageId = "com.pixelkingdom.game";

        [MenuItem("Pixel Kingdom/1. Android einrichten", false, 1)]
        public static void SetupAndroid()
        {
            EnsureScene();
            CreateIcon();

            PlayerSettings.companyName = "Pixel Kingdom";
            PlayerSettings.productName = "Pixel Kingdom";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "1.0.0";

            // Querformat (beide Richtungen)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Android: 64 Bit (Pflicht für Google Play), IL2CPP. Mindest-Android-Version bleibt Unitys Vorgabe.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.runInBackground = false;
            // Die Eingabe-Einstellung ("Active Input Handling") wird NICHT verändert: Das Spiel kommt mit jeder klar.

            // Szene in die Build-Liste
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android.ToBuildTargetGroup(), BuildTarget.Android))
                    Debug.LogWarning("Pixel Kingdom: Konnte nicht auf Android umschalten. Ist 'Android Build Support' im Unity Hub installiert?");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Pixel Kingdom: Android ist eingerichtet. Jetzt 'Pixel Kingdom/2. APK bauen' wählen.");
        }

        [MenuItem("Pixel Kingdom/2. APK bauen (zum Testen auf dem Tablet)", false, 2)]
        public static void BuildApk() => Build(false);

        [MenuItem("Pixel Kingdom/3. AAB bauen (für Google Play)", false, 3)]
        public static void BuildAab() => Build(true);

        [MenuItem("Pixel Kingdom/Szene anlegen und öffnen", false, 20)]
        public static void OpenScene()
        {
            EnsureScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        static void Build(bool bundle)
        {
            SetupAndroid();
            EditorUserBuildSettings.buildAppBundle = bundle;
            Directory.CreateDirectory("Builds");
            string path = bundle ? "Builds/PixelKingdom.aab" : "Builds/PixelKingdom.apk";
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"Pixel Kingdom: Fertig! {path} ({report.summary.totalSize / (1024 * 1024)} MB)");
                EditorUtility.RevealInFinder(path);
            }
            else Debug.LogError("Pixel Kingdom: Build fehlgeschlagen – Details siehe Console.");
        }

        /// <summary>Leere Szene mit Kamera. Das Spiel startet sich selbst (GameManager.Boot).</summary>
        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        /// <summary>App-Icon aus der Pixelkunst: die Burg auf blauem Grund.</summary>
        static void CreateIcon()
        {
            if (File.Exists(IconPath)) { ApplyIcon(); return; }
            const int size = 512;
            var art = SpriteArt.Build(ArtPalette.All[0]);
            var castle = art["b_Burg"].C;
            int scale = Mathf.Max(1, Mathf.Min((size - 96) / castle.W, (size - 96) / castle.H));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var bgTop = new Color32(88, 150, 214, 255); var bgBot = new Color32(52, 104, 168, 255);
            var grass = new Color32(111, 174, 72, 255);
            var px = new Color32[size * size];
            int groundY = 70;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = y < groundY ? grass : Color32.Lerp(bgBot, bgTop, (y - groundY) / (float)(size - groundY));
            int ox = (size - castle.W * scale) / 2, oy = groundY - scale * 2;
            for (int y = 0; y < castle.H; y++)
                for (int x = 0; x < castle.W; x++)
                {
                    var p = castle.P[y * castle.W + x];
                    if (p.a == 0) continue;
                    var c = new Color32(p.r, p.g, p.b, 255);
                    if (p.r == 255 && p.g == 255 && p.b == 255) c = new Color32(217, 67, 58, 255); // Fahne
                    for (int sy = 0; sy < scale; sy++)
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int tx = ox + x * scale + sx, ty = oy + (castle.H - 1 - y) * scale + sy;
                            if (tx >= 0 && ty >= 0 && tx < size && ty < size) px[ty * size + tx] = c;
                        }
                }
            tex.SetPixels32(px);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconPath);
            var imp = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (imp != null)
            {
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            ApplyIcon();
        }

        static void ApplyIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }
    }
}
