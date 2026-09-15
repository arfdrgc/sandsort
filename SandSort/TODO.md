# SandSort — TODO / Current State

Rolling status file. Read this first when resuming work; it records where things
actually stand, not what was planned. Design specs live in `Docs/`.

**Last updated:** 2026-09-15

---

## PNG sand pattern workflow — DONE, in use

A level's starting picture is authored as a lossless pixel-art PNG instead of a
hand-painted Inspector pattern. Faz 0 (colour/import verification) and Faz 1
(working pipeline) are both complete and verified in Play from
`Assets/Scenes/BaseScene.unity`.

Pipeline: `PNG` → `SandPaletteSO` (exact RGB → slot lookup) →
`SandPatternTextureConverter` → `SandCylinderPatternData` → the existing
`SandCylinderSandGrid` simulation. Nothing downstream of the pattern changed.

| Piece | Path |
|---|---|
| Palette mirror (PNG matching only, **not** the gameplay colour authority) | `Assets/Game/CurrentGame/Scriptables/SandPaletteSO.cs` |
| Palette asset, 17 slots | `Assets/Game/CurrentGame/Data/Sand/SandPalette.asset` |
| Converter (runtime-safe, no `UnityEditor`) | `Assets/Game/CurrentGame/Scripts/Sand/SandPatternTextureConverter.cs` |
| Forced import settings + editor-only consistency check | `Assets/Game/CurrentGame/Scripts/Editor/SandPatternTextureImportSettings.cs` |
| `.gpl` palette export for Aseprite/Piskel | `Assets/Game/CurrentGame/Scripts/Editor/SandPaletteGplExport.cs` |
| Authoring rules for level designers | `Docs/level_design.md` → *Sand Pattern Authoring (PNG)* |

Settled decisions worth not re-deriving:

- `alphaIsTransparency = false`. With it on, Unity dilates neighbour RGB into
  fully transparent pixels, so a deliberately empty region reads as sand.
- The converter checks **alpha first**: `alpha < 128` → `EMPTY`, no RGB lookup.
- Colours are matched **exactly**; there is no nearest-colour fallback. Never
  eyedropper from a screenshot (per-pixel sand noise + bloom/grading).
- Android/iOS platform overrides must stay uncompressed RGBA32, or colour
  matching breaks on device while the Editor still looks correct.
- `_sandTexture` wins over `_sandPattern`; old pattern-based levels still work.
- Measured angle of repose of the simulation is exactly **45°**. A 100 %-filled
  canvas makes that rule vacuous and gives a perfectly stable opening frame
  (0 cells of drift measured over several seconds).
- On-screen scale for a 9-block board on a 1080 px phone is **2.98 px/cell**:
  1 cell = texture, 2–3 cells = minimum readable feature, 4+ = a shape.

Regression fixtures kept on purpose: `_Faz0_ColorProbe.png` (170×170, colour +
transparency probe) and `_Faz17_AllColors.png` (272×272, all 17 slots).

**Out of scope / deliberately not built:** the full Checker EditorWindow, the
palette-authority refactor (Faz 2), Faz 3 build optimisation, and any wiring of
`EditorColorPalette` into runtime.

---

## 5 → 17 sand colours — DONE

The 5-colour limit was never in runtime code — only in data, plus the
editor-only `SandCylinderPatternDataEditor.SlotColors`. Grown to 17 slots:

- `Level.prefab`: `sandColors` 5 → 17, `_sandPaletteColors` 5 → 17,
  `_containerColors` 5 → 9.
- `SandPalette.asset`: 17 slots.
- New `ColorSO` assets: `Color_BlueLight`, `Color_BrownDark`, `Color_GreenDark`,
  `Color_Yellow` — byte-exact against their palette slots.
- `Basic_Green_Dark.mat` (`#00820A` → `#1B5E20`) and `Basic_Yellow.mat`
  (`#FFB900` → `#FFEB3B`) were re-aligned to the palette; 7 of the 17
  `Basic_*` materials had drifted and were unreferenced.
