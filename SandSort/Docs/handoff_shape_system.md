# Handoff — Shape prefab system & FBX integration

Last updated 2026-09-13 at the end of the FBX-integration session. **Phase 1 is still open.** The
latest commit is `b53a3db Core Game Play Update`; **nothing from the 2026-09-13 session is
committed** — it all lives in the working tree (see 0.6).

Section 0 is the current state and the next task. Sections 1–5 are the 2026-09-12 handoff, kept for
reference and corrected where this session superseded them.

---

## 0. Current status — Shape / FBX integration (2026-09-13)

### 0.1 FBX integration — done, verified

- All **13** canonical Shape prefabs now render their real FBX from `Assets/Game/CurrentGame/FBXs/`.
- Hierarchy (identical in all 13; nothing else in the prefab changed):
  ```
  Shape_X                          <- Shape.cs
  ├── VisualRoot
  │   └── FBX_Placeholder
  │       └── Shape_X  (FBX prefab instance)  localPosition (0,0,0) · localRotation (0,180,0) · localScale (1,1,1)
  └── FillUIAnchor
      └── FillPercentageUI
  ```
- **Import settings untouched:** `globalScale 1`, `useFileScale 1`, `fileScale 1`,
  `bakeAxisConversion 0`. One cell = 0.85, thickness 0.5. No scale correction anywhere.
- **The `(0,180,0)` is a coordinate-system conversion, not a visual nudge.** Blender contract:
  shape plane XZ, thickness Y. Gameplay (verified in code, see 0.7): plane **XY**, thickness **Z**,
  camera at −Z looking +Z. As imported, the FBX root carries the importer's `(270,0,0)` and the cells
  run −X/−Z; writing `(0,180,0)` on the instance replaces that and nets a proper rotation (det +1, no
  mirror) that puts cells on +X/+Y with the top face toward −Z. Proof it is not a mirror: L4↔J4 and
  S4↔Z4 stay distinct and match canonical. **Depends on `bakeAxisConversion = 0`** — turning that on
  would make `(0,180,0)` wrong.
- **T4** was re-exported from Blender as `### / .#.` (cells `(1,0)(0,1)(1,1)(2,1)`, origin at the
  EMPTY cell (0,0)) and integrated with the same pattern — no extra Z rotation, no offset. The old T4
  FBX had been authored stem-up (the spec's mirrored cell list), not a coordinate problem.
- Material: each FBX embeds its own `M_Shape` (URP/Lit, `_BaseColor` ≈ (0.565, 0.976, 0.400) green,
  no texture, one submesh). Same for all 13.

### 0.2 B fix — Container root placement (`Board.cs`) — done, verified

- `anchorToWorldCenter`, `anchorPointToWorldCenter`, `worldToAnchorPoint` no longer add/subtract
  `shapeCenterOffset`. Contract now: `anchor ↔ anchor * cellSize` (board-local).
- Container root == world centre of the shape's cell (0,0). Everything under the root (cubes,
  VisualRoot/FBX, FillUIAnchor) was already laid out from cell (0,0); the old bbox-centre root drew
  every multi-cell shape `((W'-1)/2, (H'-1)/2)` cells off its occupancy (legacy of the root once being
  one centred primitive; present since `091cdab`).
- Signatures kept (`shape` param now unused); `shapeCenterOffset` kept, no longer called by these.
- Drag feel unchanged by construction: `grabOffset` cancels the reference point
  (`wanted = (p − p0)/cs + v0`).
- Verified in Play: cubes vs `cellToWorldCenter(grid + cell)` err 0 (10/10 containers); 0° FBX origin
  on cell (0,0); inverse check max err 2.1e-6 over 1000 anchors; grab offset exact, no jump at grab,
  +3.3-cell drag lands visual 6.300 / grid 6; release glide settles; board edge stops at x = 8;
  obstacle stops flush.

### 0.3 A fix — rotation normalization (`Shape.cs`) — done, verified

- Cause: `rotatedCells` normalizes `R^k(C)` by `shift = −min(R^k(C))`, but VisualRoot only rotated
  about cell (0,0). Visual − gameplay was `(0,−(W−1))` / `(−(W−1),−(H−1))` / `(−(H−1),0)` at
  90 / 180 / 270 (W×H = canonical bbox). Per-axis min, so shapes with an empty (0,0) are covered.
