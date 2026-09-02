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
        private const float W = 330f, H = 360f;
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
            var water = OverlayStyle.WithA(new Color(0.35f, 0.72f, 1f), 0.55f);

            // ---------- поверхность ----------
            Line(0f, Ground, W, Ground, 2.5f, frame, "Ground");

            // корабль справа сверху
            Box(244f, 20f, 76f, 50f, 2f, frame);
            Fill(246f, 22f, 72f, 46f, scan);
            Line(258f, 70f, 258f, 82f, 2f, dim);          // опоры
            Line(306f, 70f, 306f, 82f, 2f, dim);

            // вход в комплекс
            Box(150f, 68f, 34f, 28f, 2f, frame);
            Fill(152f, 70f, 30f, 24f, scan);
            Line(160f, 80f, 174f, 80f, 2f, frame);

            // ---------- зал с лифтом ----------
            const float ix = 128f, iy = 124f, iw = 168f, ih = 74f;
            Box(ix, iy, iw, ih, 2.5f, frame);
            Fill(ix + 2f, iy + 2f, iw - 4f, ih - 4f, scan);

            // шахта лифта от входа вниз в зал
            Line(158f, 96f, 158f, iy, 2f, dim);
            Line(178f, 96f, 178f, iy, 2f, dim);
            Box(156f, iy + 6f, 24f, 26f, 2f, frame);       // кабина
            for (int i = 0; i < 3; i++)
                Line(156f + i * 8f, iy + 32f, 164f + i * 8f, iy + 6f, 1f, dim);

            // ---------- лампы в зале ----------
            var lampOn = new Color(1f, 0.85f, 0.15f, 1f);
            for (int i = 0; i < 3; i++)
                _lampImgs.Add(Line(ix + 46f + i * 42f, iy + 9f, ix + 76f + i * 42f, iy + 9f, 4.5f, lampOn, "Lamp"));

            // ---------- пещеры: кишка вниз из НИЗА зала ----------
            // Идут не сбоку, а прямо из-под комплекса и спускаются вниз, изредка
            // расширяясь в затопленные карманы.
            var spine = new[]
            {
                new Vector2(196f, ih + iy),  new Vector2(186f, 214f), new Vector2(206f, 236f),
                new Vector2(178f, 258f),     new Vector2(120f, 268f), new Vector2(84f, 292f),
                new Vector2(112f, 318f),     new Vector2(170f, 326f), new Vector2(214f, 348f),
            };
            var cave = OverlayStyle.WithA(S.Danger, 0.8f);
            for (int i = 0; i < spine.Length - 1; i++)
            {
                var a = spine[i]; var b = spine[i + 1];
                var n = new Vector2(-(b.y - a.y), b.x - a.x).normalized;
                float wid = 15f + 7f * Mathf.Sin(i * 1.7f);       // «кишка» дышит по ширине
                Line(a.x + n.x * wid, a.y + n.y * wid, b.x + n.x * wid, b.y + n.y * wid, 2f, cave);
                Line(a.x - n.x * wid, a.y - n.y * wid, b.x - n.x * wid, b.y - n.y * wid, 2f, cave);
            }
            // дно тупика
            Line(spine[spine.Length - 1].x - 15f, spine[spine.Length - 1].y,
                 spine[spine.Length - 1].x + 15f, spine[spine.Length - 1].y, 2f, cave);

            // затопленные карманы — горизонтальная гладь воды в двух низинах
            foreach (var wpt in new[] { spine[4], spine[6] })
            {
                Fill(wpt.x - 22f, wpt.y - 4f, 44f, 9f, OverlayStyle.WithA(water, 0.22f));
                Line(wpt.x - 22f, wpt.y, wpt.x + 22f, wpt.y, 2f, water, "Water");
                Line(wpt.x - 14f, wpt.y + 5f, wpt.x + 16f, wpt.y + 5f, 1.5f, OverlayStyle.WithA(water, 0.5f), "Water");
            }

            // ---------- места для монстров ----------
            for (int i = 0; i < 10; i++)                       // уличные — над линией
            {
                var sl = Slot(18f + i * 30f, Ground - 20f);
                _outSlots.Add(sl); _outDots.Add(Dot(sl));
            }
            var inside = new[]                                // в зале и вдоль пещеры
            {
                new Vector2(ix + 26f, iy + 52f),  new Vector2(ix + 66f, iy + 40f),
                new Vector2(ix + 106f, iy + 52f), new Vector2(ix + 142f, iy + 38f),
                new Vector2(190f, 214f),          new Vector2(196f, 244f),
                new Vector2(140f, 266f),          new Vector2(94f, 296f),
                new Vector2(138f, 320f),          new Vector2(196f, 342f),
            };
            foreach (var v in inside)
            {
                var sl = Slot(v.x, v.y);
                _inSlots.Add(sl); _inDots.Add(Dot(sl));
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

        /// <summary>
        /// Горит ли свет в комплексе. Главный источник — распределительный щит
        /// (BreakerBox.isPowerOn): именно его рубильники игрок и щёлкает. Если щита
        /// на карте нет, смотрим на сами лампы.
        /// </summary>
        private static bool FacilityLightsOn()
        {
            try
            {
                if (Time.unscaledTime >= _breakerNext)
                {
                    _breakerNext = Time.unscaledTime + 1f;
                    _breaker = UnityEngine.Object.FindObjectOfType<BreakerBox>();
                }
                if (_breaker != null) return _breaker.isPowerOn;

                var rm = RoundManager.Instance;
                if (rm == null || rm.allPoweredLights == null || rm.allPoweredLights.Count == 0) return true;
                foreach (var l in rm.allPoweredLights)
                    if (l != null && l.enabled) return true;
                return false;
            }
            catch { return true; }
        }

        private static BreakerBox _breaker;
        private static float _breakerNext;

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
        /// Погода. По умолчанию рисуется СХЕМАТИЧНО — узнаваемым значком в левом
        /// верхнем углу: затмение кольцом с короной, дождь тучей со штрихами, гроза
        /// молнией, потоп волнами, туман полосами, пыль косыми штрихами. Так её видно
        /// всегда. Анимационный режим этого не давал: затмение им показать было нечем,
        /// поэтому его и не было видно.
        /// </summary>
        private void UpdateWeather(BridgePayload p, float dt)
        {
            string w = (p.weatherFull ?? "").ToLowerInvariant();
            string kind =
                (w.Contains("eclips") || w.Contains("затмен")) ? "eclipse" :
                (w.Contains("flood") || w.Contains("потоп")) ? "flood" :
                (w.Contains("storm") || w.Contains("гроз")) ? "storm" :
                (w.Contains("rain") || w.Contains("дожд")) ? "rain" :
                (w.Contains("fog") || w.Contains("туман")) ? "fog" :
                (w.Contains("dust") || w.Contains("пыл")) ? "dust" : "";

            bool schematic = (ConfigSettings.MapWeatherMode.Value ?? "Schematic")
                             .Trim().ToLowerInvariant() != "effects";

            if (kind != _wxKind || schematic != _wxSchematic)
            {
                _wxKind = kind;
                _wxSchematic = schematic;
                foreach (var b in _weatherBits) if (b != null) Destroy(b.gameObject);
                _weatherBits.Clear();
                if (kind.Length > 0)
                {
                    if (schematic) DrawWeatherGlyph(kind);
                    else BuildWeatherFx(kind);
                }
            }

            if (!schematic) AnimateWeatherFx();
        }

        private string _wxKind = "?";
        private bool _wxSchematic = true;

        /// <summary>Значок погоды — теми же линиями, что и вся схема.</summary>
        private void DrawWeatherGlyph(string kind)
        {
            const float gx = 22f, gy = 22f;
            var c = S.Frame;
            var soft = OverlayStyle.WithA(S.Frame, 0.55f);

            void L(float x1, float y1, float x2, float y2, float th, Color col)
                => _weatherBits.Add(Line(gx + x1, gy + y1, gx + x2, gy + y2, th, col, "Wx"));

            switch (kind)
            {
                case "eclipse":
                    for (int i = 0; i < 12; i++)
                    {
                        float a1 = i / 12f * Mathf.PI * 2f, a2 = (i + 1) / 12f * Mathf.PI * 2f;
                        L(14f + Mathf.Cos(a1) * 13f, 14f + Mathf.Sin(a1) * 13f,
                          14f + Mathf.Cos(a2) * 13f, 14f + Mathf.Sin(a2) * 13f, 2f, c);
                        if (i % 3 == 0)
                            L(14f + Mathf.Cos(a1) * 16f, 14f + Mathf.Sin(a1) * 16f,
                              14f + Mathf.Cos(a1) * 22f, 14f + Mathf.Sin(a1) * 22f, 2f, soft);
                    }
                    break;

                case "rain":
                case "storm":
                    L(2f, 14f, 26f, 14f, 2f, c);
                    L(2f, 14f, 6f, 6f, 2f, c);
                    L(6f, 6f, 15f, 3f, 2f, c);
                    L(15f, 3f, 23f, 7f, 2f, c);
                    L(23f, 7f, 26f, 14f, 2f, c);
                    if (kind == "rain")
                        for (int i = 0; i < 4; i++) L(5f + i * 6f, 18f, 2f + i * 6f, 27f, 2f, soft);
                    else
                    {
                        L(15f, 17f, 10f, 25f, 2.5f, c);
                        L(10f, 25f, 16f, 25f, 2.5f, c);
                        L(16f, 25f, 10f, 34f, 2.5f, c);
                    }
                    break;

                case "flood":
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 10f + i * 8f;
                        L(0f, y, 7f, y - 3f, 2f, c);
                        L(7f, y - 3f, 14f, y, 2f, c);
                        L(14f, y, 21f, y - 3f, 2f, c);
                        L(21f, y - 3f, 28f, y, 2f, c);
                    }
                    break;

                case "fog":
                    for (int i = 0; i < 4; i++)
                        L(i % 2 == 0 ? 0f : 5f, 6f + i * 7f, i % 2 == 0 ? 24f : 30f, 6f + i * 7f, 2f, soft);
                    break;

                case "dust":
                    for (int i = 0; i < 4; i++)
                        L(i * 2f, 6f + i * 7f, 22f + i * 2f, 3f + i * 7f, 2f, soft);
                    break;
            }
        }

        // ---- анимационный режим, если игрок предпочёл его ----
        private void BuildWeatherFx(string kind)
        {
            int n = (kind == "rain" || kind == "storm" || kind == "flood") ? 14 : kind == "fog" ? 5 : 0;
            for (int i = 0; i < n; i++)
                _weatherBits.Add(kind == "fog"
                    ? Line(8f, 40f + i * 12f, W - 8f, 40f + i * 12f, 2f, OverlayStyle.WithA(S.FrameDim, 0.3f), "Wx")
                    : Line(0f, 0f, 6f, 14f, 1.5f, OverlayStyle.WithA(S.Frame, 0.5f), "Wx"));
        }

        private void AnimateWeatherFx()
        {
            float t = Time.unscaledTime;
            bool fog = _wxKind == "fog";
            for (int i = 0; i < _weatherBits.Count; i++)
            {
                var b = _weatherBits[i];
                if (b == null) continue;
                if (fog)
                {
                    b.color = OverlayStyle.WithA(S.FrameDim, 0.18f + 0.12f * Mathf.Sin(t * 0.7f + i));
                    continue;
                }
                var rt = (RectTransform)b.transform;
                float y = Mathf.Repeat(t * 220f + i * 37f, Ground);
                rt.anchoredPosition = new Vector2(12f + (i * 53f) % (W - 24f), -y);
                b.color = OverlayStyle.WithA(S.Frame, 0.45f);
            }
        }
    }
}
