# Emberdeep text pass, first pass (workstream X, 2026-11-21)

For the owner's review. The voice rules and glossary are in `LoopSpec.md` §13. Nothing is checked in.

- Captures, en and zh-Hans from the seated desktop eye: `Logs/EmberdeepTextPass/` (rewritten by
  `EmberdeepTextPassPlayTests`), copied to `.agents/local/captures/emberdeep-x-2026-11-21/`. The `*-zoom-*` shots turn
  to one panel at a 34° field of view so the words can be read at capture size. Each screen also has a `*-text.txt` of
  its visible labels. Start with `.agents/local/captures/emberdeep-x-2026-11-21/0-contact-sheet.jpg` (en | zh-Hans,
  9 screens).
- Scope: everything the Emberdeep flow can show. The legacy tool flow (`goddessTutorial:false`) is unchanged.
- "Start the incident": that was the old Plan dashboard button (`START INCIDENT >`, `FireRescueGame.Preparation.cs`).
  Since U's rim UI, Plan has no text and you strike the bell, so no Emberdeep screen shows it now. The line stays in code
  for the legacy flow (see Skipped).

## Before → after

### Camp sign and menu (`EmberdeepCamp.cs`)

| Where | en before | en after | zh-Hans before → after | Why |
|---|---|---|---|---|
| Sign, under the name | A MINIATURE FIRE EXPEDITION | *(removed)* | 微缩森林灭火冒险 → *(removed)* | "Expedition" is banned jargon. A title sign carries the name only. |
| Main button | Continue | CONTINUE | 继续游戏 → 继续 | Buttons are CAPS like every other button in the game; zh as short as en. |
| Main button | New Game | NEW GAME | 开始新游戏 → 新游戏 | Same. |
| Main button | How to play | HOW TO PLAY | 玩法教学 → 教程 | Same; 教程 is the usual menu word. |
| Main button | Settings | SETTINGS | 设置 | Caps only. |
| Main button | Quit | QUIT | 退出 | Caps only. |
| Quit page, title | Quit Emberdeep? | *(same)* | 退出《余烬深林》？ | Already reads right. |
| Quit page, body | This showcase does not save after closing. | Your progress won't be saved. | 此演示版关闭后不会保存进度。 → 进度不会保存。 | "Showcase" is dev talk; say what the player loses. |
| Quit page, buttons | Cancel / Yes, Quit | CANCEL / QUIT | 取消 / 确认退出 → 取消 / 退出 | A button is one verb. |
| Ray prompt, no hand seen | SHOW AN OPEN HAND TO BEGIN | Hold up an open hand | 伸出张开的手，开始操作 → 举起一只张开的手 | Prompts are sentence case; "to begin" adds nothing. |
| Ray prompt, pointing | AIM YOUR HAND AT A BUTTON␣␣-␣␣PINCH | Point at a button and pinch | 用手指向按钮，再捏合确认 → 指向按钮，捏合选择 | No shouting, no spaced dash. |
| Ray prompt, on a button | BUTTON FOUND␣␣-␣␣PINCH WITH THE POINTING HAND | Pinch to select | 已选中按钮，用指向它的那只手捏合 → 捏合选择 | The highlight already says "found". |
| Ray hint | Use either hand. Open your fingers before each pinch. | Either hand works. Open your fingers between pinches. | 左右手均可操作，每次捏合前请先张开手指。 → 左右手都可以，每次捏合前先张开手指。 | Plainer; zh drops the stiff 均可操作/请. |
| Placing the sign | Look ahead\nFinding your headset position... | Look straight ahead | 请看向前方\n正在定位头显…… → 请看向正前方 | One instruction, no system talk. |

### Options page (`EmberdeepCamp.Settings.cs`)

| Where | en before | en after | zh-Hans before → after | Why |
|---|---|---|---|---|
| Row label | WATER HAND | *(same)* | 水之手 → 喷水手 | 水之手 reads like a spell name; 喷水手 says what the hand does. |
| Row buttons | LEFT HAND / RIGHT HAND | LEFT / RIGHT | 左手 / 右手 | The row label already says HAND. |
| Unchanged | SETTINGS, HEIGHT, DISTANCE, MUSIC, SOUND, RECENTER, BACK, `+2 cm`, `80%` | | 设置, 高度, 距离, 音乐, 音效, 重置位置, 返回 | Already short and concrete. |

