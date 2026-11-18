using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RoomDesigner
{
    /// <summary>
    /// Skrytie/zobrazenie všetkých AR plôch. Detekcia beží ďalej, mení sa len viditeľnosť.
    /// Metódy sa dajú napojiť na UI tlačidlo (OnClick) alebo Toggle (OnValueChanged).
    /// </summary>
    public class PlaneVisibilityToggle : MonoBehaviour
    {
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] bool planesVisible = true;

        public bool PlanesVisible => planesVisible;

        void Awake()
        {
            if (planeManager == null)
                planeManager = FindAnyObjectByType<ARPlaneManager>();
        }

        void OnEnable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            ApplyToAll();
        }

        void OnDisable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        /// <summary>Nastaví viditeľnosť všetkých plôch (aj tých, ktoré sa nájdu neskôr).</summary>
        public void SetPlanesVisible(bool visible)
        {
            planesVisible = visible;
            ApplyToAll();
        }

        public void ShowPlanes() => SetPlanesVisible(true);

        public void HidePlanes() => SetPlanesVisible(false);

        public void TogglePlanes() => SetPlanesVisible(!planesVisible);

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            // Nové plochy prevezmú aktuálny stav viditeľnosti.
            foreach (var plane in args.added)
                plane.gameObject.SetActive(planesVisible);
        }

        void ApplyToAll()
        {
            if (planeManager == null)
                return;

            foreach (var plane in planeManager.trackables)
                plane.gameObject.SetActive(planesVisible);
        }
    }
}
