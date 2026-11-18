using System.Collections.Generic;
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
    /// Test detekcie: ťuknutie na nájdenú plochu položí na to miesto malú kocku (0,1 m).
    /// Ťuknutie mimo plochy alebo na UI nič nespraví.
    /// </summary>
    public class TapToPlaceTest : MonoBehaviour
    {
        [SerializeField] ARRaycastManager raycastManager;

        [Tooltip("Prefab kocky. Ak chýba, vytvorí sa primitívna kocka.")]
        [SerializeField] GameObject cubePrefab;

        [SerializeField] float cubeSize = 0.1f;

        static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
        static readonly List<RaycastResult> s_UiHits = new List<RaycastResult>();

        readonly List<GameObject> m_Spawned = new List<GameObject>();
        PointerEventData m_PointerData;
        EventSystem m_PointerEventSystem;

        public IReadOnlyList<GameObject> SpawnedCubes => m_Spawned;

        void Awake()
        {
            if (raycastManager == null)
                raycastManager = FindAnyObjectByType<ARRaycastManager>();
        }

        void OnEnable() => EnhancedTouchSupport.Enable();

        void OnDisable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            if (raycastManager == null)
                return;

            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != TouchPhase.Began)
                    continue;

                var screenPos = touch.screenPosition;
                if (IsOverUI(screenPos))
                    continue;

                if (raycastManager.Raycast(screenPos, s_Hits, TrackableType.PlaneWithinPolygon))
                    PlaceCube(s_Hits[0].pose); // prvý zásah = najbližší
            }
        }

        void PlaceCube(Pose pose)
        {
            GameObject cube;
            if (cubePrefab != null)
            {
                cube = Instantiate(cubePrefab);
            }
            else
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cube.GetComponent<Collider>());
            }

            cube.name = $"TestCube_{m_Spawned.Count + 1}";
            cube.transform.localScale = Vector3.one * cubeSize;
            // Pose.up je normála plochy – kocka sedí na podlahe aj na stene, nie je v nej zapustená.
            cube.transform.SetPositionAndRotation(pose.position + pose.up * (cubeSize * 0.5f), pose.rotation);
            m_Spawned.Add(cube);
        }

        /// <summary>Zmaže všetky položené kocky (pre neskoršie UI).</summary>
        public void ClearCubes()
        {
            foreach (var cube in m_Spawned)
                if (cube != null) Destroy(cube);
            m_Spawned.Clear();
        }

        bool IsOverUI(Vector2 screenPos)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            // Vlastný UI raycast – IsPointerOverGameObject() je pri dotyku s novým Input Systemom nespoľahlivé.
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
    }
}
