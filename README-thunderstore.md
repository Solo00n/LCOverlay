# LCBridgeOverlay

In-game HUD overlay for Lethal Company.

**Author:** Solo00n

**Language / Язык:** English below, Русский ниже.

An in-helmet HUD panel that shows your quota, moon, interior, live monsters and traps, events and full run analytics without ever opening the terminal.

> This overlay is currently tuned for a specific modpack. In vanilla, or a very different setup, some panels may show empty values or be less useful. Nothing crashes: every mod-specific reader is optional and fails silently. Future updates will add more features and settings so anyone can tailor it to their own game.

## English

### What it does

- Right-side HUD panel, tilted to sit like part of the helmet, in two styles: Legacy (red pixel-grunge) and Game (chat-like blue brackets).
- Quota block with rolling labels: once three quotas are done the tabs continue as Q4 Q5 Q6 and so on, and a compact marker line records every completed set of three.
- Location readout: moon, coloured weather, interior type, item counts inside and outside, beehives, Old Bird warning, and an apparatus icon while the apparatus is still in the facility.
- Run timer with milliseconds. Starts on landing, pauses in orbit, on loading screens and in menus, and resets on a new save.
- Monster icons on the side rails, left for outside and right for the interior, with a count badge and a deck for multiple variants.
- Live monster state icons: the hoarding bug turns angry, the jester pops out, the child eater grows up, the nutcracker takes aim, the snare flea clings to the ceiling, the coil-head freezes while someone looks at it, and turret-equipped heads fire tracers.
- Slayer and kamikaze variants are marked with blood splatter across the icon silhouette.
- Proximity fade: the closer a monster or trap is to you, the more solid its icon, while distant ones fade away.
- A monster icon flashes red the moment it takes a hit.
- Door radar: standing at a main entrance or fire exit reveals what waits on the other side.
- Optional scan gating, so creatures only appear once you have scanned them, using the in-game bestiary.
- Traps on the bottom edge, with turrets firing tracers during a turret event.
- Event plate that drops in after landing, visible to clients as well as the host.
- Victory analytics after every third quota, cumulative across the whole run, including total loot sold to the company.
- Centre-screen countdown over the last ten seconds before the ship leaves.
- Combined scrap-value multiplier from weather and events shown as a single number.
- Camera sway that follows the camera roll, matching camera-motion mods.
- Quality of life: CRT scanlines, idle fade, spectator support, and automatic hiding during game popups, store ads, takeoff and the pause menu.

### How it works

The mod reads the game state about once per second inside its own process and draws the panel with Unity uGUI. Nothing is scraped from the screen and nothing in the game world is modified.

It can also feed a browser or OBS overlay with the same data, for streamers who want it on screen. **That bridge is off by default: until you switch it on in the config, the mod opens no ports at all.** When you do enable it, it listens on `127.0.0.1` only, so nothing outside your own computer can reach it; it sends data one way and ignores anything sent back; and it never makes an outgoing connection of any kind. The mod collects no telemetry and contacts no server. If you previously used the separate LCBridge mod, remove it: that collector is built in now.

### Multiplayer

The overlay is read-only. It never spawns, moves or changes anything in the world, so it cannot desync a lobby.

**The host decides what the lobby sees.** The Lethal Company community does not allow client-side mods that hand one player a significant advantage, so every panel that reveals something the vanilla game keeps from you is granted by the host, for everyone at once: monsters, traps, the door radar and its radius, the apparatus, level loot and the item breakdown, the interior type, events, the end-of-day countdown and the loot multiplier.

- **The host needs the mod too.** Join a host without it and those panels stay dark; the ticker says why, so it does not look like a broken install. Singleplayer is unaffected, since you are always your own host.
- Your own settings can switch any granted panel off for yourself, but cannot switch on what the host has not allowed. The host can also require "scanned monsters only" for the whole lobby.
- The gate is applied where the data is gathered, not where it is drawn, so nothing reaches the stream bridge either.
- Panels the game already shows you — quota, deadline, moon, weather, crew, deaths, the run timer — and every cosmetic setting stay local and always work.
- The handshake uses Netcode's named messages: no extra dependency, no custom NetworkObject, and a vanilla host simply never answers.
- The death counter reports the whole crew, taken from the shared run statistics, so deaths you never witnessed are still counted.
- Event information is read from data the host synchronises to every client, which is why the event plate appears for non-host players too, including those who joined late.
- The ghost girl is shown only in the HUD of the player she is haunting.

### Requirements

- BepInEx 5.4.21 or newer.
- Optional: a stream overlay companion, only if you want the browser or OBS view fed by the built-in bridge.

