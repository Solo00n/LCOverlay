# Changelog — LCBridgeOverlay

## 1.6.0
- **The host now decides what the lobby sees.** Lethal Company does not allow client-side mods that hand one player a significant advantage, and this overlay was doing exactly that: anyone could install it alone and read the level's monsters, traps and loot. Every panel that reveals something the vanilla game keeps from you is now granted by the host, for the whole lobby at once, over a small networked handshake. Join a host who does not have the mod and those panels stay dark; the ticker says so, rather than leaving you to wonder whether the mod broke. Singleplayer is unaffected, since you are always your own host there.
- Host-granted panels: monsters, traps, the door radar and its radius, the apparatus indicator, events, the end-of-day countdown, the loot multiplier, level loot totals and the item breakdown, and the interior type. A local toggle can still switch any of them off for yourself, but it can no longer switch on what the host has not allowed, and the host can force "scanned monsters only" on everyone.
- The gate is applied where the data is collected, not where it is drawn, so nothing leaks out through the stream bridge either.
- Panels the game already shows you — quota, deadline, moon, weather, crew, deaths, the run timer and every cosmetic setting — stay entirely local and are unchanged.
- Networking uses Netcode's named messages: no extra dependency, no custom NetworkObject, and a vanilla host simply never answers.

## 1.5.4
- **The stream bridge is now off by default.** It used to start on every launch, so the mod opened a local port on every machine that installed it, whether or not anyone ever used OBS. It is now opt-in via `[WebSocket] Enabled`: while it is off, no socket is created at all. When it is on, it listens on `127.0.0.1` only, sends data one way, ignores anything sent back, and makes no outgoing connections. The in-game overlay never needed the bridge — it reads the data directly, in-process.
- Package description and README rewritten to state the networking behaviour plainly and to stop implying that other mods are required.

## 1.5.3
- **Event names now show for everyone, not just the host.** The event list Brutal Company fills in exists only on the host, so every other player saw an empty plaque. The names are now read from the event panel's own text, which is a synced value, and matched against the full event list that each client builds locally from its config — so the match is exact and independent of language. Two earlier attempts missed because the announcement hook only fires when a per-event "Show Tip?" option is on, which it isn't by default, and because panel lines were compared against internal identifiers rather than the displayed names.
- **The same host-only read was fixed in the run analytics**, where the active event is attributed to each death, and in the **loot multiplier**, which now prefers a synced value over the host-only one.

## 1.5.2
- **DeviantEnemies integration**: inverted creatures are drawn with their icon flipped upside down, and they count as a separate variant, so a deviant and a normal one of the same species no longer collapse into a single entry. Toggle with `DeviantFlipIcon`.
- **Fixes:** a new save resets the run timer again (the reset signal had been lost when the eject analytics were added); the overlay now reliably returns after a store ad, since the game never clears its ad reference and the check stayed stuck.
- **Countdown** also follows FacilityMeltdown: with the apparatus pulled the ship leaves on the meltdown timer, so the countdown targets whichever departure comes first and still only shows for the last ten seconds.
- **Countdown polish:** the zero moment is latched once and counted locally, so no digit is skipped; each second now spawns its own digit that grows and fades over three seconds, letting them overlap instead of cutting each other off, with a short interface sound on every tick.

## 1.5.1
- **Damage flash now works.** `HitEnemy` is virtual and 21 enemies override it, so patching the base method alone missed almost every monster. All overrides are patched, and the flash is triggered directly instead of travelling through the one-second bridge tick.
- **End-of-day countdown fixed and restyled.** It used to read `0` permanently (a normalised threshold was subtracted from day units), so it also sat on screen all day. It is now a centre-screen number over the last ten seconds, counted locally so no digit is skipped, and each second grows and fades as it flies toward you.
- **Weather is now part of the loot multiplier.** The weather contribution was never counted: it is an instance property on the current weather, not a static field. It is read through the weather mod's current-weather API and honours that mod's own "use scrap multipliers" setting. The multiplier row is always shown while enabled, so x1 is visible too.
- **Same monster inside and outside** now fades independently — both rails previously shared one distance.
- **Quota marks** are a single centred line of five that recolours on each pass instead of growing new rows, and the analytics banner closes when the lever is pulled so the panel returns to its normal size.
- **Door radar** triggers reliably: the "at a door" range was 6 m and almost never matched; it is now 14 m measured from the door itself.
- Re-entering a save resets the overlay; the apparatus icon sits on the interior line; apparatus art re-keyed; new eye indicator.

