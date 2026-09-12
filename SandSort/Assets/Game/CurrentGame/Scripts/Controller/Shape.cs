using System.Collections.Generic;
using UnityEngine;

// The canonical shape library. ONE prefab per shape (Shape_L4.prefab), never one per orientation:
// a level places that same prefab at a Position with a Rotation, and the four rotations are derived
// here. See Prefabs/Shapes/.
//
// WHAT IS GAMEPLAY TRUTH (2026-09-12): shape type + occupied cells + rotation. Nothing gameplay-side
// ever reads a mesh, a Renderer or any bounds — the FBX that will eventually live under
// FBX_Placeholder is decoration only. Board occupancy, the drag/collision sweep and the extraction
// footprint all run off occupiedCells (Container.shape), which is this list rotated and normalized.
//
// CANONICAL ORIENTATION is whatever _canonicalCells says and it is never rewritten: applyRotation
// leaves the prefab's own data alone and only derives a rotated cell list plus the matching visual
// transform. Cells use the Board's convention (cell.x -> world X, cell.y -> world Y, y up), so a
// shape's LAST drawn row is y = 0.
//
// Rotation is clockwise, fixed by the design spec's own example: the canonical vertical L4
//     #
//     #
//     ##
// at 90 degrees must read
//     ###
//     #
// which is (x, y) -> (y, -x) followed by a normalize back to min (0, 0). Only the four right angles
// exist; there is no arbitrary rotation.
public enum ShapeType {
    Custom,
    Shape_1x1, Shape_2x1, Shape_3x1, Shape_4x1, Shape_2x2,
    Shape_L3, Shape_L4, Shape_T4, Shape_J4, Shape_S4, Shape_Z4, Shape_U5, Shape_Plus5,
}

public enum ShapeRotation { Deg0 = 0, Deg90 = 1, Deg180 = 2, Deg270 = 3 }

public class Shape : MonoBehaviour {

    [Tooltip("Which library shape this prefab is. Informational: the cell list below is the data gameplay actually runs on.")]
    [SerializeField] ShapeType _type = ShapeType.Custom;
    [Tooltip("The occupied cells of the CANONICAL (0 degree) orientation, relative to the shape's anchor, y up. Authored explicitly here — never derived from a mesh or from renderer bounds. Empty cells inside the bounding box simply are not in this list, which is what keeps them out of collision and out of the extraction footprint.")]
    [SerializeField] List<Vector2Int> _canonicalCells = new() { Vector2Int.zero };

    [Header("Visual")]
    [Tooltip("Everything purely decorative hangs under here. Rotated to match the gameplay rotation; holds no gameplay data.")]
    [SerializeField] Transform _visualRoot;
    [Tooltip("Where the real single-piece FBX will be parented once it exists (VisualRoot/FBX_Placeholder/RealShapeFBX). Empty on purpose: no mesh, no collider, no gameplay role — just a Transform whose local position/rotation/scale can be nudged in the Inspector to line the art up.")]
    [SerializeField] Transform _fbxPlaceholder;

    [Header("Fill UI")]
    [Tooltip("Where the fill readout sits. Its X and Y are DERIVED every time the rotation changes — the bottom-right corner of the bottom-right cell the shape actually occupies (see placeFillAnchor) — so editing them here does nothing. Its Z is authored: use it to set how far in front of the piece the readout floats.")]
    [SerializeField] Transform _fillUIAnchor;
    [Tooltip("Holds the fill readout. Kept as its own transform under the anchor so the readout can be offset or scaled without disturbing the derived anchor position.")]
    [SerializeField] Transform _fillPercentageUI;
    [Tooltip("The fill readout itself. A world-space TextMeshPro (NOT TextMeshProUGUI): the board, the shapes and the sand all live in world space under one orthographic camera, so no Canvas is involved — the same approach SandExtractionCube.BuildCollectionLabel already uses for its cube labels. Still a placeholder look: plain text, no design, no animation.")]
    [SerializeField] TMPro.TMP_Text _fillPercentageText;

