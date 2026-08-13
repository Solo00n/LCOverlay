# Changelog — LCBridgeOverlay

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
