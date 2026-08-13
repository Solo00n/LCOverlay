using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Захват анонсов ивентов BrutalCompanyMinusExtraReborn на КЛИЕНТАХ.
    ///
    /// Проблема: <c>EventManager.currentEvents</c> заполняется только у ХОСТА, поэтому
    /// на клиентах наша плашка ивентов была пустой. Клиентам BCME сообщает ивенты через
    /// <c>Net.DisplayTipClientRpc(headerText, bodyText, isWarning)</c> — это выполняется
    /// на каждом клиенте. Мы патчим этот метод (рефлексией, см. Plugin.TryPatchBcmeTips)
    /// и запоминаем анонсы за текущий день; чистим их при старте нового дня.
    ///
    /// Текст здесь — уже в языке BCME (если стоит русификатор — русский), поэтому
    /// дополнительно НЕ переводим.
    /// </summary>
    public static class BcmeClientEvents
    {
        private class Entry { public string Header; public string Body; public bool Warn; }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly object _lock = new object();

        /// <summary>Harmony-postfix для Net.DisplayTipClientRpc (имена параметров совпадают).</summary>
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
                BridgeTicker.ForceImmediate(); // ивент объявлен → показать мгновенно
            }
            catch { }
        }

        /// <summary>Очистить (новый день).</summary>
        public static void Clear()
        {
            lock (_lock) { if (_entries.Count > 0) _entries.Clear(); }
        }

        public static bool Any { get { lock (_lock) { return _entries.Count > 0 || SyncedNames().Count > 0; } } }

        // ================= ЧТЕНИЕ ЧЕРЕЗ СИНХРОНИЗИРУЕМУЮ ПЕРЕМЕННУЮ =================
        // Патч на DisplayTipClientRpc оказался НЕнадёжным: у клиента ивент так и не
        // появлялся (RPC можно пропустить по таймингу — клиент мог ещё догружаться).
        // Net.Instance.textUI — это NetworkVariable<FixedString4096Bytes>, Netcode
        // синхронизирует её на всех клиентов И отдаёт текущее значение тому, кто
        // подключился позже. Поэтому читаем состояние ОТТУДА, каждый раз заново.
        private static bool _netSearched;
        private static Type _netType;
        private static PropertyInfo _netInstance;
        private static FieldInfo _textUiField;
        private static PropertyInfo _netVarValue;
        private static string _lastLogged;

        private static string SyncedRaw()
        {
            try
            {
                if (!_netSearched)
                {
                    _netSearched = true;
                    _netType = GameState.FindTypeByFullName("BrutalCompanyMinus.Net")
                            ?? GameState.FindTypeFuzzy("BrutalCompany", new[] { "Net" });
                    if (_netType != null)
                    {
                        const BindingFlags SF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                        _netInstance = _netType.GetProperty("Instance", SF);
                        _textUiField = _netType.GetField("textUI",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    Plugin.Log?.LogInfo($"[bcme-sync] Net={( _netType != null ? "OK" : "нет")}, " +
                                        $"Instance={(_netInstance != null ? "OK" : "нет")}, " +
                                        $"textUI={(_textUiField != null ? "OK" : "нет")}");
                }
                if (_netInstance == null || _textUiField == null) return null;

                var inst = _netInstance.GetValue(null);
                if (inst == null) return null;
                var netVar = _textUiField.GetValue(inst);
                if (netVar == null) return null;

                if (_netVarValue == null)
                    _netVarValue = netVar.GetType().GetProperty("Value",
                        BindingFlags.Public | BindingFlags.Instance);
                var val = _netVarValue?.GetValue(netVar);
                return val?.ToString();
            }
            catch { return null; }
        }

        /// <summary>Имена ивентов, вытащенные из синхронизированного текста BCME.</summary>
        private static List<string> SyncedNames()
        {
            var names = new List<string>();
            try
            {
                string raw = SyncedRaw();
                if (string.IsNullOrEmpty(raw)) return names;

                if (raw != _lastLogged)
                {
                    _lastLogged = raw;
                    Plugin.Log?.LogInfo("[bcme-sync] textUI = " + raw.Replace("\n", " | "));
                }

                // Панель монитора содержит разное (сложность/квота/погода/ивенты).
                // Берём строки, совпавшие с известными ивентами BCME — по нашему словарю.
                foreach (var line in raw.Split('\n'))
                {
                    string s = Regex.Replace(line ?? "", "<.*?>", "").Trim();   // убрать разметку
                    if (s.Length < 3 || s.Length > 48) continue;
                    if (!EventTranslate.IsKnownEvent(s)) continue;
                    if (!names.Contains(s)) names.Add(s);
                }
            }
            catch { }
            return names;
        }

        /// <summary>Захваченные ивенты в виде записей для плашки.</summary>
        public static List<BcmerEvents.EventInfo> Get()
        {
            var outp = new List<BcmerEvents.EventInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) надёжный источник — синхронизируемая переменная BCME
            foreach (var n in SyncedNames())
            {
                if (!seen.Add(n)) continue;
                string name = ConfigSettings.RussianActive ? EventTranslate.ToRu(n) : n;
                outp.Add(new BcmerEvents.EventInfo { Name = name, ColorHex = "#FFFFFF" });
            }

            // 2) запасной — то, что успели поймать из анонса (RPC)
            lock (_lock)
            {
                foreach (var e in _entries)
                {
                    // имя ивента — заголовок типа (обычно это название ивента); если пуст — тело
                    string name = !string.IsNullOrEmpty(e.Header) ? e.Header : e.Body;
                    if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                    outp.Add(new BcmerEvents.EventInfo { Name = name, ColorHex = e.Warn ? "#FF5141" : "#FFFFFF" });
                }
            }
            return outp;
        }
    }
}
