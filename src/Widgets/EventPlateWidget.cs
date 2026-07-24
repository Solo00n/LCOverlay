using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Мини-плашка ивента BCME. Появляется/исчезает через прозрачность
    /// (CanvasGroup), БЕЗ движения/масштабирования — чтобы эффект перспективы
    /// (запечённый в меш) не «съезжал». Позиция фиксированная под панелью.
    /// </summary>
    public class EventPlateWidget : MonoBehaviour
    {
        private const float AnimTime = 0.4f;

        private RectTransform _rt;
        private CanvasGroup _cg;
        private bool _wantVisible;
        private float _t; // 0 скрыто, 1 показано

        public void Init(RectTransform rt)
        {
            _rt = rt;
            _cg = rt.gameObject.GetComponent<CanvasGroup>();
            if (_cg == null) _cg = rt.gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
            gameObject.SetActive(false);
        }

        public bool Visible => _wantVisible;
        public float Progress => _t; // для позиционирования рейки ловушек под плашкой

        public void SetVisible(bool v)
        {
            if (_wantVisible == v) return;
            _wantVisible = v;
            if (v) gameObject.SetActive(true);
        }

        private void Update()
        {
            float target = _wantVisible ? 1f : 0f;
            _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime / AnimTime);
            float e = 1f - Mathf.Pow(1f - _t, 3f); // ease-out
            if (_cg != null) _cg.alpha = e;
            if (!_wantVisible && _t <= 0.001f && gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
