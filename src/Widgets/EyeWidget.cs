using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// «Закрывающийся глаз»: по мере ухода оверлея с корабля глаз смыкается
    /// (веко опускается — вертикальный масштаб → тонкая линия). Мол игрок
    /// больше не видит справочную информацию.
    /// </summary>
    public class EyeWidget : MonoBehaviour
    {
        public Image Img;
        private RectTransform _rt;
        private float _open = 1f;   // 1 открыт, 0 закрыт
        private float _target = 1f;

        public void Init(RectTransform rt, Image img)
        {
            _rt = rt;
            Img = img;
        }

        /// <summary>Задать целевое состояние (1 открыт / 0 закрыт).</summary>
        public void SetOpen(float target) => _target = Mathf.Clamp01(target);

        private void Update()
        {
            _open = Mathf.MoveTowards(_open, _target, Time.unscaledDeltaTime / 0.35f);
            // минимум 0.06, чтобы закрытый глаз был тонкой линией, а не исчезал
            float sy = Mathf.Lerp(0.06f, 1f, _open);
            var s = _rt.localScale;
            _rt.localScale = new Vector3(s.x, sy, s.z);
        }
    }
}
