using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Тайлит текстуру-полоску по высоте панели через uvRect (надёжнее, чем
    /// Image.Tiled). Панель меняет высоту (ContentSizeFitter), поэтому число
    /// повторов пересчитывается каждый кадр.
    /// </summary>
    public class ScanlineUV : MonoBehaviour
    {
        public RawImage Img;
        public float LinePx = 4f;

        private void LateUpdate()
        {
            if (Img == null) return;
            var r = ((RectTransform)transform).rect;
            float reps = Mathf.Max(1f, r.height / LinePx);
            Img.uvRect = new Rect(0f, 0f, 1f, reps);
        }
    }
}
