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
        private const float W = 330f, H = 400f;
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
            rt.sizeDelta = new Vector2(30f, 30f);
            return rt;
        }

        private Image Dot(RectTransform slot)
        {
            var go = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 30f);
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
            _elevCar = null;

            var frame = S.Frame;
            var dim = OverlayStyle.WithA(S.FrameDim, 0.6f);
            var scan = OverlayStyle.WithA(S.Frame, 0.10f);
            var water = new Color(0.35f, 0.72f, 1f, 0.6f);
            var cave = OverlayStyle.WithA(S.Danger, 0.85f);

            // ---------- земля: кривая линия с резкими углами ----------
            var ground = new[]
            {
                new Vector2(0f, 100f), new Vector2(38f, 94f), new Vector2(74f, 103f),
                new Vector2(112f, 92f), new Vector2(150f, 99f), new Vector2(188f, 90f),
                new Vector2(196f, 96f), new Vector2(W, 96f),
            };
            for (int i = 0; i < ground.Length - 1; i++)
                Line(ground[i].x, ground[i].y, ground[i + 1].x, ground[i + 1].y, 2.5f, frame, "Ground");

            // ---------- большое здание справа, на земле ----------
            const float bx = 200f, by = 28f, bw = 118f, bh = 68f;
            Box(bx, by, bw, bh, 2.5f, frame);
            Fill(bx + 2f, by + 2f, bw - 4f, bh - 4f, scan);

            // вагонетка внутри здания
            const float cx = bx + 16f, cy = by + 46f;
            Line(cx, cy, cx + 30f, cy, 2f, frame);                 // дно кузова
            Line(cx, cy, cx + 4f, cy - 14f, 2f, frame);             // борта с развалом
            Line(cx + 30f, cy, cx + 26f, cy - 14f, 2f, frame);
            Line(cx + 4f, cy - 14f, cx + 26f, cy - 14f, 2f, frame);
            for (int i = 0; i < 2; i++)                             // колёса
            {
                float wx = cx + 8f + i * 14f;
                Line(wx - 4f, cy + 5f, wx + 4f, cy + 5f, 2f, dim);
                Line(wx - 4f, cy + 2f, wx - 4f, cy + 5f, 1.5f, dim);
                Line(wx + 4f, cy + 2f, wx + 4f, cy + 5f, 1.5f, dim);
            }

            // дыра в полу здания — разрыв нижней грани
            const float hx = bx + 74f, hw = 26f;
            Line(bx, by + bh, hx, by + bh, 2.5f, frame);            // пол слева от дыры
            Line(hx + hw, by + bh, bx + bw, by + bh, 2.5f, frame);  // и справа

            // ---------- вертикальный коридор из дыры вниз ----------
            const float roomY = 168f;
            Line(hx, by + bh, hx, roomY, 2f, dim, "Shaft");
            Line(hx + hw, by + bh, hx + hw, roomY, 2f, dim, "Shaft");

            // ---------- главная комната комплекса ----------
            const float rx = 34f, rw = 272f, rh = 96f;
            Box(rx, roomY, rw, rh, 2.5f, frame);
            Fill(rx + 2f, roomY + 2f, rw - 4f, rh - 4f, scan);

            // лифт на тросах: приехал из коридора
            _elevTop = roomY + 10f;
            _elevBottom = roomY + rh - 30f;
            Line(hx + 5f, roomY, hx + 5f, roomY + 8f, 1.5f, dim, "Cable");
            Line(hx + hw - 5f, roomY, hx + hw - 5f, roomY + 8f, 1.5f, dim, "Cable");
            _elevCable1 = Line(hx + 5f, roomY, hx + 5f, _elevTop, 1.5f, dim, "Cable");
            _elevCable2 = Line(hx + hw - 5f, roomY, hx + hw - 5f, _elevTop, 1.5f, dim, "Cable");
            _elevCar = MakeCar(hx - 2f, _elevTop, hw + 4f, 24f, frame, scan);

            // рельсы по полу комнаты
            float railY = roomY + rh - 12f;
            Line(rx + 10f, railY, rx + rw - 10f, railY, 2f, dim, "Rail");
            Line(rx + 10f, railY + 6f, rx + rw - 10f, railY + 6f, 2f, dim, "Rail");
            for (float x = rx + 18f; x < rx + rw - 14f; x += 18f)
                Line(x, railY - 2f, x, railY + 8f, 1.5f, OverlayStyle.WithA(S.FrameDim, 0.4f), "Rail");

            // люминесцентные лампы: короткая ножка от ПОТОЛКА и трубка, больше ничего не касается
            var lampOn = new Color(1f, 0.85f, 0.15f, 1f);
            for (int i = 0; i < 3; i++)
            {
                float lx = rx + 44f + i * 74f;
                Line(lx + 16f, roomY + 2f, lx + 16f, roomY + 12f, 1.5f, dim, "LampStem");
                _lampImgs.Add(Line(lx, roomY + 13f, lx + 32f, roomY + 13f, 5f, lampOn, "Lamp"));
            }

            // ---------- шахта: из ЛЕВОЙ стены вниз и вправо, к ПРАВОЙ стене ----------
            var spine = new[]
            {
                new Vector2(rx, roomY + 62f),      // выход из левой стены комнаты
                new Vector2(58f, 300f), new Vector2(104f, 286f), new Vector2(96f, 322f),
                new Vector2(150f, 336f),           // сюда придётся большой зал
                new Vector2(210f, 328f), new Vector2(196f, 360f), new Vector2(252f, 372f),
                new Vector2(rx + rw, 366f),        // упирается в правую стену
            };
            for (int i = 0; i < spine.Length - 1; i++)
            {
                var a = spine[i]; var b = spine[i + 1];
                var n = new Vector2(-(b.y - a.y), b.x - a.x).normalized;
                float wid = (i == 4) ? 30f : 11f + 4f * Mathf.Sin(i * 1.9f);   // 4-й участок — большой зал
                Line(a.x + n.x * wid, a.y + n.y * wid, b.x + n.x * wid, b.y + n.y * wid, 2f, cave);
                Line(a.x - n.x * wid, a.y - n.y * wid, b.x - n.x * wid, b.y - n.y * wid, 2f, cave);
            }
            Line(spine[spine.Length - 1].x, spine[spine.Length - 1].y - 12f,
                 spine[spine.Length - 1].x, spine[spine.Length - 1].y + 12f, 2f, cave);

            // затопленная низина в большом зале
            var low = spine[4];
            Fill(low.x - 30f, low.y - 2f, 60f, 12f, OverlayStyle.WithA(water, 0.2f));
            Line(low.x - 30f, low.y + 2f, low.x + 30f, low.y + 2f, 2f, water, "Water");
            Line(low.x - 20f, low.y + 8f, low.x + 22f, low.y + 8f, 1.5f, OverlayStyle.WithA(water, 0.5f), "Water");

            // ---------- места для монстров ----------
            for (int i = 0; i < 8; i++)                       // уличные — над землёй, слева
            {
                var sl = Slot(20f + i * 23f, 62f);
                _outSlots.Add(sl); _outDots.Add(Dot(sl));
            }
            var inside = new[]
            {
                new Vector2(rx + 34f, roomY + 46f),  new Vector2(rx + 90f, roomY + 62f),
                new Vector2(rx + 150f, roomY + 46f), new Vector2(rx + 214f, roomY + 62f),
                new Vector2(66f, 296f),              new Vector2(102f, 306f),
                new Vector2(150f, 332f),             new Vector2(206f, 336f),
                new Vector2(232f, 366f),             new Vector2(286f, 362f),
            };
            foreach (var v in inside)
            {
                var sl = Slot(v.x, v.y);
                _inSlots.Add(sl); _inDots.Add(Dot(sl));
            }

            _builtFor = interior ?? "";
            try { _mgr?.AddPerspectiveToTree(_art); } catch { }
        }

        // ---- лифт ----
        private RectTransform _elevCar;
        private Image _elevCable1, _elevCable2;
        private float _elevTop, _elevBottom, _elevPos;

        /// <summary>Кабина лифта: рамка с диагональной штриховкой.</summary>
        private RectTransform MakeCar(float x, float y, float w, float h, Color col, Color scan)
        {
            var go = new GameObject("Elevator", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_art, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);

            var save = _art;
            _art = rt;                       // рисуем содержимое кабины в её системе
            Fill(0f, 0f, w, h, scan);
            Line(0f, 0f, w, 0f, 2f, col); Line(0f, h, w, h, 2f, col);
            Line(0f, 0f, 0f, h, 2f, col);   Line(w, 0f, w, h, 2f, col);
            for (int i = 0; i < 3; i++) Line(4f + i * 9f, h - 3f, 12f + i * 9f, 3f, 1.5f, OverlayStyle.WithA(col, 0.5f));
            _art = save;
            return rt;
        }

        /// <summary>Кабина едет вместе с настоящей: вниз, когда та внизу.</summary>
        private void UpdateElevator(float dt)
        {
            if (_elevCar == null) return;
            bool atBottom = false;
            try
            {
                if (Time.unscaledTime >= _elevNext)
                {
                    _elevNext = Time.unscaledTime + 1f;
                    _elevCtl = UnityEngine.Object.FindObjectOfType<MineshaftElevatorController>();
                }
                if (_elevCtl != null) atBottom = _elevCtl.elevatorIsAtBottom;
            }
            catch { }

            float target = atBottom ? 1f : 0f;
            _elevPos = Mathf.MoveTowards(_elevPos, target, dt / 3f);   // ход примерно как у настоящего
            float y = Mathf.Lerp(_elevTop, _elevBottom, _elevPos);
            _elevCar.anchoredPosition = new Vector2(_elevCar.anchoredPosition.x, -y);

            // тросы тянутся за кабиной
            foreach (var c in new[] { _elevCable1, _elevCable2 })
            {
                if (c == null) continue;
                var rt = (RectTransform)c.transform;
                float top = -rt.anchoredPosition.y;
                rt.sizeDelta = new Vector2(Mathf.Max(1f, y - top), rt.sizeDelta.y);
            }
        }

        private static MineshaftElevatorController _elevCtl;
        private static float _elevNext;

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

            UpdateElevator(dt);
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

        /// <summary>Метки монстров по зонам: сверху уличные, ниже комплексные.</summary>
        private void UpdateDots(BridgePayload p)
        {
            PlaceDots(_outDots, p.monstersOutside);
            PlaceDots(_inDots, p.monstersInside);
        }

        /// <summary>
        /// Метки живут по тем же правилам, что и иконки в рейке: скрываются без скана,
        /// тают по дальности, покачиваются и нервно дрожат вблизи. Иначе схема
        /// показывала бы то, чего основной оверлей не показывает.
        /// </summary>
        private void PlaceDots(List<Image> dots, string[] names)
        {
            // отбираем то же, что показала бы рейка
            var shown = new List<string>();
            if (names != null)
                foreach (var raw in names)
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (Gate.RequireScan &&
                        raw.IndexOf("+Scanned", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    shown.Add(raw);
                }

            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;

            for (int i = 0; i < dots.Count; i++)
            {
                var img = dots[i];
                if (img == null) continue;
                var rt = (RectTransform)img.transform;

                if (i >= shown.Count)
                {
                    var c0 = img.color;
                    c0.a = Mathf.MoveTowards(c0.a, 0f, dt * 4f);
                    img.color = c0;
                    if (c0.a <= 0.01f) rt.localScale = Vector3.zero;
                    continue;
                }

                string raw = shown[i];
                var spr = MobIconFor(raw);
                if (spr != null && img.sprite != spr) img.sprite = spr;

                float dist = DistOf(raw);
                float near = dist >= 0f ? Mathf.InverseLerp(40f, 4f, dist) : 0f;

                // покачивание + дрожь по близости — как у иконок в рейке
                float phase = i * 1.7f;
                float amp = 3f + 9f * near * near * near;
                float speed = 2f + 14f * near * near;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((t + phase) * speed) * amp);
                var jit = near > 0.01f ? NotifyWidget.PixelJitter(phase, 2.2f * near * near) : Vector2.zero;
                rt.anchoredPosition = jit;
                rt.localScale = Vector3.one;

                // прозрачность по дальности — тот же закон, что в рейке
                float a = 1f;
                if (ConfigSettings.ProximityFade.Value && dist >= 0f)
                    a = Mathf.Lerp(0.28f, 1f, Mathf.InverseLerp(34f, 6f, dist));

                bool tinted = MobRailWidget.TintedIconStylePublic();
                var col = tinted ? S.Frame : Color.white;
                col.a = Mathf.MoveTowards(img.color.a, a, dt * 4f);
                img.color = col;
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

        /// <summary>Дистанция из строки монстра ("@42"), или -1.</summary>
        private static float DistOf(string raw)
        {
            try
            {
                var m = System.Text.RegularExpressions.Regex.Match(raw, @"@(\d+)");
                return m.Success ? float.Parse(m.Groups[1].Value) : -1f;
            }
            catch { return -1f; }
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
