<table>
<tr>
<td width="140" valign="middle"><img src="icon.png" width="128" alt="LCBridgeOverlay"></td>
<td valign="middle">
<h1><span style="color: #cc0000;">LCBRIDGEOVERLAY</span></h1>
<p>In-game HUD overlay for Lethal Company.</p>
</td>
</tr>
</table>

![Game](https://img.shields.io/badge/Lethal%20Company-v81-cc0000?style=flat-square)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4.21%2B-cc0000?style=flat-square)
![Version](https://img.shields.io/badge/version-1.5.2-cc0000?style=flat-square)
![License](https://img.shields.io/badge/license-custom-cc0000?style=flat-square)

**Language / Язык:** [English](#english) · [Русский](#russian)

<a name="english"></a>

## <span style="color: #cc0000;">LCBRIDGEOVERLAY</span>

**Author:** <span style="color: #cc0000;">Solo00n</span>

An in-helmet HUD panel that shows your quota, moon, interior, live monsters and traps, events and full run analytics without ever opening the terminal.

<blockquote style="border-left: 4px solid #cc0000; padding-left: 15px;">
This overlay is currently tuned for a specific modpack. In vanilla, or a very different setup, some panels may show empty values or be less useful. Nothing crashes: every mod-specific reader is optional and fails silently. Future updates will add more features and settings so anyone can tailor it to their own game.
</blockquote>

### <span style="color: #cc0000;">WHAT IT DOES</span>

- Right-side HUD panel, tilted to sit like part of the helmet, in two styles: <strong style="color: #cc0000;">Legacy</strong> (red pixel-grunge) and <strong style="color: #cc0000;">Game</strong> (chat-like blue brackets).
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

### <span style="color: #cc0000;">HOW IT WORKS</span>

The mod reads the game state about once per second inside its own process and draws the panel with Unity uGUI. Nothing is scraped from the screen and nothing in the game world is modified.

It also hosts a small local WebSocket bridge on port <code>8181</code>, so a browser or OBS overlay can display the same data on stream. If you previously used the separate LCBridge mod, remove it: that collector is built in now, and running both would fight over the same port.

### <span style="color: #cc0000;">MULTIPLAYER</span>

<blockquote style="border-left: 4px solid #cc0000; padding-left: 15px;">
The overlay is purely <strong style="color: #cc0000;">client-side</strong> and read-only. It never spawns, moves or changes anything in the world, so it does not need host authority and cannot desync a lobby.
</blockquote>

- Every player installs it for themselves. It is not required on the host, and a host running it does not force it on anyone.
- The death counter reports the whole crew, taken from the shared run statistics, so deaths you never witnessed are still counted.
- Event information is read from data the host synchronises to every client, which is why the event plate appears for non-host players too, including those who joined late.
- The ghost girl is shown only in the HUD of the player she is haunting.

### <span style="color: #cc0000;">REQUIREMENTS</span>

- BepInEx <strong style="color: #cc0000;">5.4.21</strong> or newer.
- Optional: a stream overlay companion, only if you want the browser or OBS view fed by the built-in bridge.

### <span style="color: #cc0000;">INSTALLATION</span>

Using a mod manager, which is recommended:

- Install the mod from Thunderstore through r2modman or the Thunderstore app and launch the game from the manager.

Manually:

- Place <code>LCBridgeOverlay.dll</code> into <code>BepInEx/plugins/</code>.
- If the separate LCBridge mod is still installed, delete it.

### <span style="color: #cc0000;">CONFIGURATION</span>

All settings live in <code>BepInEx/config/gdlp.lcbridgeoverlay.cfg</code>. Widget and behaviour toggles apply immediately, while style, language and scanlines need a restart.

<table style="border: 1px solid #cc0000; border-collapse: collapse;">
<thead>
<tr style="background-color: #1a1a1a;">
<th style="border: 1px solid #cc0000; padding: 8px; color: #cc0000;">KEY</th>
<th style="border: 1px solid #cc0000; padding: 8px; color: #cc0000;">DEFAULT</th>
<th style="border: 1px solid #cc0000; padding: 8px; color: #cc0000;">DESCRIPTION</th>
</tr>
</thead>
<tbody>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Enabled</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Master switch for the overlay.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Style</td><td style="border: 1px solid #cc0000; padding: 8px;">Game</td><td style="border: 1px solid #cc0000; padding: 8px;">Visual style: Legacy or Game.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">AlwaysVisible</td><td style="border: 1px solid #cc0000; padding: 8px;">false</td><td style="border: 1px solid #cc0000; padding: 8px;">Keep the panel visible away from the ship.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ToggleKey</td><td style="border: 1px solid #cc0000; padding: 8px;">I</td><td style="border: 1px solid #cc0000; padding: 8px;">Show or hide the panel. Symbol keys are accepted.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Language</td><td style="border: 1px solid #cc0000; padding: 8px;">auto</td><td style="border: 1px solid #cc0000; padding: 8px;">Interface language: auto, en or ru.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Scale / RightOffsetPx</td><td style="border: 1px solid #cc0000; padding: 8px;">1.0 / 20</td><td style="border: 1px solid #cc0000; padding: 8px;">Panel size and margin from the right edge.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">PerspectiveStrength</td><td style="border: 1px solid #cc0000; padding: 8px;">0</td><td style="border: 1px solid #cc0000; padding: 8px;">Experimental keystone effect. Try 0.16.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">CameraSway / CameraSwayStrength</td><td style="border: 1px solid #cc0000; padding: 8px;">true / 1.0</td><td style="border: 1px solid #cc0000; padding: 8px;">Panel sways with the camera. Zero disables it.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">FadeWhenIdle</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Dim the panel when the camera is still.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ProximityFade</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Closer monsters and traps get a more solid icon.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">NearestVariantOnly</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Always one icon per monster, even with several versions.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">VariantNearDistance</td><td style="border: 1px solid #cc0000; padding: 8px;">14</td><td style="border: 1px solid #cc0000; padding: 8px;">Within this range the icon locks to the version that is actually near.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">VariantCycleSeconds</td><td style="border: 1px solid #cc0000; padding: 8px;">2</td><td style="border: 1px solid #cc0000; padding: 8px;">With none near, versions fade into one another on this interval.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">DamageFlash</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Flash the icon red when the monster is hit.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">DeviantFlipIcon</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Draw inverted creatures with the icon upside down.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">JesterWindUpShake</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">The jester icon shakes harder as it winds up, then switches to phase two.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">TeamDeaths</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Count the whole crew, not only deaths you saw.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">DeathsOnlyOnLeave</td><td style="border: 1px solid #cc0000; padding: 8px;">false</td><td style="border: 1px solid #cc0000; padding: 8px;">Update the death counter only when leaving a moon.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">DoorRadar / DoorRadarRadius</td><td style="border: 1px solid #cc0000; padding: 8px;">true / 22</td><td style="border: 1px solid #cc0000; padding: 8px;">Reveal monsters beyond an entrance, and the radius in metres.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">RequireScanToShow</td><td style="border: 1px solid #cc0000; padding: 8px;">false</td><td style="border: 1px solid #cc0000; padding: 8px;">Creatures appear only after you scan them.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">HideOnPopups / HideOnStoreAd</td><td style="border: 1px solid #cc0000; padding: 8px;">true / true</td><td style="border: 1px solid #cc0000; padding: 8px;">Hide during game popups and store ads.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ShowEndOfDayCountdown</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Centre-screen countdown in the final ten seconds.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ShowLootMultiplier</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Combined scrap-value multiplier from weather and events.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ShowApparatusIcon</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Apparatus icon beside the interior until it is carried out.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">AutoTimer</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Start and pause the run timer automatically.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">TimerPauseKey / TimerResetKey</td><td style="border: 1px solid #cc0000; padding: 8px;">O / None</td><td style="border: 1px solid #cc0000; padding: 8px;">Manual timer controls.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ScaleMonstersByCount</td><td style="border: 1px solid #cc0000; padding: 8px;">false</td><td style="border: 1px solid #cc0000; padding: 8px;">Experimental: drop the counts and scale icons by quantity.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Scanlines</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Subtle CRT scanlines.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Widgets: Show*</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Individual toggles for every block of the panel.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Port</td><td style="border: 1px solid #cc0000; padding: 8px;">8181</td><td style="border: 1px solid #cc0000; padding: 8px;">Port of the built-in bridge for a browser or OBS overlay.</td></tr>
</tbody>
</table>

### <span style="color: #cc0000;">INTEGRATIONS AND COMPATIBILITY</span>

Every integration is optional. When a mod is absent the related information is simply skipped and the overlay keeps working.

- <strong style="color: #cc0000;">BrutalCompanyMinusExtraReborn</strong> — event plate in the event colours, shown to clients as well as the host.
- <strong style="color: #cc0000;">WeatherRegistry and WeatherTweaks</strong> — combined weather names and the weather part of the scrap-value multiplier.
- <strong style="color: #cc0000;">LethalLevelLoader</strong> — names of custom interiors.
- <strong style="color: #cc0000;">MoreCompany</strong> — crew size.
- <strong style="color: #cc0000;">MonstersGordion</strong> — monsters listed on the Company moon.
- <strong style="color: #cc0000;">ToilHead</strong> — turret-equipped heads get their own icons and fire tracers.
- <strong style="color: #cc0000;">Camera Overhaul</strong> and other camera-motion mods — the panel sways along with the camera.
- <strong style="color: #cc0000;">DeviantEnemies</strong> — inverted creatures are drawn with their icon flipped upside down.
- Russian event names are supported when a Russian translation of the event mod is installed.

### <span style="color: #cc0000;">BUILD</span>

```
dotnet build -c Release
```

The result appears in <code>bin/Release/LCBridgeOverlay.dll</code>. NuGet pulls BepInEx and the game references automatically, and the monster art is embedded at build time.

### <span style="color: #cc0000;">CREDITS</span>

- <strong style="color: #cc0000;">Solo00n</strong> — author.
- Monster and trap art: Lethal Company renders, keyed for transparency.
- Built on BepInEx, HarmonyX, Unity uGUI and TextMeshPro.
- Licence: see the LICENSE file in this repository.

<a name="russian"></a>

## <span style="color: #cc0000;">LCBRIDGEOVERLAY</span>

**Автор:** <span style="color: #cc0000;">Solo00n</span>

Панель прямо в шлеме: квота, луна, интерьер, живые монстры и ловушки, ивенты и полная аналитика забега — без единого захода в терминал.

<blockquote style="border-left: 4px solid #cc0000; padding-left: 15px;">
Пока оверлей заточен под конкретный модпак. В ванили или сильно другой сборке часть блоков может быть пустой или менее полезной. Ничего не падает: все обращения к другим модам опциональны и молча пропускаются. В планах — больше функций и настроек, чтобы каждый мог подстроить оверлей под себя.
</blockquote>

### <span style="color: #cc0000;">ЧТО УМЕЕТ</span>

- Панель у правого края, с наклоном «как часть шлема», в двух стилях: <strong style="color: #cc0000;">Legacy</strong> (красный пиксельный) и <strong style="color: #cc0000;">Game</strong> (как игровой чат, синие уголки).
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

### <span style="color: #cc0000;">КАК УСТРОЕНО</span>

Мод читает состояние игры примерно раз в секунду внутри своего процесса и рисует панель на Unity uGUI. Ничего не считывается с экрана и ничего в мире игры не меняется.

Дополнительно поднимается локальный WebSocket-мост на порту <code>8181</code>, чтобы браузерный или OBS-оверлей показывал те же данные на стриме. Если раньше стоял отдельный мод LCBridge, удали его: сборщик теперь встроен, а вдвоём они займут один порт.

### <span style="color: #cc0000;">МУЛЬТИПЛЕЕР</span>

<blockquote style="border-left: 4px solid #cc0000; padding-left: 15px;">
Оверлей полностью <strong style="color: #cc0000;">клиентский</strong> и работает только на чтение. Он ничего не спавнит, не двигает и не меняет в мире, поэтому не требует прав хоста и не может рассинхронизировать лобби.
</blockquote>

- Каждый игрок ставит его себе сам. Хосту он не нужен, и хост никому его не навязывает.
- Счётчик смертей показывает всю команду по общей статистике забега, поэтому смерти, которых ты не видел, тоже учтены.
- Информация об ивентах читается из данных, которые хост синхронизирует всем, поэтому плашка появляется и у не-хоста, включая тех, кто подключился позже.
- Девочка-призрак видна только тому игроку, которого она преследует.

### <span style="color: #cc0000;">ТРЕБОВАНИЯ</span>

- BepInEx <strong style="color: #cc0000;">5.4.21</strong> или новее.
- Опционально: компаньон для стрим-оверлея, если нужен браузерный или OBS-вид от встроенного моста.

### <span style="color: #cc0000;">УСТАНОВКА</span>

Через менеджер модов, рекомендуемый способ:

- Поставь мод с Thunderstore через r2modman или приложение Thunderstore и запусти игру из менеджера.

Вручную:

- Положи <code>LCBridgeOverlay.dll</code> в <code>BepInEx/plugins/</code>.
- Если ещё стоит отдельный мод LCBridge, удали его.

### <span style="color: #cc0000;">НАСТРОЙКИ</span>

Все настройки лежат в <code>BepInEx/config/gdlp.lcbridgeoverlay.cfg</code>. Тумблеры виджетов и поведения применяются на лету, стиль, язык и полосы требуют перезапуска.

<table style="border: 1px solid #cc0000; border-collapse: collapse;">
<thead>
<tr style="background-color: #1a1a1a;">
<th style="border: 1px solid #cc0000; padding: 8px; color: #cc0000;">КЛЮЧ</th>
<th style="border: 1px solid #cc0000; padding: 8px; color: #cc0000;">ПО УМОЛЧАНИЮ</th>
<th style="border: 1px solid #cc0000; padding: 8px; color: #cc0000;">ОПИСАНИЕ</th>
</tr>
</thead>
<tbody>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Enabled</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Общий выключатель оверлея.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Style</td><td style="border: 1px solid #cc0000; padding: 8px;">Game</td><td style="border: 1px solid #cc0000; padding: 8px;">Стиль оформления: Legacy или Game.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">AlwaysVisible</td><td style="border: 1px solid #cc0000; padding: 8px;">false</td><td style="border: 1px solid #cc0000; padding: 8px;">Показывать панель и вне корабля.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ToggleKey</td><td style="border: 1px solid #cc0000; padding: 8px;">I</td><td style="border: 1px solid #cc0000; padding: 8px;">Клавиша показа и скрытия. Символьные клавиши тоже принимаются.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Language</td><td style="border: 1px solid #cc0000; padding: 8px;">auto</td><td style="border: 1px solid #cc0000; padding: 8px;">Язык интерфейса: auto, en или ru.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Scale / RightOffsetPx</td><td style="border: 1px solid #cc0000; padding: 8px;">1.0 / 20</td><td style="border: 1px solid #cc0000; padding: 8px;">Размер панели и отступ от правого края.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">PerspectiveStrength</td><td style="border: 1px solid #cc0000; padding: 8px;">0</td><td style="border: 1px solid #cc0000; padding: 8px;">Экспериментальная перспектива. Попробуй 0.16.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">CameraSway / CameraSwayStrength</td><td style="border: 1px solid #cc0000; padding: 8px;">true / 1.0</td><td style="border: 1px solid #cc0000; padding: 8px;">Покачивание панели за камерой. Ноль отключает.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">FadeWhenIdle</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Приглушать панель, когда камера неподвижна.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ProximityFade</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Чем ближе монстр или ловушка, тем плотнее иконка.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">NearestVariantOnly</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Всегда одна иконка на монстра, даже если версий несколько.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">VariantNearDistance</td><td style="border: 1px solid #cc0000; padding: 8px;">14</td><td style="border: 1px solid #cc0000; padding: 8px;">В пределах этой дистанции иконка закрепляется за версией, что рядом.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">VariantCycleSeconds</td><td style="border: 1px solid #cc0000; padding: 8px;">2</td><td style="border: 1px solid #cc0000; padding: 8px;">Если рядом никого — версии плавно сменяют друг друга с этим интервалом.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">DamageFlash</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Вспышка иконки при попадании по монстру.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">DeviantFlipIcon</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Инверснутые существа рисуются иконкой вверх ногами.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">JesterWindUpShake</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Пока джестер заводится, иконка трясётся всё сильнее, затем меняется на 2-ю фазу.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">TeamDeaths</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Считать смерти всей команды, а не только увиденные.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">DeathsOnlyOnLeave</td><td style="border: 1px solid #cc0000; padding: 8px;">false</td><td style="border: 1px solid #cc0000; padding: 8px;">Обновлять счётчик смертей только при отлёте с луны.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">DoorRadar / DoorRadarRadius</td><td style="border: 1px solid #cc0000; padding: 8px;">true / 22</td><td style="border: 1px solid #cc0000; padding: 8px;">Показывать монстров за дверью и радиус радара в метрах.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">RequireScanToShow</td><td style="border: 1px solid #cc0000; padding: 8px;">false</td><td style="border: 1px solid #cc0000; padding: 8px;">Существо появляется только после сканирования.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">HideOnPopups / HideOnStoreAd</td><td style="border: 1px solid #cc0000; padding: 8px;">true / true</td><td style="border: 1px solid #cc0000; padding: 8px;">Прятать на игровых окнах и на рекламе магазина.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ShowEndOfDayCountdown</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Отсчёт по центру экрана за последние десять секунд.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ShowLootMultiplier</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Суммарный множитель стоимости лута от погоды и ивентов.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ShowApparatusIcon</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Иконка аппарата у интерьера, пока его не вынесли.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">AutoTimer</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Автоматический запуск и пауза таймера забега.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">TimerPauseKey / TimerResetKey</td><td style="border: 1px solid #cc0000; padding: 8px;">O / None</td><td style="border: 1px solid #cc0000; padding: 8px;">Ручное управление таймером.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">ScaleMonstersByCount</td><td style="border: 1px solid #cc0000; padding: 8px;">false</td><td style="border: 1px solid #cc0000; padding: 8px;">Эксперимент: убрать цифры и менять размер иконок по количеству.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Scanlines</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Едва заметные CRT-полосы.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Widgets: Show*</td><td style="border: 1px solid #cc0000; padding: 8px;">true</td><td style="border: 1px solid #cc0000; padding: 8px;">Отдельные тумблеры для каждого блока панели.</td></tr>
<tr><td style="border: 1px solid #cc0000; padding: 8px;">Port</td><td style="border: 1px solid #cc0000; padding: 8px;">8181</td><td style="border: 1px solid #cc0000; padding: 8px;">Порт встроенного моста для браузерного или OBS-оверлея.</td></tr>
</tbody>
</table>

### <span style="color: #cc0000;">СОВМЕСТИМОСТЬ И ИНТЕГРАЦИИ</span>

Все интеграции опциональны. Если мода нет, соответствующая информация просто пропускается, а оверлей продолжает работать.

- <strong style="color: #cc0000;">BrutalCompanyMinusExtraReborn</strong> — плашка ивента в его цветах, видна и клиентам, и хосту.
- <strong style="color: #cc0000;">WeatherRegistry и WeatherTweaks</strong> — комбинированная погода и погодная часть множителя стоимости лута.
- <strong style="color: #cc0000;">LethalLevelLoader</strong> — названия кастомных интерьеров.
- <strong style="color: #cc0000;">MoreCompany</strong> — размер экипажа.
- <strong style="color: #cc0000;">MonstersGordion</strong> — монстры на луне компании.
- <strong style="color: #cc0000;">ToilHead</strong> — головы с турелью получают свои иконки и стреляют трассерами.
- <strong style="color: #cc0000;">Camera Overhaul</strong> и другие моды на движение камеры — панель качается вместе с камерой.
- <strong style="color: #cc0000;">DeviantEnemies</strong> — инверснутые существа рисуются перевёрнутой вверх ногами иконкой.
- Русские названия ивентов поддерживаются, если установлен русификатор мода на ивенты.

### <span style="color: #cc0000;">СБОРКА</span>

```
dotnet build -c Release
```

Результат появится в <code>bin/Release/LCBridgeOverlay.dll</code>. NuGet сам подтянет BepInEx и игровые ссылки, а арт монстров встраивается при сборке.

### <span style="color: #cc0000;">АВТОРЫ</span>

- <strong style="color: #cc0000;">Solo00n</strong> — автор.
- Арт монстров и ловушек: рендеры Lethal Company, вычищенные под прозрачность.
- Сделано на BepInEx, HarmonyX, Unity uGUI и TextMeshPro.
- Лицензия: см. файл LICENSE в репозитории.
