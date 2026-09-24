using UnityEngine;

// Assembles the board's rim at runtime from the three-piece modular kit documented in
// Docs/FRAME_KIT.md (Board_FrameEdge / Board_FrameCorner / Board_FrameTJunction), instead of using
// the single fixed-size Board_Frame.fbx — that one is authored against one 5x5 grid and one
// 4.19-cell sand area and cannot follow a level's real dimensions. Board_Frame.fbx is left
// untouched; nothing here references it.
//
// This is decoration only. It reads the grid and sand rectangles the rest of Level has ALREADY
// decided (see Level.buildBoardFrame) and never writes back to them: no gameplay, occupancy,
// extraction or sand-simulation value is derived from, or changed by, anything in this file. No
// colliders are created either.
//
// THE KIT'S CONTRACT (Docs/FRAME_KIT.md), restated here because every constant below depends on it:
//  - Pieces are laid out around a WINDOW (the empty rectangle they frame). Every pivot sits exactly
//    ON the window's boundary and the material always falls OUTSIDE it.
//  - Walk each window counter-clockwise — bottom +X, right +Y, top -X, left -Y — and the material is
//    always to the right of travel. Rotation about the board's Z follows that walk: 0 / 90 / 180 / -90.
//  - Corner and T-junction arms each consume HALF a cell (0.425) of the run they sit on, so an
//    N-cell window side is Corner(0.425) + (N-1) x Edge(0.85) + Corner|T(0.425) = N x 0.85.
//  - An Edge is exactly one cell long and may be scaled ALONG ITS LENGTH ONLY. It has no vertices
//    between its two ends, so its cross-section and bevel are unaffected by that scale — and since
//    M_Board is a flat colour with no texture, a stretched Edge is pixel-identical to a tiled run.
//
// ONE EDGE PER RUN (2026-09-14, user's call). FRAME_KIT.md's module rule describes a run as
// "(N-1) x Edge", i.e. one instance per cell. We deliberately do NOT do that: every run — however
// long — is a SINGLE Edge scaled along its length. The two are pixel-identical for the reason above
// (flat colour, no texture, no vertices between the ends, cross-section untouched by a length-axis
// scale), so tiling only bought instances and joins that can never be seen. A 10x15 board went from
// 37 pieces to 13. If a future frame material ever gains a texture or length-wise detail, this is
// the decision to revisit — the module rule in FRAME_KIT.md still describes the geometry correctly.
public class BoardFrame : MonoBehaviour {

    // Thinning factor on the kit as authored (user's call, 2026-09-24: the 0.19 rim read too thick).
    // Applied to the pieces' in-plane cross-section only — Z (height) stays 1. Edges take it on their
    // width axis; Corners and T-junctions take it on both in-plane axes, which thins both arms AND
    // shortens each arm by the same factor, so JOINT_ARM scales with it and the runs grow to meet them.
    const float FRAME_THIN = 0.8f;
    // FRAME_KIT: border width (0.19 as authored), and therefore also the divider's thickness — in the
    // three-piece kit the divider IS an Edge, so it cannot be any other thickness.
    public const float BORDER_WIDTH = 0.19f * FRAME_THIN;
    // FRAME_KIT: the Edge module is exactly one cell.
    const float EDGE_LENGTH = 0.85f;
    // FRAME_KIT: a Corner or T-junction arm covers half a cell of the run it terminates (before thinning).
    const float JOINT_ARM = EDGE_LENGTH * 0.5f * FRAME_THIN;

    // The FBXs are exported with the same Blender contract as the Shape pieces (plane XY, thickness
    // Z, Scale 1.0, -Z Forward / Y Up, bakeAxisConversion off), so they arrive carrying the
    // importer's own (270, 0, 0) on their root. Replacing that with (0, 180, 0) is the identical
    // coordinate-system conversion Shape_*.prefab's FBX_Placeholder already uses — a proper rotation
    // (det +1, no mirror), which matters here because a mirrored corner keeps its silhouette but
    // flips its bevel's normals and reads wrong under URP/Lit. Verified against FRAME_KIT.md's
    // measurements: after this rotation an Edge occupies x [0, 0.85] y [-0.19, 0], a Corner
    // x [-0.19, 0.425] y [-0.19, 0.425] and a T-junction x [-0.425, 0.615] y [-0.19, 0.425], which
    // is exactly the kit's pivot convention.
    static readonly Quaternion FBX_TO_BOARD = Quaternion.Euler(0f, 180f, 0f);

