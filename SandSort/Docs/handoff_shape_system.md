# Handoff — Shape prefab system & FBX integration

Last updated 2026-09-14 (Fill UI font alignment, priority list in 0.14, modular board frame in 0.15). The session before it
was the 2026-09-13 `SandLevelSO` validation cleanup, which followed the FBX-colour / Fill-UI-badge /
uniform-sand-scaling session earlier that day.
**Phase 1 is still open.** The latest commit is `bc2f2c2 core game play update fbxs add`; **nothing
from 2026-09-13 is committed** — it all lives in the working tree (see 0.6).

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

1. **Only `Shape_1x1.prefab` has the new Fill UI font.** It carries fontSize **26** + `MikadoBlack`;
   the other **12** Shape prefabs still carry fontSize **40** + `CONSOLA SDF`. The badge constants in
   `Shape.cs` (0.9) were calibrated against the 1x1's font, so on the other 12 the readout — and
   with it the plate — will come out noticeably larger until they are brought in line. Authored by
   the user, not by this session's code.
2. **Fill UI z-fighting (partly mitigated):** `FillUIAnchor` z = −0.5 is still coplanar with the FBX
   front face, so the TMP text can still speckle against it. The badge plate itself is clear of it
   (it sits at z −0.51 and writes no depth, see 0.9). Unchanged otherwise.
3. `Captures/` (project root, untracked) holds diagnostic screenshots from both 2026-09-13 sessions.
   Not part of the game; delete or ignore.

### 0.6 Working-tree changes, both 2026-09-13 sessions (uncommitted)

