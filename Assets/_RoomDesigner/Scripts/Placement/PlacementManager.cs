using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace RoomDesigner
{
    /// <summary>
    /// Umiestňovanie objektov:
    /// - ťuknutie na plochu položí objekt vybraný v menu a ukotví ho (ARAnchor),
    /// - ťuknutie na objekt ho vyberie, ťahanie jedným prstom ho posúva,
    /// - dva prsty menia veľkosť a otáčajú nábytok.
    /// </summary>
    public class PlacementManager : MonoBehaviour
    {
        [Header("AR")]
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] Camera arCamera;

        [Header("UI")]
        [SerializeField] TMP_Text hintText;
        [SerializeField] float messageDuration = 2.5f;

        [Header("Nastavenia")]
        [Tooltip("O koľko pixelov sa môže prst pohnúť, aby sa to ešte bralo ako ťuknutie.")]
        [SerializeField] float tapMaxMovePixels = 30f;
        [Tooltip("Odstup obrazu a okna od steny v metroch, aby neblikali.")]
        [SerializeField] float wallOffset = 0.01f;

        const float MaxDistance = 15f;

        /// <summary>Zmena vybranej položky v menu alebo vybraného objektu.</summary>
        public event Action SelectionChanged;

        public PlaceableItem SelectedItem { get; private set; }
        public PlaceableObject SelectedObject { get; private set; }
        public string CheckMark => RoomScanStatus.CheckMarkFor(hintText);

        readonly List<PlaceableObject> m_Placed = new List<PlaceableObject>();
        readonly HashSet<int> m_UiTouches = new HashSet<int>();

        static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
        static readonly List<RaycastResult> s_UiHits = new List<RaycastResult>();
        static readonly List<Touch> s_Touches = new List<Touch>();

        // Stav dotyku
        bool m_TapCandidate;
        int m_TouchId = -1;
        Vector2 m_TouchStart;
        PlaceableObject m_TouchedObject;
        bool m_Dragging;
        Vector2 m_DragScreenOffset;
        bool m_NeedsAnchor; // vybraný objekt sa pohol a treba ho znova ukotviť

        // Hlášky
        string m_Message;
        float m_MessageUntil;
        string m_LastHint;
        float m_ClearConfirmUntil;

        void Awake()
        {
            if (raycastManager == null) raycastManager = FindAnyObjectByType<ARRaycastManager>();
            if (planeManager == null) planeManager = FindAnyObjectByType<ARPlaneManager>();
            if (anchorManager == null) anchorManager = FindAnyObjectByType<ARAnchorManager>();
            if (arCamera == null) arCamera = Camera.main;
        }

        void OnEnable() => EnhancedTouchSupport.Enable();

        void OnDisable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            HandleTouches();
            UpdateHint();
        }

        // ================================================================== Menu / tlačidlá

        /// <summary>Vyberie položku z menu (opätovné ťuknutie alebo null výber zruší).</summary>
        public void SelectItem(PlaceableItem item)
        {
            SelectedItem = SelectedItem == item ? null : item;
            if (SelectedItem != null)
                SetSelectedObject(null);
            SelectionChanged?.Invoke();
        }

        public void DeleteSelected()
        {
            var obj = SelectedObject;
            if (obj == null)
                return;

            SetSelectedObject(null);
            m_Placed.Remove(obj);
            Remove(obj);
            ShowMessage("Objekt zmazaný");
            SelectionChanged?.Invoke();
        }

        /// <summary>Zmaže všetky objekty – vyžaduje druhé ťuknutie na potvrdenie.</summary>
        public void RequestClearAll()
        {
            if (m_Placed.Count == 0)
            {
                ShowMessage("Izba je prázdna");
                return;
            }

            if (Time.time > m_ClearConfirmUntil)
            {
                m_ClearConfirmUntil = Time.time + 3f;
                ShowMessage("Ťukni ešte raz na potvrdenie zmazania všetkého");
                return;
            }

            m_ClearConfirmUntil = 0f;
            SetSelectedObject(null);
            foreach (var obj in m_Placed)
                Remove(obj);
            m_Placed.Clear();
            ShowMessage("Všetko zmazané");
            SelectionChanged?.Invoke();
        }

        public void ShowMessage(string message)
        {
            m_Message = message;
            m_MessageUntil = Time.time + messageDuration;
        }

        // ================================================================== Dotyky

        void HandleTouches()
        {
            var active = Touch.activeTouches;

            s_Touches.Clear();
            foreach (var touch in active)
            {
                if (touch.phase == TouchPhase.Began && IsOverUI(touch.screenPosition))
                    m_UiTouches.Add(touch.touchId);

                if (!m_UiTouches.Contains(touch.touchId))
                    s_Touches.Add(touch);
            }

            if (s_Touches.Count >= 2)
            {
                // Dva prsty: otočenie + veľkosť. Ťuknutie ani posun už neplatí.
                m_TapCandidate = false;
                m_Dragging = false;
                if (SelectedObject != null)
                    ApplyTwoFingerGesture(s_Touches[0], s_Touches[1]);
            }
            else if (s_Touches.Count == 1)
            {
                HandleSingleTouch(s_Touches[0]);
            }
            else
            {
                m_TapCandidate = false;
                m_Dragging = false;
                ReanchorMoved();
            }

            foreach (var touch in active)
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    m_UiTouches.Remove(touch.touchId);
        }

        void HandleSingleTouch(Touch touch)
        {
            var pos = touch.screenPosition;

            if (touch.phase == TouchPhase.Began)
            {
                m_TapCandidate = true;
                m_Dragging = false;
                m_TouchId = touch.touchId;
                m_TouchStart = pos;
                m_TouchedObject = FindPlaced(pos);
                return;
            }

            if (touch.touchId != m_TouchId)
                return;

            switch (touch.phase)
            {
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (m_TapCandidate && (pos - m_TouchStart).magnitude > tapMaxMovePixels)
                    {
                        m_TapCandidate = false;
                        // Posúva sa len už vybraný objekt.
                        if (m_TouchedObject != null && m_TouchedObject == SelectedObject)
                            BeginDrag(SelectedObject, pos);
                    }
                    if (m_Dragging && SelectedObject != null)
                        Drag(SelectedObject, pos);
                    break;

                case TouchPhase.Ended:
                    if (m_TapCandidate)
                        HandleTap(pos);
                    m_TapCandidate = false;
                    m_Dragging = false;
                    break;

                case TouchPhase.Canceled:
                    m_TapCandidate = false;
                    m_Dragging = false;
                    break;
            }
        }

        void HandleTap(Vector2 screenPos)
        {
            if (m_TouchedObject != null)
            {
                SelectedItem = null;
                SetSelectedObject(m_TouchedObject);
                SelectionChanged?.Invoke();
                return;
            }

            if (SelectedItem == null)
            {
                // Ťuknutie do prázdna bez vybranej položky = zrušenie výberu objektu.
                if (SelectedObject != null)
                {
                    SetSelectedObject(null);
                    SelectionChanged?.Invoke();
                }
                return;
            }

            if (!RaycastNearestPlane(screenPos, out var hit, out var plane))
            {
                ShowMessage("Tu nie je nájdená žiadna plocha – naskenuj toto miesto");
                return;
            }

            if (!Accepts(SelectedItem.category, plane.alignment))
            {
                ShowMessage(WrongSurfaceMessage(SelectedItem.category));
                return;
            }

            Place(SelectedItem, hit.pose, plane);
        }

        bool RaycastNearestPlane(Vector2 screenPos, out ARRaycastHit hit, out ARPlane plane)
        {
            if (raycastManager.Raycast(screenPos, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                foreach (var candidate in s_Hits) // zoradené od najbližšieho
                {
                    plane = planeManager.GetPlane(candidate.trackableId);
                    if (plane != null)
                    {
                        hit = candidate;
                        return true;
                    }
                }
            }

            hit = default;
            plane = null;
            return false;
        }

        static bool Accepts(PlacementCategory category, PlaneAlignment alignment) =>
            alignment == (category == PlacementCategory.Nabytok ? PlaneAlignment.HorizontalUp : PlaneAlignment.Vertical);

        static string WrongSurfaceMessage(PlacementCategory category)
        {
            switch (category)
            {
                case PlacementCategory.Obraz: return "Obraz sa dá dať len na stenu";
                case PlacementCategory.Okno: return "Okno sa dá dať len na stenu";
                default: return "Nábytok sa dá dať len na podlahu alebo stôl";
            }
        }

        // ================================================================== Umiestnenie a kotvy

        void Place(PlaceableItem item, Pose hitPose, ARPlane plane)
        {
            var toCamera = arCamera.transform.position - hitPose.position;
            Vector3 position;
            Quaternion rotation;

            if (item.category == PlacementCategory.Nabytok)
            {
                // Nábytok sa otočí prednou stranou k používateľovi.
                toCamera.y = 0f;
                rotation = toCamera.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(toCamera, Vector3.up)
                    : Quaternion.identity;
                position = hitPose.position;
            }
            else
            {
                // Obraz/okno: predok (+Z) smeruje od steny k používateľovi, „hore“ je zvislo.
                var normal = hitPose.up;
                if (Vector3.Dot(normal, toCamera) < 0f)
                    normal = -normal;
                rotation = Quaternion.LookRotation(normal, Vector3.ProjectOnPlane(Vector3.up, normal));
                position = hitPose.position + normal * wallOffset;
            }

            var obj = Instantiate(item.prefab, position, rotation);
            obj.name = item.displayName;
            obj.PlaneId = plane.trackableId;
            if (obj.TryGetComponent<TextureApplier>(out var applier))
                applier.Apply(item.texture);
            m_Placed.Add(obj);
            AddAnchor(obj);

            SelectedItem = null;
            SetSelectedObject(obj);
            ShowMessage($"Pridané: {item.displayName}");
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Kotva je na prázdnom rodičovi, nie priamo na objekte: ARAnchor prepisuje polohu svojho objektu
        /// (posun prstom by nefungoval) a pri odstránení svoj objekt aj zničí.
        /// </summary>
        void AddAnchor(PlaceableObject obj)
        {
            if (anchorManager == null || !anchorManager.isActiveAndEnabled)
                return;

            var anchorGo = new GameObject("Anchor " + obj.name);
            anchorGo.transform.SetPositionAndRotation(obj.transform.position, obj.transform.rotation);
            var anchor = anchorGo.AddComponent<ARAnchor>();
            if (!anchor.enabled)
            {
                // Systém kotvu odmietol (napr. stratený tracking) – objekt ostane bez nej.
                Destroy(anchorGo);
                return;
            }

            anchor.destroyOnRemoval = false; // ak kotva zanikne, objekt má ostať
            obj.transform.SetParent(anchorGo.transform, true);
            obj.Anchor = anchor;
        }

        static void RemoveAnchor(PlaceableObject obj)
        {
            if (obj.Anchor == null)
                return;

            obj.transform.SetParent(null, true);
            Destroy(obj.Anchor.gameObject);
            obj.Anchor = null;
        }

        /// <summary>Po skončení posunu ukotví objekt na novom mieste.</summary>
        void ReanchorMoved()
        {
            if (!m_NeedsAnchor)
                return;

            m_NeedsAnchor = false;
            if (SelectedObject != null)
            {
                RemoveAnchor(SelectedObject);
                AddAnchor(SelectedObject);
            }
        }

        static void Remove(PlaceableObject obj)
        {
            if (obj == null)
                return;

            RemoveAnchor(obj);
            Destroy(obj.gameObject);
        }

        // ================================================================== Výber, posun, gestá

        void SetSelectedObject(PlaceableObject obj)
        {
            if (SelectedObject == obj)
                return;

            ReanchorMoved();
            if (SelectedObject != null)
                SelectedObject.SetSelected(false);
            SelectedObject = obj;
            if (obj != null)
                obj.SetSelected(true);
        }

        PlaceableObject FindPlaced(Vector2 screenPos)
        {
            // AR plochy nemajú collider, takže lúč trafí len položené objekty.
            return Physics.Raycast(arCamera.ScreenPointToRay(screenPos), out var hit, MaxDistance)
                ? hit.collider.GetComponentInParent<PlaceableObject>()
                : null;
        }

        void BeginDrag(PlaceableObject obj, Vector2 screenPos)
        {
            // Posun počítame od bodu, kde objekt stojí, aby pri chytení neskočil pod prst.
            var pivot = arCamera.WorldToScreenPoint(obj.transform.position);
            m_DragScreenOffset = pivot.z > 0f ? (Vector2)pivot - screenPos : Vector2.zero;
            m_Dragging = true;
        }

        void Drag(PlaceableObject obj, Vector2 screenPos)
        {
            var target = screenPos + m_DragScreenOffset;

            if (raycastManager.Raycast(target, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                foreach (var hit in s_Hits)
                {
                    if (obj.IsWallObject)
                    {
                        // Obraz a okno ostávajú na tej istej stene.
                        if (hit.trackableId != obj.PlaneId)
                            continue;
                        MoveTo(obj, hit.pose.position + obj.transform.forward * wallOffset);
                        return;
                    }

                    var plane = planeManager.GetPlane(hit.trackableId);
                    if (plane == null)
                        continue;
                    if (!Accepts(obj.Category, plane.alignment))
                        return; // prst je na stene – nábytok nepôjde za ňu
                    obj.PlaneId = plane.trackableId;
                    MoveTo(obj, hit.pose.position);
                    return;
                }
            }

            // Mimo naskenovanej časti plochy pokračujeme po jej predĺžení.
            var ray = arCamera.ScreenPointToRay(target);
            var surface = new Plane(obj.SurfaceNormal, obj.transform.position);
            if (surface.Raycast(ray, out float distance) && distance < MaxDistance)
                MoveTo(obj, ray.GetPoint(distance));
        }

        void MoveTo(PlaceableObject obj, Vector3 position)
        {
            obj.transform.position = position;
            m_NeedsAnchor = true;
        }

        void ApplyTwoFingerGesture(Touch a, Touch b)
        {
            var current = b.screenPosition - a.screenPosition;
            var previous = (b.screenPosition - b.delta) - (a.screenPosition - a.delta);
            if (previous.sqrMagnitude < 1f || current.sqrMagnitude < 1f)
                return;

            var obj = SelectedObject;
            obj.SetUniformScale(obj.UniformScale * current.magnitude / previous.magnitude);

            // Otáča sa len nábytok, okolo zvislej osi.
            if (!obj.IsWallObject)
                obj.transform.Rotate(Vector3.up, -Vector2.SignedAngle(previous, current), Space.World);
        }

        // ================================================================== UI pomocné

        static bool IsOverUI(Vector2 screenPos)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            s_UiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPos }, s_UiHits);
            return s_UiHits.Count > 0;
        }

        void UpdateHint()
        {
            if (hintText == null)
                return;

            string hint;
            if (Time.time < m_MessageUntil)
                hint = m_Message;
            else if (SelectedItem != null)
                hint = SelectedItem.category == PlacementCategory.Nabytok
                    ? $"{SelectedItem.displayName}: ťukni na podlahu alebo stôl"
                    : $"{SelectedItem.displayName}: ťukni na stenu (modrá)";
            else if (SelectedObject != null)
                hint = SelectedObject.IsWallObject
                    ? "Ťahaj prstom · dvoma prstami zmeň veľkosť"
                    : "Ťahaj prstom · dvoma prstami otoč a zmeň veľkosť";
            else if (m_Placed.Count == 0)
                hint = "Vyber objekt z menu dole";
            else
                hint = "Ťukni na objekt a uprav ho, alebo vyber ďalší z menu";

            if (hint != m_LastHint)
            {
                m_LastHint = hint;
                hintText.text = hint;
            }
        }
    }
}
