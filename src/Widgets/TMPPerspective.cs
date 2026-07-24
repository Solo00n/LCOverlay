using TMPro;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Перспектива для TMP-текста. Обычный BaseMeshEffect на TextMeshPro НЕ
    /// действует (TMP генерирует меш сам), поэтому текст искажаем напрямую:
    /// на событие изменения текста двигаем вершины по той же формуле, что и
    /// PerspectiveWarp у картинок — тогда текст и фон трапецируются одинаково.
    /// </summary>
    public class TMPPerspective : MonoBehaviour
    {
        public RectTransform Panel;
        public float Width = 340f;
        public float Strength = 0.16f;
        public bool Continuous; // для движущегося текста (бегущая строка) — пересчёт каждый кадр

        private TMP_Text _tmp;
        private bool _busy;

        private void Awake() { _tmp = GetComponent<TMP_Text>(); }

        private void LateUpdate()
        {
            // движущийся текст: регенерируем плоский меш (→ TEXT_CHANGED → Warp),
            // чтобы перспектива пересчитывалась под текущую позицию прокрутки
            if (Continuous && _tmp != null && Strength > 0f)
                _tmp.ForceMeshUpdate();
        }

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnChanged);
            if (_tmp != null) _tmp.SetVerticesDirty();
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnChanged);
        }

        private void OnChanged(Object obj)
        {
            if (obj == _tmp) Warp();
        }

        private void Warp()
        {
            if (_busy || _tmp == null || Panel == null || Strength <= 0f) return;
            _busy = true;
            try
            {
                var ti = _tmp.textInfo;
                if (ti == null || ti.meshInfo == null) return;
                var grt = (RectTransform)transform;
                for (int m = 0; m < ti.meshInfo.Length; m++)
                {
                    var verts = ti.meshInfo[m].vertices;
                    if (verts == null) continue;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 world = grt.TransformPoint(verts[i]);
                        Vector3 pp = Panel.InverseTransformPoint(world);
                        float far = Mathf.Clamp01(-pp.x / Width);
                        pp.y *= Mathf.Lerp(1f, 1f - Strength, far);
                        pp.x *= Mathf.Lerp(1f, 1f - Strength * 0.35f, far);
                        verts[i] = grt.InverseTransformPoint(Panel.TransformPoint(pp));
                    }
                    var mesh = ti.meshInfo[m].mesh;
                    if (mesh != null)
                    {
                        mesh.vertices = verts;
                        _tmp.UpdateGeometry(mesh, m);
                    }
                }
            }
            catch { }
            finally { _busy = false; }
        }
    }
}
