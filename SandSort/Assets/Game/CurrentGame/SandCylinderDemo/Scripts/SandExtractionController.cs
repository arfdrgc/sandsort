using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Drives the conveyor: a fixed set of cubes that move left-to-right along X
// only, at a constant Y and Z, on a track centered under the cylinder and
// slightly offset in Z. There is no orbit, no angle, no slot — each cube just
// has an X position that increases every frame; when it reaches the right
// boundary it is reset directly to the left boundary and keeps moving. Since
// every cube advances at the same constant speed and only ever resets to the
// exact same left boundary, their relative spacing along X never drifts.
// `cubes` is created once in Init() and never resized — cubes are never
// spawned, destroyed, or arbitrarily teleported after that (the boundary
// wrap is the one deliberate, spec'd exception, not an ad-hoc jump).
//
// Extraction eligibility is based on the actual sand, not a raw
// distance-based window: horizontally, a cube's reach is always the ENTIRE
// block-grid column its current world X falls within
// (SandCylinderSandGrid.GetBlockColumnRange) — every column of that block is
// simultaneously reachable for as long as the cube's X stays anywhere inside
// it, which is what lets a "1-unit cube" fully drain a matching "1-unit"
// block in one pass (see BlockColumnRangeForCube's doc comment for why an
// arbitrary tunable distance couldn't guarantee that: a fast-moving cube
// never dwelled on any single column long enough to drain its full depth
// before the window moved on). Vertically, a cube reaches from its own
// height up to tunables.extractionRangeY world units above it (one-sided;
// never below) — VerticalRowRange converts that into grid rows. A cube is
// eligible whenever grid.HasReachableColor finds matching-color sand within
// that window — wherever the block-grid fill actually put it, including at
// a negative X. The window ExtractColor is called with is recomputed from
// the cube's current position every call, so extraction tracks it
// continuously as the cube moves — the range is never cached in world
// space.
//
// All eligible cubes extract concurrently every frame (Update loops over
// every cube, not just a single winner) — there is no "only one stream at a
// time" restriction. Two cubes' block windows CAN overlap in principle (no
// explicit partitioning between neighbors any more), but in practice this
// almost never causes real contention: adjacent blocks are always different
// colors (see SandCylinderSandGrid.FillInitialLayers's Latin-square note),
// and ExtractColor's own per-tick per-column cap plus sequential (not
// concurrent-thread) processing mean even a genuine overlap just means two
// calls see each other's mutations in order, never a double-removal.
//
// Android note: the cube/stream material is passed in from
// SandCylinderDemoBootstrap's [SerializeField] Material (an asset under
// SandCylinderDemo/Materials/), not built via Shader.Find — see the note on
// SandCylinderRenderer for why a runtime Shader.Find is unsafe in an
// IL2CPP/release Android build.
public class SandExtractionController : MonoBehaviour {

    SandCylinderSandGrid grid;
    SandCylinderTunables tunables;

    Vector3 cylinderCenterWorld; // the cylinder's actual base center — anchors the world-X-to-grid-column mapping and the grid-to-world Y mapping (X/Y/Z)
    float cylinderDiameter;

    // cubeSize is the LOGICAL size (tunables.CubeWorldSize — exactly one
    // block's width) used anywhere the cube's real footprint matters for
    // gameplay (RearmCompletedCubesThatHaveLeftTheZone's clearDistance).
    // cubeVisualSize is purely how big it's actually RENDERED (cubeSize *
    // tunables.cubeVisualScale) — used only for the cube's own transform
    // scale, the particle effect's receiving offset, and gizmo Z-fighting
    // margins, so shrinking it never changes spacing/matching/reset
    // behavior, only how big the cube (and its label, which is parented to
    // it and inherits the scale automatically) looks.
    float cubeSize;
    float cubeVisualSize;

    float conveyorY; // fixed for every cube
    float conveyorZ; // fixed for every cube
    float leftBoundary;
    float rightBoundary;

    SandExtractionParticleEffect particleEffect;

    Material sharedCubeMaterial;

    readonly List<SandExtractionCube> cubes = new List<SandExtractionCube>();
    readonly List<Vector2Int> removedCellsBuffer = new List<Vector2Int>();
    int nextColorPointer;

    // Per-cube (indexed in parallel with `cubes`) rather than a single shared
    // controller-level value: since every eligible cube now extracts
    // concurrently (see the class doc comment), each needs its own
    // independent rate budget — a shared accumulator would just divide one
    // global rate across however many cubes happen to be active, defeating
    // the point of letting several color streams run at once.
    float[] extractionAccumulators;
    float[] grainSpawnAccumulators; // seconds until each cube's next single-grain spawn, gated by tunables.particlesPerExtraction (grains/second)
    bool loopComplete;

    // Set only by InitWithoutConveyor — see its doc comment. Init (the conveyor path used by
    // SandCylinderDemo/SandMixDemo) never sets it.
    bool externalCollectorsOnly;