- Fix: new `Shape.normalizationShift(cells, rotation)` (same clockwise step as `rotatedCells`, which
  is **unchanged**) and `placeVisualRoot()`:
  `VisualRoot.localPosition = visual * base + shift * _cellWorldSize`, rotation unchanged.
  Called from **both** `applyRotation` and `setCellWorldSize` — `Level` calls `applyRotation` before
  `Container.initialize` hands over the real cell size. Call-order and repeat independent (verified).
- Verified in Play: T4 / L4 / S4 / U5 / Plus5 × 0/90/180/270 = **20/20** FBX cells == `occupiedCells`,
  FBX cell-centre err 0.0000, all inside the floor when flush to the top-right corner. T4 world
  offsets `(0,−2)/(−2,−1)/(−1,0)` → `(0,0)` at 90/180/270 (VisualRoot lp `(0,1.70)/(1.70,0.85)/(0.85,0)`).
  Real `Container`s at T4 180/270: `cellVisual(i)` == FBX cell centre == board cell centre. Drag,
  obstacle, board edge and release re-verified on a rotated container (T4 90°).

### 0.4 Container cubes — renderers hidden, gameplay role kept (`Container.cs`) — done, verified

- Cubes are **not** deleted. Analysis of what uses them:
  - Occupancy, collision/sweep, extraction amounts/columns: **no** (anchor maths only).
  - `cellVisual(i)`: **yes** — returns the cube Transform.
  - Sand grain target: **yes** — `ExtractionGrid.cs:150` passes `cellVisual(i)`;
    `SandExtractionParticleEffect.SpawnGrain` reads only `targetCube.position`.
  - Pointer picking: **yes** — `Container.isPointerOverThis` raycasts the cube Colliders.
- Change: in `buildShapeVisuals`, `renderer.enabled = false` **only when** the Shape's
  `FBX_Placeholder` has a Renderer. Legacy levels (no Shape prefab) still draw their cubes. The colour
  material is still assigned to the (hidden) cubes.
- Verified in Play: 43 cubes, 0 visible cube renderers, all colliders on and active; pick rays 43/43
  hit, rays through bbox holes hit nothing; extraction ran (GREEN U5 90° 0 → 840, U5 0° 0 → 883).
  Grain target checked by calling `SpawnGrain` on the hidden cubes: X lands on the target; Y scatter
  is the system's own ±15 % fall-time jitter. In-flight grains were not caught live (too short-lived
  for MCP round-trips).

### 0.5 Still open (known, deliberately not done)

1. **FBX runtime colour — next session's first task (see 0.8).** Every piece currently renders
   green; the level colour is only on the hidden cubes.
2. **Fill UI z-fighting:** `FillUIAnchor` z = −0.5 is coplanar with the FBX front face (FBX spans
   z −0.5..0). Separate task after colour.
3. Console warning on the active level: `Sand pattern is 5 block(s) wide but the board has 10
   column(s)` — the CoreLoop level's board measured **(10, 15)** this session (section 3.2 still says
   (5,15)). Level data, not touched.
4. `Captures/` (project root, untracked) holds this session's diagnostic screenshots
   (`fbx_1x1_check`, `rotation_diag_play`, `rootfix_B_play`, `rotationfix_A_play`,
   `cubes_hidden_play`). Not part of the game; delete or ignore.

### 0.6 Working-tree changes from this session (uncommitted)

| Path | Change |
|---|---|
| `Assets/Game/CurrentGame/FBXs/` | **Untracked.** 13 `Shape_*.fbx` + `Board_Canvas/Cell/Frame.fbx` (+ .meta) |
| `Assets/Game/CurrentGame/Prefabs/Shapes/*.prefab` (13) | FBX instance added under `VisualRoot/FBX_Placeholder` |
| `Assets/Game/CurrentGame/Scripts/Controller/Board.cs` | B fix (three helpers) |
| `Assets/Game/CurrentGame/Scripts/Controller/Shape.cs` | A fix (`normalizationShift`, `placeVisualRoot`) |
| `Assets/Game/CurrentGame/Scripts/Controller/Container.cs` | Cube `MeshRenderer` disabled when an FBX draws the shape |