- Editor-only consistency check added inside
  `SandPatternTextureImportSettings` (`Tools/SandSort/Check Sand Palette
  Consistency`). It **reports only** and never writes — auto-repair would
  silently change how the game looks.

The 12 new colours were **appended as slots 6–17** rather than reordered into
`ColorSO.ItemColor` enum order. Enum ordering would repaint every existing
5-colour level, because `TestHandPaintedPattern` stores raw slot indices 1..5.
`SandCylinderPatternDataEditor.SlotColors` was deliberately left alone.

---

## All 17 colours wired end-to-end — DONE (2026-09-15)

Every palette slot now has a `ColorSO`, so the full chain
`ColorSO → material → palette slot → sand → container` exists for all 17.

- New `ColorSO` assets (same structure as the existing 9): `Color_BlueDark`,
  `Color_Brown`, `Color_BrownLight`, `Color_GreenLight`, `Color_Purple`,
  `Color_PurpleLight`, `Color_PurpleDark`, `Color_Pink`.
- `Level.prefab` `_containerColors` 9 → 17 (the 8 appended; lookup is by
  `ItemColor`, order irrelevant).
- The remaining 5 drifted, previously unreferenced materials were re-aligned to
  their palette slot (`_BaseColor` only, same as the earlier Green_Dark/Yellow
  fix): `Basic_Blue_Dark` `#0D47A1`, `Basic_Green_Light` `#81C784`,
  `Basic_Purple` `#9C27B0`, `Basic_Purple_Light` `#BA68C8`, `Basic_Pink`
  `#E91E63`. All 17 container materials now byte-match their sand colour.

Test level: `Data/Levels/SandSort_AllColors17_1x1_9x10_Level.asset` — board 9×10,
17 × `Shape_1x1`, texture `SandSort_AllColors17_9x10.png` (306×306). Artwork is
deliberately trivial: full-height vertical stripes, board block *b* (0–7) holds
slot `2b+1` in its left 17 px and `2b+2` in its right 17 px, block 8 is YELLOW.
Containers sit at row 0 (odd slots, x = block) and row 2 (even slots). Two
colours share each block, so hand-testing means verifying one, moving it out of
the top row, then the other. **Not** assigned to `_testLevel` — point
`0_Temp_Level_List` → `_testLevel` at it to play it.