    public void Init(SandCylinderSandGrid grid, Vector3 cylinderBottomWorldPos, float cylinderDiameter, Material cubeMaterial) {
        InitCommon(grid, cylinderBottomWorldPos, cylinderDiameter, cubeMaterial);

        int count = Mathf.Max(1, tunables.cubeCount);
        for (int i = 0; i < count; i++) SpawnCube(i);

        extractionAccumulators = new float[cubes.Count];
        grainSpawnAccumulators = new float[cubes.Count];
    }

    // Same setup as Init (grid, sand-area geometry, particle effect) but WITHOUT the conveyor: no
    // cubes are spawned and Update() does nothing. Extraction is instead driven from outside, one
    // collector point at a time, via ExtractAtPoint — used by the SandSort game's grid shapes
    // (CurrentGame/Scripts/Controller/ExtractionGrid.cs), which replace the conveyor cubes as the
    // thing pulling sand while reusing this class's own eligibility/budget/particle logic.
    public void InitWithoutConveyor(SandCylinderSandGrid grid, Vector3 cylinderBottomWorldPos, float cylinderDiameter, Material cubeMaterial) {
        externalCollectorsOnly = true;
        InitCommon(grid, cylinderBottomWorldPos, cylinderDiameter, cubeMaterial);
    }

    void InitCommon(SandCylinderSandGrid grid, Vector3 cylinderBottomWorldPos, float cylinderDiameter, Material cubeMaterial) {
        this.grid = grid;
        tunables = grid.Tunables;
        this.cylinderDiameter = cylinderDiameter;
        cylinderCenterWorld = cylinderBottomWorldPos;

        cubeSize = tunables.CubeWorldSize;
        cubeVisualSize = cubeSize * Mathf.Max(0.01f, tunables.cubeVisualScale);

        conveyorY = cylinderCenterWorld.y - tunables.conveyorHeight;
        conveyorZ = cylinderCenterWorld.z + tunables.conveyorZOffset;
        float halfWidth = Mathf.Max(0.01f, tunables.conveyorWidth) * 0.5f;
        leftBoundary = cylinderCenterWorld.x - halfWidth;
        rightBoundary = cylinderCenterWorld.x + halfWidth;

        if (cubeMaterial == null) {
            Debug.LogError("SandExtractionController: cubeMaterial is null — assign SandCylinderDemoBootstrap's Cube Material field in the Inspector (Assets/Game/CurrentGame/SandCylinderDemo/Materials/SandCylinderCubeUnlit.mat). Cubes will render with Unity's default (magenta) material until this is fixed.");
        }
        sharedCubeMaterial = cubeMaterial;

        BuildParticleEffect();
    }

    // Every cube position on the conveyor shares the same fixed Y and Z —
    // only X ever varies. This is the one place a cube's position is built.
    Vector3 PositionAtX(float x) => new Vector3(x, conveyorY, conveyorZ);

    // Converts a world X (anywhere across the sand quad's span) into the
    // matching grid column, so extraction can be evaluated against the
    // cube's real position instead of a fixed reference point. The sand
    // quad's world width is exactly cylinderDiameter, centered on
    // cylinderCenterWorld.x — see SandCylinderDemoBootstrap.BuildSandQuad.
    int WorldXToGridColumn(float worldX) {
        if (cylinderDiameter <= 0f) return grid.Width / 2;
        float leftWorldX = cylinderCenterWorld.x - cylinderDiameter * 0.5f;
        float t = (worldX - leftWorldX) / cylinderDiameter;
        return Mathf.Clamp(Mathf.RoundToInt(t * grid.Width), 0, grid.Width - 1);
    }

    // Exact inverse of WorldXToGridColumn — converts a grid column boundary
    // back into world X. Used to turn GetBlockColumnRange's [colStart,
    // colEnd) back into world-space bounds for the OnDrawGizmos rectangle.
    float GridColumnToWorldX(int column) {
        if (cylinderDiameter <= 0f) return cylinderCenterWorld.x;
        float leftWorldX = cylinderCenterWorld.x - cylinderDiameter * 0.5f;
        return leftWorldX + (float)column / grid.Width * cylinderDiameter;
    }

    // Converts a world Y (anywhere across the sand quad's vertical span) into
    // the matching grid row — the Y-axis counterpart of WorldXToGridColumn,
    // and the exact inverse of GridCellToWorld's Y mapping. The sand quad's
    // world height is tunables.cylinderHeight, with cylinderCenterWorld.y as
    // its bottom edge (grid row 0).
    int WorldYToGridRow(float worldY) {
        if (tunables.cylinderHeight <= 0f) return 0;
        float t = (worldY - cylinderCenterWorld.y) / tunables.cylinderHeight;
        return Mathf.RoundToInt(t * grid.Height - 0.5f);
    }

