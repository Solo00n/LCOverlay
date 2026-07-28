using System.Collections.Generic;

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

        public static bool Any { get { lock (_lock) { return _entries.Count > 0; } } }

        /// <summary>Захваченные ивенты в виде записей для плашки.</summary>
        public static List<BcmerEvents.EventInfo> Get()
        {
            var outp = new List<BcmerEvents.EventInfo>();
            lock (_lock)
            {
                foreach (var e in _entries)
                {
                    // имя ивента — заголовок типа (обычно это название ивента); если пуст — тело
                    string name = !string.IsNullOrEmpty(e.Header) ? e.Header : e.Body;
                    if (string.IsNullOrEmpty(name)) continue;
                    outp.Add(new BcmerEvents.EventInfo { Name = name, ColorHex = e.Warn ? "#FF5141" : "#FFFFFF" });
                }
            }
            return outp;
        }
    }
}