### 0.7 Coordinate system (verified from code, keep)

- Gameplay plane **XY**: `Board.cellToLocalPosition` = `(x*cs, y*cs, 0)`; sand quad identity in XY;
  gravity/sand bottom along −Y.
- Thickness **Z**: cubes z −0.25..0.25, floor at z +0.2 (behind), Fill UI z −0.5 (front); drag plane
  normal = `board.forward`.
- Camera: `Level.setupCamera` forces orthographic, rotation identity, `z = bounds.min.z − 10`,
  re-asserted every `Update`. GameScene's authored `PerspectiveCamera` (0,20,0, 70° X) is the old
  Pixel Loop Blast top-down setup and is overridden at runtime.
- Rotation about Z, clockwise, 90° steps; the Shape root is never rotated.

### 0.8 Next session — FIRST TASK: FBX runtime colour

**Problem:** all FBX renderers use the embedded green `M_Shape`; with the cube renderers hidden the
Container's real `ColorSO` colour is no longer visible.

**Goal:**
- The FBX shows its Container's `ColorSO` colour at runtime.
- Do **not** modify the `M_Shape` material asset(s); recolouring one Container must not affect any
  other Shape.
- Prefer `MaterialPropertyBlock` (or the equivalent that fits the existing render architecture — the
  project copies serialized materials and never uses `Shader.Find`, see `SandCylinderRenderer`'s
  Android note and `Board.buildFloorVisual`).
- Container cube `MeshRenderer`s stay disabled.

**Before changing anything, analyse and confirm the current pipeline:**
`ColorSO → Level (containerMaterialOf(data.color)) → Container.initialize(colorMaterial, fallbackColor)
→ cube renderers today / FBX Renderer (target) → Material`. Known starting facts: `Level.buildContainers`
resolves `colorMaterial` from the colour's ColorSO (MoowCore `Basic_Colors`) and falls back to the sand
palette colour; `Container.buildShapeVisuals` is the only place it is applied, to the cubes only. Check
which colour property the ColorSO material and URP/Lit `M_Shape` use (`_BaseColor` vs `_Color`) and
SRP Batcher implications of property blocks. Then make the **minimum** change.

**Do not touch:** Board placement, Shape rotation/normalization, FBX files, prefab geometry,
Container positioning, extraction logic, SandCylinder, drag/snap, Fill UI, level data. Transform,
Collider, `cellVisual`, extraction and grain target must stay as they are. No unrelated
cleanup/refactor. **No commit.** Fill UI / z-fighting stays a separate later task.

---

## 1. Previous session (2026-09-12) — what it finished

### 1.1 Fill Percentage UI positioning (the last piece of work)

`Shape.cs` now derives the Fill UI anchor from the **runtime `occupiedCells`** list. The bounding box
is never read.

Rule, implemented in `Shape.placeFillAnchor()`:

1. `bottomY = min(cell.y)` over `occupiedCells` — the lowest **occupied** row.
2. `rightX = max(cell.x)` over the cells **on that row only** — the right-most cell that is actually
   occupied there.
3. Anchor goes to that cell's **bottom-right corner**:
   `localPosition = ((rightX + 0.5) * cellWorldSize, (bottomY - 0.5) * cellWorldSize, authoredZ)`

Notes that matter:

- `occupiedCells` is already **rotated and normalized**, so rotation is handled for free and
  `placeFillAnchor()` is re-run on every `applyRotation()`.
- Holes never count, because an empty cell simply is not in the list.
- The anchor stays at `localRotation = identity` — its position already encodes the rotation. That is
  also what keeps the **text upright** in all four orientations, with no counter-rotation.
- Only the anchor's **authored Z** survives from the prefab (how far in front of the piece the
  readout floats). X and Y are derived; editing them in the Inspector does nothing.
- `Container.initialize()` calls `_shapeVisual.setCellWorldSize(_board.cellSize)` — the cell's world
  size comes from the sand, so a prefab cannot know it. **This link must stay.**

Fill value: `Container.refreshFillDisplay()` → `Shape.setFillPercent(fillLevel)` where
`fillLevel = filledUnits / capacityUnits`. It is the existing capacity model, not a second one.
Called from `initialize`, `addCollected` and `forceComplete`.