    // True whenever cubeWorldX is actually within the cylinder's real
    // horizontal footprint [leftWorldX, rightWorldX]. Callers MUST check
    // this before calling BlockColumnRangeForCube/HasReachableColor: since
    // WorldXToGridColumn clamps any out-of-range world X to column 0 or
    // width-1 rather than signaling "out of range", a cube still out on the
    // wide conveyor — nowhere near the cylinder yet — would otherwise
    // silently resolve to a valid-looking edge block and could start
    // "extracting" from it well before the cube visually reaches the sand.
    bool CubeXWithinCylinder(float cubeWorldX) {
        float leftWorldX = cylinderCenterWorld.x - cylinderDiameter * 0.5f;
        float rightWorldX = cylinderCenterWorld.x + cylinderDiameter * 0.5f;
        return cubeWorldX >= leftWorldX && cubeWorldX <= rightWorldX;
    }

    // Returns the [xStart, xEnd) grid column window — and its matching
    // world-space [xMinWorld, xMaxWorld] bounds, for gizmo/logging use —
    // for the ENTIRE block-grid block that cubeWorldX currently falls
    // within (SandCylinderSandGrid.GetBlockColumnRange). Callers must have
    // already confirmed CubeXWithinCylinder.
    //
    // Snapping to the whole containing block (rather than a distance-based
    // window centered or forward-projected from the cube's exact position)
    // is what makes a "1-unit cube fully drains a matching 1-unit block in
    // one pass" true by construction: every column of the block is
    // reachable for as long as the cube's X remains anywhere inside that
    // block's world-space span — giving it dwell time proportional to the
    // WHOLE block's width divided by cubeMovementSpeed, not one grid
    // column's sliver of it. A fixed-radius or forward-only distance window
    // (the previous designs) could put only a FRACTION of a block's columns
    // in range at any instant, so a fast-moving cube swept past columns
    // faster than SandCylinderTunables.maxCellsPerColumnPerTick could drain
    // their full depth — draining only a fraction of the block overall
    // before moving on to the next one.
    (int xStart, int xEnd, float xMinWorld, float xMaxWorld) BlockColumnRangeForCube(float cubeWorldX) {
        int column = WorldXToGridColumn(cubeWorldX);
        (int colStart, int colEnd) = grid.GetBlockColumnRange(column);
        return (colStart, colEnd, GridColumnToWorldX(colStart), GridColumnToWorldX(colEnd));
    }

    // Converts the cube's current world Y and the Inspector's
    // extractionRangeY into a [yStart, yEnd) grid row window, one-sided:
    // it reaches from the cube's own height up to extractionRangeY world
    // units above it, never below.
    (int yStart, int yEnd) VerticalRowRange(float cubeWorldY) {
        float rangeY = Mathf.Max(0f, tunables.extractionRangeY);
        int rowLow = WorldYToGridRow(cubeWorldY);
        int rowHigh = WorldYToGridRow(cubeWorldY + rangeY);
        return (rowLow, rowHigh + 1);
    }

    // Builds the particle system that visualizes sand travelling from the
    // real extracted cells toward the active cube. Replaces the old
    // cube-segment "SandStreamSegment_*" funnel entirely — see
    // SandExtractionParticleEffect for the actual particle behavior.
    void BuildParticleEffect() {
        GameObject particleGO = new GameObject("SandExtractionParticles");
        particleGO.transform.SetParent(transform, false);
        particleEffect = particleGO.AddComponent<SandExtractionParticleEffect>();
        // cubeVisualSize (not cubeSize): the receiving offset this computes
        // (see SandExtractionParticleEffect.Init) needs to match where the
        // cube's top face actually RENDERS, so grains visually land on it.
        particleEffect.Init(tunables, sharedCubeMaterial, cubeVisualSize);
    }

    // Cached purely for OnDrawGizmos — Gizmos callbacks run outside Update
    // (including in edit mode, when nothing is being processed), so the
    // first eligible cube found this frame (and the world-space block bounds
    // BlockColumnRangeForCube computed for it) is stashed here rather
    // than passed as a parameter. Never read by any extraction/gameplay
    // code. With multiple cubes now eligible concurrently, this is only
    // ever a representative sample for the diagnostic gizmo/log, not "the"
    // active cube.
    SandExtractionCube activeCubeForGizmos;
    float activeCubeBandXMinWorld;
    float activeCubeBandXMaxWorld;

    // TEMPORARY DIAGNOSTIC — logs only on a null<->non-null or cube-identity
    // transition (never every frame), so we get hard printed evidence of
    // whether/when Update's eligibility loop actually finds something during
    // a real Play session, without flooding the console. Remove alongside
    // the rest of the "TEMPORARY DIAGNOSTIC" block once the Gizmos issue is
    // found.
    SandExtractionCube lastLoggedActiveCube;

