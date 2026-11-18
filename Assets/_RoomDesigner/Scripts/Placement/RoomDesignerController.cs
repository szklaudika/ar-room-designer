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
    /// Hlavná logika návrhu izby:
    /// - výber objektu z menu a umiestnenie ťuknutím na podlahu/stenu,
    /// - ťuknutie na objekt = výber, ťahanie jedným prstom = posun,
    /// - dva prsty = otočenie a zmena veľkosti,
    /// - zmazanie, uloženie a načítanie návrhu.
    /// </summary>
    public class RoomDesignerController : MonoBehaviour
    {
        [Header("AR")]
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] RoomPlaneClassifier classifier;
        [SerializeField] Camera arCamera;

        [Header("Obsah")]
        [SerializeField] PlaceableCatalog catalog;

        [Header("UI")]
        [SerializeField] TMP_Text hintText;
        [SerializeField] float messageDuration = 2.5f;

        [Header("Nastavenia")]
        [Tooltip("Pripnúť objekty k AR kotvám (stabilnejšie pri dlhšom používaní).")]
        [SerializeField] bool useAnchors = true;
        [Tooltip("O koľko pixelov sa môže prst pohnúť, aby sa to ešte bralo ako ťuknutie.")]
        [SerializeField] float tapMaxMovePixels = 30f;

        /// <summary>Zmena vybranej položky v menu alebo vybraného objektu.</summary>
        public event Action SelectionChanged;

        public PlaceableCatalog Catalog => catalog;
        public int SelectedCatalogIndex { get; private set; } = -1;
        public PlaceableObject SelectedObject { get; private set; }
        public bool HasSelection => SelectedObject != null;
        public int PlacedCount => m_Placed.Count;

        readonly List<PlaceableObject> m_Placed = new List<PlaceableObject>();
        readonly HashSet<int> m_UiTouches = new HashSet<int>();

        static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
        static readonly List<RaycastResult> s_UiHits = new List<RaycastResult>();
        static readonly List<Touch> s_Touches = new List<Touch>();

        PointerEventData m_PointerData;
        EventSystem m_PointerEventSystem;

        // Stav dotyku
        bool m_TapCandidate;
        int m_TapTouchId = -1;
        Vector2 m_TapStart;
        bool m_Dragging;
        int m_DragTouchId = -1;
        Vector3 m_DragOffset;

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
            if (classifier == null) classifier = FindAnyObjectByType<RoomPlaneClassifier>();
            if (arCamera == null) arCamera = Camera.main;
        }

        void OnEnable() => EnhancedTouchSupport.Enable();

        void OnDisable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            m_Placed.RemoveAll(p => p == null); // objekt mohol zaniknúť spolu s AR kotvou
            if (SelectedObject == null && !ReferenceEquals(SelectedObject, null))
            {
                SelectedObject = null; // vybraný objekt bol zničený
                NotifySelectionChanged();
            }

            HandleTouches();
            UpdateHint();
        }

        // ================================================================== Menu / tlačidlá

        /// <summary>Vyberie položku z menu (opätovné ťuknutie výber zruší).</summary>
        public void SelectCatalogItem(int index)
        {
            if (catalog == null || catalog.Get(index) == null)
                return;

            SelectedCatalogIndex = SelectedCatalogIndex == index ? -1 : index;
            SetSelectedObject(null);
            NotifySelectionChanged();
        }

        public void DeleteSelected()
        {
            var obj = SelectedObject;
            if (obj == null)
                return;

            SetSelectedObject(null);
            m_Placed.Remove(obj);
            RemoveItem(obj);
            ShowMessage("Objekt zmazaný");
            NotifySelectionChanged();
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
            ClearAll();
            ShowMessage("Všetko zmazané");
        }

        public void SaveDesign()
        {
            var data = new DesignData();
            if (classifier != null && classifier.HasFloor)
            {
                data.hasFloor = true;
                data.floorHeight = classifier.FloorHeight;
            }

            foreach (var obj in m_Placed)
            {
                if (obj == null) continue;
                data.items.Add(new PlacedItemData
                {
                    id = obj.ItemId,
                    position = obj.transform.position,
                    rotation = obj.transform.rotation,
                    scale = obj.UniformScale
                });
            }

            ShowMessage(DesignSaveSystem.Save(data)
                ? $"Návrh uložený ({data.items.Count} obj.)"
                : "Uloženie zlyhalo");
        }

        public void LoadDesign()
        {
            if (!DesignSaveSystem.TryLoad(out var data))
            {
                ShowMessage("Zatiaľ nie je uložený žiadny návrh");
                return;
            }

            ClearAll();

            // AR session začína vo výške mobilu pri spustení – posun vyrovnáme podľa výšky podlahy.
            float dy = data.hasFloor && classifier != null && classifier.HasFloor
                ? classifier.FloorHeight - data.floorHeight
                : 0f;

            int loaded = 0;
            foreach (var item in data.items)
            {
                var entry = catalog != null ? catalog.Find(item.id) : null;
                if (entry == null || entry.prefab == null)
                {
                    Debug.LogWarning($"[Room Designer] Objekt '{item.id}' nie je v katalógu, preskakujem.");
                    continue;
                }

                Spawn(entry, item.position + Vector3.up * dy, item.rotation, item.scale);
                loaded++;
            }

            ShowMessage(classifier != null && classifier.HasFloor
                ? $"Návrh načítaný ({loaded} obj.)"
                : $"Návrh načítaný ({loaded} obj.) – najprv naskenuj podlahu pre presnú výšku");
            NotifySelectionChanged();
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
            }

            foreach (var touch in active)
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    m_UiTouches.Remove(touch.touchId);
        }

        void HandleSingleTouch(Touch touch)
        {
            var pos = touch.screenPosition;
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (TryHitPlaced(pos, out var hitObj, out var hitPoint))
                    {
                        // Ťuknutie na objekt = výber a začiatok posúvania.
                        if (SelectedCatalogIndex >= 0)
                            SelectedCatalogIndex = -1;
                        SetSelectedObject(hitObj);
                        NotifySelectionChanged();
                        BeginDrag(hitObj, pos);
                        m_DragTouchId = touch.touchId;
                        m_TapCandidate = false;
                    }
                    else
                    {
                        m_TapCandidate = true;
                        m_TapTouchId = touch.touchId;
                        m_TapStart = pos;
                    }
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (m_TapCandidate && touch.touchId == m_TapTouchId && (pos - m_TapStart).magnitude > tapMaxMovePixels)
                        m_TapCandidate = false;
                    if (m_Dragging && touch.touchId == m_DragTouchId && SelectedObject != null)
                        Drag(SelectedObject, pos);
                    break;

                case TouchPhase.Ended:
                    if (m_TapCandidate && touch.touchId == m_TapTouchId)
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
            var entry = catalog != null ? catalog.Get(SelectedCatalogIndex) : null;
            if (entry == null)
            {
                // Ťuknutie do prázdna bez vybranej položky = zrušenie výberu objektu.
                if (SelectedObject != null)
                {
                    SetSelectedObject(null);
                    NotifySelectionChanged();
                }
                return;
            }

            if (raycastManager == null || !raycastManager.Raycast(screenPos, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                ShowMessage(entry.surface == PlacementSurface.Floor
                    ? "Tu nie je nájdená podlaha – ťukni na zelenú plochu"
                    : "Tu nie je nájdená stena – ťukni na modrú plochu");
                return;
            }

            foreach (var hit in s_Hits) // zoradené od najbližšieho
            {
                var plane = planeManager != null ? planeManager.GetPlane(hit.trackableId) : null;
                if (plane == null)
                    continue;

                if (Accepts(entry, RoomPlaneClassifier.KindOf(plane)))
                {
                    Place(entry, hit.pose, plane);
                    return;
                }
            }

            ShowMessage(entry.surface == PlacementSurface.Floor
                ? $"{entry.displayName} patrí na podlahu (zelená plocha)"
                : $"{entry.displayName} patrí na stenu (modrá plocha)");
        }

        static bool Accepts(PlaceableCatalog.Entry entry, PlaneKind kind)
        {
            if (entry.surface == PlacementSurface.Wall)
                return kind == PlaneKind.Wall;
            return kind == PlaneKind.Floor || (entry.allowOnFurnitureSurfaces && kind == PlaneKind.Surface);
        }

        // ================================================================== Umiestnenie

        void Place(PlaceableCatalog.Entry entry, Pose hitPose, ARPlane plane)
        {
            var camPos = arCamera != null ? arCamera.transform.position : hitPose.position + Vector3.back;
            Vector3 position;
            Quaternion rotation;

            if (entry.surface == PlacementSurface.Floor)
            {
                // Nábytok sa otočí prednou stranou k používateľovi.
                var toCamera = camPos - hitPose.position;
                toCamera.y = 0f;
                rotation = toCamera.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(toCamera.normalized, Vector3.up)
                    : Quaternion.identity;
                position = hitPose.position;
            }
            else
            {
                // Obraz/okno: predok (+Z) smeruje von zo steny k používateľovi, „hore“ je zvislo.
                var normal = plane.normal;
                if (Vector3.Dot(normal, camPos - hitPose.position) < 0f)
                    normal = -normal;
                var up = Vector3.ProjectOnPlane(Vector3.up, normal);
                if (up.sqrMagnitude < 1e-4f)
                    up = Vector3.ProjectOnPlane(Vector3.forward, normal);
                rotation = Quaternion.LookRotation(normal, up.normalized);
                position = hitPose.position + normal * 0.005f; // kúsok od steny, aby neblikal
            }

            var obj = Spawn(entry, position, rotation, 1f);
            AttachToAnchor(obj, plane, new Pose(position, rotation));

            SelectedCatalogIndex = -1;
            SetSelectedObject(obj);
            ShowMessage($"Pridané: {entry.displayName}");
            NotifySelectionChanged();
        }

        PlaceableObject Spawn(PlaceableCatalog.Entry entry, Vector3 position, Quaternion rotation, float scale)
        {
            var obj = Instantiate(entry.prefab, position, rotation);
            obj.name = entry.prefab.name;
            obj.SetUniformScale(scale);
            m_Placed.Add(obj);
            return obj;
        }

        void AttachToAnchor(PlaceableObject obj, ARPlane plane, Pose pose)
        {
            if (!useAnchors || anchorManager == null || !anchorManager.enabled || plane == null)
                return;

            try
            {
                var anchor = anchorManager.AttachAnchor(plane, pose);
                if (anchor != null)
                {
                    obj.transform.SetParent(anchor.transform, true);
                    obj.Anchor = anchor;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Room Designer] AR kotvu sa nepodarilo vytvoriť: {e.Message}");
            }
        }

        void RemoveItem(PlaceableObject obj)
        {
            if (obj == null)
                return;

            var anchor = obj.Anchor;
            Destroy(obj.gameObject);

            if (anchor != null)
            {
                bool removed = false;
                try
                {
                    if (anchorManager != null && anchorManager.enabled)
                        removed = anchorManager.TryRemoveAnchor(anchor);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Room Designer] AR kotvu sa nepodarilo odstrániť: {e.Message}");
                }

                if (!removed)
                    Destroy(anchor.gameObject);
            }
        }

        void ClearAll()
        {
            SetSelectedObject(null);
            foreach (var obj in m_Placed)
                RemoveItem(obj);
            m_Placed.Clear();
            NotifySelectionChanged();
        }

        // ================================================================== Výber, posun, gestá

        void SetSelectedObject(PlaceableObject obj)
        {
            if (SelectedObject == obj)
                return;
            if (SelectedObject != null)
                SelectedObject.SetSelected(false);
            SelectedObject = obj;
            if (obj != null)
                obj.SetSelected(true);
        }

        bool TryHitPlaced(Vector2 screenPos, out PlaceableObject obj, out Vector3 point)
        {
            obj = null;
            point = default;
            if (arCamera == null)
                return false;

            var ray = arCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 30f))
                return false;

            obj = hit.collider.GetComponentInParent<PlaceableObject>();
            point = hit.point;
            return obj != null && m_Placed.Contains(obj);
        }

        /// <summary>Rovina, po ktorej sa objekt posúva: podlaha (vodorovná) alebo stena.</summary>
        static Plane MovementPlane(PlaceableObject obj) => new Plane(obj.SurfaceNormal, obj.transform.position);

        void BeginDrag(PlaceableObject obj, Vector2 screenPos)
        {
            m_Dragging = false;
            var ray = arCamera.ScreenPointToRay(screenPos);
            var plane = MovementPlane(obj);
            if (plane.Raycast(ray, out float enter) && enter < 20f)
            {
                m_DragOffset = obj.transform.position - ray.GetPoint(enter);
                m_Dragging = true;
            }
        }

        void Drag(PlaceableObject obj, Vector2 screenPos)
        {
            var ray = arCamera.ScreenPointToRay(screenPos);
            var plane = MovementPlane(obj);
            if (plane.Raycast(ray, out float enter) && enter < 20f)
                obj.transform.position = ray.GetPoint(enter) + m_DragOffset;
        }

        void ApplyTwoFingerGesture(Touch a, Touch b)
        {
            var current = b.screenPosition - a.screenPosition;
            var previous = (b.screenPosition - b.delta) - (a.screenPosition - a.delta);
            if (previous.sqrMagnitude < 1f || current.sqrMagnitude < 1f)
                return;

            var obj = SelectedObject;

            // Otočenie: proti smeru hodín na obrazovke = proti smeru hodín pri pohľade na plochu.
            float angle = Vector2.SignedAngle(previous, current);
            obj.transform.Rotate(obj.SurfaceNormal, -angle, Space.World);

            // Veľkosť: roztiahnutie prstov = zväčšenie.
            float factor = current.magnitude / previous.magnitude;
            obj.SetUniformScale(obj.UniformScale * factor);
        }

        // ================================================================== UI pomocné

        bool IsOverUI(Vector2 screenPos)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            if (m_PointerData == null || m_PointerEventSystem != eventSystem)
            {
                m_PointerData = new PointerEventData(eventSystem);
                m_PointerEventSystem = eventSystem;
            }

            m_PointerData.position = screenPos;
            s_UiHits.Clear();
            eventSystem.RaycastAll(m_PointerData, s_UiHits);
            return s_UiHits.Count > 0;
        }

        public void ShowMessage(string message)
        {
            m_Message = message;
            m_MessageUntil = Time.time + messageDuration;
        }

        void UpdateHint()
        {
            if (hintText == null)
                return;

            string hint;
            var entry = catalog != null ? catalog.Get(SelectedCatalogIndex) : null;
            if (Time.time < m_MessageUntil)
                hint = m_Message;
            else if (entry != null)
                hint = entry.surface == PlacementSurface.Floor
                    ? $"{entry.displayName}: ťukni na zelenú podlahu"
                    : $"{entry.displayName}: ťukni na modrú stenu";
            else if (SelectedObject != null)
                hint = "Ťahaj prstom · dvoma prstami otoč a zmeň veľkosť";
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

        void NotifySelectionChanged() => SelectionChanged?.Invoke();
    }
}
