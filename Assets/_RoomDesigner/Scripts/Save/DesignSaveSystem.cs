using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RoomDesigner
{
    /// <summary>Jeden umiestnený objekt v uloženom návrhu.</summary>
    [Serializable]
    public class PlacedItemData
    {
        public string id;
        public Vector3 position;
        public Quaternion rotation;
        public float scale = 1f;
    }

    /// <summary>Celý návrh izby – ukladá sa ako JSON.</summary>
    [Serializable]
    public class DesignData
    {
        public int version = 1;
        public string savedAt;
        public bool hasFloor;
        public float floorHeight;
        public List<PlacedItemData> items = new List<PlacedItemData>();
    }

    /// <summary>
    /// Uloženie a načítanie návrhu do súboru v Application.persistentDataPath.
    /// Pozície sú v súradniciach AR session – tá začína tam, kde bol mobil pri spustení aplikácie.
    /// </summary>
    public static class DesignSaveSystem
    {
        const string FileName = "room_design.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(FilePath);

        public static bool Save(DesignData data)
        {
            try
            {
                data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
                Debug.Log($"[Room Designer] Návrh uložený: {FilePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Room Designer] Uloženie zlyhalo: {e.Message}");
                return false;
            }
        }

        public static bool TryLoad(out DesignData data)
        {
            data = null;
            try
            {
                if (!File.Exists(FilePath))
                    return false;

                data = JsonUtility.FromJson<DesignData>(File.ReadAllText(FilePath));
                return data != null && data.items != null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Room Designer] Načítanie zlyhalo: {e.Message}");
                return false;
            }
        }
    }
}