    void Update() {
        // No conveyor to drive when started via InitWithoutConveyor — external collectors call
        // ExtractAtPoint themselves.
        if (externalCollectorsOnly) return;

        AdvanceConveyor();
        RearmCompletedCubesThatHaveLeftTheZone();

        SandExtractionCube gizmoCube = null;
        float gizmoXMinWorld = 0f, gizmoXMaxWorld = 0f;

        // Every non-full, eligible cube extracts this frame — not just a
        // single "winner". See the class doc comment for why an overlap
        // between two cubes' block windows isn't a real correctness risk.
        for (int i = 0; i < cubes.Count; i++) {
            SandExtractionCube cube = cubes[i];
            if (cube.IsCollectionFull) continue;

            float cubeWorldX = cube.transform.position.x;
            if (!CubeXWithinCylinder(cubeWorldX)) continue;

            (int xStart, int xEnd, float xMinWorld, float xMaxWorld) = BlockColumnRangeForCube(cubeWorldX);
            (int yStart, int yEnd) = VerticalRowRange(cube.transform.position.y);

            if (!grid.HasReachableColor(xStart, xEnd, yStart, yEnd, cube.ColorIndex, tunables.extractionDiagonalSpread)) continue;

            ProcessExtraction(i, cube, xStart, xEnd, yStart, yEnd);

            if (gizmoCube == null) {
                gizmoCube = cube;
                gizmoXMinWorld = xMinWorld;
                gizmoXMaxWorld = xMaxWorld;
            }
        }

        activeCubeForGizmos = gizmoCube;
        activeCubeBandXMinWorld = gizmoXMinWorld;
        activeCubeBandXMaxWorld = gizmoXMaxWorld;

        if (gizmoCube != lastLoggedActiveCube) {
            lastLoggedActiveCube = gizmoCube;
            if (gizmoCube != null) {
                Vector3 p = gizmoCube.transform.position;
                float ry = Mathf.Max(0f, tunables.extractionRangeY);
                Debug.Log($"[SandCylinderDemo][GizmoDiag] Active cube '{gizmoCube.name}' at world ({p.x:F2}, {p.y:F2}, {p.z:F2}). " +
                    $"Extraction rect: X [{gizmoXMinWorld:F2}, {gizmoXMaxWorld:F2}]  Y [{p.y:F2}, {p.y + ry:F2}]  Z {p.z - cubeVisualSize * 0.5f - 0.05f:F2}. " +
                    $"controllerGO active={gameObject.activeInHierarchy}, component enabled={enabled}.");
            } else {
                Debug.Log("[SandCylinderDemo][GizmoDiag] No active cube this frame (activeCubeForGizmos set to null).");
            }
        }

        CheckForCompletion();
    }

    // Debug-only visualization of the currently active cube's extraction
    // window — purely diagnostic, reads the same tunables/grid data
    // ProcessExtraction uses but never feeds back into
    // extraction/reachability/collection in any way. Gizmos (unlike
    // Debug.DrawLine) render in the Scene View via the Editor's own Gizmos
    // pipeline — visible without needing Play mode's Game view Gizmos toggle
    // — and OnDrawGizmos (rather than OnDrawGizmosSelected) keeps it visible
    // without having to select this GameObject first. Recomputed from
    // activeCubeForGizmos.transform.position every call (Unity calls this
    // every frame the Scene View repaints), so it tracks the moving cube
    // automatically; there's nothing to draw before Play starts populating
    // activeCubeForGizmos, or once nothing is currently active.
    //
    // Z is pushed toward the camera past the cube's own front face rather
    // than left at pos.z: SandCylinderCubeUnlit.mat and
    // SandCylinderSandUnlit.mat are both Opaque with ZWrite on, and regular
    // Gizmos ARE depth-tested against opaque scene geometry in the Scene
    // View, so a rectangle drawn at the cube's exact center depth got hidden
    // by the cube's own nearer front face wherever it crossed the cube's
    // footprint. The sand quad sits at a farther (larger) Z than the cube
    // already — see conveyorZOffset's own doc comment, "negative brings the
    // conveyor in front of (closer to the camera than) the cylinder" — and
    // SandCylinderGlass.mat is Transparent with ZWrite off, so neither of
    // those ever depth-occludes a Gizmo; only the cube's own mesh did.
    void OnDrawGizmos() {
        // TEMPORARY DIAGNOSTIC — draws completely unconditionally (no null
        // checks, no dependency on activeCubeForGizmos/tunables) at this
        // component's own transform, which is fixed and known
        // (controllerGO is parented at the bootstrap's origin — see
        // SandCylinderDemoBootstrap.Start). If this magenta sphere is not
        // visible in the Scene View during Play, the problem is entirely in
        // the Gizmos rendering pipeline itself (Scene View's Gizmos toggle
        // off, or this script/type unchecked in the Scene View's Gizmos
        // filter dropdown) — not in any of the active-cube/rectangle logic
        // below. If it IS visible, the pipeline is fine and the problem is
        // in activeCubeForGizmos or the rectangle math instead.
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 3f);