    // FACE FLIP (user's call, 2026-09-24): the kit's authored top face (the one with the 0.02 bevel)
    // is the wrong side to show; its flat bottom face is cleaner. Fixed here at runtime — the FBXs and
    // the .blend are left untouched — by turning every piece 180 degrees so its bottom faces the
    // camera. Each flip is a PROPER rotation (det +1, no mirror, see FBX_TO_BOARD on why that matters)
    // about the piece's own in-plane symmetry axis, so its footprint lands back on itself; the
    // matching FLIP_OFFSET moves the rotated piece back onto its kit pivot and puts its back face on
    // BACK_PLANE_Z again. Offsets are in the kit's own units (before scale) and in board space
    // (after FBX_TO_BOARD). Symmetry was checked against the .blend's vertex positions in-plane.
    //  - Edge: about its length axis (X). y -> -y, so it shifts back by one border width.
    //  - Corner: about the x = y diagonal. x <-> y, so no in-plane shift.
    //  - T-junction: about its stem axis (Y). x -> -x, so it shifts forward by one border width.
    // FRAME_KIT: every piece is 0.25 tall (Z = 0 is its base).
    const float KIT_HEIGHT = 0.25f;
    // FRAME_KIT's 0.19 border width as authored, i.e. before FRAME_THIN.
    const float KIT_BORDER_WIDTH = BORDER_WIDTH / FRAME_THIN;
    static readonly Quaternion EDGE_FLIP = Quaternion.Euler(180f, 0f, 0f);
    static readonly Quaternion CORNER_FLIP = Quaternion.AngleAxis(180f, new Vector3(1f, 1f, 0f));
    static readonly Quaternion T_JUNCTION_FLIP = Quaternion.Euler(0f, 180f, 0f);
    static readonly Vector3 EDGE_FLIP_OFFSET = new Vector3(0f, -KIT_BORDER_WIDTH, -KIT_HEIGHT);
    static readonly Vector3 CORNER_FLIP_OFFSET = new Vector3(0f, 0f, -KIT_HEIGHT);
    static readonly Vector3 T_JUNCTION_FLIP_OFFSET = new Vector3(KIT_BORDER_WIDTH, 0f, -KIT_HEIGHT);

    static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    static readonly int COLOR_ID = Shader.PropertyToID("_Color");

    // World-unit slack for the geometry warnings. A cell is 0.85, so this is ~0.1% of one module.
    const float EPSILON = 0.001f;

    // Where the frame's BACK face sits, in the frame's own local Z. It must be at least as deep as
    // the deepest thing the frame surrounds — Board's floor quad, at FLOOR_DEPTH_OFFSET = 0.2 —
    // because the gameplay camera is PITCHED (Level.CAMERA_PITCH = -20). Under a pitched
    // orthographic camera, depth turns into a vertical screen offset: anything closer than the
    // surface behind it is drawn shifted UP by depth * tan(20 degrees). At the frame's first depth
    // (back face on the sand plane, z = 0) the divider was drawn ~11 screen px above the board
    // floor's top edge, which opened a strip of empty background between the divider and the grid —
    // measured at 7 px in Captures/frame_kit_7x9.png. Sitting the back face ON the floor plane
    // removes it: the divider can now only ever overlap the floor, never uncover it. Overlapping is
    // safe here, a gap is not, so a deeper value would also be fine; a shallower one would not.
    // Mirrors Board.FLOOR_DEPTH_OFFSET (private there); the frame still reads as a raised rim
    // because its front face is 0.25 in front of this.
    const float BACK_PLANE_Z = 0.2f;

