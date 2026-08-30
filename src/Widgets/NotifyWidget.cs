using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Режим уведомлений: оверлей не висит перед глазами весь забег, а спит невидимым
    /// и разгорается, только когда что-то изменилось.
    ///
    /// Что происходит:
    ///  - обнаружен новый монстр — в рейке на ЕГО месте появляется «папка» цвета
    ///    стиля, мельтешит как 8-битная картинка и сменяется иконкой монстра
    ///    (сама папка живёт в MobRailWidget, здесь только её картинка);
    ///  - изменилась цифра — она коротко мерцает;
    ///  - через NotifyHoldSeconds тишины панель снова гаснет.
    ///
    /// На корабле режим не действует: там панель работает как обычно.
    /// Включение и выключение озвучены родными звуками радар-бустера.
    /// </summary>
    internal class NotifyWidget : MonoBehaviour
    {
        private const float FadeIn = 0.25f;
        private const float FadeOut = 0.7f;

        private OverlayStyle _style;

        private float _wake;              // 0 спит … 1 бодрствует
        private float _holdUntil = -999f;
        private bool _awakeSfxPlayed;
        private bool _forceOn;            // на корабле режим не действует

        /// <summary>Множитель прозрачности всей панели.</summary>
        public float Wake => _forceOn ? 1f : _wake;

        public void Init(OverlayStyle style) => _style = style;
        public void SetStyle(OverlayStyle style) => _style = style;

        /// <summary>На корабле панель всегда живая — обычная прозрачность из конфига.</summary>
        public void SetAlwaysOn(bool on)
        {
            if (_forceOn == on) return;
            _forceOn = on;
            if (on)
            {
                // на корабль зашли — гасить нечего, звук выключения не нужен
                _wake = 1f;
                _awakeSfxPlayed = true;
                _holdUntil = -999f;
            }
        }

        private class Blink { public float Age; public Color Base; }
        private readonly Dictionary<TextMeshProUGUI, Blink> _flicker =
            new Dictionary<TextMeshProUGUI, Blink>();

        // ======================= новости =======================

        /// <summary>Разбудить панель (что-то изменилось).</summary>
        public void WakeUp()
        {
            _holdUntil = Time.unscaledTime + Mathf.Max(1f, ConfigSettings.NotifyHoldSeconds.Value);
            if (!_awakeSfxPlayed)
            {
                _awakeSfxPlayed = true;
                RadarSfx.PlayOn();
            }
        }

        /// <summary>Изменилась цифра: будим панель и мерцаем этим текстом.</summary>
        public void Ping(TextMeshProUGUI flickerTarget)
        {
            WakeUp();
            Flick(flickerTarget);
        }

        /// <summary>Запустить мерцание текста.</summary>
        public void Flick(TextMeshProUGUI t)
        {
            if (t == null) return;
            // Базовый цвет запоминаем ОДИН раз. Панель перекрашивает текст только при
            // Refresh(), то есть примерно раз в секунду; если брать за базу текущий цвет
            // каждый кадр, подкраска накапливается и текст навсегда уезжает в акцент —
            // именно так названия лун становились оранжевыми вместо белых.
            if (_flicker.TryGetValue(t, out var b)) b.Age = 0f;
            else _flicker[t] = new Blink { Age = 0f, Base = t.color };
        }

        // ======================= жизнь =======================

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_forceOn) { _wake = 1f; return; }

            bool wantAwake = Time.unscaledTime < _holdUntil;
            // разгораемся быстро, гаснем медленно — так спокойнее для глаза
            _wake = Mathf.MoveTowards(_wake, wantAwake ? 1f : 0f, dt / (wantAwake ? FadeIn : FadeOut));

            if (!wantAwake && _awakeSfxPlayed && _wake <= 0.001f)
            {
                _awakeSfxPlayed = false;
                RadarSfx.PlayOff();
            }
        }

        /// <summary>
        /// Мерцание изменившихся цифр. Зовётся ПОСЛЕ отрисовки панели: та проставляет
        /// цвета заново, поэтому иначе мерцание было бы затёрто.
        /// </summary>
        public void ApplyFlicker()
        {
            if (_flicker.Count == 0) return;
            const float life = 1.1f;
            List<TextMeshProUGUI> done = null;
            var keys = new List<TextMeshProUGUI>(_flicker.Keys);
            foreach (var t in keys)
            {
                var b = _flicker[t];
                b.Age += Time.unscaledDeltaTime;

                if (t == null || b.Age > life)
                {
                    if (t != null) t.color = b.Base;      // вернуть как было
                    (done ?? (done = new List<TextMeshProUGUI>())).Add(t);
                    continue;
                }

                // несколько быстрых вспышек в акцент, затухая к концу
                float k = Mathf.Abs(Mathf.Sin(b.Age * 16f)) * (1f - b.Age / life);
                var hot = _style != null ? _style.Accent : Color.white;
                t.color = Color.Lerp(b.Base, new Color(hot.r, hot.g, hot.b, b.Base.a), k);
            }
            if (done != null) foreach (var t in done) _flicker.Remove(t);
        }

        public void ResetAll()
        {
            _holdUntil = -999f;
            _wake = 0f;
            _forceOn = false;
            _awakeSfxPlayed = false;
            foreach (var kv in _flicker) if (kv.Key != null) kv.Key.color = kv.Value.Base;
            _flicker.Clear();
        }

        // ======================= папка =======================

        private static Sprite _folder;

        /// <summary>
        /// Иконка папки, нарисованная кодом: 16×13 с точечной фильтрацией — на экране
        /// читается как настоящая 8-битная картинка и красится в цвет стиля.
        /// Используется рейкой монстров, пока не пришла настоящая иконка.
        /// </summary>
        public static Sprite Folder()
        {
            if (_folder != null) return _folder;

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

            // корпус папки
            for (int y = 0; y <= 9; y++)
                for (int x = 1; x <= 14; x++)
                    tex.SetPixel(x, y, (y == 0 || y == 9 || x == 1 || x == 14) ? solid : dim);
            // «язычок» сверху слева
            for (int y = 10; y <= 11; y++)
                for (int x = 1; x <= 7; x++)
                    tex.SetPixel(x, y, (y == 11 || x == 1 || x == 7) ? solid : dim);
            // две строки «данных» внутри — чтобы читалось как документ
            for (int x = 4; x <= 11; x++) { tex.SetPixel(x, 6, solid); tex.SetPixel(x, 4, solid); }

            tex.Apply(false, false);
            _folder = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0,
                                    SpriteMeshType.FullRect);
            return _folder;
        }

        /// <summary>Дрожание «плохого сигнала» для папки.</summary>
        public static Vector2 PixelJitter(float seed, float amp)
        {
            float t = Time.unscaledTime * 18f;
            var v = new Vector2(
                (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * amp,
                (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * amp);
            // по сетке в 2 пикселя — так это читается как 8-битная картинка
            return new Vector2(Mathf.Round(v.x / 2f) * 2f, Mathf.Round(v.y / 2f) * 2f);
        }

        /// <summary>Провалы яркости, как у плохого сигнала.</summary>
        public static float SignalDropout(float seed) =>
            Mathf.PerlinNoise(Time.unscaledTime * 22f, seed) > 0.78f ? 0.35f : 1f;
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
                var boosterInScene = Object.FindObjectOfType<RadarBoosterItem>();
                if (boosterInScene != null)
                {
                    _on = boosterInScene.turnOnSFX;
                    _off = boosterInScene.turnOffSFX;
                }
                if (_on == null)
                {
                    // бустера в сцене может не быть — берём с префаба предмета
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
