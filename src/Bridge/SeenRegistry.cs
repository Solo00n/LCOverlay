using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Память по планетам: кого игрок уже встречал на каждой луне.
    ///
    /// В режиме «показывать только просканированное» это даёт накопительное знание:
    /// один раз просветил Брекена на Rend — при следующем прилёте туда он покажется
    /// сразу. Знание привязано к ЛУНЕ и к ВИДУ существа, а не к конкретной особи,
    /// иначе оно ничего бы не значило (особи каждый день новые).
    ///
    /// Хранится рядом с конфигом, переживает перезапуск игры.
    ///
    /// ВАЖНО: это прямая противоположность ResetScansEachDay, который специально
    /// не даёт знать заранее. Поэтому по умолчанию выключено — включать осознанно.
    /// </summary>
    internal static class SeenRegistry
    {
        // луна -> набор имён существ
        private static readonly Dictionary<string, HashSet<string>> _byMoon =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static bool _loaded;
        private static bool _dirty;
        private static float _nextSave;
        private static string _path;

        private static string Path_
        {
            get
            {
                if (_path != null) return _path;
                try
                {
                    string dir = BepInEx.Paths.ConfigPath;
                    _path = System.IO.Path.Combine(dir, "gdlp.lcbridgeoverlay.seen.txt");
                }
                catch { _path = "gdlp.lcbridgeoverlay.seen.txt"; }
                return _path;
            }
        }

        private static string Moon()
        {
            try
            {
                var sor = StartOfRound.Instance;
                var lvl = sor != null ? sor.currentLevel : null;
                return lvl != null ? (lvl.PlanetName ?? "") : "";
            }
            catch { return ""; }
        }

        /// <summary>Запомнить, что на текущей луне встречалось это существо.</summary>
        public static void Remember(string enemyName)
        {
            if (!ConfigSettings.RememberSeenMonsters.Value) return;
            if (string.IsNullOrEmpty(enemyName)) return;
            string moon = Moon();
            if (string.IsNullOrEmpty(moon)) return;

            Load();
            if (!_byMoon.TryGetValue(moon, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _byMoon[moon] = set;
            }
            if (set.Add(enemyName))
            {
                _dirty = true;
                Plugin.Log?.LogInfo($"[seen] {moon}: запомнен {enemyName} (всего на луне: {set.Count}).");
            }
        }

        /// <summary>Встречалось ли это существо на текущей луне раньше.</summary>
        public static bool Knows(string enemyName)
        {
            if (!ConfigSettings.RememberSeenMonsters.Value) return false;
            if (string.IsNullOrEmpty(enemyName)) return false;
            string moon = Moon();
            if (string.IsNullOrEmpty(moon)) return false;

            Load();
            return _byMoon.TryGetValue(moon, out var set) && set.Contains(enemyName);
        }

        /// <summary>Периодическое сохранение — чтобы не писать файл на каждую запись.</summary>
        public static void Tick()
        {
            if (!_dirty) return;
            if (UnityEngine.Time.unscaledTime < _nextSave) return;
            _nextSave = UnityEngine.Time.unscaledTime + 20f;
            Save();
        }

        public static void Forget()
        {
            _byMoon.Clear();
            _dirty = true;
            Save();
            Plugin.Log?.LogInfo("[seen] память по планетам очищена.");
        }

        // ---------- файл ----------
        // Формат намеренно простой: «луна = имя1;имя2;имя3» по строке на луну.
        // Читается глазами, чинится блокнотом, не тянет за собой JSON-библиотеку.

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(Path_)) return;
                foreach (var line in File.ReadAllLines(Path_, Encoding.UTF8))
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string moon = line.Substring(0, eq).Trim();
                    if (moon.Length == 0) continue;

                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var n in line.Substring(eq + 1).Split(';'))
                    {
                        string t = n.Trim();
                        if (t.Length > 0) set.Add(t);
                    }
                    if (set.Count > 0) _byMoon[moon] = set;
                }
                Plugin.Log?.LogInfo($"[seen] память загружена: лун {_byMoon.Count}.");
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[seen] не удалось прочитать память: {e.Message}"); }
        }

        private static void Save()
        {
            if (!_dirty) return;
            _dirty = false;
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in _byMoon)
                {
                    if (kv.Value == null || kv.Value.Count == 0) continue;
                    sb.Append(kv.Key).Append(" = ").Append(string.Join(";", new List<string>(kv.Value).ToArray()))
                      .Append('\n');
                }
                File.WriteAllText(Path_, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[seen] не удалось сохранить память: {e.Message}"); }
        }
    }
}
