using System.Collections.Generic;
using UnityEngine;

// A Container's sand interaction layer — it takes the place of SandCylinderDemo's conveyor cube as
// the thing that pulls sand out of SandCylinderSandGrid. It moves with its Container for free: every
// frame it reads the Container's position, so there is no position of its own to keep in sync.
//
// It never extracts by itself. Each extraction point is handed to
// SandExtractionController.ExtractAtPoint, which runs the demo's own eligibility, budget, color
// filtering and particle logic unchanged.
//
// EXTRACTION RULE (confirmed 2026-09-11): a point only extracts while its cell sits in the Board's
// top row — the row closest to the sand area. Shapes further down never extract, even with empty
// cells above them. The row check is doing real work: ExtractColor/HasReachableColor clamp any
// window lying below the sand up to row 0, and the band below is anchored to the sand rather than to
// the cell, so without this gate EVERY row of the board would reach the same sand.
//
// EXTRACTION BAND (2026-09-12): the Y handed to ExtractAtPoint is the SAND's bottom edge, not the
// cell's own Y. SandExtractionController.VerticalRowRange is one-sided — it reaches from the Y it is
// given up to SandCylinderTunables.extractionRangeY above it — and it was written for the demo's
// conveyor, whose cubes ride just under the sand at a fixed height. The board's top row does not:
// it sits (GameplayTunables.gridSandGapCells + 0.5) * cellSize below the sand, a distance the
// designer tunes for LOOKS. Passing the cell's Y therefore spent part of extractionRangeY crossing
// that cosmetic gap and let the rest run further up into the sand the closer the board was moved —
// measured, the reachable band went 13 -> 27 -> 38 rows as the gap shrank, and a colour that existed
// only high up in a block (rows 34-37) became extractable, which reads in-game as sand vanishing in
// mid-air far above the shape. Anchoring Y to the sand's bottom edge makes extractionRangeY mean
// exactly what its name says — a fixed world-space depth of sand, [sandBottomY, sandBottomY +
// extractionRangeY] — and fully decouples it from gridSandGapCells, which goes back to being a
// purely visual knob.
//
// EXTRACTION FOOTPRINT (2026-09-12): which sand blocks a shape drains is decided by real geometric
// OVERLAP in X, not by the committed grid cell. Container.gridPosition rounds the shape to the
// nearest cell, so it names one block per cell and flips between blocks all at once: measured, a
// shape whose cells already covered 43% of the next block kept draining the block it was leaving,
// then teleported the whole draw over at the halfway point (72 frames / 0.42 world units late, and
// symmetric on the way out). Extraction now works from the shape's DRAWN position
// (Container.visualAnchor) instead:
//  - The footprint is NOT the shape's bounding box. It is the union of the 1x1 cell footprints of
//    the shape's TOP-PROFILE cells — one real occupied cell per column the shape occupies, see TOP
//    PROFILE below — so nothing is ever drained through a column the shape does not occupy.
//  - Each such cell spans exactly [center - 0.5, center + 0.5] cells in X and therefore overlaps at
//    most two board columns; a column is a candidate as soon as that overlap is positive, i.e. from
//    the first frame the drawn cell touches it.
//
// TOP PROFILE (2026-09-14): a shape's extraction points are its SKYLINE — for every column the
// shape occupies, the TOPMOST occupied cell of that column is one extraction point. The points come
// straight out of Container.shape, which is already rotated and normalized (ContainerData
// .occupiedCells / Shape.rotatedCells), so every rotation of every shape gets its own correct
// profile for free and there is no per-shape or per-rotation table anywhere.
//
// Before this the points were the cells sitting in the board's top ROW, which made a shape's reach
// narrower than the shape itself whenever its top row was narrower than its silhouette. The worked
// case is Shape_L4 — cells (0,0) (1,0) (0,1) (0,2), a 1-1-2 vertical L:
//
//     #        column 0: topmost cell (0,2) -> extraction point
//     #        column 1: topmost cell (1,0) -> extraction point (this is the new one)
//     ##
//
// Its top row is one cell wide, so it used to produce ONE point, in the left column: pushed flush
// against the board's right edge its box covered the last two sand blocks, but the right one was
// unreachable no matter where the player put the piece. The profile gives it the second point its
// own occupancy already justifies. Shapes whose top row already spans every column (an upright I, O,
// T, S or Z) are completely unaffected — their profile IS their top row.
//
// A profile cell lying lower in the shape extracts exactly like a top-row one, because the point's
// own Y is never used: the band is anchored to the sand (see EXTRACTION BAND) and the point only
// names a COLUMN. The gate is therefore asked of the shape rather than of each cell — the shape
// extracts while its TOPMOST row is the board's top row, which is the same condition every cell that
// used to pass it was tested against.
//
// What this does NOT change:
//  - The scan distance per point. A point still covers exactly its own cell, [center - 0.5,
//    center + 0.5]; there is no widening of any kind (a 0.25 reach extension was tried on
//    2026-09-14 and removed — it could only reach a neighbouring block by draining all of it).
//  - It is horizontal only: the Y gates, the band, and the sand-anchored Y are untouched.
//  - addCandidate still rejects columns outside the board, so this cannot reach past the edge.
//  - Board column c and sand block c are the same world-space X span (Level.buildBoard sizes cells
//    to SandCylinderTunables.CubeWorldSize), so "board column" and "sand block" are interchangeable
//    here and ExtractAtPoint is given the CANDIDATE COLUMN's center, not the shape cell's own X.
//
// EDGE-GATED EXTRAS (2026-09-14): a profile point that sits IN the shape's top row is a normal
// point and is always active — those are exactly the points that existed before TOP PROFILE, so a
// shape in the middle of the board behaves precisely as it always did. A profile point BELOW the top
// row is an EXTRA, and an extra is only active where it is actually needed: when its own column is
// against the board's left or right edge.
//  - An extra in the shape's LEFTMOST column is active only while the shape is flush against the
//    board's left edge, an extra in its RIGHTMOST column only while flush against the right edge.
//  - An extra in any other column is never active. A column in the middle of the shape can always be
//    reached by a normal point simply by sliding the piece one cell, so it needs no help; the U5
//    notch is the one to picture here, and it stays as non-draining as it was before TOP PROFILE.
//  - "Flush" is judged on the COMMITTED grid position (Container.gridPosition), the same input the
//    top-row gate uses. Being at the edge means the column is otherwise unreachable: the shape cannot
//    be slid further that way, so without the extra that sand block can never be drained at all,
//    which is the whole reason the L4 case was raised.
// The gate is a property of the piece plus its position only — it never looks at the sand.
//
// What it DOES change, by design: the per-frame block budget is one slot per ACTIVE extraction point
// (see THROUGHPUT), so a shape whose top row is narrower than its silhouette gains one slot while it
// is parked against the matching edge, and none anywhere else.
//
// THROUGHPUT (measured 2026-09-12): SandCylinderTunables.sandExtractionRate is applied per
// ExtractAtPoint call, through the accumulator the caller passes by ref — two calls in one frame
// really do drain twice as fast (measured 1596 -> 3193 cells/s). So overlap decides WHICH blocks may
// drain, and the shape decides HOW MANY: at most one block per ACTIVE extraction point — one per
// column the shape occupies in its top row, plus an edge-gated extra (a 1x1 straddling two blocks
// still drains at 1x). Blocks are tried
// most-overlapped first, and a call that removes nothing costs nothing — ExtractAtPoint returns
// before touching the accumulator when that block has no reachable sand of the color — so the slot
// passes to the next overlapping block instead of being wasted.
//
// Deliberately NOT done here: no change to SandCylinderDemo (VerticalRowRange and the whole
// rate/accumulator chain are reused as-is, the fix is only in which points we hand them), and no
// adjacency/opacity rule — ExtractColor climbing past other colours to reach a matching cell inside
// the band is intended behaviour and stays.
//
// A Container must also be awake (Container.isAwake): all Containers start asleep, so a shape that
// starts in the top row does not extract until the player first presses on it to drag. Condition:
// isAwake && top row.
//
// Drag end is never required (2026-09-11): extraction follows the Container's grid position, which
// is authoritative even mid-drag. A shape pressed while in the top row starts extracting right away,
// and a shape dragged up into the top row starts the moment it reaches that row — its grid position
// AND its visual have to be there (2026-09-12), see GameplayTunables.extractionArrivalToleranceCells.
public class ExtractionGrid : MonoBehaviour {