| Path | Change | By |
|---|---|---|
| `Assets/Game/CurrentGame/FBXs/` | **Untracked.** 13 `Shape_*.fbx` + `Board_Canvas/Cell/Frame.fbx` (+ .meta) | earlier |
| `Assets/Game/CurrentGame/Prefabs/Shapes/*.prefab` (13) | FBX instance added under `VisualRoot/FBX_Placeholder` | earlier |
| `.../Scripts/Controller/Board.cs` | B fix (three helpers) | earlier |
| `.../Scripts/Controller/Shape.cs` | A fix (`normalizationShift`, `placeVisualRoot`) **+ Fill UI badge (0.9)** | earlier + this |
| `.../Scripts/Controller/Container.cs` | Cube `MeshRenderer` disabled when an FBX draws the shape **+ FBX colour + badge material handoff (0.8)** | earlier + this |
| `.../Scripts/Controller/Level.cs` | **Uniform sand scaling (0.10)** | this |
| `.../Scriptables/SandLevelSO.cs` | **Stale pattern-width check removed + two comments corrected (0.11)** | validation session |
| `.../Data/Levels/SandSort_ColorTest_10x1x1_Level.asset` | **New, untracked.** 7x7 board, 10x `Shape_1x1`, 5 colours x2 — the colour-isolation rig | this |
| `Assets/Game/MoowCore/Data/Level_Lists/0_Temp_Level_List.asset` | `_testLevel` → **`SandSort_FillTest_5Shapes_Level`**. One field to flip | this |
| `.../Prefabs/Shapes/Shape_1x1.prefab` | Fill UI font → `MikadoBlack`, fontSize 40 → 26. **Only this one of the 13** — see 0.5 #1 | user |
| `.../SandCylinderDemo/Patterns/TestHandPaintedPattern.asset` | 5x7 → **5x5** blocks (cells 140 → 100 B) — set up so the sand test would be the exact 5x5 → 7x7 case | user |
| `.../Scripts/Controller/ShapeSandFill.cs` | **New, untracked.** In-shape sand heap + `PourTarget` (0.12) | fill session |
| `.../Scripts/Controller/ShapeCavityFloor.cs` | **New, untracked.** Cavity-floor depth quad (0.12) | fill session |
| `.../Scripts/Editor/ShapeSandFillTestWindow.cs` | **New, untracked.** Editor-only fill test window (0.12) | fill session |
| `.../Scripts/Controller/ExtractionGrid.cs` | Grain target -> `sandPourTarget`; extraction itself unchanged (0.12) | fill session |
| `.../Scripts/Controller/Shape.cs` | `sandPourTarget`, `setSandFillSource`, badge material/colour (0.9, 0.12) | fill session |
| `.../Scripts/Controller/Container.cs` | `sandPourTarget`, unlit material handed to the fill visuals (0.12) | fill session |
| `.../Prefabs/Shapes/*.prefab` (13) | `Cast Shadows = Off` on the FBX renderer; Fill UI font size x0.65 (0.12) | fill session |
| `.../Prefabs/Shapes/*.prefab` (12, all but `Shape_1x1`) | Fill UI TMP -> `MikadoBlack` font asset + `MikadoBlack BlackOutlineShadow` material (both `m_sharedMaterial` and `MeshRenderer.m_Materials`) + fontSize/fontSizeBase 26 -> 16.9 (0.13 #2) | font session |
| `Docs/FRAME_KIT.md` | **New, untracked.** The Blender-side contract for the modular frame kit (authored by the user) | user |
| `.../FBXs/Board_FrameEdge/Corner/TJunction.fbx` | **New, untracked.** The 3 kit pieces (+ .meta) | user |
| `.../Scripts/Controller/BoardFrame.cs` | **New, untracked.** Runtime assembly of the modular frame (0.15) | frame session |
| `.../Data/Tuning/GameplayTunables.asset` | `_gridSandGapCells` 0.15 -> **0.22352941**, so the gap equals the kit's divider (0.15) | frame session |
| `.../Scripts/Controller/Level.cs` | 3 frame-kit prefab fields + `buildBoardFrame()` (0.15) | frame session |
| `.../Prefabs/Level.prefab` | The 3 frame-kit prefab references assigned (0.15) | frame session |
| `Captures/frame_kit_*.png` | **New, untracked.** Frame verification screenshots at 7x9 / 5x5 / 10x15. Not part of the game | frame session |
| `.../Scripts/Controller/ExtractionGrid.cs` | **Top-profile extraction points + edge-gated extras (0.16).** Only this file; `SandCylinderDemo` untouched | reach session |
| `.../Prefabs/Level.prefab` | `SandCylinderTunables`: sand colours + particle values (0.12 table) | fill session |
| `.../SandCylinderDemo/Scripts/SandExtractionParticleEffect.cs` | `FollowPourForwardOffset = 0.56` Z-depth fix (0.12) | fill session |
| `.../SandCylinderDemo/Scripts/SandCylinderRenderer.cs` | `backgroundColor` (24,28,46) -> (10,11,18) | visual session |
| `.../Data/Levels/SandSort_AllShapes_7x8_Level.asset` | **New, untracked.** 7x8, all 13 shapes, 5 colours | shapes session |
| `.../Data/Levels/SandSort_FillTest_5Shapes_Level.asset` | **New, untracked.** 7x9 manual fill-test rig | fill session |
| `Assets/Game/MoowCore/Data/EditorColorPalette.asset` | 5 palette colours retuned (structure and colour codes kept) | visual session |
| `Assets/Game/MoowCore/Materials/Basic_Colors/*.mat` (5) | `_BaseColor` mirrored from palette; `_SColor` -> `#4A475E`; `_RampThreshold` 0.62 -> 0.45 | visual session |
| `Assets/Scenes/GameScene.unity` | Light, ambient, camera background, FXAA | visual session |
| `Assets/Settings/SampleSceneProfile.asset` | Bloom + Vignette + ColorAdjustments (sub-asset) | visual session |
| `.../Scripts/Controller/Level.cs` | Orthographic camera, pitch -20, auto size/position from bounds | camera session |
| `Assets/Game/MoowCore/Fonts/Consolas/CONSOLA SDF.asset` | TMP font asset re-serialised by the Editor (material ref regenerated). Incidental churn, nobody authored it | Editor |
| `Assets/Game/MoowCore/Sprites/roundedCircle300.png` | **New, untracked.** Not referenced by any code | user |

### 0.7 Coordinate system (verified from code, keep)

- Gameplay plane **XY**: `Board.cellToLocalPosition` = `(x*cs, y*cs, 0)`; sand quad identity in XY;
  gravity/sand bottom along −Y.
- Thickness **Z**: cubes z −0.25..0.25, floor at z +0.2 (behind), Fill UI z −0.5 (front); drag plane
  normal = `board.forward`.
- Camera: `Level.setupCamera` forces orthographic, rotation identity, `z = bounds.min.z − 10`,
  re-asserted every `Update`. GameScene's authored `PerspectiveCamera` (0,20,0, 70° X) is the old
  Pixel Loop Blast top-down setup and is overridden at runtime.
- Rotation about Z, clockwise, 90° steps; the Shape root is never rotated.

### 0.8 FBX runtime colour — done, verified

`Container.buildShapeVisuals` now points the FBX Renderer's **`sharedMaterial`** at the Container's
own `ColorSO` material — the same one the (hidden) cubes already get.

- Chain: `ContainerData.color` → `Level._containerColors` → `ColorSO.objectiveItemMaterial` →
  `MoowCore/Materials/Basic_Colors/Basic_*.mat` (shader **TCP2 Hybrid Shader 2**, colour on
  **`_BaseColor`**; `_Color` is a stale yellow in all five — never read it).
- **Assigning** a shared material mutates nothing; it only changes which material that Renderer
  draws with. No `SetColor`, no `renderer.material`, no `MaterialPropertyBlock`, no copy to own.
- Each FBX ships its own embedded `M_Shape` (URP/Lit, green) and **that material is shared by every
  Container built from the same Shape prefab** — writing into it would repaint all of them and dirty
  the imported asset. This is the hazard the design avoids.
- Verified in Play (10 containers, 5 colours): 10/10 correct `Basic_*` asset, exactly 5 distinct
  materials, 0 runtime copies, 13/13 `M_Shape` still the original green, 5/5 `Basic_*.mat`
  `IsDirty=False`, cube renderers 10/10 still disabled, colliders / `cellVisual` / `ExtractionGrid`
  10/10 intact.

### 0.9 Fill UI badge — done, verified

`<mark>` is gone; the readout is plain TMP text on a real rounded-rect mesh built at runtime.

- `Shape.setFillBadgeMaterial(Material)` (called from `Container.buildShapeVisuals`) creates
  `FillBadgeBackground` under `FillPercentageUI`; `Shape.refreshFillBadge()` sizes it from the text's
  **ink** box (not TMP's `textBounds`, which is the ~55 % taller line box).
- Colour comes from the same `ColorSO` material, via a **copy** with `_ZWrite = 0` and
  `renderQueue = 2990`. Both are needed: `FillUIAnchor.z = −0.5` is exactly the shape's front face,
  so the plate is pushed 0.01 in front of it, writes no depth, and draws before TMP's queue 3000 —
  the text stays on top without its position changing.
- Corner radius **0.0800 world**, measured off the FBXs (0.85 cell, 0.345 straight run). Padding
  0.38 / 0.40 of ink height, from the mockup's badge-to-number ratio (measured: padX 0.38–0.46,
  padY 0.50–0.58).
- Readout scale is **derived**, not authored: `FILL_READOUT_SCALE_PER_CELL = 0.1114` × cell size
  (0.0947 at 0.85). Editing `FillPercentageUI`'s scale in a prefab does nothing.
- Placement: the plate's **bottom-right corner sits on the anchor** and grows left and up. The shift
  lives on `FillPercentageUI` (the transform that exists for it), so the badge mesh stays a plain
  centred mesh under an unchanged parent and `placeFillAnchor`'s derived position is untouched.
- Verified in Play: bottom-right corner == anchor 10/10 (error 0.0), grows left+up 10/10, anchor
  still the derived cell corner 10/10, radius 0.0800 10/10, plate z −0.5100 10/10.

### 0.10 Uniform sand scaling — done, verified

**The sand area now follows the board's column count in BOTH axes by the same factor.** Entry point:
`Level.fitSandAreaToBoard()`, called from `buildSandArea` in place of the old
`_sandTunables.customPattern = sandLevel.sandPattern`.

```
s            = boardSize.x / pattern.width          (5 -> 7  =>  s = 1.4)
targetWidth  = boardSize.x                          (exact by construction)
targetHeight = round(pattern.height * s)            (whole blocks; 5x5 -> 7x7 is exact)
cylinderHeight = targetHeight * CubeWorldSize       (DERIVED, was authored)
```

- **Cell-resolution resampling is the whole point.** `Level.scalePatternUniformly` builds the runtime
  clone with **`subdivisionsPerBlock = blockCellSize` (34)**, which makes the pattern's paint grid
  *be* the simulation grid, so `PaintFromPattern`'s own `subCellSize` comes out at **1 cell**.
  Resampling at block resolution instead re-quantises the picture rather than zooming it: measured
  against a true 1.4× zoom it got **17.90 % of cells wrong**; at cell resolution it is **0.00 %**.
  Edge quantum 0.425 → **0.025 world**.
- Nearest-neighbour is mandatory — the bytes are colour **slot indices**, so averaging invents
  colours. Sampling is **centre-based**: `floor((d + 0.5) / cellsPerSourceCell)`.
- **One factor drives both axes.** `cellsPerSourceCell` is taken from the width and reused for the
  height, so a rounded height never stretches the picture; the remainder stays EMPTY at the **top**
  (the sand surface), never at the bottom where the extraction band reads.
- **The shared pattern asset is never written to.** `TestHandPaintedPattern.asset` is used by all
  levels; the clone is a runtime `ScriptableObject` held in `Level._runtimePattern` and destroyed in
  `Level.OnDestroy`. If the board already matches the pattern, the asset is handed over as-is and no
  clone is made.
- `cylinderHeight` becoming derived is **geometry, not physics**: `sandDensity` stays 40, so a cell
  is still 1/40 world and every rate, the accumulator and `extractionRangeY` are untouched — the
  column simply has more rows. It also makes `GridHeight` an exact multiple of `blockCellSize`, so
  `FillInitialLayers`' height clamp lands dead on (no clipping, no empty band). Side effect at
  scale 1: 6.0 → 5.95, which removes the 2-row (0.05 world) sliver that used to sit on top.
- **Sand volume grows, and that is geometry too.** 28 900 → **56 644** cells = exactly **1.4² = 1.96×**,
  because the area grew 1.96× at a fixed density. No resampling method can change that number;
  accepted deliberately. Container capacities derive from the grid, so they scale with it.
- `[Range(1, 4)]` on `subdivisionsPerBlock` is an Inspector clamp only — the clone is never an asset
  and never opened in that Inspector. Clone cost at 7x7: 55 KB.

**Verified in Play (board 7x7, source pattern 5x5):**

| Check | Result |
|---|---|
| sim grid vs ideal 1.4× zoom | **0 / 56 644 cells differ = 0.00 %** |
| clone | `7x7` blocks, subdiv **34** → paint **238x238** == sim grid; `isTheAsset = False` |
| shared asset | still `5x5`, subdiv 2, `IsDirty = False`, mtime unchanged across the run |
| sand bounds / board floor | both `5.9500 x 5.9500`; left edges both −2.9750, right edges differ 4.8e−7 |
| sand is square | True; `cylinderHeight = 5.95 = 7 × 0.85`; `SandAreaWorldWidth = 5.95` |
| board column c == sand block c | **7 / 7** |
| fill | 8092 cells in each of the 7 block columns, 0 empty columns; extraction band `[0,12)` covered in **238/238** columns |
| 5x7 → 7x10 case | single factor 23.8 both axes; content rows 0–332, **7 empty rows (0.175 world) at the TOP**; bottom band 0/238 gaps; a 2x2 source square → **48 x 48** cells, aspect error **0.00 %** |
| console | no new error/warning |

Screenshots: `Captures/uniform_sand_7x7.png` (block resolution, ragged edges) vs
`Captures/uniform_sand_cellres_7x7.png` (cell resolution, clean edges).

### 0.11 `SandLevelSO` validation — stale width check retired — done, verified

Closes the stale-width item that used to head 0.5 (now removed from that list, so 0.5's
numbering has shifted up by one). **`SandLevelSO.cs` only**; no other code or asset touched. Diff: +11 / −9.

- **Removed** the `_sandPattern.width != _boardSize.x` check and its `issues.Add(...)`. Since 0.10
  the authored width is not an input: `Level.fitSandAreaToBoard` sets `targetWidth = boardSize.x`,
  and `SandCylinderTunables.EffectiveBlockGridWidth` reads the runtime pattern — so "one board
  column per sand block" holds by construction and a width difference is just the scale factor, not
  an authoring error. A comment at the removal site says so, to stop the check being re-added.
- **It served nothing else:** the only caller is `Level.cs:83` and its return value merely picks
  `Debug.Log` vs `Debug.LogWarning` — no gameplay branch, no editor tool. Capacities come from the
  runtime grid; board/extraction alignment from `EffectiveBlockGridWidth`. The null-pattern case —
  the one path where the invariant really isn't guaranteed (`customPattern = null` falls back to the
  sand's own `blockGridWidth`) — is still caught by the early return above it.
- Two stale comments corrected with it: the class header's "must equal `boardSize.x`" sentence and
  the width item in `validateLevel`'s `Flags:` summary. Signature, return semantics and the other
  five checks untouched.

**Verified in Play** (BaseScene → `SandSort_ColorTest_10x1x1_Level`, board 7x7 vs source pattern
5x5 — the exact case that used to trip the warning):

| Check | Result |
|---|---|
| old width warning | **gone** |
| validation line | `Log`, not `LogWarning`: `OK — 5 sand color(s), matched to 10 Container(s).` |
| build unchanged | clone `7x7`, `EffectiveBlockGridWidth = 7`, `cylinderHeight = 5.95`, 10 containers |
| source asset | still `5x5`, `IsDirty = False` |
| other checks, each re-fired by calling `validateLevel` with doctored palettes (nothing written) | missing pattern ✓ · unmapped slot ✓ (5/5) · Container colour with no sand ✓ (5/5) · sand colour with no Container ✓ (`BLUE_LIGHT`) |
| missing-Shape-reference check | **not exercised** — needs a genuinely broken prefab reference; the diff does not touch that block |
| console | 0 errors; 7 warnings, all pre-existing MoowCore boot noise (DontDestroyOnLoad x2, MasterVolume, audio listener x2, EncryptedDatabase x2) |
| compile | `validate_script` standard: 0 errors, 0 warnings |

### 0.12 Shape sand fill, cavity floor, fill badge & sand-pour particles — done, verified

> **Superseded in part on 2026-09-17 (publisher feedback) — see `TODO.md` → "Session — 2026-09-17".**
> The fill picture is no longer a radial heap from the centroid: it is a uniform layer whose progress
> is shown only by the existing Z travel (`-0.08` → `-0.44`), with a rounded-corner mask (convex
> radius 0.080/0.85) and soft XY edge shading (`sandFillEdgeDarken` in GameplayTunables — code
> default 0.10, tested at 0.10; the asset is set to 0.258 by the user,
> `WALL_INSET_CELLS` 0.110/0.85, `FALLOFF_CELLS` 0.45). The pile/centroid description below is kept
> as history; the centroid now only positions `PourTarget` for the particles. Cavity floor, badge
> and particle notes are unchanged.

Everything in this section is **settled and must not be re-litigated next session**: start from this
state and change it only when there is a new reason to.

**`ShapeSandFill.cs` (new) — the in-shape sand heap.** A CPU-written `Texture2D` on one alpha-clipped
quad parented to `VisualRoot` and laid out in canonical cell space, so rotation and the normalisation
shift come along for free. Same technique as `SandCylinderRenderer`: `FilterMode.Point`, the same
`Hash01` grain, 34 px per cell, no new shader. Two hard rules: `occupiedCells` is an absolute mask
(an empty cell never fills, at any level including 100%), and the fill does **not** rise in Y — the
surface travels in Z from `-0.08` to `-0.44`, toward the camera.

- **The heap starts at the centroid of the occupied cells and stays there. This is the final
  decision.** It was A/B/C-tested against an extraction-point origin and a 60/40 blend, and on U5
  against a two-origin variant. Centroid won on balance (off-centre 0.01-0.20 cells at 25% fill vs
  0.21-0.97 for the alternatives) and on shape-to-shape consistency (13.7 points of coverage spread
  at 25% vs 31-35). **U5 uses ONE heap, not two** — two origins filled both arms by 99% while the
  bottom bar sat at 1%, i.e. sand hanging in two towers over an empty basin, and at 50% it produced
  a valley in the middle instead of a pile.
- Tuned constants, left as they are: `CENTER_GAIN 1.40`, `EDGE_GAIN 0.90`, `EDGE_NOISE 0.16`,
  `GRAIN_NOISE 0.14`, `SPREAD_EXPONENT 0.72`, `CORE_FRACTION 0.55`.
- Dead test API still present: `setPourOrigins` / `setPourOriginCell` / `pourOriginsInCells` /
  `pourMaxRadius` are left over from the A/B and nothing calls them. Removing them is optional
  cleanup, not a behaviour change.

**`ShapeCavityFloor.cs` (new) — interior depth.** All 13 FBXs are a single submesh with a single
material and the cavity floor's normal equals the outer rim's, so only depth separates them: a masked
quad at `z = -0.092` covers the cavity floor (frontmost vertex `-0.086`) and nothing else, because
where the piece has walls the wall geometry wins the depth test. Colour is derived from the
Container's own `ColorSO` colour by one multiplier (`DARKEN 0.62`) — no second palette.

**Fill badge.** Font size x0.65 on all 13 prefabs (40 -> 26; `Shape_1x1` 26 -> 16.9) and a shared
translucent black plate (`FILL_BADGE_COLOR = (0,0,0,0.9)`) instead of a per-colour one.

**Cast Shadows = Off** on the FBX `MeshRenderer` inside all 13 Shape prefabs. `Receive Shadows`, the
FBX files, materials and lighting were not touched.

**Sand-pour particles — the grain is aimed at the heap, not at the cell it came from.**
`ShapeSandFill` exposes an empty `PourTarget` Transform parented under `VisualRoot`, sitting exactly
where the heap starts; its world Z is the cell visuals' own Z (local 0), which is the reference
`FollowPourForwardOffset` is measured against. `Shape.sandPourTarget` and `Container.sandPourTarget`
forward it and `ExtractionGrid` hands it to `SandExtractionController.ExtractAtPoint` as the
`grainTarget`, falling back to `cellVisual(...)` only for a piece with no fill visual.

- **Extraction itself is untouched.** The `point` passed to `ExtractAtPoint` is still the overlapped
  board column's centre; the column choice, the bottom-up rule, the per-frame budget, the
  accumulators and therefore the amount of sand a shape pulls are all exactly as before. Only the
  Transform the grains fly toward changed.
- Why: aiming at `cellVisual` put the landing 0.25-1.28 cells away from the heap, and on a
  multi-cell top edge (T4 3 cells, U5 and Z4 2 cells) it split the stream into ribbons that missed
  the heap on both sides. With the pour target the landing sits within the X scatter alone
  (+/-0.21 cells) and the stream converges into one funnel over the heap.
- Verified live: `followedTargetLastPosition` holds **one** key, `PourTarget(parent=VisualRoot)` —
  U5 previously had two.
- The **`aimGrainsAtPile` test flag is gone** (zero references in `Assets/`).
- **Known, deliberately left alone:** on U5 the converging stream crosses the notch (the piece's
  empty middle cell) on its way in. Deferred to a later visual-polish pass.

**Particle tunables on `Level.prefab`'s `SandCylinderTunables` component** (a component, not the
`SandCylinderDemo` asset, so the demo is unaffected):

