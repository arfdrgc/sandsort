using System;
using System.Collections.Generic;
using UnityEngine;

// Prototype default: a discrete grid. Containers occupy an arbitrary set of cells (a "shape" —
// see Container._shape); two containers can never share a cell. See SandLevelSO's header
// comment for the full list of prototype defaults this resolves from game_mechanics.md's Open
// Design Questions.
//
// Layout convention (2026-09-11): the Board lies flat in the XY plane at local Z ~ 0, facing
// a camera looking down +Z — like a phone screen, not a 3D tabletop. Grid cell.x -> world X
// (left/right), cell.y -> world Y (up/down). The sand area sits directly above the Board along
// this same Y axis, matching the reference concept art (picture on top, containers below).
// Row (size.y - 1) is the topmost row, directly under the sand area — see topRow and
// ExtractionGrid's extraction rule.
//
// Cell size (2026-09-11): no longer a fixed 1 world unit. Level.buildBoard passes
// SandCylinderTunables.CubeWorldSize — exactly one SandCylinderDemo sand block's world width — so
// board column c sits exactly under sand block c. See Level.buildBoard for the full alignment.
public class Board : MonoBehaviour {

    // Retired: only the old SandCanvas.cs (no longer built by Level, pending deletion) still reads
    // this. Board geometry itself uses cellSize.
    public const float CELL_SIZE = 1f;
    const float FLOOR_DEPTH_OFFSET = 0.2f;

    Vector2Int _size;
    float _cellSize = 1f;
    // Flat 1D array (index = x + y * _size.x), NOT Container[,] — Unity cannot serialize
    // rectangular arrays, so a 2D array silently resets to null on a domain reload triggered
    // mid-Play (e.g. a script recompile), even though this field is never meant to be inspector-visible.
    Container[] _occupancy;
    // Cells held by a fixed Grid Block (see GridBlock). Same flat layout as _occupancy, kept apart
    // from it because a Grid Block is not a Container and is never freed.
    bool[] _blocked;

    public Vector2Int size => _size;
    public float cellSize => _cellSize;
    public int topRow => _size.y - 1;

    // Floor look (mockup reference): dark rounded tiles separated by thin gaps, so every cell reads
    // on its own. Painted into ONE texture on ONE quad — no per-cell GameObjects — and purely
    // visual: occupancy and grid math never look at it.
    const int FLOOR_PIXELS_PER_CELL = 96;
    const float FLOOR_CELL_GAP = 0.035f;          // per side, as a fraction of a cell
    const float FLOOR_CELL_CORNER_RADIUS = 0.12f; // as a fraction of a cell
    static readonly Color32 FLOOR_CELL_TOP_COLOR = new Color32(98, 110, 170, 255);
    static readonly Color32 FLOOR_CELL_BOTTOM_COLOR = new Color32(76, 86, 142, 255);
    static readonly Color32 FLOOR_GAP_COLOR = new Color32(46, 52, 96, 255);
    // Checkerboard: every other cell ((x + y) odd) is lifted this far toward white, so equal tones
    // never touch horizontally or vertically.
    const float FLOOR_CHECKER_LIGHTEN = 0.11f;

    // Raised-tile shading (reference: soft rounded slabs lit from the top-left). All widths are in
    // cell units measured inward from the tile edge.
    static readonly Vector2 FLOOR_LIGHT_DIRECTION = new Vector2(-1f, 1f).normalized; // top-left, texture v is up
    const float FLOOR_EDGE_SOFTEN_WIDTH = 0.022f; // soft dark falloff into the gap, all around
    const float FLOOR_EDGE_SOFTEN = 0.28f;
    // Bright arc just inside the top/left edge, strongest at the top-left corner.
    const float FLOOR_HIGHLIGHT_START = 0.006f;
    const float FLOOR_HIGHLIGHT_END = 0.05f;
    const float FLOOR_HIGHLIGHT = 0.52f;          // max lerp toward white
    // Darker side band along the bottom/right edge — the slab's visible side.
    const float FLOOR_SHADE_WIDTH = 0.075f;
    const float FLOOR_SHADE = 0.48f;              // max darkening
    // Flat face: an inner rounded rect this far in from the tile edge, sitting a touch lower than
    // the rim around it, with a faint lip where the two meet.
    const float FLOOR_FACE_INSET = 0.1f;
    const float FLOOR_FACE_CORNER_RADIUS = 0.07f;
    const float FLOOR_FACE_RECESS = 0.05f;        // face darkening relative to the rim
    const float FLOOR_FACE_LIP_WIDTH = 0.018f;
    const float FLOOR_FACE_LIP = 0.1f;

    Texture2D _floorTexture;
    Material _floorMaterial;
    Renderer _floorRenderer;

