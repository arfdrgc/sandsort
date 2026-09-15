using Moow;
using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour, ILevel {

    // The sand is SandCylinderDemo's approved system, reused as-is (see buildSandArea). These are
    // the same inputs SandCylinderDemoBootstrap takes: a SandCylinderTunables component (on this
    // prefab, values copied from SandCylinderDemoScene) and the two pre-authored materials —
    // serialized rather than Shader.Find, see SandCylinderRenderer's Android note.
    [Header("Sand (SandCylinderDemo)")]
    [SerializeField] SandCylinderTunables _sandTunables;
    // Gameplay feel/balance knobs. Kept apart from _sandTunables on purpose: that one is the sand
    // simulation's own component and must stay free of gameplay concepts so SandCylinderDemo can be
    // lifted into another project on its own. See GameplayTunables' class header.
    [SerializeField] GameplayTunables _gameplayTunables;
    [SerializeField] Material _sandMaterial;
    [SerializeField] Material _cubeMaterial;
    // Only ever used to read a pixel-art PNG's colours into sand colour SLOT indices (see
    // SandPatternTextureConverter). It is NOT the authority for what the sand looks like: the
    // rendered colours still come from _sandTunables.sandColors and the slot -> ItemColor mapping
    // still comes from _sandPaletteColors below. Nothing here writes into either.
    [SerializeField] SandPaletteSO _sandPalette;

    // The level's own uniformly-scaled copy of the level SO's sand pattern, when the board's width
    // differs from the pattern's (see fitSandAreaToBoard). Runtime-only and owned here: the pattern
    // ASSET is shared by every level in the project and is never written to. Null when the pattern
    // already matches, in which case the asset itself is handed over as-is. Also holds the pattern
    // built from a level's pixel-art PNG (tryFitSandAreaFromTexture) — that one is always a fresh
    // instance, so it needs the same OnDestroy cleanup and gets it for free here.
    SandCylinderPatternData _runtimePattern;
    // cylinderHeight's own Inspector range cap. The derived sand height is kept inside it.
    const float SAND_AREA_MAX_HEIGHT = 10f;

    // Fixed design angle for the gameplay camera, in degrees about X. Negative pitches the camera
    // up, so it sits below the level and looks up at it — the angle the board's boxes read best at.
    // Found by hand in Play; everything else about the camera is derived from it and the bounds.
    const float CAMERA_PITCH = -20f;

    // Margin kept around the level inside the frame, in world units, on whichever of the camera's
    // two axes ends up binding. 0.7 rather than the 1.0 the straight-on camera used: at a phone's
    // aspect the level is bound by its WIDTH, so this constant alone sets the zoom, and 0.7 is what
    // lands on the framing found by hand in Play (orthographicSize ~ 8).
    const float CAMERA_PADDING = 0.7f;

    // How much empty space to keep in front of the nearest content along the camera's own Z. Under
    // an orthographic projection this cannot change the picture at all (see setupCamera) — it only
    // buys clearance for the near plane, so dragged shapes and flying sand grains cannot clip.
    const float CAMERA_CLEARANCE = 10f;

    // _sandPaletteColors[i] is the gameplay ItemColor of SandCylinderTunables.sandColors[i], i.e. of
    // sand grid color index i + 1 (SandCylinderSandGrid reserves 0 for EMPTY). Default order
    // matches the demo palette: cream, red, blue, orange, green.
    [SerializeField] ColorSO.ItemColor[] _sandPaletteColors = {
        ColorSO.ItemColor.WHITE,
        ColorSO.ItemColor.RED,
        ColorSO.ItemColor.BLUE,
        ColorSO.ItemColor.ORANGE,
        ColorSO.ItemColor.GREEN,
    };

    // Container look per gameplay color, through MoowCore's own ItemColor -> material type
    // (ColorSO._objectiveItemMaterial). Each entry points at a MoowCore/Materials/Basic_Colors
    // material; see containerMaterialOf.
    [Header("Containers")]
    [SerializeField] List<ColorSO> _containerColors = new();

    // The three-piece modular frame kit (Docs/FRAME_KIT.md), assembled at runtime by BoardFrame so
    // the rim follows the level's real grid and sand dimensions. These are the imported FBX model
    // prefabs themselves — Board_FrameEdge / Board_FrameCorner / Board_FrameTJunction — not wrapper
    // prefabs; BoardFrame applies the coordinate-system rotation on each instance the same way
    // Shape_*.prefab's FBX_Placeholder does. The old fixed-size Board_Frame.fbx is not used.
    [Header("Board frame (modular kit — Docs/FRAME_KIT.md)")]
    [SerializeField] GameObject _frameEdgePrefab;
    [SerializeField] GameObject _frameCornerPrefab;
    [SerializeField] GameObject _frameTJunctionPrefab;

    LevelSO _levelSO;
    public LevelSO levelSO => _levelSO;
    public Bounds levelBounds => _cameraFramed ? _cameraBounds : computeLevelBounds();

    // The level area the camera frames, measured ONCE right after the level is built — see
    // setupCamera. Nothing that moves later (dragged shapes, sand grains) can change it.
    Bounds _cameraBounds;
    bool _cameraFramed;

    SandCylinderSandGrid _sandGrid;
    SandExtractionController _sandExtraction;
    // The sand area's bottom edge in WORLD space — the exact Vector3 handed to
    // SandExtractionController.InitWithoutConveyor, i.e. the anchor its own WorldYToGridRow /
    // GridCellToWorld treat as sand row 0. Kept so ExtractionGrid can anchor its vertical extraction
    // band to the sand instead of to the board; see ExtractionGrid's EXTRACTION BAND note.
    Vector3 _sandAreaBottomWorld;
    Board _board;
    BoardFrame _boardFrame;
    readonly List<Container> _containers = new();

    float _timeRemaining;
    bool _resolved;

    public void initialize(LevelSO levelSO) {
        _levelSO = levelSO;

        SandLevelSO sandLevel = levelSO as SandLevelSO;
        if (sandLevel == null) {
            Debug.LogError("[Level::initialize] Assigned LevelSO is not a SandLevelSO — cannot build the Sand Idea core loop.");
            this.dispatchEvent<object>(Events.LEVEL_READY_TO_PLAY, null);
            return;
        }

        if (_sandTunables == null) {
            Debug.LogError("[Level::initialize] Level prefab has no SandCylinderTunables assigned — cannot build the sand area.");
            this.dispatchEvent<object>(Events.LEVEL_READY_TO_PLAY, null);
            return;
        }

        if (sandLevel.validateLevel(_sandPaletteColors, _sandPalette, out string validationMessage)) {
            Debug.Log($"[Level::initialize] Level validation passed: {validationMessage}");
        } else {
            Debug.LogWarning($"[Level::initialize] Level validation issues: {validationMessage}");
        }

        buildSandArea(sandLevel);
        buildBoard(sandLevel);
        // After both, because the frame is measured FROM the sand quad and the board floor; before
        // computeFramingBounds only so the hierarchy reads top-down. The frame is deliberately NOT
        // part of the camera bounds: it adds 0.19 outside the sand/floor rectangle, well inside
        // CAMERA_PADDING's 0.7, so it is always visible without moving the framing that was found by
        // hand in Play.
        buildBoardFrame();
        buildContainers(sandLevel);

        _cameraBounds = computeFramingBounds();
        _cameraFramed = true;

        _timeRemaining = sandLevel.timerSeconds;
        _resolved = false;

        this.dispatchEvent<object>(Events.LEVEL_READY_TO_PLAY, null);

        setupCamera();
    }

    // Mirrors SandCylinderDemoBootstrap.Start() step for step: sand quad -> SandCylinderSandGrid
    // (Init + FillInitialLayers) -> SandCylinderRenderer -> SandExtractionController. The bootstrap
    // itself can't be reused: it builds in its own Start() (too late — ILevel.initialize already
    // needs the grid for capacities) and always starts the conveyor. The only differences here are
    // that the level's own pattern is assigned first, and the controller starts without its conveyor
    // (each Container's ExtractionGrid drives extraction instead).
    void buildSandArea(SandLevelSO sandLevel) {
        fitSandAreaToBoard(sandLevel);

        Transform sandQuad = buildSandQuad();

        _sandGrid = sandQuad.gameObject.AddComponent<SandCylinderSandGrid>();
        _sandGrid.Init(_sandTunables);
        _sandGrid.FillInitialLayers();

        SandCylinderRenderer sandRenderer = sandQuad.gameObject.AddComponent<SandCylinderRenderer>();
        sandRenderer.Init(sandQuad.GetComponent<MeshRenderer>(), _sandMaterial);

        GameObject controllerObject = new GameObject("SandExtractionController");
        controllerObject.transform.SetParent(transform, false);
        _sandExtraction = controllerObject.AddComponent<SandExtractionController>();

        _sandAreaBottomWorld = transform.position + Vector3.down * (_sandTunables.cylinderHeight * 0.5f);
        _sandExtraction.InitWithoutConveyor(_sandGrid, _sandAreaBottomWorld, _sandTunables.SandAreaWorldWidth, _cubeMaterial);
    }

    // UNIFORM SAND SCALING (2026-09-13). The board's column count is the input; the sand area follows
    // it in BOTH axes by the same factor, so a wider grid gets a bigger sand area rather than a
    // stretched one.
    //
    // Why it has to happen here and not in the sand: the width was already derived
    // (SandAreaWorldWidth = EffectiveBlockGridWidth * CubeWorldSize, and EffectiveBlockGridWidth is
    // the pattern's own block width), so the sand area has always scaled itself — the input was just
    // authored by hand and drifted from boardSize.x. The HEIGHT was not derived at all: the sand
    // area's world height IS cylinderHeight. So scaling uniformly means deriving cylinderHeight too,
    // which is geometry, not physics: sandDensity stays 40, so a cell is still 1/40 of a world unit
    // and every rate, the accumulator and extractionRangeY are untouched — the column just has more
    // rows. Deriving it from the block height also makes GridHeight an exact multiple of
    // blockCellSize (CubeWorldSize * sandDensity == blockCellSize exactly), so FillInitialLayers'
    // height clamp lands dead on and nothing is clipped or left as an empty band at the top.
    //
    // Both writes land on the Level prefab's own SandCylinderTunables INSTANCE (LevelGenerator
    // instantiates the prefab), never on an asset.
    void fitSandAreaToBoard(SandLevelSO sandLevel) {
        int targetWidth = Mathf.Max(1, sandLevel.boardSize.x);

        // A pixel-art PNG takes precedence over a hand-painted pattern (see SandLevelSO's _sandTexture
        // comment). Everything below this point is the original pattern path, unchanged — it is what
        // a level with no texture, or one whose texture could not be converted, still runs.
        if (sandLevel.sandTexture != null && tryFitSandAreaFromTexture(sandLevel, targetWidth)) return;

        SandCylinderPatternData source = sandLevel.sandPattern;
        if (source == null) {
            // No pattern: the sand falls back to its own blockGridWidth/Height and its authored
            // cylinderHeight, exactly as before.
            _sandTunables.customPattern = null;
            return;
        }

        int sourceWidth = Mathf.Max(1, source.width);
        int sourceHeight = Mathf.Max(1, source.height);

        // ONE factor for both axes. Width comes out exact by construction (the target IS the column
        // count); height is rounded, because a pattern's height is a whole number of blocks — a block
        // (CubeWorldSize on a side) is the finest the fill can be cut, subdivisionsPerBlock only
        // divides WITHIN a block. Rounding is therefore the smallest achievable aspect error:
        // 5x5 -> 7x7 is exact, 5x7 -> 7x9.8 lands on 7x10 (+2 %).
        float scale = targetWidth / (float)sourceWidth;
        int targetHeight = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));

        // cylinderHeight is a Range(1, 10) field; keep the derived value inside the range its own
        // Inspector allows rather than silently writing past it.
        int maxHeight = maxSandHeightBlocks;
        if (targetHeight > maxHeight) {
            Debug.LogWarning($"[Level::fitSandAreaToBoard] Uniform scale {scale:0.###} would need {targetHeight} blocks of sand height ({targetHeight * _sandTunables.CubeWorldSize:0.##} world units), past cylinderHeight's {SAND_AREA_MAX_HEIGHT} cap — clamped to {maxHeight}. The sand area is no longer a uniform scale of the pattern.");
            targetHeight = maxHeight;
        }

        // Same size: hand over the asset itself. Nothing is written to it either way, but this keeps
        // the common case allocation-free.
        _sandTunables.customPattern = targetWidth == sourceWidth && targetHeight == sourceHeight
            ? source
            : _runtimePattern = scalePatternUniformly(source, targetWidth, targetHeight, _sandTunables.blockCellSize);

        _sandTunables.cylinderHeight = targetHeight * _sandTunables.CubeWorldSize;
    }

    // How tall, in blocks, the sand area is allowed to get. cylinderHeight carries a Range(1, 10) on
    // its own Inspector field, so a derived value has to stay inside it rather than silently write
    // past it. Shared by both starting-picture paths so the cap can only ever be defined once.
    int maxSandHeightBlocks => Mathf.Max(1, Mathf.FloorToInt(SAND_AREA_MAX_HEIGHT / _sandTunables.CubeWorldSize));

    // The PNG path. Returns false when the picture could not be converted — a missing palette, an
    // unreadable texture, an off-palette pixel — and the caller then carries on to the hand-painted
    // pattern rather than leaving the level with no sand at all. Deliberately loud about it: a level
    // that quietly renders a different picture than the one authored is worse than one that says so.
    //
    // What it writes is exactly what the pattern path writes — customPattern, cylinderHeight, and
    // _runtimePattern for the OnDestroy cleanup that already existed — so from the next line onward
    // nothing downstream can tell a PNG-authored level from a hand-painted one. The sand simulation
    // and the extraction never learn which one it was.
    bool tryFitSandAreaFromTexture(SandLevelSO sandLevel, int targetWidth) {
        if (_sandPalette == null) {
            Debug.LogError("[Level::tryFitSandAreaFromTexture] The level has a sand texture but the Level prefab has no SandPaletteSO assigned — cannot tell which colour is which sand slot. Falling back to the level's sand pattern.");
            return false;
        }

        if (!SandPatternTextureConverter.tryBuildPattern(
                sandLevel.sandTexture, _sandPalette, targetWidth,
                _sandTunables.blockCellSize, maxSandHeightBlocks,
                out SandCylinderPatternData built, out string error)) {
            Debug.LogError($"[Level::tryFitSandAreaFromTexture] {error} Falling back to the level's sand pattern.");
            return false;
        }

        _runtimePattern = built;
        _sandTunables.customPattern = built;
        _sandTunables.cylinderHeight = built.height * _sandTunables.CubeWorldSize;
        return true;
    }

    // A runtime-only copy of `source`, its picture zoomed uniformly to fill targetWidth x targetHeight
    // BLOCKS. The asset is only ever read.
    //
    // RESOLUTION IS THE WHOLE POINT. Resampling at the pattern's own block/sub-block resolution looks
    // like it works — the aspect ratio comes out right — but it RE-QUANTISES the picture instead of
    // zooming it: at 5 -> 7 blocks some source cells double and some don't, so shape edges move by up
    // to half a block (0.425 world) and straight edges go ragged. Measured against a true 1.4x zoom,
    // 17.9 % of the simulation's cells came out the wrong colour.
    //
    // So the clone is built at SIMULATION resolution: subdivisionsPerBlock = blockCellSize makes the
    // pattern's paint grid exactly the sand grid (blocks * blockCellSize cells per side), and
    // PaintFromPattern's own subCellSize = blockCellSize / subdivisionsPerBlock then comes out at 1
    // cell. The edge quantum drops from 0.425 to 0.025 world, the error to 0. Nothing in
    // SandCylinderDemo changes; this just hands its existing painter a pattern at its own resolution.
    // (subdivisionsPerBlock carries a [Range(1, 4)] for its Inspector, which is an editor clamp only —
    // this clone is never an asset and never opened in that Inspector.)
    //
    // Nearest-neighbour, and it has to be: the bytes are colour SLOT INDICES into the sand's palette,
    // so averaging slot 2 and slot 4 into slot 3 would invent a third colour rather than blend.
    // Sampling is centred — floor((d + 0.5) / cellsPerSourceCell) — so the picture is not shifted
    // half a source cell toward the low edge.
    //
    // ONE factor drives both axes. Width fills exactly by construction; height uses that same factor
    // rather than its own, so the picture is never stretched when targetHeight had to be rounded to a
    // whole block (5x7 -> 7x9.8 lands on 7x10). Whatever is left over stays EMPTY at the TOP, which
    // is the sand's surface — never at the bottom, where the extraction band reads.
    //
    // Written through the pattern's own public API (fields + EnsureSized + SetCell) because `cells` is
    // private to it; no Resize() anywhere, since that one crops and pads rather than resamples.
    public static SandCylinderPatternData scalePatternUniformly(
        SandCylinderPatternData source, int targetWidth, int targetHeight, int blockCellSize) {
        SandCylinderPatternData scaled = ScriptableObject.CreateInstance<SandCylinderPatternData>();
        scaled.name = $"{source.name} (Scaled {targetWidth}x{targetHeight})";
        scaled.width = Mathf.Max(1, targetWidth);
        scaled.height = Mathf.Max(1, targetHeight);
        scaled.subdivisionsPerBlock = Mathf.Max(1, blockCellSize);
        scaled.EnsureSized();

        int sourcePaintWidth = source.PaintWidth;
        int sourcePaintHeight = source.PaintHeight;
        int paintWidth = scaled.PaintWidth;
        int paintHeight = scaled.PaintHeight;
        if (sourcePaintWidth <= 0 || sourcePaintHeight <= 0) return scaled;

        // Destination cells per source cell — the zoom, in simulation cells. Taken from the WIDTH,
        // which is the axis the board fixes, and then used for the height too.
        float cellsPerSourceCell = paintWidth / (float)sourcePaintWidth;

        for (int y = 0; y < paintHeight; y++) {
            int sourceY = Mathf.FloorToInt((y + 0.5f) / cellsPerSourceCell);
            if (sourceY >= sourcePaintHeight) continue;  // rounding remainder: empty, and at the top
            sourceY = Mathf.Max(0, sourceY);
            for (int x = 0; x < paintWidth; x++) {
                int sourceX = Mathf.Clamp(
                    Mathf.FloorToInt((x + 0.5f) / cellsPerSourceCell), 0, sourcePaintWidth - 1);
                scaled.SetCell(x, y, source.GetCell(sourceX, sourceY));
            }
        }
        return scaled;
    }

    void OnDestroy() {
        if (_runtimePattern != null) Destroy(_runtimePattern);
    }

    // Identical to SandCylinderDemoBootstrap.BuildSandQuad.
    Transform buildSandQuad() {
        GameObject quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObject.name = "SandCrossSection";
        Collider collider = quadObject.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        Transform quad = quadObject.transform;
        quad.SetParent(transform, false);
        quad.localPosition = Vector3.zero;
        quad.localRotation = Quaternion.identity;
        quad.localScale = new Vector3(_sandTunables.SandAreaWorldWidth, _sandTunables.cylinderHeight, 1f);
        return quad;
    }

    // The Board is placed from the sand area's geometry, in the board's own unit (cellSize), so that
    // the demo's own world<->grid conversions (SandExtractionController's WorldXToGridColumn /
    // VerticalRowRange) still resolve a top-row cell against the sand's bottom band:
    //  - cell size = CubeWorldSize (one sand block's world width) and the board's left edge = the
    //    sand quad's left edge, so column c's center X falls inside sand block c;
    //  - top row center Y = sand bottom - (gridSandGapCells + 0.5) * cellSize, so the GAP between the
    //    board's top edge and the sand is exactly gridSandGapCells cells. The extra half cell is the
    //    conversion from the board's top EDGE to the top row's CENTER, which is what this variable
    //    holds: at the default 0.5 that comes to sand bottom - one full cell, i.e. a half-cell gap.
    //
    // This used to be sand bottom - conveyorHeight (the demo's conveyor Y), which pinned the board to
    // a SAND-side number: conveyorHeight is the clearance the demo needs so its cubes can travel
    // UNDER the sand, not a gameplay distance. It left a gap of conveyorHeight - cellSize / 2
    // (0.775 world units at 1.2 / 0.85) — nearly a full cell of dead space — and made the board move
    // whenever conveyor geometry was retuned. Measuring the gap in CELLS instead keeps board and sand
    // on the same scale (the cell's world size still comes from the sand) while letting the gap be
    // tuned as the gameplay/layout value it actually is (2026-09-12).
    //
    // Extraction does NOT depend on this any more (2026-09-12). It briefly did: because
    // VerticalRowRange is one-sided — it reaches from the Y it is handed up to extractionRangeY above
    // it — feeding it the top row's own Y spent part of that range crossing this cosmetic gap, so
    // shrinking the gap pushed the reachable band further up into the sand (13 -> 27 -> 38 rows as
    // gridSandGapCells went 0.9 -> 0.5 -> 0.15). ExtractionGrid now hands ExtractAtPoint the SAND's
    // bottom edge as the Y instead (X still comes from the cell), so the band is a fixed
    // [sandBottomY, sandBottomY + extractionRangeY] slice no matter where the board sits. See
    // ExtractionGrid's EXTRACTION BAND note. Consequently gridSandGapCells is a purely visual knob
    // and has no extraction invariant to respect — the Inspector's 1-cell cap is now only about
    // keeping the board from drifting absurdly far from the sand.
    void buildBoard(SandLevelSO sandLevel) {
        float cellSize = _sandTunables.CubeWorldSize;
        float sandBottomY = -_sandTunables.cylinderHeight * 0.5f;
        float sandLeftX = -_sandTunables.SandAreaWorldWidth * 0.5f;
        float gapCells = _gameplayTunables != null
            ? _gameplayTunables.gridSandGapCells
            : GameplayTunables.DEFAULT_GRID_SAND_GAP_CELLS;
        float topRowCenterY = sandBottomY - (gapCells + 0.5f) * cellSize;

        GameObject boardObject = new GameObject("Board");
        boardObject.transform.SetParent(transform, false);
        boardObject.transform.localPosition = new Vector3(
            sandLeftX + cellSize * 0.5f,
            topRowCenterY - (sandLevel.boardSize.y - 1) * cellSize,
            0f);

        // The floor's cell texture reuses the same serialized unlit material the sand quad uses
        // (Android-safe, see SandCylinderRenderer's note) — Board only copies it.
        _board = boardObject.AddComponent<Board>();
        _board.initialize(sandLevel.boardSize, cellSize, _sandMaterial);
    }

    // Builds the rim from the modular frame kit around the two rectangles the level has already
    // committed to: the board floor and the sand quad. It only READS them — buildBoard's placement,
    // buildSandArea's geometry, the extraction band and every gameplay value are untouched, which is
    // also why this runs last. See BoardFrame for the kit's own rules.
    void buildBoardFrame() {
        if (_frameEdgePrefab == null || _frameCornerPrefab == null || _frameTJunctionPrefab == null) {
            Debug.LogWarning("[Level::buildBoardFrame] The Level prefab is missing one of the frame kit pieces (Edge / Corner / T-Junction) — skipping the board frame.");
            return;
        }

        float cellSize = _sandTunables.CubeWorldSize;
        // The board's local position IS the centre of cell (0, 0), so the floor's rectangle is half
        // a cell out from it on the bottom-left. Same derivation Board.buildFloorVisual uses, kept
        // here rather than asking Board for bounds so the frame is built from the board's exact
        // grid extent and not from a Renderer's world bounds.
        Vector3 boardOrigin = _board.transform.localPosition;
        Rect gridWindow = new Rect(
            boardOrigin.x - cellSize * 0.5f,
            boardOrigin.y - cellSize * 0.5f,
            _board.size.x * cellSize,
            _board.size.y * cellSize);
        // The sand quad is centred on the level origin (buildSandQuad), so its rectangle follows
        // from its own two dimensions alone.
        Rect sandWindow = new Rect(
            -_sandTunables.SandAreaWorldWidth * 0.5f,
            -_sandTunables.cylinderHeight * 0.5f,
            _sandTunables.SandAreaWorldWidth,
            _sandTunables.cylinderHeight);

        GameObject frameObject = new GameObject("BoardFrame");
        frameObject.transform.SetParent(transform, false);
        _boardFrame = frameObject.AddComponent<BoardFrame>();
        _boardFrame.build(gridWindow, sandWindow, cellSize,
                          _frameEdgePrefab, _frameCornerPrefab, _frameTJunctionPrefab);
    }

    // Prototype camera setup: fits an orthographic camera to the level as built (sand area + Board
    // floor + Containers), rather than hand-tuning per-level transforms.
    //
    // Camera lock (2026-09-12): the framing bounds are measured once, in initialize, and cached in
    // _cameraBounds. Measuring live renderers every frame (as before) let dragged shapes and flying
    // sand grains pan and zoom the camera. The per-frame call below only re-applies that fixed
    // framing, so the camera cannot move once the level has started. The bounds are the sand area
    // plus the Board floor only (computeFramingBounds): measuring every child renderer at build time
    // framed the camera far too wide.
    //
    // Called every Update() tick (not just once) because MoowCore's generic
    // PerspectiveCameraController also reacts to LEVEL_READY_TO_PLAY and overwrites the camera
    // position with math tuned for the OLD "Pixel Loop Blast" world convention (board depth
    // along Z, a hardcoded -45 centerFactor) — whether that runs before or after our own call is
    // a Dispatcher timing detail we don't control. Re-asserting every frame makes ours win
    // unconditionally instead of depending on that ordering.
    //
    // Also re-asserts near/far clip planes every frame for the same reason: that generic
    // controller's UpdateCameraClippingPlanes() computes them from wherever ITS bad position math
    // put the camera that frame (sometimes hundreds of units away), and leaves them there even
    // after we move the camera back — which silently clips our entire scene out of view (nothing
    // renders but the background color) without moving the camera at all, the actual cause of an
    // earlier "only a solid color, nothing visible" regression caught during this session.
    void setupCamera() {
        Camera camera = Camera.main;
        if (camera == null) return;

        const float padding = CAMERA_PADDING;

        Bounds bounds = levelBounds;

        // The camera holds CAMERA_PITCH at every level size; only its position and orthographic size
        // are solved for. That means the framing can no longer be read off the world axes the way it
        // was while the camera was axis-aligned (bounds.extents.y as the half-height, extents.x as
        // the half-width, position straight down -Z): the moment the camera tilts, its own up and
        // right stop agreeing with world Y and X, and both of those numbers are wrong. So measure
        // the bounds in CAMERA SPACE instead — rotate all eight corners into the camera's basis and
        // take the largest extent on each of its axes. That is correct at any angle, including 0.
        Quaternion rotation = Quaternion.Euler(CAMERA_PITCH, 0f, 0f);
        Quaternion toCameraSpace = Quaternion.Inverse(rotation);

        Vector3 extents = bounds.extents;
        float halfRight = 0f, halfUp = 0f, halfDepth = 0f;
        for (int corner = 0; corner < 8; corner++) {
            Vector3 offset = new Vector3(
                (corner & 1) == 0 ? -extents.x : extents.x,
                (corner & 2) == 0 ? -extents.y : extents.y,
                (corner & 4) == 0 ? -extents.z : extents.z);
            Vector3 inCameraSpace = toCameraSpace * offset;
            halfRight = Mathf.Max(halfRight, Mathf.Abs(inCameraSpace.x));
            halfUp = Mathf.Max(halfUp, Mathf.Abs(inCameraSpace.y));
            halfDepth = Mathf.Max(halfDepth, Mathf.Abs(inCameraSpace.z));
        }

        // Same rule as before, now on the camera's own axes: fit the half-height, or the half-width
        // re-expressed as a half-height when the level is wider than the viewport.
        float halfWidthAsHeight = (halfRight + padding) / Mathf.Max(0.01f, camera.aspect);
        camera.orthographicSize = Mathf.Max(halfUp + padding, halfWidthAsHeight);

        // Orthographic: distance along the view axis does NOT scale the image, it only decides what
        // the near and far planes cut. So back off far enough to clear the deepest corner plus
        // CAMERA_CLEARANCE and let orthographicSize alone do the framing.
        float distance = halfDepth + CAMERA_CLEARANCE;

        camera.orthographic = true;
        camera.transform.rotation = rotation;
        camera.transform.position = bounds.center - rotation * Vector3.forward * distance;

        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = distance + halfDepth + padding * 2f;
    }

    // Area-based capacity model (see SandLevelSO's class header): each color's starting sand cell
    // count in SandCylinderSandGrid is split among all Containers of that color in proportion to
    // each Container's areaUnits (its shape's cell count), via the "largest remainder"
    // apportionment method: floor each exact share, then hand the cells lost to flooring one each
    // to the Containers with the largest fractional remainder first. This is the correct
    // generalization of simple equal-split-plus-remainder to weighted shares — e.g. a 4-cell
    // Container gets exactly twice the capacity of a 2-cell Container of the same color.
    void buildContainers(SandLevelSO sandLevel) {
        Dictionary<ColorSO.ItemColor, List<ContainerData>> byColor = new();
        foreach (ContainerData data in sandLevel.containers) {
            if (!byColor.TryGetValue(data.color, out List<ContainerData> group)) {
                group = new List<ContainerData>();
                byColor[data.color] = group;
            }
            group.Add(data);
        }

        Dictionary<ContainerData, int> capacityByData = new();
        foreach (KeyValuePair<ColorSO.ItemColor, List<ContainerData>> pair in byColor) {
            List<ContainerData> group = pair.Value;
            int totalCells = _sandGrid.GetColorCount(sandColorIndexOf(pair.Key));
            int totalAreaUnits = 0;
            foreach (ContainerData data in group) totalAreaUnits += data.occupiedCells.Count;

            int[] floorShares = new int[group.Count];
            float[] fractions = new float[group.Count];
            int assigned = 0;

            for (int i = 0; i < group.Count; i++) {
                float exactShare = totalAreaUnits > 0 ? (float)totalCells * group[i].occupiedCells.Count / totalAreaUnits : 0f;
                floorShares[i] = Mathf.FloorToInt(exactShare);
                fractions[i] = exactShare - floorShares[i];
                assigned += floorShares[i];
            }

            int leftover = totalCells - assigned;
            List<int> byFractionDesc = new();
            for (int i = 0; i < group.Count; i++) byFractionDesc.Add(i);
            // Stable sort (List.Sort isn't stable, so break ties by original index explicitly)
            // so equal fractions fall back to list order, matching the simple equal-split case.
            byFractionDesc.Sort((a, b) => {
                int cmp = fractions[b].CompareTo(fractions[a]);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            for (int i = 0; i < leftover; i++) {
                floorShares[byFractionDesc[i]] += 1;
            }

            for (int i = 0; i < group.Count; i++) {
                capacityByData[group[i]] = floorShares[i];
            }
        }

        foreach (ContainerData data in sandLevel.containers) {
            byte sandColorIndex = sandColorIndexOf(data.color);
            Color sandColor = sandColorIndex != SandCylinderSandGrid.EMPTY
                ? _sandTunables.sandColors[sandColorIndex - 1]
                : Color.magenta;

            // With a Shape prefab (Prefabs/Shapes/Shape_L4.prefab, ...) the piece is that prefab
            // instantiated and turned to the level's rotation — one canonical asset covers all four
            // orientations, see Shape.cs. applyRotation runs BEFORE Container.initialize so the
            // Container reads an already-rotated footprint, and it only turns the decorative half
            // (VisualRoot/FBX_Placeholder) and the FillUIAnchor; the root stays unrotated because
            // Container places its own per-cell visuals from the rotated cell data.
            //
            // Without one (the pre-Shape levels) it stays a plain empty GameObject, NOT a
            // CreatePrimitive: Container builds one child cube per occupied shape cell itself (see
            // Container.buildShapeVisuals) so multi-cell shapes render correctly — a primitive root
            // would add an extra untouched default cube on top of those.
            // Loud on purpose: this entry asked for a Shape and the prefab is gone, so the line
            // below is about to build the legacy `cells` footprint instead — an L4 turning into a
            // 1x1 with the level still "working". See ContainerData.hasMissingShapeReference.
            if (data.hasMissingShapeReference) {
                Debug.LogError($"[Level::buildContainers] {data.color} container at {data.position} points at a MISSING Shape prefab — building its legacy `cells` footprint ({data.cells.Count} cell(s)) instead. Reassign the Shape prefab.");
            }

            GameObject containerObject;
            if (data.shape != null) {
                Shape shape = Instantiate(data.shape, _board.transform, false);
                shape.applyRotation(data.rotation);
                shape.name = $"Container_{data.color}_{data.shape.type}_{data.rotation}";
                containerObject = shape.gameObject;
            } else {
                containerObject = new GameObject($"Container_{data.color}");
                containerObject.transform.SetParent(_board.transform, false);
            }

            Material colorMaterial = containerMaterialOf(data.color);
            if (colorMaterial == null) {
                Debug.LogWarning($"[Level::buildContainers] No ColorSO/material for {data.color} on the Level prefab — tinting a default material with the sand color instead.");
            }

            Container container = containerObject.AddComponent<Container>();
            container.initialize(_board, data, capacityByData[data], sandColorIndex, colorMaterial, sandColor, _sandExtraction, _sandAreaBottomWorld.y, _gameplayTunables, _sandMaterial);
            _containers.Add(container);
        }
    }

    Material containerMaterialOf(ColorSO.ItemColor color) {
        foreach (ColorSO colorSO in _containerColors) {
            if (colorSO != null && colorSO.color == color) return colorSO.objectiveItemMaterial;
        }
        return null;
    }

    // Sand grid color index (1-based, 0 = EMPTY/unmapped) for a gameplay ItemColor.
    byte sandColorIndexOf(ColorSO.ItemColor color) {
        int count = Mathf.Min(_sandPaletteColors.Length, _sandTunables.sandColors.Length);
        for (int i = 0; i < count; i++) {
            if (_sandPaletteColors[i] == color) return (byte)(i + 1);
        }
        return SandCylinderSandGrid.EMPTY;
    }

    void Update() {
        if (_board == null) return;

        setupCamera();

        if (_resolved) return;

        _timeRemaining -= Time.deltaTime;

        applyDepletionGuarantee();

        if (allContainersSealed()) {
            resolveWin();
            return;
        }

        if (_timeRemaining <= 0f) {
            resolveLose();
        }

        // Deadlock check intentionally off until Phase 3: the previous rule assumed sand stayed in
        // fixed columns, but SandCylinderSandGrid's sand falls and spreads between columns.
    }

    bool allContainersSealed() {
        foreach (Container container in _containers) {
            if (!container.isSealed) return false;
        }
        return true;
    }

    // Depletion-tolerance safeguard: a Container whose color has completely run out of sand is
    // pulled up to 100% and sealed on the spot, regardless of current fill — see
    // Container.forceComplete() and SandLevelSO's class header for why (capacity-split rounding).
    void applyDepletionGuarantee() {
        foreach (Container container in _containers) {
            if (container.isSealed) continue;
            if (_sandGrid.GetColorCount(container.sandColorIndex) <= 0) {
                container.forceComplete();
            }
        }
    }

    void resolveWin() {
        _resolved = true;
        this.dispatchEvent<object>(Events.LEVEL_COMPLETED, null);
    }

    void resolveLose() {
        _resolved = true;
        this.dispatchEvent<object>(Events.LEVEL_FAILED, null);
        this.dispatchEvent<object>(Events.FAIL_CONDITION_MET, null);
    }

    // What the camera must show: the sand area and every Board cell. Containers always sit on the
    // Board, so they are inside these bounds without being measured themselves.
    Bounds computeFramingBounds() {
        Bounds bounds = _sandGrid.GetComponent<Renderer>().bounds;
        bounds.Encapsulate(_board.floorBounds);
        return bounds;
    }

    Bounds computeLevelBounds() {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
