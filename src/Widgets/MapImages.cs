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
            public bool IsLamp, IsCave, IsElevator;
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

                    list.Add(new Layer
                    {
                        Name = nm,
                        Sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                               new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect),
                        IsLamp = nm.Contains("lamp"),
                        IsCave = nm.Contains("cave"),
                        IsElevator = nm.Contains("elev"),
                    });
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
  *guide*  направляющий слой, в игру НЕ идёт
  прочее   конструкция, красится в цвет темы

Рисуй БЕЛЫМ: картинка идёт как маска, цвет берётся из темы оверлея.
Исключение — слой ламп, там важен сам жёлтый.

Места монстров задаются не картинкой, а строками slotout/slotin в файле
gdlp.lcbridgeoverlay.map.txt (он лежит уровнем выше, рядом с конфигом).
", System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
