using System.Collections.Generic;
using System.Linq;
using RoomDesigner;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using InputTrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace RoomDesigner.EditorTools
{
    /// <summary>
    /// Menu Tools → Room Designer → Vytvoriť scénu detekcie.
    /// Vytvorí (alebo prepíše) scénu RoomDetection, prefab plochy, materiály a pridá scénu do Build Settings.
    /// </summary>
    public static class RoomDetectionSceneSetup
    {
        const string Root = "Assets/_RoomDesigner";
        const string ScenesFolder = Root + "/Scenes";
        const string PrefabsFolder = Root + "/Prefabs";
        const string MaterialsFolder = Root + "/Materials";

        const string ScenePath = ScenesFolder + "/RoomDetection.unity";
        const string PlanePrefabPath = PrefabsFolder + "/RoomPlane.prefab";
        const string CubePrefabPath = PrefabsFolder + "/TestCube.prefab";
        const string PlaneMaterialPath = MaterialsFolder + "/RoomPlane.mat";
        const string CubeMaterialPath = MaterialsFolder + "/TestCube.mat";

        [MenuItem("Tools/Room Designer/Vytvoriť scénu detekcie")]
        public static void CreateScene()
        {
            // Nech sa nestratia neuložené zmeny v práve otvorenej scéne.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                EnsureFolder(ScenesFolder);
                EnsureFolder(PrefabsFolder);
                EnsureFolder(MaterialsFolder);

                var planeMaterial = CreateOrUpdatePlaneMaterial();
                var cubeMaterial = CreateOrUpdateCubeMaterial();
                var planePrefab = CreatePlanePrefab(planeMaterial);
                var cubePrefab = CreateCubePrefab(cubeMaterial);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                CreateLight();
                CreateARSession();
                var origin = CreateXROrigin(planePrefab);
                var planeManager = origin.GetComponent<ARPlaneManager>();
                var raycastManager = origin.GetComponent<ARRaycastManager>();

                CreateEventSystem();
                CreateStatusUI(planeManager);
                CreateRoomDesignerObject(planeManager, raycastManager, cubePrefab);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    Debug.LogError($"[Room Designer] Scénu sa nepodarilo uložiť: {ScenePath}");
                    return;
                }

                AddSceneToBuildSettingsFirst(ScenePath);
                AssetDatabase.SaveAssets();

                Selection.activeGameObject = origin.gameObject;
                Debug.Log($"[Room Designer] Scéna vytvorená: {ScenePath} (prvá v Build Settings).");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Room Designer", "Vytvorenie scény zlyhalo:\n" + e.Message, "OK");
            }
        }

        // ---------------------------------------------------------------- Materiály

        static Material CreateOrUpdatePlaneMaterial()
        {
            var shader = FindShader("Universal Render Pipeline/Unlit");
            var material = LoadOrCreateMaterial(PlaneMaterialPath, shader);
            material.shader = shader;
            PlaneColorByAlignment.MakeTransparent(material);
            SetColor(material, new Color(0.6f, 0.6f, 0.6f, 0.3f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateOrUpdateCubeMaterial()
        {
            var shader = FindShader("Universal Render Pipeline/Lit");
            var material = LoadOrCreateMaterial(CubeMaterialPath, shader);
            material.shader = shader;
            SetColor(material, new Color(1f, 0.55f, 0.1f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
                throw new System.InvalidOperationException(
                    $"Shader '{name}' sa nenašiel. Je v projekte nainštalovaný URP (com.unity.render-pipelines.universal)?");
            return shader;
        }

        static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material; // prepíšeme vlastnosti, GUID ostane rovnaké

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        // ---------------------------------------------------------------- Prefaby

        static GameObject CreatePlanePrefab(Material planeMaterial)
        {
            var go = new GameObject("RoomPlane");
            try
            {
                go.AddComponent<MeshFilter>();

                var meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = planeMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = planeMaterial;
                line.useWorldSpace = false;
                line.loop = true;
                line.widthMultiplier = 0.005f;
                line.numCornerVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.positionCount = 0;

                go.AddComponent<ARPlane>();
                go.AddComponent<ARPlaneMeshVisualizer>();

                var colorizer = go.AddComponent<PlaneColorByAlignment>();
                SetSerializedReference(colorizer, "baseMaterial", planeMaterial);

                // SaveAsPrefabAsset prepíše existujúci prefab a zachová jeho GUID.
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlanePrefabPath, out var success);
                if (!success)
                    throw new System.InvalidOperationException("Nepodarilo sa uložiť " + PlanePrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static GameObject CreateCubePrefab(Material cubeMaterial)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.name = "TestCube";
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<MeshRenderer>().sharedMaterial = cubeMaterial;

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, CubePrefabPath, out var success);
                if (!success)
                    throw new System.InvalidOperationException("Nepodarilo sa uložiť " + CubePrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- Scéna

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
            var cameraManager = cameraGo.GetComponent<ARCameraManager>();
            cameraManager.requestedFacingDirection = CameraFacingDirection.World;

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
            return origin;
        }

        static void CreateEventSystem()
        {
            // UI so vstupom z nového Input Systemu (nie StandaloneInputModule).
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        static void CreateStatusUI(ARPlaneManager planeManager)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Panel hore – polopriehľadné pozadie, aby bol text čitateľný na kamere.
            var panelGo = new GameObject("ScanStatusPanel", typeof(RectTransform), typeof(Image));
            panelGo.layer = canvasGo.layer;
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -100f);
            panelRect.sizeDelta = new Vector2(-80f, 220f);
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var textGo = new GameObject("ScanStatusText", typeof(RectTransform));
            textGo.layer = canvasGo.layer;
            textGo.transform.SetParent(panelGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(30f, 20f);
            textRect.offsetMax = new Vector2(-30f, -20f);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "Pomaly prejdi mobilom po izbe...";
            text.fontSize = 46f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            var status = textGo.AddComponent<RoomScanStatus>();
            SetSerializedReference(status, "planeManager", planeManager);
            SetSerializedReference(status, "statusText", text);
        }

        static void CreateRoomDesignerObject(ARPlaneManager planeManager, ARRaycastManager raycastManager, GameObject cubePrefab)
        {
            var go = new GameObject("Room Designer");

            var tap = go.AddComponent<TapToPlaceTest>();
            SetSerializedReference(tap, "raycastManager", raycastManager);
            SetSerializedReference(tap, "cubePrefab", cubePrefab);

            var visibility = go.AddComponent<PlaneVisibilityToggle>();
            SetSerializedReference(visibility, "planeManager", planeManager);
        }

        // ---------------------------------------------------------------- Pomocné

        static void AddSceneToBuildSettingsFirst(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes.Where(s => s.path != scenePath));
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void SetSerializedReference(Object target, string propertyName, Object value)
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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