### Camp tutorial (`EmberdeepCamp.Tutorial.cs`)

| Where | en | zh-Hans | Note |
|---|---|---|---|
| Skip button | SKIP | 跳过 | Unchanged words; the label now renders at its authored size (Fit, below). |

### Level select: names and chips (`FireRun.Magic.cs` `LevelNames`, `FireRescueGame.Screens.cs` `Chips`)

| Stage | Teaches | en before | en after | zh-Hans before → after | Why |
|---|---|---|---|---|---|
| 1 | palm water, sheep | PALM RESCUE | SHEEP MEADOW | 掌心救援 → 牧羊草甸 | Level names are places. A small grove with sheep. |
| 2 | the pump | FIRST RESPONSE | WELL HOLLOW | 初次出动 → 老井坳 | "First response" is jargon; a well fits the pump level. |
| 3 | towers, Plan | PARK PATROL | TOWER HILL | 公园巡护 → 水塔岗 | Names the new piece's home. |
| 4 | wind | THE GROVE | WINDY RIDGE | 林间营救 → 大风岭 | Says wind at a glance. |
| 5 | grass, excavator | FOREST FRONT | HAYFIELD | 森林前线 → 干草坡 | LV's layout is "a dry meadow down the middle". "Front" is military jargon. |
| 6 | the plane, everything | FOREST INFERNO | EMBERDEEP | 烈焰森林 → 余烬深林 | The last level is the place the game is named after. |
| Chip, stage 4 | | WIND | WIND | 风 → 风向 | One character reads thin on a chip; 风向 is the natural word. |
| Chip, stage 5 | | GRASS | EXCAVATOR | 草地 → 挖掘机 | The chip names the new piece; the level name carries the grass. |
| Chips, others | | PALM, PUMP, TOWER, PLANE | *(same)* | 掌心水, 水泵, 水塔, 飞机 | |

The legacy flow keeps `FireRun.Names` (FIRST RESPONSE … FOREST INFERNO).

### Outcome banner (`EmberdeepOutcomeView.cs`)

| Where | en before | en after | zh-Hans before → after | Why |
|---|---|---|---|---|
| Cleared | FIRE OUT! | FIRE OUT | 火灭了！ → 火已扑灭 | No exclamation marks on labels; 火已扑灭 is the standard phrase. |
| Last level cleared | FOREST SAVED! | FOREST SAVED | 森林获救！ → 森林得救 | Same. |
| Lost | FOREST LOST | *(same)* | 森林失守 → 火势失控 | 失守 is a military word ("a position falls"); 火势失控 is how a fire is lost in Chinese. Not literal, on purpose. |

### Fight pop-ups (`FireRescueGame.cs`, spell flow only)

| Where | en before | en after | zh-Hans before → after | Why |
|---|---|---|---|---|
| A tree goes out | PATH CLEAR (+ CHAIN xN) | *(nothing)*; CHAIN xN from the 2nd tree of a chain | 道路已畅通 → *(nothing)* / 连击 xN | "Path clear" was the old escape-path idea. The out puff already marks one tree (§4.6); only a chain is worth a word. |
| A sheep reaches safety | SHEEP SAFE!␣␣+ $15 | SAVED +15 | 小羊获救！ + $15 → 获救 +15 | Two words, no `$` (the game pays coins), no `!`. |
| A cabin burns | CABIN LOST | *(same)* | 小屋已毁 | Already right. |

### Between-level screens (`FireRescueGame.Screens.cs`): words unchanged

PAUSED 暂停 · RESUME 继续 · RETRY 重试 · CAMP 营地 · RECENTER 重置位置 · NEXT 下一关 · UPGRADES 升级 · PLAY 开始 ·
GEM 宝石 · PUMP 水泵 · SLOT 栏位 · TOWER 水塔 · MAX 满级 · BACK 返回. Numbers only otherwise (stars, `3/3`, coins,
`+100`).

