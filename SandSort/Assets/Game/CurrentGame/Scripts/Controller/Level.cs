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

    LevelSO _levelSO;
    public LevelSO levelSO => _levelSO;
    public Bounds levelBounds => _cameraFramed ? _cameraBounds : computeLevelBounds();

    // The level area the camera frames, measured ONCE right after the level is built — see
    // setupCamera. Nothing that moves later (dragged shapes, sand grains) can change it.
    Bounds _cameraBounds;
    bool _cameraFramed;

    SandCylinderSandGrid _sandGrid;
    SandExtractionController _sandExtraction;
    Board _board;
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

        if (sandLevel.validateLevel(_sandPaletteColors, out string validationMessage)) {
            Debug.Log($"[Level::initialize] Level validation passed: {validationMessage}");
        } else {
            Debug.LogWarning($"[Level::initialize] Level validation issues: {validationMessage}");
        }

        buildSandArea(sandLevel);
        buildBoard(sandLevel);
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
        _sandTunables.customPattern = sandLevel.sandPattern;

        Transform sandQuad = buildSandQuad();

        _sandGrid = sandQuad.gameObject.AddComponent<SandCylinderSandGrid>();
        _sandGrid.Init(_sandTunables);
        _sandGrid.FillInitialLayers();

        SandCylinderRenderer sandRenderer = sandQuad.gameObject.AddComponent<SandCylinderRenderer>();
        sandRenderer.Init(sandQuad.GetComponent<MeshRenderer>(), _sandMaterial);

        GameObject controllerObject = new GameObject("SandExtractionController");
        controllerObject.transform.SetParent(transform, false);
        _sandExtraction = controllerObject.AddComponent<SandExtractionController>();

        Vector3 sandAreaBottomWorld = transform.position + Vector3.down * (_sandTunables.cylinderHeight * 0.5f);
        _sandExtraction.InitWithoutConveyor(_sandGrid, sandAreaBottomWorld, _sandTunables.SandAreaWorldWidth, _cubeMaterial);
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

    // The Board is placed from the sand area's geometry so that the demo's own world<->grid
    // conversions (SandExtractionController's WorldXToGridColumn / VerticalRowRange) see a top-row
    // cell exactly where they used to see a conveyor cube:
    //  - cell size = CubeWorldSize (one sand block's world width) and the board's left edge = the
    //    sand quad's left edge, so column c's center X falls inside sand block c;
    //  - top row center Y = sand bottom - conveyorHeight (the demo's conveyor Y), so a top-row
    //    cell reaches the same bottom band of sand (extractionRangeY above it) the demo cube did.
    // The resulting gap between the board's top edge and the sand (conveyorHeight - cellSize / 2)
    // is the demo's geometry, deliberately left unchanged until extraction is verified against it.
    void buildBoard(SandLevelSO sandLevel) {
        float cellSize = _sandTunables.CubeWorldSize;
        float sandBottomY = -_sandTunables.cylinderHeight * 0.5f;
        float sandLeftX = -_sandTunables.SandAreaWorldWidth * 0.5f;
        float topRowCenterY = sandBottomY - _sandTunables.conveyorHeight;

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

        const float padding = 1f;

        Bounds bounds = levelBounds;
        float distance = 10f;

        camera.orthographic = true;
        camera.transform.rotation = Quaternion.identity;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - distance);

        float halfHeight = bounds.extents.y + padding;
        float halfWidthAsHeight = (bounds.extents.x + padding) / Mathf.Max(0.01f, camera.aspect);
        camera.orthographicSize = Mathf.Max(halfHeight, halfWidthAsHeight);

        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = distance + bounds.size.z + padding * 2f;
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
            foreach (ContainerData data in group) totalAreaUnits += data.cells.Count;

            int[] floorShares = new int[group.Count];
            float[] fractions = new float[group.Count];
            int assigned = 0;

            for (int i = 0; i < group.Count; i++) {
                float exactShare = totalAreaUnits > 0 ? (float)totalCells * group[i].cells.Count / totalAreaUnits : 0f;
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

            // Plain empty GameObject, not CreatePrimitive: Container builds one child cube per
            // occupied shape cell itself (see Container.buildShapeVisuals) so multi-cell shapes
            // (2x1, L, T, ...) render correctly — a primitive root would add an extra untouched
            // default cube on top of those.
            GameObject containerObject = new GameObject($"Container_{data.color}");
            containerObject.transform.SetParent(_board.transform, false);

            Material colorMaterial = containerMaterialOf(data.color);
            if (colorMaterial == null) {
                Debug.LogWarning($"[Level::buildContainers] No ColorSO/material for {data.color} on the Level prefab — tinting a default material with the sand color instead.");
            }

            Container container = containerObject.AddComponent<Container>();
            container.initialize(_board, data, capacityByData[data], sandColorIndex, colorMaterial, sandColor, _sandExtraction, _gameplayTunables);
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
