# SandSort — TODO / Current State

Rolling status file. Read this first when resuming work; it records where things
actually stand, not what was planned. Design specs live in `Docs/`.

**Last updated:** 2026-09-22 (Sand Level Designer session note; extraction-driven 3D cube grains)

---

## Sand Level Designer — session note 2026-09-22 — in code, uncommitted

**Where it is:** `Scripts/LevelDesigner/SandLevelDesigner.cs` on the `---SandLevelDesigner` object in
GameScene, plus `Scripts/Editor/SandLevelDesignerEditor.cs`. Play-mode tool (Editor / dev build):
Play from BaseScene, **F2** toggles. Gameplay code touched: `Level` only (`containerMaterialOf` public;
`sandPaletteColors`, `hasContainerColor`, `containerColorOf`, `sandPalette` read-only accessors).

- **Phase 1 — scene editing: done, verified.** Working copy of the level's `ContainerData` list drawn
  as Shape-prefab previews (gameplay Containers hidden meanwhile). **1** create `_shapePool[0]` at the
  hovered cell · drag = move, grid-snapped · **R** rotate 0→90→180→270 · **Q** next pool shape
  (position / rotation / colour kept) · **Delete** remove.
  **Placement is unrestricted while editing (2026-09-22, unverified in Play):** create / drag / R / Q
  always apply even when the result is off-board or overlaps another piece — such a piece is only
  tinted red and blocks Save until fixed (the old refuse / revert / snap-back gates are gone; Board's
  gameplay rules untouched). The anchor cell for **1** still has to be on the Board.
  Keys act on the hovered piece, else the selected one. Shape Pool = the 13 canonical prefabs listed on
  the component (no pool asset). Verified by reflection-driven Play; real mouse drag hand-checked by
  the designer.
- **Phase 2 — colour + Save/Load: done, verified.** **C / Shift+C** next / previous colour;
  **S** save; **L** reload (discard). Save is Editor-only: `Undo` + `SerializedObject` write of
  `SandLevelSO._containers` (position, shape, rotation, color, cells), `SetDirty`, `SaveAssetIfDirty`.
  **Refused, nothing written**, if any piece is off-board / overlapping or has a colour with no
  ColorSO. The running Level is not rebuilt — reopen the level. Legacy shape-less entries are skipped
  at load (and so dropped by Save; no level has any). Round-trip verified: Save → YAML on disk →
  `LevelGenerator.loadLevel` spawns the designed pieces at the designed cells.
- **Sand Texture Colour Debugger: done.** The designer analyses `_sandTexture` (empty = the loaded
  level's own PNG) the way the loader does — opaque pixels vs `SandPaletteSO` author colours, slot →
  `Level.sandPaletteColors` → ColorSO — with a per-channel `_colorTolerance` (default 8; inexact
  matches flagged `approx`, the loader itself is exact). Verified on Toucan7 (7 colours, counts equal
  to an independent PNG decode), a texture override and a tolerance probe.
- **Debugger lives in the SandLevelDesigner Inspector** (no Game-view panel): analysed texture,
  summary (Texture / Matched / Missing / Placed Colors), one row per colour with swatch, pingable
  ColorSO (or red MISSING), placed count, pixel count, plus help boxes (missing ColorSO, approx,
  unmatched, placed-but-not-in-texture). Before Play it previews the assigned texture against the
  Level PREFAB; while designing it shows live data. Draws without errors; layout not yet eyeballed.
- **Current colour filtering:** C / Shift+C and 1 can only assign colours that are in the analysed
  texture AND have a ColorSO on the Level prefab (palette order). With no readable texture it falls
  back to palette ∩ ColorSO, loudly. 1 uses `_defaultColor` if allowed, else the first allowed colour.
- **`SandSort_Toucan7_9x10_Level.asset`** currently holds a hand-test save: 1 piece (GREEN T4 at
  (1,3), Deg270) instead of the original 7 — `git checkout` it before committing if unintended.
- **Nothing committed** (working tree only).
- **Next:** manually verify the Inspector layout (select `---SandLevelDesigner`, edit mode and
  during F2), then create a simple test level using every valid sand-texture colour once.

## Extraction-driven falling grains (3D cubes) — 2026-09-22 — in code, uncommitted

Status: **in code, compiles clean, verified by reflection-driven Play runs from BaseScene (Level 1:
red 2x2, yellow T4). NOT hand-played, NOT profiled on device, NOT committed.** Extraction logic, sand
physics, capacity and the fill visual's own look are unchanged.

**What replaced what.** Before, two unrelated effects ran: (A) a fixed-rate stream (160 grains/s per
column, regardless of `removed`) aimed at one point, `PourTarget`; (B) `ShapeFillParticles`, a burst
fired by fill-level steps along the Shape's whole top edge. Both are gone for Shapes. Now one effect,
driven by the extraction itself:
- `ExtractionGrid.Update` passes `grainTarget = null` to `ExtractAtPoint` (the controller skips its old
  stream when the target is null; the demo conveyor still uses it unchanged), then
  `ExtractionGrid.spawnGrains` reads `SandExtractionController.LastRemovedCells` (the cells that call
  actually removed) and spawns grains from them, spread evenly across the list.
- **Density** (measured on a 2x2 at ~55 fps: 1–30 cells per call, ~35/frame at full flow, ~18 through
  the middle, 1–8 tail): `CELLS_PER_GRAIN = 4` with the remainder carried per column,
  `MAX_GRAINS_PER_CALL = 8` (only bites on frame hitches; the excess is dropped). T4 peak ≈ 200 live grains
  (pool is 256 per colour).
- **Motion** (`SandExtractionParticleEffect.SpawnFallingGrain`): starts on the removed cell's exact screen
  pixel, keeps its X, falls from rest under `particleGravity`, dies at `ShapeSandFill.grainLanding` (top
  of the opening in that column, then 10–60 % of that column's depth — never through an L foot / T arm).
  World space. **Follow-before-rim:** while above the Shape's rim a grain moves with the Shape one-for-one
  (existing `followedTargetBySeed` machinery + `releaseLocalYBySeed`); the first frame at/below the rim
  it is released and continues in world space. Measured during a fast `dragTo` sweep (≤ 0.31 world/frame):
  following grains drift ≤ 0.003/frame relative to the Shape (96 % inside its span), 0.003 max jump on
  the release frame.
- **Spawn height fix:** the grain is drawn `FollowPourForwardOffset` (0.56) toward the camera; moving
  along world Z alone put it 43.7 px (~8 sand cells) ABOVE its cell, because the game camera is
  orthographic and pitched 20°. `OnScreenOver` slides it along the view direction instead: first-frame
  offset 0.00 px (direct emit), median 0.9 px in a real flow.
- **Look:** Shape grains have their OWN per-colour systems (`SandCubeGrains_ColorN`, built lazily) so the
  conveyor's billboard systems (`SandGrains_ColorN`) are untouched. Mesh render mode, runtime 24-vertex
  cube, runtime copy of the level's URP Unlit material with a 3-texel shade strip (face pairs at
  100/80/61 % of the band colour) — no new shader. Random start `rotation3D`, random
  `angularVelocity3D` 180–540 °/s. Size `particleSize × 1.25` (`CubeGrainSizeScale`; ≈ 6.5 px on a phone;
  1.0 read too faint). Shrinks to 0 over the last 20 % of life so landing does not pop. Colour = the
  band's palette colour (not the fill's dark→light ramp).
- **Render order:** the cube material is on the Transparent queue (3000). At queue 2000 the held Shape's
  `SelectedShapeDepth/Body` + Outline passes (Moow_Renderer, AfterRenderingOpaques, ZTest Always)
  painted over every grain inside its outline during a drag. Conveyor material stays at 2000.

