using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using InputTrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace RoomDesigner.EditorTools
{
    /// <summary>
    /// Menu Tools → Room Designer → Vytvoriť scénu.
    /// Vytvorí (alebo prepíše) scénu RoomDetection so všetkým: AR, detekcia plôch, menu s nábytkom,
    /// gestá, uloženie návrhu. Assety pripraví RoomDesignerAssetBuilder.
    /// </summary>
    public static class RoomDetectionSceneSetup
    {
        const string ScenesFolder = RoomDesignerAssetBuilder.Root + "/Scenes";
        const string ScenePath = ScenesFolder + "/RoomDetection.unity";

        static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.92f);
        static readonly Color ButtonTextColor = new Color(0.12f, 0.16f, 0.22f);

        [MenuItem("Tools/Room Designer/Vytvoriť scénu")]
        public static void CreateScene()
        {
            // Nech sa nestratia neuložené zmeny v práve otvorenej scéne.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                RoomDesignerAssetBuilder.EnsureFolder(ScenesFolder);
                var assets = RoomDesignerAssetBuilder.BuildAll();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                CreateLight();
                CreateARSession();
                var origin = CreateXROrigin(assets.planePrefab);
                var planeManager = origin.GetComponent<ARPlaneManager>();
                var raycastManager = origin.GetComponent<ARRaycastManager>();
                var anchorManager = origin.GetComponent<ARAnchorManager>();
                var classifier = origin.GetComponent<RoomPlaneClassifier>();

                CreateEventSystem();

                var designerGo = new GameObject("Room Designer");
                var controller = designerGo.AddComponent<RoomDesignerController>();
                var planeToggle = designerGo.AddComponent<PlaneVisibilityToggle>();
                SetRef(planeToggle, "planeManager", planeManager);

                var canvas = CreateCanvas();
                CreateStatusPanel(canvas, planeManager, classifier);
                var hint = CreateDesignerPanel(canvas, controller, planeToggle, assets.catalog);

                SetRef(controller, "raycastManager", raycastManager);
                SetRef(controller, "planeManager", planeManager);
                SetRef(controller, "anchorManager", anchorManager);
                SetRef(controller, "classifier", classifier);
                SetRef(controller, "arCamera", origin.Camera);
                SetRef(controller, "catalog", assets.catalog);
                SetRef(controller, "hintText", hint);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    Debug.LogError($"[Room Designer] Scénu sa nepodarilo uložiť: {ScenePath}");
                    return;
                }

                AddSceneToBuildSettingsFirst(ScenePath);
                AssetDatabase.SaveAssets();

                Selection.activeGameObject = designerGo;
                Debug.Log($"[Room Designer] Scéna vytvorená: {ScenePath} (prvá v Build Settings).");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Room Designer", "Vytvorenie scény zlyhalo:\n" + e.Message, "OK");
            }
        }

        // ================================================================== AR

        static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void CreateARSession()
        {
            // Rovnaké komponenty ako GameObject → XR → AR Session.
            new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
        }

        static XROrigin CreateXROrigin(GameObject planePrefab)
        {
            // Zodpovedá GameObject → XR → XR Origin (Mobile AR) z AR Foundation 6.
            var originGo = new GameObject("XR Origin (Mobile AR)", typeof(XROrigin));
            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);

            var cameraGo = new GameObject("Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(ARCameraManager),
                typeof(ARCameraBackground),
                typeof(InputTrackedPoseDriver));
            cameraGo.transform.SetParent(offsetGo.transform, false);
            cameraGo.tag = "MainCamera";

            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;

            // Zadná kamera (World facing) – potrebná pre detekciu plôch.
            cameraGo.GetComponent<ARCameraManager>().requestedFacingDirection = CameraFacingDirection.World;

            var poseDriver = cameraGo.GetComponent<InputTrackedPoseDriver>();
            var positionAction = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotationAction = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            poseDriver.positionInput = new InputActionProperty(positionAction);
            poseDriver.rotationInput = new InputActionProperty(rotationAction);

            var origin = originGo.GetComponent<XROrigin>();
            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = camera;

            var planeManager = originGo.AddComponent<ARPlaneManager>();
            planeManager.planePrefab = planePrefab;
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

            originGo.AddComponent<ARRaycastManager>();
            originGo.AddComponent<ARAnchorManager>();

            var classifier = originGo.AddComponent<RoomPlaneClassifier>();
            SetRef(classifier, "planeManager", planeManager);
            return origin;
        }

        static void CreateEventSystem()
        {
            // UI so vstupom z nového Input Systemu (nie StandaloneInputModule).
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // ================================================================== UI

        static RectTransform CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvasGo.GetComponent<RectTransform>();
        }

        static void CreateStatusPanel(RectTransform canvas, ARPlaneManager planeManager, RoomPlaneClassifier classifier)
        {
            // Panel hore – stav skenovania.
            var panel = CreateUIObject("ScanStatusPanel", canvas, typeof(Image));
            SetAnchors(panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(-80f, 170f));
            StyleImage(panel.GetComponent<Image>(), new Color(0f, 0f, 0f, 0.55f));

            var text = CreateText("ScanStatusText", panel, "Pomaly prejdi mobilom po izbe...", 40f, Color.white);
            Stretch(text.rectTransform, new Vector2(30f, 16f), new Vector2(-30f, -16f));

            var status = text.gameObject.AddComponent<RoomScanStatus>();
            SetRef(status, "planeManager", planeManager);
            SetRef(status, "classifier", classifier);
            SetRef(status, "statusText", text);
        }

        static TMP_Text CreateDesignerPanel(RectTransform canvas, RoomDesignerController controller,
            PlaneVisibilityToggle planeToggle, PlaceableCatalog catalog)
        {
            // Panel dole: nápoveda, akčné tlačidlá a vodorovne posúvateľné menu objektov.
            var panel = CreateUIObject("DesignerPanel", canvas, typeof(Image));
            SetAnchors(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 470f));
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var hint = CreateText("HintText", panel, "Vyber objekt z menu dole", 34f, Color.white);
            SetAnchors(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-48f, 66f));

            // Akčné tlačidlá
            var actions = CreateUIObject("ActionRow", panel, typeof(HorizontalLayoutGroup));
            SetAnchors(actions, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(-32f, 104f));
            var actionLayout = actions.GetComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 12f;
            actionLayout.childControlWidth = actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = actionLayout.childForceExpandHeight = true;

            AddActionButton(actions, "Zmazať", DesignerActionButton.ActionType.DeleteSelected, controller, planeToggle);
            AddActionButton(actions, "Skryť plochy", DesignerActionButton.ActionType.TogglePlanes, controller, planeToggle);
            AddActionButton(actions, "Uložiť", DesignerActionButton.ActionType.Save, controller, planeToggle);
            AddActionButton(actions, "Načítať", DesignerActionButton.ActionType.Load, controller, planeToggle);
            AddActionButton(actions, "Vymazať všetko", DesignerActionButton.ActionType.ClearAll, controller, planeToggle);

            // Menu objektov (posúvateľné do strán)
            var scroll = CreateUIObject("CatalogScroll", panel, typeof(Image), typeof(ScrollRect));
            SetAnchors(scroll, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(0f, 214f));
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // neviditeľné, ale chytá ťahanie

            var viewport = CreateUIObject("Viewport", scroll, typeof(RectMask2D));
            Stretch(viewport, Vector2.zero, Vector2.zero);

            var content = CreateUIObject("Content", viewport, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.GetComponent<HorizontalLayoutGroup>();
            contentLayout.padding = new RectOffset(24, 24, 6, 6);
            contentLayout.spacing = 16f;
            contentLayout.childAlignment = TextAnchor.MiddleLeft;
            contentLayout.childControlWidth = contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = contentLayout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            for (int i = 0; i < catalog.Count; i++)
            {
                var entry = catalog.Get(i);
                var button = CreateButton(content, $"Item_{entry.id}", entry.displayName, 32f, out var label);
                var layout = button.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 230f;
                layout.preferredHeight = 196f;

                var catalogButton = button.gameObject.AddComponent<CatalogButton>();
                catalogButton.Configure(controller, i, label, button.GetComponent<Image>());
            }

            return hint;
        }

        static void AddActionButton(RectTransform parent, string text, DesignerActionButton.ActionType action,
            RoomDesignerController controller, PlaneVisibilityToggle planeToggle)
        {
            var button = CreateButton(parent, $"Action_{action}", text, 27f, out var label);
            var actionButton = button.gameObject.AddComponent<DesignerActionButton>();
            actionButton.Configure(action, controller, planeToggle, label);
        }

        static Button CreateButton(RectTransform parent, string name, string text, float fontSize, out TextMeshProUGUI label)
        {
            var rect = CreateUIObject(name, parent, typeof(Image), typeof(Button));
            var image = rect.GetComponent<Image>();
            StyleImage(image, ButtonColor);

            var button = rect.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
            button.colors = colors;

            label = CreateText("Label", rect, text, fontSize, ButtonTextColor);
            Stretch(label.rectTransform, new Vector2(8f, 6f), new Vector2(-8f, -6f));
            return button;
        }

        static TextMeshProUGUI CreateText(string name, RectTransform parent, string text, float fontSize, Color color)
        {
            var rect = CreateUIObject(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        static RectTransform CreateUIObject(string name, Transform parent, params System.Type[] components)
        {
            var types = new List<System.Type> { typeof(RectTransform) };
            types.AddRange(components);
            var go = new GameObject(name, types.ToArray());
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void StyleImage(Image image, Color color)
        {
            image.color = color;
            // Zaoblený sprite zo štandardného UI Unity (ak je k dispozícii).
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
        }

        static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        // ================================================================== Pomocné

        static void AddSceneToBuildSettingsFirst(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes.Where(s => s.path != scenePath));
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void SetRef(Object target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[Room Designer] Pole '{propertyName}' na {target.GetType().Name} sa nenašlo.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