    // Below this an overlap is a touch, not an overlap. A cell drawn exactly on a column has a
    // second, zero-width neighbour; it must not count as a candidate, or an aligned 1x1 would keep
    // two accumulators alive and a block it does not actually cover could drain.
    const float MINIMUM_OVERLAP_CELLS = 0.0001f;

    // When a profile point is allowed to extract — see the EDGE-GATED EXTRAS note. ALWAYS is a normal
    // point (one sitting in the shape's own top row); the rest are extras, gated on the shape being
    // flush against that side of the board, or never active at all.
    const byte GATE_ALWAYS = 0;
    const byte GATE_LEFT_EDGE = 1;
    const byte GATE_RIGHT_EDGE = 2;
    const byte GATE_NEVER = 3;

    Container _container;
    Board _board;
    SandExtractionController _sandExtraction;
    // World-space Y of the sand area's bottom edge — SandExtractionController's own row-0 anchor,
    // handed down from Level (the same Vector3 it gave InitWithoutConveyor). See the EXTRACTION BAND
    // note above for why the band is measured from here and not from the board cell.
    float _sandBottomWorldY;
    // Gameplay-side tuning asset (GameplayTunables.extractionArrivalToleranceCells). Never the sand's
    // SandCylinderTunables — see GameplayTunables' class header for why the two stay apart.
    GameplayTunables _tuning;

