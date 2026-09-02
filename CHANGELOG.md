# Changelog — LCBridgeOverlay

## 1.9.4
- **The frame is back aboard the ship** and gone outside. Last version removed the corners everywhere; now the whole set — corners and the blinking pixels alike — is present on the ship and hidden while the schematic has the panel.
- **The side rails of creature icons are hidden with the schematic.** They were repeating what the drawing already showed, on both edges of the screen.
- **The caves are drawn properly.** Each stretch used to be outlined by its own pair of walls, and at every bend those walls missed each other, which is why it came out as a scatter of sticks rather than a tunnel. It is one closed outline now, with the wall direction at each bend averaged from the two stretches meeting there, so the passage runs continuously from the room's left wall down to the right one, widening through the flooded chamber on the way.

## 1.9.3
- **The schematic was redrawn to read as a place.** The ground is a crooked line with hard angles; a large building stands on it to the right holding a mine cart and a hole in its floor; a vertical corridor drops from that hole into the main room, where the lift hangs on its cables. Rails run along the room's floor and strip lights hang from the ceiling on short stems, touching nothing else. The shaft leaves the room's left wall and winds down and across, opening into one large flooded chamber before it ends against the right wall.
- **The lift rides with the real one**, following the mineshaft elevator up and down, its cables paying out behind it.
- **Creature marks now obey the same rules as the panel's icons.** They were plain dots that ignored everything: no scan requirement, no fading with distance, no movement, and far too small. They are now the size of real icons, hidden until scanned when the mode calls for it, faded by distance, and they sway and grow jittery as something closes in.
- **The main block lost its corner brackets**; they remain on the event plate, where they still frame something.

## 1.9.2
- **The old rows no longer show through the schematic.** They were being switched back on with every packet, so outside you saw the drawing and the whole panel underneath it. Alongside the schematic only the frame remains: corner brackets, the eye, the run timer and the event plate.
- **The caves were redrawn.** They now leave the bottom of the facility and wind downwards like a gut, narrowing and widening as they go, with two low pockets standing in water.
- **Weather is drawn as a symbol by default** in the corner of the schematic — a ringed corona for an eclipse, a cloud with strokes for rain, a bolt for a storm, waves for flooding, bands for fog, slants for dust. The old animated treatment had nothing to show for an eclipse at all, which is why it looked like nothing was happening; it is still available as `MapWeatherMode = Effects`.
- **The lamps follow the breaker box**, since that is the switch players actually throw. Without one on the map they fall back to the lights themselves.

## 1.9.1
- **The schematic now takes the panel's place instead of sitting beside it.** Step off the ship and the location, quota, day and ticker rows give way to the drawing; step back aboard and they return. It is built as a block of the panel itself rather than a separate layer, so it inherits the width, the tilt, the scale, the camera sway, the fade and the keystone perspective rather than reimplementing any of them, and it fills the panel edge to edge. Everything in it is drawn larger to match, and the moon name and loot line use the panel's own fonts.

## 1.9.0
- **A schematic of the location, for when you are outside** (`ShowFacilityMap`). Rather than the full panel getting in the way while you move, a small drawing shows the surface, the entrance, the shaft down, the rooms and the caves. Creatures sit as marks in their own zones — above the ground line outside, below it inside — the moon's name stands over it and the loot inside, outside and in hives reads underneath. It is drawn from lines rather than an image, so it stays crisp at any size and keeps a transparent background; room interiors are filled with the overlay's own scanline blocks.
- **The lamps follow the real lights.** They are yellow because they are light, not decoration, and they go out when the facility does. `MapLightMode` chooses whether that reads as a soft pulse or a plain on/off.
- **Weather is drawn in the same language as the map** — slanted strokes for rain and flooding, drifting bands for fog, a dimming for an eclipse, an occasional flash in a storm.
- **Three more looks for creature icons** (`MonsterIconStyle`): `Pixel` for an eight-bit reading, `Vector` for a clean outline, `Symbol` for a solid silhouette in the theme's colour. All three are derived from the existing artwork, so they cover every creature and cannot drift apart from each other.
- **The countdown reads properly now.** Digits arrive already large, hold for an instant, then drop away into the distance and vanish quickly, and they take the overlay's colour instead of white.
- **The overlay can remember which creatures live on which moon** (`RememberSeenMonsters`, off by default). Scan something on a moon and it will be there waiting the next time you land. Note this is the opposite of `ResetScansEachDay`, which exists to stop you knowing in advance.
- **The signal translator can carry the overlay's data** (`RequireSignalTranslator`). Turn it on and sharing between players only works while one is aboard, which finally gives the item a reason to be bought.

## 1.8.4
- **Scanning works alongside Good Item Scan.** That mod replaces the game's scanner outright — it empties the vanilla node list and keeps its own — so watching the vanilla one could never have seen anything, which is why the previous two fixes changed nothing. Both sources are read now, so scanning registers whether or not it is installed.
- **In scan mode the overlay reacts to finding a creature, not to it spawning.** The data packet lists every creature on the level, so the panel was waking for things the player had not yet seen. Only scanned ones count now. Traps are unaffected, since those are already filtered before they reach the packet.

## 1.8.3
- **Scanning finally registers.** The scanner's node list is a private field on the game's HUD. The build compiles against a publicised copy of the game, so reading it directly looked fine, but at runtime that throws — and the exception was being swallowed, which is why two attempts in a row produced no effect and left no trace in the log. It is read through reflection now, the log says whether the field was found, and a failure is reported instead of hidden.
- **The daily scan reset no longer fires twice.** The game raises its start-of-round event twice per landing, so the second one could wipe scans made moments earlier. It now clears once per day number, and re-entering a save clears properly again.

