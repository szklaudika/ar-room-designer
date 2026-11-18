using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RoomDesigner
{
    /// <summary>
    /// Sleduje nájdené plochy (ARPlaneManager.trackablesChanged) a v UI ukazuje stav skenovania izby.
    /// </summary>
    public class RoomScanStatus : MonoBehaviour
    {
        public enum ScanState { Scanning, FloorFound, FloorAndWallFound }

        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] TMP_Text statusText;

        [Header("Texty")]
        [SerializeField] string scanningMessage = "Pomaly prejdi mobilom po izbe...";
        [SerializeField] string floorFoundMessage = "Podlaha nájdená";
        [SerializeField] string readyMessage = "Podlaha aj stena nájdená – môžeš začať";

        public ScanState State { get; private set; } = ScanState.Scanning;
        public int FloorCount { get; private set; }
        public int WallCount { get; private set; }

        /// <summary>Do UI pôjde "✓", ak ho font vie zobraziť; inak náhradný text.</summary>
        string m_CheckMark = "✓";

        void Awake()
        {
            if (planeManager == null)
                planeManager = FindAnyObjectByType<ARPlaneManager>();
            if (statusText == null)
                statusText = GetComponent<TMP_Text>();

            if (statusText != null && statusText.font != null &&
                !statusText.font.HasCharacter('✓', searchFallbacks: true, tryAddCharacter: true))
            {
                m_CheckMark = "(OK)";
            }
        }

        void OnEnable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            Recount();
        }

        void OnDisable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            // Pri pridaní/odobratí/zmene sa počty prepočítajú z aktuálneho zoznamu plôch.
            Recount();
        }

        void Recount()
        {
            int floors = 0, walls = 0;

            if (planeManager != null)
            {
                foreach (var plane in planeManager.trackables)
                {
                    // Plochy zlúčené do inej (subsumed) nepočítame dvakrát.
                    if (plane.subsumedBy != null || plane.trackingState == TrackingState.None)
                        continue;

                    if (plane.alignment == PlaneAlignment.HorizontalUp) floors++;
                    else if (plane.alignment == PlaneAlignment.Vertical) walls++;
                }
            }

            FloorCount = floors;
            WallCount = walls;
            State = floors > 0 && walls > 0 ? ScanState.FloorAndWallFound
                  : floors > 0 ? ScanState.FloorFound
                  : ScanState.Scanning;

            UpdateText();
        }

        void UpdateText()
        {
            if (statusText == null)
                return;

            string headline;
            switch (State)
            {
                case ScanState.FloorFound: headline = $"{floorFoundMessage} {m_CheckMark}"; break;
                case ScanState.FloorAndWallFound: headline = $"{readyMessage} {m_CheckMark}"; break;
                default: headline = scanningMessage; break;
            }

            statusText.text = $"{headline}\n<size=75%>Podlahy: {FloorCount}   Steny: {WallCount}</size>";
        }
    }
}