    // Runtime only. A prefab asset always sits at its canonical orientation; the rotation belongs to
    // the instance a level spawns (see Level.buildContainers / ContainerData.rotation).
    ShapeRotation _rotation = ShapeRotation.Deg0;
    List<Vector2Int> _occupiedCells;
    // The authored local position of VisualRoot, captured before the first rotation is applied so
    // applyRotation stays absolute (and therefore repeatable) instead of compounding. FillUIAnchor
    // keeps only its authored Z — see placeFillAnchor, which derives its X and Y.
    Vector3 _visualRootBasePosition;
    float _fillUIAnchorBaseZ;
    bool _basePositionsCaptured;
    // One board cell in world units. Comes from the sand (SandCylinderTunables.CubeWorldSize, via
    // Board.cellSize), so a prefab cannot know it — Container hands it over at build time. 1 until
    // then, which keeps the anchor maths sane for a prefab inspected outside a level.
    float _cellWorldSize = 1f;

    public ShapeType type => _type;
    public ShapeRotation rotation => _rotation;
    public IReadOnlyList<Vector2Int> canonicalCells => _canonicalCells;
    public Transform visualRoot => _visualRoot;
    public Transform fbxPlaceholder => _fbxPlaceholder;
    public Transform fillUIAnchor => _fillUIAnchor;
    public Transform fillPercentageUI => _fillPercentageUI;
    public TMPro.TMP_Text fillPercentageText => _fillPercentageText;

    // The cells this instance actually occupies: canonical, rotated, normalized so the lowest-left
    // cell of the bounding box is (0, 0). This is what Container.shape is built from.
    public List<Vector2Int> occupiedCells {
        get {
            if (_occupiedCells == null) _occupiedCells = rotatedCells(_canonicalCells, _rotation);
            return _occupiedCells;
        }
    }

    // Called once per instance, right after it is spawned. Rotates the gameplay cell list and turns
    // the decorative half (VisualRoot, and with it FBX_Placeholder) by the same amount, so the art
    // rides along with the footprint. The fill readout is NOT turned — see placeFillAnchor.
    public void applyRotation(ShapeRotation rotation) {
        captureBasePositions();

        _rotation = rotation;
        _occupiedCells = rotatedCells(_canonicalCells, rotation);

        placeVisualRoot();
        placeFillAnchor();
    }

    // One board cell in world units, handed over by Container at build time (Board.cellSize). Also
    // re-places the readout and the visual, since both are measured in cells.
    public void setCellWorldSize(float cellWorldSize) {
        _cellWorldSize = cellWorldSize;
        placeVisualRoot();
        placeFillAnchor();
    }

    // Turns VisualRoot to the gameplay rotation AND moves it by the same normalization shift the
    // cells got (2026-09-13). The turn alone pivots about canonical cell (0,0), so the art landed at
    // R^k(C) while the footprint is R^k(C) + shift — e.g. T4 at 90/180/270 drawn (0,-2)/(-2,-1)/(-1,0)
    // cells off its occupiedCells. Adding shift * cellWorldSize puts every drawn cell on its occupied
    // cell, including shapes whose cell (0,0) is empty (T4, Z4, Plus5): the shift is a per-axis min.
    //
    // Re-run from setCellWorldSize too: Level calls applyRotation before Container hands over the
    // real cell size, so the first placement uses the default of 1.
    void placeVisualRoot() {
        if (_visualRoot == null) return;
        captureBasePositions();

        Quaternion visual = visualRotationOf(_rotation);
        Vector2Int shift = normalizationShift(_canonicalCells, _rotation);
        _visualRoot.localPosition = visual * _visualRootBasePosition + new Vector3(shift.x, shift.y, 0f) * _cellWorldSize;
        _visualRoot.localRotation = visual;
    }

