using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Схема локации, описанная текстом.
    ///
    /// Зачем: рисунок в коде правится только через сборку мода. Здесь он вынесен в
    /// обычный файл рядом с конфигом, поэтому двигать линии, менять размеры и
    /// переставлять места монстров можно самому, без пересборки — перезашёл в сейв
    /// и увидел результат.
    ///
    /// Файл: BepInEx/config/gdlp.lcbridgeoverlay.map.txt
    /// Если его нет — мод пишет туда встроенную схему, её можно взять за основу.
    /// Если файл битый — молча используется встроенная.
    ///
    /// Система координат: 330 по ширине, 400 по высоте, Y растёт ВНИЗ (как в SVG).
    /// </summary>
    internal class MapLayout
    {
        public struct Cmd
        {
            public string Op;
            public float[] N;
            public string Arg;
        }

        public readonly List<Cmd> Cmds = new List<Cmd>();

        public static string Path_
        {
            get
            {
                try { return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "gdlp.lcbridgeoverlay.map.txt"); }
                catch { return "gdlp.lcbridgeoverlay.map.txt"; }
            }
        }

        /// <summary>Прочитать файл схемы, или null если его нет / он пуст.</summary>
        public static MapLayout Load()
        {
            try
            {
                if (!File.Exists(Path_)) return null;
                var lay = new MapLayout();
                foreach (var raw in File.ReadAllLines(Path_, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;

                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    var nums = new List<float>();
                    string arg = null;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                            nums.Add(v);
                        else if (arg == null) arg = parts[i];
                    }
                    lay.Cmds.Add(new Cmd { Op = parts[0].ToLowerInvariant(), N = nums.ToArray(), Arg = arg });
                }
                if (lay.Cmds.Count == 0) return null;
                Plugin.Log?.LogInfo($"[map] схема взята из файла: {lay.Cmds.Count} команд.");
                return lay;
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"[map] файл схемы не прочитан ({e.Message}) — рисуем встроенную.");
                return null;
            }
        }

        /// <summary>Записать образец, если файла ещё нет — чтобы было что править.</summary>
        public static void WriteTemplateIfMissing()
        {
            try
            {
                if (File.Exists(Path_)) return;
                File.WriteAllText(Path_, Template, Encoding.UTF8);
                Plugin.Log?.LogInfo($"[map] образец схемы записан: {Path_}");
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[map] образец не записан: {e.Message}"); }
        }

        // Образец = ровно та схема, что нарисована в коде. Числа те же, поэтому
        // правка файла — это правка того, что видно на экране.
        private const string Template = @"# Схема локации для LCBridgeOverlay.
# Холст 330 x 400, ось Y направлена ВНИЗ (как в SVG).
# Пустые строки и строки с # игнорируются. Числа — через точку.
#
# Команды:
#   line   x1 y1 x2 y2 [толщина] [цвет]
#   box    x y w h [толщина] [цвет]
#   fill   x y w h [прозрачность]      — сканлайн-блок фона
#   lamp   x y ширина                  — люминесцентная лампа на двух ножках
#   rails  x1 x2 y                     — рельсы со шпалами
#   railstop x y                       — стопор
#   cave   x1 y1 x2 y2 ... ; w1 w2 ... — осевая шахты и полуширины в её точках
#   elev   x y w h верх низ            — кабина лифта и пределы её хода
#   elev   ход                        — при слоях-картинках: насколько кабина
#                                       проезжает вниз от нарисованного места
#   gloom  высота [наклон]            — где полумрак сходит на нет и на сколько
#                                       пикселей его кромка падает слева направо
#   cable  x y1 y2                     — трос
#   slotout x y                        — место уличного монстра
#   slotin  x1 y1 x2 y2                — маршрут, по которому ходит монстр внутри
#
# Цвета: frame (тема), dim (приглушённая), cave (красный), lamp (жёлтый).

# ---------- земля ----------
line 0 100 36 93
line 36 93 72 102
line 72 102 108 91
line 108 91 146 99
line 146 99 182 92
line 182 92 192 96
line 192 96 330 96

# ---------- здание ----------
fill 194 24 122 70 0.10
line 192 22 318 22
line 192 22 192 96
line 318 22 318 96
line 192 96 276 96
line 310 96 318 96

# дверь
box 206 56 26 34 2 frame
line 206 73 232 73 1.5 dim
line 219 56 219 90 1.5 dim

# вагонетка
line 242 82 272 82 2 frame
line 242 82 246 66 2 frame
line 272 82 268 66 2 frame
line 246 66 268 66 2 frame
line 246 87 254 87 2 dim
line 260 87 268 87 2 dim

# ---------- коридор шахты ----------
fill 278 96 30 62 0.10
line 276 96 276 158
line 310 96 310 158

# ---------- главная комната ----------
fill 32 160 284 74 0.10
line 30 158 276 158
line 310 158 318 158
line 30 236 318 236
line 30 158 30 236
line 318 158 318 236

# ---------- лифт ----------
cable 283 96 180
cable 303 96 180
elev 274 180 38 24 180 210

# ---------- рельсы ----------
rails 42 262 225
railstop 262 225

# ---------- лампы ----------
lamp 58 158 36
lamp 124 158 36
lamp 190 158 36

# ---------- пещеры ----------
cave 30 218 76 282 132 296 160 330 216 322 252 352 298 356 ; 18 17 19 34 20 18 20

# ---------- места монстров ----------
slotout 20 78
slotout 42 78
slotout 64 78
slotout 86 78
slotout 108 78
slotout 130 78
slotout 152 78
slotout 174 78

slotin 56 210 140 210
slotin 160 210 244 210
slotin 70 192 150 192
slotin 180 192 266 192
slotin 62 276 106 288
slotin 120 296 156 312
slotin 140 330 190 328
slotin 206 322 246 340
slotin 250 352 288 356
slotin 170 336 214 330
";
    }
}