### Installation

Using a mod manager, which is recommended:

- Install the mod from Thunderstore through r2modman or the Thunderstore app and launch the game from the manager.

Manually:

- Place the mod DLL into the BepInEx plugins folder.
- If the separate LCBridge mod is still installed, delete it.

### Configuration

All settings live in the config file `gdlp.lcbridgeoverlay.cfg`. Widget and behaviour toggles apply immediately, while style, language and scanlines need a restart.

| Key | Default | Description |
| --- | --- | --- |
| Enabled | true | Master switch for the overlay. |
| Style | Game | Visual style: Legacy or Game. |
| AlwaysVisible | false | Keep the panel visible away from the ship. |
| ToggleKey | I | Show or hide the panel. Symbol keys are accepted. |
| Language | auto | Interface language: auto, en or ru. |
| Scale / RightOffsetPx | 1.0 / 20 | Panel size and margin from the right edge. |
| PerspectiveStrength | 0 | Experimental keystone effect. Try 0.16. |
| CameraSway / CameraSwayStrength | true / 1.0 | Panel sways with the camera. Zero disables it. |
| FadeWhenIdle | true | Dim the panel when the camera is still. |
| ProximityFade | true | Closer monsters and traps get a more solid icon. |
| NearestVariantOnly | true | Always one icon per monster, even with several versions. |
| VariantNearDistance | 14 | Within this range the icon locks to the version that is actually near. |
| VariantCycleSeconds | 2 | With none near, versions fade into one another on this interval. |
| DamageFlash | true | Flash the icon red when the monster is hit. |
| DeviantFlipIcon | true | Draw inverted creatures with the icon upside down. |
| JesterWindUpShake | true | The jester icon shakes harder as it winds up, then switches to phase two. |
| TeamDeaths | true | Count the whole crew, not only deaths you saw. |
| DeathsOnlyOnLeave | false | Update the death counter only when leaving a moon. |
| DoorRadar / DoorRadarRadius | true / 22 | Reveal monsters beyond an entrance, and the radius in metres. |
| RequireScanToShow | false | Creatures appear only after you scan them. |
| HideOnPopups / HideOnStoreAd | true / true | Hide during game popups and store ads. |
| ShowEndOfDayCountdown | true | Centre-screen countdown in the final ten seconds. |
| ShowLootMultiplier | true | Combined scrap-value multiplier from weather and events. |
| ShowApparatusIcon | true | Apparatus icon beside the interior until it is carried out. |
| AutoTimer | true | Start and pause the run timer automatically. |
| TimerPauseKey / TimerResetKey | O / None | Manual timer controls. |
| ScaleMonstersByCount | false | Experimental: drop the counts and scale icons by quantity. |
| Scanlines | true | Subtle CRT scanlines. |
| Widgets: Show* | true | Individual toggles for every block of the panel. |
| Enabled | false | Feed a browser or OBS overlay over a local WebSocket. Off by default: while off, no port is opened. |
| Port | 8181 | Port of that bridge, on `127.0.0.1` only. |

### Integrations and compatibility

Every integration is optional. When a mod is absent the related information is simply skipped and the overlay keeps working.

- BrutalCompanyMinusExtraReborn: event plate in the event colours, shown to clients as well as the host.
- WeatherRegistry and WeatherTweaks: combined weather names and the weather part of the scrap-value multiplier.
- LethalLevelLoader: names of custom interiors.
- MoreCompany: crew size.
- MonstersGordion: monsters listed on the Company moon.
- ToilHead: turret-equipped heads get their own icons and fire tracers.
- Camera Overhaul and other camera-motion mods: the panel sways along with the camera.
- DeviantEnemies: inverted creatures are drawn with their icon flipped upside down.
- Russian event names are supported when a Russian translation of the event mod is installed.

### Credits

- Solo00n: author.
- Monster and trap art: Lethal Company renders, keyed for transparency.
- Built on BepInEx, HarmonyX, Unity uGUI and TextMeshPro.

## Русский

Панель прямо в шлеме: квота, луна, интерьер, живые монстры и ловушки, ивенты и полная аналитика забега — без единого захода в терминал.

> Пока оверлей заточен под конкретный модпак. В ванили или сильно другой сборке часть блоков может быть пустой или менее полезной. Ничего не падает: все обращения к другим модам опциональны и молча пропускаются. В планах — больше функций и настроек, чтобы каждый мог подстроить оверлей под себя.

### Что умеет

