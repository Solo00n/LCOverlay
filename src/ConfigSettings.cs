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
        public static ConfigEntry<bool> ProximityShake;       // чем ближе монстр, тем нервнее трясётся иконка
        public static ConfigEntry<bool> ResetScansEachDay;    // забывать все сканы на новый день
        public static ConfigEntry<bool> ShareScans;           // делиться сканами с другими игроками
        public static ConfigEntry<bool> RememberSeenMonsters; // помнить, кто водится на каждой луне
        public static ConfigEntry<bool> RequireSignalTranslator; // обмен только при купленном трансляторе
        public static ConfigEntry<string> MonsterIconStyle;   // Render | Pixel | Vector | Symbol
        public static ConfigEntry<string> VectorIconColor;    // Red | Blue | Theme
        public static ConfigEntry<bool> ShowFacilityMap;      // схема локации на улице
        public static ConfigEntry<string> MapLightMode;       // Effects | Schematic
        public static ConfigEntry<string> MapWeatherMode;     // Live | Icons
        // цвет каждой погоды отдельно — как у иконок монстров и цифр отсчёта
        public static ConfigEntry<string> ColorRain, ColorStorm, ColorFlood,
                                          ColorEclipse, ColorFog, ColorDust, ColorMeteor;
        public static ConfigEntry<bool> NotifyMode;           // панель спит и просыпается на новости
        public static ConfigEntry<float> NotifyHoldSeconds;   // сколько держать её разбуженной
        public static ConfigEntry<bool> EventPlateAutoHide;   // плашка ивента гаснет сама
        public static ConfigEntry<float> EventPlateSeconds;   // сколько её держать после посадки
        public static ConfigEntry<bool> TeamDeaths;           // считать смерти всей команды
        public static ConfigEntry<bool> DeathsOnlyOnLeave;    // засчитывать смерти только при отлёте с луны
        public static ConfigEntry<bool> HideOnPopups;         // прятать оверлей на игровых окнах
        public static ConfigEntry<bool> HideOnStoreAd;        // прятать оверлей на рекламе магазина
        public static ConfigEntry<bool> DoorRadar;            // монстры за дверью
        public static ConfigEntry<float> DoorRadarRadius;     // радиус «виртуального радара» за дверью
        public static ConfigEntry<bool> NearestVariantOnly;   // из версий монстра — всегда ОДНА иконка
        public static ConfigEntry<float> VariantNearDistance; // в пределах этого — показываем версию, что рядом
        public static ConfigEntry<float> VariantCycleSeconds; // иначе версии плавно сменяют друг друга
        public static ConfigEntry<bool> DamageFlash;          // вспышка иконки при уроне
        public static ConfigEntry<bool> ShowEndOfDayCountdown;// таймер конца дня
        public static ConfigEntry<string> CountdownColor;    // цвет цифр отсчёта
        public static ConfigEntry<bool> ShowLootMultiplier;   // суммарный множитель стоимости лута
        public static ConfigEntry<bool> ShowApparatusIcon;    // иконка лампы (аппарата) у интерьера
        public static ConfigEntry<bool> DeviantFlipIcon;      // девиантов рисовать вверх ногами
        public static ConfigEntry<bool> JesterWindUpShake;    // джестер трясётся, пока заводится

        // --- [WebSocket] ---
        // Мост для HTML-оверлея в OBS. По умолчанию ВЫКЛЮЧЕН: пока он не включён,
        // мод не открывает никаких сокетов. Внутриигровому оверлею мост не нужен
        // вообще — он берёт данные напрямую, в том же процессе.
        public static ConfigEntry<bool> WebSocketEnabled;
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
            TeamDeaths = cfg.Bind("Behavior", "TeamDeaths", true,
                "Считать смерти ВСЕЙ команды (даже если ты не видел смерть), а не только замеченные локально. " +
                "Полностью заменяет старый способ подсчёта.");
            DeathsOnlyOnLeave = cfg.Bind("Behavior", "DeathsOnlyOnLeave", false,
                "Обновлять счётчик смертей только при отлёте корабля с луны (по итогам вылазки), а не мгновенно.");
            HideOnPopups = cfg.Bind("Behavior", "HideOnPopups", true,
                "Прятать оверлей во время игровых всплывающих окон (подсказки, сдача квоты, экран конца дня) " +
                "и возвращать после закрытия.");
            HideOnStoreAd = cfg.Bind("Behavior", "HideOnStoreAd", true,
                "Прятать оверлей во время рекламы магазина. Пока реклама идёт, вернуть его клавишей нельзя.");
            DoorRadar = cfg.Bind("Behavior", "DoorRadar", true,
                "У двери комплекса (главный вход/пожарный выход) показывать монстров, которые находятся ПО ТУ СТОРОНУ двери.");
            DoorRadarRadius = cfg.Bind("Behavior", "DoorRadarRadius", 22f,
                "Радиус «виртуального радара» за дверью, метров (5–60).");
            NearestVariantOnly = cfg.Bind("Behavior", "NearestVariantOnly", true,
                "Если у монстра несколько версий (например, обычный и с турелью) — показывать ВСЕГДА ОДНУ иконку: " +
                "ту версию, что рядом, а если рядом никого — версии плавно сменяют друг друга по кругу.");
            VariantNearDistance = cfg.Bind("Behavior", "VariantNearDistance", 14f,
                "До скольких метров версия считается «рядом»: её иконка закрепляется и не сменяется. 0 — никогда не закреплять.");
            VariantCycleSeconds = cfg.Bind("Behavior", "VariantCycleSeconds", 2f,
                "Сколько секунд показывается каждая версия, когда рядом никого (плавная смена по кругу). 0 — не листать, показывать ближайшую.");
            DamageFlash = cfg.Bind("Behavior", "DamageFlash", true,
                "Иконка монстра кратко вспыхивает красным, когда монстр получает урон.");
            ShowEndOfDayCountdown = cfg.Bind("Behavior", "ShowEndOfDayCountdown", true,
                "Отсчёт до конца дня (появляется за 10 секунд).");
            ShowLootMultiplier = cfg.Bind("Behavior", "ShowLootMultiplier", true,
                "Показывать суммарный множитель стоимости лута (погода + ивенты) отдельным числом.");
            ShowApparatusIcon = cfg.Bind("Behavior", "ShowApparatusIcon", true,
                "Иконка лампы (аппарата) рядом с интерьером, пока аппарат не вынесли из комплекса.");
            DeviantFlipIcon = cfg.Bind("Behavior", "DeviantFlipIcon", true,
                "Инверснутые монстры (мод DeviantEnemies) показываются перевёрнутой вверх ногами иконкой.");
            JesterWindUpShake = cfg.Bind("Behavior", "JesterWindUpShake", true,
                "Пока джестер заводится, его иконка трясётся всё сильнее — и в момент хлопка сменяется на иконку 2-й фазы.");

            CountdownColor = cfg.Bind("Widgets", "CountdownColor", "Red",
                "Цвет цифр отсчёта конца дня: Red, Blue, White или Theme (из темы оверлея).");

            ProximityShake = cfg.Bind("Behavior", "ProximityShake", true,
                "Чем ближе монстр, тем сильнее дрожит его иконка: издалека — еле заметное покачивание, вплотную — нервная тряска.");

            ResetScansEachDay = cfg.Bind("Behavior", "ResetScansEachDay", false,
                "Работает вместе с RequireScanToShow: каждый новый день все сканы забываются, включая уже открытый бестиарий. Тогда монстров и ловушки приходится сканировать заново каждую высадку, и заранее знать, что тебя ждёт, невозможно.");

            ShareScans = cfg.Bind("Behavior", "ShareScans", true,
                "Работает вместе с RequireScanToShow: сканы видны всему отряду — просветил один, увидели все. Выключи, и каждый будет видеть в оверлее только то, что просканировал сам. Решает хост для всего лобби.");

            RememberSeenMonsters = cfg.Bind("Behavior", "RememberSeenMonsters", false,
                "Работает вместе с RequireScanToShow: мод запоминает, кого ты встречал на каждой луне, и при следующем прилёте туда показывает их сразу. Память переживает перезапуск игры. Учти: это прямая противоположность ResetScansEachDay, который как раз не даёт знать заранее.");

            RequireSignalTranslator = cfg.Bind("Behavior", "RequireSignalTranslator", false,
                "Обмен данными между оверлеями работает, только если на корабле куплен сигнальный транслятор. Даёт этому предмету настоящий смысл: без него каждый видит только своё.");

            MonsterIconStyle = cfg.Bind("General", "MonsterIconStyle", "Render",
                "Как рисовать иконки монстров: Render — как есть, Pixel — 8-битный вид, Vector — только контур линиями, Symbol — сплошной силуэт в цвет темы.");

            VectorIconColor = cfg.Bind("General", "VectorIconColor", "Red",
                "Цвет иконок в стилях Vector и Symbol: Red, Blue или Theme (взять из темы оверлея).");

            ShowFacilityMap = cfg.Bind("Widgets", "ShowFacilityMap", true,
                "Вне корабля вместо полной панели показывать маленькую схему локации: поверхность, вход, комплекс и пещеры, монстры точками по зонам, лампы по настоящему свету, погода и лут. Сделано так, чтобы не мешать при передвижении.");

            MapLightMode = cfg.Bind("Widgets", "MapLightMode", "Effects",
                "Как схема показывает свет в комплексе: Effects — лампы мягко дышат, Schematic — просто горит или не горит.");

            MapWeatherMode = cfg.Bind("Widgets", "MapWeatherMode", "Live",
                "Как схема показывает погоду: Live - погода происходит на схеме (капли бьются о землю, молнии, вода поднимается вместе с настоящей, солнце с луной идут по небу, туман плывёт и наводит помехи), Icons - просто значок в углу.");

            // Имя цвета или #RRGGBB. Theme — взять цвет темы оверлея.
            const string cdesc = "Цвет этой погоды на схеме: Red, Blue, White, Yellow, Green, Theme или #RRGGBB.";
            ColorRain    = cfg.Bind("Widgets", "ColorRain",    "Blue",   cdesc);
            ColorStorm   = cfg.Bind("Widgets", "ColorStorm",   "Yellow", cdesc);
            ColorFlood   = cfg.Bind("Widgets", "ColorFlood",   "Blue",   cdesc);
            ColorEclipse = cfg.Bind("Widgets", "ColorEclipse", "Red",    cdesc);
            ColorFog     = cfg.Bind("Widgets", "ColorFog",     "White",  cdesc);
            ColorDust    = cfg.Bind("Widgets", "ColorDust",    "Yellow", cdesc);
            ColorMeteor  = cfg.Bind("Widgets", "ColorMeteor",  "Red",    cdesc);

            NotifyMode = cfg.Bind("Behavior", "NotifyMode", false,
                "Режим уведомлений: панель не висит на экране постоянно, а спит невидимой и разгорается только когда что-то изменилось. Над ней появляется мельтешащая папка, она влетает в панель и растворяется, изменившиеся цифры коротко мерцают. Включение и выключение озвучены радар-бустером.");

            NotifyHoldSeconds = cfg.Bind("Behavior", "NotifyHoldSeconds", 6f,
                "Сколько секунд панель остаётся видимой после последней новости.");

            EventPlateAutoHide = cfg.Bind("Behavior", "EventPlateAutoHide", false,
                "Плашка ивента не висит весь день, а всплывает дважды: на 10 секунд после посадки и на 5 при отлёте. В режиме уведомлений так и без этой настройки.");

            EventPlateSeconds = cfg.Bind("Behavior", "EventPlateSeconds", 10f,
                "Сколько секунд плашка ивента висит после посадки. При отлёте она показывается вдвое короче.");

            // [WebSocket]
            WebSocketEnabled = cfg.Bind("WebSocket", "Enabled", false,
                "Отдавать данные наружу по WebSocket для HTML-оверлея в OBS. " +
                "Выключено по умолчанию: пока не включишь, мод не открывает ни одного порта. " +
                "Внутриигровому оверлею это не нужно — включай, только если ведёшь стрим через OBS.");

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
