using UnityEngine;

// Single source of truth for every SandCylinderDemo feel/balance knob, so the
// mechanic can be tuned entirely from one Inspector panel. This is an isolated
// counterpart to SandFeelDemo/Scripts/SandTunables.cs — not a modification of it.
// A standalone component (rather than a nested field) so it can be dropped on
// its own GameObject and referenced by every other SandCylinderDemo piece.
public class SandCylinderTunables : MonoBehaviour {

    [Header("Sand Area")]
    [Tooltip("World-unit height of the sand area. There is no width tunable here any more — width is derived from blockCellSize (see the Grid Blocks header below), matching Sand Blocks: Drop Puzzle's approach of defining the grid in fixed-size units rather than an arbitrary world dimension.")]
    [Range(1f, 10f)] public float cylinderHeight = 6f;

    [Header("Grid Blocks")]
    [Tooltip("Cells per side of one grid block — the '1-unit' square a collector cube can fully drain in one pass (see SandCylinderSandGrid.GetBlockColumnRange). Matches the 34x34 unit size Sand Blocks: Drop Puzzle (Rollic Games) uses for its own grid. This is now what actually defines the sand area's width (GridWidth = blockGridWidth * blockCellSize) — there's no separate radius/width tunable any more.")]
    [Range(4, 128)] public int blockCellSize = 34;
    [Tooltip("How many blocks wide the grid is (X axis). Also defines the sand area's total width in cells: GridWidth = blockGridWidth * blockCellSize. IGNORED whenever customPattern is assigned below — the pattern's own Width takes over instead, so the two can never fall out of sync.")]
    [Range(1, 20)] public int blockGridWidth = 5;
    [Tooltip("How many blocks tall the initial fill is (Y axis). The fill covers exactly blockGridHeight * blockCellSize rows from the bottom, tiled with Tetris-like piece shapes (I/O/T/L/2x3 rectangle — see SandCylinderSandGrid.FillInitialLayers) rather than one solid color per block. IGNORED whenever customPattern is assigned below — the pattern's own Height takes over instead.")]
    [Range(1, 30)] public int blockGridHeight = 7;
    [Tooltip("Optional hand-painted layout (create via Assets > Create > SandCylinderDemo > Sand Pattern, then paint it in its own Inspector) to use INSTEAD of a fresh randomized Tetris-piece layout every Play. Leave empty to keep the default random generator using blockGridWidth/blockGridHeight above. Once assigned, the pattern's OWN Width/Height fully take over as the grid size (see EffectiveBlockGridWidth/Height) — blockGridWidth/blockGridHeight above are ignored while a pattern is assigned, so resizing the pattern always resizes the whole sand area with it; there's nothing left to fall out of sync.")]
    public SandCylinderPatternData customPattern;

    [Header("Sand Density")]
    [Tooltip("Simulation cells per world unit. Drives vertical grid resolution from Sand Area Height, and converts GridWidth (in cells, from blockCellSize) back into a world-space size for rendering.")]
    [Range(10f, 120f)] public float sandDensity = 40f;

    [Header("Sand Colors")]
    [Tooltip("Existing 5-color palette. The initial block-grid fill's Tetris-like pieces and the cube color cycle both follow this order/length — see SandCylinderSandGrid.FillInitialLayers.")]
    public Color[] sandColors = new Color[] {
        new Color32(240, 235, 220, 255),
        new Color32(230, 70, 70, 255),
        new Color32(70, 130, 230, 255),
        new Color32(240, 200, 50, 255),
        new Color32(90, 200, 120, 255),
    };