**Files:** `ExtractionGrid.cs`, `SandExtractionController.cs` (read-only accessors `LastRemovedCells`,
`CellToWorld`, `ParticleEffect` + null-target skip), `SandExtractionParticleEffect.cs`,
`ShapeSandFill.cs` (burst removed; `grainLanding`, `grainIntake` added), `Shape.cs` (one `configure`
call). `ShapeFillParticles.cs` deleted (was never committed).

**Open:**
- Hand-play at speed: density (`CELLS_PER_GRAIN`), cube size, tumble speed, landing feel.
- Device profile: up to ~200 live 24-vertex cubes per colour, Unlit likely not GPU-instanced.
- Same-colour grains over same-colour sand rely on the darker cube faces to stay visible.
- `PourTarget` (`ShapeSandFill` / `Shape.sandPourTarget` / `Container.sandPourTarget`) is still built
  but nothing reads it — remove when convenient.
- Landing clearance in `grainLanding` still uses the unscaled `particleSize` (deliberate for now).
- Demo conveyor: code path unchanged, but the demo scene itself was not opened to look.

---

## Contact-limited extraction mouth — 2026-09-21 — in code, uncommitted

Status: **in code, compiles clean (0 errors / 0 warnings), verified by an instrumented real-drag run in
Play from BaseScene (Level 1). NOT hand-played, NOT profiled on device. No commit.**

**Problem (measured):** `ExtractionGrid` made a board column a candidate at any overlap > 0.0001 cells,
then called `ExtractAtPoint` with that column's CENTRE, which `BlockColumnRangeForCube` widened to the
whole 34-column block. A container that barely touched a new block drained all of it: in a slow real
drag, sand was taken up to **0.847 world (one full block)** outside the container's drawn edge, at every
block crossing (interior blocks as well as the outer edge blocks), never while flush against a wall.

**Rule now (a cell (x, y) is extractable in a block call only if all hold):**
1. `x` is in the block AND in `[xa, xb)` = the grid columns whose centres lie inside the container's
   drawn footprint (the unbroken run of active top-profile cells the candidate's owner cell belongs to).
2. `y <= 7 + min(edgeDistance, 7)`, `edgeDistance = min(x - xa, xb - 1 - x)`; both 7s are
   `extractionMouthRows / 2`. Edge column rows 0–7, +1 column in 0–8, …, 7+ columns in 0–14 (full 15-row
   mouth). An edge lying on the sand's outer wall is not an open edge and trims nothing.
3. Support check, colour match, one grain per column per call, random start column — unchanged.

Block candidates are NOT widened (a block with no physical overlap is never extracted from; a contact
that covers no column centre gives an empty window, `HasColorAtMouth` is false and the slot passes on).
Rate, budget, accumulators, block processing, candidate order and sand physics are unchanged.

**Code path:** `ExtractionGrid` (`_contactLow/_contactHigh` per shape cell, filled in
`gatherOverlappingColumns`) → `SandExtractionController.ExtractAtPoint(point, contactMinWorldX,
contactMaxWorldX, …)` (world span → covered columns) → `SandCylinderSandGrid.ExtractAtMouth /
HasColorAtMouth(…, contactStart, contactEnd)` + `MouthRowLimit`. The old signatures are wrappers passing
`(0, width)` = the old whole-block behaviour, so the demo conveyor is unchanged.

**Verified:** `MouthRowLimit` table matches the rule exactly (aligned 2x2, 1 / 4 columns into a new
block, fully inside it, flush at either wall, whole-width wrapper = 0–14 everywhere). Real-drag sweep,
3 trials (orange right/left, yellow right; drag via the container's own beginDrag/dragTo/endDrag, real
ExtractionGrid → ExtractAtPoint → ExtractAtMouth, every removed cell checked per frame): **0 cells
outside the footprint** (before: up to 0.845–0.847 world), 0 column / 0 row-limit violations, max row 14;
totals ≈ unchanged (2257→2239, 2264→2260, 5643→5796). A newly entered block now opens gradually
(first contact: 1 covered column, 1 cell) instead of ~27 cells across the whole block.

**Open:** hand-play feel (a new block is now drained gradually, spread over more frames); re-check the
colour-match stall numbers of the Step 8 level validation, which were measured with the whole-block mouth.

---

## Sand-resolution reset — 2026-09-21 — CURRENT BASELINE (Steps 1–9 done, uncommitted)

Status: **in code, compiles clean, verified by instrumented runs (edit-mode harness + Play from
BaseScene). NOT hand-played with real drags, NOT profiled on device. No commit.**

**User decision:** the old extraction + sand-resolution physics were *reset*, not fixed. Everything
the older sections of this file say about `ExtractColor`, the extraction band / `extractionRangeY`,
the 45° wings, same-colour refill, `StepCell`, priming and active-region sub-steps describes code
that **no longer exists**. Those sections are marked RETIRED / SUPERSEDED below and kept only as
history. **The architecture described here is the accepted baseline.**

### What the sand is now (`SandCylinderDemo/Scripts/SandCylinderSandGrid.cs`, in place — no new class/scene)

- **Storage:** `cells` (colour; the only array physics reads), `tint` (per-grain visual identity),
  `rowCount`. **Three write primitives — `Place` / `Move` / `Remove` — are the only writers**; each
  keeps cells, tint, counts, `CellsVersion` and the wake region in step.
- **Simulation:** `SimulatePass()` is hole-centric and colour-blind: it visits EMPTY cells bottom-up
  (so a void opened at the mouth reaches the surface within one pass), alternating scan direction per
  row and per pass. **`ChooseSource(x, y)` is the only place a movement rule lives.** Policy v0: the
  grain directly above drops in; with chance `flowSpread` a *supported* diagonal grain takes its place;
  with nothing above, a supported diagonal grain slides in with chance `slideChance`, otherwise the
  hole is asked again next pass. Every move goes one row down.
- **L3 rest friction (accepted, `restFriction` 0.5):** a *marginal* slide (hole already rests on
  sand/floor AND the grain is the top of its column — a step exactly two cells high) is refused with
  that chance per pass, and a refused marginal slide does not keep the sand awake. At 0 the policy is
  exactly v0 (regression reference: 357 passes / 499 291 moves on the carve test).
- **Scheduler:** all passes run in the grid's `LateUpdate` (after extraction in `Update`, before the
  renderer at order 100): `sandSimulationSpeed` passes/s, ≥1 pass per frame while awake, ≤16, and
  ≤ `maxCellsPerFrame` cell visits. **Sleep/wake:** the primitives wake exactly the neighbourhood
  their change can set in motion; a pass that moves nothing puts the grid to sleep — zero cost at rest.
- **Randomness:** two independent xorshift streams. `physicsRng` drives every movement/extraction
  choice (nothing uses `UnityEngine.Random`); `visualRng` only assigns a new grain's tint. Tint is
  never read by physics — verified: byte-identical results with all tints zeroed and with every grain
  recoloured.
- **Extraction is mouth-only:** `ExtractAtMouth` / `HasColorAtMouth`. The mouth is the collector's
  block columns × the bottom `extractionMouthRows` rows (**since limited to the physically covered
  columns with a 45° edge trim — see "Contact-limited extraction mouth" above**). No reach above it, no wings, no climbing past
  other colours, no refill rule. Colour matching exists only here. `SandExtractionController` calls it
  directly; `ExtractAtPoint` kept its signature but only `pointWorld.x` matters now (so
  `ExtractionGrid.cs` is untouched and still passes the sand-bottom Y, which is ignored).
- **Renderer:** shades each grain by `grid.GetTint(x, y)`; the world-locked `Hash01(x, y)` is gone, so
  the grain pattern travels with the sand. Change gate and execution order unchanged.

### Tunables now (`SandCylinderTunables`, 27 fields; 18 dead ones deleted in Step 7)

