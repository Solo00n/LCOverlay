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

        public static Sprite Get(string key)
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
                var base_ = Get(key);
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
