using System.Collections.Generic;
using UnityEngine;

// 2D sand-fill visual for one Shape (2026-09-13). Deliberately NOT a simulation: it mimics sand
// poured onto a single spot — it piles up where it lands and spreads outward — using the technique
// the sand area already uses (SandCylinderRenderer): a CPU-written Texture2D drawn on an unlit quad,
// Point-filtered, with the same Hash01 grain and the same 34 px per cell, so the grain reads at the
// same scale as the sand the pieces are filled from. No new shader is introduced, so nothing new can
// be stripped out of an IL2CPP build (see SandCylinderRenderer's Android note); the material handed
// in is the level's own sand material and only a runtime COPY of it is ever touched.
//
// The quad is parented to Shape.visualRoot and laid out in CANONICAL cell space. That is the whole
// trick for rotation: placeVisualRoot already rotates and normalisation-shifts that transform so the
// FBX lands on occupiedCells, so anything authored canonically underneath it rides along for free and
// can never drift from the art.
//
// Two rules the design is built around:
//   - occupiedCells is a HARD boundary. A pixel outside the mask is never written (alpha 0), so a
//     T4's or Plus5's empty cell cannot fill at any fill level, including 100%.
//   - The fill does NOT rise in Y. The surface travels in Z, from the back of the cavity toward the
//     camera, so the piece reads as filling up from the inside out.
//
// Nothing here feeds gameplay: capacity, fillLevel, extraction and the sand grid are untouched. This
// class only ever reads the 0..1 figure Container already pushes through Shape.setFillPercent.
public class ShapeSandFill : MonoBehaviour {

    // Texture resolution per board cell. 34 matches SandCylinderTunables.blockCellSize, which is what
    // the sand area paints a cell at — so a grain here is the same size as a grain up in the sand.
    const int PIXELS_PER_CELL = 34;

    // Where the fill surface sits along the Shape's Z at empty and at full. The FBX occupies
    // z in [-0.500, 0.000] in Shape space (measured from its mesh bounds), with -0.5 the front face
    // toward the camera and 0.0 the back. So the surface starts deep inside the cavity and climbs
    // toward the rim WITHOUT ever moving in Y.
    const float Z_EMPTY = -0.08f;
    const float Z_FULL = -0.44f;

    // Colour ramp from the poured centre to the outer edge of the pile: the same base colour, lifted
    // at the core and dropped toward the rim, which is what reads as depth in a real heap.
    // Raised 2026-09-13: at 1.18/0.55 the pile read DARKER than the cavity it sits in, because the
    // cavity is lit by the key light while this quad is unlit and draws the base colour almost raw.
    // The range is lifted and compressed so even the outer edge of the pile is around the full base
    // colour, while the centre still runs clearly brighter than the rim.
    const float CENTER_GAIN = 1.40f;
    const float EDGE_GAIN = 0.90f;

    // Per-pixel jitter on the spread boundary, so the frontier is a grainy pile edge and not a clean
    // circle. In units of the normalised radius.
    const float EDGE_NOISE = 0.16f;

    // Per-pixel brightness jitter. Same value and same formula as SandCylinderTunables.colorNoiseAmount.
    const float GRAIN_NOISE = 0.14f;

    // fill -> radius curve. Area grows with radius squared, so a linear radius would make the early
    // percentages look far too fast; a sub-linear exponent keeps the pile growing at a believable rate.
    const float SPREAD_EXPONENT = 0.72f;

    // The bright core never collapses to a point, and never stops being a core: the ramp is keyed to a
    // fraction of the CURRENT radius, so at 100% the piece is full but still reads dense in the middle.
    const float CORE_FRACTION = 0.55f;

    // How fast the drawn fill chases the real one, and how much it has to move before the texture is
    // worth rewriting. The texture is NOT rebuilt per frame — only when the drawn value actually moves.
    const float FILL_SHARPNESS = 7f;
    const float REBUILD_EPSILON = 1f / 160f;

    Transform _quad;
    Renderer _renderer;
    Material _material;
    Texture2D _texture;
    Color32[] _pixels;

