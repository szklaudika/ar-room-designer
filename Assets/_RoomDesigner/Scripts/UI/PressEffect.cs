using UnityEngine;
using UnityEngine.EventSystems;

namespace RoomDesigner
{
    /// <summary>Tlačidlo sa pri stlačení jemne zmenší.</summary>
    public class PressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        const float PressedScale = 0.94f, Speed = 18f;

        float m_Target = 1f;

        public void OnPointerDown(PointerEventData eventData) => m_Target = PressedScale;

        public void OnPointerUp(PointerEventData eventData) => m_Target = 1f;

        void OnDisable()
        {
            m_Target = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float scale = Mathf.Lerp(transform.localScale.x, m_Target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
