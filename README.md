# LCBridgeOverlay

**In-game GDLP overlay for Lethal Company (v81 / BepInEx 5).**
An in-helmet HUD panel — quota, moon, weather, interior, loot, day/deaths, live monsters and traps on the sides, BCME events, and a run-victory banner — rendered natively inside the game and tilted to match the helmet HUD.

> ⚠️ **This mod is built for the GDLP modpack.** It is a companion to a specific set of mods and expects them to be present. Outside the GDLP pack it may look wrong, show empty fields, or not appear at all. Use it standalone at your own risk — it is not a general-purpose overlay.
>
> ℹ️ **Since 1.3.0 the LCBridge collector is built in.** You no longer need the separate **LCBridge** mod. If you still have it installed, **remove it** — otherwise both fight for port 8181 and the OBS/HTML overlay may get no data.

---

## English

### What it is
LCBridgeOverlay draws the same information as the GDLP HTML stream overlay, but **directly inside the game** (Unity uGUI, screen-space). It **gathers the game state itself** — the old LCBridge collector is now baked in — and at the same time **re-broadcasts that state over a built-in WebSocket** (`ws://localhost:8181`), so the OBS/HTML overlay keeps working exactly as before. One mod now does both jobs.

### Requirements
- **BepInEx 5.4.21+**
- The **GDLP modpack** (this overlay is tuned for it).
- **Do *not* also run the standalone LCBridge mod** — its job is included here now; running both causes a port 8181 conflict.
- *(Optional)* **Zehs-StreamOverlays** — only if you use the browser/OBS HTML overlay, which reads quota/day from it and the rest from this mod's built-in bridge.