Sand Simulation: `sandSimulationSpeed` (passes/s, range 1–240, **code default 120**), `flowSpread`
0.5, `slideChance` 0.6, `restFriction` **0.5** (labelled EXPERIMENTAL, accepted), `maxCellsPerFrame`
400000. Extraction: `sandExtractionRate`, `maximumSandFlowRate`, `extractionMouthRows` 2 (`Level.prefab` now serializes 15). Sand Look:
`colorNoiseAmount` (amplitude of the per-grain tint).

- **`Level.prefab` still serializes `sandSimulationSpeed: 45`** (prefab edits were out of scope), so
  the game runs at ~1 pass/frame, not the evaluated 120. Set it in the Inspector when decided.
  `restFriction` has no serialized entry yet, so the prefab and both demo scenes pick up 0.5 from the
  code default. Stale keys of the deleted fields sit in `Level.prefab` and the two demo scenes until
  each is next saved (harmless).

### Step log

1. Provisional tunables. 2. `tint` / `rowCount` / primitives / two RNGs. 3. `SimulatePass`,
`ChooseSource` v0, scheduler, sleep/wake, mouth extraction; old physics + extraction deleted.
4. Controller on the new API (`VerticalRowRange`, `WorldYToGridRow`, shims removed; gizmo/log draw
the mouth). 5. Renderer reads per-grain tint. 6. Evaluation (below). 7. Dead tunables removed,
ranges/tooltips final. 8. Level + demo-scene validation (below). 9. This documentation.

### Step 6 evaluation — measured (SandBuckets2, 60 fps, large drain ≈16.5k grains through two blocks)

| Config | Drain | Settle | Rest surface (adjacent column diffs 0/1/2) | Wall shape in the drain |
|---|---|---|---|---|
| speed 45, v0 | 920 f / 15.7 s | 2300 f / 38.7 s | 2 / 167 / 0 (ruler-straight V) | ~75° chimney |
| speed 120, v0 | 513 f / 9.5 s | 1162 f / 20.3 s | 2 / 167 / 0 | same |
| speed 120 + **L3 0.5** | 517 f / 9.2 s | 582 f / 10.3 s | **31 / 74 / 64** (irregular) | same |
| + slideChance 1.0 (L2 upper bound) | 459 f | 398 f | 32 / 79 / 58 | same, + lockstep whiskers |
| + lateral reach 2 (L1) | 309 f | 692 f | 72 / 87 / 10 (L3 mostly erased) | same at equal volume, 3× airborne grains, whiskers |

Passing in every config: inward collapse from both sides, no rigid plug, comes to rest and stays
static (0 moves / 180 frames), sleeps, conservation exact, tint travels with the grains (0 mismatches
over 812k drawn-cell checks).

### Decisions (don't re-litigate)

- **L3 `restFriction` = 0.5 is accepted.** It is the only accepted change on top of v0.
- **L1 (lateral reach) is rejected** — no clear wall-shape gain, obvious whisker artefacts, undoes L3.
- **L2 (drop-sensitive slide chance) is rejected** — its theoretical maximum (slideChance 1.0) does
  not change the wall shape and makes shedding synchronous. L4 / L5 were never needed or built.
- **The v0 movement rule stays unchanged. No further wall-collapse redesign now.** The steep chimney
  during a fast drain is structural (any supported grain in a wall face can leave sideways from every
  exposed row, so faces translate instead of slumping from the top) and is invariant to pass rate,
  slide chance and reach. Known and accepted for now.
- Extraction never reaches outside the container's physically covered columns and never uses a block
  the container does not cover (contact-limited mouth, 2026-09-21); the 45° trim only shortens the
  covered columns near the footprint edges.
- Never reintroduce reach / wings / colour-aware refill, position-based visual noise, or tint in
  physics. New movement rules go inside `ChooseSource` only.

### Step 8 validation — levels (idealised player, real `Level` prefab build, prefab values)

Method: each level built through `Level.initialize` in Play; containers parked under the blocks
holding most of their colour in the mouth, re-arranged after a 1 s stall, 1.5 s charged per
re-arrangement. **Ignores board movement and shapes blocking each other — a best case.**

| Level | Re-arrangements | Est. time | Timer | Verdict |
|---|---|---|---|---|
| SandBuckets2 5×4 | 29 | ~152 s | 240 s | physically playable |
| Demo 1 YellowDuck | 15 | ~118 s | 240 s | physically playable |
| Demo 2 Watermelon | 26 | ~218 s | 240 s | physically playable (marginal) |
| Demo 3 Ladybug | 24 | ~143 s | 240 s | physically playable |
| Demo 4 Clownfish | 21 | ~177 s | 240 s | physically playable |
| Canyon9Colors 9×10 (`SandSort_FaultScarp9_9x10.png`) | 420 | ~1208 s | 300 s | **FAILS** |

**5 of 6 validated levels are physically playable.** On all six: `validateLevel` passes, no
uncollectable colour, every colour reaches a mouth through collapse (latest: re-arrangement 4), full
drain, extraction stayed inside the mouth rows, sand ends asleep with nothing floating, no
exceptions. Demo scenes: SandMixDemo pours / piles / rests and its cubes collect (conserved);
SandCylinderDemo cubes collect (conserved).

**KNOWN LEVEL-DESIGN ISSUE — FaultScarp9 / Canyon9Colors. Do NOT fix now (user decision).** The
pattern was authored for the old extraction band, which climbed past other colours; under mouth-only
colour extraction almost every block's mouth holds several colours at once (one block starts with 8),
so a parked container stalls after a few grains: 198 of 420 re-arrangements yielded < 200 grains and
even the idealised player needs ~4× the timer. It is physically drainable — the failure is
playability, and it is a pattern/level-design incompatibility, not a simulation bug.

### Open (none of it started)

