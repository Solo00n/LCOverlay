using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Погода на схеме — не значками, а происходящим.
    ///
    /// Дождь льёт из туч и разбивается о землю, гроза добавляет к нему молнии,
    /// затопление поднимает и опускает воду вместе с настоящим, затмение ведёт
    /// закрытое луной солнце по небу вслед за игровым временем, туман плывёт
    /// полосами и наводит помехи на иконки, метеоритный дождь роняет метеоры,
    /// которые разбиваются о поверхность.
    ///
    /// Всё теми же линиями, что и сама схема, поэтому эффекты выглядят её частью,
    /// а не наклейкой поверх.
    /// </summary>
    internal class MapWeatherFx
    {
        private RectTransform _host;
        private OverlayStyle S;
        private float W, H, Ground;

        // ---- элементы ----
        private class Drop { public RectTransform Rt; public Image Img; public float X, Y, Sp, Len; }
        private class Splash { public RectTransform A, B; public Image ImgA, ImgB; public float Life = -1f; public float X; }
        private class Meteor { public RectTransform[] Seg; public Image[] Img; public float X, Y, Sp; }

        private readonly List<Drop> _drops = new List<Drop>();
        private readonly List<Splash> _splash = new List<Splash>();
        private readonly List<Image> _fogLines = new List<Image>();
        private readonly List<Meteor> _meteors = new List<Meteor>();
        private readonly List<Image> _bolt = new List<Image>();
        private readonly List<Image> _sun = new List<Image>();
        private readonly List<Image> _moon = new List<Image>();
        private Image _waterLine, _waterRipple;

        private bool _rain, _storm, _flood, _eclipse, _fog, _meteor;
        private float _boltAt = -99f, _boltNext = 2.5f;
        private float _bx1, _bx2, _broof;   // здание: дождь падает НА него

        /// <summary>Сила помех от тумана: 0 нет, 1 максимум. Её берут иконки монстров.</summary>
        public float FogAmount { get; private set; }

        /// <summary>
        /// Цвет конкретной погоды из настроек. Имя, #RRGGBB или Theme.
        /// </summary>
        private Color WxColor(string kind, float alpha = 1f)
        {
            string v = "Theme";
            switch (kind)
            {
                case "rain": v = ConfigSettings.ColorRain.Value; break;
                case "storm": v = ConfigSettings.ColorStorm.Value; break;
                case "flood": v = ConfigSettings.ColorFlood.Value; break;
                case "eclipse": v = ConfigSettings.ColorEclipse.Value; break;
                case "fog": v = ConfigSettings.ColorFog.Value; break;
                case "dust": v = ConfigSettings.ColorDust.Value; break;
                case "meteor": v = ConfigSettings.ColorMeteor.Value; break;
            }
            var c = Parse(v);
            c.a = alpha;
            return c;
        }

        private Color Parse(string v)
        {
            v = (v ?? "Theme").Trim();
            if (v.StartsWith("#") && ColorUtility.TryParseHtmlString(v, out var hex)) return hex;
            switch (v.ToLowerInvariant())
            {
                case "red": return new Color(1f, 0.30f, 0.26f);
                case "blue": return new Color(0.42f, 0.62f, 1f);
                case "white": return new Color(0.92f, 0.94f, 1f);
                case "yellow": return new Color(1f, 0.83f, 0.25f);
                case "green": return new Color(0.42f, 0.95f, 0.55f);
                default: return S != null ? S.Frame : Color.white;
            }
        }

        /// <summary>Туман внутри комплекса (ивент) — помехи и полосы там же.</summary>
        public bool FogInside { get; private set; }

        public void Init(RectTransform art, OverlayStyle style, float w, float h, float ground)
        {
            S = style; W = w; H = h; Ground = ground;
            var go = new GameObject("Weather", typeof(RectTransform));
            _host = (RectTransform)go.transform;
            _host.SetParent(art, false);
            _host.anchorMin = _host.anchorMax = new Vector2(0f, 1f);
            _host.pivot = new Vector2(0f, 1f);
            _host.anchoredPosition = Vector2.zero;
            _host.sizeDelta = new Vector2(w, h);
        }

        // ---------- примитив ----------
        private Image Line(float x1, float y1, float x2, float y2, float th, Color col)
        {
            var go = new GameObject("Wx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_host, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            Place(rt, x1, y1, x2, y2, th);
            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        private static void Place(RectTransform rt, float x1, float y1, float x2, float y2, float th)
        {
            var d = new Vector2(x2 - x1, -(y2 - y1));
            rt.anchoredPosition = new Vector2(x1, -y1);
            rt.sizeDelta = new Vector2(Mathf.Max(0.5f, d.magnitude), th);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        // ---------- сборка под набор погод ----------
        public void Rebuild(List<string> kinds, bool fogInside)
        {
            for (int i = _host.childCount - 1; i >= 0; i--) Object.Destroy(_host.GetChild(i).gameObject);
            _drops.Clear(); _splash.Clear(); _fogLines.Clear(); _meteors.Clear();
            _bolt.Clear(); _sun.Clear(); _moon.Clear();
            _waterLine = _waterRipple = null;

            _rain = kinds.Contains("rain");
            _storm = kinds.Contains("storm");
            _flood = kinds.Contains("flood");
            _eclipse = kinds.Contains("eclipse");
            _fog = kinds.Contains("fog");
            _meteor = kinds.Contains("meteor");
            FogInside = fogInside;

            var c = S.Frame;
            var soft = OverlayStyle.WithA(S.Frame, 0.55f);

            if (_rain || _storm)
                for (int i = 0; i < 20; i++)
                {
                    // дождь всегда своего цвета: в грозу жёлтые капли выглядели нелепо,
                    // грозовой цвет достаётся только молнии
                    var img = Line(0f, 0f, 3f, 9f, 1.5f, WxColor("rain", 0.75f));
                    _drops.Add(new Drop
                    {
                        Rt = (RectTransform)img.transform, Img = img,
                        X = Random.Range(4f, W - 4f),
                        Y = Random.Range(-40f, Ground),
                        Sp = Random.Range(150f, 230f),
                        Len = Random.Range(7f, 12f),
                    });
                }

            if (_storm)
                // молния толще и ярче: тонкая на схеме терялась и её просто не замечали
                for (int i = 0; i < 6; i++)
                {
                    var b = Line(0f, 0f, 1f, 1f, 4f, WxColor("storm"));
                    b.enabled = false;
                    _bolt.Add(b);
                }

            if (_meteor)
                for (int i = 0; i < 4; i++)
                {
                    var seg = new RectTransform[3];
                    var im = new Image[3];
                    for (int k = 0; k < 3; k++)
                    {
                        im[k] = Line(0f, 0f, 6f, 6f, 2.5f - k * 0.7f,
                                     WxColor("meteor", 0.9f - k * 0.28f));
                        seg[k] = (RectTransform)im[k].transform;
                    }
                    _meteors.Add(new Meteor
                    {
                        Seg = seg, Img = im,
                        X = Random.Range(10f, W - 60f),
                        Y = Random.Range(-120f, 0f),
                        Sp = Random.Range(110f, 170f),
                    });
                }

            // брызги — общий запас для дождя и метеоров
            if (_rain || _storm || _meteor)
                for (int i = 0; i < 10; i++)
                {
                    var a = Line(0f, 0f, 4f, -4f, 1.5f, WxColor(_meteor ? "meteor" : "rain", 0.6f));
                    var b = Line(0f, 0f, -4f, -4f, 1.5f, WxColor(_meteor ? "meteor" : "rain", 0.6f));
                    a.enabled = b.enabled = false;
                    _splash.Add(new Splash
                    {
                        A = (RectTransform)a.transform, B = (RectTransform)b.transform,
                        ImgA = a, ImgB = b,
                    });
                }

            if (_flood)
            {
                // Заливки больше нет: она красила пещеры и комнаты, а нужна только
                // сама линия воды с рябью.
                _waterLine = Line(0f, 0f, W, 0f, 2.5f, WxColor("flood", 0.9f));
                _waterRipple = Line(0f, 0f, W, 0f, 1.5f, WxColor("flood", 0.45f));
            }

            if (_eclipse)
            {
                for (int i = 0; i < 16; i++) _sun.Add(Line(0f, 0f, 1f, 1f, 2f, WxColor("eclipse")));
                for (int i = 0; i < 16; i++) _moon.Add(Line(0f, 0f, 1f, 1f, 2.5f, WxColor("eclipse", 0.55f)));
            }

            if (_fog)
                for (int i = 0; i < 7; i++)
                    _fogLines.Add(Line(0f, 0f, W, 0f, 2f, WxColor("fog", 0.28f)));
        }

        // ---------- ход ----------
        public void Tick(float dt, float roomTop, float roomBottom, float caveTop, float caveBottom,
                         float bx1, float bx2, float broof)
        {
            _bx1 = bx1; _bx2 = bx2; _broof = broof;
            float t = Time.unscaledTime;
            FogAmount = 0f;

            // ---- дождь ----
            foreach (var d in _drops)
            {
                d.Y += d.Sp * dt;
                // под зданием земли нет — там поверхность это крыша
                float surface = SurfaceAt(d.X);
                if (d.Y >= surface)
                {
                    PopSplash(d.X, surface);
                    d.Y = Random.Range(-30f, -4f);
                    d.X = Random.Range(4f, W - 4f);
                }
                Place(d.Rt, d.X, d.Y, d.X + 3f, d.Y + d.Len, 1.5f);
            }

            // ---- молния ----
            if (_storm)
            {
                if (t - _boltAt > _boltNext)
                {
                    _boltAt = t;
                    _boltNext = Random.Range(3.5f, 8f);
                    float bx = Random.Range(30f, W - 30f);
                    float y = 0f;
                    Plugin.Log?.LogInfo("[map] молния");
                    for (int i = 0; i < _bolt.Count; i++)
                    {
                        float ny = y + Ground / _bolt.Count;
                        float nx = bx + Random.Range(-14f, 14f);
                        Place((RectTransform)_bolt[i].transform, bx, y, nx, ny, 2.5f);
                        bx = nx; y = ny;
                    }
                }
                // держим вспышку заметно дольше и мигаем реже: прежние 0.22 с с
                // частым миганием на схеме просто не успевали попасться на глаза
                float age = t - _boltAt;
                bool on = age < 0.5f && Mathf.FloorToInt(age * 12f) % 2 == 0;
                foreach (var b in _bolt) b.enabled = on;
            }

            // ---- метеоры ----
            foreach (var m in _meteors)
            {
                m.Y += m.Sp * dt;
                float mx = m.X + m.Y * 0.45f;          // летят наискось
                if (m.Y >= Ground || mx > W)
                {
                    PopSplash(Mathf.Clamp(mx, 6f, W - 6f), Ground);
                    m.Y = Random.Range(-160f, -40f);
                    m.X = Random.Range(-20f, W - 80f);
                    m.Sp = Random.Range(110f, 170f);
                }
                for (int k = 0; k < m.Seg.Length; k++)
                {
                    float back = k * 9f;
                    Place(m.Seg[k], mx - back * 0.45f, m.Y - back,
                          mx - (back + 8f) * 0.45f, m.Y - back - 8f, 2.5f - k * 0.7f);
                }
            }

            // ---- брызги ----
            foreach (var sp in _splash)
            {
                if (sp.Life < 0f) continue;
                sp.Life += dt / 0.35f;
                if (sp.Life >= 1f)
                {
                    sp.Life = -1f;
                    sp.ImgA.enabled = sp.ImgB.enabled = false;
                    continue;
                }
                float k = sp.Life;
                float r = Mathf.Lerp(1f, 7f, k);
                Place(sp.A, sp.X, Ground, sp.X + r, Ground - r * 0.8f, 1.5f);
                Place(sp.B, sp.X, Ground, sp.X - r, Ground - r * 0.8f, 1.5f);
                var col = OverlayStyle.WithA(S.Frame, 0.6f * (1f - k));
                sp.ImgA.color = sp.ImgB.color = col;
            }

            // ---- затопление ----
            if (_waterLine != null)
            {
                // Вода стоит у самой поверхности и поднимается над ней, а не заливает
                // подземелье: пещеры и комнаты красить нельзя.
                float prog = FloodProgress();
                float y = Mathf.Lerp(Ground + 4f, Ground - 22f, prog);
                Place((RectTransform)_waterLine.transform, 8f, y, W - 8f, y, 2.5f);
                Place((RectTransform)_waterRipple.transform, 16f, y + 5f + Mathf.Sin(t * 1.6f) * 1.5f,
                      W - 16f, y + 5f + Mathf.Sin(t * 1.6f + 1f) * 1.5f, 1.5f);

            }

            // ---- затмение ----
            if (_sun.Count > 0)
            {
                float tod = TimeOfDayNorm();
                // солнце идёт дугой слева направо, как за день
                float cx = Mathf.Lerp(34f, W - 34f, tod);
                float cy = Mathf.Lerp(64f, 22f, Mathf.Sin(tod * Mathf.PI));
                Ring(_sun, cx, cy, 13f);
                // луна наползает: к середине дня закрывает полностью
                // Луна — шарик ВНУТРИ солнца: наползает по горизонтали и к середине
                // дня встаёт ровно по центру. Раньше она уезжала и вбок, и вверх, и
                // получались два разъехавшихся кружка.
                float cover = Mathf.Sin(tod * Mathf.PI);
                Ring(_moon, cx - Mathf.Lerp(11f, 0f, cover), cy, 9f);
            }

            // ---- туман ----
            if (_fogLines.Count > 0)
            {
                float top = FogInside ? roomTop + 6f : 10f;
                float span = FogInside ? (roomBottom - roomTop - 12f) : (Ground - 16f);
                for (int i = 0; i < _fogLines.Count; i++)
                {
                    float y = top + Mathf.Repeat(i * (span / _fogLines.Count) + t * 5f, span);
                    float x = Mathf.Sin(t * 0.5f + i) * 10f;
                    Place((RectTransform)_fogLines[i].transform, x, y, W + x, y, 2f);
                    _fogLines[i].color = OverlayStyle.WithA(S.FrameDim, 0.16f + 0.12f * Mathf.Sin(t * 0.8f + i));
                }
                FogAmount = 1f;
            }
        }

        private void Ring(List<Image> parts, float cx, float cy, float r)
        {
            int n = parts.Count;
            for (int i = 0; i < n; i++)
            {
                float a1 = i / (float)n * Mathf.PI * 2f;
                float a2 = (i + 1) / (float)n * Mathf.PI * 2f;
                float x1 = cx + Mathf.Cos(a1) * r, y1 = cy + Mathf.Sin(a1) * r;
                float x2 = cx + Mathf.Cos(a2) * r, y2 = cy + Mathf.Sin(a2) * r;
                // Заходя за здание, светило прячется ЗА ним, а не рисуется поверх.
                bool hidden = Behind(x1, y1) && Behind(x2, y2);
                parts[i].enabled = !hidden;
                if (!hidden) Place((RectTransform)parts[i].transform, x1, y1, x2, y2, 2f);
            }
        }

        /// <summary>Точка попадает на здание (значит перекрыта им).</summary>
        private bool Behind(float x, float y) =>
            x >= _bx1 && x <= _bx2 && y >= _broof && y <= Ground;

        /// <summary>Высота поверхности в этой точке: крыша здания либо земля.</summary>
        private float SurfaceAt(float x) =>
            (x >= _bx1 && x <= _bx2) ? _broof : Ground;

        private void PopSplash(float x, float y)
        {
            foreach (var sp in _splash)
            {
                if (sp.Life >= 0f) continue;
                sp.Life = 0f; sp.X = x;
                sp.ImgA.enabled = sp.ImgB.enabled = true;
                return;
            }
        }

        /// <summary>
        /// Насколько поднялась вода. Игра считает подъём как долю прошедшего дня
        /// (globalTime / 1080), поэтому берём ту же величину — линия на схеме растёт
        /// синхронно с настоящей водой.
        /// </summary>
        private static float FloodProgress()
        {
            try
            {
                var tod = TimeOfDay.Instance;
                if (tod == null) return 0f;
                return Mathf.Clamp01(tod.globalTime / 1080f);
            }
            catch { return 0f; }
        }

        private static float TimeOfDayNorm()
        {
            try
            {
                var tod = TimeOfDay.Instance;
                return tod != null ? Mathf.Clamp01(tod.normalizedTimeOfDay) : 0.5f;
            }
            catch { return 0.5f; }
        }
    }
}