### Optional (soft) integrations
Everything below is optional — if a mod is missing, the related info is simply skipped, no errors:
- **BrutalCompanyMinusExtraReborn (BCME)** — event plate with BCME colours.
- **BrutalCompanyMinus RUS + XUnity.AutoTranslator (RTLC)** — Russian event names (pulled from the translator's dictionary).
- **WeatherTweaks** — combined weather (`Stormy + Rainy + ...`).
- **LethalLevelLoader** — custom interior names.
- **MoreCompany** — crew size.
- **MonstersGordion** — monsters shown on the Company moon (71-Gordion).
- **ToilHead** — Coil-Head / Manticoil with turret get their own icons.

### Features
- **Right-side HUD panel**, tilted and (optionally) given a perspective/keystone effect to sit like the helmet HUD.
- **Two styles:** `Legacy` (red pixel-grunge from the original HTML) and `Game` (chat-like, blue corner brackets).
- **Eye indicator** (top of the panel): the "bridge alive" light — grey until the first tick, closes like an eyelid when you leave the ship.
- **Timer** with milliseconds; auto-starts on a raid, pauses on loading/menus, resets on a new save. Manual keys too.
- **Location:** moon, coloured weather, interior type (Facility/Mansion/Mineshaft/custom), item counts inside/outside, beehives, Old Bird warning.
- **Quota:** Q1–Q3 tabs, fill bar, loot / quota, on-planet scrap value.
- **Day & deaths**, crew.
- **Monster icons on the sides** — left = outside, right = inside; variants of one monster stack into a deck; count badge in the corner.
- **Live state icons** — many monsters change icon by state, by priority (angry/attack > aggression > transformed > special > passive): Hoarding Bug (angry), Jester (popped), Child Eater (adult), Nutcracker (attacking), Snare Flea (ceiling), Coil-Head (sway stops while looked at), ToilHead (turret + fires tracers).
- **Slayer / kamikaze** variants get a **blood-splatter** overlay on the icon (not a flat red tint).
- **Scan gating** (optional): with `RequireScanToShow`, monsters only appear after you scan them. **Ghost Girl** shows only to the player she's haunting.
- **Traps** (turrets/mines/spikes) below the panel; during a "turret" BCME event the turrets fire tracers.
- **BCME event mini-plate** that drops in after landing — shows on **clients** too (not only the host).
- **Victory banner** after the 3rd quota, with full run analytics from the bridge.
- **Ticker** with a running summary.
- **CRT scanlines** and worn frame edges, subtle.
- **Camera sway** — the panel subtly tilts/drifts with the camera, in sympathy with mods like **Camera Overhaul** (and a gentle sway in vanilla). Tunable via `CameraSway` / `CameraSwayStrength`.
- **Idle fade:** the panel dims if you don't move the camera for a while.
- **Spectator-friendly:** the overlay stays visible while you're dead/spectating.
- **Pause-aware:** on `Esc` the panel dims into the background so it doesn't compete with the menu.

### Controls
- **`I`** — show/hide (only on the ship, unless `AlwaysVisible`).
- **`O`** — pause/resume the timer (reset key configurable).

### Config (`BepInEx/config/gdlp.lcbridgeoverlay.cfg`)
Key options (widgets & behaviour apply live; style/language/scanlines need a restart):

| Section | Key | Default | Notes |
|---|---|---|---|
| General | Enabled | true | master switch |
| General | Style | Game | `Legacy` or `Game` |
| General | AlwaysVisible | false | visible off-ship too |
| General | Language | auto | `auto` (ru if RTLC/RUS installed, else en), `en`, `ru` |
| General | Scale / RightOffsetPx | 1.0 / 20 | size / right margin |
| General | PerspectiveStrength | 0 | keystone effect; try `0.16` |
| General | FadeWhenIdle / IdleFadeSeconds / IdleMinOpacity | true / 4 / 0.32 | idle dimming |
| General | CameraSway / CameraSwayStrength | true / 1.0 | panel sways with the camera (Camera Overhaul synergy); `0` = off |
| Widgets | Show* (Panel, Timer, Location, Quota, DayDeaths, Monsters, Traps, BrutalEvent, VictoryBanner, Ticker) | true | per-widget toggles |
| Behavior | AutoTimer | true | auto start/pause timer |
| Behavior | ShowAllEvents | false | all BCME events vs first only |
| Behavior | ScaleMonstersByCount | false | experimental: no counts, bigger/shakier by count |
| Behavior | RequireScanToShow | false | monsters appear only after you scan them |
| Behavior | Scanlines | true | CRT lines |
| WebSocket | Port | 8181 | built-in bridge port the HTML/OBS overlay reads |

### Install
Put `LCBridgeOverlay.dll` in `BepInEx/plugins/` (r2modman profile). If you previously used the separate **LCBridge** mod, **delete it** — it's now built in. On Thunderstore, install through the GDLP modpack.

### Known limitations
- The perspective effect is experimental; if the panel looks distorted set `PerspectiveStrength = 0` (the flat tilt remains).
- Genuinely near-black monster renders (rare) come out as faint silhouettes.
- Monster/trap icons are baked into the DLL; adding new ones needs a rebuild.

### Credits
- **Solon** — LCBridge & this overlay (part of GDLP).
- Monster/trap art — Lethal Company wiki renders, keyed for transparency.
- Built on BepInEx / HarmonyX / Unity uGUI / TextMeshPro.

### License
Personal/modpack use. Ask before redistributing outside GDLP.

---

## Русский

### Что это
LCBridgeOverlay показывает ту же информацию, что и HTML-оверлей GDLP, но **прямо в игре** (Unity uGUI). Он **сам собирает состояние игры** — сборщик из LCBridge теперь встроен — и одновременно **раздаёт эти данные по встроенному WebSocket** (`ws://localhost:8181`), так что HTML-оверлей для OBS работает как раньше. Теперь всё делает один мод.

> ⚠️ **Мод сделан для модпака GDLP.** Это компаньон к конкретному набору модов и ждёт, что они установлены. Вне сборки GDLP он может выглядеть неправильно, показывать пустые поля или вовсе не появиться. Использование отдельно — на свой риск, это не универсальный оверлей.
>
> ℹ️ **С версии 1.3.0 сборщик LCBridge встроен.** Отдельный мод **LCBridge больше не нужен.** Если он ещё установлен — **удали его**, иначе оба займут порт 8181 и HTML-оверлей может остаться без данных.

### Требования
- **BepInEx 5.4.21+**
- **Модпак GDLP** (оверлей заточен под него).
- **Не держи одновременно старый мод LCBridge** — его работа теперь здесь; вдвоём они конфликтуют за порт 8181.
- *(Опц.)* **Zehs-StreamOverlays** — только если пользуешься HTML-оверлеем для OBS: он берёт квоту/день оттуда, а остальное — из встроенного моста этого мода.

### Необязательные интеграции (мягкие)
Всё ниже опционально — если мода нет, соответствующая инфа просто пропускается, без ошибок:
- **BrutalCompanyMinusExtraReborn (BCME)** — плашка ивента в цветах BCME.
- **BrutalCompanyMinus RUS + XUnity.AutoTranslator (RTLC)** — русские названия ивентов (берутся из словаря переводчика).
- **WeatherTweaks** — комбинированная погода.
- **LethalLevelLoader** — имена кастомных интерьеров.
- **MoreCompany** — размер экипажа.
- **MonstersGordion** — монстры на луне компании (71-Gordion).
- **ToilHead** — Coil-Head / Manticoil с турелью получают свои иконки.

### Возможности
- **Панель у правого края**, с наклоном и (опционально) перспективой — «как часть шлема».
- **Два стиля:** `Legacy` (красный пиксельный из HTML) и `Game` (как чат, синие уголковые скобки).
- **Глаз-индикатор** сверху: горит серым до первого тика моста, закрывается веком при уходе с корабля.
- **Таймер** с миллисекундами; авто-старт на смене, пауза на загрузках/в меню, сброс на новом сейве. Есть и ручные клавиши.
- **Локация:** луна, цветная погода, тип интерьера, число предметов внутри/снаружи, ульи, предупреждение Old Bird.
- **Квота:** табы Q1–Q3, полоса выполнения, лут/квота, сумма лута на планете.
- **День и смерти**, экипаж.
- **Иконки монстров по бортам** — слева улица, справа комплекс; варианты монстра складываются в колоду; счётчик в углу.
- **Иконки состояний** — многие монстры меняют иконку по состоянию, по приоритету (зол/атака > агрессия > трансформация > особое > пассив): жук (зол), джестер (вылез), ребёнок-людоед (взрослый), наткрекер (атакует), потолочная личинка (на потолке), койл (перестаёт качаться под взглядом), ToilHead (турель + стреляет трассерами).
- **Slayer/камикадзе** — вместо перекраски в красный на иконку накладываются **кровавые пятна** по силуэту.
- **Сканирование** (опц.): с `RequireScanToShow` монстр появляется только после скана. **Девочка-призрак** видна только своей жертве.
- **Ловушки** (турели/мины/шипы) снизу; при «турельном» ивенте BCME турели стреляют трассерами.
- **Мини-плашка ивента BCME**, выпадает после посадки — видна и **у клиентов**, не только у хоста.
- **Баннер победы** после 3-й квоты с полной аналитикой забега.
- **Бегущая строка** со сводкой.
- **CRT-полосы** и потёртости рамок, едва заметные.
- **Покачивание за камерой** — панель слегка наклоняется/плывёт вслед за камерой, в синергии с модами вроде **Camera Overhaul** (и мягко качается без них). Настройки `CameraSway` / `CameraSwayStrength`.
- **Затухание в бездействии:** панель тускнеет, если долго не двигать камерой.
- **Спектатор:** оверлей остаётся видимым, пока ты мёртв и наблюдаешь.
- **Учёт паузы:** при `Esc` панель уходит в фон (приглушается), чтобы не мешать меню.

### Управление
- **`I`** — показать/скрыть (только на корабле, если не включён `AlwaysVisible`).
- **`O`** — пауза/запуск таймера (клавиша сброса настраивается).

### Конфиг (`BepInEx/config/gdlp.lcbridgeoverlay.cfg`)
Переключатели виджетов и поведения применяются на лету; стиль/язык/полосы — с перезапуском. Основное:

| Секция | Ключ | По умолч. | Примечание |
|---|---|---|---|
| General | Enabled | true | общий выключатель |
| General | Style | Game | `Legacy` или `Game` |
| General | AlwaysVisible | false | видно и вне корабля |
| General | Language | auto | `auto` (ru, если стоит RTLC/RUS, иначе en), `en`, `ru` |
| General | Scale / RightOffsetPx | 1.0 / 20 | масштаб / отступ справа |
| General | PerspectiveStrength | 0 | эффект перспективы; попробуй `0.16` |
| General | FadeWhenIdle / IdleFadeSeconds / IdleMinOpacity | true / 4 / 0.32 | приглушение в бездействии |
| General | CameraSway / CameraSwayStrength | true / 1.0 | панель качается за камерой (синергия с Camera Overhaul); `0` — выкл |
| Widgets | Show* (Panel, Timer, Location, Quota, DayDeaths, Monsters, Traps, BrutalEvent, VictoryBanner, Ticker) | true | тумблеры виджетов |
| Behavior | AutoTimer | true | авто старт/пауза таймера |
| Behavior | ShowAllEvents | false | все ивенты BCME или только первый |
| Behavior | ScaleMonstersByCount | false | эксперимент: без цифр, размер/тряска по кол-ву |
| Behavior | RequireScanToShow | false | монстр виден только после сканирования |
| Behavior | Scanlines | true | CRT-полосы |
| WebSocket | Port | 8181 | порт встроенного моста для HTML/OBS-оверлея |

### Установка
Положи `LCBridgeOverlay.dll` в `BepInEx/plugins/` (профиль r2modman). Если раньше ставил отдельный **LCBridge** — **удали его**, он теперь встроен. На Thunderstore — ставится через модпак GDLP.

### Известные ограничения
- Перспектива экспериментальная; если панель кривит — поставь `PerspectiveStrength = 0` (наклон останется).
- Совсем чёрные рендеры монстров (редко) выходят бледными силуэтами.
- Иконки монстров/ловушек зашиты в DLL; добавление новых требует пересборки.

### Авторы
- **Solon** — LCBridge и этот оверлей (часть GDLP).
- Арт монстров/ловушек — рендеры с вики Lethal Company, вычищены под прозрачность.
- Сделано на BepInEx / HarmonyX / Unity uGUI / TextMeshPro.

### Лицензия
Для личного использования / модпака. Перед распространением вне GDLP — спроси.