Badge look (from `Docs/selected_sand_idea_mockup.png`), TMP rich text only:

```
<mark=#0B0B12FF><b>  {percent}<size=65%>%</size>  </b></mark>
```

**Known gap:** the `<mark>` quad really is emitted (verified from the TMP mesh: 4 vertices at
rgba 0,0,0,255) but with this project's `TextMeshPro/Mobile/Distance Field` font material the
highlight renders as a **faint plate**, not the mockup's solid dark rounded pill. A solid rounded
badge needs a real quad plus a sprite/material asset threaded down from `Level` — the project builds
materials by copying serialized ones and never uses `Shader.Find` (see `SandCylinderRenderer`'s
Android note, and `Board.buildFloorVisual` for the pattern). Deliberately **not** done.

#### Test results (this session)

- Edit mode: 9 shapes x 4 rotations = **36/36** anchors matched the expected corner, text upright in
  all 36.
- Play mode on the 10-shape level: **4 of 10** shapes have
  `bottomRightOccupied.x != boundingBoxMaxX` — direct proof the bbox is not used
  (L4 Deg90 `0 vs 2`, T4 Deg0 `1 vs 2`, S4 Deg0 `1 vs 2`, Plus5 `1 vs 2`).
- Spot checks against the spec's own examples: vertical L4 Deg0 → `x=1`; horizontal L4 Deg90 → `x=0`
  (its single bottom cell, not the far right of the 3-wide row above); U5 Deg0 → `x=2`.
- Visual: `Assets/Screenshots/fillui_final_nooverlay.png`.
- **Only `Shape.cs` and `Container.cs` changed.** The 13 Shape prefabs were **not touched** this
  session, and extraction, movement, collision, camera and the rest of gameplay were not changed.

### 1.2 Two environment quirks to expect (not our bugs)

- **Domain reload while Play is running** (a recompile mid-Play) throws
  `NullReferenceException` from `Assets/Game/MoowCore/Scripts/Managers/AudioManager.cs:57`. This is a
  pre-existing MoowCore domain-reload problem, unrelated to gameplay code. Stop Play before
  recompiling. Related project rule: never use rectangular `T[,]` fields or readonly runtime-state
  fields on MonoBehaviour/ScriptableObject here — they reset on the same reload.
- **MoowCore's Screen-Space Overlay UI** lays a translucent sheet over the game view, which washes
  out screenshots (the board reads light grey instead of dark navy). To capture the true game render,
  take the screenshot **from the camera directly** (`manage_camera action=screenshot
  camera=PerspectiveCamera`) — that path excludes Screen Space - Overlay canvases.

### 1.3 Earlier in the same session (context)

- 13 canonical Shape prefabs created, plus `Shape.cs` (`ShapeType`, `ShapeRotation`, rotation maths).
- `ContainerData` gained `shape` + `rotation`; `occupiedCells` is the single resolve point, cached
  per entry, and the legacy branch returns a **copy** so the level asset can never be mutated at
  runtime.
- `ContainerData.hasMissingShapeReference` distinguishes a broken Shape reference (Unity fake-null)
  from a legitimately empty legacy entry; reported by `validateLevel` and by `Level.buildContainers`.
- `SandSort_Phase1_1x1_Level.asset` migrated to `Shape_1x1` + `Deg0` (pure addition, capacities
  unchanged).
- `SandSort_CoreLoop_Test_Level.asset` rebuilt as a 10-shape, 5-colour distribution rig.
- Same-colour distribution verified: **50.19% / 49.81%** over 10 frames for the GREEN pair.

---

## 2. The Shape system as it stands

### 2.1 The 13 canonical prefabs

Folder: `Assets/Game/CurrentGame/Prefabs/Shapes/`

Cells use the Board convention (`cell.x` → world X, `cell.y` → world Y, **y up**), so a shape's
**last drawn row is y = 0**. All lists are normalized to min (0,0).

