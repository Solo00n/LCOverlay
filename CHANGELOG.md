# Changelog — LCBridgeOverlay

## 1.15.5
- **A lamp is recognised by its colour, not by the name of its file.** A whole schematic can now be brought as one image, and the light still pools under every yellow mark in it. Only a layer of its own still breathes with the breaker — blinking the entire picture on account of three lamps would not do.
- The bundled sample layers no longer ride inside the DLL; they belong in the player's config folder.

## 1.15.4
- **A layer is only painted where it isn't painted already.** Every layer was multiplied by the theme colour, so a lift car, a lamp or a cable drawn in its own colour was painted a second time. The theme now reaches only the white and grey linework, brightness preserved as a multiplier; anything the artist has already coloured passes through as drawn. A whole schematic can therefore be brought as a single image with its coloured parts baked in.
- **Lamps drawn in colour breathe by brightness alone** rather than being tinted yellow over yellow.
- **The dropship's flame is no longer turned over.** It was drawn with its tongues upward, as fire is, and there was nothing to correct.

## 1.15.3
- **The dark is cut to the shape of the drawing.** A sheet across the lower schematic still lay over it as a slab — the empty rock around the caves, the sky at either side, and its edges read as lines. Density is now taken from the layers themselves, so only the complex and the caves go dark, and it is half as heavy as it was.

## 1.15.2
- **The version the plugin declares had been stuck at 1.13.2 since that release.** BepInEx chooses between copies of a plugin by that number, not by the date on the file, so with several copies in the plugins folder all claiming the same version it loaded whichever it happened to reach first — and a fresh build could simply never arrive in the game. The build now fails if the declared version and the project's disagree.

## 1.15.1
- **The weather stopped throwing an error every frame.** Rebuilding the schematic destroys the drops and flashes along with everything else, but the weather kept its own list of them and reached into the wreckage on the next tick. The exception aborted the rest of the refresh — the creature marks among it — and filled the log with tens of megabytes. The effects are now dropped with the schematic they belong to, and a stale one is skipped rather than followed.
- **The dark lies flat on the schematic again.** The tilt is fitted to each drawing as the schematic is built, and the vignette, the lamplight and the dropship are all made later — so they sat flat over a tilted picture. Anything made after the build now gets the tilt too.
- **The dropship burns the flame you drew** (`res/mobs/flame.png`), turned to point downward, back to a single tongue.
- **The light and the dark are pixels, not gradients** — a coarse grid, point sampling and a dozen steps of opacity, in keeping with the rest of the drawing. The light is also centred on its lamp rather than hanging below it.
- **Cables pay out as the car descends.** Drawn down the whole shaft, they are cut at the car's roof; drawn only as far as the car's resting place, the mod carries them the rest of the way itself.

## 1.15.0
- **The dropship comes in from the top right along a proper arc** and takes eight seconds to do it: six on the curve, a seventh hanging just above the pad on its jets, and the eighth setting down. It lands a little below the ground line, so its feet are under it rather than resting on top.
- **The exhaust is burning oil, not a turbine.** Five thick tongues, each writhing to its own noise, sooty at the edges and hot at the root. Near the ground they shorten, thicken and splay outward, spreading across the surface.
- **The dark is one sheet across the whole lower schematic** — solid at the bottom, fading away around the lift passage — instead of a black rectangle drawn to the walls of the complex, which read as exactly that. Its fading edge can be slanted to follow the terrain: `gloom <height> [slant]` in `map.txt`.
- **Every lamp on the drawing gets its own pool of light.** The layer's lamps are found automatically, and the light wraps around each one and pools beneath it. The falloff is far softer and carries a trace of dither, so the old visible rim of the half-circle is gone.
- **The lift copies the real one.** Its position is read from the car itself as it travels between the shaft's top and bottom points, rather than from a mere at-the-bottom flag, so the overlay moves with it. It starts the day at the top, as the game's does.
- **Cables are a layer of their own** (`*cable*` in the filename) and are drawn only above the car's roof — descending pays out more of them. The lift layer is measured from the drawing, so no coordinates need typing; `elev <travel>` in `map.txt` sets how far it runs.

## 1.14.2
- **The dropship's exhaust burns outside the hull.** The ship hung from its top edge while the flame was drawn from the point it was anchored by, some forty pixels above its feet, so the fire burned inside the craft. It now stands on its base — the point it lands on — and the flame falls away beneath it. The flame itself was also upside down, its nozzle at the far end and its point against the hull.
- **It comes in from space.** The descent is an arc from up and to the right rather than a drop down a plumb line, the craft leans into its own travel, sways as it comes, and grows as it nears the ground instead of arriving at full size.
- **The flame feels the ground.** As the ship settles the exhaust shortens and spreads wide, flattening out the way any jet does against a surface.