    [Header("Extraction")]
    [Tooltip("Cells of matching color removed from the cylinder per second while a cube is actively extracting (per cube, now that multiple cubes extract concurrently — see SandExtractionController). Doubled ceiling (was 800 max) alongside maximumSandFlowRate to allow an even denser pull.")]
    [Range(1f, 2000f)] public float sandExtractionRate = 1200f;
    [Tooltip("Hard cap on cells removed in a single simulation tick, regardless of accumulated rate (avoids extraction 'popping' after a frame hitch). Doubled ceiling (was 200 max) in step with sandExtractionRate, keeping the same relative burst-smoothing ratio.")]
    [Range(1, 500)] public int maximumSandFlowRate = 240;
    [Tooltip("Hard cap on cells removed from a SINGLE grid column in one tick, even if that column has more matching cells than this (anywhere in it, not just a contiguous run — see SandCylinderSandGrid.ExtractColor) and the overall rate budget allows more. Without this, a single column could vacuum an entire vertical color block in one frame — gravity/settle can't visually smooth over a hole that large that fast, which reads as sand 'popping'/jumping rather than draining smoothly. Since a cube's horizontal reach now always spans a whole block's many columns at once (see SandCylinderSandGrid.GetBlockColumnRange), the block still drains fully within the cube's dwell time even with this cap — it just spreads the draining across the block's width and several ticks instead of emptying one column instantly. Higher values drain faster but risk popping; lower values are smoother but slower.")]
    [Range(1, 64)] public int maxCellsPerColumnPerTick = 6;
    [Tooltip("Vertical range, upward from the cube's own height, in which it can extract matching-color sand. There is no horizontal counterpart: a cube's horizontal reach is always the ENTIRE block-grid column its current world X falls within (see SandCylinderSandGrid.GetBlockColumnRange) — a '1-unit cube' capturing every grain of a matching '1-unit' block in one pass is the whole point of the block-grid layout, and a partial/tunable horizontal window (the old design) never gave a moving cube enough time in any one column to drain it before moving on.")]
    [Range(0.1f, 20f)] public float extractionRangeY = 7f;
    [Tooltip("How many columns to each side of an extracted column also get an instant fall-priming pass the same frame (see SandCylinderSandGrid.PrimeColumnFalling), instead of waiting for the grid's next scheduled Step() tick. 0 = only the extracted column itself is primed — sand caving in sideways from a neighboring column still looks immediate to that column's OWN cells, but the neighbor's cells only start reacting on the next tick, which can read as a beat-late collapse right at the extraction point. Higher values prime more neighbor columns for a more instantly-responsive cave-in at the cost of a bit more per-extraction work.")]
    [Range(0, 4)] public int extractionNeighborPrimeRadius = 1;
    [Tooltip("When a just-extracted cell could be filled by EITHER its left or right neighbor's settled sand (both sides equally eligible to topple in), the probability the LEFT neighbor wins that slot. Without this, whichever side happened to be scanned first always won ties, so extracting a middle column between two solid-colored columns caved in from a fixed, scan-order-determined side instead of a random mix of both colors. 0.5 = fair coin flip each time (recommended for a mixed cave-in); 0 = right neighbor always wins; 1 = left neighbor always wins.")]
    [Range(0f, 1f)] public float extractionCaveInLeftBias = 0.5f;
    [Tooltip("How many consecutive EMPTY rows the instant fall-priming pass (see SandCylinderSandGrid.PrimeColumnFalling) will scan through above a just-extracted gap before giving up, instead of a fixed row count. Adaptive on purpose: a densely-packed column (e.g. SandCylinderDemo's Tetris fill) has no real empty run until it genuinely runs out of material, so priming keeps going and fully closes the gap no matter how tall the remaining pile is — a fixed row-count cap here reopened as visible speckled holes inside the pile under a fast extraction rate, because the cap stopped refilling before the dense material above could catch up. A mostly-empty column (e.g. SandMixDemo's pour shaft) hits a long empty run almost immediately, so priming stops there — protecting still-falling sand far above from an undue bonus gravity tick it has no business getting yet. Should rarely need changing: raise it only if a deliberately gappy/porous fill pattern makes priming give up on real, still-connected material too early.")]
    [Range(1, 16)] public int extractionPrimeMaxEmptyGap = 3;

    [Header("Cubes")]
    // No manual size tunable any more: a "1-unit cube" should visually be
    // exactly as wide as the "1-unit" block it fully drains in one pass
    // (see CubeMaximumCollectible) — CubeWorldSize below derives that
    // straight from blockCellSize/sandDensity, the same conversion
    // SandAreaWorldWidth uses for the whole grid's width.
    [Tooltip("Purely cosmetic render-size multiplier on top of CubeWorldSize — shrinks/grows how big the cube (and its count/percent label, which is parented to it and so scales with it automatically) LOOKS, without touching CubeWorldSize itself or anything derived from it (conveyor spacing, RearmCompletedCubesThatHaveLeftTheZone's clearDistance, block-matching). 1 = full logical size; 0.5 = rendered at half size.")]
    [Range(0.1f, 1f)] public float cubeVisualScale = 0.5f;

    [Header("Conveyor")]
    [Tooltip("Total X-axis width, in world units, of the conveyor track. Should be wider than the sand area's width (see SandAreaWorldWidth) so cubes clearly enter/exit outside its sides.")]
    [Range(1f, 15f)] public float conveyorWidth = 4.5f;
    [Tooltip("How far below the cylinder's base, in world units, the conveyor plane sits. Cube Y stays fixed at this height.")]
    [Range(0.1f, 6f)] public float conveyorHeight = 1.2f;
    [Tooltip("Z-axis offset of the conveyor from the cylinder's center. Negative brings the conveyor in front of (closer to the camera than) the cylinder.")]
    [Range(-5f, 5f)] public float conveyorZOffset = -0.8f;
    [Tooltip("How many cubes ride the conveyor. Fixed for the whole session — never spawned or destroyed after Init.")]
    [Range(2, 16)] public int cubeCount = 6;
    [Tooltip("World units/second cubes travel along X. Y and Z never change.")]
    [Range(0.5f, 20f)] public float cubeMovementSpeed = 4f;