| Prefab | canonical cells | canonical layout |
|---|---|---|
| `Shape_1x1.prefab` | (0,0) | `#` |
| `Shape_2x1.prefab` | (0,0)(1,0) | `##` |
| `Shape_3x1.prefab` | (0,0)(1,0)(2,0) | `###` |
| `Shape_4x1.prefab` | (0,0)(1,0)(2,0)(3,0) | `####` |
| `Shape_2x2.prefab` | (0,0)(1,0)(0,1)(1,1) | `##` / `##` |
| `Shape_L3.prefab` | (0,0)(1,0)(0,1) | `#.` / `##` |
| `Shape_L4.prefab` | (0,0)(1,0)(0,1)(0,2) | `#.` / `#.` / `##` |
| `Shape_T4.prefab` | (1,0)(0,1)(1,1)(2,1) | `###` / `.#.` |
| `Shape_J4.prefab` | (0,0)(1,0)(1,1)(1,2) | `.#` / `.#` / `##` |
| `Shape_S4.prefab` | (0,0)(1,0)(1,1)(2,1) | `.##` / `##.` |
| `Shape_Z4.prefab` | (1,0)(2,0)(0,1)(1,1) | `##.` / `.##` |
| `Shape_U5.prefab` | (0,0)(1,0)(2,0)(0,1)(2,1) | `#.#` / `###` |
| `Shape_Plus5.prefab` | (1,0)(0,1)(1,1)(2,1)(1,2) | `.#.` / `###` / `.#.` |

`P4` was deliberately not created.

**Known spec discrepancy:** the original brief's ASCII art and its sample cell lists disagree for
**T4 and U5** (the lists are the vertically mirrored form). The ASCII was taken as canonical, because
the extraction spec requires U5's extraction level to hold exactly two cells with a gap between them
— only the gap-up orientation satisfies that. All four rotations exist, so the listed variants are
still reachable (they are the 180 of the canonical).

### 2.2 Rotation

- `ShapeRotation { Deg0, Deg90, Deg180, Deg270 }` — right angles only, no arbitrary rotation.
- Cell rotation is **clockwise**: `(x, y) -> (y, -x)` per step, then normalized so min x and min y are
  both 0. Fixed by the spec's own example: canonical vertical L4 at 90 must read `###` / `#..`.
- Visual side gets `Quaternion.Euler(0, 0, -90 * steps)`, applied to **VisualRoot only** (and with it
  `FBX_Placeholder`). The Shape root is **never** rotated, because `Container` places its own
  per-cell visuals from the already-rotated cell data — rotating the root would double-apply.
- **Since 2026-09-13** VisualRoot is also moved by the cells' normalization shift
  (`shift * cellWorldSize`, see 0.3) — rotation alone left rotated art 1–3 cells off `occupiedCells`.
- **The canonical prefab is never rewritten.** `applyRotation` only derives a rotated list plus the
  matching visual transform; the asset always sits at Deg0.
- Level data carries `Shape + Position + Rotation + Colour`, so one prefab covers all orientations.

### 2.3 Prefab hierarchy and `FBX_Placeholder`

```
Shape_L4                     <- Shape.cs
├── VisualRoot               <- rotated to match the gameplay rotation
│   └── FBX_Placeholder      <- EMPTY Transform; the real FBX goes here as a child
└── FillUIAnchor             <- position DERIVED (bottom-right corner rule); rotation identity
    └── FillPercentageUI      <- world-space TextMeshPro, localScale 0.12, fontSize 40
```

Identical in all 13 prefabs. **Superseded 2026-09-13 — see 0.1 and 0.4:** `FBX_Placeholder` now
holds the real FBX instance (`localRotation (0,180,0)`), and the per-cell cubes built by
`Container.buildShapeVisuals()` (children of the **root**, `localPosition = offset * cellSize`,
`localScale = (cellSize*0.9, cellSize*0.9, 0.5)`) are kept for their Collider (pointer picking) and
Transform (`cellVisual(i)` / grain target) with their `MeshRenderer` disabled. `FBX_Placeholder`
itself still has no gameplay role and gameplay never reads the FBX mesh or bounds.

### 2.4 Assumptions Blender assets must respect

- **Cell size is 0.85 world units** and it is *not* a free choice: it is
  `blockCellSize / sandDensity = 34 / 40`, i.e. exactly one sand block's world width. Model one cell
  as 0.85 units, or model at 1.0 and scale `FBX_Placeholder` by 0.85.