## 1.14.1
- **Notification mode has stopped fussing over loot.** Ship loot and scrap on the planet woke the panel on every item picked up, which meant it was awake most of the day and the news worth reading was lost in it. Neither wakes it now. The crew count is likewise quiet — a death already wakes the panel, and one event needs one signal.

## 1.14.0
- **Marks travel both ways.** They slid in from the rails when you stepped outside, but on the way back they simply vanished; now the journey runs in reverse as well. Running in and out repeatedly could also leave the rails and the schematic on screen at once — the two are reconciled every frame rather than once per packet.
- **The timer hides on the schematic and the eye moves to the centre**, sliding across as you enter and leave the ship.
- **Traps sit along the floor of the complex**, scattered across its width rather than stacked in a corner where the top storey left no room, half again as large, and filling in as you approach — as creatures do.
- **The dropship is filled in and twice the size**, and its exhaust is one steady candle of flame rather than a handful of stripes that never read as fire.
- **Cut the power and the complex goes dark**: noise settles over the rooms and caves, and what is inside becomes harder to make out. Restore it and each lamp casts a soft half-circle of light beneath it.
- **Fog dims the marks** as it dims the world outside, instead of only jittering them.
- **A phase is not a species.** A grown maneater, a roused nutcracker, a larva on the ceiling — these are states of one creature and share its mark. Deviants and the turret variant are genuinely other creatures and keep their own.
- **Notification mode dims the overlay rather than removing it.** Vanishing entirely made it unclear whether the mod was running at all; it now falls back to `IdleMinOpacity` and returns to full on news.
- **The mineshaft lift actually travels** the height of the room, so its movement can be seen.

## 1.13.2
- **One creature, one mark.** The schematic was grouping by picture rather than by creature, so a species with some of its number in another phase — a maneater grown, a hoarding bug roused — split into two marks that often looked the same. It groups by creature now, and the mark shows the phase of the nearest one. Deviants still stand apart, being a genuinely different version.
- **Floodwater stops at the building** instead of running across it, and it climbs twice as high over the course of the day.
- **A scan is far less likely to be missed.** The scanner was read once a second alongside the data packet, and a scan node that came and went between those reads was simply never seen. It is checked ten times a second now.

## 1.13.1
- **Outline and fill can no longer come apart.** They were two images kept in step by copying one transform onto the other, and however carefully that was done something always drifted. They are one sprite now, the interior baked in at sixteen densities and chosen by distance, so there is nothing left to misalign.
- **Creatures stopped twitching as they walk.** Two of them near the spacing threshold shoved each other back and forth every frame; the push is now eased in rather than applied whole. Facing also flickered at each turn, where the direction is genuinely ambiguous, so it is held through the turn instead.
- **Rain stays its own colour during a storm** — the storm colour belongs to the lightning, and yellow rain looked absurd.
- **`EventPlateSeconds` sets how long the event plate stays** after landing (ten by default, half that on departure).

## 1.13.0
- **Traps appear on the schematic**, gathered in the top-right corner inside the building. They keep the panel's rules — hidden until scanned where that is required, faded by distance, growing with numbers — but they do not wander: they are fixtures, and in the corner they stay clear of the creatures.

## 1.12.3
- **The body fill trailed behind its outline.** It copied the icon's position before that position had been set for the frame, so it was always one step late — obvious outside, where creatures cover the most ground, and enough to read as a second creature standing beside the first. That is also where the pair of gunkfish came from. It is now moved after the icon, and hidden along with it.
- **Deviants show as their own version again**, drawn upside down as the panel draws them. They were being folded in with the ordinary creature of the same species and vanishing.
- **The dropship is the overlay's colour and a sensible size** rather than red and outsized — it is part of the scenery, not something hunting you.

## 1.12.2
- **The dropship has its proper artwork** now, cut from its white background and carried in the mod like the creature icons, so the Pixel, Vector and Symbol styles apply to it as well. Dropping a `dropship.png` into `lcbridgeoverlay-map/` still overrides it.

## 1.12.1
- **The dropship comes down like a rocket.** It descends onto the open grass at the left of the drawing with its engines burning, cuts them while it sits unloading, and lifts off on them again when it is done — or the moment the crew's own ship leaves the moon.
- **You can give it your own picture.** Drop a `dropship.png` into `lcbridgeoverlay-map/` and it is used in place of the drawn one, with a white background removed automatically and the same Pixel, Vector or Symbol treatment the creature icons get.

