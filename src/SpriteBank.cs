using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Спрайты иконок монстров/ловушек, встроенные в DLL (res/mobs/*.png).
    /// Загружаются лениво и кэшируются; отсутствующий ключ — null (монстр
    /// без иконки просто не показывается, как в HTML-оверлее).
    /// </summary>
    public static class SpriteBank
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Иконка в выбранном игроком стиле. Все стили выводятся из одной и той же
        /// картинки, поэтому набор не надо рисовать трижды и он не расходится.
        /// </summary>
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            string style = (ConfigSettings.MonsterIconStyle.Value ?? "Render").Trim().ToLowerInvariant();
            if (style == "render" || style.Length == 0) return GetRaw(key);

            string ck = style + ":" + key;
            if (_styled.TryGetValue(ck, out var got)) return got;

            Sprite made = null;
            try
            {
                var src = GetRaw(key);
                if (src != null && src.texture != null)
                {
                    switch (style)
                    {
                        case "pixel":  made = Pixelate(src.texture, 26); break;   // 8-битный вид
                        case "vector": made = Outline(src.texture); break;        // контур, линейная графика
                        case "symbol": made = Silhouette(src.texture); break;     // сплошной символ
                        default:       made = null; break;
                    }
                }
            }
            catch { }
            if (made == null) made = GetRaw(key);
            _styled[ck] = made;
            return made;
        }

        private static readonly Dictionary<string, Sprite> _styled = new Dictionary<string, Sprite>();

        /// <summary>
        /// Картинка как есть, точечной фильтрацией и без стилей: так рисуют
        /// пиксель-арт, принесённый игроком, — огонь доставщика, например.
        /// </summary>
        public static Sprite RawPoint(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Sprite got;
            if (_point.TryGetValue(key, out got)) return got;
            var src = GetRaw(key);
            if (src != null && src.texture != null)
            {
                src.texture.filterMode = FilterMode.Point;
                got = src;
            }
            _point[key] = got;
            return got;
        }

        private static readonly Dictionary<string, Sprite> _point = new Dictionary<string, Sprite>();

        /// <summary>
        /// Векторный контур картинки независимо от выбранного стиля иконок. Нужен
        /// огню доставщика: он должен быть из тех же линий, что и вся схема, даже
        /// когда монстры показаны как есть.
        /// </summary>
        public static Sprite VectorOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Sprite got;
            if (_vector.TryGetValue(key, out got)) return got;
            try
            {
                var src = GetRaw(key);
                if (src != null && src.texture != null) got = Outline(src.texture);
            }
            catch { }
            _vector[key] = got;
            return got;
        }

        private static readonly Dictionary<string, Sprite> _vector = new Dictionary<string, Sprite>();

        /// <summary>Исходная картинка из ресурсов, без стилизации.</summary>
        public static Sprite GetRaw(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_cache.TryGetValue(key, out var cached)) return cached;

            Sprite sprite = null;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var stream = asm.GetManifestResourceStream($"LCBridgeOverlay.res.mobs.{key}.png");
                if (stream != null)
                {
                    byte[] bytes;
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                    stream.Dispose();

                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, bytes))
                    {
                        tex.filterMode = FilterMode.Bilinear;
                        sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            catch { }
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Вариант иконки с НАЛОЖЕННЫМИ КРОВАВЫМИ ПЯТНАМИ (для slayer/камикадзе).
        /// Раньше такие версии просто заливались красным — по ТЗ нужно пятна.
        ///
        /// Пятна рисуем поверх ТОЛЬКО непрозрачных пикселей исходной иконки, поэтому
        /// кровь всегда лежит по силуэту монстра и не торчит прямоугольником.
        /// Рисунок детерминированный (сид от имени), результат кэшируется.
        /// </summary>
        public static Sprite GetBloody(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string ck = key + "#blood";
            if (_cache.TryGetValue(ck, out var cached)) return cached;

            Sprite result = null;
            try
            {
                var base_ = GetRaw(key);
                if (base_ != null && base_.texture != null)
                {
                    var src = base_.texture;
                    int w = src.width, h = src.height;
                    var px = src.GetPixels32();

                    // границы силуэта — пятна ставим внутри него
                    int minX = w, minY = h, maxX = -1, maxY = -1;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (px[y * w + x].a > 24)
                            {
                                if (x < minX) minX = x; if (x > maxX) maxX = x;
                                if (y < minY) minY = y; if (y > maxY) maxY = y;
                            }

                    if (maxX >= minX)
                    {
                        var rnd = new System.Random(key.GetHashCode());
                        int bw = maxX - minX + 1, bh = maxY - minY + 1;
                        // много мелких клякс по ВСЕЙ площади силуэта (а не 4-10 пятен)
                        int blobs = Mathf.Clamp((bw * bh) / 900, 18, 60);
                        float unit = Mathf.Max(3f, Mathf.Min(bw, bh) * 0.13f);

                        for (int i = 0; i < blobs; i++)
                        {
                            float cx = minX + (float)rnd.NextDouble() * bw;
                            float cy = minY + (float)rnd.NextDouble() * bh;
                            float r = unit * (0.35f + (float)rnd.NextDouble() * 1.1f);
                            float squash = 0.7f + (float)rnd.NextDouble() * 0.6f;
                            Splat(px, w, h, cx, cy, r, squash);
                            // брызги вокруг кляксы
                            int spd = 2 + rnd.Next(4);
                            for (int s = 0; s < spd; s++)
                            {
                                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                                float dd = r * (1.1f + (float)rnd.NextDouble() * 1.8f);
                                Splat(px, w, h, cx + Mathf.Cos(ang) * dd, cy + Mathf.Sin(ang) * dd,
                                      Mathf.Max(1f, r * 0.3f), 1f);
                            }
                            // подтёк вниз
                            if (rnd.NextDouble() < 0.5)
                            {
                                float dl = r * (1.2f + (float)rnd.NextDouble() * 2.4f);
                                float dw = Mathf.Max(1f, r * 0.26f);
                                for (float t = 0; t < dl; t += 1f)
                                    Splat(px, w, h, cx, cy - t, dw * (1f - t / dl * 0.65f), 1f);
                            }
                        }

                        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        tex.filterMode = FilterMode.Bilinear;
                        tex.SetPixels32(px);
                        tex.Apply();
                        result = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            catch { }
            _cache[ck] = result;
            return result;
        }

        // мягкая клякса крови; красим ТОЛЬКО там, где исходник непрозрачен
        /// <summary>
        /// Спрайт из файла на диске — чтобы свою картинку можно было принести, не
        /// пересобирая мод. Стиль (пиксель, контур, силуэт) применяется тот же, что
        /// и к встроенным иконкам.
        /// </summary>
        public static Sprite FromFile(string path, string key, bool solid = false)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string ck = (solid ? "extS:" : "ext:") + key +
                        ":" + (ConfigSettings.MonsterIconStyle.Value ?? "Render");
            if (_styled.TryGetValue(ck, out var got)) return got;

            Sprite made = null;
            try
            {
                if (System.IO.File.Exists(path))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path)))
                    {
                        KeyOutWhite(tex);      // фон у присланных картинок обычно белый
                        string st = (ConfigSettings.MonsterIconStyle.Value ?? "Render").Trim().ToLowerInvariant();
                        if (solid) made = Silhouette(tex);
                        else if (st == "pixel") made = Pixelate(tex, 26);
                        else if (st == "vector") made = Outline(tex);
                        else if (st == "symbol") made = Silhouette(tex);
                        else
                        {
                            tex.filterMode = FilterMode.Bilinear;
                            tex.Apply(false, false);
                            made = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                                 new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                        }
                    }
                }
            }
            catch { }
            _styled[ck] = made;
            return made;
        }

        /// <summary>
        /// Убрать белый фон. Картинки из интернета приходят на белом, а на схеме
        /// нужен прозрачный — иначе вокруг корабля висел бы белый прямоугольник.
        /// </summary>
        private static void KeyOutWhite(Texture2D tex)
        {
            try
            {
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (c.a == 0) continue;
                    if (c.r > 235 && c.g > 235 && c.b > 235) px[i] = new Color32(0, 0, 0, 0);
                }
                tex.SetPixels32(px);
                tex.Apply(false, false);
            }
            catch { }
        }

        private static Sprite _noise, _glow, _flame;

        /// <summary>
        /// Тёмный шум. Накрываем им внутренности комплекса, когда вырублен свет:
        /// разглядеть, что там творится, становится труднее — как и должно быть.
        /// </summary>
        public static Sprite Noise()
        {
            if (_noise != null) return _noise;
            const int N = 96;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };
            var px = new Color32[N * N];
            var rnd = new System.Random(20250903);
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)rnd.Next(0, 60);
                byte a = (byte)rnd.Next(90, 220);
                px[i] = new Color32(v, v, v, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _noise = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0,
                                   SpriteMeshType.FullRect);
            return _noise;
        }

        /// <summary>
        /// Свет лампы. Пятно ОБНИМАЕТ лампу со всех сторон и немного стекает вниз.
        ///
        /// Нарочно грубое: сетка в два десятка клеток, точечная фильтрация и
        /// прозрачность в дюжину ступеней — свет складывается из крупных пикселей,
        /// как и всё остальное на схеме, а не размазывается градиентом.
        /// </summary>
        public static Sprite Glow()
        {
            if (_glow != null) return _glow;
            const int N = 22;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,      // крупный пиксель, не мыло
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x / (float)(N - 1) - 0.5f) * 2f;
                    float dy = (y / (float)(N - 1) - 0.5f) * 2f;   // +1 верх, -1 низ
                    float sy = dy > 0f ? dy / 0.8f : dy / 1.05f;   // чуть охотнее вниз
                    float d = Mathf.Sqrt(dx * dx + sy * sy);
                    float a2 = Mathf.Pow(Mathf.Clamp01(1f - d), 1.9f);
                    a2 = Mathf.Round(a2 * 12f) / 12f;              // ступени, а не плавность
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a2 * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _glow = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0,
                                  SpriteMeshType.FullRect);
            return _glow;
        }

        /// <summary>
        /// Полотно полумрака: книзу глухое, кверху сходит на нет. Линия схода
        /// может идти НАКЛОННО (slope — на сколько долей высоты она опускается
        /// от левого края к правому), чтобы лечь по рельефу схемы.
        ///
        /// Зерно замешано прямо в полотно: отдельной картинкой шума его было бы
        /// не помножить на прозрачность средствами обычного UI.
        /// </summary>
        public static Sprite GloomGrad(float slope)
        {
            int key = Mathf.RoundToInt(slope * 50f);
            Sprite cached;
            if (_gloomGrads.TryGetValue(key, out cached) && cached != null) return cached;

            const int Wd = 44, Ht = 58;
            var tex = new Texture2D(Wd, Ht, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,      // крупный пиксель, как у схемы
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[Wd * Ht];
            var rnd = new System.Random(20260101);
            float sl = key / 50f;
            for (int y = 0; y < Ht; y++)
                for (int x = 0; x < Wd; x++)
                {
                    float u = x / (float)(Wd - 1);
                    float v = 1f - y / (float)(Ht - 1);           // 0 вверху … 1 внизу
                    float vv = v - (u - 0.5f) * sl;               // наклон линии схода
                    // растянутый плавный набор плотности: резкой кромки нет нигде
                    float k = Mathf.Clamp01(vv / 0.45f);
                    k = k * k * (3f - 2f * k);
                    float a2 = k * (0.76f + 0.24f * (float)rnd.NextDouble());
                    a2 = Mathf.Round(a2 * 10f) / 10f;      // ступени плотности
                    px[y * Wd + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a2) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var spr = Sprite.Create(tex, new Rect(0, 0, Wd, Ht), new Vector2(0.5f, 1f), 100f, 0,
                                    SpriteMeshType.FullRect);
            _gloomGrads[key] = spr;
            return spr;
        }

        private static readonly System.Collections.Generic.Dictionary<int, Sprite> _gloomGrads =
            new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>
        /// Язык пламени горящей нефти: толстый, с округлым концом и неровными
        /// боками, а не ровный клин турбины. Тёмный у кромок и по низу — копоть,
        /// светлый у корня.
        /// </summary>
        public static Sprite Flame()
        {
            if (_flame != null) return _flame;
            const int W2 = 64, H2 = 96;
            var tex = new Texture2D(W2, H2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[W2 * H2];
            for (int y = 0; y < H2; y++)
                for (int x = 0; x < W2; x++)
                {
                    float ny = y / (float)(H2 - 1);          // 0 низ (конец) … 1 верх (корень)
                    // округлый конец снизу и раздутое пузом тело — форма капли,
                    // перевёрнутой вниз
                    float tip = Mathf.Sqrt(Mathf.Clamp01(ny / 0.24f));
                    float belly = 0.58f + 0.42f * Mathf.Sin(Mathf.Clamp01(ny) * Mathf.PI * 0.92f);
                    float lobes = 1f + 0.09f * Mathf.Sin(ny * 11f);   // неровные бока
                    float half = 0.5f * tip * belly * lobes;

                    float dx = Mathf.Abs(x / (float)(W2 - 1) - 0.5f);
                    float k = Mathf.Clamp01(1f - dx / Mathf.Max(0.001f, half));
                    k = k * k * (3f - 2f * k);

                    // копоть по краям и у конца, жар у корня
                    var soot = new Color(0.22f, 0.05f, 0.02f);
                    var red = new Color(0.90f, 0.20f, 0.03f);
                    var hot = new Color(1f, 0.78f, 0.30f);
                    var col = Color.Lerp(soot, red, Mathf.Clamp01(ny * 1.5f));
                    col = Color.Lerp(col, hot, Mathf.Clamp01(k * ny * 1.3f));
                    px[y * W2 + x] = new Color(col.r, col.g, col.b,
                                               Mathf.Pow(k, 0.75f) * Mathf.Lerp(0.5f, 1f, ny));
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _flame = Sprite.Create(tex, new Rect(0, 0, W2, H2), new Vector2(0.5f, 1f), 100f, 0,
                                   SpriteMeshType.FullRect);
            return _flame;
        }

        // ================= стили иконок =================
        // Все три выводятся из исходной картинки. Так набор не надо рисовать трижды,
        // он не расходится между стилями и автоматически покрывает новых монстров.

        private static Sprite Wrap(Texture2D t, FilterMode fm)
        {
            t.filterMode = fm;
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(false, false);
            return Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect);
        }

        /// <summary>
        /// 8-битный вид: уменьшаем до нескольких десятков пикселей по большей стороне
        /// и ставим точечную фильтрацию. Цвета квантуем по 5 уровней на канал —
        /// без этого получалась бы просто мелкая, но всё ещё «фотографическая» картинка.
        /// </summary>
        private static Sprite Pixelate(Texture2D src, int maxSide)
        {
            int w = src.width, h = src.height;
            float k = (float)maxSide / Mathf.Max(w, h);
            int nw = Mathf.Max(4, Mathf.RoundToInt(w * k));
            int nh = Mathf.Max(4, Mathf.RoundToInt(h * k));

            var srcPx = src.GetPixels32();
            var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            var outPx = new Color32[nw * nh];

            int bx = Mathf.Max(1, w / nw), by = Mathf.Max(1, h / nh);
            for (int y = 0; y < nh; y++)
            {
                for (int x = 0; x < nw; x++)
                {
                    // усредняем блок исходника — иначе на тонких деталях каша
                    int r = 0, g = 0, b = 0, a = 0, n = 0;
                    int x0 = x * w / nw, y0 = y * h / nh;
                    for (int yy = y0; yy < Mathf.Min(y0 + by, h); yy++)
                        for (int xx = x0; xx < Mathf.Min(x0 + bx, w); xx++)
                        {
                            var c = srcPx[yy * w + xx];
                            if (c.a < 8) { n++; continue; }      // прозрачное не тянет цвет вниз
                            r += c.r; g += c.g; b += c.b; a += c.a; n++;
                        }
                    if (n == 0) { outPx[y * nw + x] = new Color32(0, 0, 0, 0); continue; }
                    byte aa = (byte)(a / n);
                    if (aa < 40) { outPx[y * nw + x] = new Color32(0, 0, 0, 0); continue; }
                    int cnt = Mathf.Max(1, n);
                    outPx[y * nw + x] = new Color32(Q(r / cnt), Q(g / cnt), Q(b / cnt), 255);
                }
            }
            dst.SetPixels32(outPx);
            return Wrap(dst, FilterMode.Point);
        }

        /// <summary>
        /// Залитый силуэт того же ключа. Нужен, когда монстр совсем рядом: контур
        /// на таком расстоянии читается плохо, а сплошное пятно — сразу.
        /// В остальных стилях просто возвращает обычную иконку.
        /// </summary>
        public static Sprite GetSolid(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string st = (ConfigSettings.MonsterIconStyle.Value ?? "Render").Trim().ToLowerInvariant();
            if (st != "vector") return Get(key);

            string ck = "solid:" + key;
            if (_styled.TryGetValue(ck, out var got)) return got;
            Sprite made = null;
            try
            {
                var src = GetRaw(key);
                if (src != null && src.texture != null) made = Silhouette(src.texture);
            }
            catch { }
            if (made == null) made = Get(key);
            _styled[ck] = made;
            return made;
        }

        /// <summary>
        /// Контур + залитая нутрянка одной картинкой.
        ///
        /// Заливка раньше была отдельным Image поверх иконки, и сколько её ни
        /// подгоняй — она всё равно расходилась с контуром. Здесь расходиться
        /// нечему: обе части лежат в ОДНОМ спрайте. Уровень 0..Levels задаёт, на
        /// сколько плотна нутрянка; шагов достаточно, чтобы переход читался плавным.
        /// </summary>
        public const int FillLevels = 16;

        public static Sprite GetOutlineFilled(string key, int level)
        {
            if (string.IsNullOrEmpty(key)) return null;
            level = Mathf.Clamp(level, 0, FillLevels);
            string ck = "of:" + key + ":" + level;
            if (_styled.TryGetValue(ck, out var got)) return got;

            Sprite made = null;
            try
            {
                var src = GetRaw(key);
                if (src != null && src.texture != null)
                {
                    var tex = src.texture;
                    int w = tex.width, h = tex.height;
                    var px = tex.GetPixels32();
                    var outPx = new Color32[w * h];
                    byte inner = (byte)Mathf.RoundToInt(255f * level / FillLevels);

                    bool Solid(int x, int y) =>
                        x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 90;

                    int th = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(w, h) / 55f));
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            if (!Solid(x, y)) { outPx[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                            bool edge = false;
                            for (int d = 1; d <= th && !edge; d++)
                                edge = !Solid(x - d, y) || !Solid(x + d, y) ||
                                       !Solid(x, y - d) || !Solid(x, y + d);
                            outPx[y * w + x] = new Color32(255, 255, 255, edge ? (byte)255 : inner);
                        }

                    var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    dst.SetPixels32(outPx);
                    made = Wrap(dst, FilterMode.Bilinear);
                }
            }
            catch { }
            if (made == null) made = Get(key);
            _styled[ck] = made;
            return made;
        }

        /// <summary>Квантование канала до 5 ступеней — узнаваемая палитра старых игр.</summary>
        private static byte Q(int v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v / 51f) * 51, 0, 255);

        /// <summary>
        /// Линейная графика: берём границу силуэта (пиксель непрозрачный, а сосед нет)
        /// и рисуем только её. Получается чистый контур, который красится в цвет темы.
        /// </summary>
        private static Sprite Outline(Texture2D src)
        {
            int w = src.width, h = src.height;
            var px = src.GetPixels32();
            var outPx = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            var line = new Color32(255, 255, 255, 255);

            bool Solid(int x, int y) =>
                x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 90;

            // толще, чем было: на 110 контур получался волосяным и терялся
            int th = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(w, h) / 55f));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Solid(x, y)) { outPx[y * w + x] = clear; continue; }
                    bool edge = false;
                    for (int d = 1; d <= th && !edge; d++)
                        edge = !Solid(x - d, y) || !Solid(x + d, y) || !Solid(x, y - d) || !Solid(x, y + d);
                    outPx[y * w + x] = edge ? line : clear;
                }

            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.SetPixels32(outPx);
            return Wrap(dst, FilterMode.Bilinear);
        }

        /// <summary>
        /// Символ: сплошной силуэт белым. Цвет ему даёт сам оверлей через Image.color,
        /// поэтому знак всегда в тон теме.
        /// </summary>
        private static Sprite Silhouette(Texture2D src)
        {
            int w = src.width, h = src.height;
            var px = src.GetPixels32();
            var outPx = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
                outPx[i] = px[i].a > 90 ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);

            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.SetPixels32(outPx);
            return Wrap(dst, FilterMode.Bilinear);
        }

        private static void Splat(Color32[] px, int w, int h, float cx, float cy, float r, float squash)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r * squash)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + r * squash));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w + x;
                if (px[i].a <= 24) continue;                 // вне силуэта — не пачкаем
                float dx = (x - cx) / Mathf.Max(0.01f, r);
                float dy = (y - cy) / Mathf.Max(0.01f, r * squash);
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                float k = Mathf.Clamp01(1f - d);
                k = k * k * 0.96f;                            // плотное ядро, мягкий край
                // тёмная запёкшаяся кровь
                px[i].r = (byte)Mathf.Lerp(px[i].r, 68f, k);
                px[i].g = (byte)Mathf.Lerp(px[i].g, 3f, k);
                px[i].b = (byte)Mathf.Lerp(px[i].b, 6f, k);
            }
        }
    }
}
