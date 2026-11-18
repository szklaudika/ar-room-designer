using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDesigner
{
    /// <summary>Tlačidlo v menu dole pre jednu položku katalógu (nábytok, obraz, okno).</summary>
    [RequireComponent(typeof(Button))]
    public class CatalogButton : MonoBehaviour
    {
        [SerializeField] RoomDesignerController controller;
        [SerializeField] int catalogIndex;
        [SerializeField] TMP_Text label;
        [SerializeField] Image background;
        [SerializeField] Color normalColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] Color selectedColor = new Color(1f, 0.6f, 0.1f, 1f);

        public void Configure(RoomDesignerController designer, int index, TMP_Text text, Image image)
        {
            controller = designer;
            catalogIndex = index;
            label = text;
            background = image;
        }

        void Awake()
        {
            if (controller == null)
                controller = FindAnyObjectByType<RoomDesignerController>();
            if (background == null)
                background = GetComponent<Image>();

            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (controller != null)
                    controller.SelectCatalogItem(catalogIndex);
            });
        }

        void OnEnable()
        {
            if (controller != null)
                controller.SelectionChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (controller != null)
                controller.SelectionChanged -= Refresh;
        }

        void Start()
        {
            // Názov podľa katalógu (ak sa katalóg zmení, tlačidlo sa prispôsobí).
            var entry = controller != null && controller.Catalog != null ? controller.Catalog.Get(catalogIndex) : null;
            if (entry != null && label != null)
                label.text = entry.displayName;
            Refresh();
        }

        void Refresh()
        {
            if (background != null && controller != null)
                background.color = controller.SelectedCatalogIndex == catalogIndex ? selectedColor : normalColor;
        }
    }
}