    // The kit's own M_Board reads mauve-grey at this camera angle rather than the light lavender it
    // is authored as: measured off the render, its albedo (182, 178, 251) arrives on screen as
    // (158, 130, 187). Rather than re-export three FBXs (M_Board is embedded in each piece,
    // materialLocation = InPrefab — there is no .mat asset to edit), the frame builds ONE material
    // from the kit's own and overrides only _BaseColor.
    //
    // The value is measured, not picked. Rendering two known albedos and solving gives this scene's
    // per-channel response, in LINEAR space: rendered ~= albedo * (0.727, 0.504, 0.511). It is that
    // lopsided because the key light is warm (1.00, 0.98, 0.94 at 1.7) while the Trilight ambient is
    // purple (0.36, 0.31, 0.55) — so green and blue are both starved and a neutral albedo comes back
    // PINK. The consequence worth knowing: even at albedo 1.0 the rendered blue caps at 188, so the
    // mockup's saturated rail (163, 160, 250) is unreachable without touching the lighting, which is
    // out of scope here. So blue is pinned at 1.0 and red/green are set to reproduce the mockup's
    // HUE RELATIONSHIP (red ~= green, blue clearly above both) at the highest value that allows.
    // Verified on screen: (171, 172, 188) against a (43, 10, 152) background — luminance 173 vs 27.
    static readonly Color FRAME_BASE_COLOR = new Color(0.7797f, 0.9123f, 1f, 1f);

    GameObject _edgePrefab;
    GameObject _cornerPrefab;
    GameObject _tJunctionPrefab;
    Material _frameMaterial;

    // The divider's rectangle in the frame's own local space, as actually placed. Exposed for
    // verification and for whatever later wants to sit against the divider; nothing reads it yet.
    public Rect dividerRect { get; private set; }

