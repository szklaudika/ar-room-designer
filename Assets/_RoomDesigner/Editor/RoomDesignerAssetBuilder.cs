using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using Object = UnityEngine.Object;

namespace RoomDesigner.EditorTools
{
    /// <summary>
    /// Vytvorí (alebo prepíše) assety pre Room Designer: materiály, prefab AR plochy,
    /// zástupné modely nábytku, obrazov a okna a katalóg pre menu.
    /// Modely sú poskladané z primitív v reálnej veľkosti – neskôr sa dajú nahradiť skutočnými 3D modelmi
    /// (stačí v katalógu vymeniť prefab; prefab musí mať PlaceableObject + BoxCollider).
    /// Vlastné položky v katalógu a knižniciach sa pri opakovanom spustení zachovajú.
    /// </summary>
    public static class RoomDesignerAssetBuilder
    {
        public const string Root = "Assets/_RoomDesigner";
        public const string MaterialsFolder = Root + "/Materials";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string PlaceablesFolder = PrefabsFolder + "/Placeables";
        public const string DataFolder = Root + "/Data";

        public const string PlanePrefabPath = PrefabsFolder + "/RoomPlane.prefab";
        public const string CatalogPath = DataFolder + "/PlaceableCatalog.asset";

        public const string PaintingPrefabPath = PlaceablesFolder + "/Painting.prefab";
        public const string WindowPrefabPath = PlaceablesFolder + "/Window.prefab";
        public const string PaintingLibraryPath = DataFolder + "/PaintingLibrary.asset";
        public const string WindowLibraryPath = DataFolder + "/WindowViewLibrary.asset";

        class Mats
        {
            public Material plane, highlight, canvas;
            public Material wood, darkWood, fabric, fabricDark, metal, mattress, blanket, lampShade, frameWhite;
        }

        public static void BuildAll()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PlaceablesFolder);
            EnsureFolder(DataFolder);

