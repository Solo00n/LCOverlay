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