        // Reference lines spanning the cylinder's real width, independent of
        // activeCubeForGizmos (unlike the per-cube rectangle below) so
        // they're always visible during Play once Init has run — answers
        // "what world-space Y level does extractionRangeY actually reach up
        // to" without having to do the row-math by hand. Nothing to draw
        // before Play populates cylinderCenterWorld/cylinderDiameter/
        // conveyorY (all default to 0 beforehand, which would draw a
        // degenerate zero-length line at Y=0 — harmless, just not
        // meaningful — so skip it explicitly).
        if (tunables != null && cylinderDiameter > 0f) {
            float leftWorldX = cylinderCenterWorld.x - cylinderDiameter * 0.5f;
            float rightWorldX = cylinderCenterWorld.x + cylinderDiameter * 0.5f;
            float lineZ = cylinderCenterWorld.z - 0.1f; // just in front of the sand quad so it isn't depth-occluded

            // Cyan: the real world-space Y that extractionRangeY actually reaches up to. It is
            // ALWAYS <collector Y> + extractionRangeY (VerticalRowRange is one-sided from whatever Y
            // the collector sits at) — but which Y that is depends on who the collector is, so the
            // line has to follow the same split:
            //  - conveyor path (Init): the collectors are the cubes, all riding at conveyorY.
            //  - external-collector path (InitWithoutConveyor): there are no cubes and conveyorY is
            //    never used by anything; the caller passes its own point to ExtractAtPoint. The
            //    SandSort board hands it the sand's bottom edge (cylinderCenterWorld.y), so that is
            //    the reference here.
            // Drawing conveyorY on the external path was wrong and silently so: it only matched
            // while SandSort's board happened to sit exactly at conveyorY, and once the board became
            // tunable the line stayed put while the real ceiling moved (measured 2026-09-12: line
            // -2.70 vs cells actually removed up to -1.4875, a 1.2125 error).
            float extractionReferenceY = externalCollectorsOnly ? cylinderCenterWorld.y : conveyorY;
            float extractionReachY = extractionReferenceY + Mathf.Max(0f, tunables.extractionRangeY);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(leftWorldX, extractionReachY, lineZ), new Vector3(rightWorldX, extractionReachY, lineZ));

