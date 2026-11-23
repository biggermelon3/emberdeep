# Emberdeep loop spec v1 (2026-11-21)

Decided with the owner on 2026-11-21, after a 3-tester playtest. Numbers marked *tune* are starting values for
device tests on Quest 2 and PICO 4 Ultra. Where this spec conflicts with the older `Docs/Emberdeep/*.json`, this
spec wins.

## 1. Pillars

- **One hand action, done well.** The water hand's open palm aims, and touching its gem with the other hand sprays.
- **Strategy lives on the board:** read the fire (wind, forecast), shape the board (pieces), then put it out.
- **The board is a chessboard.** It lies flat to the real world (a Tilted option exists for heavy headsets, §10), sits
  within reach, and pieces are placed by hand.
- **Simple progression.** No roguelite build system.

## 2. A level, start to end

1. **Plan (time stopped).** The board shows the starting fires, the wind and the forecast (§4.4). Place pieces from
   the tray. The gem is already full. Press Start.
2. **Fight.** Spray, refill, rescue sheep. Pieces you haven't placed can still be placed. You earn the plane here
   and call it in.
3. **End.**
   - Cleared: no burning trees and no sheep on fire.
   - Lost: the charred trees reach the limit.
   - Both get an in-world moment: a banner over the board, a sound sting, the board flaring or darkening. The HUD is
     only secondary.
4. **Between levels.** Coins, then slim upgrades (§7), then the next level.

## 3. Water

### 3.1 The gem is the pool

- One gem on the back of the water hand. It is the only water store and the only water meter: no HUD water bar.
- It is glass with a visible liquid level, and it drains visibly while you spray.
- Empty: it goes dull, a sputter sound plays, and the pump starts to glow.
- All water comes from it. **Removed:** per-spell crystals, the altar basin, and dipping a hand to refill.

### 3.2 Spraying (same action as today)

- The water hand's open palm aims. Any fingertip of the other hand on the gem sprays; lift it to stop.
- Flow is 14/s (today's `MagicFlow`).
- Headset test 3 (2026-11-22), `EmberdeepPalmGesture` (fireground and camp alike):
  - "Open" = openness (1 - four-finger closure) at least .70 to start aiming, and it holds down to .62. That is a relaxed
    hand: it was every finger under 40 deg of bend. A closing hand drops the aim before the grabs (closure .42).
  - The press counts on the first touching frame, with no dwell. The touch area covers the whole back of the hand,
    and the press survives short tracking blinks (.08 s). A tap sprays at least .15 s. The water hand's own fingers
    never press.

### 3.3 Passive refill

- Refills only while you are not spraying, starting 1.5 s after you stop. The rate is about 1/3 of normal pumping
  (*tune*).
- What to watch in tests:
  - Testers never pump: the passive rate is too high.
  - Testers always pump the moment the gem is empty: the passive refill doesn't matter, so raise it.

### 3.4 Pump (active reload)

- One pump beside the player, on the free-hand side, so the natural strike is the free fist while the gem stays in
  view (headset test 2026-11-22: hammering with the water hand turned the gem away). It mirrors with the hand swap.
  The piece tray is always on the board's right (owner's call 2026-11-22). With the default left water hand it shares the
  pump's side and starts behind it; with a right water hand the pump goes left and the tray's front lines up with the board's.
  Headset test 3 (2026-11-22): at the default seat its cap is ~30 cm under the eye and ~27.5 cm ahead of it (was
  ~42 / ~32 cm). It follows the distance option 1:1 and half of the height option, so the cap stays 16-41 cm under
  the eye (`EmberdeepBoardLayout.PumpHeightFor`).
- **Fist hammer.**
  - A closed fist moving down onto the top of the handle, above a speed threshold, counts as one strike.
  - Lift above a rearm height before the next strike.
  - No grabbing, no snapping, no attaching. Either hand can hammer.
  - Threshold and rearm height are *tune*.
