using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
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
    /// Menu Tools → Room Designer → Vytvoriť / aktualizovať scénu.
    /// Vytvorí (alebo prepíše) scénu RoomDetection so všetkým: AR, detekcia plôch, menu s nábytkom,
    /// obrazmi a oknami, gestá, uloženie fotky a galéria. Assety pripraví RoomDesignerAssetBuilder.
    /// </summary>
    public static class RoomDetectionSceneSetup
    {
        const string ScenesFolder = RoomDesignerAssetBuilder.Root + "/Scenes";
        const string ScenePath = ScenesFolder + "/RoomDetection.unity";

        // Teplá paleta: krémová, hnedá, terakota.
        static readonly Color PanelColor = new Color(0.98f, 0.96f, 0.93f, 0.97f);
        static readonly Color TrackColor = new Color(0.91f, 0.87f, 0.82f);
        static readonly Color TextColor = new Color(0.24f, 0.21f, 0.19f);
        static readonly Color MutedTextColor = new Color(0.45f, 0.40f, 0.36f);
        static readonly Color DangerColor = new Color(0.72f, 0.23f, 0.22f);
        static readonly Color ShadowColor = new Color(0.15f, 0.1f, 0.07f, 0.35f);

        [MenuItem("Tools/Room Designer/Vytvoriť \u2215 aktualizovať scénu")]
        public static void CreateScene()
        {
            // Nech sa nestratia neuložené zmeny v práve otvorenej scéne.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                RoomDesignerAssetBuilder.EnsureFolder(ScenesFolder);
                RoomDesignerAssetBuilder.BuildAll();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Assety načítavame až po NewScene – tá uvoľní z pamäte tie, na ktoré nič neodkazuje.
                var planePrefab = Load<GameObject>(RoomDesignerAssetBuilder.PlanePrefabPath);

                CreateLight();
                CreateARSession();
                var origin = CreateXROrigin(planePrefab);
                var planeManager = origin.GetComponent<ARPlaneManager>();
                var raycastManager = origin.GetComponent<ARRaycastManager>();
                var anchorManager = origin.GetComponent<ARAnchorManager>();
                var classifier = origin.GetComponent<RoomPlaneClassifier>();

                CreateEventSystem();

                var designerGo = new GameObject("Room Designer");
                var placement = designerGo.AddComponent<PlacementManager>();
                var planeToggle = designerGo.AddComponent<PlaneVisibilityToggle>();
                var screenshot = designerGo.AddComponent<SaveDesignScreenshot>();
                var gallery = designerGo.AddComponent<DesignGallery>();
                var menu = designerGo.AddComponent<DesignerMenu>();

                var canvas = CreateCanvas();
                CreateStatusPanel(canvas, planeManager, classifier);
                var hint = CreateBottomBar(canvas, menu);
                CreateSideButtons(canvas, placement, menu, screenshot, gallery);
                CreateGallery(canvas, gallery); // posledná, aby prekryla ostatné UI

                SetRef(planeToggle, "planeManager", planeManager);

                SetRef(placement, "raycastManager", raycastManager);
                SetRef(placement, "planeManager", planeManager);
                SetRef(placement, "anchorManager", anchorManager);
                SetRef(placement, "arCamera", origin.Camera);
                SetRef(placement, "hintText", hint);

                SetRef(screenshot, "uiCanvas", canvas.GetComponent<Canvas>());
                SetRef(screenshot, "planeToggle", planeToggle);
                SetRef(screenshot, "placement", placement);

                SetRef(menu, "placement", placement);
                SetRef(menu, "planeToggle", planeToggle);
                SetRef(menu, "furniture", Load<PlaceableCatalog>(RoomDesignerAssetBuilder.CatalogPath));
                SetRef(menu, "paintings", Load<PaintingLibrary>(RoomDesignerAssetBuilder.PaintingLibraryPath));
                SetRef(menu, "paintingPrefab", Load<PlaceableObject>(RoomDesignerAssetBuilder.PaintingPrefabPath));
                SetRef(menu, "windowViews", Load<WindowViewLibrary>(RoomDesignerAssetBuilder.WindowLibraryPath));
                SetRef(menu, "windowPrefab", Load<PlaceableObject>(RoomDesignerAssetBuilder.WindowPrefabPath));

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
            var panel = CreatePanel("ScanStatusPanel", canvas, 48f);
            SetAnchors(panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(-80f, 160f));
            CreateShadow(panel);

            var text = CreateText("ScanStatusText", panel, "Pomaly prejdi mobilom po izbe...", 38f, TextColor);
            Stretch(text.rectTransform, new Vector2(30f, 16f), new Vector2(-30f, -16f));

            var status = text.gameObject.AddComponent<RoomScanStatus>();
            SetRef(status, "planeManager", planeManager);
            SetRef(status, "classifier", classifier);
            SetRef(status, "statusText", text);
        }

        /// <summary>Panel dole: nápoveda, záložky a vodorovne posúvateľný zoznam položiek.</summary>
        static TMP_Text CreateBottomBar(RectTransform canvas, DesignerMenu menu)
        {
            // Spodné zaoblené rohy sú schované pod okrajom obrazovky.
            var panel = CreatePanel("BottomBar", canvas, 56f);
            SetAnchors(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -80f), new Vector2(0f, 590f));
            CreateShadow(panel);

            var hint = CreateText("HintText", panel, "Vyber objekt z menu dole", 34f, MutedTextColor);
            SetAnchors(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(-72f, 66f));

            var row = CreateUIObject("Tabs", panel, typeof(Image), typeof(HorizontalLayoutGroup));
            SetAnchors(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(-64f, 104f));
            StyleImage(row.GetComponent<Image>(), TrackColor, 52f);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(8, 8, 8, 8);
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = true;

            // Poradie zodpovedá PlacementCategory.
            SetRefs(menu, "tabs",
                CreateButton(row, "Tab_Nabytok", "Nábytok", 36f, out _, 44f),
                CreateButton(row, "Tab_Obrazy", "Obrazy", 36f, out _, 44f),
                CreateButton(row, "Tab_Okna", "Okná", 36f, out _, 44f));

            var scroll = CreateScroll("Items", panel, out var content);
            var scrollTransform = (RectTransform)scroll.transform;
            SetAnchors(scrollTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(0f, 246f));
            scroll.horizontal = true;
            scroll.vertical = false;

            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 8, 8);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Vzor položky: náhľad hore, názov dole.
            var item = CreateButton(content, "ItemTemplate", "Položka", 30f, out var label, 36f);
            var element = item.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 230f;
            element.preferredHeight = 226f;
            label.rectTransform.anchorMax = new Vector2(1f, 0.3f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 30f;

            var thumbnailArea = CreateUIObject("ThumbnailArea", item.transform);
            thumbnailArea.anchorMin = new Vector2(0f, 0.3f);
            thumbnailArea.anchorMax = Vector2.one;
            thumbnailArea.offsetMin = new Vector2(20f, 0f);
            thumbnailArea.offsetMax = new Vector2(-20f, -18f);
            CreateFittedImage("Thumbnail", thumbnailArea);
            item.gameObject.SetActive(false);

            SetRef(menu, "scroll", scroll);
            SetRef(menu, "itemTemplate", item);
            return hint;
        }

        /// <summary>Tlačidlá vpravo hore a Zmazať nad spodnou lištou (len pri vybranom objekte).</summary>
        static void CreateSideButtons(RectTransform canvas, PlacementManager placement, DesignerMenu menu,
            SaveDesignScreenshot screenshot, DesignGallery gallery)
        {
            var toolbar = CreatePanel("Toolbar", canvas, 48f);
            SetAnchors(toolbar, Vector2.one, Vector2.one, Vector2.one, new Vector2(-40f, -290f), new Vector2(184f, 4f * 148f + 24f));
            CreateShadow(toolbar);

            float y = -12f;
            AddSideButton("Uložiť", "Save", screenshot.Save, TextColor);
            AddSideButton("Galéria", "Gallery", gallery.Open, TextColor);
            var planesLabel = AddSideButton("Skryť plochy", "Planes", menu.TogglePlanes, TextColor);
            AddSideButton("Vymazať všetko", "Clear", placement.RequestClearAll, DangerColor);

            var delete = CreateButton(canvas, "Button_Zmazať", "Zmazať", 36f, out _, 55f, "Clear", DangerColor);
            SetAnchors((RectTransform)delete.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 546f), new Vector2(320f, 110f));
            AddClick(delete, placement.DeleteSelected);

            SetRef(menu, "planesLabel", planesLabel);
            SetRef(menu, "deleteButton", delete.gameObject);

            TMP_Text AddSideButton(string text, string icon, UnityAction action, Color color)
            {
                // Ikona hore, popis pod ňou; pozadie je vidno len pri stlačení.
                var button = CreateButton(toolbar, "Button_" + text, text, 24f, out var label, 36f);
                SetAnchors((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(164f, 140f));
                var colors = button.colors;
                colors.normalColor = colors.highlightedColor = colors.selectedColor = new Color(1f, 1f, 1f, 0f);
                colors.pressedColor = TrackColor;
                button.colors = colors;

                label.color = color;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 24f;
                label.rectTransform.anchorMax = new Vector2(1f, 0.46f);

                var iconRect = CreateIcon(button.transform, icon, color);
                SetAnchors(iconRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(60f, 60f));

                AddClick(button, action);
                y -= 148f;
                return label;
            }
        }

        static void CreateGallery(RectTransform canvas, DesignGallery gallery)
        {
            var panel = CreateUIObject("GalleryPanel", canvas, typeof(Image));
            Stretch(panel, Vector2.zero, Vector2.zero);
            panel.GetComponent<Image>().color = new Color(0.96f, 0.93f, 0.89f, 1f);

            var title = CreateText("Title", panel, "Uložené návrhy", 52f, TextColor);
            SetAnchors(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(-48f, 100f));

            var empty = CreateText("EmptyText", panel, "Zatiaľ tu nie sú žiadne fotky", 38f, MutedTextColor);
            Stretch(empty.rectTransform, Vector2.zero, Vector2.zero);

            var scroll = CreateScroll("Scroll", panel, out var content);
            Stretch((RectTransform)scroll.transform, new Vector2(0f, 200f), new Vector2(0f, -220f));
            scroll.horizontal = false;
            scroll.vertical = true;

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(280f, 560f);
            layout.spacing = new Vector2(24f, 24f);
            layout.padding = new RectOffset(32, 32, 16, 16);
            layout.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var thumbnail = CreateButton(content, "ThumbnailTemplate", "", 10f, out var thumbnailLabel, 24f);
            Object.DestroyImmediate(thumbnailLabel.gameObject);
            var thumbnailImage = CreateUIObject("Image", thumbnail.transform, typeof(RawImage));
            Stretch(thumbnailImage, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            thumbnailImage.GetComponent<RawImage>().raycastTarget = false;
            thumbnail.gameObject.SetActive(false);

            var back = CreateButton(panel, "Button_Späť", "Späť", 38f, out _, 55f, "Back");
            SetAnchors((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(400f, 110f));
            AddClick(back, gallery.Close);

            // Celoobrazovkový náhľad
            var full = CreateUIObject("FullView", panel, typeof(Image));
            Stretch(full, Vector2.zero, Vector2.zero);
            full.GetComponent<Image>().color = new Color(0.13f, 0.11f, 0.1f);
            var fullArea = CreateUIObject("ImageArea", full);
            Stretch(fullArea, new Vector2(0f, 200f), new Vector2(0f, -80f));
            var fullImage = CreateFittedImage("Photo", fullArea);

            var fullBack = CreateButton(full, "Button_Späť", "Späť", 38f, out _, 55f, "Back");
            SetAnchors((RectTransform)fullBack.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 60f), new Vector2(380f, 110f));
            AddClick(fullBack, gallery.CloseFull);

            var delete = CreateButton(full, "Button_ZmazaťFotku", "Zmazať fotku", 36f, out var deleteLabel, 55f, "Clear", DangerColor);
            SetAnchors((RectTransform)delete.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(16f, 60f), new Vector2(380f, 110f));
            AddClick(delete, gallery.DeleteCurrent);

            full.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            SetRef(gallery, "panel", panel.gameObject);
            SetRef(gallery, "grid", content);
            SetRef(gallery, "thumbnailTemplate", thumbnail);
            SetRef(gallery, "emptyText", empty.gameObject);
            SetRef(gallery, "fullView", full.gameObject);
            SetRef(gallery, "fullImage", fullImage);
            SetRef(gallery, "fullFitter", fullImage.GetComponent<AspectRatioFitter>());
            SetRef(gallery, "deleteLabel", deleteLabel);
        }

        static ScrollRect CreateScroll(string name, RectTransform parent, out RectTransform content)
        {
            var root = CreateUIObject(name, parent, typeof(Image), typeof(ScrollRect));
            root.GetComponent<Image>().color = Color.clear; // neviditeľné, ale chytá ťahanie

            var viewport = CreateUIObject("Viewport", root, typeof(RectMask2D));
            Stretch(viewport, Vector2.zero, Vector2.zero);
            content = CreateUIObject("Content", viewport);

            var scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }

        /// <summary>Obrázok, ktorý sa zmestí do rodiča a zachová pomer strán.</summary>
        static RawImage CreateFittedImage(string name, RectTransform parent)
        {
            var rect = CreateUIObject(name, parent, typeof(RawImage), typeof(AspectRatioFitter));
            rect.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var image = rect.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        static void AddClick(Button button, UnityAction action) =>
            UnityEventTools.AddPersistentListener(button.onClick, action);

        /// <summary>Zaoblené tlačidlo; s ikonou (vľavo od textu), ak je zadaná. Farebné má biely text.</summary>
        static Button CreateButton(RectTransform parent, string name, string text, float fontSize, out TextMeshProUGUI label,
            float radius, string icon = null, Color? background = null)
        {
            var rect = CreateUIObject(name, parent, typeof(Image), typeof(Button), typeof(PressEffect));
            var image = rect.GetComponent<Image>();
            StyleImage(image, background ?? Color.white, radius);
            var foreground = background.HasValue ? Color.white : TextColor;

            var button = rect.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.88f, 0.85f, 0.82f);
            colors.selectedColor = colors.highlightedColor = Color.white;
            button.colors = colors;

            label = CreateText("Label", rect, text, fontSize, foreground);
            Stretch(label.rectTransform, new Vector2(10f, 6f), new Vector2(-10f, -6f));
            if (icon != null)
            {
                // Ikona s textom spolu v strede, aj keď sa text zmení.
                var iconRect = CreateIcon(rect, icon, foreground);
                iconRect.SetAsFirstSibling();
                var element = iconRect.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = element.preferredHeight = element.minWidth = 48f;

                var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 14f;
                layout.padding = new RectOffset(16, 16, 0, 0);
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            }
            return button;
        }

        static RectTransform CreateIcon(Transform parent, string icon, Color color)
        {
            var rect = CreateUIObject("Icon", parent, typeof(Image));
            var image = rect.GetComponent<Image>();
            image.sprite = UiSprites.Load(icon);
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static RectTransform CreatePanel(string name, RectTransform parent, float radius)
        {
            var rect = CreateUIObject(name, parent, typeof(Image));
            StyleImage(rect.GetComponent<Image>(), PanelColor, radius);
            return rect;
        }

        /// <summary>Mäkký tieň za panelom (volať až po nastavení polohy panela).</summary>
        static void CreateShadow(RectTransform panel)
        {
            var rect = CreateUIObject(panel.name + "Shadow", panel.parent, typeof(Image));
            rect.SetSiblingIndex(panel.GetSiblingIndex());
            SetAnchors(rect, panel.anchorMin, panel.anchorMax, panel.pivot, panel.anchoredPosition, panel.sizeDelta);
            rect.offsetMin += new Vector2(-28f, -38f);
            rect.offsetMax += new Vector2(28f, 18f);

            var image = rect.GetComponent<Image>();
            image.sprite = UiSprites.Load("Shadow");
            image.type = Image.Type.Sliced;
            image.color = ShadowColor;
            image.raycastTarget = false;
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

        static void StyleImage(Image image, Color color, float radius)
        {
            image.color = color;
            image.sprite = UiSprites.Load("Rounded");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = UiSprites.CornerRadius / radius;
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

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException("Nepodarilo sa načítať " + path);
            return asset;
        }

        static void SetRefs(Object target, string propertyName, params Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
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