- **Cell spacing is exactly 1 cell**, no gaps: cell (x, y) sits at `(x, y) * 0.85` in the shape's
  local space. The current cubes are drawn at 0.9 of a cell so a visible seam appears — that is a
  placeholder look, not a spacing rule.
- **Origin/pivot:** the shape's local origin must be the **centre of cell (0,0)** of the canonical
  (Deg0) layout, which after normalization is the bottom-left cell of the bounding box. Everything
  (`Container`'s cube placement, the Fill UI corner maths, Board occupancy) is measured from there.
- **Orientation:** the board lies flat in the XY plane facing a camera looking down +Z — like a phone
  screen, not a 3D tabletop. `+X` is right, `+Y` is up, and the camera sits at **negative Z** looking
  toward `+Z`, so **smaller Z is closer to the viewer**. Model the piece facing `-Z`.
- **Rotation happens about Z**, clockwise, in 90 degree steps. Art must look correct under a pure Z
  rotation; do not bake any rotation into the FBX.
- **Depth:** the cubes span roughly Z -0.25..+0.25 and the Fill UI anchor sits at Z = -0.5 (in front).
  Keep the model's depth within about half a cell so the readout stays in front of it.
- Gameplay never reads a mesh, a Renderer or any bounds — the FBX is decoration only. Do not derive
  footprints from it.
- ~~FBX destination folder is empty~~ — filled 2026-09-13. The Blender export contract actually used is
  **plane XZ, thickness Y, 1 cell = 0.85, origin at canonical cell (0,0)**, converted to the XY/−Z
  gameplay convention by the `(0,180,0)` instance rotation (see 0.1). The "model facing −Z" line above
  describes the gameplay space, not what Blender exports.

---

## 3. Grid and sand area — current relationship

### 3.1 The formulas (from `SandCylinderTunables.cs`)

```
EffectiveBlockGridWidth  = customPattern != null ? customPattern.width  : blockGridWidth
EffectiveBlockGridHeight = customPattern != null ? customPattern.height : blockGridHeight
GridWidth          = EffectiveBlockGridWidth * blockCellSize
GridHeight         = max(4, round(cylinderHeight * sandDensity))
SandAreaWorldWidth = GridWidth / sandDensity          // == EffectiveBlockGridWidth * CubeWorldSize
CubeWorldSize      = blockCellSize / sandDensity      // == Board.cellSize
```

`Level.buildBoard()` then places the Board from the sand's geometry:

```
cellSize       = CubeWorldSize
sandBottomY    = -cylinderHeight * 0.5
sandLeftX      = -SandAreaWorldWidth * 0.5
gapCells       = GameplayTunables.gridSandGapCells
topRowCenterY  = sandBottomY - (gapCells + 0.5) * cellSize
board.localPos = (sandLeftX + cellSize*0.5, topRowCenterY - (boardSize.y - 1)*cellSize, 0)
```

**The key invariant:** board column `c` and sand block `c` occupy the same world X span, because both
are `CubeWorldSize` wide and the board's left edge equals the sand quad's left edge. That holds **iff
`boardSize.x == EffectiveBlockGridWidth == sandPattern.width`** — which `SandLevelSO.validateLevel()`
already checks and warns about. Board **height** is free; only X must match.

**Therefore, to widen the test grid:** widen the **sand pattern's `width`** to the new `boardSize.x`.
`SandAreaWorldWidth` is derived, so the sand area widens proportionally on its own — no physics,
density, rate, accumulator or range change needed. `SandCylinderPatternData.Resize(width, height,
subdivisionsPerBlock)` remaps existing painted cells. Note `cylinderHeight` is unchanged by this, so
a wider grid makes the sand area wider but **not taller** — its aspect ratio changes, and the
camera's framing (below) will react.

### 3.2 Current real values (read from the assets, not guessed)

**`SandCylinderTunables` — component on `Assets/Game/CurrentGame/Prefabs/Level.prefab`:**

| Field | Value |
|---|---|
| `cylinderHeight` | 6 |
| `blockCellSize` | 34 |
| `blockGridWidth` / `blockGridHeight` | 10 / 10 (ignored — a pattern is assigned at runtime) |
| `sandDensity` | 40 |
| `sandExtractionRate` | 1600 |
| `maximumSandFlowRate` | 400 |
| `maxCellsPerColumnPerTick` | 6 |
| `extractionRangeY` | **0.3** (was 1.5 at HEAD; changed by the user) |
| `conveyorHeight` | 1.2 (sand-side clearance; the Board no longer uses it) |
| `particlesPerExtraction` | 160 |
| `cubeVisualScale` | 0.5 |

**Derived at runtime (measured):**

| Quantity | Value |
|---|---|
| `EffectiveBlockGridWidth` / `Height` | 5 / 7 |
| `GridWidth` x `GridHeight` | 170 x 240 |
| `SandAreaWorldWidth` | 4.25 |
| `CubeWorldSize` = `Board.cellSize` | 0.85 |
| one sand row in world units | 0.025 |
| `sandAreaBottomWorld.y` (`sandBottomY`) | -3 |
| sand renderer bounds | centre (0, 0, 0), size (4.25, 6, 0) |
| extraction band from the sand bottom | rows `[0, 12)` = 0.3 world units |

**`GameplayTunables` — `Assets/Game/CurrentGame/Data/Tuning/GameplayTunables.asset`:**

| Field | Value |
|---|---|
| `_gridSandGapCells` | **0.15** |
| `_extractionArrivalToleranceCells` | 0.035 |
| `_dragFollowSharpness` | 30 |
| `_dragMaxSpeedCells` | 15 |
| `_releaseSnapSharpness` | 22 |
| `_releaseSnapMinSpeedCells` | 4 |

**Active test level — `SandSort_CoreLoop_Test_Level.asset`:**

| Quantity | Value |
|---|---|
| `boardSize` | **(5, 15)** |
| `timerSeconds` | 120 |
| sand pattern | `TestHandPaintedPattern` (width 5, height 7, subdivisionsPerBlock 2) |
| containers | 10 (5 colours x 2 shapes) |
| `Board.topRow` | 14 |
| board local position | (-1.7000, -15.4525, 0) |
| top row centre Y | -3.5525 |
| board floor bounds | centre (0, -9.503, 0.2), size (4.25, 12.75, 0) |

**Camera (computed every frame by `Level.setupCamera`, measured on the level above):**

| Quantity | Value |
|---|---|
| orthographic | true |
| `orthographicSize` | 10.43875 |
| position | (0, -6.4388, -10) |
| rotation | identity (0, 0, 0) |
| near / far | 0.1 / 12.2 |
| aspect (game view at capture time) | 0.4614 |

Framing rule: bounds = sand renderer bounds ∪ `Board.floorBounds`, padding 1, distance 10;
`orthographicSize = max(extents.y + 1, (extents.x + 1) / aspect)`. With a tall narrow game view the
**width term dominates**, which is why a wider grid will change the zoom noticeably. The bounds are
measured **once** in `initialize` and cached, then re-asserted every frame to beat MoowCore's
`CameraControllerPerspective`.

---

## 4. File map

| Path | Role |
|---|---|
| `Assets/Game/CurrentGame/Scripts/Controller/Shape.cs` | `ShapeType`, `ShapeRotation`, canonical cells, `applyRotation`, `rotatedCells`, `normalizationShift` + `placeVisualRoot` (2026-09-13), `setCellWorldSize`, `placeFillAnchor`, `setFillPercent` |
| `Assets/Game/CurrentGame/Scripts/Controller/Container.cs` | Drag/collision/sweep, per-cell cubes (renderer hidden when an FBX draws the shape; Collider + Transform still used), fill + sealing, `refreshFillDisplay`, `setCellWorldSize` call |
| `Assets/Game/CurrentGame/Scriptables/SandLevelSO.cs` | `SandLevelSO` + `ContainerData` (`position`, `shape`, `rotation`, `cells` (legacy), `color`, `occupiedCells`, `hasMissingShapeReference`), `validateLevel` |
| `Assets/Game/CurrentGame/Scripts/Controller/Level.cs` | Builds sand area, Board, Containers; capacity split; camera framing |
| `Assets/Game/CurrentGame/Scripts/Controller/Board.cs` | Grid geometry, occupancy, cell↔world conversions (root = cell (0,0) since 2026-09-13), floor visual |
| `Assets/Game/CurrentGame/Scripts/Controller/ExtractionGrid.cs` | Per-Container sand interaction; footprint-overlap extraction. **Do not rewrite** |
| `Assets/Game/CurrentGame/Scriptables/GameplayTunables.cs` | Gameplay-only tuning SO (class) |
| `Assets/Game/CurrentGame/Data/Tuning/GameplayTunables.asset` | Its values |
| `Assets/Game/CurrentGame/SandCylinderDemo/Scripts/SandCylinderTunables.cs` | Sand-physics-only tunables (class) |
| `Assets/Game/CurrentGame/Prefabs/Level.prefab` | Holds the `SandCylinderTunables` component instance + material/colour wiring |
| `Assets/Game/CurrentGame/Prefabs/Shapes/` | **The 13 canonical Shape prefabs** (untracked) |
| `Assets/Game/CurrentGame/Data/Levels/SandSort_CoreLoop_Test_Level.asset` | **Active test level** (10 shapes) |
| `Assets/Game/MoowCore/Data/Level_Lists/0_Temp_Level_List.asset` | `_testLevel` → **CoreLoop** (repointed from Phase1 this session; one field to flip back) |
| `Assets/Game/CurrentGame/Data/Levels/SandSort_Phase1_1x1_Level.asset` | Migrated 5x 1x1 level, kept as the regression baseline |
| `Assets/Game/CurrentGame/FBXs/` | 13 Shape FBXs + 3 Board FBXs (untracked; Board FBXs not integrated) |
| `Assets/Scenes/BaseScene.unity` | **Always start playtests here**, never from `GameScene.unity` |
| `Docs/selected_sand_idea_mockup.png` | The visual reference for the Fill UI badge |

Also still pending from earlier, untouched: `SandCanvas.cs`, `SandCanvasRenderer.cs`,
`SandColorUtility.cs` and the `SandCanvasData`/`SandColumn` types are unused and awaiting deletion
approval. `SandSort_Test_Level.asset` is a plain `LevelSO` (not a `SandLevelSO`) and would error if
loaded; `SandSort_CoreLoop_Test_Level` used to be orphaned. No cleanup was done.

---

## 5. Suggested order for the next session

**Superseded 2026-09-13.** Steps 1–4 and 8 are done (see section 0); the next session starts with the
FBX runtime colour task in **0.8**, then the Fill UI z-fighting. Steps 5, 6, 7 and 9 below are still
open and still valid, in that order, after those two.

1. **Blender:** author the mockup's shape assets.
2. Import **one** Shape FBX into `Assets/Game/CurrentGame/FBXs/` and parent it under that prefab's
   `VisualRoot / FBX_Placeholder`.
3. Verify **pivot / scale / orientation / cell spacing** against section 2.4 (origin at the centre of
   canonical cell (0,0); one cell = 0.85 world units; facing -Z; no baked rotation).
4. Apply the same treatment to all 13 Shape prefabs.
5. Build a **wide test layout** showing all 13 shapes at once — reuse `GameScene` + `Level.prefab` +
   `LevelManager` / `LevelGenerator` + a `SandLevelSO`. **No new scene.** Keep starting positions
   non-overlapping (the current 10-shape level uses five 3-row bands as a pattern to copy).
6. **Widen the sand area proportionally** with the grid: raise `sandPattern.width` to the new
   `boardSize.x` so `SandAreaWorldWidth` follows. Do not touch sand physics, `sandDensity`,
   extraction rate, the accumulator, `extractionRangeY`, or the simulation algorithm.
7. Check the **Fill UI** on the real assets — keep the rule (lowest occupied row → right-most
   occupied cell on it → that cell's bottom-right corner, never the bounding box). Optionally close
   the badge gap from section 1.1, without rewriting the UI.
8. Fix only **visual / measurement / pivot** issues found.
9. **Regression test without touching gameplay:** rebuild the Phase1 level and confirm the 5
   containers, positions, colours, capacities (BLUE 7225 / GREEN 5202 / ORANGE 5202 / RED 5491 /
   WHITE 5780) and extraction are unchanged, and that no new console errors appear.

**Reminders:** no commit until Phase 1 sign-off; every commit must use
`--author="Baris <arfdrgc@gmail.com>"` with no Co-Authored-By line; start playtests from
`BaseScene.unity`.
