using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RoomDesigner
{
    /// <summary>Uloží návrh ako fotku: bez UI, bez AR plôch a bez obrysu vybraného objektu.</summary>
    public class SaveDesignScreenshot : MonoBehaviour
    {
        [SerializeField] Canvas uiCanvas;
        [SerializeField] PlaneVisibilityToggle planeToggle;
        [SerializeField] PlacementManager placement;

        bool m_Busy;

        public static string Folder => Path.Combine(Application.persistentDataPath, "Navrhy");

        public void Save()
        {
            if (!m_Busy)
                StartCoroutine(Capture());
        }

        IEnumerator Capture()
        {
            m_Busy = true;
            bool planesWereVisible = planeToggle.PlanesVisible;
            var selected = placement.SelectedObject;

            uiCanvas.enabled = false;
            planeToggle.SetPlanesVisible(false);
            if (selected != null)
                selected.SetSelected(false);

            yield return null; // nech sa zmeny určite prejavia v celej snímke
            yield return new WaitForEndOfFrame();

            bool saved = false;
            try
            {
                SaveScreen();
                saved = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            uiCanvas.enabled = true;
            planeToggle.SetPlanesVisible(planesWereVisible);
            if (selected != null && placement.SelectedObject == selected)
                selected.SetSelected(true);
            m_Busy = false;

            placement.ShowMessage(saved ? $"Návrh uložený {placement.CheckMark}" : "Fotku sa nepodarilo uložiť");
        }

        static void SaveScreen()
        {
            Texture2D screen = null, photo = null;
            try
            {
                screen = ScreenCapture.CaptureScreenshotAsTexture();
                // Bez alfa kanála, inak by fotka mohla byť v galérii priehľadná.
                photo = new Texture2D(screen.width, screen.height, TextureFormat.RGB24, false);
                photo.SetPixels32(screen.GetPixels32());

                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, $"navrh_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                File.WriteAllBytes(path, photo.EncodeToPNG());
                Debug.Log($"[Room Designer] Návrh uložený: {path}");
            }
            finally
            {
                if (screen != null) Destroy(screen);
                if (photo != null) Destroy(photo);
            }
        }
    }
}
