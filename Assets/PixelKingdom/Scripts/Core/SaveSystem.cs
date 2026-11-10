using System;
using System.IO;
using UnityEngine;

namespace PixelKingdom
{
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        /// <summary>Aktueller Speicherplatz (1..3). Platz 1 nutzt den bisherigen Dateinamen.</summary>
        public static int Slot
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("pk_slot", 1), 1, SlotCount);
            set { PlayerPrefs.SetInt("pk_slot", Mathf.Clamp(value, 1, SlotCount)); PlayerPrefs.Save(); }
        }

        public static string SlotPath(int n) => Path.Combine(Application.persistentDataPath, n <= 1 ? "pixelkingdom_save.json" : "pixelkingdom_save" + n + ".json");
        static string MetaPath(int n) => SlotPath(n) + ".info";
        public static string FilePath => SlotPath(Slot);
        public static bool Exists => File.Exists(FilePath);
        public static bool SlotExists(int n) => File.Exists(SlotPath(n));

        /// <summary>Kurzinfo zu einem Speicherplatz (Reich, Rang, Tag, zuletzt gespielt) oder null.</summary>
        public static string SlotInfo(int n)
        {
            try
            {
                if (!SlotExists(n)) return null;
                if (File.Exists(MetaPath(n))) return File.ReadAllText(MetaPath(n));
                return Loc.T("Spielstand");
            }
            catch { return null; }
        }

        public static void DeleteSlot(int n)
        {
            try
            {
                foreach (var f in new[] { SlotPath(n), SlotPath(n) + ".bak", MetaPath(n) }) if (File.Exists(f)) File.Delete(f);
            }
            catch { }
        }

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
                var p = g.Player;
                if (p != null)
                    File.WriteAllText(MetaPath(Slot), Loc.F("{0} - {1} - {2}, {3} Bewohner", p.name, Ranks.Get(p.rank).Name, g.DateString(), (int)g.TotalPop(p.id))
                                                      + "\n" + DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
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

        public static void Delete() => DeleteSlot(Slot);

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