            var m = BuildMaterials();
            BuildPlanePrefab(m.plane);
            BuildCatalog(m);
            BuildPainting(m);
            BuildWindow(m);
            BuildLibrary<PaintingLibrary>(PaintingLibraryPath, PlaceholderTextures.PaintingsFolder, PlaceholderTextures.CreatePaintings);
            BuildLibrary<WindowViewLibrary>(WindowLibraryPath, PlaceholderTextures.ViewsFolder, PlaceholderTextures.CreateViews);
            AssetDatabase.SaveAssets();
        }

        // ================================================================== Materiály

        static Mats BuildMaterials()
        {
            var lit = FindShader("Universal Render Pipeline/Lit");
            var unlit = FindShader("Universal Render Pipeline/Unlit");

            var plane = LoadOrCreate("RoomPlane", unlit);
            PlaneColorByAlignment.MakeTransparent(plane);
            SetColor(plane, new Color(0.6f, 0.6f, 0.6f, 0.3f));

            var highlight = LoadOrCreate("Highlight", unlit);
            MakeOpaque(highlight);
            SetColor(highlight, new Color(1f, 0.6f, 0.1f, 1f));

            var canvas = LoadOrCreate("Canvas", unlit); // plátno obrazu/okna – textúru nastaví WallPainting / WindowView
            MakeOpaque(canvas);
            SetColor(canvas, Color.white);

            return new Mats
            {
                plane = plane,
                highlight = highlight,
                canvas = canvas,
                wood = Lit(lit, "Wood", new Color(0.58f, 0.39f, 0.22f), 0.35f),
                darkWood = Lit(lit, "DarkWood", new Color(0.30f, 0.20f, 0.13f), 0.35f),
                fabric = Lit(lit, "Fabric", new Color(0.36f, 0.47f, 0.58f), 0.1f),
                fabricDark = Lit(lit, "FabricDark", new Color(0.24f, 0.30f, 0.38f), 0.1f),
                metal = Lit(lit, "Metal", new Color(0.22f, 0.22f, 0.24f), 0.7f, 0.8f),
                mattress = Lit(lit, "Mattress", new Color(0.95f, 0.95f, 0.92f), 0.2f),
                blanket = Lit(lit, "Blanket", new Color(0.78f, 0.52f, 0.42f), 0.1f),
                lampShade = Lit(lit, "LampShade", new Color(1f, 0.93f, 0.78f), 0.2f, 0f, new Color(0.9f, 0.75f, 0.45f)),
                frameWhite = Lit(lit, "FrameWhite", new Color(0.95f, 0.95f, 0.93f), 0.4f),
            };
        }

        static Material Lit(Shader shader, string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            var mat = LoadOrCreate(name, shader);
            SetColor(mat, color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LoadOrCreate(string name, Shader shader)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path); // nový asset; existujúci sa len upraví (GUID ostane)
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void MakeOpaque(Material mat)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Back);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Geometry;
        }

        static void SetColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
                throw new InvalidOperationException(
                    $"Shader '{name}' sa nenašiel. Je v projekte nainštalovaný URP (com.unity.render-pipelines.universal)?");
            return shader;
        }

        // ================================================================== Prefab AR plochy

        static GameObject BuildPlanePrefab(Material planeMaterial)
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
                var so = new SerializedObject(colorizer);
                so.FindProperty("baseMaterial").objectReferenceValue = planeMaterial;
                so.ApplyModifiedPropertiesWithoutUndo();

                return SavePrefab(go, PlanePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================== Katalóg + modely

        static void BuildCatalog(Mats m)
        {
            var defaults = new[]
            {
                Entry("stol", "Stôl", BuildTable(m)),
                Entry("stolicka", "Stolička", BuildChair(m)),
                Entry("gauc", "Gauč", BuildSofa(m)),
                Entry("postel", "Posteľ", BuildBed(m)),
                Entry("lampa", "Lampa", BuildLamp(m)),
            };

            var catalog = LoadOrCreateAsset<PlaceableCatalog>(CatalogPath);
            // Katalóg je len pre nábytok; prázdne riadky by v menu nič nerobili.
            catalog.Entries.RemoveAll(e => e.prefab == null || e.prefab.IsWallObject);
            foreach (var entry in defaults)
                if (!catalog.Entries.Exists(e => e.id == entry.id))
                    catalog.Entries.Add(entry);
            EditorUtility.SetDirty(catalog);
        }

        static PlaceableCatalog.Entry Entry(string id, string name, PlaceableObject prefab) =>
            new PlaceableCatalog.Entry { id = id, displayName = name, prefab = prefab };

        /// <summary>
        /// Doplní do knižnice všetky obrázky z priečinka, ktoré v nej ešte nie sú.
        /// Zástupné obrázky vytvorí len prvýkrát, takže zmazané sa nevrátia.
        /// </summary>
        static void BuildLibrary<T>(string path, string folder, Action createPlaceholders) where T : TextureLibrary
        {
            EnsureFolder(folder);
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
                createPlaceholders();

            var library = LoadOrCreateAsset<T>(path);
            library.Entries.RemoveAll(e => e.texture == null);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (texture != null && !library.Entries.Exists(e => e.texture == texture))
                    library.Entries.Add(new TextureLibrary.Entry
                    {
                        displayName = texture.name.Replace('_', ' '),
                        texture = texture
                    });
            }
            EditorUtility.SetDirty(library);
        }

        static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        // Nábytok: pivot = stred spodnej strany, predok = +Z. Rozmery v metroch.

        static PlaceableObject BuildTable(Mats m) => BuildPlaceable("Stol", "stol", PlacementCategory.Nabytok, m, model =>
        {
            Cube(model, "Top", new Vector3(0f, 0.73f, 0f), new Vector3(1.2f, 0.04f, 0.7f), m.wood);
            foreach (var (x, z) in Corners(0.54f, 0.29f))
                Cube(model, "Leg", new Vector3(x, 0.355f, z), new Vector3(0.05f, 0.71f, 0.05f), m.darkWood);
        });

        static PlaceableObject BuildChair(Mats m) => BuildPlaceable("Stolicka", "stolicka", PlacementCategory.Nabytok, m, model =>
        {
            Cube(model, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.05f, 0.45f), m.wood);
            Cube(model, "Cushion", new Vector3(0f, 0.485f, 0.01f), new Vector3(0.40f, 0.03f, 0.40f), m.fabric);
            Cube(model, "Back", new Vector3(0f, 0.72f, -0.205f), new Vector3(0.45f, 0.5f, 0.04f), m.wood);
            foreach (var (x, z) in Corners(0.195f, 0.195f))
                Cube(model, "Leg", new Vector3(x, 0.215f, z), new Vector3(0.04f, 0.43f, 0.04f), m.darkWood);
        });

        static PlaceableObject BuildSofa(Mats m) => BuildPlaceable("Gauc", "gauc", PlacementCategory.Nabytok, m, model =>
        {
            Cube(model, "Base", new Vector3(0f, 0.22f, 0f), new Vector3(2.0f, 0.24f, 0.9f), m.fabricDark);
            Cube(model, "SeatLeft", new Vector3(-0.42f, 0.42f, 0.06f), new Vector3(0.82f, 0.16f, 0.72f), m.fabric);
            Cube(model, "SeatRight", new Vector3(0.42f, 0.42f, 0.06f), new Vector3(0.82f, 0.16f, 0.72f), m.fabric);
            Cube(model, "Back", new Vector3(0f, 0.62f, -0.36f), new Vector3(1.68f, 0.56f, 0.18f), m.fabricDark);
            Cube(model, "ArmLeft", new Vector3(-0.92f, 0.47f, 0f), new Vector3(0.16f, 0.5f, 0.9f), m.fabricDark);
            Cube(model, "ArmRight", new Vector3(0.92f, 0.47f, 0f), new Vector3(0.16f, 0.5f, 0.9f), m.fabricDark);
            foreach (var (x, z) in Corners(0.9f, 0.38f))
                Cylinder(model, "Leg", new Vector3(x, 0.05f, z), new Vector3(0.05f, 0.05f, 0.05f), m.metal);
        });

        static PlaceableObject BuildBed(Mats m) => BuildPlaceable("Postel", "postel", PlacementCategory.Nabytok, m, model =>
        {
            Cube(model, "Frame", new Vector3(0f, 0.15f, 0f), new Vector3(1.5f, 0.3f, 2.1f), m.wood);
            Cube(model, "Mattress", new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.2f, 2.0f), m.mattress);
            Cube(model, "Blanket", new Vector3(0f, 0.51f, 0.25f), new Vector3(1.44f, 0.04f, 1.42f), m.blanket);
            Cube(model, "PillowLeft", new Vector3(-0.35f, 0.55f, -0.75f), new Vector3(0.55f, 0.1f, 0.35f), m.mattress);
            Cube(model, "PillowRight", new Vector3(0.35f, 0.55f, -0.75f), new Vector3(0.55f, 0.1f, 0.35f), m.mattress);
            Cube(model, "Headboard", new Vector3(0f, 0.5f, -1.03f), new Vector3(1.5f, 1.0f, 0.06f), m.darkWood);
        });

        static PlaceableObject BuildLamp(Mats m) => BuildPlaceable("Lampa", "lampa", PlacementCategory.Nabytok, m, model =>
        {
            Cylinder(model, "Base", new Vector3(0f, 0.015f, 0f), new Vector3(0.3f, 0.015f, 0.3f), m.metal);
            Cylinder(model, "Pole", new Vector3(0f, 0.78f, 0f), new Vector3(0.03f, 0.75f, 0.03f), m.metal);
            Cylinder(model, "Shade", new Vector3(0f, 1.48f, 0f), new Vector3(0.4f, 0.14f, 0.4f), m.lampShade);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(model, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.shadows = LightShadows.None;
        });

        // Objekty na stenu: pivot = stred zadnej strany (na stene), predok (+Z) smeruje do izby.

        static void BuildPainting(Mats m)
        {
            Transform frame = null;
            Renderer canvas = null;
            BuildPlaceable("Painting", "obraz", PlacementCategory.Obraz, m, model =>
            {
                frame = Cube(model, "Frame", Vector3.zero, Vector3.one, m.darkWood).transform;
                canvas = Quad(model, "Canvas", Vector3.zero, Vector2.one, m.canvas);
            }, root => root.AddComponent<WallPainting>().Configure(frame, canvas));
        }

        static void BuildWindow(Mats m)
        {
            const float w = 1.0f, h = 1.2f, border = 0.07f, depth = 0.06f;
            Renderer view = null;

            BuildPlaceable("Window", "okno", PlacementCategory.Okno, m, model =>
            {
                view = Quad(model, "View", new Vector3(0f, 0f, 0.003f), new Vector2(w - 2f * border, h - 2f * border), m.canvas);
                Frame(model, w, h, border, depth, m.frameWhite);
                Cube(model, "MullionV", new Vector3(0f, 0f, 0.03f), new Vector3(0.04f, h - 2f * border, 0.04f), m.frameWhite);
                Cube(model, "MullionH", new Vector3(0f, 0.12f, 0.03f), new Vector3(w - 2f * border, 0.04f, 0.039f), m.frameWhite);
                Cube(model, "Sill", new Vector3(0f, -h * 0.5f - 0.02f, 0.07f), new Vector3(w + 0.12f, 0.04f, 0.14f), m.frameWhite);
            }, root => root.AddComponent<WindowView>().Configure(view));
        }

        static PlaceableObject BuildPlaceable(string prefabName, string id, PlacementCategory category, Mats m,
            Action<Transform> buildModel, Action<GameObject> extra = null)
        {
            var root = new GameObject(prefabName);
            try
            {
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                buildModel(model.transform);

                // BoxCollider okolo celého modelu – na ťuknutie a obrys pri výbere.
                var bounds = ComputeBounds(root);
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;

                var placeable = root.AddComponent<PlaceableObject>();
                placeable.Configure(id, category, m.highlight);

                extra?.Invoke(root);

                return SavePrefab(root, $"{PlaceablesFolder}/{prefabName}.prefab").GetComponent<PlaceableObject>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ================================================================== Pomocné – geometria

        static IEnumerable<(float, float)> Corners(float x, float z)
        {
            yield return (-x, -z);
            yield return (x, -z);
            yield return (x, z);
            yield return (-x, z);
        }

        static void Frame(Transform parent, float w, float h, float border, float depth, Material mat)
        {
            float z = depth * 0.5f;
            Cube(parent, "FrameTop", new Vector3(0f, (h - border) * 0.5f, z), new Vector3(w, border, depth), mat);
            Cube(parent, "FrameBottom", new Vector3(0f, -(h - border) * 0.5f, z), new Vector3(w, border, depth), mat);
            Cube(parent, "FrameLeft", new Vector3(-(w - border) * 0.5f, 0f, z), new Vector3(border, h - 2f * border, depth), mat);
            Cube(parent, "FrameRight", new Vector3((w - border) * 0.5f, 0f, z), new Vector3(border, h - 2f * border, depth), mat);
        }

        static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat) =>
            Primitive(PrimitiveType.Cube, parent, name, pos, scale, mat);

        /// <summary>Valec Unity má výšku 2 (scale.y = polovica výšky) a priemer 1.</summary>
        static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat) =>
            Primitive(PrimitiveType.Cylinder, parent, name, pos, scale, mat);

        /// <summary>Quad otočený tak, aby jeho predná strana smerovala na +Z (do izby).</summary>
        static Renderer Quad(Transform parent, string name, Vector3 pos, Vector2 size, Material mat)
        {
            var go = Primitive(PrimitiveType.Quad, parent, name, pos, new Vector3(size.x, size.y, 1f), mat);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // klikateľný je len BoxCollider na koreni
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Bounds ComputeBounds(GameObject root)
        {
            // Koreň je v počiatku bez rotácie, takže svetové bounds = lokálne.
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 0.1f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // ================================================================== Pomocné – assety

        static GameObject SavePrefab(GameObject go, string path)
        {
            // SaveAsPrefabAsset prepíše existujúci prefab a zachová jeho GUID.
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out var success);
            if (!success || prefab == null)
                throw new InvalidOperationException("Nepodarilo sa uložiť " + path);
            return prefab;
        }

        public static void EnsureFolder(string path)
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
