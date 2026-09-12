using System.Collections.Generic;
using UnityEngine;

// Hold-and-drag piece per game_mechanics.md's Controls section. Arbitrary multi-cell shapes (see
// SandLevelSO.ContainerData.cells) rather than rectangles only.
//
// The root GameObject holds no Renderer/Collider of its own — one small cube per occupied shape
// cell is built as a child instead, so irregular shapes (L, T, ...) render and can be clicked
// correctly without custom mesh generation. See buildShapeVisuals().
//
// Sand (2026-09-11): a Container never touches the sand itself. Its ExtractionGrid component (added
// in initialize) pulls sand through SandCylinderDemo's SandExtractionController and reports what it
// got via addCollected(); this class only tracks fill and seals at capacity.
//
// Movement (revised 2026-09-12) keeps two positions apart:
// - _visualAnchor (-> transform.position) is where the shape is drawn. While dragging it follows the
//   pointer freely and continuously, in any direction — no cell-by-cell stepping. Every frame it is
//   swept toward the pointer as one solid block against the Board's edges and every other
//   Container's cells (full cells, all shape cells), so it slides along other shapes and can never
//   overlap or pass through them however fast the pointer moves. See sweep().
// - _gridPosition is authoritative for gameplay: Board occupancy and ExtractionGrid read only it.
//   During a drag it is the cell nearest the visual. That cell is always free — a visual that
//   overlaps nothing rounds to a cell that overlaps nothing — so extraction can start mid-drag.
//   Releasing glides the visual onto that cell: the only snap, and only at drag end.
//
// Awake (2026-09-11): every Container starts asleep and never extracts while asleep, even when it
// starts in the top row. Pressing on it to drag (beginDrag) wakes it, and it stays awake from then
// on. ExtractionGrid extracts only while isAwake AND a shape cell is in the top row.
public class Container : MonoBehaviour {

    // Drag follow (a short exponential catch-up that smooths pointer jitter, capped so a pointer
    // jump still reads as motion rather than a teleport) and the release glide onto the committed
    // cell are player-facing feel, so they live in the GameplayTunables asset — see _tuning below.
    //
    // These two stay in code on purpose: SWEEP_SUBSTEP is the collision solver's resolution, not a
    // feel dial (coarser steps stop a shape short of what blocks it), and COLLISION_EPSILON is a
    // float comparison epsilon.
    const float SWEEP_SUBSTEP = 0.2f;
    const float COLLISION_EPSILON = 0.0001f;

    Board _board;
    // Not readonly on purpose: Unity's mid-Play domain reload (a script recompile while playing)
    // skips readonly fields, which would reset these to empty lists under a live Container.
    List<Collider> _colliders = new();
    // Parallel to _shape: the visual cube built for each occupied cell.
    List<Transform> _cellVisuals = new();
    // Other Containers' cells, gathered once per drag frame for the collision sweep.
    List<Vector2Int> _blockedCells = new();

    List<Vector2Int> _shape;
    // The Shape prefab component on this same GameObject, when the level spawned one (legacy levels
    // have none). Only used to push the fill readout at it — the footprint already arrived through
    // ContainerData.occupiedCells.
    Shape _shapeVisual;
    Vector2Int _gridPosition;
    // Gameplay feel knobs (drag + release snap). Read live rather than cached at initialize, so the
    // asset can be tuned in the Inspector while Play mode is running. Null-safe: a Level prefab with
    // no asset assigned falls back to the same values the asset defaults to.
    GameplayTunables _tuning;
    ColorSO.ItemColor _targetColor;
    byte _sandColorIndex;
    int _capacityUnits;
    int _filledUnits;
    bool _sealed;
    bool _awake;

    bool _dragging;
    // Corner slide in progress: the axis it is sliding on and the direction (+1/-1) it started in.
    // A sign of 0 means no slide is in progress — so a mid-Play domain reload zeroing these reads
    // as "not sliding", the safe state. See sweep().
    int _cornerLatchAxis;
    float _cornerLatchSign;
    // Pointer anchor minus visual anchor when the drag began, so grabbing a shape off-center
    // doesn't make it jump under the finger.
    Vector2 _grabOffset;
    // Where the shape is drawn, as a fractional anchor — see the Movement note above.
    Vector2 _visualAnchor;