## 1.12.0
- **Creature phase changes show on the schematic.** A grown maneater, an angry jester, a roused hoarding bug — none of them changed, because the schematic stripped the state off the name before choosing a picture. It picks the same variants the panel does now.
- **The fill sits square on its icon.** It was a child of the icon with its own anchoring, and any mismatch read as a sideways drift. Both are now made by the same code as neighbours in one slot, and the fill simply copies the icon's transform.
- **A resting creature carries a translucent dark red**, deepening into full colour as it approaches.
- **A creature that comes close is filled even when others of its kind are far.** Entries of one species are collapsed to a single mark, and the distance came from whichever happened to be first — so a dog at your heels showed its distant cousin's range.
- **The delivery dropship flies in.** It comes from the right, hangs over the ship while unloading, and lifts away when it is done.
- **Creatures slide across from the ship's side rails onto the schematic** when you step outside, instead of blinking into place.

## 1.11.2
- **Rain lands on the building** rather than falling through it: drops break on whatever surface is beneath them, the roof included.
- **Lightning is actually visible** — thicker, in its own colour, held twice as long and blinking half as fast. It also writes a line to the log, so if it still goes unseen we will know whether it fired.
- **Flood water sits at the surface and rises above it**, and nothing is painted inside the caves or rooms any more.
- **Each weather has its own colour** (`ColorRain`, `ColorStorm`, `ColorFlood`, `ColorEclipse`, `ColorFog`, `ColorDust`, `ColorMeteor`) — a name, a hex value, or `Theme`.
- **The eclipse is one disc inside the other.** The moon used to drift away sideways and upwards, leaving two circles apart; it now slides in horizontally and settles dead centre. Passing behind the building it is hidden by it instead of drawn over it.
- **The vector fill stays put.** Its rectangle was set once when created and never again, so any later change of icon size left it behind — the drift up and to the left. It is re-fitted to the icon every frame.
- **Icons that need to swap places arc around each other**, one each way, instead of shoving apart in a straight line.
- Outside creatures are 1.7 times the original size.

## 1.11.1
- **Icons stopped reloading themselves.** Aboard the ship they kept cycling through the packet and back into the icon every few seconds. The rail rebuilds whenever the roster changes, and a creature merely freezing or unfreezing counted as a change; on top of that every rebuild replayed the packet for creatures that were already there. Freezing no longer counts, and only creatures new to the rail get the packet.
- **Creatures walking left to right are mirrored**, since the artwork faces left; those walking the other way are left alone.
- **The animated weather is on by default.** Anyone who had set the old `Schematic` value was still getting corner symbols; only an explicit `Icons` asks for those now.
- **The first creatures found inside walk nearest the floor** — the routes are ordered bottom upwards rather than in the order they happen to be written.
- **The countdown's colour is configurable** (`CountdownColor`), red by default.

## 1.11.0
- **Weather happens on the schematic instead of being labelled.** Rain falls and breaks on the ground, storms add lightning, the flood line rises and falls with the real water, an eclipsed sun crosses the sky as the day runs, fog drifts in bands and puts interference through the creature marks, and a meteor shower drops meteors that shatter on the surface. Fog raised as an event inside the facility does the same in there. The old corner symbols remain as `MapWeatherMode = Icons`.
- **The fill on outline icons was misplaced and stepped.** It was being drawn over solid artwork as well, where it read as a shifted duplicate, and it followed the once-a-second distance directly, so it moved in visible steps. It applies only to the outline style now, is smoothed over time, and rises from fully transparent to full colour across the whole approach rather than switching at a threshold.
- **Dark patches in the caves take the facility's own background** rather than vanishing into black.

## 1.10.5
- **Combined weather shows every part of it.** Weather mods hand over strings like "Eclipsed + Stormy", and only the first match was drawn; all of them are now, stacked down the left edge, up to four at once.
- Creatures outside are back to twice the original size, with the facility ones at one and a half.

## 1.10.4
- **The event plate can close on its own without notification mode.** The log settled it: the plate was staying up because notification mode was switched off, and the two showing windows were tied to it. `EventPlateAutoHide` now gives the same behaviour on its own — ten seconds after landing, five as the ship leaves.
- **Creatures in the facility are half again the original size**, and the ones outside are back to it; they had ended up at double before the multiplier was even applied.
- **The fill comes in gradually with distance** rather than switching over in one frame at a threshold: a solid silhouette rises over the outline as something closes in.
- **The shaft is drawn in the overlay's colour** instead of red.
- **A new save forgets which creatures live where.** The moon memory outlived deleting a save and restarting the world, so scan-to-reveal handed you everything the moment you landed and the mode lost its point.