    // World bounds of the board floor (every cell) — what Level frames the camera on, together
    // with the sand area.
    public Bounds floorBounds => _floorRenderer != null ? _floorRenderer.bounds : new Bounds(transform.position, Vector3.zero);

    // floorBaseMaterial must be a pre-authored unlit texture material (Level passes
    // SandCylinderSandUnlit.mat) — copied, never built via Shader.Find; see SandCylinderRenderer's
    // Android note.
    public void initialize(Vector2Int size, float cellSize, Material floorBaseMaterial) {
        _size = size;
        _cellSize = cellSize;
        _occupancy = new Container[size.x * size.y];
        _blocked = new bool[size.x * size.y];

        buildFloorVisual(size, floorBaseMaterial);
    }

    void OnDestroy() {
        if (_floorTexture != null) Destroy(_floorTexture);
        if (_floorMaterial != null) Destroy(_floorMaterial);
    }

    void buildFloorVisual(Vector2Int size, Material floorBaseMaterial) {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "BoardFloor";
        floor.transform.SetParent(transform, false);
        floor.transform.localPosition = boundsCenterOffset(Vector2Int.zero, size) + new Vector3(0f, 0f, FLOOR_DEPTH_OFFSET);
        floor.transform.localScale = new Vector3(size.x * _cellSize, size.y * _cellSize, 1f);
        Destroy(floor.GetComponent<Collider>());
        _floorRenderer = floor.GetComponent<Renderer>();

        if (floorBaseMaterial == null) {
            Debug.LogError("[Board::buildFloorVisual] No floor base material — assign the Level prefab's Sand Material.");
            return;
        }

        _floorTexture = buildFloorTexture(size);
        _floorMaterial = new Material(floorBaseMaterial);
        _floorMaterial.mainTexture = _floorTexture;
        floor.GetComponent<Renderer>().sharedMaterial = _floorMaterial;
    }

