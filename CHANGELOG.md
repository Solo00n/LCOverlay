# Changelog — LCBridgeOverlay

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
- **Proximity fade** (`ProximityFade`, default on) — the closer a monster **or trap** is to you, the more opaque its icon; distant ones fade toward transparent.
- **Grabbable traps counted** — BCME's `GrabbableTurret`/`GrabbableLandmine` (which destroy & replace the normal ones) now show up as turrets/mines instead of vanishing.
- Trap rail now behaves like the monster rails — icons centered on the bottom border line (following the event plate) and faded by proximity.
- Scan gating now uses the **in-game bestiary** (`Terminal.scannedEnemyIDs`) — once you've scanned a creature it shows, reliably.
- Overlay **hides during takeoff / flight** (while the game shows the center-screen info and the leave-lever is disabled), then returns.
- **Fixes:**
  - Overlay now fills in **instantly on landing** (event + interior + monsters in one packet) — forced refresh on ship-landed and on level-generated, instead of trailing in over a second.
  - Auto-timer no longer keeps ticking **on orbit / during loads** — it runs only while actually landed on a moon.
  - **Toggle-key rebinding works** — symbol keys like `\` are accepted (were silently falling back to `I`), and the key applies live from config without a restart.
  - **Green halo** around icons removed — the keyed-out border is now colour-bled so bilinear downscaling can't smear green; the **Gunkfish/Stingray** icon maps correctly (any still-unmapped monster is logged).

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
