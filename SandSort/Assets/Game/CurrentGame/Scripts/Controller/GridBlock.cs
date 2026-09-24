using UnityEngine;

// Grid Block (2026-09-24): a fixed obstacle on the Board. It occupies its cells for the whole level —
// Level.buildGridBlocks spawns it and initialize marks every occupied cell with Board.block, and from
// then on Board.isAreaFree refuses them and Container's drag sweep treats them like any other blocked
// cell. It is NOT a Container: no colour, no capacity, no extraction, no pointer pick-up (the prefabs
// carry no Collider), and it never moves.
//
// SHAPES: every Grid Block prefab (Prefabs/GridBlocks/GridBlock_<Shape>.prefab) also carries a Shape
// component, and that is the whole shape model — canonical cells, clockwise rotation, normalization
// and VisualRoot placement are Shape's own (see Shape.cs). This class only positions the instance and
// blocks the cells Shape.occupiedCells reports.
//
// The visual is the matching Shape FBX turned so its CLOSED side faces the camera: the Shapes carry
// their FBX at Y 180 (open top), a Grid Block at Y 0. For the multi-cell FBXs that turn alone would
// mirror the art across X (their meshes are authored toward -X), so those prefabs also carry
// localScale.x = -1, which puts the drawn cells back on the occupied cells. The 1x1 is symmetric and
// needs no mirror. Local z -0.5 keeps the block in the same depth slab as the Shapes. Purely visual:
// the footprint is always Shape.occupiedCells, never mesh bounds.
[RequireComponent(typeof(Shape))]
public class GridBlock : MonoBehaviour {

    Shape _shape;
    // Also valid on the prefab asset: GridBlockData and the designer read the cells off it.
    public Shape shape => _shape != null ? _shape : (_shape = GetComponent<Shape>());

    Vector2Int _gridPosition;
    public Vector2Int gridPosition => _gridPosition;

    public void initialize(Board board, Vector2Int anchor, ShapeRotation rotation) {
        _gridPosition = anchor;
        shape.applyRotation(rotation);
        shape.setCellWorldSize(board.cellSize);
        transform.position = board.anchorToWorldCenter(anchor, null);
        foreach (Vector2Int cell in shape.occupiedCells) board.block(anchor + cell);
        applyPatternOrigin();
    }

    // -- Brick pattern origin (2026-09-24) -----------------------------------------------------
    // The Grid Block material (GridBlockBrickWall.shader) maps its texture in world space measured
    // from _GridBlockOrigin, so each block has its own brick pattern that moves with it and stays
    // horizontal at every rotation. The origin is this ROOT's position — the root never rotates
    // (Shape turns VisualRoot only). Set per renderer through a MaterialPropertyBlock, so the shared
    // material stays shared and the designer's own material copy (and its tints) is untouched.
    // Re-applied whenever the root moves, which also covers the designer dragging a preview.
    static readonly int GRID_BLOCK_ORIGIN_ID = Shader.PropertyToID("_GridBlockOrigin");
    MaterialPropertyBlock _patternBlock;

    void LateUpdate() {
        if (transform.hasChanged) applyPatternOrigin();
    }

    void applyPatternOrigin() {
        transform.hasChanged = false;
        if (_patternBlock == null) _patternBlock = new MaterialPropertyBlock();
        foreach (Renderer target in GetComponentsInChildren<Renderer>(true)) {
            target.GetPropertyBlock(_patternBlock);
            _patternBlock.SetVector(GRID_BLOCK_ORIGIN_ID, transform.position);
            target.SetPropertyBlock(_patternBlock);
        }
    }
}
