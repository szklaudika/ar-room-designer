using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RoomDesigner
{
    /// <summary>Čo daná AR plocha v izbe predstavuje.</summary>
    public enum PlaneKind
    {
        Unknown,
        /// <summary>Podlaha – najnižšia vodorovná plocha (a plochy v jej výške).</summary>
        Floor,
        /// <summary>Vyššia vodorovná plocha: stôl, polica, posteľ…</summary>
        Surface,
        Wall,
        Ceiling
    }

    /// <summary>
    /// Rozlišuje podlahu od stolov a políc. ARCore označí každú vodorovnú plochu ako HorizontalUp,
    /// preto za podlahu berieme iba najnižšiu vodorovnú plochu a plochy v jej výške (± tolerancia).
    /// Patrí na XR Origin vedľa ARPlaneManager.
    /// </summary>
    public class RoomPlaneClassifier : MonoBehaviour
    {
        [SerializeField] ARPlaneManager planeManager;

        [Tooltip("Plochy do tejto výšky nad najnižšou plochou sa ešte berú ako podlaha (m).")]
        [SerializeField] float floorTolerance = 0.2f;

        [Tooltip("Minimálna plocha (m²), aby sa vodorovná plocha mohla stať referenčnou podlahou.")]
        [SerializeField] float minFloorArea = 0.1f;

        [Tooltip("Menšie plochy (m²) sa zobrazia, ale nezapočítajú sa do štatistiky.")]
        [SerializeField] float minCountedArea = 0.1f;

        public static RoomPlaneClassifier Instance { get; private set; }

        /// <summary>Či už poznáme výšku podlahy.</summary>
        public bool HasFloor { get; private set; }

        /// <summary>Výška podlahy vo svetových súradniciach (os Y).</summary>
        public float FloorHeight { get; private set; }

        /// <summary>Zavolá sa po každej zmene plôch (už s prepočítanou podlahou).</summary>
        public event Action Changed;

        public ARPlaneManager PlaneManager => planeManager;

        void Awake()
        {
            if (planeManager == null)
                planeManager = GetComponent<ARPlaneManager>();
            if (planeManager == null)
                planeManager = FindAnyObjectByType<ARPlaneManager>();
        }

        void OnEnable()
        {
            Instance = this;
            if (planeManager != null)
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            Recompute();
        }

        void OnDisable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
            if (Instance == this)
                Instance = null;
        }

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            Recompute();
            Changed?.Invoke();
        }

        void Recompute()
        {
            float lowestLarge = float.PositiveInfinity;
            float lowestAny = float.PositiveInfinity;

            if (planeManager != null)
            {
                foreach (var plane in planeManager.trackables)
                {
                    if (!IsActive(plane) || plane.alignment != PlaneAlignment.HorizontalUp)
                        continue;

                    float y = plane.center.y;
                    lowestAny = Mathf.Min(lowestAny, y);
                    if (Area(plane) >= minFloorArea)
                        lowestLarge = Mathf.Min(lowestLarge, y);
                }
            }

            if (!float.IsInfinity(lowestLarge))
            {
                HasFloor = true;
                FloorHeight = lowestLarge;
            }
            else if (!float.IsInfinity(lowestAny))
            {
                HasFloor = true;
                FloorHeight = lowestAny;
            }
            else
            {
                HasFloor = false;
            }
        }

        public PlaneKind Classify(ARPlane plane)
        {
            if (plane == null)
                return PlaneKind.Unknown;

            if (plane.alignment != PlaneAlignment.HorizontalUp)
                return FromAlignment(plane.alignment);

            if (!HasFloor)
                return PlaneKind.Floor;

            return plane.center.y <= FloorHeight + floorTolerance ? PlaneKind.Floor : PlaneKind.Surface;
        }

        /// <summary>Či plochu započítať do štatistiky (nie je zlúčená a nie je príliš malá).</summary>
        public bool IsSignificant(ARPlane plane) => IsActive(plane) && Area(plane) >= minCountedArea;

        public static bool IsActive(ARPlane plane) =>
            plane != null && plane.subsumedBy == null && plane.trackingState != TrackingState.None;

        public static float Area(ARPlane plane) => plane.size.x * plane.size.y;

        /// <summary>Záložné rozdelenie len podľa orientácie (keď klasifikátor v scéne nie je).</summary>
        public static PlaneKind FromAlignment(PlaneAlignment alignment)
        {
            switch (alignment)
            {
                case PlaneAlignment.HorizontalUp: return PlaneKind.Floor;
                case PlaneAlignment.HorizontalDown: return PlaneKind.Ceiling;
                case PlaneAlignment.Vertical: return PlaneKind.Wall;
                default: return PlaneKind.Unknown;
            }
        }

        /// <summary>Druh plochy – cez klasifikátor, ak existuje, inak podľa orientácie.</summary>
        public static PlaneKind KindOf(ARPlane plane) =>
            Instance != null ? Instance.Classify(plane) : FromAlignment(plane.alignment);
    }
}