## 1.5.0
- **Quota marks & endless runs.** Every completed set of 3 quotas adds a small mark under the quota block (15 per row, colour changes after the first row, counts up to 100), and the tab labels roll onward: `Q1 Q2 Q3` → `Q4 Q5 Q6` → … Run analytics now appear at the end of **every** set of three and are **cumulative for the whole run**.
- **Team-wide death counter** (`TeamDeaths`, on by default) — counts every player's death, even ones you didn't witness. Optional `DeathsOnlyOnLeave` updates the counter only when the ship leaves a moon.
- **Monsters behind the door** (`DoorRadar`) — standing at a main entrance or fire exit reveals what's on the other side, via a virtual radar at the paired door (radius configurable, default 22 m).
- **Auto-hide on game popups** (`HideOnPopups`) — tips, quota-delivery and end-of-day screens hide the overlay, which then returns to its previous state.
- **Auto-hide on store ads** (`HideOnStoreAd`) — during a discount ad the overlay hides and can't be toggled back until it ends.
- **Sold-loot total** in the analytics — how much was actually sold to the company across the run.
- **Previous-run analytics after `eject`** — the summary stays up on the new ship and resets only when someone pulls the lever (kept in memory for the session only).
- **Nearest variant only** (`NearestVariantOnly`) — when a monster exists in several versions (e.g. plain and turret-equipped), only the one closest to you is shown.
- **Combined loot multiplier** (`ShowLootMultiplier`) — weather and event scrap-value multipliers summed into a single number.
- **End-of-day countdown** (`ShowEndOfDayCountdown`) — appears in the last 10 seconds.
- **Damage flash** (`DamageFlash`) — a monster's icon briefly flashes red when it takes damage (separate from the slayer blood-splatter).
- **Apparatus icon** next to the interior while the apparatus is still inside; it disappears once it's carried out.
- **New eye icon** for the connection indicator.
- **Fixes:** the outside-scrap count no longer misses loot that Brutal Company events spawn shortly after landing (the snapshot used to lock on the first non-zero value); BCME events now also read BCME's synced panel variable, so **clients** see them even when the announce RPC is missed.
- Verified: the timer runs on the Company moon (Gordion) and stops in orbit.