## Layout and fit changes (text only)

1. **Camp buttons keep their commands.** `Button(..., label)`: the sign shows the CAPS label, while `WorkshopButton.Command`
   stays "New Game", "Settings" and so on, so tests and logs are unchanged. The ray prompts are constants
   (`EmberdeepCamp.HandPrompt/PointPrompt/PinchPrompt`).
2. **Camp control labels fit their pill** (`EmberdeepCamp.Settings.cs` `Control`). The fit box was a fixed .03 tall, so
   every label shrank. SKIP drew at 67% of its size in English and 52% in Chinese: LiberationSans's line box is 2.24×
   the size, Noto Sans CJK's 2.9×. The box is now 75% of the pill's height. CJK glyphs fill more of the em, so the two
   languages come out about the same height.
3. **Small-button captions stay on the panel** (`FireRescueGame.Screens.cs`).
   - `CaptionGap` .055 → .045.
   - Results button row `ResultsRowY` −.26 → −.22: 重试 and 升级 sat on the bottom frame.
   - Upgrades BACK is a wide pill with the word on it (.6 × .16, was .28 × .18 with a caption). Its caption hung
     below the board's bottom edge.

## Open questions for you

1. **Level names** are mine; LV left names and chips to the text pass. Swap any you don't like. The table is
   `FireRun.LevelNames`, plus one TSV row each.
2. **SLOT / 栏位.** The tile adds one tower to the tray. A clearer word might be TRAY / 托盘, or "+1 TOWER" (but TOWER is
   already the radius tile).
3. **HEIGHT / DISTANCE** on the options page don't say "board". BOARD HEIGHT would shrink the row labels; the icons
   carry it for now.
4. **FOREST LOST → 火势失控** is deliberately not literal (above).
5. **The sign tagline** is gone. If you want a line under EMBERDEEP, it should be concrete, not a slogan.

## Skipped: legacy tool flow or never shown in the Emberdeep flow

The phrase table keeps these rows, because the code still shows them in the legacy flow.

| What | Where | Why it's not shown in Emberdeep |
|---|---|---|
| Dashboard: status, economy, mission, tactics, help lines; NEXT INCIDENT >, CONTAIN THE FIRE, GEAR SHOP >, SHOP AFTER ROUND, RETRY SECTOR, FIRE CAMP, MAIN MENU, RECENTER, RETURN TOOL, TAKE OWNED TOOL, QUIT APP, the E M B E R D E E P / FIRE EXPEDITION header | `FireRescueGame.cs` BuildWorld/Refresh, `.Quit.cs` | `RefreshRim` hides the dashboard in the spell flow. |
| Plan text: GET READY / n FIRES / n SHEEP, STARTING IN, START INCIDENT >, the Plan help lines | `FireRescueGame.Preparation.cs` | Returns early in the spell flow (bell start). |
| Palm-lesson dashboard text, the MANA/crystal lines, EQUIPMENT FROM LEVEL 2 rack label | `FireRescueGame.Magic.cs` | Dashboard and rack are hidden in the spell flow. |
| Gear shop: firehouse title, tool pages, tuning, stations, prices | `FireRescueGame.Shop.cs`, `EmberdeepShopPreview.cs` | The spell flow uses the four upgrade tiles. |
| Water stations, docking and refill tutorial cues and statuses | `EmberdeepWaterStationView.cs`, `EmberdeepRefillTutorial.cs`, `EmberdeepRefillInteraction.cs`, `EmberdeepWaterSupply.cs`, `FireRescueGame.Water.cs` | Hydrant and creek are hidden in the spell flow. |
| Tool names and controls | `FireEquipment.cs`, `FireEquipmentView.cs` | Tools are legacy only. |
| Spell names and spell hints | `FireRescueGame.Spells.cs` | Behind `PlaytestAllSpells` (§11). |
| Demo hand captions | `EmberdeepInteractionDemo.cs` | Every Emberdeep use hides the caption (camp ghost, first hints, shop preview). |
| Board labels FILL TO RAIN, FILL n %, PROTECT n %, RAIN!, EMPTY | `FireDiorama.cs` | Hidden in the spell flow (L3); towers are pieces now. |
| Slow-recovery label | `FireRescueGame.Recovery.cs` | Dashboard child. |
| In-game quit modal: Quit Emberdeep? / This showcase does not save after closing. / CANCEL / YES, QUIT | `FireRescueGame.Quit.cs` | Opened only by the dashboard's QUIT APP. The Emberdeep flow quits from camp. |
| Outcome subtitle: Too many trees burned / SHEEP n/m | `EmberdeepOutcomeView.cs` | The spell flow shows the one-word banner only. |
| Legacy pop-ups: PATH CLEAR, SHEEP SAFE!, RAIN ACTIVATED, CABIN LOST - SAVE THE OTHERS | `FireRescueGame.cs` | Legacy branch of the pop-ups above. |
| Objective, hint, level hints, legacy level names | `FireRun.cs` | Dashboard only. |

