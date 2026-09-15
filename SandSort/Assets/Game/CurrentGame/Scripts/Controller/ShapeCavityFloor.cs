using System.Collections.Generic;
using UnityEngine;

// Darkens the inside floor of a Shape's cavity so the piece reads as a box with depth rather than a
// flat tile (2026-09-13).
//
// WHY A QUAD AND NOT A MATERIAL: every one of the 13 Shape FBXs is a SINGLE submesh with a SINGLE
// material (measured), so the cavity floor cannot be given its own material or its own shade through
// the mesh. Its normal is also identical to the outer front rim's — both face the camera — so nothing
// in the shading can tell them apart either. What DOES separate them is depth: the rim sits at
// z = -0.500 and the cavity floor at z = -0.086..-0.080 in Shape space, the same in all 13 shapes.
// So a thin masked quad laid just in front of that floor covers it and nothing else: where the piece
// has walls, the wall geometry is nearer the camera and wins the depth test, so the quad never
// changes the look of an outer surface.
//
// The colour is DERIVED from the Container's own ColorSO colour (one multiplier, no second palette),
// and the quad is laid out in canonical cell space under VisualRoot, so rotation and the
// normalisation shift come along for free — the same arrangement ShapeSandFill uses.
//
// Independent of ShapeSandFill: that quad travels from z -0.08 toward the camera as the piece fills
// and simply passes in front of this one. Nothing here reads or writes fill.
public class ShapeCavityFloor : MonoBehaviour {

    // Just in front of the frontmost cavity-floor vertex (-0.086), far behind the rim (-0.500), and
    // behind where ShapeSandFill's surface becomes visible — so the sand always draws over it.
    const float CAVITY_FLOOR_Z = -0.092f;

    // How much darker than the piece's own colour. Deliberately mild: a depth cue, not a black hole.
    const float DARKEN = 0.62f;

    // Enough texels per cell that a cell boundary always lands on a texel boundary under Point
    // filtering, which is what makes the mask exact for shapes with interior holes (T4, Plus5, U5).
    const int PIXELS_PER_CELL = 8;

    Transform _quad;
    Renderer _renderer;
    Material _material;
    Texture2D _texture;

    bool[] _mask;
    int _minX, _minY, _cellsWide, _cellsHigh;
    float _cellWorldSize;
    Color _baseColor = Color.white;
    bool _built;

    // Called by Shape from the same hook that sets up the sand fill, which already has both the
    // level's unlit material and this Container's colour material.
    public void configure(Transform visualRoot, IReadOnlyList<Vector2Int> canonicalCells, float cellWorldSize, Material unlitSource) {
        if (visualRoot == null || canonicalCells == null || canonicalCells.Count == 0 || unlitSource == null) return;
        if (cellWorldSize <= 0f) return;

        _cellWorldSize = cellWorldSize;
        buildMask(canonicalCells);
        ensureQuad(visualRoot, unlitSource);
        _built = true;
        redraw();
    }

    public void setColor(Color color) {
        _baseColor = color;
        if (_built) redraw();
    }

    void buildMask(IReadOnlyList<Vector2Int> cells) {
        _minX = int.MaxValue; _minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < cells.Count; i++) {
            _minX = Mathf.Min(_minX, cells[i].x); maxX = Mathf.Max(maxX, cells[i].x);
            _minY = Mathf.Min(_minY, cells[i].y); maxY = Mathf.Max(maxY, cells[i].y);
        }
        _cellsWide = maxX - _minX + 1;
        _cellsHigh = maxY - _minY + 1;

        _mask = new bool[_cellsWide * _cellsHigh];
        for (int i = 0; i < cells.Count; i++)
            _mask[(cells[i].x - _minX) + (cells[i].y - _minY) * _cellsWide] = true;

        if (_texture != null) Destroy(_texture);
        _texture = new Texture2D(_cellsWide * PIXELS_PER_CELL, _cellsHigh * PIXELS_PER_CELL, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        if (_material != null) _material.mainTexture = _texture;
    }

    void ensureQuad(Transform visualRoot, Material unlitSource) {
        if (_quad == null) {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CavityFloor";
            Destroy(go.GetComponent<Collider>());
            _quad = go.transform;
            _renderer = go.GetComponent<Renderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }
        _quad.SetParent(visualRoot, false);

        if (_material == null) {
            // A runtime copy of the level's sand material, never the asset. Alpha clip so the cells a
            // shape does not occupy stay completely absent and depth stays correct against the FBX.
            _material = new Material(unlitSource) { name = "CavityFloor (runtime)" };
            if (_material.HasProperty("_AlphaClip")) _material.SetFloat("_AlphaClip", 1f);
            if (_material.HasProperty("_Cutoff")) _material.SetFloat("_Cutoff", 0.5f);
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", Color.white);
            _material.EnableKeyword("_ALPHATEST_ON");
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            _renderer.sharedMaterial = _material;
        }
        _material.mainTexture = _texture;

        _quad.localScale = new Vector3(_cellsWide * _cellWorldSize, _cellsHigh * _cellWorldSize, 1f);
        _quad.localPosition = new Vector3(
            (_minX + (_cellsWide - 1) * 0.5f) * _cellWorldSize,
            (_minY + (_cellsHigh - 1) * 0.5f) * _cellWorldSize,
            CAVITY_FLOOR_Z);
        _quad.localRotation = Quaternion.identity;
    }

    void redraw() {
        if (_texture == null) return;

        Color dark = _baseColor * DARKEN;
        var opaque = new Color32(
            (byte)Mathf.Clamp(dark.r * 255f, 0f, 255f),
            (byte)Mathf.Clamp(dark.g * 255f, 0f, 255f),
            (byte)Mathf.Clamp(dark.b * 255f, 0f, 255f),
            255);
        var clear = new Color32(0, 0, 0, 0);

        int w = _texture.width, h = _texture.height;
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = _mask[(x / PIXELS_PER_CELL) + (y / PIXELS_PER_CELL) * _cellsWide] ? opaque : clear;

        _texture.SetPixels32(pixels);
        _texture.Apply(false);
    }

    void OnDestroy() {
        if (_texture != null) Destroy(_texture);
        if (_material != null) Destroy(_material);
    }
}
