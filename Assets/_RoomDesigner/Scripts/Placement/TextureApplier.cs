using UnityEngine;

namespace RoomDesigner
{
    /// <summary>Objekt, ktorý po umiestnení dostane obrázok vybraný v menu (obraz, okno).</summary>
    public abstract class TextureApplier : MonoBehaviour
    {
        protected static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        protected static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        public abstract void Apply(Texture2D texture);
    }
}
