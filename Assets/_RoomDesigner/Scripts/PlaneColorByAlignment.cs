using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RoomDesigner
{
    /// <summary>
    /// Zafarbí AR plochu podľa jej orientácie:
    /// podlaha (HorizontalUp) = zelená, stena (Vertical) = modrá, strop a ostatné = sivá.
    /// Patrí na prefab, ktorý používa ARPlaneManager.
    /// </summary>
    [RequireComponent(typeof(ARPlane))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PlaneColorByAlignment : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Farby (alfa = priehľadnosť)")]
        [SerializeField] Color floorColor = new Color(0.2f, 0.85f, 0.3f, 0.35f);
        [SerializeField] Color wallColor = new Color(0.2f, 0.5f, 1f, 0.35f);
        [SerializeField] Color otherColor = new Color(0.6f, 0.6f, 0.6f, 0.3f);

        [Tooltip("Priehľadnosť okraja (LineRenderer) – okraj je výraznejší ako plocha.")]
        [SerializeField, Range(0f, 1f)] float outlineAlpha = 0.9f;

        [Tooltip("Ak nie je priradený, vytvorí sa v kóde URP Unlit transparentný materiál.")]
        [SerializeField] Material baseMaterial;

        ARPlane m_Plane;
        MeshRenderer m_MeshRenderer;
        LineRenderer m_LineRenderer;
        Material m_FillMaterial;
        Material m_LineMaterial;
        PlaneAlignment? m_AppliedAlignment;

        void Awake()
        {
            m_Plane = GetComponent<ARPlane>();
            m_MeshRenderer = GetComponent<MeshRenderer>();
            m_LineRenderer = GetComponent<LineRenderer>();

            var source = baseMaterial != null ? baseMaterial : m_MeshRenderer.sharedMaterial;
            if (source == null)
                source = CreateTransparentUnlitMaterial();

            // Vlastné inštancie, aby každá plocha mohla mať inú farbu.
            m_FillMaterial = new Material(source) { name = "RoomPlane (Fill)" };
            m_MeshRenderer.sharedMaterial = m_FillMaterial;

            if (m_LineRenderer != null)
            {
                m_LineMaterial = new Material(source) { name = "RoomPlane (Outline)" };
                m_LineRenderer.sharedMaterial = m_LineMaterial;
            }
        }

        void OnEnable() => ApplyColor(force: true);

        void Update()
        {
            // Orientácia sa môže zmeniť pri prvých aktualizáciách plochy – kontrola je lacná.
            if (m_AppliedAlignment != m_Plane.alignment)
                ApplyColor(force: false);
        }

        void OnDestroy()
        {
            if (m_FillMaterial != null) Destroy(m_FillMaterial);
            if (m_LineMaterial != null) Destroy(m_LineMaterial);
        }

        public Color GetColorFor(PlaneAlignment alignment)
        {
            switch (alignment)
            {
                case PlaneAlignment.HorizontalUp: return floorColor;
                case PlaneAlignment.Vertical: return wallColor;
                default: return otherColor; // HorizontalDown (strop), NotAxisAligned, None
            }
        }

        void ApplyColor(bool force)
        {
            if (m_Plane == null || m_FillMaterial == null)
                return;

            var alignment = m_Plane.alignment;
            if (!force && m_AppliedAlignment == alignment)
                return;

            m_AppliedAlignment = alignment;
            var color = GetColorFor(alignment);
            SetMaterialColor(m_FillMaterial, color);

            if (m_LineMaterial != null)
            {
                var lineColor = color;
                lineColor.a = outlineAlpha;
                SetMaterialColor(m_LineMaterial, lineColor);
                m_LineRenderer.startColor = lineColor;
                m_LineRenderer.endColor = lineColor;
            }
        }

        static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        /// <summary>
        /// Vytvorí URP Unlit materiál nastavený na priehľadnosť (alpha blending).
        /// Pozor: v builde musí byť shader zahrnutý – preto Editor skript vytvára materiál ako asset.
        /// </summary>
        public static Material CreateTransparentUnlitMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[PlaneColorByAlignment] Shader 'Universal Render Pipeline/Unlit' sa nenašiel, použijem Sprites/Default.");
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader) { name = "RoomPlane (Runtime)" };
            MakeTransparent(material);
            return material;
        }

        /// <summary>Nastaví URP Unlit/Lit materiál na Transparent + Alpha blend.</summary>
        public static void MakeTransparent(Material material)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f); // 1 = Transparent
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);     // 0 = Alpha
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off); // viditeľné z oboch strán

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