- Each strike throws a visible splash from the spout into the gem: an arc, a gulp sound, and the level rising.
- **Rhythm bonus.**
  - While you pump, ONE cue: a ring shrinks onto a fixed target ring around the cap and meets it on the beat. Nothing
    else changes in the window (headset test 2026-11-22: three "hit now" signals confused).
  - A strike inside the window gives bonus water and a clean "good pump" sound; an off-beat strike creaks. After any
    strike the target ring flashes briefly as feedback only: gold on the beat, rust off it.
  - Sits behind a toggle so feel tests can compare with and without.
- Starting numbers (*tune*): gem 40, strike 4, on-beat strike 7, beat ~0.55 s, passive 2.5/s.

### 3.5 Before a level

- The gem fills automatically when the level loads. No pumping before the level.

### 3.6 Hand swap

- Option "Water hand: Left / Right", default Left.
- It mirrors the aim hand, the gem, the pump side and the hints. The tray stays on the right (2026-11-22).
- Today left is hard-coded: `FireRescueGame.Magic.cs:35`, plus the hint strings at `:86` and `:91`.

### 3.7 Aim indicator

- **Today:** `EmberdeepPalmAimVfx` draws faint motes and a ground ring 1.5 mm wide at alpha .34. The ring sits under
  the canopy of the tree you aim at, so that tree hides it.
- **New:**
  1. Highlight every tree inside the splash radius with a ring or glow around its crown. Draw it as its own mesh so
     it doesn't fight the tree heat tint.
  2. Put a marker on the first surface the water hits, canopy or ground.
  3. Make the arc thicker and brighter.
- Aim assist (`EmberdeepPalmAim`): the palm ray snaps to the tree it visibly points at, or to a burning sheep (headset
  test 3: a level clears only with no sheep on fire, and sheep behind the trees were hard to hit). The arc is chosen
  so it really lands there, clearing front trees. A burning sheep the splash will put out gets its own ring. When no
  arc reaches the target, the marker shows where the water really lands.

## 4. The board

### 4.1 Fuels: tree and grass

- **Tree:** as today.
- **Grass** (*tune*):
  - short, so it never hides anything;
  - catches faster;
  - burns briefly at low heat;
  - quick to put out.
- Placing grass in the back rows also helps back-row visibility.

### 4.2 Wind

- Each level has a direction and strength. Wind can shift mid-level, with about 3 s of warning: the weathervane
  swings and gust VFX play.
- Fire spreads faster downwind.
  - Downwind neighbours catch first.
  - Reach is longer downwind (for example 2.2 downwind and 1.2 upwind; today it's 1.7 in every direction) (*tune*).
- It must read at a glance:
  - a rooster weathervane piece on the board;
  - wind streak VFX;
  - smoke leaning with the wind.

### 4.3 Wetness lasts

- Today a soaked tree resists spread for only about 1.1 s. Wetness decays at .7/s and `Spread()` skips a tree only
  while its wetness is ≥ .2.
- New: about 8 s for a full soak (*tune*), and wet trees look wet (darker, glossy). Soaking trees ahead of the fire
  becomes a plan.

### 4.4 Forecast

- Fire spread is deterministic, so the forecast can be honest: the next 1-2 trees to catch get an ember glow, in
  both Plan and Fight.

### 4.5 Fail rule

- Unchanged: Lost when charred trees ≥ the limit. It shows in-world as a meter next to the board.

### 4.6 Readability

- The heat bar sits clear of the canopy.
- A burning tree keeps a readable minimum flame until its heat reaches 0, then gives a clear "out" puff.
- Burning trees show a marker above the canopy that can be seen past the front rows.
- Front trees between the eye and the aim point fade out. Use a dither fade, not a transparency pass.

## 5. Pieces

- **Placing.**
  - Take a piece by hand from a tray beside the board.
    - Headset test 3 (2026-11-22): pinch it (thumb and index meeting within ~3 cm of the piece) or grab it (a hand that
      opened near it closes with the palm or a fingertip within ~5 cm). Distances are to the piece's visible box. The
      piece a hand would take glows first. Opening the pinch or the hand lets it go. Placed pieces in Plan follow the same
      rules. Details: `EmberdeepPieceGrasp`.
    - The first time towers appear, a short tip over the tray's tower says what it does (until a tower is set down; SHOW
      HINTS brings it back).
  - Set it down on a grid cell, with a placement ghost.
  - Not on burning cells, and never with the ray.