## 1.4.1
- **Proximity fade** (`ProximityFade`, default on) — the closer a monster **or trap** is to you, the more opaque its icon; distant ones fade toward transparent.
- **Trap rail now matches the monster rails** — icons sit on the bottom border line (following the event plate, lowered so they don't overlap the event text) and fade by proximity.
- **Grabbable traps counted** — BCME's `GrabbableTurret`/`GrabbableLandmine` (which destroy & replace the normal ones) now show up as turrets/mines instead of vanishing.
- **Scan gating uses the in-game bestiary** (`Terminal.scannedEnemyIDs`) — once you've scanned a creature it shows, reliably.
- Overlay **hides during takeoff** (while the game shows the center-screen info and the leave-lever is disabled), and stays visible on **landing**.
- **Green halo** around icons removed — the keyed-out transparent border is colour-bled so bilinear downscaling can't smear green; **Gunkfish/Stingray** icon maps correctly.
- README/manifest/LICENSE de-branded; docs updated.

## 1.4.0
- **New monster icon pack** — all monster/trap icons replaced with higher-quality green-screen renders, keyed and trimmed cleanly (incl. genuinely dark monsters like the Bracken).
- **Monster state icons.** Many monsters now switch icon by their live state, chosen by priority (angry/attacking > aggression > transformed > special > passive):
  - Hoarding Bug — passive / **angry**
  - Jester — folded / **popped (angry)**
  - Child Eater (Cave Dweller) — child / **adult**
  - Nutcracker — passive / **attacking**
  - Snare Flea — floor / **ceiling** (ceiling takes priority)
  - Coil-Head — sway **stops** while any player looks at it (frozen)
  - ToilHead — plain head / **turret**, and it fires tracers when its turret shoots
- **Slayer / kamikaze variants** now get a **blood-splatter overlay** baked onto the icon silhouette, instead of a flat red recolour.
- **`RequireScanToShow`** (config, default off) — monsters only appear in the overlay after you've scanned them.
- **Ghost Girl** only shows in the HUD of the player she's haunting.
- **Camera Overhaul synergy** — the panel subtly tilts/drifts with the camera (reads the camera's real roll, so it works with any camera-motion mod, and gives a gentle sway in vanilla). Config: `CameraSway`, `CameraSwayStrength`.
- **Multiplayer fixes:** BCME events now show on **clients** too (captured from BCME's synced tip), not just the host; and the overlay stays visible while you're **spectating** (dead).
- Detection runs on a ~0.5 s timer (not per-frame) and is all soft/reflection — missing mods never break the overlay.
- **Fixes:**
  - Overlay now fills in **instantly on landing** (event + interior + monsters in one packet) — forced refresh on ship-landed and on level-generated, instead of trailing in over a second.
  - Auto-timer no longer keeps ticking **on orbit / during loads** — it runs only while actually landed on a moon.
  - **Toggle-key rebinding works** — symbol keys like `\` are accepted (were silently falling back to `I`), and the key applies live from config without a restart.

## 1.3.0
- **LCBridge is now built in.** The overlay collects the game state itself and hosts the WebSocket bridge (`ws://localhost:8181`) that the OBS/HTML overlay reads — so **the separate LCBridge mod is no longer needed** (remove it to avoid a port conflict). One mod, no inter-mod dependency.
- Data path is now in-process (no self-connecting WebSocket client); the eye indicator tracks the bridge "heartbeat".
- **Manual RU translation of BCME event names** (~270 events) — replaces the auto-translator, which couldn't handle BCME's internal event IDs.
- **Full language consistency:** everything now follows the language setting (EN or RU) — death causes are no longer hard-coded, and the victory stamp/title localise properly. No more mixed EN/RU.
- `ScaleMonstersByCount` now scales **trap** icons by count too, not just monsters.
- New icon: **Lasso Man** (no longer hidden); redone HD icons for Feiopar, Giant Sapsucker, Gunkfish, Red Locust.
- **Fixes:** the victory banner now tilts/warps with the rest of the panel; the `Q1/Q2/Q3` tabs stay inside their frames when the panel grows (victory screen); the overlay now **dims into the background** while the pause (Esc) menu is open, so it no longer competes with the menu (LC's menu is camera-space, so a Screen-Space-Overlay panel can't render strictly behind it).
- Config `[WebSocket]` simplified to a single `Port` (the built-in bridge port).

## 1.2.0
- Helmet-HUD **tilt** (always on) + optional **perspective / keystone** effect (`PerspectiveStrength`).
- **Eye indicator** moved to the header, centered next to the timer; closes like an eyelid when leaving the ship.
- **CRT scanlines** + worn (grunge) frame edges.
- **Idle fade** with configurable delay and opacity (`FadeWhenIdle`, `IdleFadeSeconds`, `IdleMinOpacity`).
- **Pause-aware:** panel stays on the game plane under the Esc menu (no longer hidden).
- **Timer** shows milliseconds (fixed-width, no more flicker on ms updates).
- Monster rails: icons under the same angle/perspective, top-aligned (outside = inside), count badge in the corner; stable order (no per-second re-pop); experimental `ScaleMonstersByCount`.
- **MonstersGordion** support (via LCBridge) — monsters on the Company moon.
- **BCME RUS / XUnity.AutoTranslator** integration — translated event names.
- Removed redundant `UseLegacyStyle` (use `Style`) and the `TiltAngle` config (tilt is now a fixed base).
- New icons: Masked, ToilHead (coil+turret); Barber name alias fix (ClaySurgeon).

## 1.1.0
- Monster/trap **icons on the side rails** (baked into the DLL), turret-fire tracers.
- **Victory banner** with run analytics; **ticker**.
- Blue-bracket "Game" style vs red "Legacy" style.
- Custom **eye** sprite as the connection indicator.
- RTLC / Russian language support.

## 1.0.0
- First in-game overlay: panel at the right edge, reads LCBridge WebSocket, widgets with per-widget toggles, timer, location/quota/day/deaths, BCME event plate.
