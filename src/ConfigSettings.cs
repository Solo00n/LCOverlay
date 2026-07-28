using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Все параметры конфига (BepInEx ConfigFile) в статических полях.
    /// Секции и имена — по ТЗ (п.10). Переключатели виджетов применяются
    /// на лету (совместимо с LethalConfig); смена стиля — с перезапуском игры.
    /// </summary>
    public static class ConfigSettings
    {
        // --- [General] ---
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<string> Style;          // "Game" | "Legacy"
        public static ConfigEntry<bool> AlwaysVisible;
        public static ConfigEntry<string> ToggleKey;
        public static ConfigEntry<string> Language;       // "en" | "ru"
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<int> RightOffsetPx;
        public static ConfigEntry<float> PerspectiveStrength;
        public static ConfigEntry<bool> FadeWhenIdle;
        public static ConfigEntry<float> IdleFadeSeconds;
        public static ConfigEntry<float> IdleMinOpacity;
        public static ConfigEntry<bool> CameraSway;            // панель качается вслед за камерой
        public static ConfigEntry<float> CameraSwayStrength;   // сила покачивания (0 — выкл)

        // --- [Widgets] ---
        public static ConfigEntry<bool> ShowPanel;
        public static ConfigEntry<bool> ShowTimer;
        public static ConfigEntry<bool> ShowLocation;
        public static ConfigEntry<bool> ShowQuota;
        public static ConfigEntry<bool> ShowDayDeaths;
        public static ConfigEntry<bool> ShowMonsters;
        public static ConfigEntry<bool> ShowTraps;
        public static ConfigEntry<bool> ShowBrutalEvent;
        public static ConfigEntry<bool> ShowVictoryBanner;
        public static ConfigEntry<bool> ShowTicker;

        // --- [Behavior] ---
        public static ConfigEntry<bool> AutoTimer;
        public static ConfigEntry<bool> ShowAllEvents;
        public static ConfigEntry<string> TimerPauseKey;
        public static ConfigEntry<string> TimerResetKey;
        public static ConfigEntry<bool> Scanlines;
        public static ConfigEntry<bool> ScaleMonstersByCount; // эксперимент: без цифр, размер/тряска по кол-ву
        public static ConfigEntry<bool> RequireScanToShow;    // монстр виден только после сканирования
        public static ConfigEntry<bool> ProximityFade;        // чем ближе монстр, тем менее прозрачна иконка

        // --- [WebSocket] ---
        // Порт встроенного моста (мод сам собирает данные и раздаёт их по WebSocket
        // для HTML-оверлея/OBS). Внутриигровой оверлей данные берёт напрямую.
        public static ConfigEntry<int> Port;

        // распарсенные клавиши (кэш, обновляется при изменении настройки)
        public static Key ToggleKeyParsed { get; private set; } = Key.I;
        public static Key TimerPauseKeyParsed { get; private set; } = Key.O;
        public static Key TimerResetKeyParsed { get; private set; } = Key.None;

        /// <summary>Итоговый выбор стиля по параметру Style.</summary>
        public static bool LegacyStyleActive =>
            string.Equals(Style.Value?.Trim(), "Legacy", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Итоговый выбор языка. "auto" (по умолчанию) → русский, если установлен
        /// русификатор RTLC, иначе английский. "ru"/"en" — принудительно.
        /// </summary>
        public static bool RussianActive
        {
            get
            {
                string v = Language.Value?.Trim();
                if (string.Equals(v, "ru", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(v, "en", StringComparison.OrdinalIgnoreCase)) return false;
                return Rtlc.Present; // auto
            }
        }

        public static void Bind(ConfigFile cfg)
        {
            // [General]
            Enabled = cfg.Bind("General", "Enabled", true,
                "Включить/выключить оверлей целиком.");
            Style = cfg.Bind("General", "Style", "Game",
                "Стиль оверлея: \"Legacy\" (старый пиксельный из HTML) или \"Game\" (как внутриигровой чат). Требует перезапуска игры.");
            AlwaysVisible = cfg.Bind("General", "AlwaysVisible", false,
                "true — оверлей виден всегда (даже вне корабля), клавиша показа/скрытия работает везде. " +
                "false — оверлей виден только когда игрок на корабле.");
            ToggleKey = cfg.Bind("General", "ToggleKey", "I",
                "Клавиша показать/скрыть оверлей. Можно писать как символ (\\ , ` , [ , - , = , ; , ' , . , /) или имя " +
                "из UnityEngine.InputSystem.Key (I, F7, Numpad0, Backslash, Backquote). Меняется на лету, без перезапуска.");
            Language = cfg.Bind("General", "Language", "auto",
                "Язык надписей оверлея: \"auto\" (русский, если установлен русификатор RTLC, иначе английский), \"en\" или \"ru\".");
            Scale = cfg.Bind("General", "Scale", 1.0f,
                "Масштаб оверлея (0.5–2.0).");
            RightOffsetPx = cfg.Bind("General", "RightOffsetPx", 20,
                "Отступ оверлея от правого края экрана в пикселях.");
            PerspectiveStrength = cfg.Bind("General", "PerspectiveStrength", 0f,
                "ЭКСПЕРИМЕНТ: эффект перспективы (как чат «уходит вдаль») — ближняя к центру экрана сторона сужается. 0 — выключить (по умолчанию). Попробуй 0.16. Искажает и текст, и рамки. Требует перезапуска.");
            FadeWhenIdle = cfg.Bind("General", "FadeWhenIdle", true,
                "Приглушать оверлей, если долго не двигать камерой (возвращается при движении камеры).");
            IdleFadeSeconds = cfg.Bind("General", "IdleFadeSeconds", 4f,
                "Сколько секунд без движения камеры до приглушения оверлея.");
            IdleMinOpacity = cfg.Bind("General", "IdleMinOpacity", 0.32f,
                "Насколько приглушать: итоговая непрозрачность в бездействии (0 — полностью прозрачный, 1 — без приглушения).");
            CameraSway = cfg.Bind("General", "CameraSway", true,
                "Панель слегка качается/наклоняется вслед за движением камеры — синергия с модами вроде Camera Overhaul (как игровые меню). Читает наклон самой камеры, поэтому работает с любым таким модом и без него.");
            CameraSwayStrength = cfg.Bind("General", "CameraSwayStrength", 1f,
                "Сила покачивания (0 — выключить, 2 — заметнее). Применяется на лету.");

            // [Widgets] — независимые переключатели, все по умолчанию true
            ShowPanel = cfg.Bind("Widgets", "ShowPanel", true,
                "Общая панель: фон, рамка/уголки и логотип GDLP.");
            ShowTimer = cfg.Bind("Widgets", "ShowTimer", true,
                "Таймер (запуск/пауза/сброс — клавиши в секции Behavior; авто-режим — AutoTimer).");
            ShowLocation = cfg.Bind("Widgets", "ShowLocation", true,
                "Блок локации: луна, погода, тип интерьера, ульи, предметы внутри/снаружи, Old Bird.");
            ShowQuota = cfg.Bind("Widgets", "ShowQuota", true,
                "Прогресс квоты: табы Q1–Q3, полоса выполнения, собранный лут.");
            ShowDayDeaths = cfg.Bind("Widgets", "ShowDayDeaths", true,
                "День и счётчик смертей.");
            ShowMonsters = cfg.Bind("Widgets", "ShowMonsters", true,
                "Монстры: слева — наружные (outside), справа — внутренние (inside).");
            ShowTraps = cfg.Bind("Widgets", "ShowTraps", true,
                "Ловушки (турели, мины, шипы) в нижней части оверлея. При турельном ивенте — анимация стрельбы.");
            ShowBrutalEvent = cfg.Bind("Widgets", "ShowBrutalEvent", true,
                "Плашка с ивентом от BCME (BrutalCompanyMinusExtraReborn). Видна только на луне.");
            ShowVictoryBanner = cfg.Bind("Widgets", "ShowVictoryBanner", true,
                "Баннер победы после выполнения 3-й квоты (с аналитикой забега: квоты, луны, монстры, хроника).");
            ShowTicker = cfg.Bind("Widgets", "ShowTicker", true,
                "Бегущая строка с краткой сводкой: экипаж, луна, погода, день, квота, смерти.");

            // [Behavior]
            AutoTimer = cfg.Bind("Behavior", "AutoTimer", true,
                "true — таймер автоматически запускается при начале рейда и встаёт на паузу при загрузках/меню. " +
                "false — только ручное управление.");
            ShowAllEvents = cfg.Bind("Behavior", "ShowAllEvents", false,
                "true — показывать ВСЕ ивенты BCME (через запятую). false — только первый из списка.");
            TimerPauseKey = cfg.Bind("Behavior", "TimerPauseKey", "O",
                "Клавиша паузы/запуска таймера. None — отключить.");
            TimerResetKey = cfg.Bind("Behavior", "TimerResetKey", "None",
                "Клавиша сброса таймера. None — отключить (сброс всё равно происходит при новом сейве).");
            Scanlines = cfg.Bind("Behavior", "Scanlines", true,
                "Едва заметные горизонтальные полосы (CRT/LSD-эффект как в ванильных меню). Требует перезапуска игры.");
            ScaleMonstersByCount = cfg.Bind("Behavior", "ScaleMonstersByCount", false,
                "ЭКСПЕРИМЕНТ: убрать цифры количества у монстров И ловушек — вместо этого чем их больше, тем крупнее иконка и тем сильнее она трясётся.");
            RequireScanToShow = cfg.Bind("Behavior", "RequireScanToShow", false,
                "Показывать монстра в оверлее ТОЛЬКО после того, как игрок его отсканировал (сканером). " +
                "Учитывается бестиарий игры: как только вид отсканирован — он показывается. false — видно сразу.");
            ProximityFade = cfg.Bind("Behavior", "ProximityFade", true,
                "Чем БЛИЖЕ монстр к игроку, тем менее прозрачна его иконка (дальний — почти прозрачный). " +
                "false — все иконки полностью непрозрачные.");

            // [WebSocket]
            Port = cfg.Bind("WebSocket", "Port", 8181,
                "Порт встроенного WebSocket-моста. По нему HTML-оверлей (OBS) получает те же данные. " +
                "Если у тебя ещё стоит отдельный мод LCBridge — удали его, иначе порт будет занят.");

            ReparseKeys();
            ToggleKey.SettingChanged += (_, __) => ReparseKeys();
            TimerPauseKey.SettingChanged += (_, __) => ReparseKeys();
            TimerResetKey.SettingChanged += (_, __) => ReparseKeys();
        }

        private static bool _keyHooks;
        private static void ReparseKeys()
        {
            ToggleKeyParsed = ParseKey(ToggleKey.Value, Key.I);
            TimerPauseKeyParsed = ParseKey(TimerPauseKey.Value, Key.None);
            TimerResetKeyParsed = ParseKey(TimerResetKey.Value, Key.None);
            // ЖИВОЕ переназначение: пере-разбираем при любом изменении в конфиге
            // (r2modman / LethalConfig) — без перезапуска игры.
            if (!_keyHooks)
            {
                _keyHooks = true;
                ToggleKey.SettingChanged += (s, e) => ToggleKeyParsed = ParseKey(ToggleKey.Value, Key.I);
                TimerPauseKey.SettingChanged += (s, e) => TimerPauseKeyParsed = ParseKey(TimerPauseKey.Value, Key.None);
                TimerResetKey.SettingChanged += (s, e) => TimerResetKeyParsed = ParseKey(TimerResetKey.Value, Key.None);
            }
        }

        // символы → имена клавиш InputSystem.Key (иначе, например, "\" не парсится
        // и молча откатывался на I — старая клавиша продолжала работать, новая нет)
        private static readonly Dictionary<string, Key> _symbolKeys = new Dictionary<string, Key>
        {
            ["\\"] = Key.Backslash, ["`"] = Key.Backquote, ["~"] = Key.Backquote,
            ["["] = Key.LeftBracket, ["]"] = Key.RightBracket,
            ["-"] = Key.Minus, ["="] = Key.Equals, [";"] = Key.Semicolon,
            ["'"] = Key.Quote, ["\""] = Key.Quote, [","] = Key.Comma, ["."] = Key.Period,
            ["/"] = Key.Slash, [" "] = Key.Space, ["\t"] = Key.Tab,
        };

        private static Key ParseKey(string s, Key fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return Key.None;
            s = s.Trim();
            if (_symbolKeys.TryGetValue(s, out var sym)) return sym;
            // одиночная цифра "1".."9","0" → Digit1..Digit0
            if (s.Length == 1 && s[0] >= '0' && s[0] <= '9'
                && Enum.TryParse<Key>("Digit" + s, true, out var dk)) return dk;
            if (Enum.TryParse<Key>(s, true, out var k)) return k;
            Plugin.Log?.LogWarning($"Не удалось распознать клавишу '{s}'. " +
                "Пиши символ (\\ ` [ - = ; ' . /) или имя InputSystem.Key (Backslash, F7, Numpad0). " +
                $"Пока использую {fallback}.");
            return fallback;
        }
    }
}