    [Header("Extraction Particles")]
    [Tooltip("Individual sand grains spawned per second while a cube is actively extracting — a rate, not a per-tick burst size. Each grain still originates from a real removed sand cell. Doubled ceiling (was 80 max) in step with sandExtractionRate so the visual stream keeps pace with the faster removal instead of cells vanishing ahead of their grains.")]
    [Range(0.5f, 160f)] public float particlesPerExtraction = 80f;
    [Tooltip("World-unit size of each sand grain. Keep small — density comes from the emission rate above, not grain size.")]
    [Range(0.01f, 0.3f)] public float particleSize = 0.05f;
    [Tooltip("Upper clamp on a grain's free-fall time to the cube (see particleGravity) — keeps a grain spawned very far above its cube from taking an unreasonably long, slow-looking fall.")]
    [Range(0.05f, 2f)] public float particleLifetime = 0.9f;
    [Tooltip("Random positional jitter, in world units, applied around each grain's source cell so consecutive grains don't spawn from exactly the same point.")]
    [Range(0f, 0.3f)] public float particleSpread = 0.04f;
    [Tooltip("Downward gravity strength applied to falling grains (multiplier on Physics.gravity). A grain always starts at zero vertical velocity and free-falls from its source cell's own Y — this is what guarantees a grain's Y can never rise above the height it was extracted from. Higher values mean a faster, more clipped-looking drop; lower values a slower, floatier one.")]
    [Range(0.1f, 3f)] public float particleGravity = 1f;

    [Header("Sand Simulation")]
    [Tooltip("Simulation steps per second (overall sand-feel speed).")]
    [Range(1, 60)] public int sandSimulationSpeed = 36;

    [Header("Advanced (reused sand-feel knobs)")]
    [Range(1f, 6f)] public float maxFallCellsPerTick = 2f;
    [Tooltip("Chance, per tick, that a grain with a free cell directly below it spills into a free diagonal cell instead of falling straight down. 0 keeps falls perfectly vertical (old behavior, and what makes an extraction shaft's cave-in look like a rigid straight column); higher values make a collapsing column mix into its neighbors as it drops instead.")]
    [Range(0f, 1f)] public float fallSidewaysMixChance = 0f;
    [Range(0f, 1f)] public float lateralSpreadChance = 0.2f;
    [Range(0f, 1f)] public float pileStability = 0.28f;
    [Range(1, 4)] public int minGapForFreeCascade = 2;
    [Range(0f, 1f)] public float settleJitterChance = 0.015f;
    [Range(1, 60)] public int settleJitterWindowTicks = 3;
    [Range(0f, 1f)] public float colorNoiseAmount = 0.14f;

    [Header("Active-Region Sub-Steps")]
    [Tooltip("Extra gravity passes per simulation tick (spread evenly across frames, so the rate is activeSubStepsPerTick x sandSimulationSpeed passes per second), run only inside the region where sand has actually been moving recently (see SandCylinderSandGrid.RunActiveSubSteps). Each pass is the ordinary StepCell rule in Step()'s own row order, so the sand's behavior is unchanged — a collapsing slope just gets more evaluations per second where it matters. 0 = off (original behavior). Without it a steep face sheds grains one at a time, which reads as slow repositioning and leaves short horizontal ledges of briefly-hanging grains along the face. Measured on SandSort_SandBuckets2_5x4 at sandSimulationSpeed 45 (Editor, M-series Mac): 90% of the repositioning flow took 26 s with 0, 5.8 s with 16 (~1.5 ms/frame), 5.3 s with 24 (~2.1 ms/frame), 5.0 s with 32 (~2.8 ms/frame). Cost scales with passes x active-region area.")]
    [Range(0, 64)] public int activeSubStepsPerTick = 0;
    [Tooltip("Cells added on every side of the recently-moving bounding box before the extra passes run. The box is built from cells that changed, so without a margin the cells about to start moving — which have not changed yet — are left out.")]
    [Range(0, 64)] public int activeSubStepMargin = 24;
    [Tooltip("How long, in seconds, the active region remembers where sand moved. A single frame's changes cover only the thin strip moving right now; remembering a short history keeps the whole collapsing slope inside the region.")]
    [Range(0.05f, 2f)] public float activeSubStepWindowSeconds = 0.5f;
    [Tooltip("HARD CEILING on sub-step work per frame, counted in StepCell evaluations (passes x active-region area). The rate above stays the target — activeSubStepsPerTick x sandSimulationSpeed passes per second, which is what keeps the flow speed framerate-independent — but a frame never runs more than this many evaluations, so a big collapse can no longer buy itself an unbounded frame. Needed because the pass count is proportional to Time.deltaTime: measured on device (Mi 9T, IL2CPP) a Canyon collapse escalated 0.4 -> 36 -> 72 -> 107 -> 158 ms over four frames, each slow frame earning the next one more passes, until the old passes-only cap (activeSubStepsPerTick x 8) stopped it at ~230 ms. Budgeting the WORK instead breaks that feedback: passes = min(rate target, maxCellsPerFrame / activeArea), floored at 1 so extraction's holes still close before the renderer draws. Deterministic — no wall-clock timing is involved. Calibration: ~60-80k evaluations per millisecond on a Mi 9T, so 400000 is roughly 5-7 ms. 0 = no ceiling (the old unbounded behaviour; diagnostic A/B only, do not ship).")]
    [Min(0)] public int maxCellsPerFrame = 400000;