    // gridWindow and sandWindow are the two empty rectangles to frame, in this object's local space
    // (Level parents it at the origin, so they are Level-local). cellSize is passed only so the
    // module rule can be sanity-checked and so the gap warning can name a gridSandGapCells value.
    public void build(Rect gridWindow, Rect sandWindow, float cellSize,
                      GameObject edgePrefab, GameObject cornerPrefab, GameObject tJunctionPrefab) {
        _edgePrefab = edgePrefab;
        _cornerPrefab = cornerPrefab;
        _tJunctionPrefab = tJunctionPrefab;
        buildFrameMaterial();

        // The kit's module rule is "Edge == 1 cell". It is baked into the FBX, so a project that
        // ever changes cellSize away from 0.85 needs re-exported pieces, not a code change here.
        if (Mathf.Abs(cellSize - EDGE_LENGTH) > EPSILON) {
            Debug.LogWarning($"[BoardFrame::build] Cell size is {cellSize:0.####} but the frame kit's Edge module is {EDGE_LENGTH} (Docs/FRAME_KIT.md). Runs will still close, but they no longer land on cell boundaries.");
        }

        // The frame assumes one outer rim split by one divider, so both windows must share their
        // left and right edges. Level guarantees that (board columns == sand blocks, same width,
        // both centred on the level origin); this only catches a future regression.
        if (Mathf.Abs(gridWindow.xMin - sandWindow.xMin) > EPSILON || Mathf.Abs(gridWindow.xMax - sandWindow.xMax) > EPSILON) {
            Debug.LogWarning($"[BoardFrame::build] Grid window x [{gridWindow.xMin:0.###}, {gridWindow.xMax:0.###}] and sand window x [{sandWindow.xMin:0.###}, {sandWindow.xMax:0.###}] do not match. The frame is built on the grid's width; the sand side will not line up.");
        }

        float left = gridWindow.xMin;
        float right = gridWindow.xMax;
        float bottom = gridWindow.yMin;
        float top = sandWindow.yMax;

        // The divider fills the gap Level already leaves between the board's top edge and the sand's
        // bottom edge (gridSandGapCells). The kit's divider is an Edge, so it is BORDER_WIDTH thick and
        // cannot stretch across; when the real gap is not 0.19 the divider is CENTRED in it, which
        // keeps both joins symmetric and the error halved on each side. The warning names the
        // gridSandGapCells that removes the discrepancy entirely — that knob is purely visual
        // (extraction anchors to the sand's own band, see Level.buildBoard's note), so it is safe to
        // change, but it is the user's call and this file does not touch it.
        float gap = sandWindow.yMin - gridWindow.yMax;
        if (Mathf.Abs(gap - BORDER_WIDTH) > EPSILON) {
            Debug.LogWarning($"[BoardFrame::build] Grid/sand gap is {gap:0.####} but the kit's divider is exactly {BORDER_WIDTH} thick. Divider centred in the gap, overlapping each window by {Mathf.Abs(gap - BORDER_WIDTH) * 0.5f:0.####}. Set GameplayTunables.gridSandGapCells to {BORDER_WIDTH / cellSize:0.#####} for an exact fit.");
        }
        float dividerBottom = gridWindow.yMax + (gap - BORDER_WIDTH) * 0.5f;
        float dividerTop = dividerBottom + BORDER_WIDTH;
        dividerRect = Rect.MinMaxRect(left, dividerBottom, right, dividerTop);

        // --- joints: 4 corners + the 2 T-junctions the divider hangs off -----------------------
        // FRAME_KIT placement table. The corner rotations walk the outer window counter-clockwise
        // from bottom-left; the T-junctions' pivots are the divider face they carry (the right one
        // carries the divider's BOTTOM face, the left one its TOP face), which is what makes the
        // two stems meet the same 0.19 band from opposite sides.
        place(_cornerPrefab, "Corner_BottomLeft", left, bottom, 0f, FRAME_THIN);
        place(_cornerPrefab, "Corner_BottomRight", right, bottom, 90f, FRAME_THIN);
        place(_cornerPrefab, "Corner_TopRight", right, top, 180f, FRAME_THIN);
        place(_cornerPrefab, "Corner_TopLeft", left, top, -90f, FRAME_THIN);
        place(_tJunctionPrefab, "TJunction_Right", right, dividerBottom, 90f, FRAME_THIN);
        place(_tJunctionPrefab, "TJunction_Left", left, dividerTop, -90f, FRAME_THIN);

        // --- runs: every span between two joints ----------------------------------------------
        // Each run starts half a cell in from the joint that opens it and ends half a cell short of
        // the joint that closes it, so a run's length is (side length - 2 x JOINT_ARM) by construction.
        float spanX = (right - JOINT_ARM) - (left + JOINT_ARM);
        float gridSpanY = (dividerBottom - JOINT_ARM) - (bottom + JOINT_ARM);
        float sandSpanY = (top - JOINT_ARM) - (dividerTop + JOINT_ARM);

        emitRun("Edge_Bottom", new Vector2(left + JOINT_ARM, bottom), Vector2.right, spanX, 0f);
        emitRun("Edge_Top", new Vector2(right - JOINT_ARM, top), Vector2.left, spanX, 180f);
        emitRun("Edge_Divider", new Vector2(left + JOINT_ARM, dividerTop), Vector2.right, spanX, 0f);

        emitRun("Edge_RightGrid", new Vector2(right, bottom + JOINT_ARM), Vector2.up, gridSpanY, 90f);
        emitRun("Edge_RightSand", new Vector2(right, dividerTop + JOINT_ARM), Vector2.up, sandSpanY, 90f);
        emitRun("Edge_LeftGrid", new Vector2(left, dividerBottom - JOINT_ARM), Vector2.down, gridSpanY, -90f);
        emitRun("Edge_LeftSand", new Vector2(left, top - JOINT_ARM), Vector2.down, sandSpanY, -90f);
    }

