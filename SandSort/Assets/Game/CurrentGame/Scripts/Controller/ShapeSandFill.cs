using System.Collections.Generic;
using UnityEngine;

// 2D sand-fill visual for one Shape (2026-09-13). Deliberately NOT a simulation: it mimics a layer of
// sand filling the cavity from its floor toward the camera (Z-only since 2026-09-17, publisher
// feedback — it used to pile at the centroid and spread radially) — using the technique
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
// Rules the design is built around:
//   - occupiedCells is a HARD boundary. A pixel outside the mask is never written (alpha 0), so a
//     T4's or Plus5's empty cell cannot fill at any fill level, including 100%.
//   - The fill does NOT rise in Y. The surface travels in Z, from the back of the cavity toward the
//     camera, so the piece reads as filling up from the inside out.
//   - Depth is the ONLY progress axis. applyDepth moves the layer linearly with fill, and the cavity
//     has the same cross-section at every depth, so equal fill steps are equal volume steps on any
//     outline. The picture is therefore uniform over the whole mask — no level line, no origin, no
//     gradient across the piece — which also makes it rotation-invariant for free.
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

    // Brightness of the layer as it travels: a little shaded while it is deep in the cavity, brighter
    // as it reaches the rim, so the Z travel also reads as light. Kept at or above the base colour
    // (the 2026-09-13 lesson: this quad is unlit, and anything much below 1 reads darker than the lit
    // cavity around it). Uniform across the piece at any one fill.
    //
    // RIM_GAIN was 1.25 until 2026-09-18 and is now 1.10, to buy the grain headroom. The grain is a
    // multiplier applied BEFORE the byte clamp, so the gain and the grain compete for the same ceiling:
    // at 1.25 a saturated palette colour (red's R is 0.878) reached 1.05 of full before any grain was
    // added, and 56% of the interior texels had that channel pinned at 255 at fill 0.8. A pinned
    // channel cannot show the bright half of the grain, so the measured contrast fell as the piece
    // filled and bright grains lost saturation — the interior washed toward pink. The travel still
    // reads as light at 1.10; it is a smaller ramp, not a missing one.
    //
    // DEEP_GAIN was 1.00 and is now 0.92 (2026-09-18). Dropping RIM_GAIN to 1.10 bought the grain its
    // headroom but left the fill cue too weak to read: measured over a whole fill the piece's mean
    // luminance moved only 6.8%, and side by side 15%, 50% and 92% were not tellable apart without the
    // percentage label. The ramp cannot be reopened from the top without putting the clipping back, so
    // it is opened from the BOTTOM instead — empty sand starts darker rather than full sand ending
    // brighter, which costs no headroom at all.
    //
    // This deliberately breaks the 2026-09-13 rule quoted above. That rule was about the fill reading
    // darker than the lit cavity around it, so 0.92 is a measured risk, not a free change: 8% down is
    // meant to stay inside "shaded sand deep in the cavity" and not tip into "wrong colour". If a
    // low-fill piece ever reads as a different, muddier colour rather than a dimmer one, this is why.
    const float DEEP_GAIN = 0.92f;
    const float RIM_GAIN = 1.10f;

    // Per-pixel brightness jitter. Same formula as SandCylinderTunables.colorNoiseAmount, but its own
    // amount (2026-09-17): the sand area's 0.14 reads as grain at a piece's much smaller on-screen
    // size, while a little jitter is still what stops the fill looking like flat, shiny cloth.
    // Tunable: GameplayTunables.sandFillColorNoiseAmount, read live (see Update). Applied per texel,
    // so it is orthogonal to the centre-to-wall edge darken below and the two multiply together. It
    // sets how far apart two neighbouring grains can be, and therefore how much a re-roll shows: it is
    // the CONTRAST of the movement described below, not just surface texture.
    //
    // GRAIN CHURN (2026-09-18, fourth model, taken from the reference game). A grain pattern that never
    // changes makes a filling piece read as one sticker being pushed forward rather than as sand
    // arriving. Three models were tried before this one and all three put the movement in the wrong
    // place, so the record is worth keeping:
    //
    //   1. Re-seeding the per-texel jitter in fill steps. Dismissed as invisible, with the argument
    //      that re-seeding white noise gives white noise of the same mean, variance and spatial
    //      frequency — "the same picture". THAT ARGUMENT WAS WRONG, and it is the reason two further
    //      models were built on a false premise. It holds only for LOW-CONTRAST, FINE noise, where the
    //      eye averages the grain away and never resolves a single texel. Measured on the reference
    //      game, its grains are resolvable (about 28 across a cell) and carry 13% brightness spread,
    //      and at that contrast a re-roll is not invisible at all — it is the entire effect.
    //   2. A coarse field driven by a smooth phase, D(x,f) = S(phase(x) + w*f). Reads as liquid, and
    //      provably so: its iso-value contours travel at -w * grad(phase) / |grad(phase)|^2, so a
    //      smooth phase field IS a velocity field, everywhere, and that is a flow.
    //   3. Sparse local mounds, each growing and spreading in place. No flow (verified: the best
    //      matching shift between fills was exactly (0,0)), but still wrong, because it invents a
    //      low-frequency SHAPE that the reference does not have anywhere.
    //
    // What the reference actually does, measured frame by frame:
    //   - No level line, no gradient, no mounds. Once past the first fifth of the fill, the sand is a
    //     uniform granular mass over the whole cavity, and fill shows as BRIGHTNESS (Z gain here).
    //   - While sand is arriving the whole mass churns: about 23% of grains change shade per update,
    //     and the field fully decorrelates in roughly 8 updates.
    //   - The grains do not MOVE. They change shade in place — which is what a real sand surface does
    //     when grains land on it: the mass stays put and which grain is on top keeps changing.
    //   - When no sand is arriving the mass is perfectly static. Measured on a settled pile: frame to
    //     frame correlation 0.9946, i.e. not a pixel moves. The churn is caused by the pour, not by a
    //     clock, and that causal link is what makes the eye read it as sand being poured.
    //
    // So the movement lives in the GRAIN, at the highest spatial frequency, and its clock is the fill:
    //
    //   roll(x,y) = floor(fill * CHURN_RATE + phase(x,y))     phase is a static per-texel hash
    //   grain     = Hash01(x + 31, y + 17, roll)
    //
    // phase staggers the re-rolls so grains turn over independently instead of the whole texture
    // flashing at once. Everything falls out of roll being a function of fill alone:
    //   - deterministic: same fill, same roll, same hash, same picture. No time input anywhere.
    //   - frozen when the fill is: roll cannot advance, so the texture is byte-identical.
    //   - nothing translates, so scroll, wave and flow are not merely tuned out, they are unexpressible.
    //
    // NOTE, deliberately breaking the rule the previous two models were built on: a grain flips shade
    // DISCONTINUOUSLY, and that is correct. The C1 / no-pop requirement applied to a low-frequency
    // field, where a step reads as a band or a flash. At grain level the discreteness IS the sand.
    //
    //   CHURN_RATE  re-rolls per grain over a full fill. With REBUILD_EPSILON at 1/160 the texture is
    //               rebuilt 160 times over a full fill, so the share of grains that re-roll between
    //               two rebuilds is CHURN_RATE/160 — at 37 that is 23%, the reference's own figure.
    //               This is a ceiling rather than a target: well above it the surface boils like
    //               static. It is kept as its own constant so the churn can be tuned on its own.
    const float CHURN_RATE = 37f;

    // Settling-in: below this fill the floor is only partly covered, by grains scattered evenly over
    // the WHOLE mask (a per-texel hash threshold), so the first sand appears everywhere at once rather
    // than as a solid sheet popping in. Above it the layer is solid and only depth moves.
    const float COVER_FILL = 0.12f;

    // The Shape FBXs round every CONVEX outer corner — a cell corner whose two orthogonal neighbours
    // are both empty — with a 0.080 radius at the authored 0.85 cell, measured from the meshes'
    // outer wall (identical on 1x1, 2x2, L4 and T4, and at every depth the fill travels through).
    // Straight edges sit exactly on the cell boundary. The mask cuts the same arcs so the layer never
    // shows past the piece's silhouette. Concave joins are filleted OUTWARD by the mesh (material
    // added into the empty cell), which a cell-bounded mask can never cross, so they need nothing.
    // Kept as a fraction of a cell: the meshes are authored in cell units.
    const float CORNER_RADIUS_CELLS = 0.080f / 0.85f;

    // Soft depth shading in XY (2026-09-17): the sand keeps the piece's colour in the middle and darkens
    // very slightly toward the piece's walls, so it reads as a layer lying INSIDE the piece rather than
    // a flat sticker. Distance is measured from the real silhouette (_texelMask — rounded corners and
    // L/T/U outlines included, internal cell joins are not edges), so the darkening follows the walls.
    //   edge darken        darkest multiplier drop, reached at the visible wall line (1 - 0.10 = 0.90).
    //                      Tunable: GameplayTunables.sandFillEdgeDarken, read live (see Update).
    //   WALL_INSET_CELLS   the FBX walls are 0.110 thick at the authored 0.85 cell (outer edge ±0.425,
    //                      inner opening ±0.315 — measured on 1x1, 2x2, L4, T4 and U5, identical), so
    //                      that outer band of sand is never seen; the falloff starts at the inner line.
    //   FALLOFF_CELLS      distance from that line over which the darkening eases back to none. At 0.45
    //                      a 1x1's centre (0.37 cells in) is within 1% of the base colour.
    const float WALL_INSET_CELLS = 0.110f / 0.85f;
    const float FALLOFF_CELLS = 0.45f;

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
    bool[] _texelMask;   // _mask per texel, with the convex corners rounded off
    float[] _edgeFalloff; // per-texel 1 - smoothstep(distance): 1 at the visible wall line, 0 inside. Darken-independent.
    float[] _edgeShade;   // per-texel colour multiplier, 1 - edgeDarken * _edgeFalloff

    // Per-texel re-roll offset, 0..1. Static, built with the mask: it is what staggers the grains so
    // they turn over independently rather than the whole texture flashing on one fill step.
    float[] _grainPhase;

    // Source of the edge darken amount; null = GameplayTunables' default. _edgeDarken is the value the
    // current _edgeShade was built with, so a change in the Inspector is noticed and applied in Update.
    GameplayTunables _tuning;
    float _edgeDarken = GameplayTunables.DEFAULT_SAND_FILL_EDGE_DARKEN;
    float _colorNoise = GameplayTunables.DEFAULT_SAND_FILL_COLOR_NOISE_AMOUNT;

    float tunedEdgeDarken => _tuning != null ? _tuning.sandFillEdgeDarken : GameplayTunables.DEFAULT_SAND_FILL_EDGE_DARKEN;
    float tunedColorNoise => _tuning != null ? _tuning.sandFillColorNoiseAmount : GameplayTunables.DEFAULT_SAND_FILL_COLOR_NOISE_AMOUNT;

    public void setTuning(GameplayTunables tuning) {
        _tuning = tuning;
        _edgeDarken = tunedEdgeDarken;
        _colorNoise = tunedColorNoise;
        if (!_built) return;
        applyEdgeDarken();
        _lastDrawnTexture = -1f;
        redraw(Mathf.Max(0f, _drawn));
    }
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

        // Live Inspector tuning: only the multiplier table is rebuilt (no distance transform), then one redraw.
        float edgeDarken = tunedEdgeDarken;
        if (edgeDarken != _edgeDarken) {
            _edgeDarken = edgeDarken;
            applyEdgeDarken();
            _lastDrawnTexture = -1f;
        }

        // The grain has no table — it is evaluated per texel in redraw — so a change just forces one.
        float colorNoise = tunedColorNoise;
        if (colorNoise != _colorNoise) {
            _colorNoise = colorNoise;
            _lastDrawnTexture = -1f;
        }

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
        buildTexelMask();
        buildEdgeShade();
        buildGrainPhase();

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

    bool maskedAt(int px, int py) => _texelMask[px + py * _texWidth];

    // Euclidean distance from every masked texel to the nearest texel OUTSIDE the silhouette (the
    // texture border counts as outside), via two-pass vector propagation (8SSEDT) — near exact, O(N),
    // run once per configure, never per frame. Stored as the darken-independent falloff, then turned
    // into the colour multiplier by applyEdgeDarken.
    void buildEdgeShade() {
        int w = _texWidth + 2, h = _texHeight + 2;   // one texel of guaranteed "outside" on every side
        const int FAR = 1 << 14;
        var ox = new int[w * h];
        var oy = new int[w * h];
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                bool inside = x > 0 && y > 0 && x < w - 1 && y < h - 1 && _texelMask[(x - 1) + (y - 1) * _texWidth];
                int i = x + y * w;
                ox[i] = inside ? FAR : 0;
                oy[i] = inside ? FAR : 0;
            }
        }

        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                relax(ox, oy, w, h, x, y, -1, 0);
                relax(ox, oy, w, h, x, y, 0, -1);
                relax(ox, oy, w, h, x, y, -1, -1);
                relax(ox, oy, w, h, x, y, 1, -1);
            }
            for (int x = w - 1; x >= 0; x--) relax(ox, oy, w, h, x, y, 1, 0);
        }
        for (int y = h - 1; y >= 0; y--) {
            for (int x = w - 1; x >= 0; x--) {
                relax(ox, oy, w, h, x, y, 1, 0);
                relax(ox, oy, w, h, x, y, 0, 1);
                relax(ox, oy, w, h, x, y, -1, 1);
                relax(ox, oy, w, h, x, y, 1, 1);
            }
            for (int x = 0; x < w; x++) relax(ox, oy, w, h, x, y, -1, 0);
        }

        _edgeFalloff = new float[_texWidth * _texHeight];
        for (int py = 0; py < _texHeight; py++) {
            for (int px = 0; px < _texWidth; px++) {
                int i = (px + 1) + (py + 1) * w;
                // Centre-to-centre distance less half a texel = distance to the silhouette edge.
                float edgeCells = (Mathf.Sqrt((float)ox[i] * ox[i] + (float)oy[i] * oy[i]) - 0.5f) / PIXELS_PER_CELL;
                float t = Mathf.Clamp01((edgeCells - WALL_INSET_CELLS) / FALLOFF_CELLS);
                _edgeFalloff[px + py * _texWidth] = 1f - Mathf.SmoothStep(0f, 1f, t);
            }
        }
        applyEdgeDarken();
    }

    // ---- grain churn ---------------------------------------------------------------------------

    // The static per-texel re-roll offset. Without it every grain would tick over on the same fill
    // values and the whole texture would flash together; with it the turnover is spread evenly, so at
    // any one rebuild a slice of the grains has changed and the rest is exactly as it was. Built once
    // with the mask — this never depends on the fill.
    void buildGrainPhase() {
        _grainPhase = new float[_texWidth * _texHeight];
        for (int py = 0; py < _texHeight; py++)
            for (int px = 0; px < _texWidth; px++)
                _grainPhase[px + py * _texWidth] = Hash01(px * 5 + 3, py * 9 + 11);
    }

    void applyEdgeDarken() {
        if (_edgeFalloff == null) return;
        if (_edgeShade == null || _edgeShade.Length != _edgeFalloff.Length) _edgeShade = new float[_edgeFalloff.Length];
        for (int i = 0; i < _edgeFalloff.Length; i++) _edgeShade[i] = 1f - _edgeDarken * _edgeFalloff[i];
    }

    // Takes the neighbour's nearest-outside offset, extended by the step to it, if that is closer.
    static void relax(int[] ox, int[] oy, int w, int h, int x, int y, int dx, int dy) {
        int nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
        int n = nx + ny * w;
        if (ox[n] >= (1 << 14)) return;
        // The neighbour's seed is at neighbour + offset = this texel + (step + offset).
        int cx = ox[n] + dx, cy = oy[n] + dy;
        int i = x + y * w;
        if ((long)cx * cx + (long)cy * cy < (long)ox[i] * ox[i] + (long)oy[i] * oy[i]) {
            ox[i] = cx;
            oy[i] = cy;
        }
    }

    bool cellOccupied(int cx, int cy) =>
        cx >= 0 && cy >= 0 && cx < _cellsWide && cy < _cellsHigh && _mask[cx + cy * _cellsWide];

    // A texel is kept only if it lies WHOLLY inside the rounded outline: the test uses the texel's
    // corner furthest from the arc centre, so no part of a texel can poke past the silhouette.
    void buildTexelMask() {
        _texelMask = new bool[_texWidth * _texHeight];
        float r = CORNER_RADIUS_CELLS;
        float r2 = r * r;
        for (int cy = 0; cy < _cellsHigh; cy++) {
            for (int cx = 0; cx < _cellsWide; cx++) {
                if (!_mask[cx + cy * _cellsWide]) continue;
                bool left = !cellOccupied(cx - 1, cy), right = !cellOccupied(cx + 1, cy);
                bool down = !cellOccupied(cx, cy - 1), up = !cellOccupied(cx, cy + 1);

                for (int ty = 0; ty < PIXELS_PER_CELL; ty++) {
                    for (int tx = 0; tx < PIXELS_PER_CELL; tx++) {
                        // Texel edges in cell units, 0..1 across this cell.
                        float x0 = (float)tx / PIXELS_PER_CELL, x1 = (float)(tx + 1) / PIXELS_PER_CELL;
                        float y0 = (float)ty / PIXELS_PER_CELL, y1 = (float)(ty + 1) / PIXELS_PER_CELL;
                        // Overshoot past each corner's arc centre, toward that corner (>0 = inside the corner square).
                        float dxL = r - x0, dxR = x1 - (1f - r);
                        float dyD = r - y0, dyU = y1 - (1f - r);

                        bool cut =
                            (left  && down && dxL > 0f && dyD > 0f && dxL * dxL + dyD * dyD > r2) ||
                            (right && down && dxR > 0f && dyD > 0f && dxR * dxR + dyD * dyD > r2) ||
                            (left  && up   && dxL > 0f && dyU > 0f && dxL * dxL + dyU * dyU > r2) ||
                            (right && up   && dxR > 0f && dyU > 0f && dxR * dxR + dyU * dyU > r2);

                        int px = cx * PIXELS_PER_CELL + tx, py = cy * PIXELS_PER_CELL + ty;
                        _texelMask[px + py * _texWidth] = !cut;
                    }
                }
            }
        }
    }

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

        fill = Mathf.Clamp01(fill);
        bool empty = fill <= 0.0001f;

        // Fraction of the floor covered (1 once settled), and the one colour for the whole layer.
        float coverage = Mathf.Clamp01(fill / COVER_FILL);
        Color c = _baseColor * Mathf.Lerp(DEEP_GAIN, RIM_GAIN, fill);

        // Where every grain is in its own re-roll sequence at this fill. Adding the static per-texel
        // phase before the floor is what staggers them; the fill is the only clock, so this stops dead
        // — every grain frozen on the shade it is showing — the moment the fill does.
        float churn = fill * CHURN_RATE;

        for (int py = 0; py < _texHeight; py++) {
            for (int px = 0; px < _texWidth; px++) {
                int i = py * _texWidth + px;

                // The mask is absolute: outside it nothing is ever drawn, at any fill level.
                if (!maskedAt(px, py)) { _pixels[i] = new Color32(0, 0, 0, 0); continue; }
                if (empty) { _pixels[i] = new Color32(0, 0, 0, 0); continue; }

                // Evenly scattered grains while settling; solid once coverage reaches 1.
                if (coverage < 1f && Hash01(px, py) >= coverage) { _pixels[i] = new Color32(0, 0, 0, 0); continue; }

                // The grain, and the whole of the movement. Each texel re-rolls its shade every time
                // its own roll index ticks over; between ticks it is bit-for-bit the shade it had.
                int roll = Mathf.FloorToInt(churn + _grainPhase[i]);
                float mul = 1f + (Hash01(px + 31, py + 17, roll) - 0.5f) * _colorNoise;

                // Shading goes on AFTER the clamp: RIM_GAIN pushes bright colours (yellow's R) past
                // 255, and a multiplier applied before the clamp would be clipped away. The edge
                // darken term is untouched: same table, same value, same slot as it has always had.
                float shade = _edgeShade[i];
                _pixels[i] = new Color32(
                    channel(c.r, mul, shade),
                    channel(c.g, mul, shade),
                    channel(c.b, mul, shade),
                    255);
            }
        }
        _texture.SetPixels32(_pixels);
        _texture.Apply(false);
    }

    // One channel: base colour and grain, clamped to the byte range, then the post-clamp shading
    // (edge darken x local depth). The second clamp is not decoration — the depth field's DC
    // correction puts `shade` a few percent above 1 on the shallowest texels, and an unchecked byte
    // cast would WRAP a 255 channel round to near 0.
    static byte channel(float value, float mul, float shade) =>
        (byte)Mathf.Clamp(Mathf.Clamp(value * 255f * mul, 0f, 255f) * shade, 0f, 255f);

    // Same hash SandCylinderRenderer uses, so the two grains come from one family.
    static float Hash01(int x, int y) {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    // The same hash with the re-roll index folded in, so one texel walks an unrelated sequence of
    // shades as its roll ticks over. The third coefficient is far enough from the other two that
    // stepping roll by one moves the sine's argument by a large, non-commensurate amount — checked for
    // visible structure rather than assumed, since sine hashes on linear integer arguments can moire.
    static float Hash01(int x, int y, int z) {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f + z * 37.719f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    void OnDestroy() {
        if (_texture != null) Destroy(_texture);
        if (_material != null) Destroy(_material);
    }
}
