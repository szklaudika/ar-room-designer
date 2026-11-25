using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoomDesigner.EditorTools
{
    /// <summary>Vygeneruje obrázky pre UI: zaoblený tvar, mäkký tieň a ikony (biele, farbu určí Image).</summary>
    public static class UiSprites
    {
        public const string Folder = RoomDesignerAssetBuilder.Root + "/UI";

        /// <summary>Polomer rohu v sprite Rounded (v pixeloch).</summary>
        public const float CornerRadius = 64f;

        const int IconSize = 128;

        public static void BuildAll()
        {
            RoomDesignerAssetBuilder.EnsureFolder(Folder);

            Write("Rounded", 128, 63, p => Edge(p.magnitude - 64f, 1f));
            Write("Shadow", 128, 63, p => Mathf.SmoothStep(0f, 1f, 1f - p.magnitude / 64f));

            // Súradnice ikon sú 0–24, začiatok vľavo dole.
            Icon("Save", c =>
            {
                c.Box(3, 4, 21, 18, 3);
                c.Line(8.5f, 18, 9.5f, 20.5f, 14.5f, 20.5f, 15.5f, 18);
                c.Circle(12, 11, 3.6f);
            });
            Icon("Gallery", c =>
            {
                c.Box(3, 3, 21, 21, 3);
                c.Circle(8.5f, 15.5f, 1.8f);
                c.Line(4, 6.5f, 9, 11, 13, 7.5f, 16, 10.5f, 20.5f, 6.5f);
            });
            Icon("Planes", c =>
            {
                c.Line(12, 21, 21, 16, 12, 11, 3, 16, 12, 21);
                c.Line(3, 11.5f, 12, 6.5f, 21, 11.5f);
                c.Line(3, 7, 12, 2, 21, 7);
            });
            Icon("Clear", c =>
            {
                c.Line(4, 18, 20, 18);
                c.Line(9, 18, 9, 21, 15, 21, 15, 18);
                c.Line(6, 18, 7, 3, 17, 3, 18, 18);
                c.Line(10, 14, 10, 7);
                c.Line(14, 14, 14, 7);
            });
            Icon("Back", c =>
            {
                c.Line(20, 12, 4, 12);
                c.Line(11, 19, 4, 12, 11, 5);
            });

            Icon("stol", c =>
            {
                c.Box(2, 15, 22, 18, 1);
                c.Line(5, 15, 5, 5);
                c.Line(19, 15, 19, 5);
            });
            Icon("stolicka", c =>
            {
                c.Box(7, 12, 17, 22, 2);
                c.Line(5, 12, 19, 12);
                c.Line(6, 12, 5, 2);
                c.Line(18, 12, 19, 2);
                c.Line(5.6f, 7, 18.4f, 7);
            });
            Icon("gauc", c =>
            {
                c.Line(5, 13, 5, 17, 6, 19, 18, 19, 19, 17, 19, 13);
                c.Box(2, 5, 22, 13, 2.5f);
                c.Line(6, 12, 6, 9, 18, 9, 18, 12);
                c.Line(5, 5, 5, 3);
                c.Line(19, 5, 19, 3);
            });
            Icon("postel", c =>
            {
                c.Line(2, 20, 2, 4);
                c.Line(2, 7, 22, 7, 22, 4);
                c.Line(22, 7, 22, 12, 20, 14, 10, 14, 10, 7);
                c.Circle(6, 12, 2f);
            });
            Icon("lampa", c =>
            {
                c.Line(8.5f, 21, 15.5f, 21, 19, 13, 5, 13, 8.5f, 21);
                c.Line(12, 13, 12, 3);
                c.Line(7.5f, 3, 16.5f, 3);
            });
        }

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(PathOf(name));

        /// <summary>Ikona nábytku podľa ID v katalógu (null, ak pre neho ikona nie je).</summary>
        public static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(PathOf(name));

        static string PathOf(string name) => $"{Folder}/{name}.png";

        static void Icon(string name, Action<IconShape> draw)
        {
            var shape = new IconShape();
            draw(shape);
            Write(name, IconSize, 0, p => Edge(shape.Distance((p / IconSize + Vector2.one * 0.5f) * 24f) - 0.85f, IconSize / 24f));
        }

        /// <summary>Vyhladený okraj: 1 vnútri tvaru, 0 mimo.</summary>
        static float Edge(float distance, float pixelsPerUnit) => Mathf.Clamp01(0.5f - distance * pixelsPerUnit);

        static void Write(string name, int size, int border, Func<Vector2, float> alpha)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * alpha(p)));
                }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(PathOf(name), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PathOf(name));

            var importer = (TextureImporter)AssetImporter.GetAtPath(PathOf(name));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.one * border;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = border == 0; // ikony sa zmenšujú
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>Ikona z čiar so zaoblenými koncami.</summary>
        class IconShape
        {
            Func<Vector2, float> m_Distance = p => float.MaxValue;

            public float Distance(Vector2 p) => m_Distance(p);

            void Add(Func<Vector2, float> distance)
            {
                var previous = m_Distance;
                m_Distance = p => Mathf.Min(previous(p), distance(p));
            }

            public void Line(params float[] xy)
            {
                for (int i = 0; i + 3 < xy.Length; i += 2)
                {
                    Vector2 a = new Vector2(xy[i], xy[i + 1]), ab = new Vector2(xy[i + 2], xy[i + 3]) - a;
                    Add(p => (p - a - ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude)).magnitude);
                }
            }

            public void Circle(float x, float y, float radius)
            {
                var center = new Vector2(x, y);
                Add(p => Mathf.Abs((p - center).magnitude - radius));
            }

            public void Box(float x0, float y0, float x1, float y1, float radius)
            {
                Vector2 center = new Vector2(x0 + x1, y0 + y1) * 0.5f;
                Vector2 half = new Vector2(x1 - x0, y1 - y0) * 0.5f - Vector2.one * radius;
                Add(p =>
                {
                    var q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half;
                    return Mathf.Abs(Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius);
                });
            }
        }
    }
}