    // Mask and geometry, all in canonical cell space. Rebuilt only when the cell list or cell size
    // changes, never per frame.
    bool[] _mask;
    int _minX, _minY, _cellsWide, _cellsHigh, _texWidth, _texHeight;
    float _centerX, _centerY, _maxRadius;
    Vector2[] _origins = System.Array.Empty<Vector2>();   // pour points, local cell space (min already subtracted)
    float _centroidX, _centroidY;
    float _cellWorldSize;
    bool _built;

    Color _baseColor = Color.white;
    float _target;
    float _drawn = -1f;
    float _lastDrawnTexture = -1f;

    // TEST HOOK (2026-09-13): where the pile starts, in canonical cell coordinates. Null/empty = the
    // occupied-cells centroid, which is the behaviour this class has always had and still defaults to.
    // Fractional coordinates are allowed (so the origin can sit part-way between the extraction cell
    // and the centroid), and more than one origin is allowed (so a two-armed piece like U5 can pile in
    // both arms): a pixel's distance is then the distance to the NEAREST origin. Used to A/B the pile
    // origin against the incoming particle stream. Nothing in the game sets it.
    Vector2[] _pourOriginCells;

    public void setPourOrigins(Vector2[] canonicalOrigins) {
        _pourOriginCells = canonicalOrigins != null && canonicalOrigins.Length > 0 ? canonicalOrigins : null;
        if (!_built) return;
        recomputeOrigin();
        _lastDrawnTexture = -1f;
        redraw(Mathf.Max(0f, _drawn));
    }

    public void setPourOriginCell(Vector2Int? canonicalCell) {
        setPourOrigins(canonicalCell.HasValue ? new[] { (Vector2)canonicalCell.Value } : null);
    }

    public Vector2 pourOriginInCells => new Vector2(_centerX, _centerY);

    public Vector2[] pourOriginsInCells => _origins;

    public float pourMaxRadius => _maxRadius;

    // An empty Transform sitting exactly where the pile starts, in the SAME space as the piece's cell
    // visuals — parented under VisualRoot, so it rotates, shifts and drags along with the shape for
    // free, and its world Z is the cell visuals' own Z (local 0), which is what the particle effect's
    // FollowPourForwardOffset is measured against. ExtractionGrid hands it to the particle system as
    // the grain target (2026-09-13), so the falling sand lands on the heap instead of on the cell it
    // was pulled through and the two read as one pour. Nothing about the fill picture, the mask or the
    // fill curve is affected by this — it is a marker, and this class never reads it back.
    Transform _pourTarget;

    public Transform pourTarget => _pourTarget;

    void ensurePourTarget(Transform visualRoot) {
        if (_pourTarget == null) {
            var go = new GameObject("PourTarget");
            _pourTarget = go.transform;
        }
        _pourTarget.SetParent(visualRoot, false);
        placePourTarget();
    }

    void placePourTarget() {
        if (_pourTarget == null || _origins.Length == 0) return;
        _pourTarget.localPosition = new Vector3(
            (_origins[0].x + _minX) * _cellWorldSize,
            (_origins[0].y + _minY) * _cellWorldSize,
            0f);
        _pourTarget.localRotation = Quaternion.identity;
    }

    // Called by Shape once Container has handed over both the real cell size and the level's unlit
    // sand material. Safe to call again: it rebuilds the mask and leaves the current fill alone.
    public void configure(Transform visualRoot, IReadOnlyList<Vector2Int> canonicalCells, float cellWorldSize, Material unlitSource) {
        if (visualRoot == null || canonicalCells == null || canonicalCells.Count == 0 || unlitSource == null) return;
        if (cellWorldSize <= 0f) return;

        _cellWorldSize = cellWorldSize;
        buildMask(canonicalCells);
        ensureQuad(visualRoot, unlitSource);
        ensurePourTarget(visualRoot);
        _lastDrawnTexture = -1f;
        _built = true;
        redraw(Mathf.Max(0f, _drawn));
    }

    public void setColor(Color color) {
        _baseColor = color;
        _lastDrawnTexture = -1f;
        if (_built) redraw(Mathf.Max(0f, _drawn));
    }