| Field | Value | Note |
|---|---|---|
| `particleSize` | **0.0336** | natural grain; ~3.8 px at a 1217x1786 game view |
| `particlesPerExtraction` | **160** | the field's own `[Range]` maximum — raise the attribute first if more is ever wanted |
| `particleSpread` | **0.06** | landing scatter is `particleSpread * 3`, X only |
| `particleGravity` | **1.0** | set by the user; arrival speed judged right at this value |
| `particleLifetime` | **1.8** | closes an older mid-air-death bug (0.7 covered only 2.40 units of fall) |
| `sandExtractionRate` | 1600 | authored value, unchanged |

`SandExtractionParticleEffect.FollowPourForwardOffset = 0.56f` (the Z-depth fix) stands: source AND
target are put on one plane and the Z landing scatter is zeroed for followed grains, which is what
keeps every grain in front of the piece's visible face instead of crossing through it.

**Test rig (Editor-only, keep):** `ShapeSandFillTestWindow.cs` under
`Window > SandSort > Shape Sand Fill Test` drives `Shape.setFillPercent` for every Container at
0/25/50/75/100 — visual only, capacity and extraction untouched. Test levels:
`SandSort_FillTest_5Shapes_Level` (7x9; Plus5/BLUE, U5/GREEN, L4/ORANGE, T4/RED, Z4/WHITE; none on
the extraction row so nothing auto-fills over a pinned value) and `SandSort_AllShapes_7x8_Level`
(7x8, all 13 shapes, 5 colours, every colour represented on the extraction row).

