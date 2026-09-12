# SandSort — Pre-Development Checklist

This document lists the scaffolding work — scripts, ScriptableObjects, prefabs, scenes, events, and config — that must exist before/while gameplay logic for the **"Sand Idea"** concept is implemented under `Assets/Game/CurrentGame/`.

It complements [`game_mechanics.md`](./game_mechanics.md) (what the systems are) and [`level_design.md`](./level_design.md) (how levels configure them). Check items off as they're completed; keep this in sync if design decisions change.

IMPORTANT — this document was rewritten on 2026-09-11 and replaces a previous version written for an unrelated concept ("Pixel Loop Blast", a PuzzleGrid/ColorLoop block puzzle). That previous version's checked-off items (`PuzzleGrid.cs`, `ColorLoop.cs`, `ObjectiveArea.cs`, etc.) did **not** correspond to code that actually exists in this repo. Do not trust old completion state carried over from memory or prior sessions without re-verifying against actual files.

Many items below could not be finalized until `game_mechanics.md`'s **Open Design Questions** were resolved. On 2026-09-11 they were resolved with explicit **prototype defaults** (not final design decisions — see `SandLevelSO.cs`'s header comment) specifically to unblock the First Playable Milestone: discrete grid Board, rectangular-only Container footprints, 2D SandCanvas grid aligned 1:1 with the Board (binary present/eroded cells, no partial amounts), fixed-rate discrete-tick pouring, and fully static hand-authored level data. Anything below built against those defaults should be revisited if a later design discussion changes them.

**UPDATE (2026-09-11): the First Playable Milestone (§10) is implemented and verified working end-to-end** via a live Unity Editor session (UnityMCP) — `SandLevelSO`, `Board.cs`, `Container.cs`, `SandCanvas.cs`, and an updated `Level.cs` all exist and compile; a real test level asset (`SandSort_CoreLoop_Test_Level.asset`) is wired in and was played through to a win in Play Mode from `BaseScene`. Sections below are updated to reflect this. Two environment problems were found and fixed along the way, unrelated to gameplay code:
- `Assets/Scenes/BaseScene.unity` and `GameScene.unity` were **not in Build Settings at all** (only `SandFeelDemoScene` was) — additive loading of `GameScene` silently failed. Fixed by adding both to Build Settings.
- Unity cannot serialize rectangular 2D arrays (`T[,]`) on a `MonoBehaviour`. `Board`'s occupancy grid and `SandCanvas`'s cell grid originally used `Container[,]` / `ColorSO.ItemColor[,]`, which silently reset to `null` after a domain reload triggered mid-Play (e.g. a script recompile), causing intermittent `NullReferenceException`s that only appeared after the objects had already worked correctly for a while. Fixed by switching both to flat 1D arrays with manual `x + y * width` indexing. **Apply the same rule to any future MonoBehaviour/ScriptableObject field in this codebase: never use a `T[,]` field — flatten it.**

