using System;
using System.IO;
using UnityEngine;

namespace PixelKingdom
{
    public static class SaveSystem
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "pixelkingdom_save.json");
        public static bool Exists => File.Exists(FilePath);

        public static bool Save(Game g)
        {
            try
            {
                g.PrepareSave();
                g.S.lastSaveTicks = DateTime.UtcNow.Ticks;
                g.S.palette = Palette.Index;
                string json = JsonUtility.ToJson(g.S);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(Loc.T("PixelKingdom: Speichern fehlgeschlagen: ") + e.Message);
                return false;
            }
        }

        public static GameState Load()
        {
            foreach (var path in new[] { FilePath, FilePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var s = JsonUtility.FromJson<GameState>(File.ReadAllText(path));
                    if (s != null && s.version < GameState.CurrentVersion)
                    {
                        // Spielstand einer älteren Version (andere Kartengröße): beiseitelegen statt kaputt zu laden
                        File.Copy(path, path + ".alt-v" + s.version, true);
                        File.Delete(path);
                        Debug.Log(Loc.T("PixelKingdom: Alter Spielstand (Version ") + s.version + Loc.T(") wurde gesichert und wird nicht mehr geladen."));
                        continue;
                    }
                    if (s != null && s.factions != null && s.factions.Count > 0) return s;
                }
                catch (Exception e)
                {
                    Debug.LogWarning(Loc.T("PixelKingdom: Laden fehlgeschlagen (") + path + "): " + e.Message);
                }
            }
            return null;
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                if (File.Exists(FilePath + ".bak")) File.Delete(FilePath + ".bak");
            }
            catch { }
        }

        /// <summary>Wie viele Spieltage während der Abwesenheit vergehen (1 Tag pro 3 Minuten, max. 100).</summary>
        public static int OfflineDays(GameState s)
        {
            if (s.lastSaveTicks <= 0) return 0;
            var away = DateTime.UtcNow - new DateTime(s.lastSaveTicks, DateTimeKind.Utc);
            if (away.TotalMinutes < 3) return 0;
            return Mathf.Clamp((int)(away.TotalMinutes / 3.0), 0, 100);
        }
    }
}