**Note for whoever runs the next playtest:** in `SandSort_FillTest_5Shapes_Level` no stream appears
until a piece is dragged onto the top row — extraction needs `Container.isAwake`, which only
`beginDrag` sets.

### 0.13 Backlog — parked on 2026-09-14, not the next task

**These are ON HOLD.** The working order is now 0.14. Come back to this list only when 0.14 is done
or when the user re-prioritises it. Item 2 was completed on 2026-09-14 before the list was parked.

Treat 0.12 as settled: the centroid heap, the single U5 mound, the `sandPourTarget` grain aiming and
the particle values are the **starting point**, not open questions. Do not re-analyse or re-tune them
without a new reason, and keep any new change from disturbing that behaviour.

1. **U5 notch pass-through** (0.12, deliberately deferred): the converging stream crosses U5's empty
   middle cell on the way to the heap. Visual polish only. **Related, decided 2026-09-14 (0.16):**
   the notch column briefly became a real extraction point under TOP PROFILE and then went back to
   never extracting, because an extra point in a MIDDLE column is gated off. So no sand is drawn
   through the notch — same as before — and the open item here stays purely about the stream's path.
2. ~~**Bring the other 12 Shape prefabs' Fill UI font in line with `Shape_1x1`** (0.5 #1)~~ —
   **DONE 2026-09-14.** All 13 prefabs now carry `MikadoBlack` + `MikadoBlack BlackOutlineShadow`
   at fontSize/fontSizeBase **16.9**. Two corrections to what 0.5 #1 said: the other 12 were on
   **`LiberationSans SDF`** (TMP's default — the font had never been assigned), not `CONSOLA SDF`;
   and a world-space TMP carries the material reference **twice** (`m_sharedMaterial` **and**
   `MeshRenderer.m_Materials`), so both had to move or the text would still have drawn with the old
   atlas. 16.9 is the right target because `Shape.FILL_READOUT_SCALE_PER_CELL` (0.1114) fixes the
   readout's WORLD size and was calibrated against the 1x1's font, and the badge plate is measured
   off the text's ink (`refreshFillBadge`), so 26 made number and plate ~1.54x too big. Only these
   four fields changed per prefab; no code, no `Shape.cs` constants, nothing from 0.12. Remaining
   diff vs `Shape_1x1` is benign: authored placeholder text (`0%` vs empty), TMP's own
   `m_TextStyleHashCode`, and the per-prefab `m_renderer` fileID.
3. Fill UI / FBX-face z-fighting for the TMP text (0.5 #2).
4. Optional cleanup, behaviour-neutral: drop the unused `setPourOrigins` / `setPourOriginCell` /
   `pourOriginsInCells` / `pourMaxRadius` test API from `ShapeSandFill` and restore the single-origin
   inline distance in `redraw`.
5. **Sand-colour set vs. a DOWNSCALED pattern** — found while doing 0.11; not a regression, not
   urgent. `validateLevel` collects the level's sand colours from the SOURCE asset while the
   simulation runs on the scaled clone. At scale >= 1 (every level today) nearest-neighbour keeps
   every source cell, so the two agree. If a board is ever NARROWER than its pattern, an isolated
   one-cell colour can be dropped by the resample and the "Container(s) have no matching sand —
   will auto-complete immediately" warning would quietly stop firing for it. Fix when it matters:
   read the colour set from the scaled clone.
6. **Pre-existing MoowCore fragility, not ours:** `AudioManager.cs:57` throws a
   `NullReferenceException` flood (`_playingAudioElements` null) on the **recompile -> Play** path; a
   clean **stop -> Play** is fine. Needs a null check. Out of scope so far.
7. **Phase 1 sign-off.** The whole working tree is still uncommitted (0.6); decide what lands and in
   how many commits.

### 0.14 Working priority list — set by the user 2026-09-14

**This is the order of work from here on.** 0.13 is parked behind it. Work top-down; do not start a
lower item because it looks easier. As always: 0.12 stays settled, and nothing here may disturb it.

1. ~~**Modular Board / Sand frame.**~~ **DONE 2026-09-14 — see 0.15.** `FBXs/Board_Frame` as it exists is authored against one specific
   grid size and one specific sand-area size. Instead of using it directly, build a MODULAR frame
   that keeps the reference image's look but assembles itself correctly for any grid and any sand
   area. The user's intended kit is three pieces:
   - one **rounded corner**,
   - one **straight edge** that can be stretched / repeated,
   - one **T-junction**.

   Intended placement: rounded corners at the sand area's top-left and top-right, the same corner
   piece (rotated) at the grid's bottom-left and bottom-right, T-junctions on the left and right at
   the divider line between sand and grid, and straight edges filling every span between them.

   Must hold: grid width/height changes, sand-area size changes, different aspect ratios, visually
   clean joins between corner/edge/T, and it has to fit the EXISTING runtime board/sand positioning
   rather than replacing it.

2. ~~**Extraction reach.**~~ **DONE 2026-09-14 — see 0.16.** Example: blue sand sits at the far right
   of the sand area, and even with a 2x3 vertical L placed at the top-right, the current extraction
   point cannot reach it. Not a bug in the current system's own terms, but a gameplay-level
   reachability problem. Find a fix that does NOT change the extraction logic settled in 0.12.

3. **Richer sand pixel-art system.** A tool / workflow for authoring and later editing more complex
   sand pixel-art images per level, like the reference image.

4. **Exit animation for a 100%-filled Shape.** When a piece fills completely, animate it off the
   board.

5. **Selected-Shape highlight.** While a piece is held (until the drop lands) it must read clearly
   in front of the others: moved toward the camera on Z, plus a white outline. Both revert on drop.

### 0.15 Modular board frame — done, verified (0.14 #1)

The rim is now assembled at runtime from the three-piece kit in `Docs/FRAME_KIT.md`
(`Board_FrameEdge` / `Board_FrameCorner` / `Board_FrameTJunction`), sized from the level's own grid
and sand rectangles. **`Board_Frame.fbx` is untouched and unreferenced** — it stays in the repo as
the fixed-size original. `Board_Canvas` was deliberately left alone.

**`BoardFrame.cs` (new).** `Level.buildBoardFrame()` hands it two rectangles — the board floor and
the sand quad, both already decided by `buildBoard` / `buildSandArea` — and it lays pieces around
them. It only READS those rectangles: no gameplay, occupancy, extraction or sand value is derived
from or written by it, and it creates no colliders.

- **FBX conversion is the Shape pattern, verified numerically, not assumed.** The kit FBXs arrive
  with the importer's own `(270.02, 0, 0)`; each instance gets
  `Quaternion.Euler(0,0,rotZ) * Quaternion.Euler(0,180,0)` — Z first, then the same `(0,180,0)`
  conversion `Shape_*.prefab`'s `FBX_Placeholder` uses. Measured: after the conversion an Edge
  occupies x [0, 0.85] y [-0.19, 0], a Corner x [-0.19, 0.425] y [-0.19, 0.425], a T-junction
  x [-0.425, 0.615] y [-0.19, 0.425] — exactly FRAME_KIT's pivot convention. Removing the `(0,180,0)`
  from a placed piece's rotation leaves a PURE Z rotation (x and y components 0), i.e. det +1, no
  mirror. That matters: a mirrored corner keeps its silhouette but flips its bevel normals.
- **ONE Edge per run, always** (2026-09-14, second pass, user's call). Every run — however long — is
  a SINGLE Edge scaled on its length axis only. This is a deliberate departure from FRAME_KIT.md's
  `(N-1) x Edge` module rule: the two are pixel-identical, because an Edge has no vertices between
  its two ends (so a length-axis scale cannot touch the cross-section or the bevel) and `M_Board` is
  a flat colour with no texture at all — checked, no TexEnv property is set on any of the three kit
  materials. Tiling only bought instances and joins that can never be seen. **Revisit this if the
  frame material ever gains a texture or length-wise detail**; FRAME_KIT.md's module rule still
  describes the GEOMETRY correctly either way. No `_0` / `_1` / `_2` instances exist any more; the
  seven runs are `Edge_Bottom`, `Edge_Top`, `Edge_Divider`, `Edge_RightGrid`, `Edge_RightSand`,
  `Edge_LeftGrid`, `Edge_LeftSand`.
- **The frame is 13 instances for ANY board size** — 7 Edges + 4 Corners + 2 T-junctions — down from
  28 at 7x9 and 37 at 10x15 in the first pass. The only exception is the 1x1 degenerate case, which
  needs **6** (4 Corners + 2 Ts): each window side is exactly one cell, so the joint arms already
  close it and no run is emitted.
- **The divider is Edges + the two T-junctions**, per the kit. It is 0.19 thick because in a
  three-piece kit the divider IS an Edge.
- **Frame depth: local Z `[-0.05, 0.20]`** (`BACK_PLANE_Z = 0.2`, mirroring `Board.FLOOR_DEPTH_OFFSET`,
  which is private there). See the parallax fix below. The front face is still 0.25 in front of that,
  so the rim still reads as raised.

**The gap now matches the divider exactly — settled 2026-09-14.**
`GameplayTunables._gridSandGapCells` is **`0.22352941`** (= `0.19 / 0.85`), so the space between the
board's top edge and the sand's bottom edge is exactly the kit's `0.19` divider. Measured in Play:
gap `0.19000`, divider `y [-3.1650, -2.9750]`, `boardTop -3.1650`, `sandBottom -2.9750` — the divider
fills the gap with **no overlap and no slack**, and `BoardFrame`'s mismatch warning no longer fires.
That knob is purely visual (extraction anchors to the sand's own band — see `Level.buildBoard`'s
note), so this moves the board down slightly and changes nothing about play. The centring fallback in
`BoardFrame` is kept for any future gap that is not 0.19.

**The visible gap under the divider was PARALLAX, not geometry — fixed.** With the frame's back face
on the sand plane (`z = 0`) and `Board`'s floor quad at `z = +0.2`, the `-20` degree camera pitch drew
the closer frame shifted UP, uncovering a strip of empty background: measured at **7 px of pure
background `(63, 19, 190)`** in `Captures/frame_kit_7x9.png`, between the divider and the grid's first
tile row. Sitting the frame's BACK face ON the floor plane (`BACK_PLANE_Z = 0.2`) removes it — the
divider can now only overlap the floor, never uncover it. Re-measured after the fix: that strip is
gone. Two bands remain there and **both are correct, not artefacts**: ~15 px of the divider's own
shaded UNDERSIDE (a 0.25-deep rim seen from below is `0.25 * sin 20 = 0.0855` world ≈ 13.5 px, which
matches), and ~6 px of `Board`'s own `FLOOR_CELL_GAP` band, which exists on all four sides of the
floor and is `Board.cs` territory, untouched.

**Frame colour: one shared runtime material, albedo derived by measurement.** `M_Board` is embedded
in each of the three kit FBXs (`materialLocation = InPrefab`) — there is no `.mat` asset to edit — so
`BoardFrame` builds ONE material from the kit's own and overrides only `_BaseColor`. One material for
all pieces also collapses them into a single batch.

- The problem: at this camera angle the authored albedo `(182, 178, 251)` arrived on screen as
  **`(158, 130, 187)`** — mauve-grey, not lavender.
- The cause, measured by rendering two known albedos and solving: in LINEAR space this scene responds
  `rendered ~= albedo * (0.727, 0.504, 0.511)`. It is that lopsided because the key light is warm
  (`1.00, 0.98, 0.94` at 1.7) while the Trilight ambient is purple (`0.36, 0.31, 0.55`) — green and
  blue are both starved, so a neutral albedo comes back PINK.
- **The consequence worth remembering: rendered blue caps at 188 even at albedo 1.0.** The mockup's
  saturated rail `(163, 160, 250)` is therefore unreachable without changing the lighting, which was
  out of scope. So blue is pinned at 1.0 and red/green reproduce the mockup's HUE RELATIONSHIP
  (red ~= green, blue clearly above both) at the highest value that allows.
- Result: `FRAME_BASE_COLOR = (0.7797, 0.9123, 1.0)`, verified on screen as **`(171, 172, 188)`**
  against a `(43, 10, 152)` background — luminance 173 vs 27.

**Verification (current, after the 2026-09-14 second pass).**
- Editor harness, **10 configurations** — the FRAME_KIT mockup plus 7x9 / 5x5 / 10x15 / 3x4 / 8x6 /
  12x4 / 2x14, the 1x1 degenerate, and the old 0.15 gap for regression: **13 instances every time**
  (6 for 1x1); outer rectangle exactly `window ± 0.19` on all four sides; all four walls AND the
  divider each merge into ONE contiguous interval (no gaps); zero pairwise overlap; nothing intrudes
  into either window; piece Z spans `[-0.05, 0.20]` in all of them.
- Against FRAME_KIT's own mockup table (first pass, tiled): 28 instances at exactly the documented
  pivots, rotations and the `x3.2` Edge scale — every row matched. That run is what validated the
  pivot/rotation mapping; the kit contract itself has not changed, only how many Edges fill a run.
- In Play from `BaseScene`, 7x9: gap `0.19000`, **13 pieces (7 scaled)**, one shared material,
  frame colour `(171, 172, 188)`, and no background strip under the divider. Earlier Play runs
  covered 5x5 and 10x15 with frame extents equal to `board/sand rect ± 0.19` to 1e-3.
  Screenshots in `Captures/` (`frame_kit_v2_7x9.png` is the current look).
- The frame is deliberately NOT added to `computeFramingBounds`: it adds 0.19 outside the
  sand/floor rectangle, well inside `CAMERA_PADDING`'s 0.7, so the framing found by hand in Play is
  unchanged.

**Unrelated pre-existing issue seen while testing:** on `SandSort_CoreLoop_Test_Level` (10x15) the
camera clips the level vertically — `setupCamera` ends up width-bound (half-width 4.973 vs the 4.950
needed) and the height needs 11.389 against 10.778 available. It clips the sand and floor themselves,
not just the frame, and `levelBounds` never included the frame, so this is not caused by the frame.

**Still open here:** `Board_Cell.fbx` is decided ("we will use it") but NOT done — the grid floor is
still `Board.buildFloorVisual`'s single procedurally-textured quad.

**Standing rules:** start playtests from `BaseScene.unity`; no commit until Phase 1 sign-off; every
commit uses `--author="Baris <arfdrgc@gmail.com>"` with no Co-Authored-By line.

### 0.16 Extraction reach — done, verified (0.14 #2)

Solved in **`Assets/Game/CurrentGame/Scripts/Controller/ExtractionGrid.cs` only**. Nothing in
`SandCylinderDemo` changed: `SandExtractionController`, `SandCylinderSandGrid`, `VerticalRowRange`,
`BlockColumnRangeForCube` and the whole rate/accumulator/particle chain are used exactly as they were,
and 0.12 is undisturbed. The scan distance per point is **unchanged** — a point still covers exactly
its own cell, `[center - 0.5, center + 0.5]`.

**The problem, stated precisely.** Extraction points used to be the shape's cells sitting in the
BOARD's top row. When a shape's top row is narrower than its silhouette, its reach was narrower than
the shape itself. Worked case, `Shape_L4` — cells `(0,0) (1,0) (0,1) (0,2)`, the 1-1-2 vertical L:

```
#      column 0: topmost cell (0,2)
#      column 1: topmost cell (1,0)
##
```

Flush against the board's right edge its box covers the last two sand blocks, but the only top-row
cell sits in the LEFT one, so the right block was unreachable at **every** legal placement.

**What was tried first and REMOVED — do not bring it back.** A `+0.25` cell widening of the outer
extraction cells' footprint (`EXTRA_SCAN_CELLS`). It reached the neighbouring block, but
`ExtractAtPoint` snaps a point to the WHOLE block containing it, so a column that became a candidate
on a 0.25 overlap drained its entire 1.0-wide block — 0.75 cells of sand the shape does not cover
(measured: the drained span was board cells `[3.50, 6.50]` where the footprint was `[4.25, 5.75]`).
Clipping the sand scan to the footprint fixed the leak but needed a new scan-span parameter on
`ExtractAtPoint`, and it reduced the neighbour to a 0.235-cell sliver, which never empties that block
— i.e. it did not actually solve the reachability problem. Both the widening and the clip were
reverted in full; `SandExtractionController.cs` is back at HEAD.

**The solution: TOP PROFILE.** A shape's extraction points are its SKYLINE — for every column the
shape occupies, the TOPMOST occupied cell of that column is one point. Built once per Container in
`buildTopProfile()` from `Container.shape`, which is already rotated and normalised
(`ContainerData.occupiedCells` / `Shape.rotatedCells`), so **every rotation of every shape gets its
own correct profile with no per-shape or per-rotation table anywhere**. A point's own Y is never used
— the band is anchored to the sand (see the EXTRACTION BAND note in the file) and a point only names a
COLUMN — so a profile cell lying lower in the shape extracts exactly like a top-row one. The gate is
therefore asked of the shape, not of each cell: it extracts while its TOPMOST row is the board's top
row, which is the same condition every cell that used to pass it was tested against.

**EDGE-GATED EXTRAS** (second pass, same day, user's call). A profile point in the shape's own top row
is a NORMAL point and always active — those are exactly the pre-TOP-PROFILE points, so **a shape in
the middle of the board behaves precisely as it always did**. A point below the top row is an EXTRA
and carries a gate:

| Extra's column | Gate | Active when |
|---|---|---|
| shape's leftmost | `GATE_LEFT_EDGE` | `gridPosition.x + shapeMinX == 0` |
| shape's rightmost | `GATE_RIGHT_EDGE` | `gridPosition.x + shapeMaxX == board.size.x - 1` |
| any middle column | `GATE_NEVER` | never |

Reasoning: a middle column can always be reached by a normal point by sliding the piece one cell, so
it needs no help. At an edge the piece cannot slide further that way, so without the extra that block
can never be drained at all — which is the entire L4 case. "Flush" is judged on the COMMITTED
`gridPosition`, the same input the top-row gate uses; the gate reads only the piece and its position,
never the sand. On a board exactly as wide as the shape both sides read true and both extras run.

The per-frame block budget is one slot per **ACTIVE** point (`gatherOverlappingColumns`' return
value), so a shape gains one slot only while parked against the matching edge. Everything else is
untouched: the overlap maths (plain interval intersection, `1` when the drawn cell sits on a column,
splitting `1` between two columns in between), `addCandidate`'s keep-max-per-column merge,
`sortCandidatesByOverlap`, the grain target, the accumulators and the capacity chain.

**Classification over all 13 shapes x 4 rotations (52 combos), from the real `buildTopProfile`:**

- **30 combos unchanged** (every point `ALWAYS`): `1x1`, `2x1`, `2x2`, `3x1`, `4x1` all rotations,
  `T4 r0`, `U5 r90/r180/r270`, `J4 r180/r270`, `L4 r90/r180`, `L3 r90/r180`.
- **22 combos gained points:** 26 edge-gated extras + 3 never-active extras.
- The 3 never-active ones are middle columns: `U5 r0` (the notch), `L4 r270`, `J4 r90`.
- `L4 r0` -> `x0y2:ALWAYS`, `x1y0:RIGHT`. `J4 r0` -> `x0y0:LEFT`, `x1y2:ALWAYS`.
  `Plus5` (all four rotations) -> `LEFT` + `ALWAYS` + `RIGHT`.
- In every combo the `ALWAYS` count equals the old top-row point count, so the middle-of-board
  behaviour is identical by construction, not just by measurement.

**Verified in Play from `BaseScene`** (`SandSort_FillTest_5Shapes_Level`, board 7x9, columns 0..6,
topRow 8), driving the real `ExtractionGrid` through reflection:

| Shape | left edge | middle | right edge |
|---|---|---|---|
| `L4 r0` (extra RIGHT) | 1 slot `[0]` | 1 slot `[3]` | **2 slots `[5,6]`** |
| `Plus5` (extras both) | 2 slots `[0,1]` | 1 slot `[3]` | 2 slots `[5,6]` |
| `Z4 r0` (extra RIGHT) | 2 slots `[0,1]` | 2 slots `[2,3]` | 3 slots `[4,5,6]` |
| `U5 r0` (extra NEVER) | 2 slots `[0,2]` | 2 slots `[2,4]` | 2 slots `[4,6]` |
| `T4 r0` (no extras) | 3 slots | 3 slots | 3 slots |

Real drains (extraction sites read per call from `SandExtractionController.removedCellsBuffer`):

- `L4` flush top-right: blocks **5 and 6** both drain (b5 -254, b6 -184 on a clean sand grid);
  accumulators live only on c5/c6. Before the fix block 6 could not be reached at all.
- `L4` in the middle (gx=1): **only** block 1, 616 cells — the neighbouring block 2 has orange and is
  never touched, so the extra really is off.
- Sharpest test, `Plus5` forced to ORANGE in the middle (gx=1): block 1 sat under its left column with
  **578 orange cells in the band and gave zero**; only the normal point's column (block 2) drained.
  The same piece at gx=0 drained block 0 (-185) AND block 1 (-617), accumulators c0/c1 — the left
  extra working at the edge.
- Off-grid mid-drag is unchanged: `L4` at anchor 3.40 gives `col3=0.60 col4=0.60 col5=0.40`, the
  classic half-cell split.
- One row below the top row: 0 slots. The row gate and the arrival tolerance are untouched.

**Capacity can never be exceeded — verified, no code change needed.** Three independent clamps:
`ExtractionGrid.Update` re-reads `_container.remainingCapacity` before EVERY `ExtractAtPoint` (and
`addCollected` runs in between, so the 2nd/3rd point of the same frame sees the updated value);
`ExtractForCollector` does `budget = Min(budget, remainingCapacity)` BEFORE `ExtractColor`, so
over-capacity sand is never even removed from the grid; `Container.addCollected` clamps with
`Min(_capacityUnits, ...)` and a sealed Container refuses everything. Measured with
`sandExtractionRate 1600` / `maximumSandFlowRate 400` against cells actually removed from the sand
grid:

| Shape | capacity | filled | removed from grid | sealed |
|---|---|---|---|---|
| `L4` (2 points) | 2 / 7 / 101 | 2 / 7 / 101 | 2 / 7 / 101 | yes |
| `Plus5` (3 points) | 1 / 5 / 13 / 399 | 1 / 5 / 13 / 399 | same | yes |
| `L4`, real frames | 400 | 400 | 400 | yes, then left the board |

`removed == filled` in every run, so no sand is lost either. `capacity = 1` with 3 points in one frame
is the strict case: the first call took 1, the other two hit `remainingCapacity <= 0` and never ran.

**Known consequence, accepted:** shapes whose top row is narrower than their silhouette now drain one
extra block per frame **while parked against the matching edge** (L4, J4, L3, S4, Z4, T4, Plus5 in the
rotations listed above). The user approved this explicitly: "point sayisinin artmasi sorun degil".

**Measurement caveat for whoever tests this next:** per-block sand counts are blurred by a few cells
at block boundaries because `ExtractColor` runs `ResolveCaveInBias`, which slides sand in from the
neighbouring COLUMN into a freshly emptied cell. That is the sand simulation, not extraction reach —
read `removedCellsBuffer` (or the accumulators) when you need exact extraction sites.

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
`boardSize.x == EffectiveBlockGridWidth`**. Board **height** is free; only X must match.
**Superseded:** `sandPattern.width` is no longer part of that equality, and `validateLevel` no
longer checks it — `Level.fitSandAreaToBoard` (0.10) makes it hold by construction; see 0.11.

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
| `Assets/Game/CurrentGame/Scripts/Controller/Level.cs` | Builds sand area, Board, Containers; capacity split; camera framing. **`fitSandAreaToBoard` + `scalePatternUniformly` + `_runtimePattern`/`OnDestroy` (uniform sand scaling, 0.10)** |
| `Assets/Game/CurrentGame/Data/Levels/SandSort_ColorTest_10x1x1_Level.asset` | 7x7 board, 10x `Shape_1x1`, 5 colours x2 — the colour-isolation rig; **`_testLevel` currently points here** |
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

**Superseded — see 0.13 for the current next tasks.** Steps 1–4 and 8 are done (section 0); so are
the FBX runtime colour (0.8), the Fill UI badge (0.9) and, in place of step 6, the uniform sand
scaling (0.10) — the sand area now follows `boardSize.x` automatically, so widening the grid no
longer needs a manual pattern resize. Steps 5, 7 and 9 below are still open and still valid.

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