- **Supply.** Each level hands out a fixed set of 2-3 pieces. Upgrades add slots.

| Piece | When you place it | What it does | Notes |
|---|---|---|---|
| Water tower | Plan or Fight | Arrives full. Rains once, on its own, when fire comes within its radius (2.4 for 4 s, today's values). | Replaces "spray the tower to fill it". |
| Excavator | Plan or Fight | Draw a line with your index finger, up to N cells. It drives the line at about 1 cell/s. Dug cells become dirt that can't burn. Trees and grass on them are cleared and don't count as charred. | Slow on purpose, so start it early. |
| Plane drop | Fight only, once earned | Place a marker and set the line with your wrist yaw. About 3 s later a plane flies the line and drops water on a strip. | Earned by play (a combo, a sheep saved), like a killstreak. Big SFX and VFX. |

- **Art:** placeholder geometry first (plane, excavator, weathervane, tray, grass), then an art pass. Who does the art
  is still open.
- **Reach: shrink the board** (owner's decision, 2026-11-21; turning or sliding the board was considered and dropped).
  - Today the trees sit 0.56-1.02 m in front of the eye and about 0.65 m below it, so seated hands can't reach the
    middle and back rows (`FireMiniature.Place`; `FireRun.cs` rows at `z = 3 + row*1.15`; scale .08).
  - New field: at most **4 rows deep** (about 28 cm) and wider instead (up to 8 columns, about 70 cm), keeping the .08
    scale so trees and sheep stay readable. Go to .07 only if device tests show reach is still short.
  - The island and plinth shrink to fit.
  - Default placement: front row about 30 cm from the eye, back row about 58 cm, board top about 45 cm below the eye
    (desk height). Everything is within seated reach.
    - Headset tests 2026-11-22: test 1 moved it to front row 40 cm / top 36 cm (`EmberdeepBoardLayout`, still the base
      of the options); test 2 settled on options -14 cm height / +6 cm distance as the default (front row 46 cm, back
      row ~74 cm, top 50 cm under the eye, ~40° down to the middle of a 4-row field).
  - Watch on device: the view is steeper than today (about 45° down to the middle of the board, against 39° today),
    and the rim sits low in your view. That's why the rim HUD tilts up toward the eye (§11b), and why §10 has height and
    distance sliders.

## 6. Plan phase look: time stopped

- **Entering Plan:**
  - The board desaturates to a whitish grade.
  - Fire and smoke freeze mid-motion.
  - The ambience drops to a low hum.
- **Pressing Start:** colour floods back from the centre and the fire resumes with a whoosh.
- **Implementation:** keep it on the board itself (a material keyword or global float, plus pausing the particles).
  No full-screen post pass, because of the Quest 2 cost.

## 7. Progression (slim)

- Coins come from saved sheep, saved cabins and stars.
- Upgrades: bigger gem, stronger pump, +1 piece slot, larger tower radius. About 4-6 items in total, with no
  tuning tree.
- New piece types arrive with the levels, not from the shop.

## 8. Levels (draft)

- 6 stages after the camp tutorial. Today there are 8: 3 palm lessons and 5 incidents.
  1. Palm water and sheep.
  2. Pump (the first time the gem runs low).
  3. Towers and the Plan phase.
  4. Wind.
  5. Grass and the excavator.
  6. The plane, and everything together.
- Each stage introduces one thing and teaches it through the board, with no text.
- Layouts are hand-authored. The final layouts are the owner's call.
  - First pass (LV, 2026-11-22): `Runtime/EmberdeepLevels.cs`, one ASCII map per level (trees, fires, clearings,
    grass, cabins, sheep) plus its fire clock (seconds between catches, seconds to char), wind, piece set, burn limit,
    par and the Plan hint squares. The legacy tool flow keeps its generated field.
  - Gate: `EmberdeepLevelTests` (EditMode). With no player, each level is lost in 44-82 s, and that time grows level
    by level. A scripted player (`EmberdeepLevelBot`) clears each within par, the forecast names every catch, and
    without each level's new thing the same player does worse.
  - Seated Plan captures: `Logs/EmberdeepLevels/` (`EmberdeepLevelCapturePlayTests`).

## 9. Camp

- The camp tutorial never starts by itself (headset test 2026-11-22). The first NEW GAME / CONTINUE before it was
  ever done asks once: NEW HERE? HOW TO PLAY (runs it, then the game starts) or SKIP. The answer is remembered.
  "How to play" in the menu plays it any time.
- It teaches the hand actions at camp scale (.27, against the board's .08):
  - put out the campfire with palm water;
  - top the gem up at the pump: it glows for the whole step, and the step teaches on the water the campfire left (no
    emptying, no relight; owner 2026-11-22);
  - watch a scripted sheep catch fire, run, and light a nearby tree; put both out;
  - pet the sheep.
- **Voice-over** (owner, 2026-11-22). A sarcastic, cynical forest spirit talks the player through it:
  - who: a small blue glowing orb with a bright core, a soft halo, circling fairy motes and falling dust. It comes out
    of the tent, hovers bobbing over the island's left half, and goes back in when it is done or on SKIP
    (`EmberdeepForestSpirit`);
  - voice: English only, voiced by the owner, script v2 (`.agents/local/vo/script-v2.md`). 15 lines, processed with
    +3 semitones, smaller-creature formants and an open-air tail, at -18 LUFS
    (`Resources/EmberdeepVoice`, `EmberdeepSpiritLines`);
  - subtitles: under the orb, en + zh-Hans (a Translations.tsv row per card).
    - Legibility: on a near-opaque dark band, with outlined, drop-shadowed letters.
    - Timing: each card shows as its first word starts. Card times were measured against the audio by word alignment,
      and the clock follows the playing clip's position.
  - pauses: about 1.1 s of silence between chained lines (PauseAfter .8 s), and about 1.4 s after a joke or a
    question (1.0-1.15 s);
  - pacing: a step's done moment waits until the spirit stops talking. A queued line whose deed is already done is
    skipped (e.g. putting the campfire out during the hello skips both campfire lines), and a playing line is never
    cut. Reactions: the first spray ("Too fiddly?"), the gem topped up, the sheep catching fire. Reminders: after 8-10 s
    of silence with nothing done, at most two at the campfire and one at the pump and the sheep
    (`EmberdeepSpiritDirector`);
  - sound: the voice takes the SOUND volume and ducks every other sound to 60% while it speaks.
  - Levels: the first-time ghost hints stay as they are. Two Plan-phase intros by the same spirit (owner, 2026-11-22):
    - water towers on TOWER HILL, wind on WINDY RIDGE, the owner's own script;
    - each plays once per install when that level's Plan starts (time is frozen, so the line always finishes);
    - SHOW HINTS and How to play bring them back;
    - the orb hovers over the board's back corner on the water-hand side, clear of the PLAN banner over the tray
      (`FireRescueGame.Spirit.cs`, `EmberdeepPlanSpirit`).
  - Camp pump: in the tutorial it stays hidden until the spirit has finished the pump lines, then rises in. The gem
    refills by itself in the intro and campfire steps, so nobody gets stuck before the pump appears.
- While it runs, the sign shrinks to a small plank at the top of its posts: three step chips and SKIP in one row, far
  above the aiming line to the island (headset test 3, 2026-11-22: aiming palm water at the campfire raised the menu
  ray). Water comes first at camp: a water hand with its open palm aimed at the island gets a menu ray only on a direct
  button hit, and the near-sign ray reach shrinks from 8 cm to 3 cm during the tutorial.
- Camp sheep can be petted any time and do the boing hop that rescued sheep do. In a level, petting waits until every
  fire is out (not in Plan, not in the fight).

## 10. Options

- **Play area height** (slider) and **distance** (slider).
  - Every continuous option is a drag slider (headset test 3, 2026-11-22), on the camp settings page and the pause
    panel: pinch the knob or the track with the ray (or touch it with a fingertip, when in reach) and drag. The value
    follows live (the board re-places as you drag), snaps to whole cm (volume: 5%), and is saved on release. The -/+
    ends stay as nudges.
  - Paired choices (Flat / Tilted, Left / Right) show the current one as a lit gold tile and the other muted, not an
    underline.
  - These fine-tune the shrunk board's default placement (§5, Reach).
  - Default -14 cm / +6 cm (headset test 2). Range: height -35..+15 cm (board top 21-71 cm under the eye; the floor
    guard holds the top 45 cm over the floor), distance -15..+30 cm (front row 25-70 cm). The far end is for an
    overview: back-row pieces need a lean or more past about +5.
  - **Flat / Tilted** (default Flat; 2026-11-22, for players whose headset is too heavy to look down for long). Tilted
    pitches the whole board 30° (*tune*) toward the player about the front row: the near rows stay at hand height, the
    back rises (4th row +14 cm) and faces the eye. Things struck by a real hand (pump, start bell) stay upright in the
    world; everything else follows the board.
  - Back-row visibility comes from the shallower field plus §4.1 and §4.6.
- **Water hand:** Left or Right.
- **Volume:** music and sound effects, separately.
- **Recenter:** already exists.
- **Show hints** (camp settings, 2026-11-22): brings the first-time hand hints back; dimmed while none was used up.

## 11. Cut / out of scope

- The spells (Water Bolt, Rain Fan, Water Orb) leave the game flow. Their code stays behind
  `EmberdeepSession.PlaytestAllSpells`.
- Embers: unreadable at board scale.
- Tilting the board.
- Altar basin, hand dip, station tiers (lever, rotary, button), per-spell crystals, tool tuning upgrades.
- The legacy FireRescue tool flow (`goddessTutorial:false`) stays untouched.

## 11b. In-game UI: layout A, board rim (picked 2026-11-21)

Mockups: https://claude.ai/artifact/DE2u8hpffi4sZPBHpgPpH6 (owner-only until shared). All copy on them is
placeholder until the text pass.

- **Rules for every screen:**
  - No sentences during a fight.
  - Banners are 1-2 words.
  - Buttons are icons with targets of at least 3 cm.
  - **The ray exists only between levels.** In Plan and Fight, everything is done by hand on the board.
- **During a fight there is no panel at all.**
  - The board's front rim (the plinth face toward the player) carries three readouts:
    - sheep: one icon each, coloured saved / on fire / waiting;
    - the burn meter: charred trees against the limit;
    - the plane charge ring.
  - The gem is the water meter (§3.1).
  - Pause is a wooden token on the rim corner that you poke with a finger.
- **Plan:**
  - the time-stopped look (§6);
  - dashed ember rings on the next trees to catch;
  - a piece tray beside the board on its right (fixed, whichever hand holds the water);
  - a placement ghost showing the drop cell and the tower's rain radius;
  - a bell on the rim that you strike to start (the same fist move as the pump);
  - an hourglass on the rim.
- **First-time hints:** a ghost hand plays the move on the player's own hand or object, with pips that count down.
  Shown the first 2 times per action, then never again. This replaces every tutorial text line.
- **Cleared / Lost:**
  - A one-word banner appears over the board (workstream B).
  - A results panel rises behind the board: stars, sheep, coins.
  - Cleared: Next is the big button, Retry and Upgrades are small.
  - Lost: Retry is the big button, Camp is small. The rim's burn meter glows full so the reason is visible.
- **Level select:**
  - a card carousel above the next level's dimmed board;
  - slap left or right with an open hand to browse;
  - ray-pinch Play;
  - each card shows the level number, name, stars, and a chip for what the level introduces;
  - Camp and Upgrades as small icon buttons.
- **Upgrades:** 4 tiles (gem, pump, slot, tower). Each has one word, one number and a price. Tiles you can't afford
  are greyed out.
  - Headset test 3 (2026-11-22): each tile shows a still picture of its model (rendered once, offscreen), not a
    turning 3D model. Pointing at SLOT or TOWER swaps to a picture of the change.
  - While at least one upgrade is affordable, the UPGRADES buttons (results, level select) carry a pulsing yellow star.
- **Tilted board:** every between-level panel also clears the tilted board's raised top edge as seen from the seat
  (the panels go up to just under eye level; a flat board is unchanged).

## 12. Work split

All work happens in one Plastic workspace with one Unity editor, so sessions are separated only by which files they
own.

- Run at most 3 code sessions at once, and only one Play or test run at a time.
- Keep every edit compiling.
- `FireRescueGame.cs` is split by region:
  - C: trees;
  - L1: water stations and pump;
  - U: dashboard;
  - O: arena placement.
  Re-read it before every edit and keep edits small.

**Wave 1: start now**

| ID | Work | Owns |
|---|---|---|
| A | Ray origin at the thumb-index web (虎口) + menu snap | `EmberdeepHandRay`, `EmberdeepHandRayMenu`, `EmberdeepGazePinch`, the aim parts of `MetaShadowPlayer.cs` |
| L1 | Water loop, all of §3 (critical path) | `FireRun.Magic/Spells`, `FireRescueGame.Magic/Spells/Water`, `EmberdeepManaCrystal`, `EmberdeepPalmAimVfx`, `EmberdeepPalmGesture`, `EmberdeepWaterStationView`, `EmberdeepWaterSupply`, `EmberdeepRefillInteraction`, `EmberdeepRefillTutorial`, `EmberdeepLeverStroke`, `FirePumpGesture`, `EmberdeepSpellVfx`, and the station/pump lines of `FireRescueGame.cs` |
| C | Readability, §4.6 | `FireRescueVfx`, the tree build and refresh regions of `FireRescueGame.cs`, the tree material/shader, a new readout file |
| B | Sheep audio, petting + boing, outcome moments (§2 step 3, §4.5), camp prop overlap | sheep files, `FireRun.Animals`, `FireRescueGame.Animals`, `FireFeedback`, a new outcome view, `EmberdeepCamp` (.cs/.unity), `EmberdeepCraftDetails` |

UI mockups: done. The owner picked layout A (§11b).

Status, 2026-11-21:
- Wave 1: L1, C and B are done, not checked in. A local backup is in `.agents/local/snapshots/emberdeep-wave1-2026-11-21`.
- A hasn't started.
- Wave 2: L2, U, A and O are done, not checked in, not tested on a headset. The rim strip now sits ~75° below the eye
  (O's measurement). If it doesn't read on a headset, move the readouts to the board's back edge (~37°).
- Wave 3: L3 and T are done, not checked in, not tested on a headset.
- Wave 4: LV's first pass is done (six authored levels, §8), not checked in, not tested on a headset.
- Wave 4: LV (level content, first pass) and X (text pass) are running in parallel. LV adds no strings; X owns all
  text, including level names. X's first pass is done, not checked in: §13 plus `Docs/Emberdeep/TextPass.md` for
  review.

**Wave 2**

| ID | Work | After | Owns |
|---|---|---|---|
| L2 | Board sim: §4.1-4.4 (grass, wind, wetness, forecast) | C | `FireRun.cs` sim, tree build (grass), `FireRescueVfx` (grass fire), new wind and weathervane files |
| L3 | Pieces (§5) + Plan phase and look (§6) | L2, O | new piece files, `FireDiorama`, `FireRun.Preparation`, `FireRescueGame.Preparation` |
| U | In-game UI pass, layout A (§11b) | L1; alongside L3 | the dashboard part of `FireRescueGame.cs`, `.Shop`, `.Tutorials`, `EmberdeepHudPose/MenuPose/ForestUi` |
| O | Board shrink (§5, Reach) + options (§10) | B, C, L2 | `FireMiniature.cs` placement, the field generator in `FireRun.cs` (rows/columns), island/plinth size, the `EmberdeepCamp` settings page |

**Wave 3**

| ID | Work | After | Owns |
|---|---|---|---|
| T | Camp tutorial (§9) + stage cut from 8 to 6 | L1, B | camp files, the `FireRun.Magic` stage tables |
| LV | Level content, §8 | L2, L3 | level data |
| X | Text pass | everything else | all strings + `Localization/Translations.tsv` (phrase-matched, so one owner) |

## 13. Text voice (workstream X, 2026-11-21)

The player reads in a headset, often mid-move. Text should read like a shipped game's: plain, short, and the same
word for the same thing every time. The review table for the first pass is `Docs/Emberdeep/TextPass.md`.

**Rules**

1. **Short and concrete.** Name the thing or the move, not the system behind it.
2. **Two kinds of text.**
   - *Labels* (buttons, banners, chips, tiles, page titles): CAPS, 1-2 words, no punctuation except `?` on a
     question.
   - *Prompts* (menu only, never in a fight): sentence case, one short sentence. Use a full stop only when there are
     two sentences.
   - *Spirit subtitles* (camp tutorial voice-over only, §9): the owner's spoken lines, word for word, in the
     character's voice. The banned-word list is for UI text, not dialogue. Lines that name a move still use the
     glossary nouns (gem, palm water, pump), so the zh cards match the game's terms.
3. **Buttons are verbs or places:** PLAY, RETRY, NEXT, CAMP, BACK, RESUME, SKIP, QUIT, CANCEL.
4. **Banners are 1-2 words:** FIRE OUT, FOREST SAVED, FOREST LOST.
5. **In a fight, no sentences.** A pop-up is at most a word and a number: SAVED +15, CHAIN x3, CABIN LOST.
6. **Level names are place names** (§13 table), never jargon.
7. **Banned words.**
   - Jargon: incident, sector, expedition, first response, deploy, mission, objective, showcase, gear.
   - AI tells: embark, journey, adventure, unleash, master, dive in, "let's", "ready to …?".
   - Tics: stacked em dashes, exclamation marks, Title Case On Every Word.
8. **One fixed noun per thing** (glossary below). Never a synonym: no crystal, mana or orb for the gem; no well,
   hydrant or station for the pump; no tank for the tower.
9. **Numbers:** counts as `3/5`, units only where they are not obvious (`+2 cm`, `80%`).
10. **zh-Hans** reads natural, not literal. Match the tone, and keep labels as short as the English (2-4 characters
    for a button). No 请 on buttons; full-width punctuation in prompts, none on labels.
11. **Adding a string:** add its `en` → `zh-Hans` row to `Localization/Translations.tsv` in the same change and rebuild
    the tables (`EmberdeepLocalizationBuilder.RebuildTables`). Text is phrase-matched, so a changed English string with
    no row shows English in zh.

**Glossary**

| en | zh-Hans | What it is |
|---|---|---|
| gem | 宝石 | the water store on the back of the water hand |
| palm water | 掌心水 | the spray from the open palm |
| water hand | 喷水手 | the hand that aims (option: LEFT / RIGHT) |
| pump | 水泵 | hammered with a fist to refill the gem |
| tower | 水塔 | piece: rains once when fire comes near |
| excavator | 挖掘机 | piece: digs a firebreak line |
| plane | 飞机 | piece: earned in a fight, drops water on a strip |
| tray | 托盘 | where the pieces wait beside the board |
| board | 沙盘 | the miniature forest (not on screen yet) |
| sheep | 小羊 | |
| cabin | 小屋 | |
| camp | 营地 | the menu island |
| level | 关 (第 3 关) | one of the 6 stages |
| coins, stars | 金币, 星 | |
| upgrades | 升级 | |
| wind, grass | 风 (chip: 风向), 草 | |

**Level names**

| Stage | Teaches (§8) | en | zh-Hans |
|---|---|---|---|
| 1 | palm water, sheep | SHEEP MEADOW | 牧羊草甸 |
| 2 | the pump | WELL HOLLOW | 老井坳 |
| 3 | towers, Plan | TOWER HILL | 水塔岗 |
| 4 | wind | WINDY RIDGE | 大风岭 |
| 5 | grass, excavator | HAYFIELD | 干草坡 |
| 6 | the plane, everything | EMBERDEEP | 余烬深林 |