    // One pair per BOARD COLUMN (= per sand block), not per shape cell: the accumulator belongs to
    // the block being drained, so a shape sliding from one block to the next picks up that block's
    // own progress instead of carrying its cell's across. Same role as the demo's per-cube
    // SandExtractionController.extractionAccumulators / grainSpawnAccumulators.
    float[] _extractionAccumulators;
    float[] _grainSpawnAccumulators;

    // This frame's overlapping columns, rebuilt from scratch every Update — parallel arrays rather
    // than a struct list so the per-frame work allocates nothing. Each entry is a distinct column;
    // _candidateOwners[k] is the shape cell that overlaps it most, used only as the grain target.
    int[] _candidateColumns;
    float[] _candidateOverlaps;
    int[] _candidateOwners;
    int _candidateCount;

    // The shape's extraction points, as indices into Container.shape: the topmost occupied cell of
    // each column the shape occupies, left to right. Built once — Container.shape is assigned in
    // Container.initialize (already rotated) and never replaced, so the profile cannot go stale.
    int[] _profileCells;
    // Parallel to _profileCells: when that point is active (GATE_*). Also built once.
    byte[] _profileGates;
    // The shape's own top row, in shape coordinates. The gate compares THIS to the board's top row,
    // so a profile cell further down still extracts along with the rest of the shape.
    int _shapeTopY;
    // The shape's leftmost and rightmost occupied columns, in shape coordinates — added to
    // Container.gridPosition.x they say whether the shape is flush against either board edge.
    int _shapeMinX;
    int _shapeMaxX;

    public void initialize(Container container, Board board, SandExtractionController sandExtraction, float sandBottomWorldY, GameplayTunables tuning) {
        _container = container;
        _board = board;
        _sandExtraction = sandExtraction;
        _sandBottomWorldY = sandBottomWorldY;
        _tuning = tuning;

        int columnCount = board.size.x;
        _extractionAccumulators = new float[columnCount];
        _grainSpawnAccumulators = new float[columnCount];

        buildTopProfile(container.shape);

        // One profile cell spans exactly one cell and so reaches at most TWO columns, and a column is
        // never listed twice — so both bounds hold and the smaller one sizes the buffers.
        int maxCandidates = Mathf.Min(columnCount, _profileCells.Length * 2);
        _candidateColumns = new int[maxCandidates];
        _candidateOverlaps = new float[maxCandidates];
        _candidateOwners = new int[maxCandidates];
    }