    // What is actually on screen right now (the animated value, not the requested one). Read-only,
    // for tooling — nothing in the game reads it.
    public float drawnFill => Mathf.Max(0f, _drawn);

    // The only value this class takes from gameplay: Container.fillLevel, 0..1.
    public void setFill(float normalized) {
        _target = Mathf.Clamp01(normalized);
        // First value seen lands instantly, so a level that starts part-filled does not animate up
        // from zero on load.
        if (_drawn < 0f) _drawn = _target;
    }

    void Update() {
        if (!_built) return;

        if (!Mathf.Approximately(_drawn, _target)) {
            _drawn = Mathf.Lerp(_drawn, _target, 1f - Mathf.Exp(-FILL_SHARPNESS * Time.deltaTime));
            if (Mathf.Abs(_target - _drawn) < 0.0005f) _drawn = _target;
        }

        applyDepth(_drawn);
        if (Mathf.Abs(_drawn - _lastDrawnTexture) >= REBUILD_EPSILON || _lastDrawnTexture < 0f) redraw(_drawn);
    }

    // ---- geometry ----------------------------------------------------------------------------

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
        float sumX = 0f, sumY = 0f;
        for (int i = 0; i < cells.Count; i++) {
            _mask[(cells[i].x - _minX) + (cells[i].y - _minY) * _cellsWide] = true;
            sumX += cells[i].x - _minX;
            sumY += cells[i].y - _minY;
        }
        _centroidX = sumX / cells.Count;
        _centroidY = sumY / cells.Count;

        _texWidth = _cellsWide * PIXELS_PER_CELL;
        _texHeight = _cellsHigh * PIXELS_PER_CELL;