    // Puts FillUIAnchor on the bottom-right CORNER of the piece's bottom-right occupied cell:
    // lowest occupied row first, then the right-most cell that is actually occupied ON that row.
    //
    // Driven by occupiedCells — the real, already-rotated footprint — so the bounding box plays no
    // part and a hole never counts. A U (#.# / ###) measures from the right cell of its full bottom
    // row, and a horizontal L (### / #..) from its single bottom cell, not from the far right of the
    // 3-wide box above it. Rotation is handled for free: the cells are rotated before they get here,
    // so the anchor lands on whatever cell is bottom-right AFTER the turn, and it is recomputed on
    // every applyRotation.
    //
    // The anchor therefore stays UNROTATED — its position already encodes the rotation, and leaving
    // the transform at identity is also what keeps the text upright in all four orientations without
    // a counter-rotation. Only the authored Z is preserved, so the readout's depth in front of the
    // piece stays an Inspector value.
    void placeFillAnchor() {
        if (_fillUIAnchor == null) return;
        // Safe to call before any rotation has been applied (setCellWorldSize can come first).
        captureBasePositions();

        List<Vector2Int> cells = occupiedCells;
        if (cells.Count == 0) return;

        int bottomY = int.MaxValue;
        for (int i = 0; i < cells.Count; i++) bottomY = Mathf.Min(bottomY, cells[i].y);

        int rightX = int.MinValue;
        for (int i = 0; i < cells.Count; i++) {
            if (cells[i].y != bottomY) continue;
            rightX = Mathf.Max(rightX, cells[i].x);
        }

        // Cell (x, y)'s centre sits at (x, y) * cellSize in this shape's local space — the same
        // placement Container.buildShapeVisuals uses for the cubes — so its bottom-right corner is
        // half a cell right and half a cell down from there.
        _fillUIAnchor.localPosition = new Vector3(
            (rightX + 0.5f) * _cellWorldSize,
            (bottomY - 0.5f) * _cellWorldSize,
            _fillUIAnchorBaseZ);
        _fillUIAnchor.localRotation = Quaternion.identity;

        Transform readout = _fillPercentageUI != null ? _fillPercentageUI
                          : (_fillPercentageText != null ? _fillPercentageText.transform : null);
        if (readout != null) readout.localRotation = Quaternion.identity;
    }

    // Shows `normalized` (0..1) as a whole percentage. Container drives this from its own fill —
    // filledUnits / capacityUnits — so the number on the piece is the real collected-sand figure,
    // never a second capacity model. Safe to call on a shape with no text wired up.
    //
    // Styled after Docs/selected_sand_idea_mockup.png: a dark badge with a bold white number and a
    // smaller per-cent sign. The badge is TMP's own <mark> highlight rather than a quad behind the
    // text, which keeps it to one draw call, needs no new material (the project builds materials from
    // serialized assets, never Shader.Find — see SandCylinderRenderer's Android note) and leaves the
    // prefab hierarchy untouched. The mockup's rounded corners are the one thing it cannot do.
    public void setFillPercent(float normalized) {
        if (_fillPercentageText == null) return;
        int percent = Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f);
        _fillPercentageText.text = $"<mark=#0B0B12FF><b>  {percent}<size=65%>%</size>  </b></mark>";
    }

    // Clockwise about Z, matching the cell rotation below.
    public static Quaternion visualRotationOf(ShapeRotation rotation) {
        return Quaternion.Euler(0f, 0f, -90f * (int)rotation);
    }

    // One quarter turn clockwise per step — (x, y) -> (y, -x) — then shifted so the result's minimum
    // x and y are both 0. Normalizing matters because Board treats the anchor as cell (0, 0) of the
    // shape: without it a rotated shape would hang off its own anchor and the bounds checks in
    // Board.isInBounds / Container.sweepAxis would be measured against the wrong corner.
    public static List<Vector2Int> rotatedCells(IReadOnlyList<Vector2Int> cells, ShapeRotation rotation) {
        int steps = (int)rotation & 3;
        List<Vector2Int> result = new(cells.Count);

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        for (int i = 0; i < cells.Count; i++) {
            Vector2Int cell = cells[i];
            for (int step = 0; step < steps; step++) cell = new Vector2Int(cell.y, -cell.x);
            result.Add(cell);
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }

        if (minX != 0 || minY != 0) {
            for (int i = 0; i < result.Count; i++) result[i] -= new Vector2Int(minX, minY);
        }
        return result;
    }

    // The shift rotatedCells adds after turning: minus the per-axis minimum of the turned (not yet
    // normalized) cells, using the same clockwise step. rotatedCells(cells, r)[i] ==
    // turned(cells[i]) + normalizationShift(cells, r).
    public static Vector2Int normalizationShift(IReadOnlyList<Vector2Int> cells, ShapeRotation rotation) {
        if (cells.Count == 0) return Vector2Int.zero;
        int steps = (int)rotation & 3;

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        for (int i = 0; i < cells.Count; i++) {
            Vector2Int cell = cells[i];
            for (int step = 0; step < steps; step++) cell = new Vector2Int(cell.y, -cell.x);
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }
        return new Vector2Int(-minX, -minY);
    }

    void captureBasePositions() {
        if (_basePositionsCaptured) return;
        if (_visualRoot != null) _visualRootBasePosition = _visualRoot.localPosition;
        if (_fillUIAnchor != null) _fillUIAnchorBaseZ = _fillUIAnchor.localPosition.z;
        _basePositionsCaptured = true;
    }
}
