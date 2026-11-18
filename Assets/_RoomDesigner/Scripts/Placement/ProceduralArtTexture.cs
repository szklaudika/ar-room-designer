using System.Collections.Generic;
using UnityEngine;

namespace RoomDesigner
{
    /// <summary>
    /// Vygeneruje obrázok priamo v kóde (bez obrázkových súborov) a nastaví ho na plátno obrazu alebo okna.
    /// Okno má navyše jednoduchú paralaxu: pri pohľade zboku sa výhľad posúva, takže pôsobí ako hĺbka za stenou.
    /// </summary>
    public class ProceduralArtTexture : MonoBehaviour
    {
        public enum ArtStyle { Abstract, Landscape, WindowView }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField] Renderer targetRenderer;
        [SerializeField] ArtStyle style = ArtStyle.Abstract;
        [SerializeField] int seed = 1;
        [SerializeField] Vector2Int resolution = new Vector2Int(256, 256);

        [Header("Paralaxa (okno)")]
        [SerializeField] bool parallax;
        [Tooltip("Akú časť obrázka vidno naraz (zvyšok je rezerva na posun).")]
        [SerializeField, Range(0.5f, 1f)] float parallaxView = 0.75f;
        [SerializeField] float parallaxStrength = 0.35f;

        // Rovnaký obrázok sa generuje len raz, aj keď je v izbe viac rovnakých obrazov.
        static readonly Dictionary<(ArtStyle, int, int, int), Texture2D> s_Cache = new Dictionary<(ArtStyle, int, int, int), Texture2D>();

        MaterialPropertyBlock m_Block;
        Camera m_Camera;

        public void Configure(Renderer target, ArtStyle artStyle, int artSeed, Vector2Int size, bool useParallax)
        {
            targetRenderer = target;
            style = artStyle;
            seed = artSeed;
            resolution = size;
            parallax = useParallax;
        }

