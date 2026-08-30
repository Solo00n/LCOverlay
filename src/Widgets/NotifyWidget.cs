using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Режим уведомлений: оверлей не висит перед глазами всё время, а спит с нулевой
    /// прозрачностью и просыпается, только когда что-то изменилось.
    ///
    /// Что происходит при новости:
    ///  1. над панелью появляется «папка» цвета текущего стиля и мельтешит, как
    ///     8-битная картинка (пиксельная сетка + дрожание + скачки яркости);
    ///  2. папка уезжает вниз, в верхний край панели, и там растворяется;
    ///  3. панель разгорается, изменившиеся цифры коротко мерцают;
    ///  4. через несколько секунд тишины панель снова гаснет.
    ///
    /// Включение и выключение озвучены родными звуками радар-бустера.
    /// </summary>
    internal class NotifyWidget : MonoBehaviour
    {
        // ---- каналы новостей: у каждого свой текст, который мерцает ----
        public enum Channel { Quota, Deaths, Day, Monsters, Traps, Events, Loot, Moon }

        private const float PacketHover = 0.75f;   // сколько папка висит и мельтешит
        private const float PacketFly = 0.45f;     // сколько летит в панель
        private const float FadeIn = 0.25f;
        private const float FadeOut = 0.7f;

        private RectTransform _host;      // куда класть папки (корень панели)
        private OverlayStyle _style;
        private Sprite _folder;

        private float _wake;              // 0 спит … 1 бодрствует
        private float _holdUntil = -999f;
        private bool _awakeSfxPlayed;

        /// <summary>Множитель прозрачности всей панели.</summary>
        public float Wake => _wake;

        /// <summary>Сейчас панель разбужена (для звука/логики снаружи).</summary>
        public bool IsAwake => _wake > 0.01f;

        private class Packet
        {
            public RectTransform Rt;
            public Image Img;
            public float T;          // прожитое время
            public float Seed;
            public Vector2 Home;     // где висит
            public Vector2 Target;   // куда влетает
        }

        private readonly List<Packet> _packets = new List<Packet>();
        private readonly Dictionary<TextMeshProUGUI, float> _flicker =
            new Dictionary<TextMeshProUGUI, float>();

        public void Init(RectTransform host, OverlayStyle style)
        {
            _host = host;
            _style = style;
            _folder = BuildFolderSprite();
        }

        public void SetStyle(OverlayStyle style) => _style = style;

        // ======================= новости =======================

        /// <summary>Пришла новость: будим панель и запускаем папку.</summary>
        public void Ping(Channel ch, TextMeshProUGUI flickerTarget)
        {
            _holdUntil = Time.unscaledTime + Mathf.Max(1f, ConfigSettings.NotifyHoldSeconds.Value);
            if (!_awakeSfxPlayed)
            {
                _awakeSfxPlayed = true;
                RadarSfx.PlayOn();
            }
            SpawnPacket();
            if (flickerTarget != null) _flicker[flickerTarget] = 0f;
        }

        private void SpawnPacket()
        {
            if (_host == null || _folder == null) return;
            if (_packets.Count >= 4) return;         // не заваливаем экран

            var go = new GameObject("NotifyPacket", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_host, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // левый верх панели
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(26f, 20f);

            var img = go.GetComponent<Image>();
            img.sprite = _folder;
            img.raycastTarget = false;
            img.preserveAspect = true;

            // папки выстраиваются в ряд, чтобы не наезжали друг на друга
            float x = 34f + _packets.Count * 30f;
            var home = new Vector2(x, 34f);          // ВЫШЕ верхнего края панели
            _packets.Add(new Packet
            {
                Rt = rt,
                Img = img,
                T = 0f,
                Seed = Random.Range(0f, 100f),
                Home = home,
                Target = new Vector2(x, -6f),        // внутрь, в шапку панели
            });
        }

        // ======================= жизнь =======================

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool wantAwake = Time.unscaledTime < _holdUntil;

            // разгораемся быстро, гаснем медленно — так спокойнее для глаза
            _wake = Mathf.MoveTowards(_wake, wantAwake ? 1f : 0f, dt / (wantAwake ? FadeIn : FadeOut));

            if (!wantAwake && _awakeSfxPlayed && _wake <= 0.001f)
            {
                _awakeSfxPlayed = false;
                RadarSfx.PlayOff();
            }

            UpdatePackets(dt);
        }

        private void UpdatePackets(float dt)
        {
            for (int i = _packets.Count - 1; i >= 0; i--)
            {
                var p = _packets[i];
                p.T += dt;

                float a;
                Vector2 pos;
                float scale = 1f;

                if (p.T < PacketHover)
                {
                    // висит над панелью и мельтешит
                    a = Mathf.Clamp01(p.T / 0.12f);
                    pos = p.Home + PixelJitter(p.Seed, 2f);
                    // скачки яркости, как у плохого сигнала
                    if (Mathf.PerlinNoise(Time.unscaledTime * 22f, p.Seed) > 0.78f) a *= 0.35f;
                }
                else
                {
                    float k = Mathf.Clamp01((p.T - PacketHover) / PacketFly);
                    pos = Vector2.Lerp(p.Home, p.Target, k * k);   // ускоряется к панели
                    pos += PixelJitter(p.Seed, 1f) * (1f - k);
                    a = 1f - k;                                    // растворяется на входе
                    scale = Mathf.Lerp(1f, 0.55f, k);
                    if (k >= 1f) { Destroy(p.Rt.gameObject); _packets.RemoveAt(i); continue; }
                }

                // пиксельная сетка: позиция кратна 2 — так это читается как 8-битная картинка
                pos = new Vector2(Mathf.Round(pos.x / 2f) * 2f, Mathf.Round(pos.y / 2f) * 2f);
                p.Rt.anchoredPosition = pos;
                p.Rt.localScale = new Vector3(scale, scale, 1f);
                var c = _style != null ? _style.Accent : Color.white;
                p.Img.color = new Color(c.r, c.g, c.b, a);
            }
        }

        private static Vector2 PixelJitter(float seed, float amp)
        {
            float t = Time.unscaledTime * 18f;
            return new Vector2(
                (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * amp,
                (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * amp);
        }

        /// <summary>
        /// Мерцание изменившихся цифр. Зовётся ПОСЛЕ отрисовки панели: та каждый кадр
        /// проставляет цвета заново, поэтому иначе мерцание было бы затёрто.
        /// </summary>
        public void ApplyFlicker()
        {
            if (_flicker.Count == 0) return;
            var done = new List<TextMeshProUGUI>();
            var keys = new List<TextMeshProUGUI>(_flicker.Keys);
            foreach (var t in keys)
            {
                float age = _flicker[t] + Time.unscaledDeltaTime;
                _flicker[t] = age;
                if (t == null || age > 1.1f) { done.Add(t); continue; }

                // три быстрых вспышки в акцент, затем затухание
                float blink = Mathf.Abs(Mathf.Sin(age * 16f)) * Mathf.Clamp01(1f - age / 1.1f);
                var baseC = t.color;
                var hot = _style != null ? _style.Accent : Color.white;
                t.color = Color.Lerp(baseC, new Color(hot.r, hot.g, hot.b, baseC.a), blink);
            }
            foreach (var t in done) _flicker.Remove(t);
        }

        public void ResetAll()
        {
            _holdUntil = -999f;
            _wake = 0f;
            _awakeSfxPlayed = false;
            _flicker.Clear();
            foreach (var p in _packets) if (p.Rt != null) Destroy(p.Rt.gameObject);
            _packets.Clear();
        }

        // ======================= папка =======================

        /// <summary>
        /// Рисуем иконку папки кодом: 16×13, точечная фильтрация — на экране это
        /// выглядит как настоящая 8-битная картинка и красится в цвет стиля.
        /// </summary>
        private static Sprite BuildFolderSprite()
        {
            const int W = 16, H = 13;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var clear = new Color(1f, 1f, 1f, 0f);
            var solid = Color.white;
            var dim = new Color(1f, 1f, 1f, 0.55f);

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    tex.SetPixel(x, y, clear);

            // корпус папки (низ), y = 0..9
            for (int y = 0; y <= 9; y++)
                for (int x = 1; x <= 14; x++)
                {
                    bool edge = y == 0 || y == 9 || x == 1 || x == 14;
                    tex.SetPixel(x, y, edge ? solid : dim);
                }
            // «язычок» сверху слева, y = 10..11
            for (int y = 10; y <= 11; y++)
                for (int x = 1; x <= 7; x++)
                {
                    bool edge = y == 11 || x == 1 || x == 7;
                    tex.SetPixel(x, y, edge ? solid : dim);
                }
            // две «строки данных» внутри — чтобы читалось как документ
            for (int x = 4; x <= 11; x++) { tex.SetPixel(x, 6, solid); tex.SetPixel(x, 4, solid); }

            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect);
        }
    }

    /// <summary>Звуки включения/выключения радар-бустера — берём прямо из игры.</summary>
    internal static class RadarSfx
    {
        private static bool _searched;
        private static AudioClip _on, _off;

        private static void Ensure()
        {
            if (_searched) return;
            _searched = true;
            try
            {
                // предмет может лежать не в сцене, а в общем списке предметов
                var boosterInScene = Object.FindObjectOfType<RadarBoosterItem>();
                if (boosterInScene != null)
                {
                    _on = boosterInScene.turnOnSFX;
                    _off = boosterInScene.turnOffSFX;
                }
                if (_on == null)
                {
                    var sor = StartOfRound.Instance;
                    if (sor != null && sor.allItemsList != null && sor.allItemsList.itemsList != null)
                    {
                        foreach (var it in sor.allItemsList.itemsList)
                        {
                            if (it == null || it.spawnPrefab == null) continue;
                            var rb = it.spawnPrefab.GetComponent<RadarBoosterItem>();
                            if (rb == null) continue;
                            _on = rb.turnOnSFX;
                            _off = rb.turnOffSFX;
                            break;
                        }
                    }
                }
                Plugin.Log?.LogInfo($"[notify] звуки радар-бустера: вкл={(_on != null ? "OK" : "нет")}, " +
                                    $"выкл={(_off != null ? "OK" : "нет")}");
            }
            catch { }
        }

        private static void Play(AudioClip c)
        {
            try
            {
                if (c == null) return;
                var hud = HUDManager.Instance;
                if (hud != null && hud.UIAudio != null) hud.UIAudio.PlayOneShot(c, 0.7f);
            }
            catch { }
        }

        public static void PlayOn() { Ensure(); Play(_on); }
        public static void PlayOff() { Ensure(); Play(_off); }

        public static void Forget() { _searched = false; _on = null; _off = null; }
    }
}