    public Vector2Int gridPosition => _gridPosition;
    // Where the shape is actually drawn, in the same anchor units as gridPosition. gridPosition
    // rounds to the nearest cell, so the two differ by up to half a cell while dragging —
    // ExtractionGrid reads this to tell "committed to that cell" from "arrived there".
    public Vector2 visualAnchor => _visualAnchor;
    public List<Vector2Int> shape => _shape;
    public int areaUnits => _shape.Count;
    public ColorSO.ItemColor targetColor => _targetColor;
    // The same 1-based color index SandCylinderSandGrid stores (index into
    // SandCylinderTunables.sandColors + 1) — see Level.sandColorIndexOf.
    public byte sandColorIndex => _sandColorIndex;
    public bool isSealed => _sealed;
    public bool isAwake => _awake;
    public bool isDragging => _dragging;
    public int filledUnits => _filledUnits;
    public int capacityUnits => _capacityUnits;
    public int remainingCapacity => Mathf.Max(0, _capacityUnits - _filledUnits);
    public float fillLevel => _capacityUnits <= 0 ? 1f : (float)_filledUnits / _capacityUnits;

    public Transform cellVisual(int shapeIndex) => _cellVisuals[shapeIndex];

    float dragFollowSharpness => _tuning != null ? _tuning.dragFollowSharpness : GameplayTunables.DEFAULT_DRAG_FOLLOW_SHARPNESS;
    float dragMaxSpeedCells => _tuning != null ? _tuning.dragMaxSpeedCells : GameplayTunables.DEFAULT_DRAG_MAX_SPEED_CELLS;
    float releaseSnapSharpness => _tuning != null ? _tuning.releaseSnapSharpness : GameplayTunables.DEFAULT_RELEASE_SNAP_SHARPNESS;
    float releaseSnapMinSpeedCells => _tuning != null ? _tuning.releaseSnapMinSpeedCells : GameplayTunables.DEFAULT_RELEASE_SNAP_MIN_SPEED_CELLS;

    // capacityUnits is passed in explicitly (computed by Level.buildContainers from the sand
    // grid's starting color counts, weighted by areaUnits) rather than read from ContainerData —
    // see SandLevelSO's class header for the capacity model. 0 is a valid value: it means this
    // color has no matching sand anywhere, and forceComplete() (via Level's depletion-guarantee
    // check) will seal this Container on the very first Update tick.
    // colorMaterial is the MoowCore Basic_Colors material for data.color (resolved by Level through
    // that color's ColorSO). fallbackColor — the sand's own palette color — is only used to tint a
    // default material if no ColorSO/material is set up for this color.
    // sandBottomWorldY is passed straight through to ExtractionGrid (a Container never uses it
    // itself) — see ExtractionGrid's EXTRACTION BAND note for what it anchors.
    public void initialize(Board board, ContainerData data, int capacityUnits, byte sandColorIndex, Material colorMaterial, Color fallbackColor, SandExtractionController sandExtraction, float sandBottomWorldY, GameplayTunables tuning) {
        _board = board;
        _tuning = tuning;

        // Already rotated and normalized when the level named a Shape prefab — see
        // ContainerData.occupiedCells. This list is the only footprint this class knows about.
        _shape = data.occupiedCells;
        _gridPosition = data.position;
        _targetColor = data.color;
        _sandColorIndex = sandColorIndex;
        _capacityUnits = Mathf.Max(0, capacityUnits);
        _filledUnits = 0;
        _sealed = false;
        _awake = false;

        _shapeVisual = GetComponent<Shape>();
        // The cell's world size comes from the sand, so the prefab cannot know it — the Shape needs
        // it to place its fill readout on the right cell corner (see Shape.placeFillAnchor).
        if (_shapeVisual != null) _shapeVisual.setCellWorldSize(_board.cellSize);

        buildShapeVisuals(colorMaterial, fallbackColor);
        snapVisualToGridPosition();
        refreshFillDisplay();

        _board.occupy(this);

        gameObject.AddComponent<ExtractionGrid>().initialize(this, _board, sandExtraction, sandBottomWorldY, tuning);
    }