- **Colour-match stall (design decision, the user's):** with mixed colours in a 2-row mouth a parked
  container stalls after a few hundred grains; even the 5 playable levels need 15–29 idealised
  re-arrangements. Levels/patterns authored from now on should keep mouths colour-coherent.
- `Level.prefab` speed 45 vs the evaluated 120; at 1 pass/frame a 2-row mouth draws a one-cell hanging
  row at the floor during extraction (mostly gone at 2+ passes/frame).
- Hand-play with real drags; by-eye judgement of the grain look in motion; **device profile of the new
  system** (all device numbers further down belong to the deleted sub-step system).
- Comment-only leftovers naming `ExtractColor` / `VerticalRowRange` / `extractionRangeY` /
  `maxCellsPerColumnPerTick` in `ExtractionGrid.cs`, `Level.cs`, `SandExtractionController.cs` (1 line)
  and `SandExtractionCube.cs` (1 line) — those files were out of scope.

### Files touched by the reset (all uncommitted)

`SandCylinderDemo/Scripts/SandCylinderSandGrid.cs`, `SandCylinderTunables.cs`,
`SandCylinderRenderer.cs`, `SandExtractionController.cs`. No prefab, scene, pattern, `ExtractionGrid`,
fill, particle or container change.

---

## Session — 2026-09-21 (publisher feedback: same-colour-first extraction flow) — RETIRED

> **RETIRED 2026-09-21 by the sand-resolution reset above.** `extractionSameColorBlocksSkip`,
> `extractionSameColorRefillFirst`, `FollowSameColorRefill`, `ResolveCaveInBias`, the wings and
> `extractionRangeY` were all deleted the same day. Kept as history only: the publisher's ask
> (bottom-up, gradual settling, no rigid plug) and the measured cause below are what motivated the reset.

Status at the time: **done in the Editor, Play-tested from BaseScene (watermelon level). No commit.**

Publisher: range may stay high, but sand must go bottom-up, upper layers must settle gradually
instead of dropping straight down, and sand may only be skipped when it is a DIFFERENT colour.

- **Cause found (measured):** a one-cell extraction hole always climbs straight up its own column
  (`StepCell` tests "fall" before "slide"; priming + 24 sub-steps close it the same frame), so
  everything above the drained block sinks as a rigid plug. Draining a colour that lies UNDER another
  (green rind under red) punched the red through to the floor in H and left the rind as two short
  stubs with vertical faces; the 45° wings then shaved those to the diagonal. Extraction stalled at
  581 / 2454.
- **Two flags in `SandCylinderTunables`, both default false (demo scenes unchanged), both ON in
  `Level.prefab`.** `StepCell`, `Step`, sub-steps, particles, materials, `ExtractionGrid.cs`,
  `SandExtractionController.cs` untouched.
  - `extractionSameColorBlocksSkip` — a matching run that continues from BELOW a column's floor is
    not extractable (only ever true in the wings; H's floor is row 0). Matching sand resting on a
    different colour is still taken through it. Same rule mirrored in `HasReachableColor`.
  - `extractionSameColorRefillFirst` — `SandCylinderSandGrid.FollowSameColorRefill`: the hole is
    walked upward through sand of the extracted colour (straight-up cell first, else a settled
    diagonal neighbour, `extractionCaveInLeftBias` on ties) before `ResolveCaveInBias`/gravity get it.
    Only legal moves, only their order changes. Sideways travel per hole ≤ window height in rows
    (15 at `extractionRangeY` 0.35 — same reach as the wings), so `extractionRangeY` is the dial.
- **Result:** rind case — layer thins as a smooth 45° V, red settles into it gradually, no plug and
  no vertical face at any point; stubs end in natural 45° slopes. Red-through-green case — wide
  funnel instead of a narrow yellow chimney.
- **Side effect to judge:** one parked shape now collects MORE (red 2x2: 4222 vs 2144 cells before
  stalling). Levels tuned around colour residue (demo L2/L4) need a re-check.
- **Still open:** at the outer edge of the refill reach a steep face can remain (non-target sand
  still falls as a plug there — holes do not travel sideways through other colours; changing that
  would be a physics change, not done). Sand resting above the band ceiling on non-target sand is
  still unreachable (that is `extractionRangeY`, by design). Not tested on device.
- `Level.prefab` currently has `extractionRangeY: 0.35` (the 2026-09-17 note below says 0.7).
- Before/after grid dumps: `Captures/sandflow_old_0394.png` vs `sandflow_new_0203/0603.png`,
  `sandflow_red_old_2144.png` vs `sandflow_red_new_4222.png` (folder is git-ignored).

---

## Resume here — open work as of 2026-09-17

Nothing from 2026-09-16 or 2026-09-17 is committed; all changes are in the working tree on purpose.

1. **`_pourOriginCells` edge case (`ShapeSandFill.cs`)** — at runtime the field was observed as an
   EMPTY array (not null) on every Container. `recomputeOrigin` only null-checks it, so a second
   `configure` call throws `IndexOutOfRange` on `_origins[0]`. Same code as HEAD; the normal game
   calls `configure` once, so it does not show today. Fix = also require `Length > 0` (matches the
   field's own "null/empty = centroid" comment). Not fixed — out of scope for the shading work.
2. **Win/Lose leftovers** — `UIRestart` stays visible on the Lose screen; revive does not resume
   music; `LEVEL_FAILED` has no listeners.
3. **Demo level 3 (Ladybug)** — created but NOT verified end to end. Play it (by hand or with the
   solver approach used for L2/L4) and confirm it completes; watch the BLUE_LIGHT T4 parking.
4. **Demo levels feel** — L1 (25 s) and L2 v2 / L4 v2 complete, but L2/L4 needed ~23–29 automated
   moves with parks because of colour residue. Hand-play the demo order 1→4 and judge the flow.
   **Re-check under mouth-only extraction** (2026-09-21 reset): all four are physically playable but
   an idealised player needs 15–26 re-arrangements; Demo 2 is marginal on the timer — see the
   validation table at the top.
5. **Device checks** — selected-shape render order, a real touch drag (all tests so far used
   reflection-driven drags), and how readable the Z-only sand fill + edge shading are on a phone
   (the game camera is nearly front-on, so 25 % vs 100 % reads mostly through wall exposure).
   Edge darken in the asset is now **0.258** (user-set; tested value was 0.10) — confirm it still
   reads as depth, not as a border.
6. **Real-input fill test** — the new fill was verified on real L4/T4/U5/Plus5/Z4 and 1x1 shapes,
   but 2x2 and the multi-cell masks were partly measured by re-configuring a live `ShapeSandFill`;
   nothing was filled by an actual drag + extraction yet.
7. **`ShapeCavityFloor` square corners** — its floor quad is still square, so a small dark triangle
   shows past the rounded FBX corner (independent of fill). Same rounded-mask idea would fix it.
   Not done (the fill work was scoped to `ShapeSandFill.cs`).
8. **AudioManager / success popup NullReferenceExceptions** — seen in the Console during a
   2026-09-17 Play run: `AudioManager.Update` (line 57) and `AudioManager.generateAudioSourceIfNeeded`
   via `GameSuccessPopup.show` ← `Level.Update` (level complete). No `ShapeSandFill` frame in any
   trace — **not related to the sand-fill changes**. Why the level completed in that run was not
   investigated. Separately, recompiling while the Editor is in Play produced NREs once; stop Play
   before compiling.
9. **Save state** — not re-checked on 2026-09-17. Today's Play runs loaded a 17 × 1x1 level and later
   a 5-shape level (T4, L4, U5, Plus5, Z4), and one run reached level complete. Check `DataSO.level`
   before the next Play test.
10. ~~**Publisher feedback leftovers** — sand-pour particles still aim at the centroid `PourTarget`
    (not at the fill surface); decide whether that needs a pass now that the fill has no pile.~~
    **Resolved 2026-09-22:** grains no longer aim at `PourTarget`; they fall straight down from the
    removed cells (see the 2026-09-22 section at the top).

---

## Session — 2026-09-17 (publisher feedback)

Status: **done in the Editor, Play-tested from BaseScene. No commit.**

### 45° extraction coverage — RETIRED (deleted by the 2026-09-21 sand-resolution reset)
> The wings, `extractionDiagonalSpread`, `DiagonalColumnFloor`, `ExtractColor` and
> `HasReachableColor` no longer exist; extraction is mouth-only. History only:
- `SandCylinderTunables.extractionDiagonalSpread` (default false, so the demo scenes are unchanged),
  set to **on** in `Level.prefab`.
- Coverage = the vertical window H = [xStart,xEnd)×[yStart,yEnd) plus 45° wings A: a column `d`
  cells outside H is reachable from row `yStart + d - 1`. Never above `yEnd`, never past the grid X
  edges. The geometry lives only in `SandCylinderSandGrid.DiagonalColumnFloor`, shared by
  `ExtractColor` and `HasReachableColor`. `ExtractionGrid.cs` untouched.
- User decisions: no spread cap; `extractionRangeY` stays 0.7 (29-row band, wings ≈ 0.85 block per
  side); no H-over-A priority on the same row.
- Verified: `diagonal=false` identical to the old algorithm (300 random trials); still 1600/s per
  point; sand conserved. Side effect: a 2x2 now drains the uncovered middle block (≈76 % of sampled
  cells in the test). If reach feels greedy, the dial is `extractionRangeY` or a future cap — do
  not change the geometry without asking.

### Shape sand fill — Z-only progress — DONE
- Publisher "bottom-up" = from the cavity FLOOR toward the camera along Z, **not** a level rising in
  screen Y. (A Y-height fill was built first and rejected; don't reintroduce it.)
- The radial centroid heap is gone. The picture is a uniform layer over the whole mask: no level
  line, no origin, rotation-invariant. Progress is shown only by `applyDepth` (Z `-0.08` → `-0.44`,
  linear in fill), which was already there and is unchanged.
- Below `COVER_FILL = 0.12` grains are hash-scattered evenly over the mask so the layer does not pop
  in; solid above. Brightness gain `DEEP_GAIN 1.00` → `RIM_GAIN 1.25` with fill. Grain
  (`GRAIN_NOISE 0.14`), lerp (`FILL_SHARPNESS 7`), `REBUILD_EPSILON` and `PourTarget` unchanged.
- Verified on 1x1, 2x2, L4, L4 r90, T4, T4 r90 at 5/25/50/75/100 %: quad Z exactly the lerp;
  coverage 100 % from 25 %; screen-lower vs upper half identical (no Y gradient); per-cell coverage
  equal; nothing drawn outside the mask. Real `Update` path (setFillPercent → lerp) checked too.

### Rounded-corner fill mask — DONE
- The rounding is only in the FBX meshes (no code radius). Measured: every **convex** outer corner
  (both orthogonal neighbours empty) has radius **0.080** at the authored 0.85 cell, identical on
  1x1/2x2/L4/T4; straight edges sit on the cell boundary. Concave joins are filleted outward (into
  the empty cell), so a cell-bounded mask never crosses them.
- FBX meshes are **not readable** in builds (`isReadable=False`) — never derive the mask from the
  mesh at runtime.
- `ShapeSandFill` builds a per-texel `_texelMask` with those arcs (`CORNER_RADIUS_CELLS = 0.080/0.85`);
  a texel is kept only if it is wholly inside.
- Verified against the real mesh triangles (1x1/2x2/L4/T4 prefabs): old square mask overflowed
  28/28/35/42 texels, new mask **0**, and 0 texels drawn past the silhouette at 1/25/100 %; the new
  mask equals the set of texels fully inside the silhouette (no over-cut). Before/after corner
  renders from the game-camera angle: the yellow wedge at 25/100 % and the sliver at 1 % are gone.

### 2D edge shading (depth feel) — DONE
- Soft XY shading: centre = shape colour, very slightly darker toward the walls. Not an outline.
- Distance map: 8SSEDT (two-pass vector propagation) on `_texelMask` — real silhouette, rounded
  corners and L/T/U outlines; internal cell joins are not edges. Built once per `configure`
  (0–2 ms), stored as a darken-independent `_edgeFalloff`; `_edgeShade = 1 - edgeDarken * falloff`.
- `shade = 1 - EDGE_DARKEN * (1 - smoothstep((d - WALL_INSET_CELLS) / FALLOFF_CELLS))`
- **Final values:** `EDGE_DARKEN = 0.10`, `WALL_INSET_CELLS = 0.110 / 0.85`, `FALLOFF_CELLS = 0.45`.
  (EDGE_DARKEN is now the GameplayTunables knob below; code default 0.10, asset currently 0.258.)
- Wall inset measured from the meshes: outer edge ±0.425, inner opening ±0.315 → **0.110** on 1x1,
  2x2, L4, T4 and U5 (also at the concave joins). The sand under the walls is never visible, so the
  falloff starts at the inner wall line.
- Colour order: shade is multiplied **after** the gain × grain clamp. Before the clamp, `RIM_GAIN`
  pushes bright channels (yellow R) past 255 and the gradient would be clipped away.
- **Shading tests passed:** distance map = brute-force Euclidean (max error 0); centre 0.989–0.994
  (2x2 middle 1.000); wall line 0.90 easing 0.905 → 0.935 → 0.964 → 0.989; all four rounded
  corners ease the same; L/T internal joins 0.989–0.994 (no false arm/centre darkening); real
  L4 25 %, T4 50 %, U5 75 %, Plus5 100 %, Z4 100 % (temporarily yellow): coverage 100 %, Z unchanged,
  edge/centre ≈ 0.906 on every colour including yellow; game-camera renders read as depth, no border.

### `sandFillEdgeDarken` in GameplayTunables — DONE
- `GameplayTunables.sandFillEdgeDarken` (header "Shape Sand Fill", default
  `DEFAULT_SAND_FILL_EDGE_DARKEN = 0.10`, Range 0–0.3). `WALL_INSET_CELLS` / `FALLOFF_CELLS` stay
  constants in `ShapeSandFill`.
- Plumbing: `Container._tuning` → `Shape.setSandFillSource(unlit, colour, tuning)` (optional param)
  → `ShapeSandFill.setTuning`. `Update` re-reads the value and, on change, rebuilds only
  `_edgeShade` from `_edgeFalloff` and redraws once — live in Play.
- **Current asset value: `_sandFillEdgeDarken: 0.258`** — set by the user in the Inspector at the end
  of the session and saved in `Data/Tuning/GameplayTunables.asset` (line 29). This is what the Level
  uses now. The code default (`DEFAULT_SAND_FILL_EDGE_DARKEN`) is still 0.10, and all shading tests
  above ran at 0.10. 0.258 has not been re-measured or checked for "reads as outline" — look at it
  on device.
- Verified: at 0.10 `_edgeShade` is bit-identical to the old constant formula on all 5 shapes; set
  to 0.25 in Play → min shade 0.750 and L4 edge/centre 0.754 on the next frame; back to 0.10 →
  identical again. (Changed through the field by reflection, not the Inspector slider itself.)

### Files touched on 2026-09-17 (all uncommitted)
- 45° extraction: `SandCylinderSandGrid.cs`, `SandCylinderTunables.cs`,
  `SandExtractionController.cs`, `Level.prefab`.
- Sand fill: `ShapeSandFill.cs`; tunable plumbing: `GameplayTunables.cs`, `Container.cs` (1 line),
  `Shape.cs` (optional parameter + `setTuning` call); `GameplayTunables.asset` (`_sandFillEdgeDarken:
  0.258`, by the user in the Inspector).
- Docs: this file; `Docs/handoff_shape_system.md` §0.12 got a "superseded in part" note.
- Also modified in the working tree, not by the sand-fill work: `Data/Levels/SandSort_FillTest_5Shapes_Level.asset`
  — most likely the 5-shape level (T4, L4, U5, Plus5, Z4) used in today's Play tests. Check what changed before
  committing. `0_Temp_Level_List.asset` and `MechanicUnlocks.asset` were already modified at the start of the day.

---

## Previous resume list — open work as of 2026-09-16 (kept for reference; merged above)

Nothing from 2026-09-16 is committed; all changes below are in the working tree on purpose.

1. **Demo level 3 (Ladybug)** — created but NOT verified end to end. Play it (by hand or with the
   solver approach used for L2/L4) and confirm it completes; watch the BLUE_LIGHT T4 parking.
2. **Demo levels feel** — L1 (25 s) and L2 v2 / L4 v2 complete, but L2/L4 needed ~23–29 automated
   moves with parks because of colour residue. Hand-play the demo order 1→4 and judge the flow.
3. **Device checks** — selected-shape render order and a real touch drag (all tests so far used
   reflection-driven drags).
4. **Win/Lose leftovers** — `UIRestart` stays visible on fail; revive does not resume music;
   `LEVEL_FAILED` has no listeners.
5. **Save state** — `DataSO.level` is 1, so the next Play starts Demo 1 (list index 1).

---

## Game UI session — 2026-09-16 (session note)

Status: **done in the Editor, Play-tested from BaseScene, no new Console errors. No commit.**

### Selected shape render order — DONE
- The held shape draws **Normal shapes → Selected Shape Body → Selected Shape Outline**,
  with its transform untouched (no X/Y/Z change).
- New layer `SelectedShape` (6) in `ProjectSettings/TagManager.asset`.
- `Assets/Settings/Moow_Renderer.asset`, all at `AfterRenderingOpaques`, in this order:
  1. `SelectedShapeDepth` — layer SelectedShape, `DepthOnly`, ZTest Always + write.
  2. `SelectedShapeBody` — layer SelectedShape, `UniversalForward` / `UniversalForwardOnly` /
     `SRPDefaultUnlit`, own materials, ZTest LEqual + write, stencil Always/Keep.
  3. Existing Outline `RenderObjects` — now ZTest Always, no depth write (the
     `ShapeSelectedOutlineMask` stencil still keeps it outside the piece).
- `Container.setSelectedRenderLayer` moves the shape's renderer GameObjects to the layer on press
  and restores their original layers on release and on seal.
- `selectedShapeZOffset` / `selectionLift` removed from code, `GameplayTunables` and its asset.

### Restart button — DONE
- `UIRestart`: hidden and inactive on level open and on every `LEVEL_LOADED` (first load,
  restart, next level). Shows and becomes interactable 5 s (`_showDelayAfterFirstDrag`) after the
  level's **first drag**, not after load. Click behaviour unchanged. A pending show is cancelled on
  win (`LEVEL_OBJECTIVE_COMPLETE`).
- New event `Events.LEVEL_FIRST_DRAG`, dispatched once per Level from `Level.tickTimer` when the
  timer starts.

### Not done yet
- Verify the selected-shape render order (RenderObjects depth/stencil overrides + mask stencil) on
  a real device.
- Short test with a real touch drag. Play tests drove the drag through `Container.beginDrag`.

### Win flow step 1 — DONE (Play-tested from BaseScene)
- `Level.resolveWin` sets `_resolved` at once (timer and lose check stop) and `_winPending`; it
  no longer sends `LEVEL_COMPLETED` (that one stays MoowCore's "going to the next level" event).
- `Container.isCompleteExitFinished` turns true at the real end of the complete exit (after the
  StarExplosion stops, or right away if there is no effect prefab), just before `SetActive(false)`.
- `Level.Update` sends `LEVEL_OBJECTIVE_COMPLETE` once, when every Container reports that. No timer
  or estimated duration is involved. `GameMenuCanvasUI` opens the success popup on that event.
- Verified on the tutorial level (2 containers, driven by `forceComplete`, timer set to 0.5 s):
  last seal f3092 → scale-out ends and effect starts f3111 → effect ends f3232 → event + popup
  f3233. The event came exactly once; no `FAIL_CONDITION_MET` or `LEVEL_COMPLETED`; time left
  stayed at 0.17 s the whole time. No new Console errors.

### Win/Lose flow — still open
- ~~Input is not locked after win/lose~~ — DONE: `Level.setInputLocked` → `Container.setInputLocked`
  (lock in `resolveWin`/`resolveLose`, unlock in `onReviveClicked`). A drag in progress is released
  via `endDrag`; only `pollPointer` is skipped, `updateVisual` still runs. Restart/next level builds
  unlocked Containers. Play-tested (drag via reflection; a real touch was not simulated).
- ~~Extraction continues behind the fail popup~~ — DONE: `Container.isInputLocked` (read-only) +
  `ExtractionGrid.Update` returns while it is set. Lose freezes fill and sand count at once; revive
  resumes at the same rate (accumulators untouched). Play-tested on the tutorial level: ORANGE set to
  cap-500 while extracting ~50-70/frame, lost the same frame; 150 frames with no fill change and no
  seal; after revive it filled again from the next frame, sealed at +10 f, and the win event came
  169 frames later (after the exit), not on the revive frame. Restart: fresh unlocked Containers,
  extraction works.
- ~~Settings popup does not block drag or extraction~~ — DONE (`Level.cs` only):
  `onSettingsOpened`/`onSettingsClosed` set `_timerPaused` and call `refreshInputLock()`, which
  locks from `_resolved || _timerPaused`; `onReviveClicked` uses it too. Play-tested: Settings
  opened mid-drag releases the drag, fill/sand/time freeze for 60 f and resume on close; on a lost
  level Settings open/close keeps the lock; revive behind Settings stays locked with time frozen
  until close; Settings during win pending keeps the lock and the win event came once; restart is
  normal. Not covered: a restart/next level while Settings is open (new Level starts unpaused).
- ~~`_showFailedTween` not killed on `LEVEL_LOADED`~~ — DONE: `GameMenuCanvasUI` kills it in
  `onLevelLoaded` and before creating a new one in `onLevelFailed`. Play-tested: normal lose still
  shows the popup ~0.25 s later; a retry 0.083 s after the lose kills the pending show and the new
  level stays popup-free and playable; a second `FAIL_CONDITION_MET` kills the first tween and the
  popup opens once. Music could not be checked (the music source has no clip in the Editor).
- Also noted: `UIRestart` stays visible on fail; revive does not resume music; `LEVEL_FAILED`
  has no listeners.

### Level complete reward + coin burst — DONE (Play-tested from BaseScene)
- `GameSuccessPopup.claimReward(value)`: one claim per shown popup (`_rewardClaimed`, reset in
  `show`). Continue and the rewarded-ad `Rewarded` branch both use it; it disables both buttons, sends
  `GIVE_COIN_ANIMATION`, calls `InventoryManager.increase` right away (the 0.1 s DelayedCall is gone),
  then `UI_NEXT_LEVEL_CLICK`. `onDoubleClaimClick` returns once claimed.
- `CoinCollectController`: coins pop out around the screen centre and fly into
  `UIGoldContainer.iconTarget` (~1.46 s for 20 coins); each landing sends
  `UI_GOLD_ANIMATION_PROGRESS` with an integer share and punches the icon. The old coinTop counter is
  never shown. Hidden per coin: the sprite-less child Image (white square) and `Sparks` (its
  glow1_ADD / Particles/Standard Unlit material draws black squares on the UI canvas under URP).
- `UIGoldContainer`: holds back gold announced by `GIVE_COIN_ANIMATION` (`_pendingAnimated`) so
  `MONEY_CHANGED` no longer jumps ahead of the coins; the counter reaches the saved money on the last coin.
- Verified: double Continue + late claimReward + Double after claim → +50 once, one level advance;
  Double (ads primary set to the stub at runtime) + second Double + Continue + claimReward(999) →
  +70 once. Save values restored after the test. In the Editor `AdsService` has no provider, so
  rewarded ads return Failed.

---

## Demo levels 1–4 (5×6) — created 2026-09-16, not committed

`_levels` = `[SandBuckets2_5x4, Demo1_YellowDuck, Demo2_Watermelon, Demo3_Ladybug, Demo4_Clownfish]`.
PNGs in `Data/SandPatterns/SandSort_Demo*_5x6.png` (170×170, 100 % filled), levels in
`Data/Levels/SandSort_Demo*_5x6_Level.asset` (board 5×6, timer 240 s).

| Level | Colours | Shapes (start) | Play result |
|---|---|---|---|
| 1 Yellow Duck | YELLOW, BLUE | 2x2 (0,0), 3x1 (2,0) | completes, 25 s (v1, unchanged) |
| 2 Watermelon (v2) | RED, GREEN_DARK, YELLOW | 2x2 (0,2), T4 (0,0), L3 (3,0) | completes, 32 s, 23 moves / 7 parks |
| 3 Ladybug | RED, BROWN_DARK, GREEN, BLUE_LIGHT | 2x2 (0,0), 2x1 r90 (2,0), L3 r90 (3,0), T4 (1,2) | PNG ↔ grid OK, no start overlap; **NOT verified end to end** (untouched since v1) |
| 4 Clownfish (v2) | ORANGE, WHITE, BLUE, YELLOW | U5 (0,0), 2x1 (0,2), S4 r90 (3,0), 2x1 (3,3) | completes, ~48 s |

Learned (don't re-derive):
- Every colour leaves 20–120 cells of residue on top of neighbouring colours; a shape only reaches
  100 % after the colour under that residue is drained, so levels need back-and-forth by design.
- Two 3-wide shapes can never pass each other on a 5-wide board. If the upper one cannot reach
  100 % on its own, the level deadlocks (v1 of L2 and L4). v2 uses 2-wide shapes for RED (L2) and
  BLUE (L4), and L4's eye is WHITE instead of an enclosed BLUE.
- Extraction here runs ~1500–4000 cells/s per shape; one colour drains in 3–12 s.
- Test harness note: calling `dragTo` by reflection on a container that sealed mid-drag re-registers
  it on the Board (ghost occupancy). Real input cannot do this (sealed Update returns early).

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

## Sand flow speed + horizontal hanging rows — SUPERSEDED (2026-09-15 work, deleted 2026-09-21)

> **SUPERSEDED by the sand-resolution reset at the top of this file.** `StepCell`, `Step`,
> `PrimeColumnFalling`, `RunActiveSubSteps` and every tunable listed under *Current sand tuning*
> below (except `sandSimulationSpeed`, `colorNoiseAmount`, `blockCellSize`) were deleted. The
> "straight 45° settled V is accepted" decision below is also superseded: the rest surface is now
> irregular by `restFriction` 0.5. Still true and worth keeping from this section: the
> `EditorApplication.update` rate pitfall, the `_testLevel` Editor-only note and the screenshot note
> under *Notes for the next session*. Everything else is history.

Status at the time: **in code, verified in Play from BaseScene, accepted.** Only open item
was device cost (see *Open* below). No commit.

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

- ~~**Device profiling (IL2CPP).**~~ **Closed 2026-09-16** — profiled on a
  Xiaomi Mi 9T and fixed in two stages; see *Sand performance on device* below.
  The Editor estimate (~6.5 ms Canyon / ~2 ms Buckets) understated the device by
  more than an order of magnitude at peak.

### Notes for the next session

- Measurement harness pitfall: `EditorApplication.update` fires ~5.7× per player
  frame (346/s vs 60 fps). Drive per-frame prototype work from
  `PlayerLoop` injection (PreUpdate / PostLateUpdate) or gate on
  `Time.frameCount`; measure what is drawn at the start of PostLateUpdate.
  Remove injected systems afterwards.
- **`_testLevel` is Editor-only** (`LevelListSO.get` wraps it in
  `#if UNITY_EDITOR`), so a player build ignores it and loads `_levels` /
  `_repeatingLevels` instead — a device build pointed only via `_testLevel`
  fails with *"Assigned LevelSO is not a SandLevelSO"*. For device tests
  repoint `_levels[0]` **and** `_repeatingLevels[0]`, and restore afterwards.
  On disk as of 2026-09-16: `_levels[0]` / `_repeatingLevels[0]` =
  `SandSort_Test_Level`, `_testLevel` = `SandSort_SandBuckets2_5x4_Level`
  (restored and verified byte-identical after the device runs).
- MCP `manage_camera` screenshots land in `Assets/Screenshots/` (creates
  `.meta`); move them out / delete after use.
- ~~`CLAUDE.md` *Current work status* still says the sand simulation "has not been
  modified"~~ — fixed 2026-09-21: the paragraph now describes the sand-resolution reset.

---

## Sand performance on device — Stage 1 + Stage 2 (2026-09-16) — PARTLY SUPERSEDED

> **2026-09-21:** the simulation these numbers measured was deleted by the sand-resolution reset.
> What survives: **Stage 1, the renderer change gate** (unchanged, still in `SandCylinderRenderer`)
> and the *idea* of Stage 2 — `maxCellsPerFrame` is now the work ceiling of the new `LateUpdate`
> scheduler (passes × awake area). Obsolete: every sub-step figure, the calibration table as a guide
> for the new system, and both Stage-3 candidates (the new grid goes to sleep the pass after the last
> move and has no full-grid diff scan). **The new system has not been profiled on a device yet** —
> only an Editor worst case of ~0.6 ms per full-width pass on 170×240. History below.

Status at the time: **in code, measured on a Xiaomi Mi 9T (60 Hz, IL2CPP development
build), accepted for the 30 FPS target. No commit.** Files touched:
`SandCylinderSandGrid.cs`, `SandCylinderRenderer.cs`,
`SandCylinderTunables.cs`. `Level.prefab` and every existing tuning value are
untouched (`activeSubStepsPerTick` still 24, `sandSimulationSpeed` 45,
`colorNoiseAmount` 0.2). StepCell, extraction, the active-region logic and the
`Step()` catch-up were not changed.

### The two problems found on device

1. **The renderer redrew every frame even when nothing moved.**
   `SandCylinderRenderer.LateUpdate` rebuilt and re-uploaded the whole texture
   unconditionally: **11.3 ms/frame on Canyon while the sand was static**, 42 %
   of a 27 ms idle frame.
2. **The sub-step pass count fed back on itself.** Passes came from
   `Time.deltaTime × activeSubStepsPerTick × sandSimulationSpeed`, so a slow
   frame bought the *next* frame more passes. Measured escalation on Canyon:
   `sub` 0.40 → 36.3 → 71.9 → 106.7 → **157.9 ms** with the frame at 202 ms; a
   second dig peaked at **233.8 ms sub-step / 281 ms frame**. `GC.Alloc` was
   0.002 ms, so this is the loop, not GC. The only brake was the
   `activeSubStepsPerTick × 8` = 192-pass cap, ≈12× a frame's budget.

### Stage 1 — renderer change gate

Monotonic `cellsVersion` in `SandCylinderSandGrid.SetCell` (the only
`cells[...] =` in the file, so every mutation path bumps it for free; `Resize`
bumps it separately because it replaces the array). The renderer records the
version, `colorNoiseAmount` and the `sandColors` palette of its last completed
draw and returns early when all still match. `DefaultExecutionOrder(100)` plus
LateUpdate-only means the renderer is the last thing in the frame to touch the
sand, so no frame that needs a redraw can be skipped.

Result: Canyon idle renderer **11.281 → 0.004 ms** (0 of 965 frames did work),
Buckets extraction 3.68 → 0.39 ms, no visual regression. **FPS did not move**
(37.1 → 36.8): the freed time went into `Gfx.WaitForPresentOnGfxThread`
(2.9 → 9.9 ms) because the frame becomes present/vsync-bound once the CPU drops
near the deadline. Real CPU work per idle Canyon frame fell ~24 → ~18 ms. The
gate buys headroom a sub-step budget can spend; it is not an FPS win by itself.

### Stage 2 — deterministic cell budget

New tunable `maxCellsPerFrame` (default 400000, **a starting value, not final
tuning**). In `RunActiveSubSteps`, at the first point where the active area is
known, work is bounded by the *product* rather than the pass count:

```
passes = min(rate target, maxCellsPerFrame / activeArea)   // floored at 1
```

Integer arithmetic only — no stopwatch, no wall-clock, determinism preserved.
The floor of 1 is deliberate: extraction opens its holes in Update and the
renderer draws at order 100, so the region must get at least one pass per frame
or the P1 hanging-row fix regresses. The P1 frame order is unchanged:
Step → extraction → sub-steps → renderer. Clamp observed live: Buckets 13
passes vs a 27-pass rate target, Canyon 4 vs 27.

### Measured, P1 vs Stage 2 (Mi 9T)

Canyon, 36,839 logged frames across 5 windows and ~10 real digs:

| Metric | P1 | **Stage 2** |
|---|---|---|
| Peak sub-step | **233.8 ms** | **14.60 ms** |
| Sub-step during motion | escalating | p50 7.83 / p90 9.67 / p99 12.23 ms |
| Frames with sub-step ≥100 ms | yes, 2 independent digs | **0** |
| Frames with sub-step ≥20 ms | yes | **0** |
| Peak frame | **281 ms** | 68.3 ms |
| Idle | 27.8 ms / 36.0 FPS | unchanged (Stage 2 only acts during motion) |
| Motion frames | — | 29.3 ms / 34.2 FPS |

The only two frames ≥100 ms in the whole run were **level loads**
(`sub` ≈ 0.1 ms, `upd` = 0, renderer doing its first full draw) — not
sub-steps. The tight p50–p99 spread is the signature of a hard ceiling.

SandBuckets, ORANGE then YELLOW, same order as the P1 measurement:

| Metric | P1 (Editor) | **Stage 2 (device)** |
|---|---|---|
| YELLOW t90 | 5.3 s | **5.68 s** |
| YELLOW settle | 7.5 s | **9.26 s** (video) / **9.36 s** (profiler) |
| Sub-step during flow | 10.75 ms sustained, 16.35 ms peak | 7.31 ms avg, 15.32 ms peak |
| Flow FPS | — | 24.4 ms / 41.0 FPS |

Two independent instruments agreed on the flow length to within 0.1 s. The
~5.3 s / 7.5 s flow character is preserved; it is **not** pushed toward the
no-sub-step regime (26 s flow / >45 s settle).

Horizontal hanging rows, measured from lossless device frames:

| Metric | Canyon E0 | Canyon P1 | **Canyon S2** | Buckets E0 | Buckets P1 | **Buckets S2** |
|---|---|---|---|---|---|---|
| Frames with a 2+ cell row | 12.5 % | 0.1 % | **0 / 26** | 37.0 % | 11.6 % | **18 % (2/11)** |
| Longest run (cells) | 8 | 2 | **1.2** | 14 | 5 | **4.0** |
| Runs in extraction band | 437 | 0 | **0** | 583 | 9 | **0** |

Both levels sit in the P1 regime, not E0. The two metrics that do not depend on
sampling luck — longest run and extraction-band runs — are at or better than P1
on both levels. The Buckets frame percentage is a 2-of-11 point estimate whose
confidence interval spans P1 comfortably; it cannot resolve a difference at the
11 % level.

### Device calibration (use this to tune `maxCellsPerFrame`)

**~55,000 StepCell evaluations per millisecond** on a Mi 9T, plus ~0.6 ms fixed
cost for the full-grid diff scan. So `maxCellsPerFrame ≈ 55000 × target_ms`:
400000 → ~7.8 ms (current), ~165000 for a 3 ms budget, ~800000 if 15 ms is
acceptable. 30 FPS is met at 400000 with room to spare; **60 FPS is not
reachable by tuning this value alone** — during motion the sand alone costs
sub 7.8 + renderer 7.6 + Step 2.3 ≈ 17.7 ms.

### Two Stage-3 candidates this exposed (not started)

- The active region keeps burning a full budget for
  `activeSubStepWindowSeconds` (0.5 s) *after* the sand has stopped: 923 frames
  × 7.88 ms = **24.6 s of passes with zero visual effect** in one session.
- The full-grid diff scan in `RunActiveSubSteps` costs **0.59 ms (Canyon) /
  0.21 ms (Buckets) every idle frame**, whether or not anything changed.

### Measurement toolchain (reusable, no code changes needed)

- Remote `UnityEditorInternal.ProfilerDriver` over WiFi:
  `GetAvailableProfilers()` → `AndroidPlayer(...)`, then
  `GetRawFrameDataView(frame, 0)` summed per `GetSampleName`. Automatic
  MonoBehaviour markers (`Assembly-CSharp.dll!::SandCylinderSandGrid.LateUpdate()
  [Invoke]`) isolate sub-steps from `Step()` with no instrumentation. Profiler
  overhead is negligible (23.84 ms attached vs 23.92 ms detached).
- `EditorApplication.update` + `SessionState` writing a per-frame CSV survives
  the ~2000-frame ring buffer, which otherwise rolls a peak off in ~54 s.
- **Stage 1's gate doubles as a free motion detector**: the renderer does work
  exactly on the frames where cells changed, which is how flow and settle times
  above were measured.
- `dumpsys SurfaceFlinger --latency` present timestamps for profiler-independent
  FPS. `dumpsys gfxinfo` is useless (Unity GameActivity renders on its own GL
  surface). GPU time is unreadable on this GL ES 3.2 path — use
  `Gfx.WaitForPresentOnGfxThread` as a vsync-bound proxy.
- MIUI blocks every touch-injection path (`input`, `monkey`, raw `sendevent`),
  so device playtests need the user's own fingers. Launch with
  `am start -n com.moowgames.sandsort/com.unity3d.player.UnityPlayerGameActivity`
  — `monkey` pauses/resumes the activity, which blanks the sand texture and
  removes Containers (a separate bug, still unfixed).
- Grid sizes for pixel analysis: Canyon 306×306, Buckets 170×170 (from the
  pattern PNGs).
- **`screenrecord` h264 is not trustworthy for hanging-row counts** on this
  content: it reported 25.4 % of frames and a 16.3-cell longest run on Buckets,
  where lossless `screencap` on the same regime gave 18 % and 4.0 cells. It also
  turns UI slide animations into "runs" hundreds of cells long. Use raw
  `adb exec-out screencap` (~1.0 s/frame, 16-byte header then RGBA) and drop any
  run ≥20 cells as a UI edge. Validate any such detector with a positive control
  (paint a synthetic bar) *and* on static frames before trusting a zero.

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

## 9-colour artwork — v2 delivered; **incompatible with mouth-only extraction (known, not fixed)**

> **KNOWN ISSUE 2026-09-21 — do not fix now (user decision).** v2 (`FaultScarp9`) was authored and
> verified against the OLD extraction band (16+ rows, climbing past other colours). Under the reset's
> mouth-only colour extraction it is physically drainable but not playable: almost every block's
> 2-row mouth holds several colours at once (one starts with 8), so a parked container stalls after a
> few grains — an idealised player needs 420 re-arrangements / ~1208 s against a 300 s timer. The
> "Verified in Play" figures below were measured with the old band and no longer describe the game.
> Any v3 has to keep each block's mouth colour-coherent. Details: validation table at the top.

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

### Verified in Play (BaseScene) — with the OLD extraction band, history only

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
added to the grid/renderer; then on 2026-09-21 the sand simulation and the
extraction were rebuilt from scratch — see *Sand-resolution reset* at the top.
Capacity algorithm, container matching, `ExtractionGrid`, `SandColorUtility`,
`GameplayTunables` and `EditorColorPalette` are still untouched.) No
shape-freezing or shape-preserving system exists or should be added — post-
extraction deformation is a core ASMR feature.

---

## Next up

**0. Sand-resolution reset follow-ups (see the top section) — decisions first, no work started:**
the colour-match stall under mouth-only extraction (how many re-arrangements a level may demand, and
what that means for pattern authoring), which `sandSimulationSpeed` to save in `Level.prefab` (45 now,
120 evaluated), a hand-played pass with real drags, and a device profile of the new system.
FaultScarp9 / Canyon9Colors is knowingly left broken until those are settled.

**Artwork quality, not pipeline.** The goal is a picture a level designer could
plausibly hand-draw in Aseprite — v2 is a good skeleton but still recognisably
machine-even in places. Concretely:

1. Push v2 (or a v3) closer to hand-made: less uniform feature density, more
   deliberate focal points, fewer evenly-distributed accents.
2. ~~Spread the scarce colours better across board blocks in the extraction band
   (some blocks still carry only 4 of 9).~~ **Reversed by the 2026-09-21 reset:** there is no
   band any more, and many colours in one block's mouth is exactly what stalls a container. A
   mouth-only pattern wants each block's bottom rows colour-coherent.
3. Decide whether the generator stays a throwaway prototyping aid or whether the
   authoring story is purely "paint it in Aseprite from the exported `.gpl`".
4. Then, and only then, revisit the deferred items: Checker Window, palette
   authority refactor (Faz 2), build optimisation (Faz 3).

5. Sand feel: the device profiling of 2026-09-16 measured the deleted sub-step
   system (see *Sand performance on device*, partly superseded). For the new
   system: profile it on device, then settle `maxCellsPerFrame` (400000 carried
   over unchanged) and whether 60 FPS is a goal. The two old Stage-3 candidates
   are obsolete — the new grid sleeps the pass after the last move and has no
   idle diff scan.

No commits have been made for any of this work.