    // Picks the shape's extraction points: the topmost occupied cell of every column it occupies, in
    // left-to-right order, and records when each one is allowed to extract (see EDGE-GATED EXTRAS).
    // Runs once per Container. See the TOP PROFILE note for why this is the whole rotation story —
    // Container.shape is the ROTATED cell list, so a rotated piece simply has a different profile.
    void buildTopProfile(List<Vector2Int> shape) {
        if (shape == null || shape.Count == 0) {
            _profileCells = new int[0];
            _profileGates = new byte[0];
            _shapeTopY = 0;
            _shapeMinX = 0;
            _shapeMaxX = 0;
            return;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        _shapeTopY = int.MinValue;
        for (int i = 0; i < shape.Count; i++) {
            if (shape[i].x < minX) minX = shape[i].x;
            if (shape[i].x > maxX) maxX = shape[i].x;
            if (shape[i].y > _shapeTopY) _shapeTopY = shape[i].y;
        }
        _shapeMinX = minX;
        _shapeMaxX = maxX;

        int columns = maxX - minX + 1;
        int[] topmost = new int[columns];
        for (int c = 0; c < columns; c++) topmost[c] = -1;
        for (int i = 0; i < shape.Count; i++) {
            int c = shape[i].x - minX;
            if (topmost[c] < 0 || shape[i].y > shape[topmost[c]].y) topmost[c] = i;
        }

        // Every column of a connected shape's box is occupied, so this normally keeps all of them —
        // the filter only stops a hypothetical disconnected shape from putting a point in a column it
        // does not actually occupy.
        int used = 0;
        for (int c = 0; c < columns; c++) if (topmost[c] >= 0) used++;
        _profileCells = new int[used];
        _profileGates = new byte[used];
        int w = 0;
        for (int c = 0; c < columns; c++) {
            if (topmost[c] < 0) continue;
            int i = topmost[c];
            _profileCells[w] = i;
            // A point in the shape's own top row is a normal point and always runs; an extra runs
            // only where its column cannot be reached by sliding the piece, i.e. at the board edge on
            // that same side.
            if (shape[i].y == _shapeTopY) _profileGates[w] = GATE_ALWAYS;
            else if (shape[i].x == minX) _profileGates[w] = GATE_LEFT_EDGE;
            else if (shape[i].x == maxX) _profileGates[w] = GATE_RIGHT_EDGE;
            else _profileGates[w] = GATE_NEVER;
            w++;
        }
    }

    void Update() {
        if (_sandExtraction == null) return;
        if (_container.isSealed || !_container.isAwake) return;
        // Lose (time up) freezes progress with the timer; revive unlocks and extraction resumes here
        // with its accumulators untouched.
        if (_container.isInputLocked) return;
        if (_container.remainingCapacity <= 0) return;

        // One slot per active extraction point — see the THROUGHPUT note.
        int slots = gatherOverlappingColumns();
        if (slots <= 0) return;

        sortCandidatesByOverlap();

        int used = 0;
        for (int k = 0; k < _candidateCount && used < slots; k++) {
            if (_container.remainingCapacity <= 0) return;

            int column = _candidateColumns[k];
            // The point names the BLOCK, not the shape cell: its X is the candidate column's own
            // center, so the block it lands in is the block we measured the overlap against. Y is
            // the sand's bottom edge — see the EXTRACTION BAND note.
            Vector3 point = _board.cellToWorldCenter(new Vector2Int(column, _board.topRow));
            point.y = _sandBottomWorldY;

            // GRAIN TARGET ONLY (2026-09-13): where the falling sand is AIMED, never where it is taken
            // from. `point` above is the extraction site and is untouched by this — the column, the
            // bottom-up rule, the budget and the accumulators all still run off it, so the amount of
            // sand a shape pulls is exactly what it was.
            //
            // The grains are aimed at the spot ShapeSandFill grows its heap from, so the stream and
            // the heap read as one pour. Aiming at cellVisual instead — the cell the sand was pulled
            // through — puts the landing 0.25 to 1.28 cells away from the heap on a 4-5 cell piece,
            // and on a multi-cell top edge (T4, Z4, U5) it splits the stream into two or three
            // ribbons that miss the heap on both sides. cellVisual stays as the fallback for a piece
            // with no fill visual.
            Transform grainTarget = _container.sandPourTarget != null
                ? _container.sandPourTarget
                : _container.cellVisual(_candidateOwners[k]);

            int removed = _sandExtraction.ExtractAtPoint(
                point,
                _container.sandColorIndex,
                _container.remainingCapacity,
                grainTarget,
                ref _extractionAccumulators[column],
                ref _grainSpawnAccumulators[column]);

            // Nothing of this color reachable in that block: no accumulator was touched, so the slot
            // is still free for the next overlapping block.
            if (removed <= 0) continue;

            _container.addCollected(removed);
            used++;
        }
    }

    // Fills the candidate arrays with every board column the shape's ACTIVE top-profile points
    // actually overlap, and returns how many such points there are (the per-frame block budget).
    int gatherOverlappingColumns() {
        _candidateCount = 0;

        // The shape's topmost row must be COMMITTED to the board's top row, and the shape has to have
        // actually got there rather than merely rounded to it. Both gates keep their old meaning —
        // they used to be asked per cell, and only the shape's top row could ever pass them.
        if (_container.gridPosition.y + _shapeTopY != _board.topRow) return 0;

        float arrivalTolerance = _tuning != null
            ? _tuning.extractionArrivalToleranceCells
            : GameplayTunables.DEFAULT_EXTRACTION_ARRIVAL_TOLERANCE_CELLS;
        Vector2 visualAnchor = _container.visualAnchor;
        if (visualAnchor.y + _shapeTopY < _board.topRow - arrivalTolerance) return 0;

        // Which extras are live this frame — see the EDGE-GATED EXTRAS note. Judged on the committed
        // grid position, like the top-row gate above; on a board exactly as wide as the shape both
        // come out true and both extras run.
        bool atLeftEdge = _container.gridPosition.x + _shapeMinX == 0;
        bool atRightEdge = _container.gridPosition.x + _shapeMaxX == _board.size.x - 1;

        List<Vector2Int> shape = _container.shape;
        int activePoints = 0;
        for (int p = 0; p < _profileCells.Length; p++) {
            byte gate = _profileGates[p];
            if (gate == GATE_NEVER) continue;
            if (gate == GATE_LEFT_EDGE && !atLeftEdge) continue;
            if (gate == GATE_RIGHT_EDGE && !atRightEdge) continue;

            int i = _profileCells[p];
            activePoints++;

            // This one cell's footprint in board cell units. Column c covers [c - 0.5, c + 0.5], so
            // it is touched exactly when c lies strictly inside (low - 0.5, high + 0.5), and the
            // overlap is the plain interval intersection: 1 when the drawn cell sits on the column,
            // splitting 1 between two columns everywhere in between.
            float center = visualAnchor.x + shape[i].x;
            float low = center - 0.5f;
            float high = center + 0.5f;

            int firstColumn = Mathf.FloorToInt(low - 0.5f) + 1;
            int lastColumn = Mathf.CeilToInt(high + 0.5f) - 1;
            for (int column = firstColumn; column <= lastColumn; column++) {
                float overlap = Mathf.Min(high, column + 0.5f) - Mathf.Max(low, column - 0.5f);
                addCandidate(column, overlap, i);
            }
        }

        return activePoints;
    }

    // Records one (column, overlap) pair. A column reached by several cells of the shape is kept
    // once, at its largest overlap — that is what stops a wide shape from draining the same block
    // twice in a frame.
    void addCandidate(int column, float overlap, int shapeIndex) {
        if (overlap <= MINIMUM_OVERLAP_CELLS) return;
        if (column < 0 || column >= _board.size.x) return;

        for (int k = 0; k < _candidateCount; k++) {
            if (_candidateColumns[k] != column) continue;
            if (overlap > _candidateOverlaps[k]) {
                _candidateOverlaps[k] = overlap;
                _candidateOwners[k] = shapeIndex;
            }
            return;
        }

        _candidateColumns[_candidateCount] = column;
        _candidateOverlaps[_candidateCount] = overlap;
        _candidateOwners[_candidateCount] = shapeIndex;
        _candidateCount++;
    }

    // Most-overlapped first, so a shape with fewer slots than overlapping blocks spends them on the
    // blocks it actually covers. Insertion sort: at most a couple of entries per shape cell.
    void sortCandidatesByOverlap() {
        for (int k = 1; k < _candidateCount; k++) {
            int column = _candidateColumns[k];
            float overlap = _candidateOverlaps[k];
            int owner = _candidateOwners[k];

            int j = k - 1;
            while (j >= 0 && _candidateOverlaps[j] < overlap) {
                _candidateColumns[j + 1] = _candidateColumns[j];
                _candidateOverlaps[j + 1] = _candidateOverlaps[j];
                _candidateOwners[j + 1] = _candidateOwners[j];
                j--;
            }

            _candidateColumns[j + 1] = column;
            _candidateOverlaps[j + 1] = overlap;
            _candidateOwners[j + 1] = owner;
        }
    }
}