    void buildShapeVisuals(Material colorMaterial, Color fallbackColor) {
        float cellSize = _board.cellSize;

        // The cubes stay even when the Shape prefab's FBX is what the player sees (2026-09-13): each
        // cube's Collider is what isPointerOverThis raycasts to pick the piece up, and its Transform is
        // cellVisual(i) — the target ExtractionGrid hands SandExtractionController for the flying sand
        // grains. Only the cube's MeshRenderer is switched off, and only when an FBX renderer exists;
        // legacy levels (no Shape prefab) have nothing else to draw them.
        bool fbxDrawsShape = _shapeVisual != null
                             && _shapeVisual.fbxPlaceholder != null
                             && _shapeVisual.fbxPlaceholder.GetComponentInChildren<Renderer>(true) != null;

        foreach (Vector2Int offset in _shape) {
            GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.name = $"Cell_{offset.x}_{offset.y}";
            cell.transform.SetParent(transform, false);
            cell.transform.localPosition = new Vector3(offset.x * cellSize, offset.y * cellSize, 0f);
            cell.transform.localScale = new Vector3(cellSize * 0.9f, cellSize * 0.9f, 0.5f);

            Renderer renderer = cell.GetComponent<Renderer>();
            if (colorMaterial != null) {
                renderer.sharedMaterial = colorMaterial;
            } else {
                renderer.material = new Material(renderer.sharedMaterial);
                renderer.material.color = fallbackColor;
            }
            if (fbxDrawsShape) renderer.enabled = false;

            _colliders.Add(cell.GetComponent<Collider>());
            _cellVisuals.Add(cell.transform);
        }
    }

    void Update() {
        if (_sealed) return;

        pollPointer();
        updateVisual(Time.deltaTime);
    }

    void pollPointer() {
        if (!_dragging) {
            if (Input.GetMouseButtonDown(0) && isPointerOverThis() && raycastBoardPlane(out Vector3 grab)) {
                beginDrag(grab);
            }
            return;
        }

        if (!Input.GetMouseButton(0)) {
            endDrag();
            return;
        }

        if (raycastBoardPlane(out Vector3 hit)) {
            dragTo(hit);
        }
    }

