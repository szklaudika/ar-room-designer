using UnityEngine;

namespace RoomDesigner
{
    /// <summary>
    /// Obraz na stenu: rám (kocka) a plátno (quad). Šírka sa prispôsobí pomeru strán obrázka.
    /// Pivot je v strede zadnej strany, predok smeruje na +Z.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class WallPainting : TextureApplier
    {
        [SerializeField] Transform frame;
        [SerializeField] Renderer canvasRenderer;
        [Tooltip("Výška plátna v metroch.")]
        [SerializeField] float height = 0.5f;
        [SerializeField] float border = 0.025f;
        [SerializeField] float depth = 0.03f;

        public void Configure(Transform frameTransform, Renderer canvas)
        {
            frame = frameTransform;
            canvasRenderer = canvas;
            Resize(1f);
        }

        public override void Apply(Texture2D texture)
        {
            if (texture == null)
                return;

            Resize((float)texture.width / texture.height);

            var block = new MaterialPropertyBlock();
            block.SetTexture(BaseMapId, texture);
            canvasRenderer.SetPropertyBlock(block);
        }

        void Resize(float aspect)
        {
            float width = height * aspect;
            var outer = new Vector3(width + 2f * border, height + 2f * border, depth);

            frame.localPosition = new Vector3(0f, 0f, depth * 0.5f);
            frame.localScale = outer;
            canvasRenderer.transform.localPosition = new Vector3(0f, 0f, depth + 0.002f);
            canvasRenderer.transform.localScale = new Vector3(width, height, 1f);

            var box = GetComponent<BoxCollider>();
            box.center = new Vector3(0f, 0f, depth * 0.5f);
            box.size = outer;
        }
    }
}
