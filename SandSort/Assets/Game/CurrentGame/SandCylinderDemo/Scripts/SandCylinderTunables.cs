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
    [Tooltip("Height, in grid rows counted up from the sand's bottom row, of the outlet a collector removes sand from. The mouth is the collector's block columns x this many rows and nothing else — there is no reach above it (see SandCylinderSandGrid.ExtractAtMouth); sand higher up has to come down by the simulation first. Note: with more than 1 row, a frame that only gets a single simulation pass can draw the bottom row empty under a suspended row for that frame; sandSimulationSpeed at 2+ passes per frame closes it.")]
    [Range(1, 20)] public int extractionMouthRows = 10;

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
    [Tooltip("Simulation passes per second. One pass moves every grain that can move by one row, so this is the sand's fall speed in cells per second and the speed at which faces shed and piles settle. A frame in which sand is moving always gets at least one pass (so the effective rate is never below the frame rate) and never more than 16 or maxCellsPerFrame's worth; while the sand is at rest the simulation costs nothing at any value. Measured at 60 fps: 45 leaves a one-cell hanging row at the mouth during extraction and takes ~39 s to settle a large drain; 120 (two passes per frame) mostly closes the row and settles in ~20 s, ~10 s with restFriction 0.5.")]
    [Range(1, 240)] public int sandSimulationSpeed = 120;
    [Tooltip("When an empty cell has sand directly above it AND a supported grain diagonally above it, the chance the diagonal grain fills it instead of the one directly above. 0 = a hole always climbs straight up its own column (everything above a drained spot sinks as a rigid plug); higher values let the hole wander sideways as it rises, so the flow zone widens with height — and mixes colors more inside it.")]
    [Range(0f, 1f)] public float flowSpread = 0.5f;
    [Tooltip("Per-pass chance that a supported grain slides into a free cell diagonally below it. Re-rolled every pass, so it sets how fast a face sheds grains and keeps neighbouring grains from moving in lockstep; it is never a lasting per-grain decision. Measured: 1.0 settles about 30% faster than 0.6 but sheds rows in unison (small horizontal whiskers on the faces) and does not change the shape of a draining pit.")]
    [Range(0f, 1f)] public float slideChance = 0.6f;
    [Tooltip("EXPERIMENTAL (accepted at 0.5). Rest friction: chance, per pass, that a MARGINAL slide — a grain on top of its column dropping a single row onto sand, i.e. a step exactly two cells high — is refused, and a refused marginal slide does not keep the sand awake, so such steps can genuinely come to rest instead of always being ground down to a ruler-straight one-cell-per-column staircase. Taller steps are unaffected. Re-rolled every pass while the region is awake; nothing is stored per grain. 0 = off (the baseline policy exactly). Measured rest surface, adjacent column differences 0/1/2: 0 -> 2/167/0, 0.5 -> 31/74/64; values near 1 turn it into a regular steeper staircase again.")]
    [Range(0f, 1f)] public float restFriction = 0.5f;
    [Tooltip("HARD CEILING on simulation work per frame, counted in cell visits (passes x awake-region area). sandSimulationSpeed stays the target — that is what keeps the flow speed framerate-independent — but a frame never runs more than this, so a slow frame cannot buy the next one more passes and spiral (measured on device, Mi 9T IL2CPP, with the previous sand system: 0.4 -> 36 -> 72 -> 107 -> 158 ms over four frames before a work ceiling existed). Passes = min(rate target, maxCellsPerFrame / awake area), floored at 1 so a hole opened by extraction still gets a pass before it is drawn. Deterministic — no wall-clock timing is involved. 0 = no ceiling (diagnostic A/B only, do not ship).")]
    [Min(0)] public int maxCellsPerFrame = 400000;

    [Header("Sand Look")]
    [Tooltip("Brightness spread of the per-grain tint: every grain gets a random tint when it is created and carries it for life (see SandCylinderSandGrid.GetTint), and the renderer shades the palette color by up to +/- half this amount. Purely visual — the simulation never reads it.")]
    [Range(0f, 1f)] public float colorNoiseAmount = 0.14f;

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
