using UnityEngine;

namespace RoomDesigner
{
    /// <summary>
    /// Výhľad z virtuálneho okna s paralaxou: vidno len výrez textúry, ktorý sa posúva podľa polohy kamery,
    /// takže výhľad pôsobí vzdialene za stenou.
    /// </summary>
    public class WindowView : TextureApplier
    {
        [SerializeField] Renderer viewRenderer;
        [Tooltip("Akú časť obrázka vidno naraz (zvyšok je rezerva na posun).")]
        [SerializeField, Range(0.5f, 1f)] float visiblePart = 0.75f;
        [SerializeField] float strength = 0.35f;

        MaterialPropertyBlock m_Block;
        Camera m_Camera;
        Vector2 m_Scale;

        public void Configure(Renderer view) => viewRenderer = view;

        public override void Apply(Texture2D texture)
        {
            if (texture == null)
                return;

            // Orez, aby sa obrázok s iným pomerom strán nedeformoval.
            var size = viewRenderer.transform.localScale;
            float quadAspect = size.x / size.y;
            float textureAspect = (float)texture.width / texture.height;
            m_Scale = textureAspect > quadAspect
                ? new Vector2(quadAspect / textureAspect, 1f)
                : new Vector2(1f, textureAspect / quadAspect);
            m_Scale *= visiblePart;

            m_Block = new MaterialPropertyBlock();
            m_Block.SetTexture(BaseMapId, texture);
            UpdateOffset(Vector2.zero);
        }

        void LateUpdate()
        {
            if (m_Block == null)
                return;
            if (m_Camera == null && (m_Camera = Camera.main) == null)
                return;

            // Poloha kamery voči oknu (+Z = do izby).
            var local = transform.InverseTransformPoint(m_Camera.transform.position);
            local.z = Mathf.Max(local.z, 0.05f);
            UpdateOffset(new Vector2(local.x / local.z, local.y / local.z) * strength);
        }

        void UpdateOffset(Vector2 shift)
        {
            var margin = (Vector2.one - m_Scale) * 0.5f;
            // Quad je otočený o 180°, takže U rastie smerom k -X okna.
            // Pri pohľade z jednej strany vidno cez okno viac z opačnej strany krajiny.
            float u = margin.x + Mathf.Clamp(shift.x, -1f, 1f) * margin.x;
            float v = margin.y - Mathf.Clamp(shift.y, -1f, 1f) * margin.y;
            m_Block.SetVector(BaseMapStId, new Vector4(m_Scale.x, m_Scale.y, u, v));
            viewRenderer.SetPropertyBlock(m_Block);
        }
    }
}
