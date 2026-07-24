using System;
using BepInEx.Bootstrap;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Интеграция с русификаторами: RTLC (Hayrizan) и перевод ивентов
    /// BrutalCompanyMinusExtraReborn RUS (314ZDAteam). Если любой установлен,
    /// оверлей по умолчанию (Language=auto) переключается на русский и берёт
    /// кириллический шрифт — тогда переведённые названия ивентов (их наш
    /// оверлей читает через тот же Name() рефлексией) отображаются корректно.
    /// </summary>
    public static class Rtlc
    {
        private static bool _checked;
        private static bool _present;

        public static bool Present
        {
            get
            {
                if (_checked) return _present;
                _checked = true;
                try
                {
                    foreach (var kv in Chainloader.PluginInfos)
                    {
                        string guid = kv.Key ?? "";
                        string name = kv.Value?.Metadata?.Name ?? "";
                        string both = guid + " " + name;
                        if (both.IndexOf("RTLC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            both.IndexOf("Russian", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            both.IndexOf("314ZDA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (both.IndexOf("BrutalCompany", StringComparison.OrdinalIgnoreCase) >= 0 &&
                             both.IndexOf("RUS", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            _present = true;
                            Plugin.Log?.LogInfo($"Обнаружен русификатор ({name}) — язык оверлея по умолчанию русский.");
                            break;
                        }
                    }
                }
                catch { }
                return _present;
            }
        }
    }
}