    // Fills `length` starting at `start` and travelling along `direction` with ONE Edge, scaled on
    // its length axis — see the class note on why this replaces the kit's per-cell tiling.
    void emitRun(string name, Vector2 start, Vector2 direction, float length, float rotationZ) {
        if (length < -EPSILON) {
            Debug.LogWarning($"[BoardFrame::emitRun] '{name}' is {length:0.####} long — the window side is shorter than the two joints that close it ({2f * JOINT_ARM:0.####}). The joints will overlap; the level is too small for the frame kit.");
            return;
        }
        if (length <= EPSILON) return; // the two joint arms already close the side.

        place(_edgePrefab, name, start.x, start.y, rotationZ, length / EDGE_LENGTH);
    }

    // One material for every piece, from the kit's own so nothing but the colour changes (shader,
    // smoothness and the rest stay as authored). See FRAME_BASE_COLOR.
    void buildFrameMaterial() {
        MeshRenderer source = _edgePrefab != null ? _edgePrefab.GetComponentInChildren<MeshRenderer>() : null;
        if (source == null || source.sharedMaterial == null) {
            Debug.LogWarning("[BoardFrame::buildFrameMaterial] The Edge piece has no material — the frame will render with each FBX's own embedded M_Board and will not pick up the tuned colour.");
            return;
        }
        _frameMaterial = new Material(source.sharedMaterial);
        _frameMaterial.name = "M_Board (frame runtime)";
        if (_frameMaterial.HasProperty(BASE_COLOR_ID)) _frameMaterial.SetColor(BASE_COLOR_ID, FRAME_BASE_COLOR);
        // URP/Lit's legacy alias, kept in step so anything reading _Color sees the same value.
        if (_frameMaterial.HasProperty(COLOR_ID)) _frameMaterial.SetColor(COLOR_ID, FRAME_BASE_COLOR);
    }

    void OnDestroy() {
        if (_frameMaterial != null) Destroy(_frameMaterial);
    }

    // scaleAlongLength scales the piece's own X, which after FBX_TO_BOARD is its length axis. Y (its
    // in-plane width) always takes FRAME_THIN; Z (height) is never scaled. Corners and T-junctions pass
    // FRAME_THIN as scaleAlongLength too, so they shrink uniformly in-plane and keep their shape.
    void place(GameObject prefab, string name, float x, float y, float rotationZ, float scaleAlongLength) {
        GameObject piece = Instantiate(prefab, transform);
        piece.name = name;
        Transform t = piece.transform;
        Quaternion boardRotation = Quaternion.Euler(0f, 0f, rotationZ);
        getFaceFlip(prefab, out Quaternion flip, out Vector3 flipOffset);
        t.localScale = new Vector3(scaleAlongLength, FRAME_THIN, 1f);
        // The offset scales with the piece (each flip maps every axis onto itself or swaps two equally
        // scaled ones, so the scale still lands on the axes it did before) and turns with it.
        t.localPosition = new Vector3(x, y, BACK_PLANE_Z) + boardRotation * Vector3.Scale(flipOffset, t.localScale);
        // Z first, then the face flip, then the FBX conversion — the same order Shape_*.prefab uses
        // (VisualRoot carries the board-space rotation, the FBX instance under it carries the conversion).
        t.localRotation = boardRotation * flip * FBX_TO_BOARD;
        if (_frameMaterial != null) {
            foreach (MeshRenderer renderer in piece.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterial = _frameMaterial;
        }
    }

    // The FACE FLIP for one of the three kit pieces; anything else is placed as authored.
    void getFaceFlip(GameObject prefab, out Quaternion flip, out Vector3 offset) {
        if (prefab == _edgePrefab) { flip = EDGE_FLIP; offset = EDGE_FLIP_OFFSET; return; }
        if (prefab == _cornerPrefab) { flip = CORNER_FLIP; offset = CORNER_FLIP_OFFSET; return; }
        if (prefab == _tJunctionPrefab) { flip = T_JUNCTION_FLIP; offset = T_JUNCTION_FLIP_OFFSET; return; }
        flip = Quaternion.identity;
        offset = Vector3.zero;
    }
}
