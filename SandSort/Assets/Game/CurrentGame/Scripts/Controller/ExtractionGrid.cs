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
//    the cells that are actually occupied at the extraction level — so a U drains from its two top
//    cells and never from the gap between them, and a vertical L drains from its single top cell,
//    not from the 2-wide box around it.
//  - Each such cell spans [center - 0.5, center + 0.5] cells in X and therefore overlaps at most two
//    board columns; a column is a candidate as soon as that overlap is positive, i.e. from the first
//    frame the drawn cell touches it.
//  - Board column c and sand block c are the same world-space X span (Level.buildBoard sizes cells
//    to SandCylinderTunables.CubeWorldSize), so "board column" and "sand block" are interchangeable
//    here and ExtractAtPoint is given the CANDIDATE COLUMN's center, not the shape cell's own X.
//
// THROUGHPUT (measured 2026-09-12): SandCylinderTunables.sandExtractionRate is applied per
// ExtractAtPoint call, through the accumulator the caller passes by ref — two calls in one frame
// really do drain twice as fast (measured 1596 -> 3193 cells/s). So overlap decides WHICH blocks may
// drain, and the shape decides HOW MANY: at most one block per occupied cell at the extraction level
// (a 1x1 straddling two blocks still drains at 1x, a U drains at most two blocks). Blocks are tried
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

    public void initialize(Container container, Board board, SandExtractionController sandExtraction, float sandBottomWorldY, GameplayTunables tuning) {
        _container = container;
        _board = board;
        _sandExtraction = sandExtraction;
        _sandBottomWorldY = sandBottomWorldY;
        _tuning = tuning;

        int columnCount = board.size.x;
        _extractionAccumulators = new float[columnCount];
        _grainSpawnAccumulators = new float[columnCount];

        // Each extraction-level cell can reach at most two columns, and a column is never listed
        // twice — so both bounds hold and the smaller one sizes the buffers.
        int maxCandidates = Mathf.Min(columnCount, container.shape.Count * 2);
        _candidateColumns = new int[maxCandidates];
        _candidateOverlaps = new float[maxCandidates];
        _candidateOwners = new int[maxCandidates];
    }

    void Update() {
        if (_sandExtraction == null) return;
        if (_container.isSealed || !_container.isAwake) return;
        if (_container.remainingCapacity <= 0) return;

        // One slot per occupied cell at the extraction level — see the THROUGHPUT note.
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

            int removed = _sandExtraction.ExtractAtPoint(
                point,
                _container.sandColorIndex,
                _container.remainingCapacity,
                _container.cellVisual(_candidateOwners[k]),
                ref _extractionAccumulators[column],
                ref _grainSpawnAccumulators[column]);

            // Nothing of this color reachable in that block: no accumulator was touched, so the slot
            // is still free for the next overlapping block.
            if (removed <= 0) continue;

            _container.addCollected(removed);
            used++;
        }
    }

    // Fills the candidate arrays with every board column the shape's extraction-level cells actually
    // overlap, and returns how many such cells there are (the per-frame block budget).
    int gatherOverlappingColumns() {
        _candidateCount = 0;

        List<Vector2Int> shape = _container.shape;
        Vector2 visualAnchor = _container.visualAnchor;
        Vector2Int gridPosition = _container.gridPosition;
        int topRow = _board.topRow;
        float arrivalTolerance = _tuning != null
            ? _tuning.extractionArrivalToleranceCells
            : GameplayTunables.DEFAULT_EXTRACTION_ARRIVAL_TOLERANCE_CELLS;

        int extractionLevelCells = 0;
        for (int i = 0; i < shape.Count; i++) {
            // Y gate, unchanged: the cell must be committed to the top row...
            if (gridPosition.y + shape[i].y != topRow) continue;
            // ...and the shape has to have actually got there, not just rounded to it.
            if (visualAnchor.y + shape[i].y < topRow - arrivalTolerance) continue;

            extractionLevelCells++;

            // This one occupied cell's own footprint, in board cell units: [center - 0.5,
            // center + 0.5]. Overlap with column c is 1 - |center - c|, so only the two columns
            // either side of the center can be positive.
            float center = visualAnchor.x + shape[i].x;
            int left = Mathf.FloorToInt(center);
            addCandidate(left, 1f - (center - left), i);
            addCandidate(left + 1, center - left, i);
        }

        return extractionLevelCells;
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
