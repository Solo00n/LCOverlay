using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Схема локации, нарисованная картинками.
    ///
    /// Кладём PNG-слои в BepInEx/config/lcbridgeoverlay-map/ — и мод рисует их
    /// вместо своих линий. Слои одного размера накладываются друг на друга в
    /// порядке имён, поэтому набор из Aseprite ложится как есть.
    ///
    /// Роль слоя определяется по имени файла:
    ///   *lamp*   — лампы: цвет свой (жёлтый), яркость следует за светом в комплексе;
    ///   *cave*   — пещеры: красятся в цвет опасности темы;
    ///   *elev*   — кабина лифта: ездит вверх-вниз вместе с настоящей;
    ///   *cable*  — тросы лифта: показываются ровно до потолка кабины;
    ///   *guide*  — направляющий слой, в игру НЕ идёт (места монстров и т.п.);
    ///   остальные — конструкция, красится в цвет темы.
    ///
    /// Рисовать лучше БЕЛЫМ: картинка используется как маска, цвет даёт тема.
    /// Фильтрация точечная, поэтому пиксель-арт остаётся пиксель-артом.
    /// </summary>
    internal static class MapImages
    {
        public class Layer
        {
            public string Name;
            public Sprite Sprite;
            public Texture2D Tex;
            public bool IsLamp, IsCave, IsElevator, IsCable;

            /// <summary>
            /// На слое есть заведомо цветные пиксели — жёлтая лампа, серая
            /// вагонетка, крашеная кабина. Такое перекрашивать в цвет темы
            /// нельзя: получится краска поверх краски.
            /// </summary>
            public bool HasColor;

            internal Sprite Tinted, Lamps;

            /// <summary>
            /// Насколько плотно клетка занята ФОНОМ — полупрозрачной заливкой, а не
            /// линиями. По ней ложится полумрак: темнеть должен фон комплекса и
            /// пещер, а не обводка, лампы и рельсы поверх него.
            /// </summary>
            public float[] Backdrop;
            internal string TintKey;

            /// <summary>
            /// Непрозрачная часть слоя в долях холста, отсчёт сверху вниз. По ней
            /// мод узнаёт, где нарисована кабина лифта, не заставляя вписывать
            /// координаты руками.
            /// </summary>
            public Rect Bounds;

            /// <summary>Центры отдельных пятен слоя — по ним тянутся тросы.</summary>
            public List<Vector2> Blobs = new List<Vector2>();

            /// <summary>
            /// Центры ЖЁЛТЫХ пятен. Лампу узнаём по цвету, а не по имени файла:
            /// совмещённый макет приходит одной картинкой, и отдельного слоя ламп
            /// в нём нет — а свет под ними всё равно должен быть.
            /// </summary>
            public List<Vector2> LampBlobs = new List<Vector2>();
        }

        /// <summary>Сетка, в которой считается фон и по которой кроится полумрак.</summary>
        public const int GloomW = 220, GloomH = 268;

        /// <summary>Пиксель жёлтый — значит лампа, на каком бы слое он ни лежал.</summary>
        private static bool IsLampPixel(Color32 c)
        {
            return c.a > 8 && c.r > 150 && c.g > 120 && c.r - c.b > 80;
        }

        public static string Dir
        {
            get
            {
                try { return Path.Combine(BepInEx.Paths.ConfigPath, "lcbridgeoverlay-map"); }
                catch { return "lcbridgeoverlay-map"; }
            }
        }

        private static List<Layer> _cache;
        private static bool _scanned;

        /// <summary>Слои из папки, или null если её нет / она пуста.</summary>
        public static List<Layer> Load()
        {
            if (_scanned) return _cache;
            _scanned = true;
            try
            {
                if (!Directory.Exists(Dir))
                {
                    Directory.CreateDirectory(Dir);
                    WriteReadme();
                    return null;
                }

                var files = Directory.GetFiles(Dir, "*.png");
                if (files == null || files.Length == 0) return null;
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                var list = new List<Layer>();
                foreach (var f in files)
                {
                    string nm = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (nm.Contains("guide") || nm.Contains("preview") || nm.StartsWith("_")) continue;

                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(f))) continue;
                    tex.filterMode = FilterMode.Point;      // пиксель-арт остаётся чётким
                    tex.wrapMode = TextureWrapMode.Clamp;

                    var layer = new Layer
                    {
                        Name = nm,
                        Tex = tex,
                        Sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                               new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect),
                        IsLamp = nm.Contains("lamp"),
                        IsCave = nm.Contains("cave"),
                        IsElevator = nm.Contains("elev"),
                        IsCable = nm.Contains("cable") || nm.Contains("rope") || nm.Contains("tros"),
                    };
                    Measure(tex, layer);
                    list.Add(layer);
                }

                if (list.Count == 0) return null;
                Plugin.Log?.LogInfo($"[map] схема из картинок: слоёв {list.Count} ({Dir}).");
                _cache = list;
                return _cache;
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"[map] слои не прочитаны: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Обмерить слой: где на нём вообще что-то нарисовано и, если это лампы,
        /// на сколько отдельных пятен рисунок распадается.
        ///
        /// Всё в долях холста с отсчётом СВЕРХУ — в тех же единицах, в которых
        /// схема расставляет остальное.
        /// </summary>
        private static void Measure(Texture2D tex, Layer layer)
        {
            try
            {
                int w = tex.width, h = tex.height;
                var px = tex.GetPixels32();
                int x0 = w, y0 = h, x1 = -1, y1 = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 8)
                        {
                            if (x < x0) x0 = x;
                            if (x > x1) x1 = x;
                            if (y < y0) y0 = y;
                            if (y > y1) y1 = y;
                        }
                if (x1 < 0) return;

                // считаем, много ли на слое насыщенных пикселей: белую и серую
                // графику красим темой, а цветную оставляем как нарисована
                int opaque = 0, colored = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (c.a <= 8) continue;
                    opaque++;
                    int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    int mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    if (mx - mn > 30) colored++;
                }
                layer.HasColor = opaque > 0 && colored > opaque * 0.02f;

                // Фон — это ПОЛУПРОЗРАЧНАЯ заливка: в макете она белая с альфой 30,
                // а линии, лампы и рельсы кладут поверх неё в полную силу. Считаем,
                // насколько плотно фон занимает каждую клетку будущего полумрака.
                var back = new float[GloomW * GloomH];
                var hits = new int[GloomW * GloomH];
                for (int y = 0; y < h; y++)
                {
                    int gy = (int)((1f - (y + 0.5f) / h) * GloomH);
                    if (gy < 0) gy = 0; else if (gy >= GloomH) gy = GloomH - 1;
                    for (int x = 0; x < w; x++)
                    {
                        int gx = (x * GloomW) / w;
                        if (gx >= GloomW) gx = GloomW - 1;
                        int k = (GloomH - 1 - gy) * GloomW + gx;
                        hits[k]++;
                        var c = px[y * w + x];
                        if (c.a > 8 && c.a < 200) back[k] += 1f;
                    }
                }
                for (int i = 0; i < back.Length; i++)
                    if (hits[i] > 0) back[i] = Mathf.Clamp01(back[i] / hits[i] * 1.6f);
                layer.Backdrop = back;

                // текстура считает строки снизу, холст — сверху
                layer.Bounds = new Rect(x0 / (float)w, 1f - (y1 + 1) / (float)h,
                                        (x1 - x0 + 1) / (float)w, (y1 - y0 + 1) / (float)h);

                // Пятна ищем по огрублённой сетке: лампа — это несколько пикселей,
                // и точность до клетки здесь более чем достаточна.
                const int Cell = 3;
                int gw = (w + Cell - 1) / Cell, gh = (h + Cell - 1) / Cell;

                Action<Func<Color32, bool>, List<Vector2>> find = (fits, into) =>
                {
                    var full = new bool[gw * gh];
                    bool anyCell = false;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            var c = px[y * w + x];
                            if (c.a <= 8 || !fits(c)) continue;
                            full[(y / Cell) * gw + (x / Cell)] = true;
                            anyCell = true;
                        }
                    if (!anyCell) return;

                    var stack = new List<int>();
                    for (int i = 0; i < full.Length && into.Count < 32; i++)
                    {
                        if (!full[i]) continue;
                        stack.Clear(); stack.Add(i); full[i] = false;
                        long sx = 0, sy = 0; int cnt = 0;
                        while (stack.Count > 0)
                        {
                            int c = stack[stack.Count - 1]; stack.RemoveAt(stack.Count - 1);
                            int cx = c % gw, cy = c / gw;
                            sx += cx; sy += cy; cnt++;
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    int nx = cx + dx, ny = cy + dy;
                                    if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                                    int k = ny * gw + nx;
                                    if (!full[k]) continue;
                                    full[k] = false; stack.Add(k);
                                }
                        }
                        if (cnt < 2) continue;               // одинокая точка — не лампа
                        into.Add(new Vector2((sx / (float)cnt + 0.5f) * Cell / w,
                                             1f - (sy / (float)cnt + 0.5f) * Cell / h));
                    }
                };

                // жёлтое — это лампа, где бы она ни лежала
                find(c => c.r > 150 && c.g > 120 && c.r - c.b > 80, layer.LampBlobs);
                // а по всем непрозрачным пятнам слоя тросов видно, где каждый трос
                if (layer.IsCable) find(c => true, layer.Blobs);

                Plugin.Log?.LogInfo($"[map] слой {layer.Name}: ламп {layer.LampBlobs.Count}, " +
                                    $"пятен {layer.Blobs.Count}, свой цвет {layer.HasColor}");
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[map] слой не обмерен: {e.Message}"); }
        }

        /// <summary>
        /// Слой в цвете темы — но ТОЛЬКО там, где игрок оставил графику белой
        /// или серой. Что уже покрашено (жёлтая лампа, крашеная кабина), остаётся
        /// как нарисовано: красить это второй раз — красить поверх краски.
        ///
        /// Яркость белого пикселя сохраняем множителем, поэтому полутона линий не
        /// теряются, а совмещённый макет можно принести одной картинкой.
        /// </summary>
        public static Sprite Tinted(Layer layer, Color frame)
        {
            try
            {
                if (layer == null || layer.Tex == null) return layer?.Sprite;
                string key = ColorKey(frame);
                if (layer.TintKey == key && layer.Tinted != null) return layer.Tinted;

                var src = layer.Tex.GetPixels32();
                var dst = new Color32[src.Length];
                byte fr = (byte)(Mathf.Clamp01(frame.r) * 255f);
                byte fg = (byte)(Mathf.Clamp01(frame.g) * 255f);
                byte fb = (byte)(Mathf.Clamp01(frame.b) * 255f);

                for (int i = 0; i < src.Length; i++)
                {
                    var c = src[i];
                    if (c.a <= 8) { dst[i] = new Color32(0, 0, 0, 0); continue; }
                    int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    int mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    if (mx - mn > 30)
                    {
                        // лампы уезжают в собственный слой, чтобы их можно было
                        // гасить вместе со щитком; остальное цветное — как есть
                        dst[i] = (!layer.IsLamp && IsLampPixel(c)) ? new Color32(0, 0, 0, 0) : c;
                        continue;
                    }
                    // Серое поднимаем: в лоб оно давало половинную яркость темы и
                    // рельсы с колёсами тонули. Белое остаётся белым.
                    float k = 0.55f + 0.45f * (mx / 255f);
                    dst[i] = new Color32((byte)(fr * k), (byte)(fg * k), (byte)(fb * k), c.a);
                }

                var tex = new Texture2D(layer.Tex.width, layer.Tex.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                tex.SetPixels32(dst);
                tex.Apply(false, false);
                layer.Tinted = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                             new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                layer.TintKey = key;
                return layer.Tinted;
            }
            catch { return layer.Sprite; }
        }

        /// <summary>
        /// Только лампы этого слоя, всё прочее прозрачно. Отдельной картинкой их
        /// можно приглушать вместе со щитком, оставаясь в жёлтом, — в совмещённом
        /// макете своего слоя ламп нет.
        /// </summary>
        public static Sprite LampsOnly(Layer layer)
        {
            try
            {
                if (layer == null || layer.Tex == null || layer.IsLamp) return null;
                if (layer.Lamps != null) return layer.Lamps;
                if (layer.LampBlobs.Count == 0) return null;

                var src = layer.Tex.GetPixels32();
                var dst = new Color32[src.Length];
                for (int i = 0; i < src.Length; i++)
                    dst[i] = IsLampPixel(src[i]) ? src[i] : new Color32(0, 0, 0, 0);

                var tex = new Texture2D(layer.Tex.width, layer.Tex.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                tex.SetPixels32(dst);
                tex.Apply(false, false);
                layer.Lamps = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return layer.Lamps;
            }
            catch { return null; }
        }

        private static string ColorKey(Color c)
        {
            return Mathf.RoundToInt(c.r * 255f) + "," + Mathf.RoundToInt(c.g * 255f) + "," +
                   Mathf.RoundToInt(c.b * 255f);
        }

        /// <summary>Сбросить кэш — чтобы подхватить поправленные файлы без перезапуска.</summary>
        public static void Forget()
        {
            _scanned = false;
            _cache = null;
        }

        private static void WriteReadme()
        {
            try
            {
                File.WriteAllText(Path.Combine(Dir, "ЧИТАЙ.txt"),
@"Сюда кладутся PNG-слои схемы локации.

Все слои одного размера, с прозрачным фоном, накладываются в порядке имён
(поэтому удобно называть 1-, 2-, 3-...). Пустая папка — мод рисует своё.

Роль слоя определяется по имени файла:
  *lamp*   лампы: жёлтые, яркость следует за светом в комплексе
  *cave*   пещеры: красятся в цвет опасности темы
  *elev*   кабина лифта: ездит вместе с настоящей
  *cable*  тросы лифта: видны только выше потолка кабины
  *guide*  направляющий слой, в игру НЕ идёт
  прочее   конструкция, красится в цвет темы

Рисуй БЕЛЫМ: картинка идёт как маска, цвет берётся из темы оверлея.
Исключение — слой ламп, там важен сам жёлтый.

Места монстров задаются не картинкой, а строками slotout/slotin в файле
gdlp.lcbridgeoverlay.map.txt (он лежит уровнем выше, рядом с конфигом).

ДОСТАВЩИК
  Положи сюда dropship.png — и мод возьмёт его вместо нарисованного силуэта.
  Белый фон убирается сам. Стиль (Pixel, Vector, Symbol) применяется тот же,
  что и к иконкам монстров. Файл называется строго dropship.png.
", System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