## 1.8.2
- **New `ShareScans`.** Scans reach the whole crew by default; turn it off and each player sees only what they scanned themselves. Nothing is sent and nothing is accepted while it is off, so it holds even if someone else leaves sharing on. The host decides it for the lobby, like the other reveal settings.
- **The notification sound no longer sounds like a radar booster.** It is still built from the booster's own clips, but plays through the mod's own 2D source with the pitch shifted, the low end cut and a short echo, so it reads as an interface alert and cannot be mistaken for the item in play. It follows the game's volume.

## 1.8.1
- **Scanning a creature reveals it again.** The scanner hook never fired in a loaded modpack — another mod intercepts the path to it, and not a single scan was recorded. The scanner's result is now read directly each tick instead of intercepting the route to it, so other mods on that path no longer matter. Traps are picked up the same way.
- **Moon names went orange and stayed there.** The new number blinking took the current colour as its base every frame, but the panel only repaints about once a second, so the tint compounded until the text was permanently accented. The original colour is remembered once and restored when the blink ends.
- **Packets only announce creatures now**, and they appear where the creature's icon will be, in the overlay's own colour, flickering there until the real icon replaces them. They no longer float above the panel for every number change.
- **Changed numbers blink** on their own, without a packet — quota, loot, deaths, day, level scrap, crew, events and moon.
- **On the ship the panel behaves normally** and settles at the opacity from the config rather than vanishing; notification mode only applies on a moon.

## 1.8.0
- **New notification mode (`NotifyMode`).** Instead of sitting on screen all run, the panel sleeps invisible and wakes only when something actually changes. A packet in the overlay's own colour appears above it, flickering like an eight-bit image on a bad signal, then slides down into the top of the panel and dissolves as the new information arrives; whichever number changed blinks. The panel fades back out after `NotifyHoldSeconds` of quiet. Waking and sleeping are announced with the game's own radar booster power-up and power-down sounds.
- **The eye follows the overlay's colour.** It was drawn in red and left untinted, which looked wrong against the blue-bracketed style. The artwork is converted to a white mask on load, so it now takes the current style's colour cleanly instead of muddying it — tinting the red original would have multiplied red by blue.

## 1.7.0
Scan-to-reveal mode was leaking information it was supposed to withhold, and it did not carry between players. Both are fixed.

- **A scan by one player now counts for everybody.** Scans used to be remembered by an instance number, which is private to each machine, so sharing them was impossible in principle. They are keyed on the network id instead, which is the same for everyone in the lobby, and passed around: a player's scans reach the host, the host hands the combined set to everyone, and someone who joins late receives it immediately.
- **Traps have to be scanned too.** Turrets, mines and spike traps appeared for free while monsters stayed hidden. If a trap has no scan node at all it is still shown, since otherwise it could never appear.
- **The "OLD BIRD" line no longer gives the bird away** while scanning is required — it announced one before anybody had seen it. Its icon still appears once scanned, as normal.
- **New `ResetScansEachDay`.** Every landing starts blank: the same creatures and traps have to be scanned again, and the bestiary's earlier unlocks stop counting, so a day cannot be read in advance. The host decides this for the lobby, like the other reveal settings.

## 1.6.3
- **The death counter is the host's, and it resets.** It was reading the save file's lifetime death total, which is loaded from disk and never cleared, so it survived restarts and counted deaths from previous runs. The count is now measured from the start of the current run and broadcast by the host, so everyone sees the same number and a new run starts at zero.
- **Turrets on the ship are no longer counted as hazards.** Mini turrets bought from Defend Facility and carried aboard were showing up in the trap panel alongside the ones trying to kill you. Anything standing inside the ship is treated as yours and left out.
- **Monster icons shake harder the closer the creature is.** Far away it is barely a drift; up close it turns into a nervous jitter. The curve is cubic, so the effect stays out of the way until something is genuinely near. Switch it off with `ProximityShake`.

## 1.6.2
- **None of the mod's patches were being applied.** One patch named a parameter that does not exist in the game's method, which made Harmony abort the whole `PatchAll` — so every other patch in the mod, including all of the networking added in 1.6.0, silently never ran. Both players' logs showed it. Patches are now applied one class at a time, so a single bad signature can no longer take the rest down with it, and each failure is named in the log.
- **The run timer is the same for everyone.** It used to count locally from each player's own landing, so it drifted between players. The host now broadcasts it, along with the reset signal, so it starts, runs and resets identically for the whole lobby.
- **The timer resets when a run ends.** Ejection or bankruptcy zeroes it immediately instead of waiting for the next lever pull; a fresh save still clears everything.
- **Event names now reach other players.** They are sent by the host directly. The previous approach read the event mod's on-screen panel, which turns out to hold a table of chances rather than the active events, so there was never anything there to find.
- **Rejoining no longer leaves other players with a dead overlay.** The message handlers were bound to the network manager from the previous session and were never rebound, so a client that reconnected heard nothing. They are now rebound when the session changes, and a client that hears nothing from the host retries instead of giving up permanently.

## 1.6.1
- **The Giant Sapsucker was drawn as a Forest Keeper.** The game does not store the pretty bestiary names: the sapsucker's internal name is `GiantKiwi` and the Forest Keeper's is `ForestGiant`, so the rule matching `giant` swallowed the bird before anything could recognise it. It now matches `kiwi` first and gets its own icon.

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