            // Orange: the block-grid fill's top edge (cylinderCenterWorld.y +
            // blockGridHeight * blockCellSize / sandDensity) — how tall the
            // Tetris-piece fill is, for a direct visual comparison against
            // the cyan reach line above (if cyan sits above orange,
            // extractionRangeY isn't actually constraining anything — see
            // the reasoning this was added to answer).
            float blockGridTopY = cylinderCenterWorld.y + tunables.EffectiveBlockGridHeight * tunables.blockCellSize / Mathf.Max(1f, tunables.sandDensity);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawLine(new Vector3(leftWorldX, blockGridTopY, lineZ), new Vector3(rightWorldX, blockGridTopY, lineZ));

#if UNITY_EDITOR
            Handles.color = Color.cyan;
            Handles.Label(new Vector3(rightWorldX + 0.1f, extractionReachY, lineZ), $"extractionRangeY reach (Y={extractionReachY:F2})");
            Handles.color = new Color(1f, 0.5f, 0f);
            Handles.Label(new Vector3(rightWorldX + 0.1f, blockGridTopY, lineZ), $"Block grid top (Y={blockGridTopY:F2})");
#endif
        }

        if (activeCubeForGizmos == null || tunables == null) return;

        Vector3 pos = activeCubeForGizmos.transform.position;

        // TEMPORARY DIAGNOSTIC — a deliberately oversized (2-unit) wire cube
        // dead center on the active cube's exact position, in a color used
        // nowhere else here. If this shows but the red/green rectangle
        // doesn't, the active-cube data is fine and the bug is specific to
        // the rectangle's own coordinates/edges.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(pos, Vector3.one * 2f);
        float rangeY = Mathf.Max(0f, tunables.extractionRangeY);

        // Clears the cube's own RENDERED front face (pos.z - cubeVisualSize/2)
        // with a small extra margin so the rectangle never z-fights right at
        // that boundary.
        float gizmoZ = pos.z - cubeVisualSize * 0.5f - 0.05f;

        float xMin = activeCubeBandXMinWorld; // matches BlockColumnRangeForCube
        float xMax = activeCubeBandXMaxWorld; // matches BlockColumnRangeForCube
        float yMin = pos.y;                   // matches VerticalRowRange: cubeY
        float yMax = pos.y + rangeY;           // matches VerticalRowRange: cubeY + rangeY

        Vector3 bottomLeft = new Vector3(xMin, yMin, gizmoZ);
        Vector3 bottomRight = new Vector3(xMax, yMin, gizmoZ);
        Vector3 topLeft = new Vector3(xMin, yMax, gizmoZ);
        Vector3 topRight = new Vector3(xMax, yMax, gizmoZ);

        // Horizontal (X range) edges in red — bottom edge sits exactly at
        // the cube's own height, top edge at the upward Y limit.
        Gizmos.color = Color.red;
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(topLeft, topRight);

        // Vertical (Y range) edges in green — left edge at xMin, right at xMax.
        Gizmos.color = Color.green;
        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(bottomRight, topRight);
    }

    // Moves every cube's X forward at a constant rate; Y and Z are rebuilt
    // from the same fixed values every frame so they can never drift. When a
    // cube reaches the right boundary it is reset directly to the left
    // boundary (the one spec'd exception to "no teleporting") and keeps
    // moving right from there — a continuous conveyor loop.
    void AdvanceConveyor() {
        float step = tunables.cubeMovementSpeed * Time.deltaTime;
        for (int i = 0; i < cubes.Count; i++) {
            Transform t = cubes[i].transform;
            float x = t.position.x + step;
            if (x >= rightBoundary) x = leftBoundary;
            t.position = PositionAtX(x);
        }
    }

    // A cube that has hit its collection ceiling (IsCollectionFull —
    // TotalCollected >= MaximumCollectible, exactly one block's cell count)
    // stays skipped by ProcessExtraction until it has physically travelled
    // clear of the extraction footprint it filled up in — tracked purely by
    // world-X distance moved since MarkFullSincePosition, not by whether
    // matching sand is still reachable nearby.
    //
    // This used to gate the reset on grid.HasReachableColor being false in
    // the cube's current window instead. That broke down for a common color
    // (e.g. the palette's first/base color, which with the default
    // cubeCount=6 > palette.Length=5 also ends up on two cubes): sand keeps
    // settling back into a narrow window as it falls, so "zero reachable
    // matching sand" could go arbitrarily long without ever becoming true —
    // a full cube got stuck full forever, and (back when only a single
    // global cube could ever be active) its same-colored sibling further
    // down the list was then starved of a turn indefinitely too. Clearing
    // the footprint's width is a hard, sand-distribution-independent
    // guarantee: every full cube resets within at most one conveyor lap.
    //
    // clearDistance is one full block's world-space width (cylinderDiameter
    // / tunables.EffectiveBlockGridWidth — the exact horizontal reach
    // BlockColumnRangeForCube now uses) plus cubeSize (the LOGICAL size, not
    // cubeVisualSize — this is a gameplay distance, not a render detail):
    // that's the widest this cube's own extraction footprint can ever be,
    // so once it's travelled that far past where it became full it's
    // guaranteed to have left every block it could possibly have been
    // drawing from.
    void RearmCompletedCubesThatHaveLeftTheZone() {
        float blockWidthWorld = cylinderDiameter / Mathf.Max(1, tunables.EffectiveBlockGridWidth);
        float clearDistance = blockWidthWorld + cubeSize;
        for (int i = 0; i < cubes.Count; i++) {
            SandExtractionCube cube = cubes[i];
            if (!cube.IsCollectionFull) continue;

            float x = cube.transform.position.x;
            cube.MarkFullSincePosition(x);
            float fullSinceX = cube.FullSinceWorldX.Value;

            // AdvanceConveyor only ever increases a cube's X within a lap
            // (wrap snaps it back to leftBoundary), so a smaller X than
            // where it became full can only mean it has wrapped — which
            // always counts as having left the zone.
            bool wrapped = x < fullSinceX;
            bool traveledClear = x - fullSinceX >= clearDistance;
            if (wrapped || traveledClear) {
                cube.ResetAmount();
            }
        }
    }

    // Called once per eligible, non-full cube per frame (see Update) — every
    // cube that has matching-color sand reachable within its own
    // BlockColumnRangeForCube window extracts concurrently, not just a
    // single "winner". A cube keeps being eligible until IsCollectionFull
    // (MaximumCollectible — exactly one block's cell count — reached), at
    // which point RearmCompletedCubesThatHaveLeftTheZone resets it and it
    // becomes eligible again.
    void ProcessExtraction(int cubeIndex, SandExtractionCube active, int xStart, int xEnd, int yStart, int yEnd) {
        int removed = ExtractForCollector(active.ColorIndex, active.RemainingCapacity, active.transform,
            xStart, xEnd, yStart, yEnd,
            ref extractionAccumulators[cubeIndex], ref grainSpawnAccumulators[cubeIndex]);

        if (removed > 0) {
            active.Increment(removed);
        }
    }

    // External-collector entry point (see InitWithoutConveyor): runs one frame of
    // extraction for a single collector point at pointWorld, applying exactly the
    // same eligibility chain Update() applies to a conveyor cube at that position —
    // CubeXWithinCylinder, the whole containing block (BlockColumnRangeForCube), the
    // one-sided vertical band (VerticalRowRange), HasReachableColor — then the same
    // shared ExtractForCollector step. The caller owns the collector's remaining
    // capacity and its two accumulators (one pair per point, like the per-cube
    // arrays). Returns the number of cells actually removed.
    public int ExtractAtPoint(Vector3 pointWorld, byte colorIndex, int remainingCapacity, Transform grainTarget, ref float extractionAccumulator, ref float grainSpawnAccumulator) {
        if (grid == null || remainingCapacity <= 0) return 0;
        if (!CubeXWithinCylinder(pointWorld.x)) return 0;

        var (xStart, xEnd, _, _) = BlockColumnRangeForCube(pointWorld.x);
        (int yStart, int yEnd) = VerticalRowRange(pointWorld.y);

        if (!grid.HasReachableColor(xStart, xEnd, yStart, yEnd, colorIndex, tunables.extractionDiagonalSpread)) return 0;

        return ExtractForCollector(colorIndex, remainingCapacity, grainTarget,
            xStart, xEnd, yStart, yEnd, ref extractionAccumulator, ref grainSpawnAccumulator);
    }

    // The per-collector extraction step shared by the conveyor (ProcessExtraction) and
    // external collectors (ExtractAtPoint). This is the original ProcessExtraction body,
    // unchanged except that the collector's color, remaining capacity, Transform and
    // accumulators are passed in instead of being read from a SandExtractionCube and the
    // per-cube arrays.
    int ExtractForCollector(byte colorIndex, int remainingCapacity, Transform grainTarget,
        int xStart, int xEnd, int yStart, int yEnd,
        ref float extractionAccumulator, ref float grainSpawnAccumulator) {
        if (grid.GetColorCount(colorIndex) <= 0) {
            // Nothing left of this color anywhere in the cylinder — this cube
            // simply has nothing to do this pass; it keeps moving and will
            // check again next time it comes around.
            return 0;
        }

        extractionAccumulator += tunables.sandExtractionRate * Time.deltaTime;
        int budget = Mathf.Min(Mathf.FloorToInt(extractionAccumulator), tunables.maximumSandFlowRate);
        // Clamped to the cube's remaining lifetime capacity so a single tick
        // can never push TotalCollected past MaximumCollectible, even with
        // the much higher sandExtractionRate/maximumSandFlowRate throughput.
        budget = Mathf.Min(budget, remainingCapacity);

        int removed = 0;
        if (budget > 0) {
            removedCellsBuffer.Clear();
            // extractionDiagonalSpread only widens WHICH cells are reachable (see
            // SandCylinderSandGrid.DiagonalColumnFloor); budget and caps are unchanged.
            removed = grid.ExtractColor(xStart, xEnd, yStart, yEnd, colorIndex, budget, removedCellsBuffer, tunables.extractionDiagonalSpread);
            extractionAccumulator -= budget;
        }

        if (removed > 0) {
            TrySpawnExtractionGrain(ref grainSpawnAccumulator, grainTarget, colorIndex, removedCellsBuffer);
        }

        return removed;
    }

    // Converts a removed grid cell's (x, y) into its real world position —
    // the exact inverse of WorldXToGridColumn for X, and the matching mapping
    // for Y against the sand quad's world span (see SandCylinderDemoBootstrap
    // .BuildSandQuad: the quad's world height is cylinderHeight, and
    // cylinderCenterWorld.y is its bottom edge, matching grid row 0). Z is
    // shared by the whole flat sand quad, so every cell sits at the same Z.
    Vector3 GridCellToWorld(int x, int y) {
        float leftWorldX = cylinderCenterWorld.x - cylinderDiameter * 0.5f;
        float worldX = leftWorldX + (x + 0.5f) / grid.Width * cylinderDiameter;
        float worldY = cylinderCenterWorld.y + (y + 0.5f) / grid.Height * tunables.cylinderHeight;
        return new Vector3(worldX, worldY, cylinderCenterWorld.z);
    }

    // Gates grain spawning to tunables.particlesPerExtraction grains/second —
    // decoupled from the per-frame extraction tick so the spawn cadence is
    // controlled purely by that one tunable rather than framerate. At low
    // rates individual grains read as visibly spaced; at higher rates
    // (raise particlesPerExtraction) they read as a dense, near-continuous
    // stream — the accumulator loops per call so it can emit several grains
    // in one frame instead of capping at one/frame. Each grain that does
    // spawn picks one of the cells
    // actually removed this tick (never the cylinder center) and is handed
    // the cube's own Transform, not a snapshot position — see
    // SandExtractionParticleEffect for why that's what makes the grain
    // permanently "belong" to this cube.
    //
    // The interval is jittered (+/-25%) rather than fixed, so consecutive
    // grains land at slightly uneven moments instead of ticking out on a
    // perfectly metronomic beat — a "gentle vacuum" reads as organic, not
    // mechanical.
    //
    // grainSpawnAccumulator/target/colorIndex are the collector's own (a conveyor
    // cube's per-cube array slot and Transform, or an external collector point's —
    // see ExtractForCollector).
    void TrySpawnExtractionGrain(ref float grainSpawnAccumulator, Transform target, byte colorIndex, List<Vector2Int> removedCells) {
        if (particleEffect == null || removedCells.Count == 0) return;

        grainSpawnAccumulator -= Time.deltaTime;
        float rate = Mathf.Max(0.01f, tunables.particlesPerExtraction);

        // Loops (rather than spawning at most once per frame) so raising
        // particlesPerExtraction actually densifies the stream instead of
        // silently capping out at one grain/frame — at high rates or on a
        // low framerate, several intervals can elapse within one Update.
        while (grainSpawnAccumulator <= 0f) {
            grainSpawnAccumulator += (1f / rate) * Random.Range(0.75f, 1.25f);

            Vector2Int cell = removedCells[Random.Range(0, removedCells.Count)];
            Vector3 sourcePos = GridCellToWorld(cell.x, cell.y);
            // External collectors (dragged shapes) move unpredictably, so their grains follow the
            // target in flight; conveyor cubes keep the original constant-speed prediction.
            particleEffect.SpawnGrain(sourcePos, target, colorIndex, externalCollectorsOnly);
        }
    }

    // Local-space Y (in cube-size units, since the label is parented to the
    // cube and so inherits its scale) placing the collection label below the
    // cube's bottom face (-0.5), clear of it — the label now shows two
    // stacked lines (count, then percent — see
    // SandExtractionCube.RefreshCollectionLabel), so the offset needs
    // enough margin for that taller two-line block to sit fully underneath
    // the cube instead of overlapping it.
    const float CollectionLabelLocalHeight = -1f;

    // Builds the fixed set of cubes once, during Init. Never called again —
    // the conveyor never spawns or destroys cubes during normal operation.
    // Cubes start evenly spaced left-to-right (cube 0 leftmost), wrapped into
    // the conveyor's width so the initial layout is never off-track.
    void SpawnCube(int index) {
        Color[] palette = tunables.sandColors;
        if (palette.Length == 0) return;

        byte colorIndex = (byte)(nextColorPointer + 1);
        Color color = palette[nextColorPointer];
        nextColorPointer = (nextColorPointer + 1) % palette.Length;

        float conveyorWidth = Mathf.Max(0.01f, tunables.conveyorWidth);
        float startX = leftBoundary + Mathf.Repeat(index * tunables.CubeSpacingWorld, conveyorWidth);

        GameObject cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // Includes the spawn index, not just colorIndex: whenever cubeCount
        // exceeds the palette length (e.g. default 6 cubes over a 5-color
        // palette), more than one cube shares a color and, without the
        // index, an identical name — making two distinct GameObjects
        // indistinguishable in the Hierarchy and in the "[GizmoDiag]" console
        // logs, which log active.name. That made it look like a single cube
        // was extracting from a position it hadn't reached yet, when it was
        // actually its same-colored sibling (already there) doing its own,
        // correctly-ranged extraction.
        cubeGO.name = "SandExtractionCube_" + colorIndex + "_" + index;
        Collider col = cubeGO.GetComponent<Collider>();
        if (col != null) Destroy(col);
        cubeGO.transform.SetParent(transform, true);
        // cubeVisualSize (not cubeSize): purely how big the cube — and its
        // count/percent label, parented to it and so scaled automatically —
        // is actually RENDERED. Positions/spacing (PositionAtX,
        // CubeSpacingWorld) never read this transform's scale, so shrinking
        // it here changes nothing else.
        cubeGO.transform.localScale = Vector3.one * cubeVisualSize;
        cubeGO.transform.position = PositionAtX(startX);
        cubeGO.GetComponent<MeshRenderer>().sharedMaterial = sharedCubeMaterial;

        SandExtractionCube cube = cubeGO.AddComponent<SandExtractionCube>();
        cube.Setup(colorIndex, tunables.CubeMaximumCollectible, color, CollectionLabelLocalHeight);
        cubes.Add(cube);
    }

    // cubes never shrinks (nothing is ever removed), so completion is judged
    // purely on remaining sand. Logs once — the conveyor keeps moving and
    // cubes keep passing through the zone afterward (they simply have
    // nothing left to extract); what happens next is left for later.
    void CheckForCompletion() {
        if (loopComplete) return;
        if (grid.TotalSandCount <= 0) {
            loopComplete = true;
            Debug.Log("[SandCylinderDemo] Complete — cylinder is empty. Conveyor keeps moving; no sand left to extract.");
        }
    }
}
