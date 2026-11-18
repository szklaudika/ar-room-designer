using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomDesigner
{
    /// <summary>Kam sa objekt dá umiestniť.</summary>
    public enum PlacementSurface
    {
        /// <summary>Nábytok – na podlahu.</summary>
        Floor,
        /// <summary>Obrazy, okno – na stenu.</summary>
        Wall
    }

    /// <summary>
    /// Zoznam objektov, ktoré sa dajú umiestniť (nábytok, obrazy, okno).
    /// Z neho sa vytvára menu dole. Nový objekt = nový riadok v tomto assete.
    /// </summary>
    [CreateAssetMenu(menuName = "Room Designer/Katalóg objektov", fileName = "PlaceableCatalog")]
    public class PlaceableCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Jedinečné ID – ukladá sa do JSON návrhu, po vydaní ho nemeň.")]
            public string id;

            [Tooltip("Názov v menu.")]
            public string displayName;

            public PlacementSurface surface;

            [Tooltip("Iba pre podlahové objekty: smie stáť aj na stole/polici (napr. lampa).")]
            public bool allowOnFurnitureSurfaces;

            public PlaceableObject prefab;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public int Count => entries.Count;

        public Entry Get(int index) => index >= 0 && index < entries.Count ? entries[index] : null;

        public Entry Find(string id)
        {
            foreach (var entry in entries)
                if (entry != null && entry.id == id)
                    return entry;
            return null;
        }

        /// <summary>Používa Editor skript pri generovaní katalógu.</summary>
        public void SetEntries(IEnumerable<Entry> newEntries)
        {
            entries = new List<Entry>(newEntries);
        }
    }
}
