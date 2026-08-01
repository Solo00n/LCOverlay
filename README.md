# LCBridgeOverlay

**In-game HUD overlay for Lethal Company (v81 / BepInEx 5).**
An in-helmet HUD panel — quota, moon, weather, interior, loot, day/deaths, live monsters and traps on the sides, brutal-company events, and a run-victory banner — rendered natively inside the game and tilted to match the helmet HUD.

> ⚠️ **For now this overlay is tuned for a specific modpack.** Some panels read data that other mods provide, so in **vanilla** (or a very different setup) parts may show empty or not behave exactly as intended — nothing should crash, it just won't be as useful. Everything mod-specific is optional and read safely: missing mods are simply skipped.
>
> 🔭 **Where it's going.** The plan is to keep updating it — more convenient features, more settings, and fewer assumptions — so anyone can tailor it to their own setup, modded or vanilla.

It also runs a small built-in **WebSocket bridge** (`ws://localhost:8181`) that re-broadcasts the same game state, so a browser/OBS overlay can read it for streaming.

---

## English

### Requirements
- **BepInEx 5.4.21+**
- *(Optional)* **Zehs-StreamOverlays** — only if you use a browser/OBS HTML overlay that reads quota/day from it and the rest from this mod's built-in bridge.

> If you used the older separate **LCBridge** mod, remove it — its data collector is built into this mod now, and running both fights over port 8181.

### Optional (soft) integrations
All optional — if a mod is missing, the related info is simply skipped, no errors:
- **BrutalCompanyMinusExtraReborn (BCME)** — event plate in the event's colours; events also show on **clients**, not just the host.
- **BrutalCompanyMinus RUS + XUnity.AutoTranslator** — Russian event names.
- **WeatherTweaks** — combined weather (`Stormy + Rainy + …`).
- **LethalLevelLoader** — custom interior names.
- **MoreCompany** — crew size.
- **MonstersGordion** — monsters shown on the Company moon.
- **ToilHead** — Coil-Head / Manticoil with a turret get their own icons and fire tracers.
- **Camera Overhaul** (or any camera-motion mod) — the panel sways in sympathy with the camera.

### Features
- **Right-side HUD panel**, tilted and (optionally) given a perspective/keystone effect to sit like the helmet HUD. **Two styles:** `Legacy` (red pixel-grunge) and `Game` (chat-like, blue corner brackets).
- **Timer** with milliseconds; auto-starts on a raid, pauses on loading/menus/orbit, resets on a new save. Manual keys too.
- **Location:** moon, coloured weather, interior type, item counts inside/outside, beehives, Old Bird warning.
- **Quota:** Q1–Q3 tabs, fill bar, loot / quota, on-planet scrap value. **Day & deaths**, crew.
- **Monster icons on the sides** — left = outside, right = inside; variants stack into a deck; count badge in the corner.
- **Live state icons** — many monsters change icon by state, by priority (angry/attack > aggression > transformed > special > passive): Hoarding Bug (angry), Jester (popped), Child Eater (adult), Nutcracker (attacking), Snare Flea (ceiling), Coil-Head (sway stops while looked at), ToilHead (turret + tracers).
- **Slayer / kamikaze** variants get a **blood-splatter** overlay on the icon (not a flat red tint).
- **Proximity fade** — the closer a monster or trap is, the more opaque its icon; distant ones fade toward transparent.
- **Scan gating** (optional): with `RequireScanToShow`, monsters appear only after you scan them (uses the in-game bestiary). **Ghost Girl** shows only to the player she's haunting.
- **Traps** (turrets/mines/spikes) on the bottom edge, faded by proximity; turrets fire tracers during a "turret" event. Grabbable turret/mine variants are counted too.
- **Victory banner** after the 3rd quota, with full run analytics. **Ticker** with a running summary.
- **Camera sway** — the panel subtly tilts/drifts with the camera (`CameraSway` / `CameraSwayStrength`).
- **CRT scanlines** and worn frame edges; **idle fade** when the camera is still.
- **Spectator-friendly** (stays visible while dead), **pause-aware** (dims under the Esc menu), and hides during takeoff.