- Панель у правого края, с наклоном «как часть шлема», в двух стилях: Legacy (красный пиксельный) и Game (как игровой чат, синие уголки).
- Блок квоты с продолжающейся нумерацией: после трёх сданных квот вкладки идут дальше как Q4 Q5 Q6, а компактная линия меток отмечает каждую пройденную тройку.
- Локация: луна, цветная погода, тип интерьера, число предметов внутри и снаружи, ульи, предупреждение об Old Bird и иконка аппарата, пока его не вынесли из комплекса.
- Таймер забега с миллисекундами. Идёт на луне, стоит на орбите, на загрузках и в меню, сбрасывается на новом сейве.
- Иконки монстров по бортам: слева улица, справа комплекс, со счётчиком и колодой из вариантов.
- Иконки состояний: жук звереет, джестер вылезает, ребёнок-людоед взрослеет, наткрекер целится, потолочная личинка висит на потолке, койл замирает под взглядом, а головы с турелью стреляют трассерами.
- Slayer и камикадзе помечаются кровавыми пятнами по силуэту иконки.
- Прозрачность по близости: чем ближе монстр или ловушка, тем плотнее иконка, дальние бледнеют.
- Иконка вспыхивает красным в момент попадания по монстру.
- Радар у двери: стоя у главного входа или пожарного выхода видно, кто ждёт по ту сторону.
- Опциональное сканирование: существо появляется только после скана, по игровому бестиарию.
- Ловушки на нижней кромке, при турельном ивенте турели стреляют трассерами.
- Плашка ивента после посадки, видна и клиентам, а не только хосту.
- Аналитика после каждой третьей квоты, накопительная за весь забег, включая суммарно проданный компании лут.
- Отсчёт по центру экрана за десять секунд до отлёта корабля.
- Суммарный множитель стоимости лута от погоды и ивентов одним числом.
- Покачивание панели вслед за камерой, в такт модам на движение камеры.
- Мелочи для удобства: CRT-полосы, затухание в бездействии, режим наблюдателя и автоскрытие на игровых окнах, рекламе магазина, взлёте и в меню паузы.

### Как устроено

Мод читает состояние игры примерно раз в секунду внутри своего процесса и рисует панель на Unity uGUI. Ничего не считывается с экрана и ничего в мире игры не меняется.

Те же данные можно отдать браузерному или OBS-оверлею — для тех, кто стримит. **Мост выключен по умолчанию: пока не включишь его в конфиге, мод не открывает ни одного порта.** Если включишь, он слушает только `127.0.0.1`, то есть достучаться до него снаружи твоего компьютера нельзя; данные идут в одну сторону, а всё присланное обратно игнорируется; исходящих соединений мод не делает вообще никаких. Никакой телеметрии он не собирает и ни на какой сервер не ходит. Если раньше стоял отдельный мод LCBridge, удали его: сборщик теперь встроен.

### Мультиплеер

Оверлей работает только на чтение. Он ничего не спавнит, не двигает и не меняет в мире, поэтому не может рассинхронизировать лобби.

**Что видит лобби, решает хост.** В сообществе Lethal Company запрещены клиентские моды, дающие одному игроку заметное преимущество, поэтому каждая панель, которая показывает скрытое от ванильной игры, разрешается хостом сразу для всех: монстры, ловушки, радар за дверью и его радиус, аппарат, лут локации и разбивка предметов, тип интерьера, ивенты, таймер конца дня и множитель лута.

- **Мод нужен и хосту тоже.** Зайдёшь к хосту без мода — эти панели останутся погашенными, а бегущая строка объяснит почему, чтобы это не выглядело поломкой. Одиночной игры это не касается: там ты сам себе хост.
- Свои настройки могут выключить любую разрешённую панель лично тебе, но не могут включить то, чего хост не разрешил. Хост также может потребовать «только просканированные монстры» для всего лобби.
- Ворота стоят там, где данные собираются, а не где рисуются, — поэтому наружу, в мост для стрима, тоже ничего не уходит.
- То, что игра и так показывает — квота, дедлайн, луна, погода, экипаж, смерти, таймер забега — и все косметические настройки остаются локальными и работают всегда.
- Обмен идёт именованными сообщениями Netcode: никаких лишних зависимостей и своих NetworkObject, а ванильный хост просто не отвечает.
- Счётчик смертей показывает всю команду по общей статистике забега, поэтому смерти, которых ты не видел, тоже учтены.
- Информация об ивентах читается из данных, которые хост синхронизирует всем, поэтому плашка появляется и у не-хоста, включая тех, кто подключился позже.
- Девочка-призрак видна только тому игроку, которого она преследует.

### Требования

- BepInEx 5.4.21 или новее.
- Опционально: компаньон для стрим-оверлея, если нужен браузерный или OBS-вид от встроенного моста.

