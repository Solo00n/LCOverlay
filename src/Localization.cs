using System.Collections.Generic;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Локализация надписей оверлея (en/ru). Строки встроенные, выбор по
    /// ConfigSettings.Language. Ключ не найден — возвращаем английский,
    /// затем сам ключ (чтобы пропуск перевода был виден, а не ронял UI).
    ///
    /// ВАЖНО про кириллицу: игровой 3270-шрифт содержит только латиницу,
    /// поэтому при Language=ru оверлей использует динамический системный
    /// моноширинный шрифт (см. OverlayManager.ResolveFont).
    /// </summary>
    public static class Localization
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["location"] = "LOCATION",
            ["deadline"] = "DEADLINE PROGRESS",
            ["lootQuota"] = "LOOT / QUOTA",
            ["onPlanet"] = "ON PLANET",
            ["day"] = "DAY",
            ["left"] = "LEFT",
            ["crew"] = "CREW",
            ["deaths"] = "DEATHS",
            ["outside"] = "OUTSIDE",
            ["inside"] = "INSIDE",
            ["traps"] = "TRAPS",
            ["brutalEvent"] = "!! BRUTAL EVENT",
            ["noData"] = "- no data -",
            ["offline"] = "OFFLINE",
            ["bridge"] = "BRIDGE",
            ["interior"] = "INTERIOR",
            ["items"] = "ITEMS",
            ["in"] = "IN",
            ["out"] = "OUT",
            ["hives"] = "HIVES",
            ["oldBird"] = "!! OLD BIRD !!",
            ["met"] = "MET",
            // ticker
            ["tMoon"] = "MOON",
            ["tWx"] = "WX",
            ["tQuota"] = "QUOTA",
            ["tEvent"] = "EVENT",
            ["tObjective"] = "OBJECTIVE: SURVIVE 3 QUOTAS",
            // victory
            ["vicStamp"] = "CHALLENGE",
            ["vicTitle"] = "COMPLETE",
            ["vicSub"] = "3 QUOTAS DONE - YOU SURVIVED",
            ["vicTime"] = "TIME",
            ["vicLoot"] = "LOOT",
            ["vicDeaths"] = "DEATHS",
            ["vicQuotas"] = "BY QUOTA",
            ["vicMoons"] = "MOONS",
            ["vicMonsters"] = "MOST ENCOUNTERED",
            ["vicTimeline"] = "DAY BY DAY",
            ["vicVisits"] = "visits",
            ["vicNoLosses"] = "no losses",
            ["vicEvent"] = "event",
            ["vicDeath"] = "deaths",
            // v1.5
            ["vicSold"] = "SOLD",
            ["mult"] = "MULT",
            ["endOfDay"] = "SHIP LEAVES IN",
            ["lastRun"] = "PREVIOUS RUN",
        };

        private static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            ["location"] = "ЛОКАЦИЯ",
            ["deadline"] = "ПРОГРЕСС КВОТЫ",
            ["lootQuota"] = "ЛУТ / КВОТА",
            ["onPlanet"] = "НА ПЛАНЕТЕ",
            ["day"] = "ДЕНЬ",
            ["left"] = "ОСТ.",
            ["crew"] = "ЭКИПАЖ",
            ["deaths"] = "СМЕРТИ",
            ["outside"] = "УЛИЦА",
            ["inside"] = "КОМПЛЕКС",
            ["traps"] = "ЛОВУШКИ",
            ["brutalEvent"] = "!! BRUTAL ИВЕНТ",
            ["noData"] = "— нет данных —",
            ["offline"] = "НЕТ СВЯЗИ",
            ["bridge"] = "МОСТ",
            ["interior"] = "ИНТЕРЬЕР",
            ["items"] = "ПРЕДМЕТЫ",
            ["in"] = "ВНУТРИ",
            ["out"] = "СНАРУЖИ",
            ["hives"] = "УЛЬИ",
            ["oldBird"] = "!! OLD BIRD !!",
            ["met"] = "ЕСТЬ",
            // ticker
            ["tMoon"] = "ЛУНА",
            ["tWx"] = "ПОГОДА",
            ["tQuota"] = "КВОТА",
            ["tEvent"] = "ИВЕНТ",
            ["tObjective"] = "ЦЕЛЬ: ПЕРЕЖИТЬ 3 КВОТЫ",
            // victory
            ["vicStamp"] = "ИСПЫТАНИЕ",
            ["vicTitle"] = "ПРОЙДЕНО",
            ["vicSub"] = "3 КВОТЫ ВЫПОЛНЕНЫ — ТЫ ВЫЖИЛ",
            ["vicTime"] = "ВРЕМЯ",
            ["vicLoot"] = "ЛУТ",
            ["vicDeaths"] = "СМЕРТИ",
            ["vicQuotas"] = "ПО КВОТАМ",
            ["vicMoons"] = "ЛУНЫ",
            ["vicMonsters"] = "КОГО ВСТРЕЧАЛИ ЧАЩЕ",
            ["vicTimeline"] = "ХРОНИКА ПО ДНЯМ",
            ["vicVisits"] = "визитов",
            ["vicNoLosses"] = "без потерь",
            ["vicEvent"] = "ивент",
            ["vicDeath"] = "смерти",
            // v1.5
            ["vicSold"] = "ПРОДАНО",
            ["mult"] = "МНОЖ",
            ["endOfDay"] = "КОРАБЛЬ УЛЕТИТ ЧЕРЕЗ",
            ["lastRun"] = "ПРОШЛЫЙ ЗАБЕГ",
        };

        /// <summary>Перевод по ключу для текущего языка из конфига.</summary>
        public static string T(string key)
        {
            var dict = ConfigSettings.RussianActive ? Ru : En;
            if (dict.TryGetValue(key, out var s)) return s;
            if (En.TryGetValue(key, out var en)) return en;
            return key;
        }
    }
}
