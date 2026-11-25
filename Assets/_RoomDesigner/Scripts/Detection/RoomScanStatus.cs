using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RoomDesigner
{
    /// <summary>
    /// Sleduje nájdené plochy a v UI ukazuje stav skenovania izby.
    /// Počíta len väčšie plochy (malé kúsky stien sa nezapočítajú) a stoly/police nepočíta ako podlahu.
    /// </summary>
    public class RoomScanStatus : MonoBehaviour
    {
        public enum ScanState { Scanning, FloorFound, FloorAndWallFound }

        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] RoomPlaneClassifier classifier;
        [SerializeField] TMP_Text statusText;

        [Header("Texty")]
        [SerializeField] string scanningMessage = "Pomaly prejdi mobilom po izbe...";
        [SerializeField] string floorFoundMessage = "Podlaha nájdená – nájdi ešte stenu";
        [SerializeField] string readyMessage = "Podlaha aj stena nájdená";

        public ScanState State { get; private set; } = ScanState.Scanning;
        public int FloorCount { get; private set; }
        public int WallCount { get; private set; }
        public int SurfaceCount { get; private set; }

        /// <summary>Do UI pôjde "✓", ak ho font vie zobraziť; inak náhradný text.</summary>
        string m_CheckMark = "✓";

        void Awake()
        {
            if (planeManager == null)
                planeManager = FindAnyObjectByType<ARPlaneManager>();
            if (classifier == null)
                classifier = FindAnyObjectByType<RoomPlaneClassifier>();
            if (statusText == null)
                statusText = GetComponent<TMP_Text>();

            m_CheckMark = CheckMarkFor(statusText);
        }

        /// <summary>"✓", ak ho font vie zobraziť, inak náhradný text.</summary>
        public static string CheckMarkFor(TMP_Text text) =>
            text != null && text.font != null &&
            !text.font.HasCharacter('✓', searchFallbacks: true, tryAddCharacter: true) ? "(OK)" : "✓";

        void OnEnable()
        {
            // S klasifikátorom čakáme na jeho udalosť, aby bola výška podlahy už prepočítaná.
            if (classifier != null)
                classifier.Changed += Recount;
            else if (planeManager != null)
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            Recount();
        }

        void OnDisable()
        {
            if (classifier != null)
                classifier.Changed -= Recount;
            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args) => Recount();

        void Recount()
        {
            int floors = 0, walls = 0, surfaces = 0;

            if (planeManager != null)
            {
                foreach (var plane in planeManager.trackables)
                {
                    bool significant = classifier != null
                        ? classifier.IsSignificant(plane)
                        : RoomPlaneClassifier.IsActive(plane);
                    if (!significant)
                        continue;

                    switch (classifier != null ? classifier.Classify(plane) : RoomPlaneClassifier.FromAlignment(plane.alignment))
                    {
                        case PlaneKind.Floor: floors++; break;
                        case PlaneKind.Wall: walls++; break;
                        case PlaneKind.Surface: surfaces++; break;
                    }
                }
            }

            FloorCount = floors;
            WallCount = walls;
            SurfaceCount = surfaces;
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

            statusText.text = $"{headline}\n<size=75%>Podlaha: {FloorCount}   Steny: {WallCount}   Stoly/police: {SurfaceCount}</size>";
        }
    }
}
