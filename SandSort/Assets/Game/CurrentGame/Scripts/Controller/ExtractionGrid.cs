using System.Collections.Generic;
using UnityEngine;

// A Container's sand interaction layer — it takes the place of SandCylinderDemo's conveyor cube as
// the thing that pulls sand out of SandCylinderSandGrid. It moves with its Container for free: every
// frame it reads the Container's committed gridPosition, so there is no position of its own to keep
// in sync.
//
// It never extracts by itself. Each extraction point is handed to
// SandExtractionController.ExtractAtPoint, which runs the demo's own eligibility, budget, color
// filtering and particle logic unchanged.
//
// EXTRACTION RULE (confirmed 2026-09-11): a point only extracts while its cell sits in the Board's
// top row — the row closest to the sand area. Shapes further down never extract, even with empty
// cells above them. Level.buildBoard lines the top row's cell centers up with the demo's conveyor
// height, so a top-row point sees exactly the demo cube's extraction window. The row check is not
// redundant with that geometry: ExtractColor/HasReachableColor clamp any window lying below the sand
// up to row 0, so a lower-row point would still reach the bottom row of sand without this gate.
//
// A Container must also be awake (Container.isAwake): all Containers start asleep, so a shape that
// starts in the top row does not extract until the player first presses on it to drag. Condition:
// isAwake && top row.
//
// Drag end is never required (2026-09-11): extraction follows the Container's grid position, which
// is authoritative even mid-drag. A shape pressed while in the top row starts extracting right away,
// and a shape dragged up into the top row starts the moment it reaches that row — its grid position
// AND its visual have to be there (2026-09-12), see GameplayTunables.extractionArrivalToleranceCells.
//
// Phase 1: every occupied cell of the shape is a candidate point (a 1x1 has exactly one). Which
// cells of a multi-cell shape count as sand-facing is decided in Phase 4.
public class ExtractionGrid : MonoBehaviour {

    Container _container;
    Board _board;
    SandExtractionController _sandExtraction;
    // Gameplay-side tuning asset (GameplayTunables.extractionArrivalToleranceCells). Never the sand's
    // SandCylinderTunables — see GameplayTunables' class header for why the two stay apart.
    GameplayTunables _tuning;

    // One pair per shape cell (parallel to Container.shape), just like the demo keeps one pair per
    // cube (SandExtractionController.extractionAccumulators / grainSpawnAccumulators).
    float[] _extractionAccumulators;
    float[] _grainSpawnAccumulators;

    public void initialize(Container container, Board board, SandExtractionController sandExtraction, GameplayTunables tuning) {
        _container = container;
        _board = board;
        _sandExtraction = sandExtraction;
        _tuning = tuning;

        int pointCount = container.shape.Count;
        _extractionAccumulators = new float[pointCount];
        _grainSpawnAccumulators = new float[pointCount];
    }

    void Update() {
        if (_sandExtraction == null) return;
        if (_container.isSealed || !_container.isAwake) return;

        List<Vector2Int> shape = _container.shape;
        for (int i = 0; i < shape.Count; i++) {
            if (_container.remainingCapacity <= 0) return;

            Vector2Int cell = _container.gridPosition + shape[i];
            if (cell.y != _board.topRow) continue;
            // ...and the shape has to have actually got there, not just rounded to it.
            float arrivalTolerance = _tuning != null
                ? _tuning.extractionArrivalToleranceCells
                : GameplayTunables.DEFAULT_EXTRACTION_ARRIVAL_TOLERANCE_CELLS;
            if (_container.visualAnchor.y + shape[i].y < _board.topRow - arrivalTolerance) continue;

            int removed = _sandExtraction.ExtractAtPoint(
                _board.cellToWorldCenter(cell),
                _container.sandColorIndex,
                _container.remainingCapacity,
                _container.cellVisual(i),
                ref _extractionAccumulators[i],
                ref _grainSpawnAccumulators[i]);

            _container.addCollected(removed);
        }
    }
}