    // Texture row 0 / column 0 is the quad's bottom-left, i.e. board cell (0,0), matching the
    // cell.x -> X, cell.y -> Y layout convention above.
    static Texture2D buildFloorTexture(Vector2Int size) {
        int pixelsPerCell = FLOOR_PIXELS_PER_CELL;
        int width = size.x * pixelsPerCell;
        int height = size.y * pixelsPerCell;
        float antiAliasBand = 2f / pixelsPerCell;

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++) {
            int cellY = y / pixelsPerCell;
            float v = (y % pixelsPerCell + 0.5f) / pixelsPerCell;
            Color baseTileColor = Color.Lerp(FLOOR_CELL_BOTTOM_COLOR, FLOOR_CELL_TOP_COLOR, v);

            for (int x = 0; x < width; x++) {
                int cellX = x / pixelsPerCell;
                float u = (x % pixelsPerCell + 0.5f) / pixelsPerCell;
                float distance = roundedTileDistance(u, v);

                Color tileColor = baseTileColor;
                if (((cellX + cellY) & 1) == 1) tileColor = Color.Lerp(tileColor, Color.white, FLOOR_CHECKER_LIGHTEN);
                tileColor = shadeRaisedTile(tileColor, u, v, -distance);

                float coverage = Mathf.Clamp01(0.5f - distance / antiAliasBand);
                pixels[y * width + x] = Color.Lerp(FLOOR_GAP_COLOR, tileColor, coverage);
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    // Lights one tile pixel as a soft raised slab from the top-left. `depth` is how far (cell units)
    // the pixel lies inside the tile edge; negative in the gap, where only the anti-aliased rim of
    // this colour is ever seen.
    static Color shadeRaisedTile(Color color, float u, float v, float depth) {
        float facing = Vector2.Dot(roundedRectNormal(u, v, FLOOR_CELL_GAP, FLOOR_CELL_CORNER_RADIUS), FLOOR_LIGHT_DIRECTION);

        // Soft dark falloff toward the gap, all around.
        color *= 1f - FLOOR_EDGE_SOFTEN * (1f - Mathf.SmoothStep(0f, 1f, depth / FLOOR_EDGE_SOFTEN_WIDTH));

        // Top/left highlight: a band just inside the edge; squaring `facing` narrows it into an arc
        // that peaks at the top-left corner and fades along both edges.
        if (facing > 0f) {
            float band = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FLOOR_HIGHLIGHT_START, FLOOR_HIGHLIGHT_START * 3f, depth))
                       * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FLOOR_HIGHLIGHT_END * 0.5f, FLOOR_HIGHLIGHT_END, depth)));
            color = Color.Lerp(color, Color.white, FLOOR_HIGHLIGHT * facing * facing * band);
        }
        // Bottom/right side band: solid for most of its width, then a soft fade into the rim.
        else {
            float band = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FLOOR_SHADE_WIDTH * 0.55f, FLOOR_SHADE_WIDTH, depth));
            color *= 1f - FLOOR_SHADE * -facing * band;
        }

        // Flat, slightly recessed face with a faint lip: shaded on its top/left (the rim casts onto
        // it), caught by the light on its bottom/right — the reverse of the outer edge.
        float faceDistance = roundedRectDistance(u, v, FLOOR_CELL_GAP + FLOOR_FACE_INSET, FLOOR_FACE_CORNER_RADIUS);
        color *= 1f - FLOOR_FACE_RECESS * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.006f, -0.006f, faceDistance));
        float lip = 1f - Mathf.Clamp01(Mathf.Abs(faceDistance) / FLOOR_FACE_LIP_WIDTH);
        if (lip > 0f) {
            float faceFacing = Vector2.Dot(roundedRectNormal(u, v, FLOOR_CELL_GAP + FLOOR_FACE_INSET, FLOOR_FACE_CORNER_RADIUS), FLOOR_LIGHT_DIRECTION);
            lip *= lip;
            if (faceFacing > 0f) color *= 1f - FLOOR_FACE_LIP * faceFacing * lip;
            else color = Color.Lerp(color, Color.white, FLOOR_FACE_LIP * 0.5f * -faceFacing * lip);
        }

        color.a = 1f;
        return color;
    }

    // Signed distance, in cell units, from (u, v) inside one cell to the edge of that cell's
    // rounded tile: negative inside the tile, positive in the gap around it.
    static float roundedTileDistance(float u, float v) {
        return roundedRectDistance(u, v, FLOOR_CELL_GAP, FLOOR_CELL_CORNER_RADIUS);
    }

    // Signed distance to a rounded rect centred in the cell, `inset` in from each cell side.
    static float roundedRectDistance(float u, float v, float inset, float cornerRadius) {
        float innerHalfExtent = 0.5f - inset - cornerRadius;
        float qx = Mathf.Abs(u - 0.5f) - innerHalfExtent;
        float qy = Mathf.Abs(v - 0.5f) - innerHalfExtent;
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - cornerRadius;
    }

    // Outward unit normal of that rounded rect's edge nearest (u, v) — the distance field's gradient.
    static Vector2 roundedRectNormal(float u, float v, float inset, float cornerRadius) {
        const float step = 0.004f;
        return new Vector2(
            roundedRectDistance(u + step, v, inset, cornerRadius) - roundedRectDistance(u - step, v, inset, cornerRadius),
            roundedRectDistance(u, v + step, inset, cornerRadius) - roundedRectDistance(u, v - step, inset, cornerRadius)).normalized;
    }

    int cellIndex(int x, int y) => x + y * _size.x;

    // -- Shape helpers -----------------------------------------------------------------------
    // A "shape" is a list of cell offsets relative to an anchor position (ContainerData.cells /
    // Container.shape). These helpers turn that into absolute cells, bounding boxes, and the
    // local-space center used for visual placement — generalizing the old single-rectangle
    // footprint math to arbitrary shapes (1x1, 2x1, L, T, ...).

    public static Vector2Int shapeMin(IReadOnlyList<Vector2Int> shape) {
        Vector2Int min = shape[0];
        for (int i = 1; i < shape.Count; i++) {
            min.x = Mathf.Min(min.x, shape[i].x);
            min.y = Mathf.Min(min.y, shape[i].y);
        }
        return min;
    }

    public static Vector2Int shapeMax(IReadOnlyList<Vector2Int> shape) {
        Vector2Int max = shape[0];
        for (int i = 1; i < shape.Count; i++) {
            max.x = Mathf.Max(max.x, shape[i].x);
            max.y = Mathf.Max(max.y, shape[i].y);
        }
        return max;
    }

    // Local-space center of the shape's bounding box when its anchor sits at cell (0,0) of THIS
    // Board. No longer used by the anchor <-> world helpers below (they place the root at cell
    // (0,0), see anchorToWorldCenter).
    public Vector3 shapeCenterOffset(IReadOnlyList<Vector2Int> shape) {
        Vector2Int min = shapeMin(shape);
        Vector2Int max = shapeMax(shape);
        return new Vector3((min.x + max.x) * _cellSize * 0.5f, (min.y + max.y) * _cellSize * 0.5f, 0f);
    }

    Vector3 boundsCenterOffset(Vector2Int anchor, Vector2Int size) {
        return new Vector3(anchor.x + (size.x - 1) * 0.5f, anchor.y + (size.y - 1) * 0.5f, 0f) * _cellSize;
    }

    public bool isInBounds(Vector2Int anchor, IReadOnlyList<Vector2Int> shape) {
        foreach (Vector2Int offset in shape) {
            Vector2Int cell = anchor + offset;
            if (cell.x < 0 || cell.y < 0 || cell.x >= _size.x || cell.y >= _size.y) return false;
        }
        return true;
    }

    public bool isAreaFree(Vector2Int anchor, IReadOnlyList<Vector2Int> shape, Container ignore = null) {
        if (!isInBounds(anchor, shape)) return false;

        foreach (Vector2Int offset in shape) {
            Vector2Int cell = anchor + offset;
            if (_blocked[cellIndex(cell.x, cell.y)]) return false;
            Container occupant = _occupancy[cellIndex(cell.x, cell.y)];
            if (occupant != null && occupant != ignore) return false;
        }
        return true;
    }

    // True when a fixed Grid Block sits on `cell`. False for an out-of-bounds cell.
    public bool isBlocked(Vector2Int cell) {
        if (cell.x < 0 || cell.y < 0 || cell.x >= _size.x || cell.y >= _size.y) return false;
        return _blocked[cellIndex(cell.x, cell.y)];
    }

    // Marks `cell` as a fixed obstacle for the rest of the level. Only GridBlock.initialize calls it.
    public void block(Vector2Int cell) {
        if (cell.x < 0 || cell.y < 0 || cell.x >= _size.x || cell.y >= _size.y) return;
        _blocked[cellIndex(cell.x, cell.y)] = true;
    }

    // The Container occupying `cell`, or null for an empty or out-of-bounds cell.
    public Container occupantAt(Vector2Int cell) {
        if (cell.x < 0 || cell.y < 0 || cell.x >= _size.x || cell.y >= _size.y) return null;
        return _occupancy[cellIndex(cell.x, cell.y)];
    }

    public void occupy(Container container) {
        setArea(container.gridPosition, container.shape, container);
    }

    public void free(Container container) {
        setArea(container.gridPosition, container.shape, null);
    }

    void setArea(Vector2Int anchor, IReadOnlyList<Vector2Int> shape, Container value) {
        foreach (Vector2Int offset in shape) {
            Vector2Int cell = anchor + offset;
            _occupancy[cellIndex(cell.x, cell.y)] = value;
        }
    }

    public Vector3 cellToLocalPosition(Vector2Int cell) {
        return new Vector3(cell.x * _cellSize, cell.y * _cellSize, 0f);
    }

    // World-space center of a single board cell — where ExtractionGrid places an extraction point.
    public Vector3 cellToWorldCenter(Vector2Int cell) {
        return transform.TransformPoint(cellToLocalPosition(cell));
    }

    // Where a Container's ROOT goes: the world centre of the shape's cell (0,0) at this anchor — NOT
    // the shape's bounding-box centre (2026-09-13). Everything under the root is laid out from cell
    // (0,0): Container's per-cell cubes at offset * cellSize, Shape's VisualRoot/FBX origin and its
    // FillUIAnchor. Adding shapeCenterOffset here (left over from when the root itself was one
    // centred primitive) drew every multi-cell shape (W-1)/2, (H-1)/2 cells off its real occupancy.
    // `shape` is no longer read; kept so callers stay unchanged.
    public Vector3 anchorToWorldCenter(Vector2Int anchor, IReadOnlyList<Vector2Int> shape) {
        return transform.TransformPoint(cellToLocalPosition(anchor));
    }

    // Continuous (fractional) counterparts of anchorToWorldCenter, in cell units — no rounding and
    // no clamping. Used only by Container's drag: the pointer position becomes a fractional anchor
    // the grid position steps toward, and the visual glides between anchors. Occupancy and
    // extraction never see fractional anchors. The two stay exact inverses of each other; the drag
    // only ever uses pointer DELTAS (Container.beginDrag's grab offset), so the reference point does
    // not change how a drag feels.
    public Vector2 worldToAnchorPoint(Vector3 worldPosition, IReadOnlyList<Vector2Int> shape) {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return new Vector2(local.x / _cellSize, local.y / _cellSize);
    }

    public Vector3 anchorPointToWorldCenter(Vector2 anchor, IReadOnlyList<Vector2Int> shape) {
        Vector3 local = new Vector3(anchor.x * _cellSize, anchor.y * _cellSize, 0f);
        return transform.TransformPoint(local);
    }

    // Enumerates every in-bounds anchor position this shape could ever occupy and returns true if
    // any of them satisfies isTargetPosition — deliberately IGNORING current occupancy (other
    // Containers). Currently unused: the deadlock check that called it is off until Phase 3 (see
    // Level.Update).
    public bool hasAnyValidPosition(IReadOnlyList<Vector2Int> shape, Func<Vector2Int, bool> isTargetPosition) {
        Vector2Int min = shapeMin(shape);
        Vector2Int max = shapeMax(shape);

        for (int x = -min.x; x <= _size.x - 1 - max.x; x++) {
            for (int y = -min.y; y <= _size.y - 1 - max.y; y++) {
                if (isTargetPosition(new Vector2Int(x, y))) return true;
            }
        }

        return false;
    }
}