## Dead in both flows but left in code (deleting them needs a code change, not a text change)

- **Forced first-upgrade shop lesson.**
  - Strings: "1. BUY YOUR FIRST MAGIC UPGRADE", "FIRST UPGRADE / MORE PALM WATER", `UPGRADE $ {price}`, "More water for
    your next rescue…", "UPGRADE YOUR MAGIC >", "Visit the shop and buy your first magic upgrade."
  - Where: `FireRescueGame.Tutorials.cs`, `FireRescueGame.Magic.cs:261`.
  - `FireRun.NeedsMagicUpgrade` is always false since T's stage cut. Their TSV rows stay until the branches go.
- **Palm-lesson objective** "PALM RESCUE: cool the grove and protect the sheep." (`FireRun.cs` `Objective`). It is only
  read by the hidden dashboard, and the palm lesson exists only in the spell flow.

## Removed from `Translations.tsv` (36 rows; no code literal uses them)

| Group | Rows |
|---|---|
| Replaced by this pass | A MINIATURE FIRE EXPEDITION · GRASS (the chip is now EXCAVATOR) |
| Old sound toggle (O made it a volume slider) | Sound: On · Sound: Off |
| Phase names nothing displays | FIGHTING · Fighting · CLEARED · Cleared |
| Old level name | FIRST SPARK |
| Single-tree goddess lesson (cut) | GODDESS HAND: extinguish the single tree with your palms. |
| Old shop and rack lines | GROWING MAGIC · WATER STATION / EQUIPMENT FROM LEVEL 4 · EQUIPMENT FROM LEVEL 4 · GET READY / RESCUE PREVIEW · WATER STATION / OPENS AFTER THE FIRST LESSON |
| Spell flow from before the gem (L1 removed it) | ABSORBING WATER · A crystal is empty - pump the well, then dip a hand in the basin · Cast with your hands. The board on the right shows each gesture. · SPACE: palm water \| 2: bolt \| 3: fan \| 4: orb \| R: pump \| E: absorb |
| Basin and lever well (L1 removed them) | PUMP THE LEVER · Water fills the basin · FILLING THE BASIN · TWIST THE WHEEL · The basin keeps filling · FAST FILL · AUTO FILL · Press the button for a fast fill · BASIN · DIP A HAND TO ABSORB · Pump the lever: each stroke fills the basin · Dip a hand in the basin to absorb · Twist the wheel once: the basin keeps filling · The basin refills slowly by itself · LIFT THE LEVER BACK UP · PUSH THE LEVER DOWN · CLOSE YOUR HAND ON THE LEVER |

How this was checked: every English row was matched against every C# string literal under `Runtime/` and `Editor/`
the way the localizer matches (letter-bounded, any case, `\n` as a newline). Only rows with no match were removed.

Changed in place (old English no longer used): AIM YOUR HAND…, Use either hand…, SHOW AN OPEN HAND…, BUTTON FOUND…,
Look ahead…, FIRE OUT!, LEFT HAND, RIGHT HAND. Changed zh only: Continue, New Game, How to play, FOREST LOST, WIND,
WATER HAND. Added: Your progress won't be saved., the six level names, EXCAVATOR, FOREST SAVED, SAVED.
`Translations.tsv`: 401 → 375 rows.

## Tables