        recomputeOrigin();
        if (_texture != null) { Destroy(_texture); _texture = null; }
        _texture = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,     // same as the sand area: pixels, not a smear
            wrapMode = TextureWrapMode.Clamp
        };
        _pixels = new Color32[_texWidth * _texHeight];
        if (_material != null) _material.mainTexture = _texture;
    }

    // Pour point, and the radius that means "100%" measured from it. Default is the centroid of the
    // OCCUPIED cells, so on an L or a T the pile starts inside the piece rather than in the hole of
    // its bounding box; _pourOriginCell overrides it for the A/B test.
    void recomputeOrigin() {
        if (_pourOriginCells != null) {
            _origins = new Vector2[_pourOriginCells.Length];
            for (int i = 0; i < _pourOriginCells.Length; i++)
                _origins[i] = new Vector2(_pourOriginCells[i].x - _minX, _pourOriginCells[i].y - _minY);
        } else {
            _origins = new[] { new Vector2(_centroidX, _centroidY) };
        }
        _centerX = _origins[0].x;
        _centerY = _origins[0].y;
        placePourTarget();

        _maxRadius = 0f;
        for (int py = 0; py < _texHeight; py++) {
            for (int px = 0; px < _texWidth; px++) {
                if (!maskedAt(px, py)) continue;
                _maxRadius = Mathf.Max(_maxRadius, distanceToNearestOrigin(px, py));
            }
        }
        if (_maxRadius <= 0f) _maxRadius = 0.5f;
    }

    // Distance from a texel to the nearest pour point, in cells. With one origin this is exactly the
    // radial distance the class has always used; with two it is what makes both arms of a U fill.
    float distanceToNearestOrigin(int px, int py) {
        float x = (px + 0.5f) / PIXELS_PER_CELL - 0.5f;
        float y = (py + 0.5f) / PIXELS_PER_CELL - 0.5f;
        float best = float.MaxValue;
        for (int i = 0; i < _origins.Length; i++) {
            float dx = x - _origins[i].x, dy = y - _origins[i].y;
            float d = dx * dx + dy * dy;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    bool maskedAt(int px, int py) => _mask[(px / PIXELS_PER_CELL) + (py / PIXELS_PER_CELL) * _cellsWide];

    void ensureQuad(Transform visualRoot, Material unlitSource) {
        if (_quad == null) {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "SandFill";
            Destroy(go.GetComponent<Collider>());          // never a pick target — that is the cubes' job
            _quad = go.transform;
            _renderer = go.GetComponent<Renderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }
        _quad.SetParent(visualRoot, false);

        if (_material == null) {
            // A copy, never the asset: the level's sand material is shared by the sand quad and the
            // board floor. Alpha clip rather than blend keeps the hard pixel edge and correct depth.
            _material = new Material(unlitSource) { name = "SandFill (runtime)" };
            if (_material.HasProperty("_AlphaClip")) _material.SetFloat("_AlphaClip", 1f);
            if (_material.HasProperty("_Cutoff")) _material.SetFloat("_Cutoff", 0.5f);
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", Color.white);
            _material.EnableKeyword("_ALPHATEST_ON");
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            _renderer.sharedMaterial = _material;
        }
        _material.mainTexture = _texture;

        // Bounding box of the canonical cells, in Shape-local units. Cell (cx,cy) is centred on
        // (cx * cell, cy * cell), so the box runs half a cell past the outermost centres.
        _quad.localScale = new Vector3(_cellsWide * _cellWorldSize, _cellsHigh * _cellWorldSize, 1f);
        applyDepth(Mathf.Max(0f, _drawn));
    }

    void applyDepth(float fill) {
        if (_quad == null) return;
        float cx = (_minX + (_cellsWide - 1) * 0.5f) * _cellWorldSize;
        float cy = (_minY + (_cellsHigh - 1) * 0.5f) * _cellWorldSize;
        _quad.localPosition = new Vector3(cx, cy, Mathf.Lerp(Z_EMPTY, Z_FULL, Mathf.Clamp01(fill)));
        _quad.localRotation = Quaternion.identity;
    }

    // ---- the picture -------------------------------------------------------------------------

    void redraw(float fill) {
        if (_texture == null || _pixels == null) return;
        _lastDrawnTexture = fill;

        // Radius of the pile right now, in normalised units. The headroom above 1 is what guarantees
        // that at 100% every masked pixel is inside the pile even after the edge jitter is added.
        float radius = Mathf.Pow(Mathf.Clamp01(fill), SPREAD_EXPONENT) * (1f + EDGE_NOISE * 0.5f);
        bool empty = fill <= 0.0001f;
        float coreRadius = Mathf.Max(radius * CORE_FRACTION, 0.0001f);

        Color center = _baseColor * CENTER_GAIN;
        Color edge = _baseColor * EDGE_GAIN;

        for (int py = 0; py < _texHeight; py++) {
            for (int px = 0; px < _texWidth; px++) {
                int i = py * _texWidth + px;

                // The mask is absolute: outside it nothing is ever drawn, at any fill level.
                if (!maskedAt(px, py)) { _pixels[i] = new Color32(0, 0, 0, 0); continue; }
                if (empty) { _pixels[i] = new Color32(0, 0, 0, 0); continue; }

                float d = distanceToNearestOrigin(px, py) / _maxRadius;

                float n = Hash01(px, py);
                float jittered = d + (n - 0.5f) * EDGE_NOISE;
                if (jittered > radius) { _pixels[i] = new Color32(0, 0, 0, 0); continue; }

                // Dense and bright where the sand lands, thinning toward the edge of the pile. Keyed
                // to the CURRENT radius, so the dense core widens as the piece fills.
                float t = Mathf.Clamp01(jittered / coreRadius);
                Color c = Color.Lerp(center, edge, t);
                float mul = 1f + (Hash01(px + 31, py + 17) - 0.5f) * GRAIN_NOISE;

                _pixels[i] = new Color32(
                    (byte)Mathf.Clamp(c.r * 255f * mul, 0f, 255f),
                    (byte)Mathf.Clamp(c.g * 255f * mul, 0f, 255f),
                    (byte)Mathf.Clamp(c.b * 255f * mul, 0f, 255f),
                    255);
            }
        }
        _texture.SetPixels32(_pixels);
        _texture.Apply(false);
    }

    // Same hash SandCylinderRenderer uses, so the two grains come from one family.
    static float Hash01(int x, int y) {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    void OnDestroy() {
        if (_texture != null) Destroy(_texture);
        if (_material != null) Destroy(_material);
    }
}
