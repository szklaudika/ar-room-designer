using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDesigner
{
    /// <summary>
    /// Spodná lišta: záložky Nábytok / Obrazy / Okná a vodorovný zoznam položiek.
    /// Položky sa tvoria pri spustení z katalógu a knižníc, takže nový obraz sa v menu objaví sám.
    /// </summary>
    public class DesignerMenu : MonoBehaviour
    {
        [SerializeField] PlacementManager placement;
        [SerializeField] PlaneVisibilityToggle planeToggle;

        [Header("Obsah")]
        [SerializeField] PlaceableCatalog furniture;
        [SerializeField] PaintingLibrary paintings;
        [SerializeField] PlaceableObject paintingPrefab;
        [SerializeField] WindowViewLibrary windowViews;
        [SerializeField] PlaceableObject windowPrefab;

        [Header("UI")]
        [Tooltip("V poradí Nábytok, Obrazy, Okná.")]
        [SerializeField] Button[] tabs;
        [SerializeField] ScrollRect scroll;
        [Tooltip("Vypnutý vzor položky: Button, text a RawImage s náhľadom.")]
        [SerializeField] Button itemTemplate;
        [SerializeField] GameObject deleteButton;
        [SerializeField] TMP_Text planesLabel;
        [SerializeField] Color itemColor = Color.white;
        [SerializeField] Color tabColor = Color.clear;
        [SerializeField] Color textColor = new Color(0.24f, 0.21f, 0.19f);
        [SerializeField] Color selectedColor = new Color(0.76f, 0.42f, 0.31f);
        [SerializeField] Color selectedTextColor = Color.white;

        const float IconScale = 0.7f;

        readonly Dictionary<PlaceableItem, Image> m_Items = new Dictionary<PlaceableItem, Image>();
        PlacementCategory m_Tab;

        void Start()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                var category = (PlacementCategory)i;
                tabs[i].onClick.AddListener(() => ShowTab(category));
            }

            placement.SelectionChanged += Refresh;
            ShowTab(PlacementCategory.Nabytok);
        }

        void OnDestroy()
        {
            if (placement != null)
                placement.SelectionChanged -= Refresh;
        }

        public void TogglePlanes()
        {
            planeToggle.TogglePlanes();
            planesLabel.text = planeToggle.PlanesVisible ? "Skryť plochy" : "Ukázať plochy";
        }

        void ShowTab(PlacementCategory category)
        {
            m_Tab = category;
            placement.SelectItem(null);

            foreach (var image in m_Items.Values)
                Destroy(image.gameObject);
            m_Items.Clear();

            switch (category)
            {
                case PlacementCategory.Nabytok:
                    foreach (var entry in furniture.Entries)
                        if (entry.prefab != null)
                            AddItem(category, entry.displayName, entry.prefab, null, entry.icon);
                    break;
                case PlacementCategory.Obraz:
                    AddTextures(category, paintings, paintingPrefab);
                    break;
                case PlacementCategory.Okno:
                    AddTextures(category, windowViews, windowPrefab);
                    break;
            }

            scroll.horizontalNormalizedPosition = 0f;
            Refresh();
        }

        void AddTextures(PlacementCategory category, TextureLibrary library, PlaceableObject prefab)
        {
            foreach (var entry in library.Entries)
                if (entry.texture != null)
                    AddItem(category, entry.displayName, prefab, entry.texture);
        }

        void AddItem(PlacementCategory category, string displayName, PlaceableObject prefab, Texture2D texture, Texture2D icon = null)
        {
            var item = new PlaceableItem
            {
                category = category,
                displayName = displayName,
                prefab = prefab,
                texture = texture
            };

            var button = Instantiate(itemTemplate, scroll.content);
            button.onClick.AddListener(() => placement.SelectItem(item));

            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = displayName;

            var thumbnail = button.GetComponentInChildren<RawImage>(true);
            var image = texture != null ? texture : icon;
            if (image != null)
            {
                thumbnail.texture = image;
                var fitter = thumbnail.GetComponent<AspectRatioFitter>();
                fitter.aspectRatio = (float)image.width / image.height;
                if (texture == null)
                    thumbnail.transform.parent.localScale = Vector3.one * IconScale;
            }
            else
            {
                // Bez obrázka zaberie text celé tlačidlo.
                thumbnail.transform.parent.gameObject.SetActive(false);
                label.rectTransform.anchorMax = Vector2.one;
            }

            button.gameObject.SetActive(true);
            m_Items.Add(item, button.GetComponent<Image>());
        }

        void Refresh()
        {
            foreach (var pair in m_Items)
            {
                bool selected = pair.Key == placement.SelectedItem;
                Style(pair.Value, selected, itemColor);

                // Ikona nábytku má farbu textu, obrázky ostávajú bez zafarbenia.
                if (pair.Key.texture == null)
                    pair.Value.GetComponentInChildren<RawImage>(true).color = selected ? selectedTextColor : textColor;
            }

            for (int i = 0; i < tabs.Length; i++)
                Style(tabs[i].image, i == (int)m_Tab, tabColor);

            deleteButton.SetActive(placement.SelectedObject != null);
        }

        void Style(Image background, bool selected, Color normalColor)
        {
            background.color = selected ? selectedColor : normalColor;
            background.GetComponentInChildren<TMP_Text>(true).color = selected ? selectedTextColor : textColor;
        }
    }
}