    // The actual block-grid dimensions in use — blockGridWidth/blockGridHeight
    // UNLESS customPattern is assigned, in which case the pattern's own
    // Width/Height take over completely. This is the single place that
    // decision is made; every other piece of code (GridWidth,
    // SandCylinderSandGrid.FillInitialLayers, GetBlockColumnRange,
    // RearmCompletedCubesThatHaveLeftTheZone's blockWidthWorld) reads
    // through here rather than checking customPattern itself, so there is
    // no second place that could disagree about which size is "real" — the
    // bug this was added to fix was exactly that disagreement: a custom
    // pattern resized to 5x7 while blockGridWidth/blockGridHeight were still
    // sitting at an old 10x10 left the sand area's actual background/quad
    // size (which read blockGridWidth/blockGridHeight directly) stuck at
    // 10x10 while only the painted sand itself shrank to 5x7.
    public int EffectiveBlockGridWidth => Mathf.Max(1, customPattern != null ? customPattern.width : blockGridWidth);
    public int EffectiveBlockGridHeight => Mathf.Max(1, customPattern != null ? customPattern.height : blockGridHeight);

    // Cell count, not world size — the grid's actual authoritative width.
    // Exactly EffectiveBlockGridWidth blocks wide, each blockCellSize cells
    // across, so it's always an exact multiple with no rounding remainder
    // (unlike the old cylinderRadius-derived formula, block column
    // boundaries are now just blockCol * blockCellSize — see
    // SandCylinderSandGrid.GetBlockColumnRange).
    public int GridWidth => EffectiveBlockGridWidth * blockCellSize;
    public int GridHeight => Mathf.Max(4, Mathf.RoundToInt(cylinderHeight * sandDensity));

    // World-unit width of the sand area/quad — derived FROM the fixed cell
    // grid (GridWidth / cells-per-world-unit) rather than the other way
    // around, now that blockCellSize (not a world-space radius) is the
    // source of truth for how wide the grid is.
    public float SandAreaWorldWidth => GridWidth / Mathf.Max(1f, sandDensity);

    // Exactly one grid block's cell count (blockCellSize squared) — a
    // '1-unit cube' collecting a full '1-unit block' is now literal instead
    // of an arbitrary tunable ceiling.
    public int CubeMaximumCollectible => blockCellSize * blockCellSize;

    // World-unit edge length of each extraction cube — one block's cell
    // width converted to world units, the same blockCellSize / sandDensity
    // conversion SandAreaWorldWidth uses for the whole grid. A "1-unit
    // cube" is now literally as wide as the "1-unit" block it fully drains.
    public float CubeWorldSize => blockCellSize / Mathf.Max(1f, sandDensity);

    // World-unit distance, along X, between consecutive cubes on the
    // conveyor — derived from conveyorWidth/cubeCount rather than an
    // independent tunable, so cubeCount cubes are ALWAYS exactly evenly
    // spaced around the full conveyor loop with no gap, regardless of what
    // either value is set to. A free-standing cubeSpacing tunable could
    // disagree with conveyorWidth (e.g. cubeCount * cubeSpacing exceeding
    // conveyorWidth) — SpawnCube's Mathf.Repeat would then wrap a cube's
    // starting position back around near an earlier cube's, and since cubes
    // are now sized to match a real grid block (CubeWorldSize) rather than
    // an arbitrarily small placeholder, that wrap-collision became a
    // visible overlap instead of an unnoticed one.
    public float CubeSpacingWorld => conveyorWidth / Mathf.Max(1, cubeCount);
}
