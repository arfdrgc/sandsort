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
    [Tooltip("Holds the fill readout. Kept as its own transform under the anchor so the readout can be offset without disturbing the derived anchor position. Its SCALE is DERIVED from the board cell size (see FILL_READOUT_SCALE_PER_CELL) so every Shape's readout comes out the same size — editing the scale here does nothing.")]
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

    // The 2D sand-fill visual inside the piece, built at runtime by setSandFillSource so the 13 Shape
    // prefabs stay as authored (same rule as the fill badge below). Null until Container hands over
    // the level's unlit sand material; everything that talks to it is null-guarded.
    ShapeSandFill _sandFill;

    // The darkened cavity floor, built the same way and from the same hook. Separate component so the
    // fill effect and the depth cue can be tuned without touching each other.
    ShapeCavityFloor _cavityFloor;

    // The rounded plate behind the fill readout, built at runtime by setFillBadgeMaterial so the 13
    // Shape prefabs stay as authored — the same way Container builds its per-cell cubes. Owned here:
    // both the mesh and the material copy are destroyed with the instance.
    MeshRenderer _fillBadgeRenderer;
    Mesh _fillBadgeMesh;
    Material _fillBadgeMaterial;

    static readonly int ZWRITE_ID = Shader.PropertyToID("_ZWrite");
    // The colour the piece is drawn with, read off the ColorSO material (TCP2 keeps it here;
    // _Color on those materials is a stale yellow and must never be read — see Container 0.8).
    static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    // Measured off the Shape FBXs: a 0.85-unit cell with a 0.08-unit corner fillet. Using the same
    // world radius on the badge keeps the curvature identical to the art sitting right next to it.
    const float FILL_BADGE_CORNER_RADIUS = 0.08f;
    // FillUIAnchor's authored z (-0.5) is exactly the shape's front face, so the plate has to clear
    // it. Small, but ortho depth is linear — this is a wide margin at this camera's 0.1..12.2 range.
    const float FILL_BADGE_FORWARD_OFFSET = 0.01f;
    // Transparent range so URP skips the opaque depth prepass for it, and below TMP's 3000 so the
    // text always draws after the plate.
    const int FILL_BADGE_RENDER_QUEUE = 2990;

    // Shared plate colour behind the fill readout: translucent black, the same on every piece.
    static readonly Color FILL_BADGE_COLOR = new Color(0f, 0f, 0f, 0.9f);
    static readonly int SURFACE_ID = Shader.PropertyToID("_Surface");
    static readonly int BLEND_ID = Shader.PropertyToID("_Blend");
    static readonly int ALPHA_CLIP_ID = Shader.PropertyToID("_AlphaClip");
    static readonly int SRC_BLEND_ID = Shader.PropertyToID("_SrcBlend");
    static readonly int DST_BLEND_ID = Shader.PropertyToID("_DstBlend");
    // Padding around the text, as a fraction of the text's own ink height (so it tracks the font
    // size). Set from the mockup's badge-to-number proportions in Docs/selected_sand_idea_mockup.png,
    // measured off its two clean badges: padX 0.38-0.46 there, padY 0.50-0.58. These sit at or just
    // inside that, so the plate is never looser than the reference.
    const float FILL_BADGE_PAD_X = 0.38f;
    const float FILL_BADGE_PAD_Y = 0.40f;
    // The readout's world size, as a fraction of one board cell. Derived, not authored, for the same
    // reason the anchor's X/Y are (see placeFillAnchor): a cell is the sand's size, so a prefab cannot
    // know it, and deriving it is also what keeps every Shape's badge the same size as every other's.
    //
    // Docs/selected_sand_idea_mockup.png puts the number's ink at 0.125 of a cell (12 px of a 96 px
    // cell), which at this font and font size (untouched) is a 0.0557-per-cell scale. This is twice
    // that: the plate no longer straddles the corner it is placed on, it hangs off it into the piece,
    // so it reads at the larger size. The badge follows from the text, so this one value sizes the
    // whole readout, and deriving it from the cell keeps every Shape's readout the same size.
    // 2026-09-16: +10% (0.1114 -> 0.1225) for readability; text and badge grow together.
    const float FILL_READOUT_SCALE_PER_CELL = 0.1225f;

    public ShapeType type => _type;
    public ShapeRotation rotation => _rotation;
    public IReadOnlyList<Vector2Int> canonicalCells => _canonicalCells;
    public Transform visualRoot => _visualRoot;
    // Where ShapeSandFill's pile starts, as a Transform the particle system can follow. Null until
    // the fill visual has been configured, and null on a piece that has none.
    public Transform sandPourTarget => _sandFill != null ? _sandFill.pourTarget : null;
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

        // Size the readout off the cell, so it reads the same on a 1x1 as on a Plus5 and survives a
        // change of cell size. The badge is measured from the text, so this sizes the badge too — and
        // the badge's world-unit constants (corner radius, clearance) are divided back out by this
        // scale in refreshFillBadge, which keeps the corners' curvature exactly as authored.
        if (_fillPercentageUI != null) {
            _fillPercentageUI.localScale = Vector3.one * (FILL_READOUT_SCALE_PER_CELL * _cellWorldSize);
            refreshFillBadge();
        }
    }

    // Shows `normalized` (0..1) as a whole percentage. Container drives this from its own fill —
    // filledUnits / capacityUnits — so the number on the piece is the real collected-sand figure,
    // never a second capacity model. Safe to call on a shape with no text wired up.
    //
    // Plain TMP text now (2026-09-13): the badge behind it is a real rounded-rect mesh built by
    // refreshFillBadge, which is what TMP's old <mark> highlight could not give — it has no rounded
    // corners. The text keeps its bold number and smaller per-cent sign from
    // Docs/selected_sand_idea_mockup.png.
    public void setFillPercent(float normalized) {
        // The sand-fill visual reads the same 0..1 figure, and reads it FIRST so a shape with no
        // readout wired up still fills. No second capacity model here either — see ShapeSandFill.
        if (_sandFill != null) _sandFill.setFill(normalized);

        if (_fillPercentageText == null) return;
        // 100 only once the piece is actually full. Rounding let 99.5%+ read as 100 while sand was
        // still missing, so anything short of full is floored and capped at 99.
        int percent = normalized >= 1f ? 100 : Mathf.Min(99, Mathf.FloorToInt(Mathf.Clamp01(normalized) * 100f));
        _fillPercentageText.text = $"<b>{percent}<size=65%>%</size></b>";
        refreshFillBadge();
    }

    // Builds (or re-configures) the in-piece sand fill. Called by Container.buildShapeVisuals, which
    // already has both materials: `unlitSource` is the level's own sand material — reused so no new
    // shader enters the build — and `colorMaterial` is this Container's ColorSO material, the one the
    // FBX and the badge are already drawn with, so the fill needs no second colour source.
    //
    // Runtime-built on purpose: the quad goes under VisualRoot, which placeVisualRoot has already
    // rotated and shifted, so the fill lands on occupiedCells without this class knowing the rotation.
    // `tuning` is the Level's GameplayTunables (null = the defaults); the fill reads its knobs live.
    public void setSandFillSource(Material unlitSource, Material colorMaterial, GameplayTunables tuning = null) {
        if (unlitSource == null || _visualRoot == null || _canonicalCells == null || _canonicalCells.Count == 0) return;

        if (_sandFill == null) _sandFill = gameObject.AddComponent<ShapeSandFill>();
        _sandFill.setTuning(tuning);
        _sandFill.configure(_visualRoot, _canonicalCells, _cellWorldSize, unlitSource);

        // Same two materials also drive the darkened cavity floor behind the sand — a separate,
        // independent visual (see ShapeCavityFloor); the fill never reads it and it never reads fill.
        if (_cavityFloor == null) _cavityFloor = gameObject.AddComponent<ShapeCavityFloor>();
        _cavityFloor.configure(_visualRoot, _canonicalCells, _cellWorldSize, unlitSource);

        if (colorMaterial != null && colorMaterial.HasProperty(BASE_COLOR_ID)) {
            Color baseColor = colorMaterial.GetColor(BASE_COLOR_ID);
            _sandFill.setColor(baseColor);
            _cavityFloor.setColor(baseColor);
        }
    }

    // The rounded plate behind the fill readout. Container hands over the same ColorSO material the
    // piece itself is drawn with, so the badge is the container's colour by construction — there is
    // no second colour source to keep in sync.
    //
    // A COPY of that material, per the project's build-materials-from-serialized-assets rule (see
    // Board.buildFloorVisual and SandCylinderRenderer's Android note), because two properties have to
    // change: depth writing off and a transparent-range queue. Both are needed because the readout
    // sits on FillUIAnchor's authored z = -0.5, which is exactly the shape's front face — there is no
    // gap to slot a plate into. So the badge is pushed FORWARD of that face (nothing occludes it) but
    // writes no depth and draws before TMP's queue-3000 text, which keeps the text on top. The
    // anchor, the readout transform and placeFillAnchor's derived position are all left alone.
    public void setFillBadgeMaterial(Material unlitSource) {
        if (unlitSource == null || _fillPercentageText == null || _fillPercentageUI == null) return;

        if (_fillBadgeMaterial != null) Destroy(_fillBadgeMaterial);
        _fillBadgeMaterial = new Material(unlitSource) { name = "FillBadge (runtime)" };

        // One shared look for every piece (2026-09-13): translucent black instead of the Container's
        // own colour. Built from the level's UNLIT sand material, not the ColorSO one, for two
        // reasons: the plate must read the same on all five colours, and unlit means the key light
        // cannot tint it. The badge therefore no longer has a colour source at all.
        //
        // Surface type is switched to Transparent by hand because the source asset is authored
        // Opaque: URP/Unlit needs the blend factors, the ZWrite flag and the keyword set together,
        // not just the queue.
        _fillBadgeMaterial.mainTexture = null;
        if (_fillBadgeMaterial.HasProperty(SURFACE_ID)) _fillBadgeMaterial.SetFloat(SURFACE_ID, 1f);   // Transparent
        if (_fillBadgeMaterial.HasProperty(BLEND_ID)) _fillBadgeMaterial.SetFloat(BLEND_ID, 0f);       // Alpha
        if (_fillBadgeMaterial.HasProperty(ALPHA_CLIP_ID)) _fillBadgeMaterial.SetFloat(ALPHA_CLIP_ID, 0f);
        if (_fillBadgeMaterial.HasProperty(SRC_BLEND_ID)) _fillBadgeMaterial.SetFloat(SRC_BLEND_ID, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (_fillBadgeMaterial.HasProperty(DST_BLEND_ID)) _fillBadgeMaterial.SetFloat(DST_BLEND_ID, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _fillBadgeMaterial.DisableKeyword("_ALPHATEST_ON");
        _fillBadgeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (_fillBadgeMaterial.HasProperty(BASE_COLOR_ID)) _fillBadgeMaterial.SetColor(BASE_COLOR_ID, FILL_BADGE_COLOR);

        // Unchanged from before: no depth writing, and a queue that still sits under TMP's text so
        // the readout stays on top and URP's depth prepass and shadow pass skip this plate.
        if (_fillBadgeMaterial.HasProperty(ZWRITE_ID)) _fillBadgeMaterial.SetFloat(ZWRITE_ID, 0f);
        _fillBadgeMaterial.renderQueue = FILL_BADGE_RENDER_QUEUE;

        if (_fillBadgeRenderer == null) {
            GameObject badge = new GameObject("FillBadgeBackground");
            badge.transform.SetParent(_fillPercentageUI, false);
            _fillBadgeMesh = new Mesh { name = "FillBadgeBackground" };
            badge.AddComponent<MeshFilter>().sharedMesh = _fillBadgeMesh;
            _fillBadgeRenderer = badge.AddComponent<MeshRenderer>();
            _fillBadgeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _fillBadgeRenderer.receiveShadows = false;
        }
        _fillBadgeRenderer.sharedMaterial = _fillBadgeMaterial;
        refreshFillBadge();
    }

    // Sizes the plate to whatever the text currently renders, plus padding. Everything here lives in
    // the readout's own local space — the badge is a child of FillPercentageUI and TMP sits on that
    // same transform, so TMP's local textBounds need no conversion; only the world-unit constants do.
    void refreshFillBadge() {
        if (_fillBadgeRenderer == null || _fillPercentageText == null) return;

        // The INK box of the visible glyphs, not TMP's textBounds: textBounds is the line box, which
        // for this font runs ~55 % taller than the digits and would wrap the number in dead space.
        if (!inkBounds(_fillPercentageText, out Vector2 inkCenter, out Vector2 inkSize)) return;

        // World -> readout-local. The readout is scaled down (0.12 on the prefabs) so the badge's
        // corner radius and its clearance in front of the piece stay world-sized, matching the art.
        float scale = transform.lossyScale.x != 0f ? _fillPercentageUI.lossyScale.x / transform.lossyScale.x : 1f;
        if (scale <= 0f) scale = 1f;

        // Padding is measured in text heights, so the plate keeps its proportions whatever the number
        // is: it grows sideways with "100%" and never changes height.
        Vector2 size = new Vector2(
            inkSize.x + 2f * FILL_BADGE_PAD_X * inkSize.y,
            inkSize.y * (1f + 2f * FILL_BADGE_PAD_Y));
        // Same corner curvature as the shape art's own fillet (measured 0.08 world units on the
        // FBXs), clamped so a short badge can never round past a full pill.
        float radius = Mathf.Min(FILL_BADGE_CORNER_RADIUS / scale, Mathf.Min(size.x, size.y) * 0.5f);
        // Negative z is toward the camera (it sits at -Z looking +Z), i.e. in front of the piece.
        buildRoundedRect(_fillBadgeMesh, inkCenter, size, radius, -FILL_BADGE_FORWARD_OFFSET / scale);

        // FillUIAnchor already IS the bottom-right corner of the piece's bottom-right occupied cell
        // (placeFillAnchor derives it and nothing here touches that). Hang the plate off that corner:
        // its own bottom-right corner on the anchor, growing left and up over the piece.
        //
        // The shift goes on FillPercentageUI — the transform that exists for exactly this — rather
        // than into the badge's own local offset, so FillBadgeBackground stays a plain centred mesh
        // under its unchanged parent and the text, which lives on that same transform, keeps its
        // centred position inside the plate for free. Recomputed with the mesh because the plate
        // widens with the number ("0%" -> "100%").
        Vector2 corner = new Vector2(inkCenter.x + size.x * 0.5f, inkCenter.y - size.y * 0.5f);
        _fillPercentageUI.localPosition = new Vector3(
            -corner.x * scale,
            -corner.y * scale,
            _fillPercentageUI.localPosition.z);
    }

    // Tight box around the glyphs TMP actually drew, in the text's own local space.
    static bool inkBounds(TMPro.TMP_Text text, out Vector2 center, out Vector2 size) {
        center = Vector2.zero;
        size = Vector2.zero;

        text.ForceMeshUpdate();
        TMPro.TMP_TextInfo info = text.textInfo;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        bool any = false;
        for (int i = 0; i < info.characterCount; i++) {
            TMPro.TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue;
            any = true;
            minX = Mathf.Min(minX, character.bottomLeft.x);
            maxX = Mathf.Max(maxX, character.topRight.x);
            minY = Mathf.Min(minY, character.bottomLeft.y);
            maxY = Mathf.Max(maxY, character.topRight.y);
        }
        if (!any) return false;

        center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        size = new Vector2(maxX - minX, maxY - minY);
        return size.x > 0f && size.y > 0f;
    }

    void OnDestroy() {
        if (_fillBadgeMaterial != null) Destroy(_fillBadgeMaterial);
        if (_fillBadgeMesh != null) Destroy(_fillBadgeMesh);
    }

    // A centre-fan rounded rectangle in the XY plane, facing -Z like everything else the camera sees
    // (the board lies flat in XY with the camera at negative Z). Wound clockwise in XY, which is the
    // front-facing direction toward -Z — the same winding Unity's own Quad primitive uses.
    static void buildRoundedRect(Mesh mesh, Vector2 center, Vector2 size, float radius, float z) {
        const int CORNER_SEGMENTS = 6;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;
        radius = Mathf.Clamp(radius, 0f, Mathf.Min(halfW, halfH));

        Vector2[] arcCenters = {
            new Vector2(center.x + halfW - radius, center.y + halfH - radius),
            new Vector2(center.x - halfW + radius, center.y + halfH - radius),
            new Vector2(center.x - halfW + radius, center.y - halfH + radius),
            new Vector2(center.x + halfW - radius, center.y - halfH + radius),
        };

        int ring = 4 * (CORNER_SEGMENTS + 1);
        Vector3[] vertices = new Vector3[ring + 1];
        Vector3[] normals = new Vector3[ring + 1];
        Vector2[] uvs = new Vector2[ring + 1];
        vertices[0] = new Vector3(center.x, center.y, z);

        int v = 1;
        for (int corner = 0; corner < 4; corner++) {
            for (int step = 0; step <= CORNER_SEGMENTS; step++) {
                float angle = (corner * 90f + 90f * step / CORNER_SEGMENTS) * Mathf.Deg2Rad;
                vertices[v++] = new Vector3(
                    arcCenters[corner].x + Mathf.Cos(angle) * radius,
                    arcCenters[corner].y + Mathf.Sin(angle) * radius,
                    z);
            }
        }
        for (int i = 0; i < vertices.Length; i++) {
            normals[i] = new Vector3(0f, 0f, -1f);
            uvs[i] = new Vector2(
                halfW > 0f ? (vertices[i].x - center.x) / (2f * halfW) + 0.5f : 0.5f,
                halfH > 0f ? (vertices[i].y - center.y) / (2f * halfH) + 0.5f : 0.5f);
        }

        int[] triangles = new int[ring * 3];
        for (int i = 0; i < ring; i++) {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = 1 + (i + 1) % ring;
            triangles[i * 3 + 2] = 1 + i;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
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
