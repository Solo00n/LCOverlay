using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Анимация стрельбы турелей на блоке ловушек (как в HTML: трассеры и
    /// вспышки, летящие влево). Активируется, когда идёт «турельный» ивент
    /// BCME (BerserkTurrets и т.п.) и среди ловушек есть турели.
    /// Все трассеры — из пула (никаких аллокаций в кадре), текст ловушек
    /// пульсирует цветом (вместо сдвига, чтобы не воевать с LayoutGroup).
    /// </summary>
    public class TrapFireEffect : MonoBehaviour
    {
        private const int PoolSize = 8;
        private const float TracerLife = 0.3f;   // сек полёта трассера
        private const float SpawnEvery = 0.26f;  // интервал очередей
        private const float FlyDistance = 84f;   // дальность полёта влево

        private class Tracer
        {
            public RectTransform Rt;
            public Image Img;
            public float T;        // 0..1, < 0 — неактивен
            public Vector2 Start;
            public float DirY;     // небольшой наклон
        }

        private readonly List<Tracer> _pool = new List<Tracer>(PoolSize);
        private RectTransform _layer;
        private TextMeshProUGUI _text;
        private Color _baseColor;
        private float _spawnT;

        /// <summary>Включена ли стрельба (ставит OverlayManager по данным ивентов).</summary>
        public bool Firing;

        /// <summary>Иконки турелей — точки вылета трассеров (список из MobRailWidget, обновляется сам).</summary>
        public System.Collections.Generic.List<RectTransform> Emitters;

        /// <summary>layer — слой для трассеров; text — текст для пульсации (может быть null).</summary>
        public void Init(RectTransform layer, TextMeshProUGUI text, Color baseColor)
        {
            _layer = layer;
            _text = text;
            _baseColor = baseColor;

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Tracer", typeof(RectTransform));
                go.transform.SetParent(_layer, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);            // «дуло» — правый конец линии
                rt.sizeDelta = new Vector2(42f, 3f);
                var img = go.AddComponent<Image>();
                img.color = OverlayStyle.FromHex("FFD246");
                img.raycastTarget = false;
                go.SetActive(false);
                _pool.Add(new Tracer { Rt = rt, Img = img, T = -1f });
            }
        }

        private void Update()
        {
            try { UpdateInner(); } catch { /* эффект стрельбы не должен ронять оверлей */ }
        }

        private void UpdateInner()
        {
            if (_layer == null) return;
            float dt = Time.unscaledDeltaTime;

            if (Firing)
            {
                _spawnT += dt;
                if (_spawnT >= SpawnEvery)
                {
                    _spawnT = 0f;
                    if (Random.value < 0.8f) Spawn();
                }
                // пульсация текста ловушек — «турели работают»
                if (_text != null)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
                    _text.color = Color.Lerp(_baseColor, Color.white, k * 0.6f);
                }
            }
            else
            {
                if (_text != null && _text.color != _baseColor) _text.color = _baseColor;
            }

            // полёт активных трассеров (пул, без аллокаций)
            for (int i = 0; i < _pool.Count; i++)
            {
                var tr = _pool[i];
                if (tr.T < 0f) continue;
                tr.T += dt / TracerLife;
                if (tr.T >= 1f)
                {
                    tr.T = -1f;
                    tr.Rt.gameObject.SetActive(false);
                    continue;
                }
                tr.Rt.anchoredPosition = tr.Start + new Vector2(-FlyDistance * tr.T, tr.DirY * tr.T);
                // резкий вылет, затем угасание
                float a = tr.T < 0.18f ? tr.T / 0.18f : 1f - (tr.T - 0.18f) / 0.82f;
                var c = tr.Img.color;
                c.a = a * 0.95f;
                tr.Img.color = c;
            }
        }

        private void Spawn()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var tr = _pool[i];
                if (tr.T >= 0f) continue; // занят
                // приоритет — вылет из «дула» случайной иконки турели на рейке
                if (Emitters != null && Emitters.Count > 0)
                {
                    var e = Emitters[Random.Range(0, Emitters.Count)];
                    if (e == null) return;
                    tr.Start = e.anchoredPosition + new Vector2(-16f, Random.Range(2f, 16f));
                }
                else
                {
                    var rect = _layer.rect;
                    tr.Start = new Vector2(
                        Random.Range(0f, rect.width * 0.45f),
                        Random.Range(-rect.height * 0.25f, rect.height * 0.25f));
                }
                tr.DirY = Random.Range(-8f, 8f);
                tr.T = 0f;
                tr.Rt.anchoredPosition = tr.Start;
                tr.Rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
                tr.Rt.gameObject.SetActive(true);
                return;
            }
        }
    }
}