### Установка

Через менеджер модов, рекомендуемый способ:

- Поставь мод с Thunderstore через r2modman или приложение Thunderstore и запусти игру из менеджера.

Вручную:

- Положи DLL мода в папку плагинов BepInEx.
- Если ещё стоит отдельный мод LCBridge, удали его.

### Настройки

Все настройки лежат в файле конфига `gdlp.lcbridgeoverlay.cfg`. Тумблеры виджетов и поведения применяются на лету, стиль, язык и полосы требуют перезапуска.

| Ключ | По умолчанию | Описание |
| --- | --- | --- |
| Enabled | true | Общий выключатель оверлея. |
| Style | Game | Стиль оформления: Legacy или Game. |
| AlwaysVisible | false | Показывать панель и вне корабля. |
| ToggleKey | I | Клавиша показа и скрытия. Символьные клавиши тоже принимаются. |
| Language | auto | Язык интерфейса: auto, en или ru. |
| Scale / RightOffsetPx | 1.0 / 20 | Размер панели и отступ от правого края. |
| PerspectiveStrength | 0 | Экспериментальная перспектива. Попробуй 0.16. |
| CameraSway / CameraSwayStrength | true / 1.0 | Покачивание панели за камерой. Ноль отключает. |
| FadeWhenIdle | true | Приглушать панель, когда камера неподвижна. |
| ProximityFade | true | Чем ближе монстр или ловушка, тем плотнее иконка. |
| NearestVariantOnly | true | Всегда одна иконка на монстра, даже если версий несколько. |
| VariantNearDistance | 14 | В пределах этой дистанции иконка закрепляется за версией, что рядом. |
| VariantCycleSeconds | 2 | Если рядом никого — версии плавно сменяют друг друга с этим интервалом. |
| DamageFlash | true | Вспышка иконки при попадании по монстру. |
| DeviantFlipIcon | true | Инверснутые существа рисуются иконкой вверх ногами. |
| JesterWindUpShake | true | Пока джестер заводится, иконка трясётся всё сильнее, затем меняется на 2-ю фазу. |
| TeamDeaths | true | Считать смерти всей команды, а не только увиденные. |
| DeathsOnlyOnLeave | false | Обновлять счётчик смертей только при отлёте с луны. |
| DoorRadar / DoorRadarRadius | true / 22 | Показывать монстров за дверью и радиус радара в метрах. |
| RequireScanToShow | false | Существо появляется только после сканирования. |
| HideOnPopups / HideOnStoreAd | true / true | Прятать на игровых окнах и на рекламе магазина. |
| ShowEndOfDayCountdown | true | Отсчёт по центру экрана за последние десять секунд. |
| ShowLootMultiplier | true | Суммарный множитель стоимости лута от погоды и ивентов. |
| ShowApparatusIcon | true | Иконка аппарата у интерьера, пока его не вынесли. |
| AutoTimer | true | Автоматический запуск и пауза таймера забега. |
| TimerPauseKey / TimerResetKey | O / None | Ручное управление таймером. |
| ScaleMonstersByCount | false | Эксперимент: убрать цифры и менять размер иконок по количеству. |
| Scanlines | true | Едва заметные CRT-полосы. |
| Widgets: Show* | true | Отдельные тумблеры для каждого блока панели. |
| Enabled | false | Отдавать данные браузерному или OBS-оверлею по локальному WebSocket. По умолчанию выключено: пока выключено, порт не открывается. |
| Port | 8181 | Порт этого моста, только на `127.0.0.1`. |

### Совместимость и интеграции

Все интеграции опциональны. Если мода нет, соответствующая информация просто пропускается, а оверлей продолжает работать.

- BrutalCompanyMinusExtraReborn: плашка ивента в его цветах, видна и клиентам, и хосту.
- WeatherRegistry и WeatherTweaks: комбинированная погода и погодная часть множителя стоимости лута.
- LethalLevelLoader: названия кастомных интерьеров.
- MoreCompany: размер экипажа.
- MonstersGordion: монстры на луне компании.
- ToilHead: головы с турелью получают свои иконки и стреляют трассерами.
- Camera Overhaul и другие моды на движение камеры: панель качается вместе с камерой.
- DeviantEnemies: инверснутые существа рисуются перевёрнутой вверх ногами иконкой.
- Русские названия ивентов поддерживаются, если установлен русификатор мода на ивенты.

### Авторы

- Solo00n: автор.
- Арт монстров и ловушек: рендеры Lethal Company, вычищенные под прозрачность.
- Сделано на BepInEx, HarmonyX, Unity uGUI и TextMeshPro.