        void Start()
        {
            if (targetRenderer == null)
                return;

            m_Block = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(m_Block);
            var texture = GetTexture(style, seed, resolution);
            m_Block.SetTexture(BaseMapId, texture);
            m_Block.SetTexture(MainTexId, texture);
            m_Block.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));
            targetRenderer.SetPropertyBlock(m_Block);
            m_Camera = Camera.main;
        }

        void LateUpdate()
        {
            if (!parallax || targetRenderer == null || m_Block == null)
                return;
            if (m_Camera == null && (m_Camera = Camera.main) == null)
                return;

            // Smer pohľadu v lokálnych súradniciach okna (+Z = von zo steny k používateľovi).
            var local = transform.InverseTransformPoint(m_Camera.transform.position);
            if (local.z < 0.05f)
                local.z = 0.05f;

            float view = parallaxView;
            float margin = (1f - view) * 0.5f;
            // Keď sa pozeráš zľava, „cez okno“ vidíš viac z pravej strany krajiny – posun je opačný.
            float offX = margin - Mathf.Clamp(local.x / local.z * parallaxStrength, -1f, 1f) * margin;
            float offY = margin - Mathf.Clamp(local.y / local.z * parallaxStrength, -1f, 1f) * margin;

            // Plátno je otočené o 180° (pozri editor), preto je os X zrkadlená.
            m_Block.SetVector(BaseMapStId, new Vector4(view, view, 1f - view - offX, offY));
            targetRenderer.SetPropertyBlock(m_Block);
        }

        public static Texture2D GetTexture(ArtStyle style, int seed, Vector2Int size)
        {
            size.x = Mathf.Clamp(size.x, 16, 1024);
            size.y = Mathf.Clamp(size.y, 16, 1024);
            var key = (style, seed, size.x, size.y);
            if (s_Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var pixels = new Color32[size.x * size.y];
            var rng = new System.Random(seed);
            switch (style)
            {
                case ArtStyle.Landscape: DrawLandscape(pixels, size.x, size.y, rng); break;
                case ArtStyle.WindowView: DrawWindowView(pixels, size.x, size.y, rng); break;
                default: DrawAbstract(pixels, size.x, size.y, rng); break;
            }

            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, true)
            {
                name = $"Art_{style}_{seed}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            s_Cache[key] = texture;
            return texture;
        }

        // ------------------------------------------------------------------ kreslenie

        static void DrawAbstract(Color32[] px, int w, int h, System.Random rng)
        {
            Color[] palette =
            {
                new Color(0.93f, 0.36f, 0.27f), new Color(0.98f, 0.76f, 0.25f),
                new Color(0.16f, 0.45f, 0.58f), new Color(0.12f, 0.16f, 0.24f),
                new Color(0.55f, 0.75f, 0.68f)
            };
            Fill(px, w, h, (x, y) => new Color(0.96f, 0.93f, 0.87f));

            for (int i = 0; i < 9; i++)
            {
                var color = palette[rng.Next(palette.Length)];
                float cx = (float)rng.NextDouble() * w, cy = (float)rng.NextDouble() * h;
                if (rng.Next(2) == 0)
                {
                    float r = (0.08f + (float)rng.NextDouble() * 0.22f) * Mathf.Min(w, h);
                    Fill(px, w, h, (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r ? color : (Color?)null);
                }
                else
                {
                    float hw = (0.05f + (float)rng.NextDouble() * 0.2f) * w, hh = (0.05f + (float)rng.NextDouble() * 0.2f) * h;
                    Fill(px, w, h, (x, y) => Mathf.Abs(x - cx) < hw && Mathf.Abs(y - cy) < hh ? color : (Color?)null);
                }
            }
        }

        static void DrawLandscape(Color32[] px, int w, int h, System.Random rng)
        {
            var skyTop = new Color(0.98f, 0.55f, 0.35f);
            var skyBottom = new Color(1f, 0.85f, 0.55f);
            float sunX = w * (0.3f + (float)rng.NextDouble() * 0.4f), sunY = h * 0.55f, sunR = Mathf.Min(w, h) * 0.12f;
            Fill(px, w, h, (x, y) =>
            {
                var sky = Color.Lerp(skyBottom, skyTop, (float)y / h);
                float d = Mathf.Sqrt((x - sunX) * (x - sunX) + (y - sunY) * (y - sunY));
                return d < sunR ? new Color(1f, 0.95f, 0.8f) : sky;
            });
            DrawHills(px, w, h, rng, 0.55f, 0.08f, new Color(0.62f, 0.38f, 0.42f));
            DrawHills(px, w, h, rng, 0.40f, 0.07f, new Color(0.42f, 0.25f, 0.33f));
            DrawHills(px, w, h, rng, 0.22f, 0.06f, new Color(0.22f, 0.14f, 0.22f));
        }

        static void DrawWindowView(Color32[] px, int w, int h, System.Random rng)
        {
            var skyTop = new Color(0.30f, 0.55f, 0.92f);
            var skyBottom = new Color(0.75f, 0.88f, 1f);
            Fill(px, w, h, (x, y) => Color.Lerp(skyBottom, skyTop, (float)y / h));

            // Oblaky – zhluky mäkkých kruhov.
            for (int c = 0; c < 5; c++)
            {
                float cx = (float)rng.NextDouble() * w, cy = h * (0.65f + (float)rng.NextDouble() * 0.3f);
                for (int i = 0; i < 5; i++)
                {
                    float ox = cx + ((float)rng.NextDouble() - 0.5f) * w * 0.18f;
                    float oy = cy + ((float)rng.NextDouble() - 0.5f) * h * 0.04f;
                    float r = Mathf.Min(w, h) * (0.04f + (float)rng.NextDouble() * 0.04f);
                    Fill(px, w, h, (x, y) =>
                    {
                        float d = Mathf.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));
                        return d < r ? Color.white : (Color?)null;
                    });
                }
            }

            DrawHills(px, w, h, rng, 0.45f, 0.10f, new Color(0.55f, 0.65f, 0.80f)); // vzdialené hory
            DrawHills(px, w, h, rng, 0.30f, 0.06f, new Color(0.40f, 0.65f, 0.35f));
            DrawHills(px, w, h, rng, 0.18f, 0.05f, new Color(0.28f, 0.52f, 0.25f));

            // Stromy – tmavé trojuholníky na poslednom kopci.
            for (int i = 0; i < 9; i++)
            {
                float tx = (float)rng.NextDouble() * w, baseY = h * (0.12f + (float)rng.NextDouble() * 0.08f);
                float th = h * (0.10f + (float)rng.NextDouble() * 0.06f), tw = th * 0.35f;
                var green = new Color(0.12f, 0.32f, 0.16f);
                Fill(px, w, h, (x, y) =>
                {
                    float t = (y - baseY) / th;
                    return t >= 0f && t <= 1f && Mathf.Abs(x - tx) < tw * (1f - t) ? green : (Color?)null;
                });
            }
        }

        static void DrawHills(Color32[] px, int w, int h, System.Random rng, float baseHeight, float amplitude, Color color)
        {
            float f1 = 1.5f + (float)rng.NextDouble() * 2f, f2 = 4f + (float)rng.NextDouble() * 3f;
            float p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f;
            Fill(px, w, h, (x, y) =>
            {
                float u = (float)x / w;
                float top = (baseHeight + amplitude * (Mathf.Sin(u * f1 * 6.28f + p1) * 0.7f + Mathf.Sin(u * f2 * 6.28f + p2) * 0.3f)) * h;
                return y < top ? color : (Color?)null;
            });
        }

        delegate Color? PixelFunc(int x, int y);

        static void Fill(Color32[] px, int w, int h, System.Func<int, int, Color> func)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = func(x, y);
        }

        static void Fill(Color32[] px, int w, int h, PixelFunc func)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = func(x, y);
                    if (c.HasValue)
                        px[y * w + x] = c.Value;
                }
        }
    }
}