    bool isPointerOverThis() {
        if (Camera.main == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        foreach (Collider collider in _colliders) {
            if (collider.Raycast(ray, out _, 1000f)) return true;
        }
        return false;
    }

    // Pointer down on this Container = drag start = wake up (see the Awake note above).
    void beginDrag(Vector3 pointerWorld) {
        _awake = true;
        _dragging = true;
        _cornerLatchSign = 0f;
        _grabOffset = _board.worldToAnchorPoint(pointerWorld, _shape) - _visualAnchor;
    }

    void dragTo(Vector3 pointerWorld) {
        Vector2 wanted = _board.worldToAnchorPoint(pointerWorld, _shape) - _grabOffset;
        Vector2 toWanted = wanted - _visualAnchor;
        float distance = toWanted.magnitude;

        if (distance > COLLISION_EPSILON) {
            float travel = Mathf.Min(
                distance * (1f - Mathf.Exp(-dragFollowSharpness * Time.deltaTime)),
                dragMaxSpeedCells * Time.deltaTime);

            gatherBlockedCells();
            _visualAnchor = sweep(_visualAnchor, toWanted * (travel / distance));
        }

        commitNearestCell();
        applyVisual();
    }

    // The grid position is already the committed, valid cell nearest the visual — the visual just
    // settles onto it (updateVisual).
    void endDrag() {
        _dragging = false;
    }

    // Keeps the authoritative grid position on the cell nearest the visual, updating Board
    // occupancy when it changes.
    void commitNearestCell() {
        Vector2Int nearest = new Vector2Int(Mathf.RoundToInt(_visualAnchor.x), Mathf.RoundToInt(_visualAnchor.y));
        if (nearest == _gridPosition) return;
        // Always free (see the Movement note); still checked so a float edge case can never
        // double-book a cell.
        if (!_board.isAreaFree(nearest, _shape, this)) return;

        _board.free(this);
        _gridPosition = nearest;
        _board.occupy(this);
    }

    // -- Collision sweep -----------------------------------------------------------------------
    // Everything below works in anchor units (1 = one cell). Every shape cell and every blocked
    // cell is a full 1x1 square, so two shapes may touch edge to edge but never overlap.

    void gatherBlockedCells() {
        _blockedCells.Clear();
        Vector2Int size = _board.size;
        for (int x = 0; x < size.x; x++) {
            for (int y = 0; y < size.y; y++) {
                Vector2Int cell = new Vector2Int(x, y);
                Container occupant = _board.occupantAt(cell);
                if (occupant != null && occupant != this) _blockedCells.Add(cell);
            }
        }
    }

    // Moves the shape by `delta` in small sub-steps, stopping flush against whatever blocks it and
    // sliding along it on the free axis.
    //
    // Each sub-step moves the drag direction first — the axis the pointer pulls along more — and
    // only that axis may slide around a corner (moveAxis). A real pointer is never perfectly
    // straight, so a shape dragged sideways drifts a little into the next row and catches the
    // corner of a shape sitting diagonally ahead of it. The drag axis then slides it back into the
    // row it is mostly in, and the other axis must not pull it back toward the pointer during that
    // sub-step — otherwise the two cancel out and the shape sticks on the corner.
    //
    // A slide, once started, keeps the axis it started on until the pointer pulls back along that
    // axis or the shape is moving freely again (2026-09-13). Choosing the axis fresh from the
    // pointer offset every frame is what made a shape jitter on a corner: the slide moves the shape
    // toward its lane, i.e. away from the pointer on the other axis, so that axis's share of the
    // offset grows until it wins the next frame and pulls the shape straight back off the lane —
    // the two decisions then alternate every frame and the shape never gets around the corner.
    Vector2 sweep(Vector2 from, Vector2 delta) {
        int substeps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) / SWEEP_SUBSTEP));
        Vector2 step = delta / substeps;
        int dragAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? 0 : 1;

        if (_cornerLatchSign != 0f) {
            if (delta[_cornerLatchAxis] * _cornerLatchSign < -COLLISION_EPSILON) {
                _cornerLatchSign = 0f;
            } else {
                dragAxis = _cornerLatchAxis;
            }
        }

        Vector2 position = from;
        for (int i = 0; i < substeps; i++) {
            position = moveAxis(position, dragAxis, step[dragAxis], true, out bool slidAroundCorner, out bool dragAxisBlocked);
            bool otherAxisBlocked = false;
            if (!slidAroundCorner) {
                position = moveAxis(position, 1 - dragAxis, step[1 - dragAxis], false, out _, out otherAxisBlocked);
            }

            if (slidAroundCorner) {
                _cornerLatchAxis = dragAxis;
                _cornerLatchSign = Mathf.Sign(step[dragAxis]);
            } else if (!dragAxisBlocked && !otherAxisBlocked) {
                // Nothing in the way any more: the corner is behind us.
                _cornerLatchSign = 0f;
            }
        }
        return position;
    }

    // One axis (0 = x, 1 = y) of a sweep sub-step. With allowCornerSlide, a move blocked only by the
    // corner of a diagonal neighbor slides around it: if the shape, lined up with the row (or column)
    // it is mostly in, could continue, the blocked part of the move is spent sliding into that lane
    // (at most 45 degrees). A blocker straight ahead in that lane still stops the shape — there is
    // no rerouting.
    Vector2 moveAxis(Vector2 position, int axis, float amount, bool allowCornerSlide, out bool slidAroundCorner, out bool blockedByObstacle) {
        slidAroundCorner = false;
        blockedByObstacle = false;
        if (Mathf.Abs(amount) < COLLISION_EPSILON) return position;

        Vector2 moved = sweepAxis(position, axis, amount);
        float blocked = Mathf.Abs(amount) - Mathf.Abs(moved[axis] - position[axis]);
        if (blocked <= COLLISION_EPSILON) return moved;

        blockedByObstacle = true;
        if (!allowCornerSlide) return moved;

        int other = 1 - axis;
        float lane = Mathf.Round(moved[other]);
        float offLane = lane - moved[other];
        if (Mathf.Abs(offLane) <= COLLISION_EPSILON) return moved;

        Vector2 aligned = moved;
        aligned[other] = lane;
        if (!fits(aligned)) return moved;

        Vector2 alignedMoved = sweepAxis(aligned, axis, Mathf.Sign(amount) * blocked);
        if (Mathf.Abs(alignedMoved[axis] - aligned[axis]) <= COLLISION_EPSILON) return moved;

        slidAroundCorner = true;
        return sweepAxis(moved, other, Mathf.Sign(offLane) * Mathf.Min(Mathf.Abs(offLane), blocked));
    }

    // Moves along one axis by `amount`, stopping at the Board edge or flush against the first
    // blocked cell the shape overlaps on the other axis. Never moves backwards.
    Vector2 sweepAxis(Vector2 position, int axis, float amount) {
        int other = 1 - axis;
        Vector2Int shapeMin = Board.shapeMin(_shape);
        Vector2Int shapeMax = Board.shapeMax(_shape);
        float low = -shapeMin[axis];
        float high = _board.size[axis] - 1 - shapeMax[axis];

        foreach (Vector2Int blocked in _blockedCells) {
            foreach (Vector2Int offset in _shape) {
                if (Mathf.Abs(position[other] + offset[other] - blocked[other]) >= 1f - COLLISION_EPSILON) continue;

                if (amount > 0f) {
                    float contact = blocked[axis] - 1 - offset[axis];
                    if (contact >= position[axis] - COLLISION_EPSILON) high = Mathf.Min(high, contact);
                } else {
                    float contact = blocked[axis] + 1 - offset[axis];
                    if (contact <= position[axis] + COLLISION_EPSILON) low = Mathf.Max(low, contact);
                }
            }
        }

        float target = position[axis] + amount;
        position[axis] = amount > 0f
            ? Mathf.Max(position[axis], Mathf.Min(target, high))
            : Mathf.Min(position[axis], Mathf.Max(target, low));
        return position;
    }

    // True if the shape at this fractional anchor is inside the Board and overlaps no blocked cell.
    bool fits(Vector2 anchor) {
        Vector2Int shapeMin = Board.shapeMin(_shape);
        Vector2Int shapeMax = Board.shapeMax(_shape);
        if (anchor.x < -shapeMin.x - COLLISION_EPSILON || anchor.x > _board.size.x - 1 - shapeMax.x + COLLISION_EPSILON) return false;
        if (anchor.y < -shapeMin.y - COLLISION_EPSILON || anchor.y > _board.size.y - 1 - shapeMax.y + COLLISION_EPSILON) return false;

        foreach (Vector2Int blocked in _blockedCells) {
            foreach (Vector2Int offset in _shape) {
                float dx = Mathf.Abs(anchor.x + offset.x - blocked.x);
                float dy = Mathf.Abs(anchor.y + offset.y - blocked.y);
                if (dx < 1f - COLLISION_EPSILON && dy < 1f - COLLISION_EPSILON) return false;
            }
        }
        return true;
    }

    // -- Visual --------------------------------------------------------------------------------

    // While dragging, dragTo moves the visual. Otherwise the visual glides straight onto the grid
    // position (at drag end: the snap). That straight line is always clear: the visual and its
    // nearest cell are both free, and on every axis that separates the shape from a blocked cell,
    // rounding keeps it on the same side.
    void updateVisual(float deltaTime) {
        if (_dragging) return;

        Vector2 toCell = (Vector2)_gridPosition - _visualAnchor;
        float distance = toCell.magnitude;
        if (distance <= COLLISION_EPSILON) return;

        float travel = Mathf.Max(distance * (1f - Mathf.Exp(-releaseSnapSharpness * deltaTime)), releaseSnapMinSpeedCells * deltaTime);
        _visualAnchor = travel >= distance ? (Vector2)_gridPosition : _visualAnchor + toCell * (travel / distance);
        applyVisual();
    }

    void applyVisual() {
        transform.position = _board.anchorPointToWorldCenter(_visualAnchor, _shape);
    }

    bool raycastBoardPlane(out Vector3 hit) {
        if (Camera.main == null) {
            hit = default;
            return false;
        }

        // Board lies flat in the XY plane facing the camera (see Board.cs's layout convention),
        // so the drag plane's normal is the Board's forward (Z) axis, not its up (Y) axis.
        Plane plane = new Plane(_board.transform.forward, _board.transform.position);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (plane.Raycast(ray, out float distance)) {
            hit = ray.GetPoint(distance);
            return true;
        }

        hit = default;
        return false;
    }

    // Called by ExtractionGrid with the cell count SandExtractionController actually removed for
    // this Container this frame (already clamped to remainingCapacity there).
    public void addCollected(int amount) {
        if (_sealed || amount <= 0) return;

        _filledUnits = Mathf.Min(_capacityUnits, _filledUnits + amount);
        refreshFillDisplay();

        if (_filledUnits >= _capacityUnits) {
            seal();
        }
    }

    // Pushes this Container's own fill at the Shape's readout. fillLevel is filledUnits /
    // capacityUnits — the figure the extraction and the area-weighted capacity split already
    // produce, so the percentage on the piece is not a separate calculation.
    void refreshFillDisplay() {
        if (_shapeVisual != null) _shapeVisual.setFillPercent(fillLevel);
    }

    // Depletion-tolerance safeguard (see Level.applyDepletionGuarantee): when this Container's
    // color has run out of sand everywhere, pull it up to 100% and seal it regardless of its
    // current fill — covers rounding edge cases in the area-weighted capacity split (SandLevelSO's
    // class header) rather than stranding a Container a few cells short.
    public void forceComplete() {
        if (_sealed) return;

        _filledUnits = _capacityUnits;
        refreshFillDisplay();
        seal();
    }

    void seal() {
        _sealed = true;
        _dragging = false;
        _board.free(this);
        gameObject.SetActive(false);
    }

    void snapVisualToGridPosition() {
        _visualAnchor = _gridPosition;
        transform.position = _board.anchorToWorldCenter(_gridPosition, _shape);
    }
}