## 1.10.3
- **Creatures no longer flip as they walk.** The artwork does not face a consistent direction to begin with, so mirroring it turned as many of them backwards as it turned the right way round.
- **They keep out of each other's way.** Marks that end up closer than an icon's width push apart, so two creatures in the same part of the facility no longer stack into one. Outside they also have far more room to pace.
- **Facility creatures are drawn half again as large** as the ones outside.
- **Vector icons: a heavier outline**, a colour of your choosing (`VectorIconColor` — red by default, or blue, or the overlay's own), and a solid fill once something is within eight metres, since a thin outline is exactly what you cannot read when it matters.
- **Unrecognised weather now shows a plain cloud** instead of nothing, and the raw weather name is written to the log, so a modded one can be given its own symbol.
- **The event plate logs why it is on screen** — every term that decides it — rather than leaving us to guess.

## 1.10.2
- **Two of the same creature on the schematic.** The data groups creatures together with their state, so one species in two states arrived as two entries — hence two dogs where there was one. The schematic now collapses entries that share an icon.
- **Creatures outside walk too**, and every creature faces the way it is going: the icon mirrors on the turn. `slotout` accepts a full segment now, and falls back to pacing around its point.
- **Icons are twice the size** and sit above their line rather than sinking into the floor.
- **The stray line under the schematic is gone** — the divider was being switched back on with every packet, the same way the other rows were.
- **The event plate closes on time.** Its ten-second window is measured from the ship landing, which is when the day begins for the player, and it is now checked every frame; before, the check sat inside the once-a-second refresh and could leave the plate up.

## 1.10.1
- **The schematic can be a drawing you made.** Put PNG layers in `BepInEx/config/lcbridgeoverlay-map/` and the mod renders those instead of its own lines, stacked in filename order with point filtering, so pixel art stays pixel art. Draw in white: the image is used as a mask and the theme colours it. Keywords in the filename give a layer its job — `lamp` follows the facility lights, `cave` takes the danger colour, `elev` rides with the lift, `guide` is ignored. Re-entering a save picks up edited files, so there is no rebuild and no restart.
- Creature positions stay in the text file, since they move: `slotout` for a spot outside, `slotin` for the stretch one paces inside.

## 1.10.0
- **The schematic is now yours to draw.** It reads `gdlp.lcbridgeoverlay.map.txt` next to the config, and the mod writes the current drawing there on first run so there is something to start from. Move a line, resize a room, put the creatures somewhere else — reload the save and it is there, with no rebuild. If the file is missing or unreadable the built-in drawing is used, so nothing can break by editing it.
- Every piece of the drawing is also published as a separate SVG, so it can be arranged in a vector editor instead of by numbers.
- **Creature marks sit on the ground** rather than hovering above it, both outside and along the room floor.

## 1.9.6
- **Duplicate creatures on the schematic.** It was drawing the small background wildlife the panel deliberately hides, so a second, near-identical bee sat beside the real one. The schematic now uses the same hide list as the panel.
- **Creatures walk about inside the facility** instead of sitting in a row, each pacing its own stretch at its own speed, and their icons grow with numbers when the panel is set to do that.
- **The cave walls only bulge outwards now.** The noise pushed them both ways, so lines wandered into the passage itself; the multiplier can no longer fall below the base width. The far end is closed with a rounded cap rather than a flat cut.
- **The drawing follows the sketch**: a door on the left of the building, the cart beside it, a wide shaft on the right, a wider lift on visible cables, a stop block at the end of the rails, and the main room shortened from below so the entrance to the caves has room.
- **In notification mode the event plate appears twice a day** — ten seconds as the day starts, five as the ship leaves — rather than sitting on screen throughout.

## 1.9.5
- **The caves look hand-drawn now.** The passage is cut into many short links and each one is nudged aside by a fixed noise, with the two walls given different noise so they stop mirroring each other. They came out ruler-straight before because the outline followed the control points exactly. The water is gone.
- **The corridor is open again.** The room's ceiling was drawn as one unbroken line straight across the passage, sealing it; it now has a gap where the corridor comes down.
- **The lift's cables are visible.** They hung from the ceiling to a car parked just beneath it — about ten pixels of rope. They now run from the building's floor, down the corridor, to the car, and pay out as it descends.
- **The rails stop short of the lift** instead of running underneath it, and the strip lights hang from two stems each rather than balancing on one.

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