`EmberdeepLocalizationBuilder` now also deletes table entries whose row left the TSV. Before, it only ever added:
there were 408 keys for 401 rows, and a stale English phrase could still match at runtime. The rebuilt tables hold 375
entries in en and zh-Hans. Every CJK character on the captured screens is in the zh font (Noto Sans CJK SC, the full
font), and the zh check finds no English word on any of them.

## Tests

- Changed because they assert old strings:
  - `EmberdeepMenuPlayTests`: the no-hand prompt is now `EmberdeepCamp.HandPrompt`.
  - `EmberdeepSettingsPlayTests`: LEFT HAND / RIGHT HAND → LEFT / RIGHT.
  - `EmberdeepRimUiTests.LevelCardsNameEveryStageAndAChip`: the new names, plus a check that the legacy flow keeps
    `FireRun.Names`.
  - `EmberdeepMagicTests`: the palm lesson's name.
- New: `PlayMode/EmberdeepTextPassPlayTests` (2). It walks the camp menu, options, quit and tutorial, then Plan,
  banner, results, upgrades, level card and pause. On every screen in zh-Hans it checks for visible English words and
  missing glyphs.
- Runs (2026-11-21 ~23:58). LV's level work was mid-flight in the same tree, and every failure outside the 6 legacy
  tests is a level-layout count (none touch text).
  - EditMode FireRescue.Tests: 342/347. The 5 failures are LV's: 023 progression counts, BoardLayout whole rows,
    LevelTests ×3.
  - PlayMode FireRescue.PlayTests full run: 266/280. 6 are the known legacy `FireRescuePlayTests`; 7 are LV's
    (BoardCues wind lean, MagicLesson / Readability / Showcase tree counts, Pieces ×3); 1 was mine
    (`OptionsPageReadsInChinese` expected 水之手) and is fixed.
  - Re-run of Settings, Menu, 022 Experience and TextPass: 26/26.
  - Build list restored byte-identical after each run.
  - LV's later run on the combined tree (LV's levels + this pass + the banner fix, 2026-11-22, LV's report):
    EditMode 347/347; PlayMode 275/281, the 6 being the known legacy `FireRescuePlayTests`.

## Layout issues seen in the captures (not text; for U, B, L2)

1. **Fixed (owner's request, 2026-11-22): the outcome banner stood in front of the results panel.**
   - Why: since the board shrink, the banner sits ~0.43 m ahead of the seated eye and spans ~−7° to −27°. The results
     panel, behind the board at ~0.94 m, spans ~+2° to −19°, so the banner covered its lower half and the buttons.
   - Fix: they take turns. The banner holds the view for `FireRescueGame.ResultsDelay`, now 1.5 s (was 1.1 s; it
     covers the loss flare and darken, 1.44 s, and the clear sting, 1.35 s). It then shrinks away while the panel
     rises (`EmberdeepOutcomeView.TickBanner`, the same 0.25 s exit it uses for the level select).
   - A fixed offset would break again under the HEIGHT/DISTANCE options, which move both.
   - Shots: `banner-*.png` → `results-handoff-en.png`. `EmberdeepTextPassPlayTests` asserts the hand-off.
2. **The dim veil cuts across the panels.** `PauseDim`/`SelectDim` puts a grey band over the lower part of the pause
   panel and the level cards (`pause-*.png`, `level-card-zoom-*.png`).
3. **The weathervane rooster pokes up through the middle of the level carousel** (`level-card-zoom-*.png`).

## Added later: tower tip (headset test 3, 2026-11-22)

`EmberdeepTowerTip.cs`: one prompt over the tray's tower in Plan, the first time towers appear. Plan is not a fight
(§13 rule 5), and the tip hides when the bell is struck. TOWER is the existing label row.

| Where | en | zh-Hans | Note |
|---|---|---|---|
| Tip title | TOWER | 水塔 | Existing row. |
| Tip line 1 | Pick it up and set it near a fire. | 拿起它，放在火附近。 | How to place it. |
| Tip line 2 | It rains once when fire gets close. | 火一靠近，它就会喷淋一次。 | What it does. 喷淋 matches the other tower rows. |
