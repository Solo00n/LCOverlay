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
        private readonly List<Vector4> _inPaths = new List<Vector4>();   // маршруты хождения
        private readonly List<Vector4> _outPaths = new List<Vector4>();  // и снаружи тоже
        private readonly List<float> _nearSmooth = new List<float>();    // сглаженная близость
        private readonly List<float> _face = new List<float>();          // куда смотрит иконка
        private readonly List<float> _faceHold = new List<float>();      // и её удержанное значение
        private readonly List<Vector2> _sep = new List<Vector2>();       // сглаженный расход
        private float _arrive = 1f;   // 0 метки ещё летят с краёв, 1 на местах
        private string _wxKind = "?";   // что сейчас нарисовано
        private string _wxRaw;          // последняя сырая строка погоды
        private readonly List<Image> _outDots = new List<Image>();
        private readonly List<Image> _outFill = new List<Image>();   // заливка-сосед
        private readonly List<Image> _inDots = new List<Image>();
        private readonly List<Image> _inFill = new List<Image>();
        private readonly List<RectTransform> _trapSlots = new List<RectTransform>();
        private readonly List<Image> _trapDots = new List<Image>();
        private readonly List<float> _trapNear = new List<float>();

        private string _builtFor;               // для какого интерьера собрана схема
        private bool _lightsOn = true;
        private bool _lampOwnColor;
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
            var img = MakeMark(slot, "Dot");
            img.sprite = NotifyWidget.Folder();
            return img;
        }

        /// <summary>
        /// Метка на схеме. И контур, и заливка создаются ЭТИМ ЖЕ методом и живут
        /// соседями в одном слоте: раньше заливка была ребёнком иконки со своими
        /// якорями, и любое несовпадение настроек читалось как сдвиг вбок.
        /// </summary>
        private Image MakeMark(RectTransform slot, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 30f);
            var img = go.GetComponent<Image>();
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
            _lampImgs.Clear(); _weatherBits.Clear(); _lampGlows.Clear();
            _darkNoise = null; _gloomTop = float.NaN;
            _cableImg = null; _cableExt.Clear(); _elevCarTop = 0f; _elevPos = 0f;
            _gloomSrc = null; _gloomSpr = null;
            _outSlots.Clear(); _inSlots.Clear(); _outDots.Clear(); _inDots.Clear();
            _outFill.Clear(); _inFill.Clear();
            _trapSlots.Clear(); _trapDots.Clear();
            _inPaths.Clear(); _outPaths.Clear();
            _elevCar = null;
            if (_movers == null)
                _movers = new[] { _outDots, _inDots, _outFill, _inFill, _trapDots,
                                  _cableExt, _ship };
            // рисунки погоды уничтожены вместе с детьми _art: без сброса
            // Tick лезет в снесённые объекты и сыплет ошибками каждый кадр
            _fx = null; _wxKind = null; _wxRaw = null;

            // Порядок источников: картинки из папки, потом текстовый файл, потом
            // встроенный рисунок. Так свой макет можно принести и картинками,
            // и координатами, ничего не пересобирая.
            var imgs = MapImages.Load();
            if (imgs != null && imgs.Count > 0)
            {
                _gloomSrc = imgs;
                DrawFromImages(imgs);
                var slotsLay = MapLayout.Load();
                if (slotsLay != null) AddSlotsFromLayout(slotsLay);
                if (_inDots.Count == 0 && _outDots.Count == 0) DefaultSlots();
                SortInsidePathsByFloor();
                BuildTrapSlots();
                _builtFor = interior ?? "";
                Warp();
                return;
            }

            var lay = MapLayout.Load();
            if (lay != null && DrawFromLayout(lay))
            {
                _builtFor = interior ?? "";
                Warp();
                return;
            }

            var frame = S.Frame;
            var dim = OverlayStyle.WithA(S.FrameDim, 0.6f);
            var scan = OverlayStyle.WithA(S.Frame, 0.10f);
            var cave = OverlayStyle.WithA(S.Frame, 0.85f);

            // ---------- земля: кривая линия с резкими углами ----------
            var ground = new[]
            {
                new Vector2(0f, 100f), new Vector2(36f, 93f), new Vector2(72f, 102f),
                new Vector2(108f, 91f), new Vector2(146f, 99f), new Vector2(182f, 92f),
                new Vector2(192f, 96f), new Vector2(W, 96f),
            };
            for (int i = 0; i < ground.Length - 1; i++)
                Line(ground[i].x, ground[i].y, ground[i + 1].x, ground[i + 1].y, 2.5f, frame, "Ground");

            // ---------- здание на земле ----------
            const float bx = 192f, by = 22f, bw = 126f, bh = 74f;
            const float shaftL = bx + 84f, shaftR = bx + 118f;   // широкий проём шахты справа
            Line(bx, by, bx + bw, by, 2.5f, frame);              // крыша
            Line(bx, by, bx, by + bh, 2.5f, frame);              // левая стена
            Line(bx + bw, by, bx + bw, by + bh, 2.5f, frame);    // правая стена
            Line(bx, by + bh, shaftL, by + bh, 2.5f, frame);     // пол слева от проёма
            Line(shaftR, by + bh, bx + bw, by + bh, 2.5f, frame);// и справа
            Fill(bx + 2f, by + 2f, bw - 4f, bh - 4f, scan);

            // дверь слева внутри здания
            const float dx = bx + 14f, dy = by + 34f, dw = 26f, dh = 34f;
            Box(dx, dy, dw, dh, 2f, frame);
            Line(dx, dy + dh / 2f, dx + dw, dy + dh / 2f, 1.5f, dim);
            Line(dx + dw / 2f, dy, dx + dw / 2f, dy + dh, 1.5f, dim);

            // вагонетка посередине
            const float cx = bx + 50f, cy = by + 60f;
            Line(cx, cy, cx + 30f, cy, 2f, frame);
            Line(cx, cy, cx + 4f, cy - 16f, 2f, frame);
            Line(cx + 30f, cy, cx + 26f, cy - 16f, 2f, frame);
            Line(cx + 4f, cy - 16f, cx + 26f, cy - 16f, 2f, frame);
            for (int i = 0; i < 2; i++)
            {
                float wx = cx + 8f + i * 14f;
                Line(wx - 4f, cy + 5f, wx + 4f, cy + 5f, 2f, dim);
                Line(wx - 4f, cy + 2f, wx - 4f, cy + 5f, 1.5f, dim);
                Line(wx + 4f, cy + 2f, wx + 4f, cy + 5f, 1.5f, dim);
            }

            // ---------- широкий коридор от здания вниз ----------
            const float roomY = 158f;
            Line(shaftL, by + bh, shaftL, roomY, 2.5f, frame, "Shaft");
            Line(shaftR, by + bh, shaftR, roomY, 2.5f, frame, "Shaft");
            Fill(shaftL + 2f, by + bh, shaftR - shaftL - 4f, roomY - (by + bh), scan);

            // ---------- главная комната: короче снизу, чтобы поместился вход в шахту ----------
            const float rx = 30f, rw = 288f, rh = 78f;
            Line(rx, roomY, shaftL, roomY, 2.5f, frame);          // потолок с разрывом под коридором
            Line(shaftR, roomY, rx + rw, roomY, 2.5f, frame);
            Line(rx, roomY + rh, rx + rw, roomY + rh, 2.5f, frame);
            Line(rx, roomY, rx, roomY + rh, 2.5f, frame);
            Line(rx + rw, roomY, rx + rw, roomY + rh, 2.5f, frame);
            Fill(rx + 2f, roomY + 2f, rw - 4f, rh - 4f, scan);

            // ---------- лифт: широкий, на тросах от пола здания ----------
            _elevTop = roomY + 22f;
            _elevBottom = roomY + rh - 26f;
            _elevCableTop = by + bh;
            _elevCable1 = Line(shaftL + 7f, _elevCableTop, shaftL + 7f, _elevTop, 1.5f, frame, "Cable");
            _elevCable2 = Line(shaftR - 7f, _elevCableTop, shaftR - 7f, _elevTop, 1.5f, frame, "Cable");
            _elevCar = MakeCar(shaftL - 2f, _elevTop, (shaftR - shaftL) + 4f, 24f, frame, scan);

            // ---------- рельсы со стопором, не заходя под лифт ----------
            float railY = roomY + rh - 11f;
            float railEnd = shaftL - 14f;
            Line(rx + 12f, railY, railEnd, railY, 2f, dim, "Rail");
            Line(rx + 12f, railY + 6f, railEnd, railY + 6f, 2f, dim, "Rail");
            for (float x = rx + 20f; x < railEnd - 4f; x += 18f)
                Line(x, railY - 2f, x, railY + 8f, 1.5f, OverlayStyle.WithA(S.FrameDim, 0.4f), "Rail");
            Line(railEnd, railY - 6f, railEnd, railY + 10f, 2.5f, frame, "RailStop");   // стопор

            // ---------- люминесцентные лампы: две ножки каждая ----------
            var lampOn = new Color(1f, 0.85f, 0.15f, 1f);
            for (int i = 0; i < 3; i++)
            {
                float lx = rx + 28f + i * 66f;
                const float lw = 36f;
                Line(lx + 7f, roomY + 2f, lx + 7f, roomY + 13f, 1.5f, dim, "LampStem");
                Line(lx + lw - 7f, roomY + 2f, lx + lw - 7f, roomY + 13f, 1.5f, dim, "LampStem");
                _lampImgs.Add(Line(lx, roomY + 14f, lx + lw, roomY + 14f, 5f, lampOn, "Lamp"));
            }

            // ---------- шахта: от ЛЕВОЙ стены комнаты вниз и вправо ----------
            var spine = new[]
            {
                new Vector2(rx, roomY + rh - 18f),   // выходит из левой стены у самого пола
                new Vector2(76f, 282f),
                new Vector2(132f, 296f),
                new Vector2(160f, 330f),             // большой зал
                new Vector2(216f, 322f),
                new Vector2(252f, 352f),
                new Vector2(298f, 356f),             // округлый тупик
            };
            var widths = new[] { 18f, 17f, 19f, 34f, 20f, 18f, 20f };
            CaveOutline(spine, widths, cave);

            // ---------- места для монстров ----------
            for (int i = 0; i < 8; i++)                       // уличные — над землёй
            {
                var sl = Slot(20f + i * 22f, 78f);   // на землю, а не в воздухе
                _outSlots.Add(sl); _outFill.Add(MakeMark(sl, "Fill")); _outDots.Add(Dot(sl));
            }
            // Внутри задаём не точки, а ОТРЕЗКИ, вдоль которых иконка ходит туда-сюда.
            _inPaths.Clear();
            _inPaths.Add(new Vector4(rx + 26f, roomY + rh - 26f, rx + 110f, roomY + rh - 26f));
            _inPaths.Add(new Vector4(rx + 130f, roomY + rh - 26f, rx + 214f, roomY + rh - 26f));
            _inPaths.Add(new Vector4(rx + 40f, roomY + rh - 44f, rx + 120f, roomY + rh - 44f));
            _inPaths.Add(new Vector4(rx + 150f, roomY + rh - 44f, rx + 236f, roomY + rh - 44f));
            _inPaths.Add(new Vector4(62f, 276f, 106f, 288f));
            _inPaths.Add(new Vector4(120f, 296f, 156f, 312f));
            _inPaths.Add(new Vector4(140f, 330f, 190f, 328f));
            _inPaths.Add(new Vector4(206f, 322f, 246f, 340f));
            _inPaths.Add(new Vector4(250f, 352f, 288f, 356f));
            _inPaths.Add(new Vector4(170f, 336f, 214f, 330f));
            foreach (var seg in _inPaths)
            {
                var sl = Slot(seg.x, seg.y);
                _inSlots.Add(sl); _inFill.Add(MakeMark(sl, "Fill")); _inDots.Add(Dot(sl));
            }

            _builtFor = interior ?? "";
            Warp();
        }

        /// <summary>
        /// Обводка тоннеля одним замкнутым контуром.
        ///
        /// Два условия, без которых это не выглядит пещерой:
        ///  1) стык на изгибе. Направление стенки в вершине берём как СРЕДНЕЕ от двух
        ///     сходящихся участков, иначе стенки расходятся и рисунок рассыпается;
        ///  2) неровность. Осевую дробим на короткие отрезки и уводим каждую точку в
        ///     сторону по детерминированному шуму — стенка идёт множеством мелких
        ///     звеньев и получается небрежной от руки, а не чертёжной.
        /// </summary>
        private void CaveOutline(Vector2[] ctrl, float[] ctrlW, Color col)
        {
            if (ctrl == null || ctrl.Length < 2) return;
            const int Sub = 6;                    // звеньев на участок — чем больше, тем живее

            var spine = new List<Vector2>();
            var wid = new List<float>();
            for (int i = 0; i < ctrl.Length - 1; i++)
            {
                for (int k = 0; k < Sub; k++)
                {
                    float t = k / (float)Sub;
                    spine.Add(Vector2.Lerp(ctrl[i], ctrl[i + 1], t));
                    wid.Add(Mathf.Lerp(ctrlW[i], ctrlW[Mathf.Min(i + 1, ctrlW.Length - 1)], t));
                }
            }
            spine.Add(ctrl[ctrl.Length - 1]);
            wid.Add(ctrlW[ctrlW.Length - 1]);

            int n = spine.Count;
            var left = new Vector2[n];
            var right = new Vector2[n];

            for (int i = 0; i < n; i++)
            {
                Vector2 dir;
                if (i == 0) dir = (spine[1] - spine[0]).normalized;
                else if (i == n - 1) dir = (spine[n - 1] - spine[n - 2]).normalized;
                else
                {
                    var a = (spine[i] - spine[i - 1]).normalized;
                    var b = (spine[i + 1] - spine[i]).normalized;
                    dir = (a + b).normalized;
                    if (dir.sqrMagnitude < 0.0001f) dir = b;
                }
                var nrm = new Vector2(-dir.y, dir.x);

                // Шум ТОЛЬКО НАРУЖУ: множитель всегда >= 1, поэтому стенка выпирает
                // от оси и никогда не заходит внутрь тоннеля. Раньше он гулял в обе
                // стороны, и линии залезали в просвет.
                float wl = wid[i] * (1f + 0.42f * Mathf.PerlinNoise(i * 0.5f, 3.1f));
                float wr = wid[i] * (1f + 0.42f * Mathf.PerlinNoise(i * 0.5f, 9.7f));
                // сдвиг вдоль оси — чтобы стенки не были параллельны друг другу
                var along = dir * (Mathf.PerlinNoise(i * 0.8f, 5.5f) - 0.5f) * 6f;

                // у входа в комнату не кривим: там стык со стеной
                if (i == 0) { wl = wr = wid[i]; along = Vector2.zero; }

                left[i] = spine[i] + nrm * wl + along;
                right[i] = spine[i] - nrm * wr + along;
            }

            for (int i = 0; i < n - 1; i++)
            {
                Line(left[i].x, left[i].y, left[i + 1].x, left[i + 1].y, 2f, col, "Cave");
                Line(right[i].x, right[i].y, right[i + 1].x, right[i + 1].y, 2f, col, "Cave");
            }
            // вход в комнату — прямой торец
            Line(left[0].x, left[0].y, right[0].x, right[0].y, 2f, col, "Cave");

            // тупик — округлая шапка: обводим полукругом от левой стенки к правой
            var endC = spine[n - 1];
            var endDir = (spine[n - 1] - spine[n - 2]).normalized;
            float endR = (left[n - 1] - endC).magnitude;
            float a0 = Mathf.Atan2(left[n - 1].y - endC.y, left[n - 1].x - endC.x);
            const int Arc = 7;
            var prev = left[n - 1];
            for (int k = 1; k <= Arc; k++)
            {
                float ang = a0 - Mathf.PI * k / Arc;   // полукруг через направление движения
                var pt = endC + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * endR
                              + endDir * (Mathf.Sin(Mathf.PI * k / Arc) * 5f);
                Line(prev.x, prev.y, pt.x, pt.y, 2f, col, "Cave");
                prev = pt;
            }
            Line(prev.x, prev.y, right[n - 1].x, right[n - 1].y, 2f, col, "Cave");
        }

        /// <summary>
        /// Показ слоёв-картинок. Каждый слой растягивается на весь холст схемы, так
        /// что рисовать можно в любом разрешении — важны пропорции, а не размер.
        /// </summary>
        private void DrawFromImages(List<MapImages.Layer> layers)
        {
            foreach (var l in layers)
            {
                var go = new GameObject("Layer_" + l.Name,
                                        typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_art, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(W, H);

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.type = Image.Type.Simple;

                // Цвет темы достаётся только белой и серой графике; всё, что игрок
                // уже покрасил, идёт как нарисовано. Иначе кабина лифта и лампы
                // красились ВТОРОЙ раз — краска поверх краски.
                img.sprite = MapImages.Tinted(l, S.Frame) ?? l.Sprite;
                img.color = l.IsCave ? new Color(1f, 1f, 1f, 0.9f) : Color.white;

                // Свет ставим под КАЖДОЙ жёлтой отметиной, на каком бы слое она ни
                // была: совмещённый макет приходит одной картинкой, и отдельного
                // слоя ламп в нём нет.
                foreach (var b in l.LampBlobs) AddLampGlow(b.x * W, b.y * H);

                // а сами лампы вынимаем в свою картинку поверх — тогда они гаснут
                // со щитком, оставаясь жёлтыми, и не тянут за собой весь макет
                var only = MapImages.LampsOnly(l);
                if (only != null)
                {
                    var lgo = new GameObject("Layer_" + l.Name + "_lamps",
                                             typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    var lrt = (RectTransform)lgo.transform;
                    lrt.SetParent(_art, false);
                    lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
                    lrt.pivot = new Vector2(0f, 1f);
                    lrt.anchoredPosition = Vector2.zero;
                    lrt.sizeDelta = new Vector2(W, H);
                    var lim = lgo.GetComponent<Image>();
                    lim.sprite = only;
                    lim.raycastTarget = false;
                    lim.color = Color.white;
                    _lampImgs.Add(lim);
                    _lampOwnColor = true;
                }

                // а дышать яркостью может только отдельный слой: мигать всей
                // картинкой из-за трёх ламп никуда не годится
                if (l.IsLamp)
                {
                    _lampImgs.Add(img);
                    _lampOwnColor = l.HasColor;
                }

                if (l.IsElevator)
                {
                    // Слой лифта ездит целиком: кабину рисуют в ВЕРХНЕМ положении —
                    // там она и стоит в начале дня, — а мод возит её вслед за
                    // настоящей. Куда именно, он смотрит по самому рисунку.
                    _elevCar = rt;
                    _elevTop = 0f;
                    _elevCarTop = l.Bounds.yMin * H;
                    float carBottom = l.Bounds.yMax * H;
                    _elevBottom = _elevTravel > 0f ? _elevTravel
                                                   : Mathf.Max(20f, (H - 24f) - carBottom);
                    Plugin.Log?.LogInfo($"[map] лифт: потолок кабины {_elevCarTop:0}, ход {_elevBottom:0}");
                }
                else if (l.IsCable)
                {
                    // Трос виден ровно до потолка кабины. Дальшедва  пути: если его
                    // нарисовали на весь ствол — лишнее срезаем заливкой (маска не
                    // переживает наклон, под которым стоит схема); если только до
                    // верхнего положения кабины — дотягиваем сами.
                    _cableImg = img;
                    _cableBottom = l.Bounds.yMax * H;
                    img.type = Image.Type.Filled;
                    img.fillMethod = Image.FillMethod.Vertical;
                    img.fillOrigin = (int)Image.OriginVertical.Top;
                    img.fillAmount = 1f;

                    _cableOwnColor = l.HasColor;
                    foreach (var b in l.Blobs)
                        _cableExt.Add(Line(b.x * W, _cableBottom, b.x * W, _cableBottom + 1f,
                                           1.6f, S.Frame, "CableExt"));
                    Plugin.Log?.LogInfo($"[map] тросы: конец рисунка {_cableBottom:0}, штук {_cableExt.Count}");
                }
            }
        }

        /// <summary>Места монстров из текстового файла (картинки их не задают).</summary>
        private void AddSlotsFromLayout(MapLayout lay)
        {
            _inPaths.Clear();
            foreach (var c in lay.Cmds)
            {
                var n = c.N;
                if (c.Op == "gloom" && n.Length >= 1)
                {
                    // gloom <высота схода> [<наклон кромки в пикселях на всю ширину>]
                    _gloomTop = n[0];
                    _gloomSlope = n.Length >= 2 ? n[1] : 0f;
                    continue;
                }
                if (c.Op == "elev" && n.Length == 1)
                {
                    // сколько кабина проезжает вниз; читается уже после картинок,
                    // поэтому ход правим прямо здесь
                    _elevTravel = n[0];
                    if (_elevCar != null) _elevBottom = n[0];
                    continue;
                }
                if (c.Op == "slotout" && n.Length >= 2)
                {
                    // отрезок можно задать явно (4 числа), иначе монстр ходит вокруг точки
                    var seg = n.Length >= 4
                        ? new Vector4(n[0], n[1], n[2], n[3])
                        : new Vector4(n[0] - 26f, n[1], n[0] + 26f, n[1]);
                    _outPaths.Add(seg);
                    var sl = Slot(seg.x, seg.y);
                    _outSlots.Add(sl); _outFill.Add(MakeMark(sl, "Fill")); _outDots.Add(Dot(sl));
                }
                else if (c.Op == "slotin" && n.Length >= 4)
                {
                    _inPaths.Add(new Vector4(n[0], n[1], n[2], n[3]));
                    var sl = Slot(n[0], n[1]);
                    _inSlots.Add(sl); _inFill.Add(MakeMark(sl, "Fill")); _inDots.Add(Dot(sl));
                }
            }
        }

        /// <summary>
        /// Маршруты внутри упорядочиваем СНИЗУ ВВЕРХ: монстры приходят в порядке
        /// обнаружения, и первым просканированным логично ходить у самого пола,
        /// а не под потолком.
        /// </summary>
        private void SortInsidePathsByFloor()
        {
            var order = new List<int>();
            for (int i = 0; i < _inPaths.Count; i++) order.Add(i);
            order.Sort((a, b) => _inPaths[b].y.CompareTo(_inPaths[a].y));   // больше Y = ниже

            var np = new List<Vector4>();
            var ns = new List<RectTransform>();
            var nd = new List<Image>();
            var nf = new List<Image>();
            foreach (var i in order)
            {
                np.Add(_inPaths[i]);
                if (i < _inSlots.Count) ns.Add(_inSlots[i]);
                if (i < _inDots.Count) nd.Add(_inDots[i]);
                if (i < _inFill.Count) nf.Add(_inFill[i]);
            }
            _inPaths.Clear(); _inPaths.AddRange(np);
            _inSlots.Clear(); _inSlots.AddRange(ns);
            _inDots.Clear(); _inDots.AddRange(nd);
            _inFill.Clear(); _inFill.AddRange(nf);
        }

        /// <summary>Запасные места, если их нигде не задали.</summary>
        private void DefaultSlots()
        {
            for (int i = 0; i < 8; i++)
            {
                float x = 20f + i * 22f;
                _outPaths.Add(new Vector4(x - 20f, 78f, x + 20f, 78f));
                var sl = Slot(x - 20f, 78f);
                _outSlots.Add(sl); _outFill.Add(MakeMark(sl, "Fill")); _outDots.Add(Dot(sl));
            }
            _inPaths.Clear();
            var paths = new[]
            {
                new Vector4(56f, 210f, 140f, 210f), new Vector4(160f, 210f, 244f, 210f),
                new Vector4(70f, 192f, 150f, 192f), new Vector4(180f, 192f, 266f, 192f),
                new Vector4(62f, 276f, 106f, 288f), new Vector4(120f, 296f, 156f, 312f),
                new Vector4(140f, 330f, 190f, 328f), new Vector4(206f, 322f, 246f, 340f),
                new Vector4(250f, 352f, 288f, 356f), new Vector4(170f, 336f, 214f, 330f),
            };
            foreach (var seg in paths)
            {
                _inPaths.Add(seg);
                var sl = Slot(seg.x, seg.y);
                _inSlots.Add(sl); _inFill.Add(MakeMark(sl, "Fill")); _inDots.Add(Dot(sl));
            }
        }

        /// <summary>
        /// Отрисовка схемы по текстовому описанию. Возвращает false, если в файле не
        /// оказалось ничего осмысленного — тогда рисуется встроенная схема.
        /// </summary>
        private bool DrawFromLayout(MapLayout lay)
        {
            var frame = S.Frame;
            var dim = OverlayStyle.WithA(S.FrameDim, 0.6f);
            var scan = OverlayStyle.WithA(S.Frame, 0.10f);
            var cave = OverlayStyle.WithA(S.Frame, 0.85f);
            var lampOn = new Color(1f, 0.85f, 0.15f, 1f);

            Color Pick(string a)
            {
                switch ((a ?? "frame").ToLowerInvariant())
                {
                    case "dim": return dim;
                    case "cave": return cave;
                    case "lamp": return lampOn;
                    case "scan": return scan;
                    default: return frame;
                }
            }

            int drawn = 0;
            _inPaths.Clear();

            foreach (var c in lay.Cmds)
            {
                var n = c.N;
                try
                {
                    switch (c.Op)
                    {
                        case "line":
                            if (n.Length < 4) break;
                            Line(n[0], n[1], n[2], n[3], n.Length > 4 ? n[4] : 2.5f, Pick(c.Arg));
                            drawn++; break;

                        case "box":
                            if (n.Length < 4) break;
                            Box(n[0], n[1], n[2], n[3], n.Length > 4 ? n[4] : 2.5f, Pick(c.Arg));
                            drawn++; break;

                        case "fill":
                            if (n.Length < 4) break;
                            Fill(n[0], n[1], n[2], n[3],
                                 OverlayStyle.WithA(S.Frame, n.Length > 4 ? n[4] : 0.10f));
                            drawn++; break;

                        case "lamp":
                        {
                            if (n.Length < 3) break;
                            float lx = n[0], ly = n[1], lw = n[2];
                            Line(lx + 7f, ly + 2f, lx + 7f, ly + 13f, 1.5f, dim, "LampStem");
                            Line(lx + lw - 7f, ly + 2f, lx + lw - 7f, ly + 13f, 1.5f, dim, "LampStem");
                            _lampImgs.Add(Line(lx, ly + 14f, lx + lw, ly + 14f, 5f, lampOn, "Lamp"));
                            drawn++; break;
                        }

                        case "rails":
                        {
                            if (n.Length < 3) break;
                            float x1 = n[0], x2 = n[1], y = n[2];
                            Line(x1, y, x2, y, 2f, dim, "Rail");
                            Line(x1, y + 6f, x2, y + 6f, 2f, dim, "Rail");
                            for (float x = x1 + 8f; x < x2 - 4f; x += 18f)
                                Line(x, y - 2f, x, y + 8f, 1.5f, OverlayStyle.WithA(S.FrameDim, 0.4f), "Rail");
                            drawn++; break;
                        }

                        case "railstop":
                            if (n.Length < 2) break;
                            Line(n[0], n[1] - 6f, n[0], n[1] + 10f, 2.5f, frame, "RailStop");
                            drawn++; break;

                        case "cable":
                            if (n.Length < 3) break;
                            var cab = Line(n[0], n[1], n[0], n[2], 1.5f, frame, "Cable");
                            if (_elevCable1 == null) { _elevCable1 = cab; _elevCableTop = n[1]; }
                            else if (_elevCable2 == null) _elevCable2 = cab;
                            drawn++; break;

                        case "elev":
                            if (n.Length < 6) break;
                            _elevTop = n[4];
                            _elevBottom = n[5];
                            _elevCar = MakeCar(n[0], n[1], n[2], n[3], frame, scan);
                            drawn++; break;

                        case "cave":
                        {
                            // числа до ';' — осевая, после — полуширины
                            int half = n.Length / 3 * 2;      // на 2 координаты приходится 1 ширина
                            int pts = Mathf.Max(2, half / 2);
                            if (n.Length < pts * 2 + pts) break;
                            var spine = new Vector2[pts];
                            var wid = new float[pts];
                            for (int i = 0; i < pts; i++) spine[i] = new Vector2(n[i * 2], n[i * 2 + 1]);
                            for (int i = 0; i < pts; i++) wid[i] = n[pts * 2 + i];
                            CaveOutline(spine, wid, cave);
                            drawn++; break;
                        }

                        case "slotout":
                        {
                            if (n.Length < 2) break;
                            var sl = Slot(n[0], n[1]);
                            _outSlots.Add(sl); _outFill.Add(MakeMark(sl, "Fill")); _outDots.Add(Dot(sl));
                            drawn++; break;
                        }

                        case "slotin":
                        {
                            if (n.Length < 4) break;
                            _inPaths.Add(new Vector4(n[0], n[1], n[2], n[3]));
                            var sl = Slot(n[0], n[1]);
                            _inSlots.Add(sl); _inFill.Add(MakeMark(sl, "Fill")); _inDots.Add(Dot(sl));
                            drawn++; break;
                        }
                    }
                }
                catch { }
            }

            if (drawn == 0) Plugin.Log?.LogWarning("[map] в файле схемы не нашлось ни одной команды.");
            return drawn > 0;
        }

        // ---- лифт ----
        private RectTransform _elevCar;
        private Image _elevCable1, _elevCable2;
        private float _elevTop, _elevBottom, _elevPos, _elevCableTop;
        private float _elevCarTop, _elevTravel;   // потолок кабины и заданный ход
        private Image _cableImg;
        private float _cableBottom;
        private bool _cableOwnColor;
        private readonly List<Image> _cableExt = new List<Image>();

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

            if (Time.unscaledTime >= _elevLog)
            {
                _elevLog = Time.unscaledTime + 10f;
                Plugin.Log?.LogInfo($"[map] лифт: пульт {(_elevCtl != null ? "есть" : "нет")}, " +
                                    $"доля {ElevatorFraction(_elevCtl):0.00}, внизу {atBottom}, " +
                                    $"ход {_elevBottom:0}, потолок {_elevCarTop:0}");
            }

            // Настоящий лифт едет плавно, и оверлей повторяет его положение один
            // в один: доля пути считается по самой кабине между её верхней и
            // нижней точками. Если до них не дотянуться — довольствуемся флажком
            // «внизу» и едем к нему сами.
            float frac = ElevatorFraction(_elevCtl);
            if (frac >= 0f) _elevPos = frac;
            else _elevPos = Mathf.MoveTowards(_elevPos, atBottom ? 1f : 0f, dt / 3f);

            float y = Mathf.Lerp(_elevTop, _elevBottom, _elevPos);
            _elevCar.anchoredPosition = new Vector2(_elevCar.anchoredPosition.x, -y);

            float roof = _elevCarTop + y;
            if (_cableImg != null)
            {
                // нарисован весь ствол — срезаем ниже потолка кабины
                bool full = _cableBottom > _elevCarTop + 4f;
                _cableImg.fillAmount = full ? Mathf.Clamp01(roof / Mathf.Max(1f, H)) : 1f;
            }
            // а если трос нарисован только до верхнего положения — доводим его
            // до кабины сами: спускается — троса открывается больше
            foreach (var ext in _cableExt)
            {
                if (ext == null) continue;
                float len = roof - _cableBottom;
                var ert = (RectTransform)ext.transform;
                ert.sizeDelta = new Vector2(Mathf.Max(0.5f, len), ert.sizeDelta.y);
                ext.enabled = len > 1f;
                if (!_cableOwnColor) ext.color = S.Frame;
            }

            // нарисованные линиями тросы тянутся за кабиной от пола здания
            foreach (var c in new[] { _elevCable1, _elevCable2 })
            {
                if (c == null) continue;
                var rt = (RectTransform)c.transform;
                rt.sizeDelta = new Vector2(Mathf.Max(1f, y - _elevCableTop), rt.sizeDelta.y);
            }
        }

        private static System.Reflection.FieldInfo _fElevTr, _fElevTop, _fElevBot;

        /// <summary>
        /// Доля пути настоящего лифта: 0 наверху, 1 внизу, -1 если не выяснить.
        ///
        /// Поля читаем отражением: публикованная сборка для сборки мода не значит,
        /// что к ним пустят в игре.
        /// </summary>
        private static float ElevatorFraction(MineshaftElevatorController c)
        {
            try
            {
                if (c == null) return -1f;
                if (_fElevTr == null)
                {
                    var T = typeof(MineshaftElevatorController);
                    const System.Reflection.BindingFlags B =
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;
                    _fElevTr = T.GetField("elevatorTransform", B);
                    _fElevTop = T.GetField("elevatorTopPoint", B);
                    _fElevBot = T.GetField("elevatorBottomPoint", B);
                }
                var tr = _fElevTr != null ? _fElevTr.GetValue(c) as Transform : null;
                var tp = _fElevTop != null ? _fElevTop.GetValue(c) as Transform : null;
                var bp = _fElevBot != null ? _fElevBot.GetValue(c) as Transform : null;
                if (tr == null || tp == null || bp == null) return -1f;

                float top = tp.position.y, bot = bp.position.y;
                if (Mathf.Abs(top - bot) < 0.01f) return -1f;
                return Mathf.Clamp01((top - tr.position.y) / (top - bot));
            }
            catch { return -1f; }
        }

        private static MineshaftElevatorController _elevCtl;
        private static float _elevNext;

        // ================= жизнь =================

        public void Refresh(BridgePayload p, bool wantVisible, float mapT = 1f)
        {
            if (_root == null) return;

            // Метки ездят между схемой и боковыми рейками В ОБЕ стороны: сходятся
            // с краёв при выходе наружу и разъезжаются обратно при возвращении.
            // Держим схему живой, пока идёт переход, иначе уезжать было бы нечему.
            bool alive = wantVisible || mapT > 0.001f;
            if (_root.gameObject.activeSelf != alive) _root.gameObject.SetActive(alive);
            _arrive = mapT;
            if (!alive || p == null) return;

            float dt = Time.unscaledDeltaTime;
            string interior = p.interiorType ?? "";
            if (_builtFor == null || _builtFor != interior) Build(interior);

            _moonText.text = (p.moonName ?? "- -").ToUpperInvariant();
            _moonText.color = S.Text;
            _lootText.text = $"{Localization.T("in")} {p.itemsInside}   " +
                             $"{Localization.T("out")} {p.itemsOutside}   " +
                             $"{Localization.T("hives")} {p.beehiveCount}";

            UpdateElevator(dt);
            UpdateDropship(dt, p != null && !p.onMoon);
            UpdateLights(dt);
            UpdateGloom(dt);
            UpdateDots(p);
            UpdateTraps(Gate.Traps ? p.traps : null);
            UpdateWeather(p, dt);
        }

        /// <summary>Лампы схемы повторяют настоящий свет в комплексе.</summary>
        private Image _darkNoise;
        private readonly List<Image> _lampGlows = new List<Image>();
        private float _gloom;

        // Где полумрак сходит на нет и под каким наклоном лежит его кромка. По
        // умолчанию — по переходу лифта: выше него комплекс освещён и так.
        private float _gloomTop = float.NaN, _gloomSlope;

        /// <summary>
        /// Полумрак и свет.
        ///
        /// Тьма — это ОДНО полотно на весь низ схемы: книзу глухое, кверху сходит
        /// на нет где-то на переходе лифта. Прежний прямоугольник по стенам
        /// комплекса читался в игре как чёрный квадрат посреди картинки; у полотна
        /// же боковые и нижняя кромки совпадают с краями схемы, а верхней попросту
        /// нет. Кромку схода можно наклонить, чтобы она легла по рельефу.
        ///
        /// Свет — по пятну на КАЖДУЮ лампу с рисунка, а не одна дуга на комнату.
        /// </summary>
        /// <summary>
        /// Наделить перспективой то, что появилось уже после сборки схемы.
        ///
        /// Наклон навешивается на каждый рисунок по отдельности при сборке, и
        /// полумрак с огнём, которые рождаются позже, оставались плоскими поверх
        /// наклонённой картинки — виньетка ложилась мимо.
        /// </summary>
        private void Warp()
        {
            try { _mgr?.AddPerspectiveToTree(_art); KeepWarpedAll(); } catch { }
        }

        /// <summary>
        /// Пометить графику подвижной: её меш перекладывается перспективой КАЖДЫЙ
        /// кадр, а не только при смене размера.
        ///
        /// Наклон считается по тому, где вершины оказались в панели, а меш uGUI
        /// пересобирает лишь когда меняется размер прямоугольника. Всё, что ездит
        /// не меняя размера — доставщик, кабина лифта, метки монстров, — так и
        /// оставалось перекошенным по старому месту и разъезжалось с соседями.
        /// Огонь под кораблём дышал размером и потому ложился верно: расходились
        /// они именно из-за этого.
        /// </summary>
        private static void KeepWarped(Graphic g)
        {
            if (g == null) return;
            var w = g.GetComponent<PerspectiveWarp>();
            if (w != null) w.Continuous = true;
        }

        private void KeepWarpedAll()
        {
            foreach (var l in _movers)
                for (int i = 0; i < l.Count; i++) KeepWarped(l[i]);
            KeepWarped(_shipSprite);
            KeepWarped(_flameImg);
            if (_elevCar != null) KeepWarped(_elevCar.GetComponent<Graphic>());
        }

        /// <summary>Списки графики, которая ездит по схеме, не меняя размера.</summary>
        private List<Image>[] _movers;

        private void UpdateGloom(float dt)
        {
            if (float.IsNaN(_gloomTop))
                _gloomTop = _elevCar != null ? Mathf.Max(0f, _elevCarTop - 24f) : RoomTop;

            if (_darkNoise == null)
            {
                var go = new GameObject("Gloom", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_art, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                _darkNoise = go.GetComponent<Image>();
                _darkNoise.raycastTarget = false;

                var carved = CarveGloom();
                if (carved != null)
                {
                    // тьма выкроена по самому рисунку — ложится на холст один в один
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(W, H);
                    _darkNoise.sprite = carved;
                }
                else
                {
                    // рисунка нет (схема собрана линиями) — кладём полотно во всю
                    // ширину; наклон уводим в САМО полотно, поворот дал бы косые углы
                    float span = Mathf.Max(20f, H - _gloomTop);
                    rt.anchoredPosition = new Vector2(0f, -_gloomTop);
                    rt.sizeDelta = new Vector2(W, span);
                    _darkNoise.sprite = SpriteBank.GloomGrad(_gloomSlope * W / span);
                }
                Warp();
            }

            _gloom = Mathf.MoveTowards(_gloom, _lightsOn ? 0f : 1f, dt / 1.2f);
            _darkNoise.color = new Color(1f, 1f, 1f, 0.5f * _gloom);
            _darkNoise.enabled = _gloom > 0.01f;

            float glow = (1f - _gloom) * (0.30f + 0.06f * Mathf.Sin(_lightPulse));
            var lit = new Color(1f, 0.86f, 0.42f, glow);
            foreach (var g in _lampGlows)
            {
                if (g == null) continue;
                g.color = lit;
                g.enabled = glow > 0.01f;
            }
        }

        /// <summary>
        /// Выкроить полумрак по силуэту схемы.
        ///
        /// Прямоугольное полотно ложилось на схему плитой: под ним оказывалась и
        /// пустая порода вокруг пещер, и небо по бокам, и края читались линиями.
        /// Здесь плотность берётся из самих слоёв — где нарисован комплекс и
        /// пещеры, там и темнеет, — и умножается на набор плотности сверху вниз.
        ///
        /// Разрешение нарочно втрое грубее холста: тьма должна быть из тех же
        /// крупных пикселей, что и всё остальное.
        /// </summary>
        private Sprite CarveGloom()
        {
            try
            {
                if (_gloomSpr != null) return _gloomSpr;
                if (_gloomSrc == null || _gloomSrc.Count == 0) return null;

                const int GW = MapImages.GloomW, GH = MapImages.GloomH;
                var tex = new Texture2D(GW, GH, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var px = new Color32[GW * GH];
                var rnd = new System.Random(20260101);

                float span = Mathf.Max(20f, (H - _gloomTop) * 0.55f);
                bool any = false;

                for (int y = 0; y < GH; y++)
                    for (int x = 0; x < GW; x++)
                    {
                        float u = (x + 0.5f) / GW;
                        float v = (y + 0.5f) / GH;              // снизу вверх, как в текстуре
                        float cy = (1f - v) * H;                // та же высота, но сверху вниз

                        // Темнеет ТОЛЬКО фон комплекса и пещер. Обводка, лампы и
                        // рельсы лежат поверх него в полную силу и глушить их
                        // незачем — от этого схема переставала читаться.
                        float cover = 0f;
                        foreach (var l in _gloomSrc)
                        {
                            if (l == null || l.Backdrop == null) continue;
                            if (l.IsLamp || l.IsElevator || l.IsCable) continue;
                            float a = l.Backdrop[y * GW + x];
                            if (a > cover) cover = a;
                        }
                        if (cover <= 0.02f) { px[y * GW + x] = new Color32(0, 0, 0, 0); continue; }

                        // набор плотности сверху вниз, кромка может идти наклонно
                        float t = (cy - _gloomTop - (u - 0.5f) * _gloomSlope) / span;
                        t = Mathf.Clamp01(t);
                        t = t * t * (3f - 2f * t);

                        float a2 = cover * t * (0.76f + 0.24f * (float)rnd.NextDouble());
                        a2 = Mathf.Round(a2 * 10f) / 10f;       // ступени, а не плавность
                        if (a2 > 0.01f) any = true;
                        px[y * GW + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a2) * 255f));
                    }

                if (!any) return null;
                tex.SetPixels32(px);
                tex.Apply(false, false);
                _gloomSpr = Sprite.Create(tex, new Rect(0, 0, GW, GH), new Vector2(0f, 1f), 100f, 0,
                                          SpriteMeshType.FullRect);
                Plugin.Log?.LogInfo($"[map] полумрак выкроен по рисунку, сход с {_gloomTop:0}");
                return _gloomSpr;
            }
            catch (System.Exception e)
            {
                Plugin.Log?.LogWarning($"[map] полумрак не выкроен: {e.Message}");
                return null;
            }
        }

        private List<MapImages.Layer> _gloomSrc;
        private Sprite _gloomSpr;

        /// <summary>Пятно света под лампой, нарисованной на слое.</summary>
        private void AddLampGlow(float x, float y)
        {
            var go = new GameObject("LampGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_art, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // центр НА лампе: пятно обнимает её, а не висит под ней
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(96f, 96f);
            var im = go.GetComponent<Image>();
            im.sprite = SpriteBank.Glow();
            im.raycastTarget = false;
            _lampGlows.Add(im);
            rt.SetAsFirstSibling();
            Warp();          // под рисунком, чтобы линии не мылились
        }


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
                if (_lampOwnColor)
                {
                    // лампа нарисована в своём цвете — трогаем только яркость,
                    // иначе жёлтый лёг бы на жёлтый
                    float g = !_lightsOn ? 0.45f
                            : (!effects ? 1f : 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(_lightPulse)));
                    l.color = new Color(g, g, g, !_lightsOn ? 0.55f : 1f);
                    continue;
                }
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
            PlaceDots(_outDots, p.monstersOutside, false);
            PlaceDots(_inDots, p.monstersInside, true);
        }

        /// <summary>
        /// Метки живут по тем же правилам, что и иконки в рейке: скрываются без скана,
        /// тают по дальности, покачиваются и нервно дрожат вблизи. Иначе схема
        /// показывала бы то, чего основной оверлей не показывает.
        /// </summary>
        private void PlaceDots(List<Image> dots, string[] names, bool walking)
        {
            // Отбираем и СХЛОПЫВАЕМ по иконке: пакет группирует монстров вместе с
            // их состояниями, поэтому один вид в разных состояниях приходил
            // отдельными записями — на схеме это выглядело как два одинаковых зверя.
            var shown = new List<string>();
            var seen = new List<string>();
            if (names != null)
                foreach (var raw in names)
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (MobRailWidget.IsHiddenPublic(raw)) continue;
                    if (Gate.RequireScan &&
                        raw.IndexOf("+Scanned", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    // Девиант — ОТДЕЛЬНАЯ версия монстра, как в основном оверлее:
                    // если валить его в одну кучу с обычным, инверснутый пропадал.
                    // Схлопываем по ВИДУ, а не по иконке: фазы одного и того же
                    // существа (вырос, разозлился, с турелью) — это по-прежнему одно
                    // существо, и двух меток ему не полагается. Девиант отдельно:
                    // это действительно другая версия.
                    // Разновидность — это девиант и версия с турелью: они и правда
                    // другие существа. А «на потолке», «зол», «атакует», «вырос» —
                    // это СОСТОЯНИЯ одного и того же, и второй метки им не полагается.
                    string key = (MobRailWidget.SpeciesKeyPublic(raw) ?? raw)
                               + (raw.IndexOf("+Deviant", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "#d" : "")
                               + (raw.IndexOf("+Turret", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "#t" : "");
                    int at = seen.IndexOf(key);
                    if (at >= 0)
                    {
                        // Тот же вид уже есть — оставляем БЛИЖАЙШУЮ особь. Иначе
                        // подошедшая вплотную собака не закрашивалась: дистанция
                        // бралась от её дальнего сородича.
                        float dn = DistOf(raw), dp = DistOf(shown[at]);
                        if (dn >= 0f && (dp < 0f || dn < dp)) shown[at] = raw;
                        continue;
                    }
                    seen.Add(key);
                    shown.Add(raw);
                }

            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            var paths = walking ? _inPaths : _outPaths;
            var slots = walking ? _inSlots : _outSlots;
            // от ОРИГИНАЛЬНОГО размера иконки: снаружи вдвое, в комплексе в полтора
            float baseScale = walking ? 1.5f : 1.7f;

            // сначала считаем, кто где хочет стоять
            int n = Mathf.Min(dots.Count, shown.Count);
            var want = new Vector2[dots.Count];
            _face.Clear();
            for (int q = 0; q < dots.Count; q++) _face.Add(1f);
            while (_faceHold.Count < dots.Count) _faceHold.Add(1f);
            while (_sep.Count < dots.Count) _sep.Add(Vector2.zero);
            for (int i = 0; i < n; i++)
            {
                Vector2 off = Vector2.zero;
                if (i < paths.Count && i < slots.Count)
                {
                    var seg = paths[i];
                    float sp = 0.07f + (i % 5) * 0.018f;
                    float ph = Mathf.PingPong(t * sp + i * 0.41f, 1f);
                    var a = new Vector2(seg.x, seg.y);
                    var b = new Vector2(seg.z, seg.w);
                    var pos = Vector2.Lerp(a, b, Mathf.SmoothStep(0f, 1f, ph));
                    off = new Vector2(pos.x, -pos.y) - slots[i].anchoredPosition;

                    // Куда идём. У самого разворота ph и ahead почти равны, и знак
                    // прыгал туда-сюда — иконка мелко дёргалась. Поэтому у точек
                    // разворота направление НЕ меняем, а держим прежнее.
                    float ahead = Mathf.PingPong(t * sp + i * 0.41f + 0.03f, 1f);
                    if (ph > 0.06f && ph < 0.94f)
                    {
                        bool goingRight = (ahead > ph) == (b.x >= a.x);
                        _faceHold[i] = goingRight ? -1f : 1f;
                    }
                    _face[i] = _faceHold.Count > i ? _faceHold[i] : 1f;
                }
                want[i] = off + new Vector2(0f, IconLift);
            }

            // и раздвигаем тех, кто налез друг на друга: иначе иконки перекрываются
            // и вместо двух зверей видно одного
            // Расталкивание считаем в отдельный вектор и ПОДМЕШИВАЕМ его плавно.
            // Раньше оно применялось сразу и целиком: две метки у порога расстояния
            // толкали друг друга каждый кадр туда-обратно — это и было дрожание.
            const float MinSep = 34f;
            var push = new Vector2[dots.Count];
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    if (i >= slots.Count || j >= slots.Count) continue;
                    var pi = slots[i].anchoredPosition + want[i];
                    var pj = slots[j].anchoredPosition + want[j];
                    var d = pj - pi;
                    float dist2 = d.magnitude;
                    if (dist2 >= MinSep || dist2 < 0.001f) continue;
                    var n2 = d.normalized;
                    var tangent = new Vector2(-n2.y, n2.x);
                    float need = (MinSep - dist2) * 0.5f;
                    var p2 = n2 * (need * 0.45f) + tangent * (need * 0.85f);
                    push[i] -= p2;
                    push[j] += p2;
                }
            for (int i = 0; i < n; i++)
            {
                while (_sep.Count <= i) _sep.Add(Vector2.zero);
                _sep[i] = Vector2.Lerp(_sep[i], push[i], 1f - Mathf.Exp(-dt * 4f));
                want[i] += _sep[i];
            }

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
                    var gone = walking ? _inFill : _outFill;
                    if (i < gone.Count && gone[i] != null) gone[i].enabled = false;
                    continue;
                }

                string raw = shown[i];
                float dist = DistOf(raw);
                float nearTarget = dist >= 0f ? Mathf.InverseLerp(40f, 4f, dist) : 0f;
                // дистанция приходит раз в секунду целым числом — без сглаживания
                // заливка шла ступеньками
                while (_nearSmooth.Count <= i) _nearSmooth.Add(0f);
                _nearSmooth[i] = Mathf.MoveTowards(_nearSmooth[i], nearTarget, dt / 0.7f);
                float near = _nearSmooth[i];

                // Контур и заливка — ОДИН спрайт: разъезжаться нечему.
                var spr = MobIconFilled(raw, near);
                if (spr != null && img.sprite != spr) img.sprite = spr;

                float phase = i * 1.7f;
                float amp = 3f + 9f * near * near * near;
                float speed = 2f + 14f * near * near;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((t + phase) * speed) * amp);
                var jit = near > 0.01f ? NotifyWidget.PixelJitter(phase, 2.2f * near * near) : Vector2.zero;
                // туман наводит помехи: иконку дёргает и подмигивает ей
                float fog = FogNoise > 0f && (walking == FogInsideNow) ? FogNoise : 0f;
                if (fog > 0f) jit += NotifyWidget.PixelJitter(phase + 7f, 3f * fog);
                var target = want[i] + jit;
                if (_arrive < 1f)
                {
                    // прилетают со своей стороны: уличные слева, комплексные справа
                    float e = 1f - Mathf.Pow(1f - _arrive, 3f);
                    float fromX = walking ? (W - slots[i].anchoredPosition.x) + 90f
                                          : -slots[i].anchoredPosition.x - 90f;
                    target += new Vector2(fromX * (1f - e), 0f);
                }
                rt.anchoredPosition = target;

                float sc = baseScale;
                if (ConfigSettings.ScaleMonstersByCount.Value)
                    sc *= Mathf.Min(1.6f, 1f + 0.16f * (CountOf(raw) - 1));
                // девианта рисуем вверх ногами — тем же приёмом, что и рейка
                float flipY = (ConfigSettings.DeviantFlipIcon.Value &&
                               raw.IndexOf("+Deviant", System.StringComparison.OrdinalIgnoreCase) >= 0) ? -1f : 1f;
                rt.localScale = new Vector3(sc * (i < _face.Count ? _face[i] : 1f), sc * flipY, 1f);

                float a2 = 1f;
                if (ConfigSettings.ProximityFade.Value && dist >= 0f)
                    a2 = Mathf.Lerp(0.28f, 1f, Mathf.InverseLerp(34f, 6f, dist));

                // Туман глушит метки так же, как темнота глушит комплекс: они не
                // просто дёргаются, а становятся ощутимо хуже видны.
                if (fog > 0f)
                {
                    a2 *= Mathf.Lerp(1f, 0.4f, fog);
                    if (Mathf.PerlinNoise(t * 14f, phase) > 0.72f) a2 *= 0.45f;
                }

                var col = MobRailWidget.IconTint(S);
                col.a = Mathf.MoveTowards(img.color.a, a2, dt * 4f);
                img.color = col;

                var fills = walking ? _inFill : _outFill;
                if (i < fills.Count && fills[i] != null) fills[i].enabled = false;
            }
        }

        /// <summary>На сколько поднять иконку над её точкой, чтобы не тонуть в полу.</summary>
        private const float IconLift = 22f;

        /// <summary>Сколько особей в записи ("Name x3 @20").</summary>
        private static int CountOf(string raw)
        {
            try
            {
                var m = System.Text.RegularExpressions.Regex.Match(raw, @"\sx(\d+)");
                return m.Success ? int.Parse(m.Groups[1].Value) : 1;
            }
            catch { return 1; }
        }

        /// <summary>
        /// Иконка с нутрянкой по близости. В контурном стиле заливка вшита в сам
        /// спрайт; в остальных стилях иконка и так сплошная, и ничего не нужно.
        /// </summary>
        private static Sprite MobIconFilled(string raw, float near)
        {
            try
            {
                string key = MobRailWidget.IconKeyPublic(raw);
                if (string.IsNullOrEmpty(key)) return NotifyWidget.Folder();
                if (!MobRailWidget.TintedIconStylePublic()) return SpriteBank.Get(key);

                // в покое нутрянка уже чуть видна, вплотную — залита целиком
                float k = Mathf.Clamp01(near);
                k = k * k * (3f - 2f * k);
                int lvl = Mathf.RoundToInt(Mathf.Lerp(4f, SpriteBank.FillLevels, k));
                return SpriteBank.GetOutlineFilled(key, lvl);
            }
            catch { return NotifyWidget.Folder(); }
        }

        private static Sprite MobIconFor(string raw, bool solid = false)
        {
            try
            {
                string key = MobRailWidget.IconKeyPublic(raw);
                if (string.IsNullOrEmpty(key)) return NotifyWidget.Folder();
                return solid ? SpriteBank.GetSolid(key) : SpriteBank.Get(key);
            }
            catch { return NotifyWidget.Folder(); }
        }

        /// <summary>
        /// Силуэт поверх контурной иконки. Чем ближе монстр, тем он плотнее —
        /// раньше подмена шла одним кадром на пороге, и это выглядело как рывок.
        /// </summary>
        /// <summary>
        /// Заливка силуэта. Живёт СОСЕДОМ иконки в том же слоте и повторяет её
        /// положение один в один — так исключён любой сдвиг.
        ///
        /// В покое силуэт виден тёмно-красным полупрозрачным, и чем ближе монстр,
        /// тем плотнее и ярче он становится.
        /// </summary>
        private void UpdateFillMark(Image fill, RectTransform iconRt, string raw, float near)
        {
            try
            {
                if (fill == null || iconRt == null) return;

                if (!MobRailWidget.TintedIconStylePublic())
                {
                    if (fill.enabled) fill.enabled = false;
                    return;
                }

                var solid = MobIconFor(raw, true);
                if (solid != null && fill.sprite != solid) fill.sprite = solid;

                var frt = (RectTransform)fill.transform;
                frt.anchoredPosition = iconRt.anchoredPosition;
                frt.localScale = iconRt.localScale;
                frt.localRotation = iconRt.localRotation;
                frt.sizeDelta = iconRt.sizeDelta;
                frt.SetSiblingIndex(0);

                float k = Mathf.Clamp01(near);
                k = k * k * (3f - 2f * k);
                var rest = new Color(0.45f, 0.10f, 0.10f, 1f);
                var hot = MobRailWidget.IconTint(S);
                var col = Color.Lerp(rest, hot, k);
                var host = iconRt.GetComponent<Image>();
                col.a = Mathf.Lerp(0.32f, 0.95f, k) * (host != null ? host.color.a : 1f);
                fill.color = col;
                fill.enabled = col.a > 0.01f;
            }
            catch { }
        }

        // ---- доставщик из магазина ----
        private readonly List<Image> _ship = new List<Image>();
        private Image _shipSprite;                 // если игрок положил свою картинку
        private float _shipT = -1f;                // 0 садится … 1 улетел, -1 его нет
        private bool _shipWasHere;

        private static ItemDropship _shipObj;
        private static float _shipNext;
        private static float _elevLog;

        // Садится СЛЕВА, на траву, а не над кораблём: там пусто и он никому не мешает.
        // Садится СЛЕВА на траву; НИЖЕ линии земли — так он стоит на ней, а не
        // висит над. Заходит из правого верхнего угла.
        private const float ShipX = 62f, ShipGroundY = Ground + 9f;
        private const float ShipFallSecs = 8f;      // ровно столько идёт посадка
        private const float ShipHoverAt = 6f;       // с этой секунды висит у земли
        private const float ShipDropAt = 7f;        // с этой — опускается на неё
        private float _shipAge;                     // секунд с начала манёвра

        /// <summary>Путь к своей картинке доставщика, если её положили рядом со слоями.</summary>
        private static string ShipImagePath
        {
            get
            {
                try { return System.IO.Path.Combine(MapImages.Dir, "dropship.png"); }
                catch { return null; }
            }
        }

        /// <summary>
        /// Доставщик садится и взлетает КАК РАКЕТА: идёт вертикально, под ним горит
        /// пламя. Пока разгружается — стоит на земле без огня. Улетает, когда
        /// доставка закончена или когда корабль игроков покидает луну.
        /// </summary>
        private void UpdateDropship(float dt, bool shipLeaving)
        {
            bool here = false, landed = false;
            try
            {
                if (Time.unscaledTime >= _shipNext)
                {
                    _shipNext = Time.unscaledTime + 1f;
                    _shipObj = UnityEngine.Object.FindObjectOfType<ItemDropship>();
                }
                if (_shipObj != null) { here = _shipObj.deliveringOrder; landed = _shipObj.shipLanded; }
            }
            catch { }
            if (shipLeaving) here = false;          // корабль улетает — и этот тоже

            if (here && !_shipWasHere)
            {
                _shipT = 0f; _shipAge = 0f;
                Plugin.Log?.LogInfo("[map] доставщик садится");
            }
            _shipWasHere = here;

            if (_shipT < 0f)
            {
                Hide();
                return;
            }

            EnsureShipParts();

            // Своё время манёвра. Игра сообщает лишь «летит» и «сел», а посадка
            // должна занимать ровно восемь секунд с зависанием на седьмой.
            _shipAge += dt;
            if (here)
            {
                // если игра успела отчитаться о посадке раньше — досрочно доводим
                if (landed && _shipAge < ShipFallSecs) _shipAge = Mathf.Max(_shipAge, ShipDropAt);
                _shipT = Mathf.Clamp01(_shipAge / ShipFallSecs) * 0.5f;
            }
            else
            {
                _shipT = Mathf.MoveTowards(_shipT, 1f, dt / 4f);
                if (_shipT >= 1f) { _shipT = -1f; Hide(); return; }
            }

            float sx, y, tilt, sc;
            if (_shipT < 0.5f) DescentPath(_shipAge, out sx, out y, out tilt, out sc);
            else
            {
                // взлёт — та же дуга задом наперёд, только быстрее
                float u = (_shipT - 0.5f) / 0.5f;
                DescentPath(Mathf.Lerp(ShipFallSecs, 0f, u), out sx, out y, out tilt, out sc);
            }

            bool burning = _shipT < 0.49f || _shipT > 0.51f;   // на стоянке двигатели молчат
            float low = 1f - Mathf.Clamp01((ShipGroundY - y) / 55f);

            var col = OverlayStyle.WithA(S.Frame, 1f);
            if (_shipSprite != null)
            {
                var rt = (RectTransform)_shipSprite.transform;
                rt.anchoredPosition = new Vector2(sx, -y);
                rt.localScale = new Vector3(sc, sc, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
                _shipSprite.enabled = true;
                // доставщик — часть постройки, а не угроза: красим его цветом темы,
                // а не тем, каким помечены монстры
                if (MobRailWidget.TintedIconStylePublic())
                {
                    var full = SpriteBank.GetOutlineFilled("dropship", SpriteBank.FillLevels);
                    if (full != null && _shipSprite.sprite != full) _shipSprite.sprite = full;
                    _shipSprite.color = S.Frame;
                }
                else _shipSprite.color = Color.white;
                foreach (var g in _ship) if (g != null) g.enabled = false;
            }
            else DrawShipLines(sx, y, col);

            DrawFlame(sx, y, burning, low, tilt, sc);
        }

        /// <summary>
        /// Где доставщик на такой-то секунде посадки. Отдаёт точку касания (низ
        /// корпуса), крен и размер.
        ///
        /// Он приходит из правого верхнего угла по КВАДРАТИЧНОЙ ДУГЕ: опорная
        /// точка стоит прямо над площадкой, поэтому кривая приходит в неё отвесно,
        /// без излома. Первые секунды он пролетает быстро — их он проводит за
        /// краем схемы, — а к земле подходит всё медленнее.
        ///
        /// На шестой секунде он уже у самой земли и ВИСИТ, чуть покачиваясь, и
        /// только на восьмой опускается на неё.
        /// </summary>
        private void DescentPath(float age, out float x, out float y, out float tilt, out float scale)
        {
            float hoverY = ShipGroundY - 15f;       // высота зависания
            float tt = Time.unscaledTime;

            if (age < ShipHoverAt)
            {
                float a = Mathf.Clamp01(age / ShipHoverAt);
                float e = 1f - (1f - a) * (1f - a);          // быстро вдали, медленно у земли

                var p0 = new Vector2(W + 46f, -96f);        // из-за правого верхнего угла
                var p1 = new Vector2(ShipX, -34f);          // опора прямо над площадкой
                var p2 = new Vector2(ShipX, hoverY);
                float m = 1f - e;
                x = m * m * p0.x + 2f * m * e * p1.x + e * e * p2.x;
                y = m * m * p0.y + 2f * m * e * p1.y + e * e * p2.y;

                // касательная к дуге — по ней и кренится корпус
                float dx = 2f * m * (p1.x - p0.x) + 2f * e * (p2.x - p1.x);
                float dy = 2f * m * (p1.y - p0.y) + 2f * e * (p2.y - p1.y);
                tilt = Mathf.Clamp(Mathf.Atan2(-dx, dy) * Mathf.Rad2Deg * 0.55f, -28f, 28f);

                float sway = (1f - e) * (Mathf.Sin(tt * 2.3f) * 3.4f + Mathf.Sin(tt * 5.1f) * 1.3f);
                x += sway; tilt += sway * 0.6f;
                scale = Mathf.Lerp(0.42f, 1f, e);
                return;
            }

            x = ShipX; scale = 1f;
            if (age < ShipDropAt)
            {
                // висит: лёгкая болтанка на струе, вниз почти не идёт
                float b = (age - ShipHoverAt) / (ShipDropAt - ShipHoverAt);
                y = hoverY + Mathf.Sin(tt * 3.1f) * 2.2f * (1f - b * 0.5f);
                x += Mathf.Sin(tt * 1.9f) * 1.6f;
                tilt = Mathf.Sin(tt * 2.4f) * 2.2f;
                return;
            }

            float d = Mathf.Clamp01((age - ShipDropAt) / (ShipFallSecs - ShipDropAt));
            y = Mathf.Lerp(hoverY, ShipGroundY, d * d * (3f - 2f * d));
            tilt = Mathf.Sin(tt * 2.4f) * 2.2f * (1f - d);
        }

        private void Hide()
        {
            foreach (var g in _ship) if (g != null) g.enabled = false;
            if (_flameImg != null) _flameImg.enabled = false;
            if (_shipSprite != null) _shipSprite.enabled = false;
        }

        private void EnsureShipParts()
        {
            if (_shipSprite == null && _ship.Count == 0)
            {
                // Картинка доставщика встроена в мод (res/mobs/dropship.png), поэтому
                // стили Pixel/Vector/Symbol применяются к ней сами. Файл в папке слоёв
                // остаётся способом подменить её своей.
                var spr = SpriteBank.FromFile(ShipImagePath, "dropship") ?? SpriteBank.Get("dropship");
                if (spr != null)
                {
                    var go = new GameObject("Dropship", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(_art, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    // опора — НИЗ корабля: именно им он касается земли, и от него же
                    // бьёт пламя. С прежней опорой сверху низ корпуса оказывался
                    // на четыре десятка пикселей ниже сопла, и огонь горел внутри.
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.sizeDelta = new Vector2(52f, 72f);
                    _shipSprite = go.GetComponent<Image>();
                    _shipSprite.sprite = spr;
                    _shipSprite.preserveAspect = true;
                    _shipSprite.raycastTarget = false;
                    Plugin.Log?.LogInfo("[map] доставщик: своя картинка из lcbridgeoverlay-map/dropship.png");
                    Warp();
                }
                else
                    for (int i = 0; i < 16; i++) _ship.Add(Line(0f, 0f, 1f, 1f, 2f, S.Frame));
            }

        }

        /// <summary>
        /// Силуэт капсулы по присланному образцу: корпус с плечиками, юбка с
        /// опорами внизу, два ряда люков и вертикальный шов.
        /// </summary>
        private void DrawShipLines(float x, float y, Color col)
        {
            int n = 0;
            void Seg(float x1, float y1, float x2, float y2, float th)
            {
                if (n >= _ship.Count) return;
                var img = _ship[n++];
                var rt = (RectTransform)img.transform;
                var d = new Vector2(x2 - x1, -(y2 - y1));
                rt.anchoredPosition = new Vector2(x + x1, -(y + y1));
                rt.sizeDelta = new Vector2(Mathf.Max(0.5f, d.magnitude), th);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                img.enabled = true;
                img.color = col;
            }

            // корпус
            Seg(-13f, -52f, 13f, -52f, 2.5f);        // крышка
            Seg(-15f, -48f, -15f, -14f, 2.5f);       // борта
            Seg(15f, -48f, 15f, -14f, 2.5f);
            Seg(-13f, -52f, -15f, -48f, 2f);         // плечики
            Seg(13f, -52f, 15f, -48f, 2f);
            // юбка
            Seg(-15f, -14f, -25f, 0f, 2.5f);
            Seg(15f, -14f, 25f, 0f, 2.5f);
            Seg(-25f, 0f, 25f, 0f, 2.5f);
            // опоры
            Seg(-19f, 0f, -22f, 7f, 2f);
            Seg(19f, 0f, 22f, 7f, 2f);
            // шов и люки
            Seg(0f, -50f, 0f, -14f, 1.5f);
            Seg(-11f, -44f, -3f, -44f, 1.5f);
            Seg(-11f, -44f, -11f, -34f, 1.5f);
            Seg(3f, -44f, 11f, -44f, 1.5f);
            Seg(-11f, -30f, -3f, -30f, 1.5f);
            Seg(3f, -30f, 11f, -30f, 1.5f);

            for (int i = n; i < _ship.Count; i++) _ship[i].enabled = false;
        }

        /// <summary>
        /// Огонь под соплом — картинка из ресурсов (res/mobs/flame.png), а не
        /// собранная кодом струя. Нарисована языками вверх, как и положено огню,
        /// поэтому под кораблём её переворачиваем: опора у сопла, отрицательный
        /// масштаб по вертикали разворачивает пламя вниз.
        ///
        /// У земли оно чувствует под собой опору: укорачивается и расходится
        /// вширь, растекаясь по поверхности.
        /// </summary>
        private void DrawFlame(float x, float y, bool on, float low, float tilt, float scale)
        {
            if (_flameImg == null)
            {
                var go = new GameObject("Flame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var frt = (RectTransform)go.transform;
                frt.SetParent(_art, false);
                frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);
                // свисает от сопла вниз и НЕ зеркалится: языки нарисованы вверх,
                // как у всякого огня, и разворачивать их было незачем
                frt.pivot = new Vector2(0.5f, 1f);
                _flameImg = go.GetComponent<Image>();
                _flameImg.sprite = SpriteBank.RawPoint("flame");
                _flameImg.raycastTarget = false;
                Warp();
            }

            if (!on) { _flameImg.enabled = false; return; }

            float t = Time.unscaledTime;
            float k = Mathf.Clamp01(low);
            k = k * k * (3f - 2f * k);
            float n1 = Mathf.PerlinNoise(t * 4.1f, 0.3f);

            float len = Mathf.Lerp(46f, 20f, k) * (0.82f + 0.28f * n1) * scale;
            float wide = Mathf.Lerp(30f, 52f, k) * (0.9f + 0.16f * n1) * scale;

            var rt = (RectTransform)_flameImg.transform;
            rt.anchoredPosition = new Vector2(x, -(y - 2f));
            rt.sizeDelta = new Vector2(wide, len);
            rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
            _flameImg.enabled = true;
            _flameImg.color = new Color(1f, 1f, 1f, 0.88f + 0.12f * n1);
        }

        private Image _flameImg;


        // Ловушки стоят в правом верхнем углу ЗДАНИЯ: это не обитатели уровня, им
        // незачем ходить по схеме, и в углу они не спорят за место с монстрами.
        private const float TrapX = 246f, TrapY = 32f, TrapStep = 26f;

        private void BuildTrapSlots()
        {
            // Ловушки раскиданы по ПОЛУ всего комплекса, а не сложены в углу:
            // верхний этаж на схеме крошечный, и там они не помещались.
            // Раскладка детерминированная — не прыгает от захода к заходу.
            for (int i = 0; i < 8; i++)
            {
                float f = (i * 0.618f) % 1f;                      // равномерно, но без сетки
                float x = Mathf.Lerp(RoomLeft + 24f, RoomRight - 24f, f);
                float y = RoomBottom - 18f - (i % 2) * 12f;       // у самого пола, через один чуть выше
                var sl = Slot(x, y);
                _trapSlots.Add(sl);
                _trapDots.Add(MakeMark(sl, "Trap"));
            }
        }

        /// <summary>
        /// Ловушки на схеме. Живут по тем же правилам, что и в панели: скрываются
        /// без скана, тают по дальности, растут от количества.
        /// </summary>
        private void UpdateTraps(string[] traps)
        {
            var shown = new List<string>();
            var seen = new List<string>();
            if (traps != null)
                foreach (var raw in traps)
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    string key = MobRailWidget.TrapIconPublic(raw);
                    if (string.IsNullOrEmpty(key)) continue;
                    int at = seen.IndexOf(key);
                    if (at >= 0)
                    {
                        float dn = DistOf(raw), dp = DistOf(shown[at]);
                        if (dn >= 0f && (dp < 0f || dn < dp)) shown[at] = raw;
                        continue;
                    }
                    seen.Add(key);
                    shown.Add(raw);
                }

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _trapDots.Count; i++)
            {
                var img = _trapDots[i];
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
                float dist = DistOf(raw);
                // ловушки тоже закрашиваются с приближением
                float nearT = dist >= 0f ? Mathf.InverseLerp(40f, 4f, dist) : 0f;
                while (_trapNear.Count <= i) _trapNear.Add(0f);
                _trapNear[i] = Mathf.MoveTowards(_trapNear[i], nearT, Time.unscaledDeltaTime / 0.7f);

                var spr = TrapIconFilled(seen[i], _trapNear[i]);
                if (spr != null && img.sprite != spr) img.sprite = spr;

                float sc = 1.5f;   // в полтора раза крупнее монстров
                if (ConfigSettings.ScaleMonstersByCount.Value)
                    sc *= Mathf.Min(1.6f, 1f + 0.16f * (CountOf(raw) - 1));
                rt.localScale = new Vector3(sc, sc, 1f);
                rt.anchoredPosition = Vector2.zero;

                float a = 1f;
                if (ConfigSettings.ProximityFade.Value && dist >= 0f)
                    a = Mathf.Lerp(0.28f, 1f, Mathf.InverseLerp(34f, 6f, dist));

                var col = MobRailWidget.IconTint(S);
                col.a = Mathf.MoveTowards(img.color.a, a, dt * 4f);
                img.color = col;
            }
        }

        /// <summary>Иконка ловушки с нутрянкой по близости — как у монстров.</summary>
        private static Sprite TrapIconFilled(string key, float near)
        {
            try
            {
                if (string.IsNullOrEmpty(key)) return null;
                if (!MobRailWidget.TintedIconStylePublic()) return SpriteBank.Get(key);
                float k = Mathf.Clamp01(near);
                k = k * k * (3f - 2f * k);
                int lvl = Mathf.RoundToInt(Mathf.Lerp(4f, SpriteBank.FillLevels, k));
                return SpriteBank.GetOutlineFilled(key, lvl);
            }
            catch { return SpriteBank.Get(key); }
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
        private MapWeatherFx _fx;

        /// <summary>
        /// Погода теперь ПРОИСХОДИТ на схеме, а не обозначается значком: дождь льёт
        /// и разбивается о землю, гроза бьёт молнией, вода поднимается вместе с
        /// настоящей, солнце с луной идут по небу, туман плывёт полосами и наводит
        /// помехи. Значки остались как запасной режим (MapWeatherMode = Icons).
        /// </summary>
        private void UpdateWeather(BridgePayload p, float dt)
        {
            string w = (p.weatherFull ?? "").ToLowerInvariant();
            string ev = (p.brutalEvent ?? "").ToLowerInvariant();

            var kinds = new List<string>();
            void Add(string k) { if (!kinds.Contains(k)) kinds.Add(k); }
            if (w.Contains("eclips") || w.Contains("затмен")) Add("eclipse");
            if (w.Contains("flood") || w.Contains("потоп")) Add("flood");
            if (w.Contains("storm") || w.Contains("гроз")) Add("storm");
            if (w.Contains("rain") || w.Contains("дожд")) Add("rain");
            if (w.Contains("fog") || w.Contains("туман")) Add("fog");
            if (w.Contains("dust") || w.Contains("пыл")) Add("dust");
            // метеоритный дождь приходит ивентом, а не погодой
            if (ev.Contains("meteor") || ev.Contains("метеор")) Add("meteor");
            if (kinds.Count == 0 && w.Length > 0 && w != "none" && w != "clear") Add("unknown");

            // туман В КОМПЛЕКСЕ — это ивент, и полосы с помехами должны быть внутри
            bool fogInside = ev.Contains("fog") || ev.Contains("туман");
            if (fogInside) Add("fog");

            string mode = (ConfigSettings.MapWeatherMode.Value ?? "Live").Trim().ToLowerInvariant();
            // «Schematic» — прежнее значение настройки; иначе у всех, кто
            // настраивался раньше, так и остались бы значки вместо анимации
            bool icons = mode == "icons";

            string key = string.Join(",", kinds.ToArray()) + "|" + (icons ? "i" : "l") + (fogInside ? "F" : "");
            if (w != _wxRaw)
            {
                _wxRaw = w;
                Plugin.Log?.LogInfo($"[map] погода: \"{p.weatherFull}\" -> " +
                                    (kinds.Count > 0 ? string.Join(",", kinds.ToArray()) : "нечего рисовать"));
            }

            if (key != _wxKind)
            {
                _wxKind = key;
                foreach (var b in _weatherBits) if (b != null) Destroy(b.gameObject);
                _weatherBits.Clear();

                if (icons)
                {
                    _fx?.Rebuild(new List<string>(), false);
                    for (int i = 0; i < kinds.Count; i++) DrawWeatherGlyph(kinds[i], 18f, 16f + i * 40f);
                }
                else
                {
                    if (_fx == null) { _fx = new MapWeatherFx(); _fx.Init(_art, S, W, H, Ground); }
                    _fx.Rebuild(kinds, fogInside);
                }
            }

            if (!icons && _fx != null)
                _fx.Tick(dt, RoomTop, RoomBottom, CaveTop, CaveBottom,
                         BuildX1, BuildX2, BuildRoofY);
        }

        // Границы зон на холсте — эффекты по ним ориентируются. Значения совпадают с
        // тем, что нарисовано; при своём макете правятся здесь же.
        private const float RoomTop = 158f, RoomBottom = 236f;
        private const float RoomLeft = 30f, RoomRight = 318f;
        private const float BuildX1 = 192f, BuildX2 = 318f, BuildRoofY = 22f;
        private const float CaveTop = 250f, CaveBottom = 384f;

        /// <summary>Помехи от тумана для иконок монстров: 0 нет, 1 максимум.</summary>
        private float FogNoise => _fx != null ? _fx.FogAmount : 0f;
        private bool FogInsideNow => _fx != null && _fx.FogInside;

        /// <summary>Значок погоды — теми же линиями, что и вся схема.</summary>
        private void DrawWeatherGlyph(string kind, float gx, float gy)
        {
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
            bool fog = _wxKind.StartsWith("fog");
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
