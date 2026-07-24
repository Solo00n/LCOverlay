using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Мерцающий «отвалившийся пиксель» у уголков рамки — аналог CSS-анимации
    /// flick (steps(1)) из HTML-оверлея: горит, коротко гаснет, вспыхивает.
    /// </summary>
    public class PixbitFlicker : MonoBehaviour
    {
        private Image _img;
        private float _period = 1.1f;
        private float _phase;

        /// <summary>Мастер-переключатель (гасится вместе с панелью, ShowPanel).</summary>
        public bool Master = true;

        public void Init(Image img, float period, float phase)
        {
            _img = img;
            _period = Mathf.Max(0.2f, period);
            _phase = phase;
        }

        private void Update()
        {
            if (_img == null) return;
            if (!Master) { if (_img.enabled) _img.enabled = false; return; }
            // тайминги из @keyframes flick: 0-50% горит, 50-70% погас, 72%+ горит
            float t = Mathf.Repeat(Time.unscaledTime / _period + _phase, 1f);
            bool on = t < 0.5f || t >= 0.72f;
            if (_img.enabled != on) _img.enabled = on;
        }
    }
}
