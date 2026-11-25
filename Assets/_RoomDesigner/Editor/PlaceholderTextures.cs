using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoomDesigner.EditorTools
{
    /// <summary>Vygeneruje zástupné obrázky (obrazy a výhľady z okna) ako PNG súbory.</summary>
    public static class PlaceholderTextures
    {
        public const string PaintingsFolder = RoomDesignerAssetBuilder.Root + "/Paintings";
        public const string ViewsFolder = RoomDesignerAssetBuilder.Root + "/WindowViews";

        public static void CreatePaintings()
        {
            Write(PaintingsFolder, "Abstrakt", 384, 512, DrawAbstract);
            Write(PaintingsFolder, "Západ slnka", 640, 448, DrawSunset);
            Write(PaintingsFolder, "Mozaika", 448, 448, DrawMosaic);
        }

        public static void CreateViews()
        {
            Write(ViewsFolder, "More", 1024, 1024, DrawSea);
            Write(ViewsFolder, "Hory", 1024, 1024, DrawMountains);
            Write(ViewsFolder, "Mesto", 1024, 1024, DrawCity);
        }

        static void Write(string folder, string name, int width, int height, Action<Painter> draw)
        {
            var path = $"{folder}/{name}.png";
            if (File.Exists(path))
                return;

            var painter = new Painter(width, height, 7);
            draw(painter);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels32(painter.pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        // ------------------------------------------------------------------ obrazy

        static void DrawAbstract(Painter p)
        {
            Color[] palette =
            {
                new Color(0.93f, 0.36f, 0.27f), new Color(0.98f, 0.76f, 0.25f), new Color(0.16f, 0.45f, 0.58f),
                new Color(0.12f, 0.16f, 0.24f), new Color(0.55f, 0.75f, 0.68f)
            };
            p.Rect(0f, 0f, 1f, 1f, new Color(0.96f, 0.93f, 0.87f));
            for (int i = 0; i < 9; i++)
            {
                var color = palette[i % palette.Length];
                if (i % 2 == 0)
                    p.Circle(p.Random(0f, 1f), p.Random(0f, 1f), p.Random(0.08f, 0.28f), color);
                else
                    p.Rect(p.Random(0f, 0.7f), p.Random(0f, 0.7f), p.Random(0.1f, 0.4f), p.Random(0.1f, 0.4f), color);
            }
        }

        static void DrawSunset(Painter p)
        {
            p.Gradient(new Color(1f, 0.85f, 0.55f), new Color(0.98f, 0.55f, 0.35f));
            p.Circle(0.6f, 0.55f, 0.13f, new Color(1f, 0.95f, 0.8f));
            p.Hills(0.52f, 0.08f, 2f, new Color(0.62f, 0.38f, 0.42f));
            p.Hills(0.38f, 0.07f, 1.6f, new Color(0.42f, 0.25f, 0.33f));
            p.Hills(0.22f, 0.06f, 2.4f, new Color(0.22f, 0.14f, 0.22f));
        }

        static void DrawMosaic(Painter p)
        {
            const int count = 8;
            for (int y = 0; y < count; y++)
                for (int x = 0; x < count; x++)
                    p.Rect((float)x / count, (float)y / count, 1f / count, 1f / count,
                        Color.HSVToRGB(p.Random(0.5f, 0.72f), p.Random(0.3f, 0.7f), p.Random(0.5f, 0.95f)));
        }

        // ------------------------------------------------------------------ výhľady z okna

        static void DrawSky(Painter p)
        {
            p.Gradient(new Color(0.75f, 0.88f, 1f), new Color(0.30f, 0.55f, 0.92f));
            for (int c = 0; c < 6; c++)
            {
                float x = p.Random(0f, 1f), y = p.Random(0.7f, 0.95f);
                for (int i = 0; i < 6; i++)
                    p.Circle(x + p.Random(-0.09f, 0.09f), y + p.Random(-0.015f, 0.015f), p.Random(0.02f, 0.04f), Color.white);
            }
        }

        static void DrawSea(Painter p)
        {
            DrawSky(p);
            p.Circle(0.7f, 0.68f, 0.06f, new Color(1f, 0.96f, 0.75f));
            p.VerticalBand(0.18f, 0.52f, new Color(0.10f, 0.48f, 0.62f), new Color(0.20f, 0.42f, 0.72f));
            for (int i = 0; i < 120; i++) // odlesky na vlnách
                p.Rect(p.Random(0f, 1f), p.Random(0.2f, 0.5f), p.Random(0.01f, 0.05f), 0.0025f, new Color(0.65f, 0.83f, 0.93f));
            p.Hills(0.18f, 0.02f, 1.2f, new Color(0.93f, 0.84f, 0.64f));
        }

        static void DrawMountains(Painter p)
        {
            DrawSky(p);
            for (int i = 0; i < 7; i++) // štíty so snehom
            {
                float x = i / 6f + p.Random(-0.05f, 0.05f), top = p.Random(0.55f, 0.72f);
                p.Triangle(x, 0.3f, 0.24f, top - 0.3f, new Color(0.45f, 0.52f, 0.63f));
                p.Triangle(x, 0.3f + (top - 0.3f) * 0.72f, 0.24f * 0.28f, (top - 0.3f) * 0.28f, Color.white);
            }
            p.Hills(0.36f, 0.05f, 1.7f, new Color(0.40f, 0.65f, 0.35f));
            p.Hills(0.22f, 0.04f, 2.2f, new Color(0.28f, 0.52f, 0.25f));
            for (int i = 0; i < 26; i++)
                p.Triangle(p.Random(0f, 1f), p.Random(0.08f, 0.2f), p.Random(0.02f, 0.035f), p.Random(0.09f, 0.14f), new Color(0.12f, 0.32f, 0.16f));
        }

        static void DrawCity(Painter p)
        {
            DrawSky(p);
            DrawHouses(p, 14, 0.45f, 0.68f, new Color(0.62f, 0.68f, 0.76f), new Color(0.76f, 0.82f, 0.88f));
            DrawHouses(p, 9, 0.22f, 0.48f, new Color(0.36f, 0.38f, 0.45f), new Color(1f, 0.88f, 0.55f));
            p.Rect(0f, 0f, 1f, 0.1f, new Color(0.3f, 0.45f, 0.28f));
        }

        static void DrawHouses(Painter p, int count, float minHeight, float maxHeight, Color wall, Color window)
        {
            float width = 1f / count;
            for (int i = 0; i < count; i++)
            {
                float height = p.Random(minHeight, maxHeight);
                p.Rect(i * width, 0f, width, height, wall * p.Random(0.85f, 1.1f));
                for (float y = 0.12f; y < height - 0.03f; y += 0.035f)
                    for (int k = 0; k < 3; k++)
                        if (p.Random(0f, 1f) < 0.75f)
                            p.Rect((i + 0.16f + k * 0.26f) * width, y, width * 0.14f, 0.018f, window);
            }
        }

        /// <summary>Kreslenie do poľa pixelov; súradnice sú 0–1, začiatok vľavo dole.</summary>
        class Painter
        {
            public readonly Color32[] pixels;
            readonly int m_Width, m_Height;
            readonly System.Random m_Random;

            public Painter(int width, int height, int seed)
            {
                m_Width = width;
                m_Height = height;
                pixels = new Color32[width * height];
                m_Random = new System.Random(seed);
            }

            public float Random(float min, float max) => min + (float)m_Random.NextDouble() * (max - min);

            public void Gradient(Color bottom, Color top) => VerticalBand(0f, 1f, bottom, top);

            public void VerticalBand(float from, float to, Color bottom, Color top) =>
                Fill(0f, from, 1f, to, (x, y) => Color.Lerp(bottom, top, (y - from) / (to - from)));

            public void Rect(float x, float y, float width, float height, Color color) =>
                Fill(x, y, x + width, y + height, (u, v) => color);

            public void Circle(float cx, float cy, float radius, Color color)
            {
                float aspect = (float)m_Height / m_Width; // polomer je v násobkoch šírky
                float ry = radius / aspect;
                Fill(cx - radius, cy - ry, cx + radius, cy + ry, (x, y) =>
                {
                    float dx = x - cx, dy = (y - cy) * aspect;
                    return dx * dx + dy * dy < radius * radius ? color : (Color?)null;
                });
            }

            public void Triangle(float cx, float baseY, float halfWidth, float height, Color color) =>
                Fill(cx - halfWidth, baseY, cx + halfWidth, baseY + height, (x, y) =>
                    Mathf.Abs(x - cx) < halfWidth * (1f - (y - baseY) / height) ? color : (Color?)null);

            public void Hills(float baseHeight, float amplitude, float frequency, Color color)
            {
                float p1 = Random(0f, 6.28f), p2 = Random(0f, 6.28f);
                Fill(0f, 0f, 1f, baseHeight + amplitude, (x, y) =>
                {
                    float wave = Mathf.Sin(x * frequency * 6.28f + p1) * 0.7f + Mathf.Sin(x * frequency * 16f + p2) * 0.3f;
                    return y < baseHeight + amplitude * wave ? color : (Color?)null;
                });
            }

            void Fill(float x0, float y0, float x1, float y1, Func<float, float, Color?> color)
            {
                int left = Mathf.Clamp(Mathf.RoundToInt(x0 * m_Width), 0, m_Width);
                int right = Mathf.Clamp(Mathf.RoundToInt(x1 * m_Width), 0, m_Width);
                int bottom = Mathf.Clamp(Mathf.RoundToInt(y0 * m_Height), 0, m_Height);
                int top = Mathf.Clamp(Mathf.RoundToInt(y1 * m_Height), 0, m_Height);

                for (int y = bottom; y < top; y++)
                    for (int x = left; x < right; x++)
                    {
                        var c = color((x + 0.5f) / m_Width, (y + 0.5f) / m_Height);
                        if (c.HasValue)
                            pixels[y * m_Width + x] = c.Value;
                    }
            }
        }
    }

    /// <summary>
    /// Nastavenie importu pre obrazy a výhľady: zachová pôvodný pomer strán
    /// (Unity inak rozmery zaokrúhli na mocniny dvojky) a obmedzí veľkosť.
    /// </summary>
    class WallTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PlaceholderTextures.PaintingsFolder + "/") &&
                !assetPath.StartsWith(PlaceholderTextures.ViewsFolder + "/"))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
        }
    }
}