Verified from `BaseScene` in Play: `validateLevel` OK (17 sand colours ↔ 17
containers), palette consistency check OK, every block's bottom 16-row band
holds exactly its 2 stripe colours (272 cells each, YELLOW 544). Each of the 17
containers, moved in turn to its block's top-row cell for 2.5 s, pulled only its
own colour (`filled` delta == own-slot sand drop, other slots' drop = 0) and
rendered with the matching `Basic_*` material. No code, simulation, extraction or
tuning change; the 9-colour level and its PNGs are untouched.

---

## Sand flow speed + horizontal hanging rows — DONE, final state (2026-09-15)

Status: **in code, verified in Play from BaseScene, accepted.** Only open item
is device cost (see *Open* below). No commit.

### Problems that were solved

1. **Slow repositioning.** After extraction a steep face moved only through its
   outermost grains (one diagonal StepCell move each; grains behind wait), so a
   large collapse took 25–45 s. Target from the user: 3–5 s, natural 45° flow.
2. **Horizontal hanging rows ("sarkıt").** 2+ cell rows of sand drawn over empty
   cells, mostly in the extraction band.

### Solution (no StepCell / extraction rule change)

- **Active-region sub-steps** — `SandCylinderSandGrid.RunActiveSubSteps`: extra
  row-major StepCell passes (alternating row direction) only inside the union
  of bounding boxes of cells that changed in the last
  `activeSubStepWindowSeconds`, grown by `activeSubStepMargin`. Budget is
  `activeSubStepsPerTick × sandSimulationSpeed` passes/s, spread evenly over
  frames by an accumulator (cap `activeSubStepsPerTick × 8` per frame).
- **Frame order (P1)** — the sub-steps run in `SandCylinderSandGrid.LateUpdate`,
  not `Update`. `Update` now only runs the `Step()` loop.
  `SandCylinderRenderer` has `[DefaultExecutionOrder(100)]` (it has no
  `Update`, so Step-vs-extraction ordering is unchanged), so each frame is:
  grid Step (Update) → extraction (Update) → sub-steps (grid LateUpdate) →
  texture copy (renderer LateUpdate).
  Cause it fixes: with sub-steps in `Update`, extraction holes opened *after*
  the passes and were drawn once → 1-frame hanging rows (97 % lived 1 frame,
  99 % in the extraction band, ~2/3 had lost support since the previous frame).
- Files: `SandCylinderSandGrid.cs`, `SandCylinderRenderer.cs`,
  `SandCylinderTunables.cs` (new header *Active-Region Sub-Steps*, default 0 =
  off), `Level.prefab`.

### Verification (Editor, M-series Mac, BaseScene, fixed seed)

Flow speed, SandBuckets ORANGE→YELLOW (`activeSubStepsPerTick` sweep):
90 % of the flow 26 s (0) → 5.8 s (16) → **5.3 s (24)** → 5.0 s (32); settle
>45 s → ~7.5 s. Canyon 9 colours (24 s dig, blocks 3–4) settles 0.5 s after
digging stops.

Frame order, old order (E0, emulated in PreUpdate) → **P1 (permanent)**:

| Metric | Canyon 9 E0 | Canyon 9 P1 | Buckets E0 | Buckets P1 |
|---|---|---|---|---|
| Frames with a 2+ cell hanging row | 12.5 % | **0.1 %** | 37.0 % | **11.6 %** |
| Runs per frame | 0.388 | **0.001** | 0.848 | **0.166** |
| Longest run (cells) | 8 | **2** | 14 | **5** |
| Runs inside extraction band (y<40) | 437 | **0** | 583 | **9** |
| Total extracted | 11935 | 11960 | 12465 | 12449 |
| YELLOW 90 % / settle | – | – | 5.3 / 7.3 s | 5.3 / 7.5 s |
| Sub-step cost | 4.91 ms* | 6.57 ms** | 1.48 ms* | 2.25 ms** |

\* average over the run, measured in the hook. \*\* snapshot bench at the
busiest moment (18 passes/frame). Different methods — P1 runs the same passes on
the same region, so cost is unchanged by design.
Extraction totals differ <0.2 % (run-to-run noise from different grain positions).

Visual close-ups: Canyon +7 s — 0 hanging rows, 0 covered holes, band clean;
+16 s — clean 45° V, no ledges/streaks. Buckets +2 s — 0 rows; +4 s — one
2-cell row, not visible, bucket pixel art intact.
Remaining bucket runs are ≤5 cells, 1 frame, on the flowing slope face —
StepCell peeling the surface, not frame order. Horizontal dangling is
**largely solved**; not worth chasing further unless it shows on device.

### Decisions (don't re-litigate)

- **The straight 45° settled V is accepted** (user decision). It is the
  StepCell rule's attractor (every static state has column steps ≤1); without
  sub-steps the sim reaches the same V, just in ~53 s. No jitter / taper-off of
  sub-steps is wanted.
- Tried and rejected: surface "avalanche" rules (slab shear, rolling grains) —
  no effect; column-major extra gravity — vertical colour streaks;
  `pileStability` 0 — curtain/barcode look; `fallSidewaysMixChance` 0 and
  `minGapForFreeCascade` 4 — no effect on hanging rows; +1 extra pass after
  extraction (P2) — worse (112 runs) and +0.39 ms.
- StepCell, extraction, total extracted sand and all other tuning are unchanged.

### Current sand tuning (`Level.prefab` → SandCylinderTunables)

`sandSimulationSpeed 45`, `maxFallCellsPerTick 6`, `fallSidewaysMixChance 0.1`,
`lateralSpreadChance 1`, `pileStability 0.28`, `minGapForFreeCascade 2`,
`settleJitterChance 0.015`, `settleJitterWindowTicks 3`, `colorNoiseAmount 0.2`,
`activeSubStepsPerTick 24`, `activeSubStepMargin 24`,
`activeSubStepWindowSeconds 0.5`, `blockCellSize 34`, `extractionRangeY 0.7`
(changed by the user), `extractionNeighborPrimeRadius 1`,
`extractionCaveInLeftBias 0.5`, `extractionPrimeMaxEmptyGap 3`.

### Open

- **Device profiling (IL2CPP).** Cost scales with passes × active-region area:
  the 306-row Canyon grid is ~6.5 ms/frame in the Editor at peak (the active box
  spans the full height); the 170-row grid ~2 ms. If too expensive on device,
  16 passes ≈ 2/3 the cost at 5.8 s flow.

### Notes for the next session

- Measurement harness pitfall: `EditorApplication.update` fires ~5.7× per player
  frame (346/s vs 60 fps). Drive per-frame prototype work from
  `PlayerLoop` injection (PreUpdate / PostLateUpdate) or gate on
  `Time.frameCount`; measure what is drawn at the start of PostLateUpdate.
  Remove injected systems afterwards.
- Level redirects for tests were done in memory only. On disk
  `0_Temp_Level_List.asset → _testLevel` = Toucan
  (`SandSort_Toucan7_9x10_Level`); the Editor held an **unsaved** in-memory
  change to SandBuckets from the user — check before saving the project.
- MCP `manage_camera` screenshots land in `Assets/Screenshots/` (creates
  `.meta`); move them out / delete after use.
- `CLAUDE.md` *Current work status* still says the sand simulation "has not been
  modified" — stale since this work (grid sub-steps + renderer execution order).

---

## Sand buckets level (2 colours, 5×4) — delivered (2026-09-15)

`Data/SandPatterns/SandSort_SandBuckets2_5x4.png` (170×170 = 5×5 blocks) and
`Data/Levels/SandSort_SandBuckets2_5x4_Level.asset` (board 5×4). YELLOW sand
with 4 ORANGE sand buckets, wind ripples and a layered orange/yellow dune floor.
The buckets are **pixel art inside the sand**, not scene objects — no bucket
model/prefab/system exists and none was added (decided with the user). All 4
share one size, rotations −30/20/78/−36°, ≥17 cells from the frame, ≥22 cells
apart, ≥7 cells from any other orange. Two `Shape_2x2`: YELLOW at (0,0), ORANGE
at (3,0). Not assigned to `_testLevel`.

Verified in Play: PNG ↔ grid 0 mismatches; both colours in every block's bottom
band (ORANGE 203–345, YELLOW 199–341 per block). YELLOW 20 s: +4970 / YELLOW
−4970 / ORANGE −0. ORANGE 19 s: +1006 / ORANGE −1006 / YELLOW −0 — it stalls
once the floor bed under blocks 3–4 is gone, since the buckets sit behind
yellow. Yellow extraction collapses the whole mass into one 45° slope and the
buckets smear into orange clusters rather than sliding intact.

---

## Tutorial level (apple, 2 colours) — delivered (2026-09-15)

`Data/SandPatterns/SandSort_TutorialApple2_7x5.png` (238×238 = 7×7 blocks) and
`Data/Levels/SandSort_TutorialApple2_7x5_Level.asset`. Board is **7×5**
(`boardSize {7,5}`), not 5×7: sand width is locked to `boardSize.x`, so a
7-block-wide picture needs 7 columns (decided with the user). RED apple (body,
stem, leaf) on a BLUE background, BLUE shine + leaf vein. Two `Shape_2x2`:
RED at (2,0) under the apple, BLUE at (5,0) under the background. Not assigned
to `_testLevel`.

Verified in Play: PNG ↔ live grid 0 mismatching cells; bottom band RED in blocks
1–5, BLUE in blocks 0–2 and 4–6. RED at the top row for 25 s: +11229 filled,
RED sand −11229, BLUE sand −0. BLUE for 24 s: +7842 / −7842 / RED −0. The apple
collapses into a V funnel with 45° slopes. Note the pace: on this small grid one
2×2 drains ~36 % of the red in 25 s, so the picture is mostly gone quickly —
worth watching when tuning tutorial feel (no tuning was changed).

---

## Toucan artwork trial — delivered (2026-09-15)

`Data/SandPatterns/SandSort_Toucan7_9x10.png` (306×306): the mockup's toucan
(`Docs/selected_sand_idea_mockup.png`) on a branch with leaves and a flower bed.
7 colours: BROWN_DARK (background), BLUE, BLUE_DARK, WHITE, ORANGE, YELLOW,
GREEN. 100 % filled, 1 px = 1 cell, 0 one-cell-thick features. Test level
`Data/Levels/SandSort_Toucan7_9x10_Level.asset` (9×10, 7 × `Shape_2x2`), not
assigned to `_testLevel`.

Verified in Play: readable at game scale, 0 cells of drift before extraction,
all 7 colours present in the bottom 16-row band (live grid == PNG). A single
BLUE container at blocks 5–6 for ~45 s pulled 1687 cells: a V funnel opened
through the tail and body, the beak and back streaked down into it and the
surface notched, while blocks 0–3 and 8 stayed untouched.

---

## 9-colour artwork — v2 delivered, quality work continues

Test level: `Data/Levels/SandSort_Canyon9Colors_9x10_Level.asset` (board 9×10,
9 × `Shape_2x2` containers). Colours in use: WHITE, RED, BLUE, ORANGE, GREEN,
BLUE_LIGHT, BROWN_DARK, GREEN_DARK, YELLOW. Reachable from
`0_Temp_Level_List.asset` → `_testLevel`.

### v1 — `SandSort_CanyonStrata9_9x10.png` (306×306) — kept as reference

Purely horizontal stratification. Evaluated in Play and judged not good enough:

1. **Gameplay-critical:** 5 of 9 colours had **zero** cells in the bottom 16-row
   extraction band (BLUE, ORANGE, GREEN, BLUE_LIGHT, GREEN_DARK), forcing
   strictly bottom-up sequential play.
2. ~14 parallel dashed keylines (same `(x//6)%3` rhythm everywhere) read as a
   topographic map.
3. Identical Bayer 4×4 and ~11–13 row thickness at all 8 boundaries — the
   clearest "generated" tell.
4. High-contrast dithers (GREEN_DARK↔WHITE, RED↔BROWN_DARK) turned to
   salt-and-pepper at 3 px/cell.
5. Colours over-separated, one band each, no echoes.
6. Detail leaned on 1-cell speckle, which reads as noise at this scale.

File is **unchanged** and stays in the repo as the before/after reference.

### v2 — `SandSort_FaultScarp9_9x10.png` (306×306, 19 KB) — current

Artwork-only rework; no code, palette, `ColorSO`, container, simulation or
extraction change. Currently assigned to the test level's `_sandTexture`.

Composition moved from a horizontal band stack to a **fault-scarp cross
section**: strata dip ~9° and are broadly folded, a near-vertical fault splits
the picture with a 66 px throw, topography is stepped (high plateau left, cliff,
low plain right). Vertical/diagonal connectors carry upper-zone colours down to
the floor: a fault-breccia wedge from surface to base, three mineral veins
(BLUE_LIGHT / GREEN / YELLOW) that cut every bed, a meandering groundwater
channel, cave chambers and a basal conglomerate of clustered pebbles.

Specific fixes against the v1 findings:

- Dither varied per boundary — 5 different kernels, thicknesses 0/6/7/9/13/16/18
  px, and **3 boundaries with no dither at all**.
- High-contrast pairs (YELLOW→BROWN_DARK, BROWN_DARK→WHITE, WHITE→BROWN_DARK)
  now use a clean hard edge plus a keyline instead of a wide 50 % dither.
- Keylines kept but 4 of 8 boundaries dropped; the rest use different dash
  rhythms (`solid` / `long` / `short` / `dot`).
- Colour echoes added: ORANGE at 3 depths, YELLOW and WHITE at 4 each, GREEN on
  the surface *and* in a vein *and* in the gravel.
- Single-pixel random speckle removed; detail is now 4–15 cell pebbles
  (elliptical, lit top edge), 2.2–4.2 boulders, 10–14 px cave lenses.
- Canvas 100 % filled, no internal holes, uniform cell size, 0 off-palette
  pixels, alpha 255 everywhere.

Distribution: WHITE 15.1 %, RED 15.8 %, BLUE 15.2 %, BLUE_LIGHT 15.2 %,
BROWN_DARK 13.0 %, ORANGE 10.7 %, YELLOW 7.4 %, GREEN_DARK 3.9 %, GREEN 3.8 %.

### Verified in Play (BaseScene)

- **All 9 slots present in the bottom 16-row extraction band** — measured on the
  live grid: BROWN_DARK 2497, BLUE 961, BLUE_LIGHT 368, YELLOW 231,
  GREEN_DARK 222, WHITE 187, GREEN 163, ORANGE 156, RED 111. Per board block the
  count is 4–9 colours (v1 was 3–4). v1's five unreachable colours are fixed.
- Converter slot counts match the generated PNG exactly; container capacities
  derive correctly from pixel counts.
- **Single-container extraction, realistic rate, no speed-up:** BROWN_DARK over
  blocks 3–4 for ~95 s pulled 1656 cells (1.77 % of the grid, container 13.6 %
  full). A narrow V funnel forms, strata bend into it, colours mix at the
  throat, the surface collapses into a notch, and everything outside the funnel
  stays crisp. This is the intended erosion feel.
- **The WHITE test stalled at 47 cells.** This is the existing extraction
  mechanic, not an artwork defect: a container only removes its own colour, so
  once the reachable white in that column is gone it must wait for other
  containers to clear the intervening colours. Abundant colours (BROWN_DARK,
  RED, BLUE) flow for a long time alone; scarce ones (GREEN, GREEN_DARK) depend
  on sequencing — which is the puzzle.

### Untouched, on purpose

Sand simulation, extraction, capacity algorithm, container matching, renderer
behaviour, `SandCylinderDemo/**`, `SandColorUtility`, `GameplayTunables`,
`EditorColorPalette`, `pileStability` and every other tuning value — as of the
artwork work. (Later, 2026-09-15: active-region sub-steps + frame order were
added to the grid/renderer; see *Sand flow speed + horizontal hanging rows*.) No
shape-freezing or shape-preserving system exists or should be added — post-
extraction deformation is a core ASMR feature.

---

## Next up

**Artwork quality, not pipeline.** The goal is a picture a level designer could
plausibly hand-draw in Aseprite — v2 is a good skeleton but still recognisably
machine-even in places. Concretely:

1. Push v2 (or a v3) closer to hand-made: less uniform feature density, more
   deliberate focal points, fewer evenly-distributed accents.
2. Spread the scarce colours better across board blocks in the extraction band
   (some blocks still carry only 4 of 9).
3. Decide whether the generator stays a throwaway prototyping aid or whether the
   authoring story is purely "paint it in Aseprite from the exported `.gpl`".
4. Then, and only then, revisit the deferred items: Checker Window, palette
   authority refactor (Faz 2), build optimisation (Faz 3).

5. Sand feel: profile active sub-steps on device (IL2CPP) before tuning
   further; the flow speed, 45° V and hanging-row fix are otherwise closed.

No commits have been made for any of this work.
