using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RoomDesigner
{
    /// <summary>
    /// Koreň umiestniteľného objektu (nábytok, obraz, okno).
    /// Pivot: nábytok = stred spodnej strany, predok smeruje na +Z.
    /// Objekt na stenu = stred zadnej strany (dotýka sa steny), predok (+Z) smeruje od steny.
    /// Potrebuje BoxCollider, aby sa naň dalo ťuknúť.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class PlaceableObject : MonoBehaviour
    {
        [SerializeField] string itemId;
        [SerializeField] PlacementSurface surface;

        [Tooltip("Materiál obrysu pri výbere.")]
        [SerializeField] Material highlightMaterial;
        [SerializeField] Color highlightColor = new Color(1f, 0.6f, 0.1f, 1f);
        [SerializeField] float highlightWidth = 0.01f;

        [Tooltip("Povolený rozsah zmeny veľkosti (1 = reálna veľkosť).")]
        [SerializeField] Vector2 scaleLimits = new Vector2(0.5f, 2f);

        LineRenderer m_Outline;

        public string ItemId => itemId;
        public PlacementSurface Surface => surface;
        public bool IsSelected { get; private set; }

        /// <summary>AR kotva, ku ktorej je objekt pripnutý (môže byť null).</summary>
        public ARAnchor Anchor { get; set; }

        /// <summary>Os, okolo ktorej sa objekt otáča dvoma prstami (normála plochy).</summary>
        public Vector3 SurfaceNormal => surface == PlacementSurface.Floor ? Vector3.up : transform.forward;

        public float UniformScale => transform.localScale.x;

        public void Configure(string id, PlacementSurface placementSurface, Material highlight)
        {
            itemId = id;
            surface = placementSurface;
            highlightMaterial = highlight;
        }

        public void SetUniformScale(float scale)
        {
            scale = Mathf.Clamp(scale, scaleLimits.x, scaleLimits.y);
            transform.localScale = Vector3.one * scale;
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selected && m_Outline == null)
                m_Outline = CreateOutline();
            if (m_Outline != null)
                m_Outline.enabled = selected;
        }

        /// <summary>Drôtený kváder okolo objektu (z BoxCollidera) – ukazuje, čo je vybrané.</summary>
        LineRenderer CreateOutline()
        {
            var box = GetComponent<BoxCollider>();
            var go = new GameObject("SelectionOutline");
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.widthMultiplier = highlightWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = line.endColor = highlightColor;
            if (highlightMaterial != null)
                line.sharedMaterial = highlightMaterial;

            var c = box.center;
            var e = box.size * 0.5f + Vector3.one * 0.01f; // trochu väčšie, aby obrys nebol v objekte
            var b0 = c + new Vector3(-e.x, -e.y, -e.z);
            var b1 = c + new Vector3(e.x, -e.y, -e.z);
            var b2 = c + new Vector3(e.x, -e.y, e.z);
            var b3 = c + new Vector3(-e.x, -e.y, e.z);
            var t0 = c + new Vector3(-e.x, e.y, -e.z);
            var t1 = c + new Vector3(e.x, e.y, -e.z);
            var t2 = c + new Vector3(e.x, e.y, e.z);
            var t3 = c + new Vector3(-e.x, e.y, e.z);

            // Jedna súvislá čiara cez všetkých 12 hrán kvádra.
            var points = new[] { b0, b1, b2, b3, b0, t0, t1, b1, t1, t2, b2, t2, t3, b3, t3, t0 };
            line.positionCount = points.Length;
            line.SetPositions(points);
            return line;
        }
    }
}