### Controls
- **`I`** — show/hide (only on the ship, unless `AlwaysVisible`). Rebindable (symbol keys like `\` work).
- **`O`** — pause/resume the timer (reset key configurable).

### Config (`BepInEx/config/gdlp.lcbridgeoverlay.cfg`)
Widgets & behaviour apply live; style/language/scanlines need a restart.

| Section | Key | Default | Notes |
|---|---|---|---|
| General | Enabled | true | master switch |
| General | Style | Game | `Legacy` or `Game` |
| General | AlwaysVisible | false | visible off-ship too |
| General | ToggleKey | I | show/hide key (symbol or InputSystem name) |
| General | Language | auto | `auto` (ru if a RU translation is installed, else en), `en`, `ru` |
| General | Scale / RightOffsetPx | 1.0 / 20 | size / right margin |
| General | PerspectiveStrength | 0 | keystone effect; try `0.16` |
| General | FadeWhenIdle / IdleFadeSeconds / IdleMinOpacity | true / 4 / 0.32 | idle dimming |
| General | CameraSway / CameraSwayStrength | true / 1.0 | panel sways with the camera; `0` = off |
| Widgets | Show* (Panel, Timer, Location, Quota, DayDeaths, Monsters, Traps, BrutalEvent, VictoryBanner, Ticker) | true | per-widget toggles |
| Behavior | AutoTimer | true | auto start/pause timer |
| Behavior | ShowAllEvents | false | all events vs first only |
| Behavior | ScaleMonstersByCount | false | experimental: no counts, bigger/shakier by count |
| Behavior | RequireScanToShow | false | monsters appear only after you scan them |
| Behavior | ProximityFade | true | closer = more opaque |
| Behavior | Scanlines | true | CRT lines |
| WebSocket | Port | 8181 | built-in bridge port a browser/OBS overlay reads |

### Install
Put `LCBridgeOverlay.dll` in `BepInEx/plugins/` (r2modman profile), or install from Thunderstore. If you previously used the separate **LCBridge** mod, delete it — it's built in now.

### Known limitations
- The perspective effect is experimental; if the panel looks distorted set `PerspectiveStrength = 0` (the flat tilt remains).
- Monster/trap icons are baked into the DLL; adding new ones needs a rebuild.

### Credits
- **Solon** — author.
- Monster/trap art — Lethal Company renders, keyed for transparency.
- Built on **BepInEx / HarmonyX / Unity uGUI / TextMeshPro**.

### License
See [LICENSE](LICENSE).

---

## Русский

### Что это
Внутриигровой HUD-оверлей для Lethal Company (прямо в игре, Unity uGUI): квота, луна, погода, интерьер, лут, день/смерти, живые монстры и ловушки по бортам, ивенты brutal-company и баннер победы. Плюс встроенный **WebSocket-мост** (`ws://localhost:8181`), который раздаёт то же состояние — чтобы браузерный/OBS-оверлей мог читать его для стрима.

> ⚠️ **Пока оверлей заточен под конкретный модпак.** Часть панелей берёт данные, которые дают другие моды, поэтому в **ванили** (или сильно другой сборке) что-то может быть пустым или вести себя не совсем как задумано — ничего не падает, просто пользы меньше. Всё, что зависит от модов, опционально и читается безопасно: отсутствующий мод просто пропускается.
>
> 🔭 **Куда движемся.** План — развивать дальше: больше удобных функций, больше настроек и меньше жёстких допущений, чтобы каждый мог подстроить оверлей под себя — хоть с модами, хоть в ванили.

> Если стоял старый отдельный мод **LCBridge** — удали его: его сборщик данных теперь встроен сюда, а вдвоём они конфликтуют за порт 8181.

### Требования
- **BepInEx 5.4.21+**
- *(Опц.)* **Zehs-StreamOverlays** — только если пользуешься браузерным/OBS-оверлеем.

### Необязательные интеграции (мягкие)
Всё опционально — нет мода, соответствующая инфа просто пропускается, без ошибок:
- **BrutalCompanyMinusExtraReborn (BCME)** — плашка ивента в его цветах; ивенты видны и **у клиентов**, не только у хоста.
- **BrutalCompanyMinus RUS + XUnity.AutoTranslator** — русские названия ивентов.
- **WeatherTweaks** — комбинированная погода.
- **LethalLevelLoader** — имена кастомных интерьеров.
- **MoreCompany** — размер экипажа.
- **MonstersGordion** — монстры на луне компании.
- **ToilHead** — Coil-Head / Manticoil с турелью получают свои иконки и стреляют трассерами.
- **Camera Overhaul** (или любой мод на движение камеры) — панель качается вслед за камерой.

### Возможности
- **Панель у правого края**, с наклоном и (опц.) перспективой. **Два стиля:** `Legacy` (красный пиксельный) и `Game` (как чат, синие уголковые скобки).
- **Таймер** с миллисекундами; авто-старт на смене, пауза на загрузках/в меню/на орбите, сброс на новом сейве. Есть и ручные клавиши.
- **Локация:** луна, цветная погода, тип интерьера, предметы внутри/снаружи, ульи, предупреждение Old Bird.
- **Квота:** табы Q1–Q3, полоса выполнения, лут/квота, сумма лута на планете. **День и смерти**, экипаж.
- **Иконки монстров по бортам** — слева улица, справа комплекс; варианты складываются в колоду; счётчик в углу.
- **Иконки состояний** по приоритету (зол/атака > агрессия > трансформация > особое > пассив): жук (зол), джестер (вылез), ребёнок-людоед (взрослый), наткрекер (атакует), потолочная личинка (на потолке), койл (перестаёт качаться под взглядом), ToilHead (турель + трассеры).
- **Slayer/камикадзе** — вместо перекраски в красный на иконку накладываются **кровавые пятна** по силуэту.
- **Прозрачность по близости** — чем ближе монстр или ловушка, тем плотнее иконка; дальние бледнеют.
- **Сканирование** (опц.): с `RequireScanToShow` монстр появляется только после скана (по игровому бестиарию). **Девочка-призрак** видна только своей жертве.
- **Ловушки** (турели/мины/шипы) на нижней кромке, бледнеют по расстоянию; при «турельном» ивенте стреляют трассерами. Переносные варианты турелей/мин тоже учитываются.
- **Баннер победы** после 3-й квоты с аналитикой забега. **Бегущая строка** со сводкой.
- **Покачивание за камерой** (`CameraSway` / `CameraSwayStrength`).
- **CRT-полосы** и потёртости рамок; **затухание** при неподвижной камере.
- **Спектатор** (виден, пока мёртв), **учёт паузы** (уходит в фон под меню), прячется на взлёте.

### Управление
- **`I`** — показать/скрыть (только на корабле, если не включён `AlwaysVisible`). Переназначается (символы вроде `\` работают).
- **`O`** — пауза/запуск таймера (клавиша сброса настраивается).

### Конфиг (`BepInEx/config/gdlp.lcbridgeoverlay.cfg`)
Переключатели виджетов и поведения применяются на лету; стиль/язык/полосы — с перезапуском.

| Секция | Ключ | По умолч. | Примечание |
|---|---|---|---|
| General | Enabled | true | общий выключатель |
| General | Style | Game | `Legacy` или `Game` |
| General | AlwaysVisible | false | видно и вне корабля |
| General | ToggleKey | I | клавиша показа (символ или имя InputSystem) |
| General | Language | auto | `auto` (ru, если стоит русификатор, иначе en), `en`, `ru` |
| General | Scale / RightOffsetPx | 1.0 / 20 | масштаб / отступ справа |
| General | PerspectiveStrength | 0 | эффект перспективы; попробуй `0.16` |
| General | FadeWhenIdle / IdleFadeSeconds / IdleMinOpacity | true / 4 / 0.32 | приглушение в бездействии |
| General | CameraSway / CameraSwayStrength | true / 1.0 | панель качается за камерой; `0` — выкл |
| Widgets | Show* (Panel, Timer, Location, Quota, DayDeaths, Monsters, Traps, BrutalEvent, VictoryBanner, Ticker) | true | тумблеры виджетов |
| Behavior | AutoTimer | true | авто старт/пауза таймера |
| Behavior | ShowAllEvents | false | все ивенты или только первый |
| Behavior | ScaleMonstersByCount | false | эксперимент: без цифр, размер/тряска по кол-ву |
| Behavior | RequireScanToShow | false | монстр виден только после сканирования |
| Behavior | ProximityFade | true | ближе — плотнее иконка |
| Behavior | Scanlines | true | CRT-полосы |
| WebSocket | Port | 8181 | порт встроенного моста для браузерного/OBS-оверлея |

### Установка
Положи `LCBridgeOverlay.dll` в `BepInEx/plugins/` (профиль r2modman) или ставь с Thunderstore. Если раньше стоял отдельный **LCBridge** — удали его, он теперь встроен.

### Авторы
- **Solon** — автор.
- Арт монстров/ловушек — рендеры Lethal Company, вычищены под прозрачность.
- Сделано на **BepInEx / HarmonyX / Unity uGUI / TextMeshPro**.

### Лицензия
См. [LICENSE](LICENSE).
