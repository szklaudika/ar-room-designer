using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomDesigner
{
    /// <summary>Zoznam obrázkov s názvami – spoločný základ pre obrazy a výhľady z okna.</summary>
    public abstract class TextureLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Názov v menu.")]
            public string displayName;

            public Texture2D texture;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public List<Entry> Entries => entries;
    }
}
