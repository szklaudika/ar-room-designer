using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDesigner
{
    /// <summary>Akčné tlačidlo (zmazať, plochy, uložiť, načítať, vymazať všetko).</summary>
    [RequireComponent(typeof(Button))]
    public class DesignerActionButton : MonoBehaviour
    {
        public enum ActionType { DeleteSelected, TogglePlanes, Save, Load, ClearAll }

        [SerializeField] ActionType action;
        [SerializeField] RoomDesignerController controller;
        [SerializeField] PlaneVisibilityToggle planeToggle;
        [SerializeField] TMP_Text label;

        Button m_Button;

        public void Configure(ActionType actionType, RoomDesignerController designer, PlaneVisibilityToggle toggle, TMP_Text text)
        {
            action = actionType;
            controller = designer;
            planeToggle = toggle;
            label = text;
        }

        void Awake()
        {
            if (controller == null)
                controller = FindAnyObjectByType<RoomDesignerController>();
            if (planeToggle == null)
                planeToggle = FindAnyObjectByType<PlaneVisibilityToggle>();

            m_Button = GetComponent<Button>();
            m_Button.onClick.AddListener(OnClick);
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

        void OnClick()
        {
            if (controller == null)
                return;

            switch (action)
            {
                case ActionType.DeleteSelected: controller.DeleteSelected(); break;
                case ActionType.Save: controller.SaveDesign(); break;
                case ActionType.Load: controller.LoadDesign(); break;
                case ActionType.ClearAll: controller.RequestClearAll(); break;
                case ActionType.TogglePlanes:
                    if (planeToggle != null)
                        planeToggle.TogglePlanes();
                    break;
            }

            Refresh();
        }

        void Refresh()
        {
            if (m_Button == null)
                return;

            if (action == ActionType.DeleteSelected)
                m_Button.interactable = controller != null && controller.HasSelection;

            if (action == ActionType.TogglePlanes && label != null && planeToggle != null)
                label.text = planeToggle.PlanesVisible ? "Skryť plochy" : "Ukázať plochy";
        }
    }
}
