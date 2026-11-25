using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDesigner
{
    /// <summary>Galéria uložených návrhov: náhľady (najnovšie prvé), celá obrazovka, zmazanie fotky.</summary>
    public class DesignGallery : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] RectTransform grid;
        [Tooltip("Vypnutý vzor náhľadu (RawImage + Button).")]
        [SerializeField] Button thumbnailTemplate;
        [SerializeField] GameObject emptyText;

        [Header("Celá obrazovka")]
        [SerializeField] GameObject fullView;
        [SerializeField] RawImage fullImage;
        [SerializeField] AspectRatioFitter fullFitter;
        [SerializeField] TMP_Text deleteLabel;

        const int ThumbnailWidth = 280;
        const string DeleteText = "Zmazať fotku", ConfirmText = "Naozaj zmazať?";

        readonly List<Texture2D> m_Thumbnails = new List<Texture2D>();
        Texture2D m_FullTexture;
        string m_FullPath;
        bool m_DeleteArmed;
        Coroutine m_Loading;

        public void Open()
        {
            panel.SetActive(true);
            Reload();
        }

        public void Close()
        {
            CloseFull();
            Clear();
            panel.SetActive(false);
        }

        public void CloseFull()
        {
            fullView.SetActive(false);
            fullImage.texture = null;
            if (m_FullTexture != null)
                Destroy(m_FullTexture);
            m_FullTexture = null;
            m_FullPath = null;
        }

        /// <summary>Prvé ťuknutie sa spýta, druhé fotku zmaže.</summary>
        public void DeleteCurrent()
        {
            if (m_FullPath == null)
                return;

            if (!m_DeleteArmed)
            {
                SetDeleteArmed(true);
                return;
            }

            try
            {
                File.Delete(m_FullPath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            CloseFull();
            Reload();
        }

        void OnDestroy()
        {
            Clear();
            if (m_FullTexture != null)
                Destroy(m_FullTexture);
        }

        void Reload()
        {
            Clear();

            var files = Directory.Exists(SaveDesignScreenshot.Folder)
                ? Directory.GetFiles(SaveDesignScreenshot.Folder, "*.png")
                : Array.Empty<string>();
            // V názve je dátum a čas, takže zostupne podľa názvu = najnovšie prvé.
            Array.Sort(files, (a, b) => string.CompareOrdinal(b, a));

            emptyText.SetActive(files.Length == 0);
            if (files.Length > 0)
                m_Loading = StartCoroutine(LoadThumbnails(files));
        }

        IEnumerator LoadThumbnails(string[] files)
        {
            foreach (var path in files)
            {
                var texture = LoadThumbnail(path);
                if (texture != null)
                {
                    m_Thumbnails.Add(texture);
                    var button = Instantiate(thumbnailTemplate, grid);
                    button.GetComponentInChildren<RawImage>().texture = texture;
                    button.onClick.AddListener(() => ShowFull(path));
                    button.gameObject.SetActive(true);
                }
                yield return null; // jedna fotka na snímku, aby sa UI nezaseklo
            }
            m_Loading = null;
        }

        void ShowFull(string path)
        {
            CloseFull();
            m_FullTexture = LoadTexture(path);
            if (m_FullTexture == null)
                return;

            m_FullPath = path;
            fullImage.texture = m_FullTexture;
            fullFitter.aspectRatio = (float)m_FullTexture.width / m_FullTexture.height;
            SetDeleteArmed(false);
            fullView.SetActive(true);
        }

        void SetDeleteArmed(bool armed)
        {
            m_DeleteArmed = armed;
            deleteLabel.text = armed ? ConfirmText : DeleteText;
        }

        void Clear()
        {
            if (m_Loading != null)
                StopCoroutine(m_Loading);
            m_Loading = null;

            if (grid != null)
                foreach (Transform child in grid)
                    if (child != thumbnailTemplate.transform)
                        Destroy(child.gameObject);

            foreach (var texture in m_Thumbnails)
                if (texture != null)
                    Destroy(texture);
            m_Thumbnails.Clear();
        }

        static Texture2D LoadTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                if (texture.LoadImage(File.ReadAllBytes(path)))
                    return texture;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Room Designer] Fotku sa nepodarilo načítať ({path}): {e.Message}");
            }

            Destroy(texture);
            return null;
        }

        /// <summary>Zmenšená kópia fotky – plné rozlíšenie všetkých fotiek by zabralo stovky MB.</summary>
        static Texture2D LoadThumbnail(string path)
        {
            var full = LoadTexture(path);
            if (full == null)
                return null;

            int width = Mathf.Min(ThumbnailWidth, full.width);
            int height = Mathf.Max(1, Mathf.RoundToInt((float)width * full.height / full.width));
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;

            Graphics.Blit(full, target);
            RenderTexture.active = target;
            var small = new Texture2D(width, height, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            small.Apply(false, true);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Destroy(full);
            return small;
        }
    }
}
