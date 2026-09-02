using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Упрощённая схема локации — то, что видно на улице, пока идёшь.
    ///
    /// Задача: не мешать. Поэтому вместо полной панели рисуется маленькая
    /// «инсталляция»: поверхность, вход, шахта лифта, помещения комплекса и пещеры.
    /// Сверху — название луны, снизу — лут внутри/снаружи и ульи. Монстры показаны
    /// точками в своих зонах: над линией поверхности — уличные, ниже — комплексные.
    ///
    /// Всё рисуется линиями (тонкие прямоугольники под углом), фон прозрачный —
    /// это векторная графика в цвет темы, а не картинка. Внутренности помещений
    /// закрыты нашими сканлайн-блоками.
    ///
    /// Лампы жёлтые и привязаны к настоящему свету в комплексе: гаснет свет в игре —
    /// гаснут и они. Показывать это схемой или эффектом свечения выбирает игрок.
    /// </summary>
    internal class FacilityMapWidget : MonoBehaviour
    {
        // холст схемы: рисуем в этих координатах, потом всё масштабируется целиком
        private const float W = 330f, H = 250f;
        private const float Ground = 96f;       // линия поверхности (сверху вниз)

        private OverlayManager _mgr;
        private OverlayStyle S;
        private RectTransform _root;

        private TextMeshProUGUI _moonText, _lootText;
        private RectTransform _art;             // сама схема
        private readonly List<Image> _lampImgs = new List<Image>();
        private readonly List<Image> _weatherBits = new List<Image>();
        private readonly List<RectTransform> _outSlots = new List<RectTransform>();
        private readonly List<RectTransform> _inSlots = new List<RectTransform>();
        private readonly List<Image> _outDots = new List<Image>();
        private readonly List<Image> _inDots = new List<Image>();

        private string _builtFor;               // для какого интерьера собрана схема
        private float _wxMul = 1f;
        private bool _lightsOn = true;
        private float _lightPulse;

        // ================= сборка =================

        public void Init(OverlayManager mgr, RectTransform parent, OverlayStyle style, float panelWidth)
        {
            _mgr = mgr;
            S = style;

            // Схема — ОБЫЧНЫЙ блок панели, а не отдельный слой: так она сама собой
            // получает её положение, наклон, масштаб, качание вслед за камерой,
            // прозрачность и перспективу, а не повторяет всё это своим кодом.
            float inner = panelWidth - 30f;              // минус паддинги layout-группы
            _fit = inner / W;                            // рисуем в своих координатах, растягиваем целиком

            var go = new GameObject("FacilityMap", typeof(RectTransform));
            _root = (RectTransform)go.transform;
            _root.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = inner;
            le.preferredHeight = H * _fit + 52f;         // схема + название сверху + лут снизу
            _le = le;

            _moonText = Text(_root, 26f, new Vector2(0f, 0f), TextAlignmentOptions.Top, S.Text);
            _lootText = Text(_root, 15f, new Vector2(0f, -(H * _fit + 30f)), TextAlignmentOptions.Top, S.TextDim);

            var artGo = new GameObject("Art", typeof(RectTransform));
            _art = (RectTransform)artGo.transform;
            _art.SetParent(_root, false);
            _art.anchorMin = _art.anchorMax = new Vector2(0f, 1f);
            _art.pivot = new Vector2(0f, 1f);
            _art.anchoredPosition = new Vector2(0f, -30f);
            _art.sizeDelta = new Vector2(W, H);
            _art.localScale = new Vector3(_fit, _fit, 1f);   // увеличиваем всё разом

            _root.gameObject.SetActive(false);
        }

        /// <summary>Корень блока — панель вешает на него перспективу.</summary>
        public Transform Root => _root;

        private float _fit = 1f;
        private LayoutElement _le;

        /// <summary>Шрифты берём те же, что у панели, — иначе схема выбивается из неё.</summary>
        public void ApplyFonts(TMP_FontAsset body, TMP_FontAsset big)
        {
            if (_moonText != null && big != null) _moonText.font = big;
            if (_lootText != null && body != null) _lootText.font = body;
        }

        private TextMeshProUGUI Text(RectTransform parent, float size, Vector2 pos,
                                     TextAlignmentOptions align, Color col)
        {
            var go = new GameObject("T", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
            rt.sizeDelta = new Vector2(0f, size * 1.5f);
            rt.anchoredPosition = pos;
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = col;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            return t;
        }

        // ---- примитивы векторной графики ----

        /// <summary>Отрезок: тонкий прямоугольник, повёрнутый по направлению. Чистая линия.</summary>
        private Image Line(float x1, float y1, float x2, float y2, float thick, Color col, string name = "L")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_art, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);

            var d = new Vector2(x2 - x1, -(y2 - y1));
            rt.anchoredPosition = new Vector2(x1, -y1);
            rt.sizeDelta = new Vector2(d.magnitude, thick);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        private void Box(float x, float y, float w, float h, float thick, Color col)
        {
            Line(x, y, x + w, y, thick, col);
            Line(x, y + h, x + w, y + h, thick, col);
            Line(x, y, x, y + h, thick, col);
            Line(x + w, y, x + w, y + h, thick, col);
        }

        /// <summary>Замкнутая ломаная — ею рисуем пещеры.</summary>
        private void Poly(Vector2[] pts, float thick, Color col)
        {
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                Line(a.x, a.y, b.x, b.y, thick, col);
            }
        }

        /// <summary>Заливка сканлайн-блоком — им закрываем внутренности помещений.</summary>
        private void Fill(float x, float y, float w, float h, Color col)
        {
            var go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_art, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            rt.SetAsFirstSibling();              // под линиями
        }

        private RectTransform Slot(float x, float y)
        {
            var go = new GameObject("Slot", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_art, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(11f, 11f);
            return rt;
        }

        private Image Dot(RectTransform slot)
        {
            var go = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(11f, 11f);
            var img = go.GetComponent<Image>();
            img.sprite = NotifyWidget.Folder();   // пока не пришла иконка — «пакет данных»
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        // ================= схема под конкретный интерьер =================

        /// <summary>
        /// Перерисовать схему под текущий интерьер. Пока рисунок один (шахта), но
        /// зоны монстров и лампы уже разнесены по интерьерам — добавить свой рисунок
        /// сводится к новой ветке здесь.
        /// </summary>
        private void Build(string interior)
        {
            for (int i = _art.childCount - 1; i >= 0; i--) Destroy(_art.GetChild(i).gameObject);
            _lampImgs.Clear(); _weatherBits.Clear();
            _outSlots.Clear(); _inSlots.Clear(); _outDots.Clear(); _inDots.Clear();

            var frame = S.Frame;
            var dim = OverlayStyle.WithA(S.FrameDim, 0.55f);
            var scan = OverlayStyle.WithA(S.Frame, 0.10f);   // сканлайн-блок фона

            // ---------- поверхность ----------
            Line(0f, Ground, W, Ground, 2f, frame, "Ground");

            // корабль справа сверху
            Box(232f, 18f, 92f, 62f, 2f, frame);
            Fill(234f, 20f, 88f, 58f, scan);

            // вход в комплекс — коробка на линии
            Box(196f, 66f, 30f, 30f, 2f, frame);
            Fill(198f, 68f, 26f, 26f, scan);
            Line(204f, 78f, 218f, 78f, 2f, frame);   // дверь

            // ---------- подземелье ----------
            const float ix = 118f, iy = 132f, iw = 190f, ih = 86f;
            Box(ix, iy, iw, ih, 2f, frame);
            Fill(ix + 2f, iy + 2f, iw - 4f, ih - 4f, scan);

            // шахта лифта от корабля вниз в помещение
            Line(262f, 24f, 262f, iy + 44f, 2f, frame);
            Line(284f, 24f, 284f, iy + 44f, 2f, frame);
            Box(258f, iy + 40f, 30f, 30f, 2f, frame);          // кабина
            for (int i = 0; i < 4; i++)                        // штриховка кабины
                Line(258f + i * 8f, iy + 70f, 268f + i * 8f, iy + 40f, 1f, dim);

            // пол помещения — штриховка
            for (int i = 0; i < 7; i++)
                Line(ix + 12f + i * 22f, iy + ih, ix + 30f + i * 22f, iy + ih - 16f, 1f, dim);

            // ---------- лампы ----------
            // жёлтые всегда: это свет, а не элемент темы
            var lampOn = new Color(1f, 0.85f, 0.15f, 1f);
            for (int i = 0; i < 3; i++)
            {
                var l = Line(ix + 22f + i * 58f, iy + 7f, ix + 62f + i * 58f, iy + 7f, 4f, lampOn, "Lamp");
                _lampImgs.Add(l);
            }

            // ---------- пещеры ----------
            var cave = OverlayStyle.WithA(S.Danger, 0.75f);
            Poly(new[]
            {
                new Vector2(16f, 176f), new Vector2(52f, 150f), new Vector2(96f, 158f),
                new Vector2(118f, 150f), new Vector2(112f, 178f), new Vector2(150f, 186f),
                new Vector2(196f, 176f), new Vector2(238f, 190f), new Vector2(232f, 222f),
                new Vector2(178f, 234f), new Vector2(120f, 226f), new Vector2(64f, 238f),
                new Vector2(22f, 220f), new Vector2(8f, 198f),
            }, 2f, cave);

            // ---------- места для монстров ----------
            // уличные — над линией поверхности, ровным рядом
            for (int i = 0; i < 10; i++)
            {
                var s = Slot(16f + i * 19f, Ground - 16f);
                _outSlots.Add(s); _outDots.Add(Dot(s));
            }
            // комплексные — часть в помещении, часть в пещерах
            var inside = new[]
            {
                new Vector2(140f, 168f), new Vector2(170f, 156f), new Vector2(200f, 168f),
                new Vector2(230f, 156f), new Vector2(96f, 196f),  new Vector2(140f, 208f),
                new Vector2(184f, 200f), new Vector2(56f, 186f),  new Vector2(212f, 212f),
                new Vector2(34f, 206f),
            };
            foreach (var v in inside)
            {
                var s = Slot(v.x, v.y);
                _inSlots.Add(s); _inDots.Add(Dot(s));
            }

            _builtFor = interior ?? "";

            // Перспективу вешаем ЗДЕСЬ, а не при создании виджета: линии и метки
            // рождаются только сейчас, и на пустой блок вешать было нечего.
            try { _mgr?.AddPerspectiveToTree(_art); } catch { }
        }

        // ================= жизнь =================

        public void Refresh(BridgePayload p, bool wantVisible)
        {
            if (_root == null) return;

            if (_root.gameObject.activeSelf != wantVisible) _root.gameObject.SetActive(wantVisible);
            if (!wantVisible || p == null) return;

            float dt = Time.unscaledDeltaTime;
            string interior = p.interiorType ?? "";
            if (_builtFor == null || _builtFor != interior) Build(interior);

            _moonText.text = (p.moonName ?? "- -").ToUpperInvariant();
            _moonText.color = S.Text;
            _lootText.text = $"{Localization.T("in")} {p.itemsInside}   " +
                             $"{Localization.T("out")} {p.itemsOutside}   " +
                             $"{Localization.T("hives")} {p.beehiveCount}";

            UpdateLights(dt);
            UpdateDots(p);
            UpdateWeather(p, dt);
        }

        /// <summary>Лампы схемы повторяют настоящий свет в комплексе.</summary>
        private void UpdateLights(float dt)
        {
            _lightsOn = FacilityLightsOn();
            bool effects = (ConfigSettings.MapLightMode.Value ?? "Effects")
                           .Trim().ToLowerInvariant() != "schematic";

            _lightPulse += dt * (_lightsOn ? 1.6f : 0f);
            var on = new Color(1f, 0.85f, 0.15f, 1f);
            var off = new Color(0.35f, 0.32f, 0.18f, 0.5f);

            foreach (var l in _lampImgs)
            {
                if (l == null) continue;
                if (!_lightsOn) { l.color = off; continue; }
                if (!effects) { l.color = on; continue; }
                // «эффект»: мягкое дыхание, как у лампы дневного света
                float k = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(_lightPulse));
                l.color = new Color(on.r, on.g * k, on.b * k, k);
            }
        }

        private static bool FacilityLightsOn()
        {
            try
            {
                var rm = RoundManager.Instance;
                if (rm == null || rm.allPoweredLights == null || rm.allPoweredLights.Count == 0) return true;
                foreach (var l in rm.allPoweredLights)
                    if (l != null && l.enabled) return true;   // хоть одна горит — свет есть
                return false;
            }
            catch { return true; }
        }

        /// <summary>Точки монстров по зонам: сверху уличные, ниже комплексные.</summary>
        private void UpdateDots(BridgePayload p)
        {
            PlaceDots(_outDots, p.monstersOutside);
            PlaceDots(_inDots, p.monstersInside);
        }

        private void PlaceDots(List<Image> dots, string[] names)
        {
            int n = names != null ? names.Length : 0;
            for (int i = 0; i < dots.Count; i++)
            {
                var img = dots[i];
                if (img == null) continue;
                if (i >= n)
                {
                    var c0 = img.color; c0.a = Mathf.MoveTowards(c0.a, 0f, Time.unscaledDeltaTime * 4f);
                    img.color = c0;
                    continue;
                }

                string raw = names[i] ?? "";
                var spr = MobIconFor(raw);
                if (spr != null && img.sprite != spr) img.sprite = spr;

                var c = S.Frame;
                c.a = Mathf.MoveTowards(img.color.a, 1f, Time.unscaledDeltaTime * 4f);
                img.color = c;
            }
        }

        private static Sprite MobIconFor(string raw)
        {
            try
            {
                string key = MobRailWidget.IconKeyPublic(raw);
                return string.IsNullOrEmpty(key) ? NotifyWidget.Folder() : SpriteBank.Get(key);
            }
            catch { return NotifyWidget.Folder(); }
        }

        /// <summary>
        /// Погода — стилизованными линиями поверх схемы, в том же ключе, что и рисунок:
        /// дождь и потоп косыми штрихами, туман горизонтальными полосами, затмение
        /// приглушает всё, гроза изредка бьёт вспышкой.
        /// </summary>
        private void UpdateWeather(BridgePayload p, float dt)
        {
            string w = (p.weatherFull ?? "").ToLowerInvariant();
            bool rain = w.Contains("rain") || w.Contains("дожд");
            bool flood = w.Contains("flood") || w.Contains("потоп");
            bool fog = w.Contains("fog") || w.Contains("туман");
            bool storm = w.Contains("storm") || w.Contains("гроз");
            bool eclipse = w.Contains("eclips") || w.Contains("затмен");

            int want = (rain || flood) ? 14 : fog ? 5 : 0;
            while (_weatherBits.Count > want)
            {
                int last = _weatherBits.Count - 1;
                if (_weatherBits[last] != null) Destroy(_weatherBits[last].gameObject);
                _weatherBits.RemoveAt(last);
            }
            while (_weatherBits.Count < want)
            {
                int i = _weatherBits.Count;
                Image bit = fog
                    ? Line(8f, 40f + i * 12f, W - 8f, 40f + i * 12f, 2f, OverlayStyle.WithA(S.FrameDim, 0.3f), "Wx")
                    : Line(0f, 0f, 6f, 14f, 1.5f, OverlayStyle.WithA(S.Frame, 0.5f), "Wx");
                _weatherBits.Add(bit);
            }

            float t = Time.unscaledTime;
            for (int i = 0; i < _weatherBits.Count; i++)
            {
                var b = _weatherBits[i];
                if (b == null) continue;
                if (fog)
                {
                    float a = 0.18f + 0.12f * Mathf.Sin(t * 0.7f + i);
                    b.color = OverlayStyle.WithA(S.FrameDim, a);
                    continue;
                }
                // капли падают сверху вниз и уходят под линию поверхности
                var rt = (RectTransform)b.transform;
                float span = flood ? H : Ground;
                float y = Mathf.Repeat(t * (flood ? 150f : 220f) + i * 37f, span);
                float x = 12f + (i * 53f) % (W - 24f);
                rt.anchoredPosition = new Vector2(x, -y);
                b.color = OverlayStyle.WithA(S.Frame, flood ? 0.55f : 0.45f);
            }

            // Затмение приглушает схему, гроза изредка подсвечивает. Трогаем ТОЛЬКО
            // свои линии: общей прозрачностью владеет панель, и лезть в неё нельзя.
            float mul = eclipse ? 0.62f : 1f;
            if (storm && Mathf.PerlinNoise(t * 1.7f, 4.2f) > 0.86f) mul *= 1.6f;
            if (Mathf.Abs(mul - _wxMul) > 0.01f)
            {
                _wxMul = mul;
                foreach (var img in _art.GetComponentsInChildren<Image>(true))
                {
                    if (img == null) continue;
                    var c = img.color;
                    img.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * mul));
                }
            }
        }
    }
}
