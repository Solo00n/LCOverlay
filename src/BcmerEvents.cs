using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Чтение активных ивентов BrutalCompanyMinusExtraReborn (BCMER) через рефлексию —
    /// тем же путём, что и LCBridge (EventManager.currentEvents), но дополнительно
    /// достаём ТИП ивента, чтобы окрасить его в цвет, соответствующий цветам BCMER:
    /// ярко-зелёный / зелёный / белый / красный / тёмно-красный / чёрный.
    /// BCMER не обязателен: если мод не установлен, список просто пуст.
    /// </summary>
    public static class BcmerEvents
    {
        public struct EventInfo
        {
            public string Name;
            public string ColorHex;
        }

        private static Type _emType;
        private static FieldInfo _curEventsField;
        private static bool _searched;

        // кэш рефлексии по типу ивента: метод Name() и член с типом ивента
        private static readonly Dictionary<Type, MethodInfo> _nameCache = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, Func<object, string>> _typeGetterCache =
            new Dictionary<Type, Func<object, string>>();

        /// <summary>Цвета из ТЗ, соответствуют раскраске ивентов в BCMER.</summary>
        private static string ColorFor(string typeName)
        {
            string t = (typeName ?? "").ToLowerInvariant();
            if (t.Contains("verygood") || t.Contains("great")) return "#00FF00"; // ярко-зелёный
            if (t.Contains("good")) return "#008000";                             // зелёный
            if (t.Contains("verybad")) return "#8B0000";                          // тёмно-красный
            if (t.Contains("bad")) return "#FF0000";                              // красный
            if (t.Contains("black") || t.Contains("deadly") ||
                t.Contains("impossible") || t.Contains("death")) return "#000000"; // чёрный
            return "#FFFFFF";                                                     // белый (нейтральный)
        }

        public static List<EventInfo> GetEvents()
        {
            var result = new List<EventInfo>();
            try
            {
                EnsureSearched();
                if (_curEventsField == null) return result;

                var list = _curEventsField.GetValue(null) as IEnumerable;
                if (list == null) return result;

                foreach (var ev in list)
                {
                    if (ev == null) continue;
                    string name = GetName(ev);
                    if (string.IsNullOrEmpty(name)) continue;
                    // на русском — переводим имя ивента по нашему ручному словарю
                    // (BCMER отдаёт camelCase-идентификатор, авто-переводчик его не брал)
                    if (ConfigSettings.RussianActive) name = EventTranslate.ToRu(name);
                    result.Add(new EventInfo { Name = name, ColorHex = ColorFor(GetEventTypeName(ev)) });
                }
            }
            catch { /* рефлексия не должна ронять оверлей */ }

            // КЛИЕНТ: у не-хоста EventManager.currentEvents пуст (ивенты живут только у
            // хоста). Тогда берём анонсы, пойманные из Net.DisplayTipClientRpc.
            if (result.Count == 0 && BcmeClientEvents.Any)
                result.AddRange(BcmeClientEvents.Get());

            return result;
        }

        /// <summary>Проверка наличия BCME по списку загруженных плагинов BepInEx (по ТЗ п.6).</summary>
        public static bool BcmePresent()
        {
            try
            {
                foreach (var kv in Chainloader.PluginInfos)
                {
                    if (kv.Key.IndexOf("BrutalCompany", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    string nm = kv.Value?.Metadata?.Name ?? "";
                    if (nm.IndexOf("BrutalCompany", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            catch { }
            return false;
        }

        private static void EnsureSearched()
        {
            if (_searched) return;
            _searched = true;
            if (!BcmePresent())
            {
                Plugin.Log?.LogInfo("BCME не найден в Chainloader.PluginInfos — плашка ивентов возьмёт данные моста.");
                return;
            }
            _emType = FindTypeByFullName("BrutalCompanyMinus.Minus.EventManager")
                   ?? FindTypeFuzzy("BrutalCompany", "EventManager");
            if (_emType != null)
            {
                _curEventsField = _emType.GetField("currentEvents",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                Plugin.Log?.LogInfo($"[reflection] BCMER EventManager={_emType.FullName}, " +
                    $"currentEvents={(_curEventsField != null ? "OK" : "НЕ НАЙДЕНО")}");
            }
            else
            {
                Plugin.Log?.LogInfo("[reflection] BCMER не найден (не установлен?) — плашка ивентов возьмёт данные моста.");
            }
        }

        private static string GetName(object ev)
        {
            var t = ev.GetType();
            if (!_nameCache.TryGetValue(t, out var m))
            {
                m = t.GetMethod("Name",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
                _nameCache[t] = m;
            }
            if (m != null)
            {
                try { return m.Invoke(ev, null) as string; } catch { }
            }
            return ev.ToString();
        }

        /// <summary>
        /// Имя типа/редкости ивента: пробуем метод Type(), затем свойства/поля
        /// Type / EventType / type / Rarity — что найдётся и вернёт enum или строку.
        /// </summary>
        private static string GetEventTypeName(object ev)
        {
            var t = ev.GetType();
            if (!_typeGetterCache.TryGetValue(t, out var getter))
            {
                getter = BuildTypeGetter(t);
                _typeGetterCache[t] = getter;
            }
            try { return getter?.Invoke(ev); } catch { return null; }
        }

        private static Func<object, string> BuildTypeGetter(Type t)
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            string[] candidates = { "Type", "EventType", "type", "eventType", "Rarity", "rarity" };

            foreach (var cand in candidates)
            {
                var m = t.GetMethod(cand, F, null, Type.EmptyTypes, null);
                if (m != null && (m.ReturnType.IsEnum || m.ReturnType == typeof(string)))
                    return o => { var v = m.Invoke(o, null); return v?.ToString(); };
            }
            foreach (var cand in candidates)
            {
                var p = t.GetProperty(cand, F);
                if (p != null && p.CanRead && (p.PropertyType.IsEnum || p.PropertyType == typeof(string)))
                    return o => { var v = p.GetValue(o); return v?.ToString(); };
            }
            foreach (var cand in candidates)
            {
                var f = t.GetField(cand, F);
                if (f != null && (f.FieldType.IsEnum || f.FieldType == typeof(string)))
                    return o => { var v = f.GetValue(o); return v?.ToString(); };
            }
            return null; // тип не нашли — ивент будет белым
        }

        // ---- поиск типов (как в LCBridge) ----
        private static Type FindTypeByFullName(string fullName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = asm.GetType(fullName, false); } catch { }
                    if (t != null) return t;
                }
            }
            catch { }
            return null;
        }

        private static Type FindTypeFuzzy(string asmNameContains, string typeName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string an = asm.GetName().Name ?? "";
                    if (an.IndexOf(asmNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types.Where(x => x != null).ToArray(); }

                    var hit = types.FirstOrDefault(x =>
                        string.Equals(x.Name, typeName, StringComparison.OrdinalIgnoreCase));
                    if (hit != null) return hit;
                }
            }
            catch { }
            return null;
        }
    }
}
