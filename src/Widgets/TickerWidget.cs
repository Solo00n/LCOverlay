using TMPro;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Бегущая строка (тикер): две одинаковые копии текста едут влево,
    /// когда первая полностью уходит — цикл продолжается бесшовно
    /// (тот же приём, что в HTML-оверлее). Анимация — простое смещение
    /// RectTransform, без аллокаций в кадре.
    /// UI-элементы создаёт OverlayManager и передаёт сюда через Init.
    /// </summary>
    public class TickerWidget : MonoBehaviour
    {
        private const float Speed = 60f;  // пикселей в секунду
        private const float Gap = 48f;    // зазор между копиями

        private RectTransform _track;
        private TextMeshProUGUI _copy1, _copy2;
        private float _copyWidth;
        private float _offset;
        private string _lastText;
        private bool _dirtyWidth;

        public void Init(RectTransform track, TextMeshProUGUI copy1, TextMeshProUGUI copy2)
        {
            _track = track;
            _copy1 = copy1;
            _copy2 = copy2;
        }

        /// <summary>Обновить содержимое (только при реальном изменении).</summary>
        public void SetContent(string text)
        {
            if (text == _lastText) return;
            _lastText = text;
            _copy1.text = text;
            _copy2.text = text;
            _dirtyWidth = true;
        }

        private void LateUpdate()
        {
            if (_copy1 == null || _track == null) return;

            if (_dirtyWidth)
            {
                _dirtyWidth = false;
                var pref = _copy1.GetPreferredValues(_copy1.text);
                float w = Mathf.Max(40f, pref.x);
                _copyWidth = w + Gap;
                _copy1.rectTransform.sizeDelta = new Vector2(w + 4f, pref.y);
                _copy2.rectTransform.sizeDelta = new Vector2(w + 4f, pref.y);
                _copy2.rectTransform.anchoredPosition = new Vector2(_copyWidth, 0f);
                if (_offset <= -_copyWidth) _offset = 0f;
            }

            _offset -= Speed * Time.unscaledDeltaTime;
            if (_offset <= -_copyWidth) _offset += _copyWidth;
            _track.anchoredPosition = new Vector2(_offset, 0f);
        }
    }
}
