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
            public bool IsLamp, IsCave, IsElevator, IsCable;

            /// <summary>
            /// Непрозрачная часть слоя в долях холста, отсчёт сверху вниз. По ней
            /// мод узнаёт, где нарисована кабина лифта, не заставляя вписывать
            /// координаты руками.
            /// </summary>
            public Rect Bounds;

            /// <summary>Центры отдельных пятен — по ним расставляется свет ламп.</summary>
            public List<Vector2> Blobs = new List<Vector2>();
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

                // текстура считает строки снизу, холст — сверху
                layer.Bounds = new Rect(x0 / (float)w, 1f - (y1 + 1) / (float)h,
                                        (x1 - x0 + 1) / (float)w, (y1 - y0 + 1) / (float)h);

                if (!layer.IsLamp) return;

                // Пятна ищем по огрублённой сетке: лампа — это несколько пикселей,
                // и точность до клетки здесь более чем достаточна.
                const int Cell = 3;
                int gw = (w + Cell - 1) / Cell, gh = (h + Cell - 1) / Cell;
                var full = new bool[gw * gh];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 8) full[(y / Cell) * gw + (x / Cell)] = true;

                var stack = new List<int>();
                for (int i = 0; i < full.Length && layer.Blobs.Count < 32; i++)
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
                    float bx = (sx / (float)cnt + 0.5f) * Cell / w;
                    float by = 1f - (sy / (float)cnt + 0.5f) * Cell / h;
                    layer.Blobs.Add(new Vector2(bx, by));
                }
                Plugin.Log?.LogInfo($"[map] слой {layer.Name}: ламп найдено {layer.Blobs.Count}");
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[map] слой не обмерен: {e.Message}"); }
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
