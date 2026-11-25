using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomDesigner
{
    /// <summary>Druh objektu – určuje, kam sa dá umiestniť.</summary>
    public enum PlacementCategory
    {
        /// <summary>Na vodorovnú plochu (podlaha, stôl).</summary>
        Nabytok,
        /// <summary>Na stenu.</summary>
        Obraz,
        /// <summary>Na stenu.</summary>
        Okno
    }

    /// <summary>Jedna položka v menu: čo sa má po ťuknutí na plochu vytvoriť.</summary>
    public class PlaceableItem
    {
        public PlacementCategory category;
        public string displayName;
        public PlaceableObject prefab;
        /// <summary>Obrázok obrazu alebo výhľad okna (nábytok nemá).</summary>
        public Texture2D texture;
    }

    /// <summary>Zoznam nábytku v menu. Nový nábytok = nový riadok v tomto assete.</summary>
    [CreateAssetMenu(menuName = "Room Designer/Katalóg nábytku", fileName = "PlaceableCatalog")]
    public class PlaceableCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Jedinečné ID.")]
            public string id;

            [Tooltip("Názov v menu.")]
            public string displayName;

            public PlaceableObject prefab;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public List<Entry> Entries => entries;
    }
}
