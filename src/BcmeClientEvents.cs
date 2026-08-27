using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Активные ивенты BrutalCompanyMinusExtraReborn для КЛИЕНТОВ (не-хостов).
    ///
    /// Почему это вообще нужно: EventManager.currentEvents заполняется ТОЛЬКО у
    /// хоста, поэтому у остальных игроков плашка ивента оставалась пустой.
    ///
    /// Что НЕ сработало раньше:
    ///  1) патч на Net.DisplayTipClientRpc — этот RPC шлётся, только если у ивента
    ///     включено «Show Tip?», а по умолчанию оно ВЫКЛЮЧЕНО: ловить было нечего;
    ///  2) разбор текста панели по нашему словарю переводов — панель показывает не
    ///     внутренние идентификаторы ивентов, поэтому совпадений не находилось.
    ///
    /// Что работает: текст панели BCME доезжает до клиентов (синхронизируемая
    /// переменная Net.textUI, плюс он же отрисован локально на мониторе). А ПОЛНЫЙ
    /// список ивентов (EventManager.events) есть у каждого клиента — он строится из
    /// конфига локально. Поэтому строки панели сверяем именно с ним: это точное
    /// совпадение, не зависящее ни от языка, ни от наших словарей.
    /// </summary>
    public static class BcmeClientEvents
    {
        private class Entry { public string Header; public string Body; public bool Warn; }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly object _lock = new object();

        /// <summary>Harmony-postfix для Net.DisplayTipClientRpc (запасной путь).</summary>
        public static void OnDisplayTip(string headerText, string bodyText, bool isWarning)
        {
            try
            {
                string h = (headerText ?? "").Trim();
                string b = (bodyText ?? "").Trim();
                if (h.Length == 0 && b.Length == 0) return;
                lock (_lock)
                {
                    foreach (var e in _entries)
                        if (e.Header == h && e.Body == b) return; // без дублей
                    _entries.Add(new Entry { Header = h, Body = b, Warn = isWarning });
                }
                Plugin.Log?.LogInfo($"[bcme-client] анонс ивента: header=\"{h}\" body=\"{b}\" warn={isWarning}");
                BridgeTicker.ForceImmediate();
            }
            catch { }
        }

        /// <summary>Очистить (новый день).</summary>
        public static void Clear()
        {
            lock (_lock) { if (_entries.Count > 0) _entries.Clear(); }
            _lastRaw = null;
        }

        public static bool Any => Get().Count > 0;

        /// <summary>Активные ивенты для плашки (пусто, если BCME нет или ивентов нет).</summary>
        public static List<BcmerEvents.EventInfo> Get()
        {
            var outp = new List<BcmerEvents.EventInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) основной путь — имена ивентов, вычитанные из панели BCME
            foreach (var n in NamesFromPanel())
            {
                if (!seen.Add(n)) continue;
                outp.Add(new BcmerEvents.EventInfo
                {
                    Name = ConfigSettings.RussianActive ? EventTranslate.ToRu(n) : n,
                    ColorHex = "#FFFFFF",
                });
            }

            // 2) запасной — то, что успели поймать из анонса (если типсы включены)
            lock (_lock)
            {
                foreach (var e in _entries)
                {
                    string name = !string.IsNullOrEmpty(e.Header) ? e.Header : e.Body;
                    if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                    outp.Add(new BcmerEvents.EventInfo { Name = name, ColorHex = e.Warn ? "#FF5141" : "#FFFFFF" });
                }
            }
            return outp;
        }

        // ============ полный список ивентов BCME (есть у КАЖДОГО клиента) ============
        private static bool _knownSearched;
        private static FieldInfo[] _eventListFields;
        private static readonly Dictionary<Type, MethodInfo> _nameMethods = new Dictionary<Type, MethodInfo>();
        private static HashSet<string> _knownNames;
        private static float _knownRefreshed;

        private static HashSet<string> KnownEventNames()
        {
            // список строится из конфига при запуске мода; пока он пуст — пробуем снова
            if (_knownNames != null && _knownNames.Count > 0 &&
                Time.unscaledTime - _knownRefreshed < 30f) return _knownNames;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!_knownSearched)
                {
                    _knownSearched = true;
                    var em = GameState.FindTypeByFullName("BrutalCompanyMinus.Minus.EventManager")
                          ?? GameState.FindTypeFuzzy("BrutalCompany", new[] { "EventManager" });
                    if (em != null)
                    {
                        const BindingFlags SF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                        var want = new[] { "events", "vanillaEvents", "moddedEvents", "customEvents" };
                        var found = new List<FieldInfo>();
                        foreach (var n in want)
                        {
                            var f = em.GetField(n, SF);
                            if (f != null) found.Add(f);
                        }
                        _eventListFields = found.ToArray();
                        Plugin.Log?.LogInfo($"[bcme-client] списков ивентов найдено: {found.Count}");
                    }
                }
                if (_eventListFields == null) return _knownNames ?? set;

                foreach (var f in _eventListFields)
                {
                    var list = f.GetValue(null) as IEnumerable;
                    if (list == null) continue;
                    foreach (var ev in list)
                    {
                        if (ev == null) continue;
                        string nm = EventName(ev);
                        if (!string.IsNullOrEmpty(nm)) set.Add(nm.Trim());
                    }
                }
            }
            catch { }

            if (set.Count > 0)
            {
                if (_knownNames == null || _knownNames.Count != set.Count)
                    Plugin.Log?.LogInfo($"[bcme-client] известных имён ивентов: {set.Count}");
                _knownNames = set;
                _knownRefreshed = Time.unscaledTime;
            }
            return _knownNames ?? set;
        }

        private static string EventName(object ev)
        {
            try
            {
                var t = ev.GetType();
                MethodInfo m;
                if (!_nameMethods.TryGetValue(t, out m))
                {
                    m = t.GetMethod("Name",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, Type.EmptyTypes, null);
                    _nameMethods[t] = m;
                }
                return m != null ? m.Invoke(ev, null) as string : null;
            }
            catch { return null; }
        }

        // ============ текст панели BCME ============
        private static bool _panelSearched;
        private static PropertyInfo _netInstance, _netVarValue, _uiInstance;
        private static FieldInfo _textUiField, _panelTextField;
        private static string _lastRaw;

        private static string PanelRaw()
        {
            try
            {
                if (!_panelSearched)
                {
                    _panelSearched = true;
                    const BindingFlags SF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                    const BindingFlags IF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                    var netType = GameState.FindTypeByFullName("BrutalCompanyMinus.Net")
                               ?? GameState.FindTypeFuzzy("BrutalCompany", new[] { "Net" });
                    if (netType != null)
                    {
                        _netInstance = netType.GetProperty("Instance", SF);
                        _textUiField = netType.GetField("textUI", IF);
                    }
                    var uiType = GameState.FindTypeByFullName("BrutalCompanyMinus.UI")
                              ?? GameState.FindTypeFuzzy("BrutalCompany", new[] { "UI" });
                    if (uiType != null)
                    {
                        _uiInstance = uiType.GetProperty("Instance", SF);
                        _panelTextField = uiType.GetField("panelText", IF);
                    }
                    Plugin.Log?.LogInfo($"[bcme-client] Net.textUI={(_textUiField != null ? "OK" : "нет")}, " +
                                        $"UI.panelText={(_panelTextField != null ? "OK" : "нет")}");
                }

                // а) синхронизируемая переменная — доезжает и до опоздавших клиентов
                if (_netInstance != null && _textUiField != null)
                {
                    var inst = _netInstance.GetValue(null);
                    if (inst != null)
                    {
                        var netVar = _textUiField.GetValue(inst);
                        if (netVar != null)
                        {
                            if (_netVarValue == null)
                                _netVarValue = netVar.GetType().GetProperty("Value",
                                    BindingFlags.Public | BindingFlags.Instance);
                            var val = _netVarValue != null ? _netVarValue.GetValue(netVar) : null;
                            string v = val != null ? val.ToString() : null;
                            if (!string.IsNullOrEmpty(v)) return v;
                        }
                    }
                }

                // б) то, что реально нарисовано на мониторе у этого игрока
                if (_uiInstance != null && _panelTextField != null)
                {
                    var ui = _uiInstance.GetValue(null);
                    if (ui != null)
                    {
                        var tmp = _panelTextField.GetValue(ui);
                        if (tmp != null)
                        {
                            var tp = tmp.GetType().GetProperty("text",
                                BindingFlags.Public | BindingFlags.Instance);
                            var val = tp != null ? tp.GetValue(tmp) : null;
                            string v = val != null ? val.ToString() : null;
                            if (!string.IsNullOrEmpty(v)) return v;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>Имена активных ивентов, вычитанные из текста панели BCME.</summary>
        private static List<string> NamesFromPanel()
        {
            var names = new List<string>();
            try
            {
                string raw = PanelRaw();
                if (string.IsNullOrEmpty(raw)) return names;

                if (raw != _lastRaw)
                {
                    _lastRaw = raw;
                    // помогает разобраться, если формат панели вдруг изменится
                    Plugin.Log?.LogInfo("[bcme-client] панель BCME: " +
                        Regex.Replace(raw, "<.*?>", "").Replace("\n", " | "));
                }

                var known = KnownEventNames();
                if (known == null || known.Count == 0) return names;

                foreach (var line in raw.Split('\n'))
                {
                    string s = Regex.Replace(line ?? "", "<.*?>", "").Trim();   // убрать разметку
                    if (s.Length == 0) continue;

                    // строка целиком — имя ивента
                    if (known.Contains(s))
                    {
                        if (!names.Contains(s)) names.Add(s);
                        continue;
                    }

                    // либо в строке перечислено несколько через запятую
                    if (s.IndexOf(',') >= 0)
                    {
                        foreach (var part in s.Split(','))
                        {
                            string p = part.Trim();
                            if (p.Length > 0 && known.Contains(p) && !names.Contains(p)) names.Add(p);
                        }
                    }
                }
            }
            catch { }
            return names;
        }
    }
}