**UPDATE (2026-09-11, later same day): visuals, camera, and drag input fixed after a first manual playtest attempt failed** (only one gray cube visible, wrong camera angle, drag didn't seem to work). Root causes and fixes:
- Board/SandCanvas/Container had **no visual representation at all** originally (Board/SandCanvas were pure data GameObjects with no Renderer, and Containers were untinted default-gray primitives) — nothing to see or click on beyond one plain cube. Fixed: `Board.cs` now builds a tan floor plate sized to the board; `SandCanvas.cs` now builds one small colored cube per present sand cell (destroyed as it's eroded); `Container.cs` now tints itself via a new `SandColorUtility.cs` (a hardcoded `ColorSO.ItemColor → Color` fallback, since no `ColorPaletteSO` asset exists yet — see `LevelSO.GetColor`).
- **Redefined the spatial layout convention**: Board/Container/SandCanvas now lie flat in the **XY plane at Z≈0** (cell.x → world X, cell.y → world Y), not the XZ "tabletop" plane used in the first version. This makes SandCanvas naturally stack directly above Board on-screen (matching the reference concept art: picture on top, grid below) when viewed by a camera looking straight down +Z with identity rotation — instead of needing an oblique 3D viewing angle. `Board.cellToLocalPosition`/`worldToNearestCell`/`footprintCenterOffset` and `Container.raycastBoardPlane`'s plane normal were updated accordingly (`transform.forward` instead of `transform.up`).
- `Level.cs` now has its own `setupCamera()` that fits an **orthographic** camera to whatever's actually visible (`computeLevelBounds()`), called every `Update()` tick. It must be re-asserted every frame, not just once on `LEVEL_READY_TO_PLAY`: MoowCore's generic `PerspectiveCameraController` also reacts to that same event and overwrites camera **position** with math tuned for the old "Pixel Loop Blast" world convention (a hardcoded `-45` centerFactor along a Z-depth axis we no longer use) — that generic controller was the actual cause of "camera açısı yanlış", not our own setup code being wrong. Calling ours every frame makes it win unconditionally instead of depending on undocumented `Dispatcher` event-ordering.
- **Drag input itself was not actually broken** — verified in isolation (a raycast against a Container's live screen position correctly hits its `BoxCollider`, and `Board.worldToNearestCell`/`cellToWorldCenter` round-trip correctly under the new axis convention). The reported "drag doesn't work" was a symptom of the camera pointing at empty space, not a raycast/input bug. **This session has no mouse hardware, so a human still needs to do one real manual click-and-drag playtest** to confirm feel — only the underlying math/raycast was verified, not actual hardware mouse-move events.

Re-verified end-to-end after these fixes: entering Play from `BaseScene` now shows the tan floor, the colored Containers, and the colored SandCanvas cells correctly framed by the camera (screenshotted via UnityMCP); pour/seal/win still fire correctly (re-tested by driving a Container to a second matching cell as before).

**UPDATE (2026-09-11, later same day): capacity math rework + 5×5 test level + sand pour particles.** Three critical fixes:
- **Test level rebuilt as 5×5 Board, all Containers 1×1** (`SandSort_CoreLoop_Test_Level.asset`): 2 BLUE, 1 RED, 1 GREEN, 1 YELLOW, a fully-painted 25-pixel SandCanvas.
- **Capacity is now derived, not hand-authored** — `ContainerData.capacityUnits` was removed entirely. `Level.buildContainers()` now computes it: total SandCanvas pixels of a color ÷ number of Containers of that color (integer division), remainder pixels distributed +1 to the first N Containers in list order (verified live: 9 BLUE pixels / 2 Containers → capacities 5 and 4, exactly as the remainder rule specifies). See `SandLevelSO`'s class header comment for the full model.
- **Depletion Tolerance Guarantee added**: `Container.forceComplete()` + `Level.applyDepletionGuarantee()` (runs every `Update()` tick, before the win/lose checks) — the moment a color's SandCanvas pixels hit zero, every not-yet-sealed Container of that color is force-set to 100% and sealed, whatever its actual fill was. Verified live by manually zeroing out GREEN's remaining pixels via script: the GREEN Container (at 1/5 filled) was immediately force-completed to 5/5 and sealed on the next frame. **This changes `isDeadlocked()`'s semantics** — "color fully depleted" is no longer a Lose trigger (it's now handled by the guarantee instead); only "no reachable board position has matching sand" remains a genuine deadlock/Lose. `game_mechanics.md`'s Lose Condition section was updated to match.
- **`SandLevelSO.validateLevel(out string message)` added** — pure-data check (no Play Mode needed) that every sand color has a matching Container and vice versa; logged from `Level.initialize()`. Verified live: `"OK — 25 total sand pixel(s) across 4 color(s), matched to 5 Container(s)."`
- **New `SandPourEffect.cs`**: a small particle-burst effect (one `ParticleSystem`, closed-form free-fall velocity solve — zero initial vertical speed, gravity does the rest) played whenever `SandCanvas.tryConsumeOneMatchingCell` succeeds, referencing (not directly reusing — see the script's header comment for why) the visual technique in `SandCylinderDemo/Scripts/SandExtractionParticleEffect.cs`. The static per-cell picture cubes are unchanged; this adds a transient "pouring" flourish at the moment of consumption.
- **A second, distinct camera bug found and fixed**: after the axis/camera rework above, the screen went back to showing only a solid background color on the 5×5 level. Cause: MoowCore's generic `PerspectiveCameraController` also calls `UpdateCameraClippingPlanes()` off of *its own* (wrong-for-us) camera position, leaving `nearClipPlane`/`farClipPlane` set to values appropriate for a camera hundreds of units away — even after `Level.setupCamera()` moved the camera back to the correct spot, those stale clip planes clipped the entire scene out of view. Fixed by having `setupCamera()` also reassert `nearClipPlane`/`farClipPlane` every frame, for the same "must win unconditionally, not depend on Dispatcher event ordering" reason position/rotation already needed to.

Re-verified live via UnityMCP: validation log correct, capacities correct (5/4/6/5/5, summing to 25), screenshot shows the full 5×5 picture and 5 correctly-positioned/colored Containers, depletion guarantee fires correctly, no console errors beyond pre-existing unrelated ones (missing AudioListener, MasterVolume, EncryptedDatabase singleton — none introduced by this work).

**UPDATE (2026-09-11, later same day): full core-loop rearchitecture** — multi-cell Container Shapes, Top Row Trigger, Bottom-Up Extraction, area-weighted Capacity, dense sand-pool visuals, and a corrected deadlock check. This superseded several claims in §2-§4 below (rectangular-footprint-only, `ContainerData.size`, BFS-based deadlock) — read this block as authoritative over those where they conflict; `game_mechanics.md` has the full up-to-date spec.

- **`ContainerData.size: Vector2Int` (rectangle) → `ContainerData.cells: List<Vector2Int>`** (arbitrary shape, relative to `position`). `Board`, `Container`, and `Level` all now work in terms of a "shape" (occupied-cell list) rather than a width×height rectangle — see `Board.cs`'s shape helpers (`shapeMin`/`shapeMax`/`shapeCenterOffset`) and `Container.buildShapeVisuals()` (one child cube per occupied cell, so an L/T shape renders and can be clicked correctly with no custom mesh needed).
- **Top Row Trigger**: `Container.tryPour()` now only draws sand through occupied cells sitting in `Board.topRow` (`size.y - 1`); everywhere else on the Board, pouring is a no-op regardless of color. A multi-column Shape draws from every one of its top-row columns simultaneously each tick.
- **Bottom-Up Extraction**: `SandCanvas` is conceptually a per-column stack now (still the same flat 1D array internally — see the domain-reload note above, don't revert that) — `tryConsumeFromColumn`/`canExtractColumn` always target the lowest not-yet-eroded layer in a column, and refuse if that layer's color doesn't match, even if the right color exists higher up. Old 1:1 `(x, board-row)` cell/pour addressing (`tryConsumeOneMatchingCell`, `hasAnyMatchingCellInFootprint`) is gone.
- **Area-based Capacity**: `Level.buildContainers()` now apportions each color's total pixels across its Containers by AreaUnits (shape cell count) using the "largest remainder" method (floor each exact share, hand out the leftover pixels to the largest fractional remainders first) — not a flat equal split. Verified live: a 4-area-unit vs. 2×1-area-unit BLUE group with 9 total pixels produced capacities 2/5/2 exactly as the weighted+remainder math predicts.
- **Dense sand-pool visuals**: `SandCanvas.buildCellVisual` now builds a ~20-grain cluster (4×5 small cubes, jittered) per logical layer instead of one flat cube — ~500 grains across a fully-painted 5×5 picture, referencing (not depending on) `SandCylinderDemo`'s dense-pool look. Destroying a layer's cluster in one call still erodes it from the bottom up, visually confirmed via screenshot (the extracted-from column visibly starts partway up compared to untouched columns).
- **Deadlock check rewritten twice in one session** after two real false-positive bugs, both caught by actually driving a multi-step solve through Play Mode rather than trusting the logic on paper:
  1. First bug: `Board.hasReachablePosition`'s BFS treated any cell currently occupied by another *still-unsealed* Container as permanently blocked, instantly declaring a lose the moment two Containers happened to want the same top-row column at different times — even though the blocking Container could simply move away later. Fixed by replacing the BFS with `Board.hasAnyValidPosition`, which enumerates every in-bounds anchor for a Shape ignoring current occupancy entirely (occupancy is "traffic," not structural).
  2. Second bug, found immediately after fixing the first by continuing the same live playthrough: a Container blocked by Bottom-Up Extraction's "wrong color currently at the bottom of this column" was ALSO being treated as a permanent deadlock, even though that blocking color is itself removable by another Container. Fixed by changing the deadlock predicate from `SandCanvas.canExtractColumn` (bottom layer must match RIGHT NOW) to the new `SandCanvas.hasAnyRemainingCellInColumn` (color exists ANYWHERE in the column, regardless of what's currently blocking it) — only a color's total absence from every reachable column still counts as deadlocked.
  - After both fixes, a full hand-driven solve of the 6-Container test level (moving Containers to the top row in a specific order, including one Container waiting for another to clear a Bottom-Up blockage) completed to a genuine win with the picture fully eroded — screenshotted for confirmation. `game_mechanics.md`'s Lose Condition section documents the remaining known limitation honestly: this still isn't real multi-Container solvability analysis, just "is this color permanently unreachable," which is deliberately lenient rather than falsely strict.

Re-verified live via UnityMCP end-to-end: 5×5 board, 6 Containers (three Shapes: 1×1, 2×1, and an L-shape), capacities 2/5/6/5/2/5 (sum 25, matching total sand), full solve reaches `LEVEL_COMPLETED` with no console errors beyond the same pre-existing unrelated ones noted above.

**UPDATE (2026-09-11, later same day): SandCanvas's per-grain Cube visuals replaced with SandCylinderDemo's actual rendering/particle classes, reused directly (not reimplemented).** The previous version's `SandCanvas.buildCellVisual` built a ~20-Cube "grain cluster" per logical layer (~500 `GameObject.CreatePrimitive(PrimitiveType.Cube)` calls for a full picture) — explicitly rejected as unacceptable. Replaced with:
- **New `SandCanvasRenderer.cs`** — ports the RENDERING ARCHITECTURE of `SandCylinderDemo/Scripts/SandCylinderRenderer.cs` verbatim: a single `Quad` + a procedurally-painted `Texture2D` (`Color32[]` buffer, `SetPixels32`, the exact same `Hash01` per-texel noise formula for an organic grainy look), repainted only when a cell actually changes (not every frame — our sand doesn't fall/move, unlike the demo's). Zero GameObjects per grain.
- **`SandCanvas.cs` now directly instantiates and uses `SandCylinderTunables` and `SandExtractionParticleEffect`** (both classes as-is from `SandCylinderDemo/Scripts/`, not copied/adapted) for the pour/erosion burst, in place of the earlier `SandPourEffect.cs` (now deleted). `cubeMovementSpeed` is forced to `0` on the constructed `SandCylinderTunables` instance, since `SpawnGrain`'s landing-position prediction assumes a target moving at constant velocity (built for cubes on a conveyor) — `0` correctly degrades that prediction to "the Container's current position," since our Containers are stationary while pouring. `sandColors`/palette-index mapping is built at `initialize()` time from the distinct `ColorSO.ItemColor`s actually present in that level's SandCanvas data (a plain array, not a `Dictionary` — see the domain-reload note: Dictionaries aren't Unity-serializable either, same failure mode as a 2D array).
- Container visuals (small per-cell cubes) and the Board floor plate were **not** changed — the rejection was specifically about SandCanvas/the pour effect, not Containers or the floor.

Re-verified live via UnityMCP: screenshot shows the picture as one continuous grainy/noisy-textured surface (visibly speckled per-color banding, not flat solid blocks) with no Cube seams; moving a Container to the top row and letting it extract still correctly erodes exactly the right texel block (a clean rectangular gap appears at the bottom of the extracted column) and seals at capacity as before, with the real `SandExtractionParticleEffect` firing with no console errors.

---

## 0. Current Actual State (verified 2026-09-11, updated same day after Core Loop work)

- [x] Folder skeleton exists under `Assets/Game/CurrentGame/`.
- [x] `Scripts/Controller/Level.cs` — now builds Board + SandCanvas + Containers from a `SandLevelSO` at `initialize()`, runs the Win/Lose/deadlock check loop, and dispatches `LEVEL_READY_TO_PLAY` / `LEVEL_COMPLETED` / `LEVEL_FAILED` / `FAIL_CONDITION_MET`.
- [x] `Scripts/Controller/Board.cs`, `Container.cs`, `SandCanvas.cs` — see §4, implemented and verified in Play Mode.
- [x] `Scriptables/SandLevelSO.cs` — see §3, implemented.
- [x] `Prefabs/Level.prefab` exists — unchanged; still just hosts `Level.cs`. Board/SandCanvas/Containers are built procedurally at runtime (primitive cubes, no visual prefabs yet — see §6, still pending).
- [x] `Data/Levels/SandSort_Test_Level.asset` — still a bare `LevelSO`, unused; kept as-is (harmless leftover).
- [x] `Data/Levels/SandSort_CoreLoop_Test_Level.asset` — a real `SandLevelSO` test level (3×1 board, 2 Containers, matching SandCanvas), wired as `0_Temp_Level_List.asset`'s `_testLevel` override, played through to a win.
- [x] `SandCylinderDemo/`, `SandFeelDemo/`, `SandMixDemo/` — standalone sand-physics/feel prototypes, **separate from this checklist**; inform look/feel only.

---

## 1. Folder Setup

The folder skeleton already exists (see §0) and mirrors `Assets/Game/ReferanceGame/`'s layout closely enough to reuse as-is:

- [x] `Scripts/Controller/`
- [x] `Scripts/Managers/`
- [x] `Scriptables/`
- [x] `Data/Colors/`
- [x] `Data/Levels/`
- [x] `Data/Level_Lists/` — not yet created; add when level-sequencing work starts.
- [x] `Data/PowerUps/`
- [x] `Data/Tutorials/`
- [x] `Prefabs/`
- [x] `Materials/`
- [x] `Sprites/`
- [ ] `Audios/` — not yet created.

---

## 2. Open Design Questions — Resolved with Prototype Defaults (2026-09-11)

These were blocking scaffolding; each now has a prototype default (see `SandLevelSO.cs`'s header comment) so the First Playable Milestone could be built. None of these are final design decisions — revisit with the user if they should become permanent:

- [x] Board granularity → discrete grid cells (`Board.cs`).
- [x] Movement/collision rules → axis-aligned rectangular footprints, grid-snap on drop, no overlap (`Board.isAreaFree`).
- [x] Deadlock detection algorithm → BFS reachability over free cells to any position with matching sand in footprint, plus a "no matching color left anywhere" fast check (`Board.hasReachablePosition`, `Level.isDeadlocked`).
- [x] Sand depletion model → 2D grid aligned 1:1 with the Board; each cell is present (one color) or eroded (NONE), consumed one cell at a time.
- [x] FillLevel drive mechanism → fixed-interval discrete tick (one matching cell consumed per `_pourIntervalSeconds`), not continuous volume.
- [x] Level authoring format → fully static, hand-authored `SandLevelSO` fields (no generation/randomization).

---

## 3. ScriptableObjects to Create

- [x] **`SandLevelSO : LevelSO`** (`Assets/Game/CurrentGame/Scriptables/SandLevelSO.cs`) — board size, `List<ContainerData>` (position, size, color, capacityUnits), `SandCanvasData` (per-column `List<ColorSO.ItemColor>`), timer seconds. Reuses base `LevelSO`'s `levelPrefab`/`difficulty`/etc.
- [ ] **`ContainerLibrarySO`** (or per-shape `ContainerSO` assets) — Container shape/size definitions, once shape variety (including the planned Irregular Shapes blocker) is needed. Not needed for the milestone (rectangular footprints are inline `Vector2Int` fields on `ContainerData`).
- [x] **Color** → reused MoowCore's existing `ColorSO.ItemColor` enum + `ColorPaletteSO.GetColor()` directly; no new class needed.
- [x] **`LevelListSO`** — already existed in MoowCore (generic, game-agnostic); reused as-is. `0_Temp_Level_List.asset`'s `_testLevel` now points at `SandSort_CoreLoop_Test_Level.asset`.

Reuse as-is (no new class needed), just review field values later:

- [ ] `GameDataSO` — default LevelTimer duration, reward values, etc.
- [ ] `InventoryDataSO` — starting coins/booster counts.
- [ ] `DataSO` — level attempt tracking, rating prompt config.
- [ ] `SdkConfig` — ad unit IDs/keys can stay stubbed until monetization work begins.

---

## 4. Scripts to Create — Controller Layer (`Scripts/Controller/`)

- [x] `Level.cs` — builds Board/SandCanvas/Containers from a `SandLevelSO`, runs the per-frame Win/Timer/Deadlock check.
- [x] `Board.cs` — flat-array occupancy grid (see the domain-reload note above — do not change back to `Container[,]`), `isAreaFree`/`occupy`/`free`, grid↔world conversion, BFS `hasReachablePosition` for deadlock checks.
- [x] `Container.cs` — `[RequireComponent(typeof(Collider))]`, `OnMouseDown`-style raycast-driven hold-and-drag (via `Camera.main` + manual raycasts, not Unity's `OnMouseX` messages), grid-snap on release, fixed-interval pour tick, seals (deactivates) at capacity.
- [x] `SandCanvas.cs` — flat-array 2D grid of `ColorSO.ItemColor`, `tryConsumeOneMatchingCell`/`hasAnyMatchingCellInFootprint`/`hasAnyRemainingCellOfColor`.
- [ ] `SandPixel.cs` — **not created as a separate class**; a cell is just a `ColorSO.ItemColor` value inside `SandCanvas`'s flat array. Revisit only if per-pixel behavior (VFX, per-cell amount) is needed later.
- [ ] `LevelTimer.cs` — **not a separate class**; `_timeRemaining` is a private field ticked in `Level.Update()`. Split out only if the timer needs to be driven/paused from elsewhere (e.g. a Pause popup).
- [x] Win/Lose condition hook — implemented in `Level.Update()` exactly as specified; verified via Play Mode (win path) — see §10.

---

## 5. Scripts to Create — Manager Layer (`Scripts/Managers/`)

Scene-scoped `BaseSingleton<T>`, per the ReferanceGame convention:

- [ ] A Board/deadlock manager — periodically or event-driven checks whether any remaining Container can still reach matching sand (algorithm per §2).
- [ ] A Continue/booster manager — grants extra time or a booster on Lose, per the (currently undefined) booster set.

Reuse directly from MoowCore — likely still applicable, confirm during implementation:

- [ ] `GameManager`, `LevelManager`, `LevelGenerator`, `InventoryManager`, `PowerUpManager`, `AudioManager`, `CameraManager`.

---

## 6. Prefabs to Create (`Prefabs/`)

- [ ] `Container_*` prefab(s) — starting with basic shapes; irregular shapes (L/T) are a later blocker-mechanic addition, not part of the first playable slice.
- [ ] `SandCanvas` prefab — pixel/particle-based sand picture renderer.
- [ ] `Board` prefab
- [ ] Updated `Level.prefab` root — wire in Board + SandCanvas once they exist (an existing `Level.prefab` is already present; confirm what it currently references before extending it).
- [ ] UI: HUD (Coins/Timer), Win popup, Lose/Continue popup, Pause popup (implementing `IPopup`).
- [ ] VFX: sand pour effect, container-seal effect, SandCanvas erosion effect.

---

## 7. New `Events.*` Constants (`AppConstants.cs`)

Add a CurrentGame-specific section (do not repurpose the fish-named constants):

- [ ] Container hold/drag/release events
- [ ] Sand pour started/stopped events
- [ ] Container sealed event
- [ ] SandCanvas pixel eroded / region depleted events
- [ ] Deadlock detected event
- [ ] Timer tick / timer expired events

Reuse directly (already generic):

- [ ] `LEVEL_LOADED`, `LEVEL_READY_TO_PLAY`, `LEVEL_COMPLETED`, `LEVEL_FAILED`, `FAIL_CONDITION_MET`
- [ ] `MONEY_CHANGED`, `UI_RETRY_CLICKED`, `UI_NEXT_LEVEL_CLICK`
- [ ] `POWER_UP_*` family (for boosters, once defined)
- [ ] `ENABLE_INPUT`, `DISABLE_INPUT`

---

## 8. Editor Tooling (Optional, Recommended)

- [ ] A `LevelEditor`-equivalent for authoring Board/Container layout and SandCanvas color regions directly in-scene and saving to `LevelSO` (mirrors ReferanceGame's `LevelEditor.cs` + `LevelSOEditor.cs`).

---

## 9. Config/Data Review

- [ ] Set `GameDataSO`/`InventoryDataSO`/`DataSO` initial values once `game_mechanics.md`'s Economy questions (currently open) are answered.

---

## 10. First Playable Milestone — DONE (verified 2026-09-11)

- [x] One `SandLevelSO` instance (`SandSort_CoreLoop_Test_Level.asset`): 3×1 board, 2 Containers (BLUE capacity 2, RED capacity 1), a matching 3-cell SandCanvas.
- [x] Movement/collision/pour verified: confirmed via a live Play Mode session (BaseScene → GameScene → `Level(Clone)` → `Board`/`SandCanvas`/2 `Container`s built correctly). Hold-and-drag input code (`Container.cs`) is written and wired to `Camera.main`/mouse but a human has not yet manually played it with a mouse in the Editor — movement was validated by directly driving `Board.free`/`gridPosition`/`Board.occupy` to simulate a completed drag, since this session has no mouse. **A human should still do one manual mouse playtest** to confirm the actual drag/raycast input feel works, not just the underlying data model.
- [x] A Container reaching 100% FillLevel seals (deactivates) and frees its Board space — confirmed (RED sealed automatically at capacity 1; BLUE sealed after being moved to a second matching cell).
- [x] Win condition fires when all Containers are sealed — confirmed (`Level._resolved == true` with time remaining, both Containers sealed).
- [ ] Lose condition fires on Timer expiry — implemented (`Level.Update`) but not exercised in this session (would need waiting out a full timer).
- [ ] Lose condition fires on a constructed deadlock — implemented (`Level.isDeadlocked`, BFS) but not exercised against a level actually authored to be unsolvable.

Two bugs were caught and fixed during this verification pass — see the note at the top of this document (Build Settings missing scenes; `T[,]` fields silently nulled by domain reload).

---

## 11. Blocker Mechanics (Do Not Start Yet)

The five remaining planned blocker mechanics (Locked, Linked, Frozen/Ice, Hidden/Mystery, Fixed Grid Obstacles — see `game_mechanics.md`'s Future Gameplay Features section) are explicitly **out of scope** until §10's First Playable Milestone is complete and the core loop feels right. Do not add scaffolding for them (ScriptableObject fields, prefabs, events) until their turn comes, in the order listed there. (Irregular/multi-cell Shapes, originally item 6 on this list, were pulled into the core loop on 2026-09-11 — see the rearchitecture update above — and are no longer a future blocker.)

---

## Testing

All Play Mode verification for this project **must** start from `Assets/Scenes/BaseScene.unity`, never `GameScene.unity` directly — see `CLAUDE.md`'s Testing Rules. `BaseScene` owns the persistent `DontDestroyOnLoad`/scene-scoped bootstrap managers — verified 2026-09-11 to actually be `Dispatcher`, `ApplicationInitializer`, `AudioManager`, `CameraManager` (**not** `ObjectPoolManager` — that class does not exist anywhere in this repo; ignore that specific claim wherever it appears). `GameScene` (loaded additively) owns `GameManager`, `LevelManager`, `LevelGenerator`, `GameDataManager`, `InventoryManager`; it must not have its own copies of BaseScene's managers.

**Both `BaseScene.unity` and `GameScene.unity` must be present in Build Settings** (File → Build Profiles → Scene List) or additive loading of `GameScene` fails silently with a console error and nothing in `GameManager`/`LevelManager`/`LevelGenerator` ever runs. This was missing and has been fixed (2026-09-11) — if it goes missing again (e.g. a fresh clone, a Build Settings reset), re-add both scenes before assuming the gameplay code itself is broken.
