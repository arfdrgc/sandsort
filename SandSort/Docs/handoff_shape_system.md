# Handoff — Shape prefab system & FBX integration prep

Written 2026-09-12 at the end of the session. **Phase 1 is still open: nothing has been committed**
(`git log` still shows only `091cdab first`). Everything below is in the working tree.

Next session's first goal is **visual verification / asset-integration setup**, not new gameplay.

---

## 1. What this session finished

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

Identical in all 13 prefabs. Current state of the placeholder:

- `FBX_Placeholder` has **no mesh, no collider, no material, no gameplay role**. Its local
  position/rotation/scale are free Inspector values for lining art up.
- 0 MeshFilters and 0 Colliders across every Shape prefab (verified).
- Target layout once the art lands: `VisualRoot / FBX_Placeholder / RealShapeFBX`.
- The placeholder cubes you see in Play are built by `Container.buildShapeVisuals()` as children of
  the **root** (not of VisualRoot), one per occupied cell, at `localPosition = offset * cellSize`,
  `localScale = (cellSize*0.9, cellSize*0.9, 0.5)`, with a Collider used for pointer picking. **That
  is the switch-over point when the real FBX arrives** — decide there whether to keep the cubes (for
  picking) and hide their renderers, or replace them.

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
- FBX destination folder already exists and is **empty**: `Assets/Game/CurrentGame/FBXs/`.

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
| `Assets/Game/CurrentGame/Scripts/Controller/Shape.cs` | **NEW, untracked.** `ShapeType`, `ShapeRotation`, canonical cells, `applyRotation`, `rotatedCells`, `setCellWorldSize`, `placeFillAnchor`, `setFillPercent` |
| `Assets/Game/CurrentGame/Scripts/Controller/Container.cs` | Drag/collision/sweep, per-cell visuals, fill + sealing, `refreshFillDisplay`, `setCellWorldSize` call |
| `Assets/Game/CurrentGame/Scriptables/SandLevelSO.cs` | `SandLevelSO` + `ContainerData` (`position`, `shape`, `rotation`, `cells` (legacy), `color`, `occupiedCells`, `hasMissingShapeReference`), `validateLevel` |
| `Assets/Game/CurrentGame/Scripts/Controller/Level.cs` | Builds sand area, Board, Containers; capacity split; camera framing |
| `Assets/Game/CurrentGame/Scripts/Controller/Board.cs` | Grid geometry, occupancy, cell↔world conversions, floor visual |
| `Assets/Game/CurrentGame/Scripts/Controller/ExtractionGrid.cs` | Per-Container sand interaction; footprint-overlap extraction. **Do not rewrite** |
| `Assets/Game/CurrentGame/Scriptables/GameplayTunables.cs` | Gameplay-only tuning SO (class) |
| `Assets/Game/CurrentGame/Data/Tuning/GameplayTunables.asset` | Its values |
| `Assets/Game/CurrentGame/SandCylinderDemo/Scripts/SandCylinderTunables.cs` | Sand-physics-only tunables (class) |
| `Assets/Game/CurrentGame/Prefabs/Level.prefab` | Holds the `SandCylinderTunables` component instance + material/colour wiring |
| `Assets/Game/CurrentGame/Prefabs/Shapes/` | **The 13 canonical Shape prefabs** (untracked) |
| `Assets/Game/CurrentGame/Data/Levels/SandSort_CoreLoop_Test_Level.asset` | **Active test level** (10 shapes) |
| `Assets/Game/MoowCore/Data/Level_Lists/0_Temp_Level_List.asset` | `_testLevel` → **CoreLoop** (repointed from Phase1 this session; one field to flip back) |
| `Assets/Game/CurrentGame/Data/Levels/SandSort_Phase1_1x1_Level.asset` | Migrated 5x 1x1 level, kept as the regression baseline |
| `Assets/Game/CurrentGame/FBXs/` | **Empty — destination for the Blender FBXs** |
| `Assets/Scenes/BaseScene.unity` | **Always start playtests here**, never from `GameScene.unity` |
| `Docs/selected_sand_idea_mockup.png` | The visual reference for the Fill UI badge |

Also still pending from earlier, untouched: `SandCanvas.cs`, `SandCanvasRenderer.cs`,
`SandColorUtility.cs` and the `SandCanvasData`/`SandColumn` types are unused and awaiting deletion
approval. `SandSort_Test_Level.asset` is a plain `LevelSO` (not a `SandLevelSO`) and would error if
loaded; `SandSort_CoreLoop_Test_Level` used to be orphaned. No cleanup was done.

---

## 5. Suggested order for the next session

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
