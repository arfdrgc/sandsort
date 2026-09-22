using UnityEngine;

// Single Inspector panel for the GAMEPLAY feel/balance knobs — shape movement, drag, collision,
// snapping, extraction thresholds. One asset, assigned on the Level prefab, so these can be tuned
// without touching code.
//
// DELIBERATELY SEPARATE FROM THE SAND (2026-09-12). SandCylinderDemo's sand simulation is meant to
// stay reusable on its own: drop SandCylinderDemo/ into another project and it must compile and run
// without a single gameplay class or gameplay asset coming along. So:
//  - SandCylinderTunables (a scene component, not this asset) stays sand physics ONLY: sand area
//    size, density, grid blocks, colors, conveyor/extraction rates. Nothing about shapes or the
//    board belongs there.
//  - This asset stays gameplay ONLY. It must never reference a SandCylinder* type, and no
//    SandCylinderDemo script may ever reference it.
// The dependency runs one way and only through the gameplay-side interaction layer: ExtractionGrid
// and Level call INTO the sand system, never the reverse.
[CreateAssetMenu(fileName = "GameplayTunables", menuName = "_ScriptableObjects/GameplayTunables", order = 3)]
public class GameplayTunables : ScriptableObject {

    // Used when the Level prefab has no asset assigned, so one missing reference can't silently
    // change how the game feels. Kept here rather than in the gameplay classes so each number has a
    // single home.
    public const float DEFAULT_EXTRACTION_ARRIVAL_TOLERANCE_CELLS = 0.035f;
    public const float DEFAULT_GRID_SAND_GAP_CELLS = 0.5f;
    public const float DEFAULT_DRAG_FOLLOW_SHARPNESS = 30f;
    public const float DEFAULT_DRAG_MAX_SPEED_CELLS = 40f;
    public const float DEFAULT_RELEASE_SNAP_SHARPNESS = 22f;
    public const float DEFAULT_RELEASE_SNAP_MIN_SPEED_CELLS = 4f;
    public const float DEFAULT_COMPLETE_GROW_SCALE = 1.08f;
    public const float DEFAULT_COMPLETE_GROW_DURATION = 0.08f;
    public const float DEFAULT_COMPLETE_SCALE_OUT_DURATION = 0.18f;
    public const float DEFAULT_COMPLETE_EFFECT_SCALE = 0.3f;
    public const float DEFAULT_SHAPE_COMPLETE_PARTICLE_WAIT_MULTIPLIER = 1.15f;
    public const float DEFAULT_SAND_FILL_EDGE_DARKEN = 0.10f;
    public const float DEFAULT_SAND_FILL_COLOR_NOISE_AMOUNT = 0.20f;
    public const float DEFAULT_SAND_FILL_DEPTH_AMOUNT = 0.14f;
    [Header("Drag")]
    [Tooltip("How hard the shape is pulled toward the pointer, per second. The visual catches up exponentially, so this is really a time constant: 1/sharpness is roughly the lag (30 => ~33 ms). Higher feels locked to the finger, lower feels heavy and smooths out a shaky pointer.")]
    [Range(5f, 60f)]
    [SerializeField] float _dragFollowSharpness = DEFAULT_DRAG_FOLLOW_SHARPNESS;
    [Tooltip("Speed cap while dragging, in cells per second. Keeps a pointer JUMP (alt-tab, a teleporting touch) reading as fast motion instead of a teleport. High enough that normal dragging never hits it.")]
    [Range(5f, 80f)]
    [SerializeField] float _dragMaxSpeedCells = DEFAULT_DRAG_MAX_SPEED_CELLS;

    [Header("Release Snap")]
    [Tooltip("How hard the shape is pulled onto its committed cell after release, per second — the only snap in the game. Same exponential shape as Drag Follow Sharpness: higher snaps faster.")]
    [Range(5f, 60f)]
    [SerializeField] float _releaseSnapSharpness = DEFAULT_RELEASE_SNAP_SHARPNESS;
    [Tooltip("Floor on the release snap speed, in cells per second, so the last fraction of a cell does not crawl in (an exponential alone never quite arrives).")]
    [Range(0.5f, 20f)]
    [SerializeField] float _releaseSnapMinSpeedCells = DEFAULT_RELEASE_SNAP_MIN_SPEED_CELLS;
    public float dragFollowSharpness => _dragFollowSharpness;
    public float dragMaxSpeedCells => _dragMaxSpeedCells;
    public float releaseSnapSharpness => _releaseSnapSharpness;
    public float releaseSnapMinSpeedCells => _releaseSnapMinSpeedCells;

    [Header("Extraction")]
    [Tooltip("How close to the top row a shape's cell must be DRAWN before it may extract, in cells. Container.gridPosition rounds to the nearest cell, so it reads as \"top row\" while the shape is still drawn up to half a cell short of it — without this gate sand pours across a visible gap. Keep it small: large values re-introduce that gap, and 0 risks a shape held just under the row never starting at all.")]
    [Range(0f, 0.25f)]
    [SerializeField] float _extractionArrivalToleranceCells = DEFAULT_EXTRACTION_ARRIVAL_TOLERANCE_CELLS;
    public float extractionArrivalToleranceCells => _extractionArrivalToleranceCells;

    [Header("Board Placement")]
    [Tooltip("Empty space between the board's top edge and the sand area's bottom edge, measured in CELLS: 0.5 = half a cell, 0.25 = a quarter, 0 = the board's top edge sits flush against the sand. The cell's world size still comes from the sand (SandCylinderTunables.CubeWorldSize), so this stays a pure gameplay/layout knob and board and sand can never drift out of scale. Read by Level.buildBoard at level build time only — changing it during Play does nothing until the level is rebuilt. PURELY VISUAL: extraction does not depend on it. ExtractionGrid anchors its vertical extraction band to the sand's own bottom edge, so the sand a shape can reach is the same at every gap — see ExtractionGrid's EXTRACTION BAND note. The 1-cell cap is just to keep the board from drifting absurdly far from the sand.")]
    [Range(0f, 1f)]
    [SerializeField] float _gridSandGapCells = DEFAULT_GRID_SAND_GAP_CELLS;
    public float gridSandGapCells => _gridSandGapCells;

    [Header("Selection")]
    [Tooltip("Outline shown on the shape the player is holding (from press until release). A material on Toony Colors Pro's 'Hybrid Shader 2 (Outline)' shader with every pass except 'Outline' disabled, added to the piece while it is held; Moow_Renderer's RenderObjects feature draws that pass. Colour = _OutlineColor, width = _OutlineWidth (pixels). The piece's own colour material is never replaced or modified. Being a serialized asset also keeps its shader variant in device builds. Empty = no outline.")]
    [SerializeField] Material _selectedOutlineMaterial;
    [Tooltip("Stencil mask for that outline: the same shader with only its Main pass on, blending Zero/One and no depth write (draws no colour), queue 2001 so it runs after the piece itself. It writes the stencil value the Outline pass tests against, which keeps the outline outside the piece's silhouette instead of also drawing lines inside the hollow shape.")]
    [SerializeField] Material _selectedOutlineMaskMaterial;
    public Material selectedOutlineMaterial => _selectedOutlineMaterial;
    public Material selectedOutlineMaskMaterial => _selectedOutlineMaskMaterial;

    [Header("Shape Complete")]
    [Tooltip("How long a completed shape waits before its exit animation, as a multiple of the sand's particleLifetime (SandCylinderTunables, read from the Level). Grain flights are jittered up to x1.15, so 1.15 waits exactly until the last grain aimed at the shape can have landed. Lower starts the swell sooner but may cut grains still in flight.")]
    [Range(0f, 3f)]
    [SerializeField] float _shapeCompleteParticleWaitMultiplier = DEFAULT_SHAPE_COMPLETE_PARTICLE_WAIT_MULTIPLIER;
    public float shapeCompleteParticleWaitMultiplier => _shapeCompleteParticleWaitMultiplier;
    [Tooltip("How much a completed shape swells before it scales out, as a multiple of its own size (1.08 = 8% bigger). The swell starts once the last extraction grain aimed at the shape has landed.")]
    [Range(1f, 1.5f)]
    [SerializeField] float _completeGrowScale = DEFAULT_COMPLETE_GROW_SCALE;
    [Tooltip("Duration of that swell, in seconds.")]
    [Range(0.01f, 1f)]
    [SerializeField] float _completeGrowDuration = DEFAULT_COMPLETE_GROW_DURATION;
    [Tooltip("Duration of the scale-out from the swollen size down to 0, in seconds.")]
    [Range(0.01f, 1f)]
    [SerializeField] float _completeScaleOutDuration = DEFAULT_COMPLETE_SCALE_OUT_DURATION;
    [Tooltip("Particle effect played where the shape was, once it has scaled out (EpicToonFX StarExplosion). The shape is disabled when this effect has fully finished. Empty = no effect, the shape is disabled right after scaling out.")]
    [SerializeField] GameObject _completeEffectPrefab;
    [Tooltip("Size of that effect relative to the shape: the effect's scale is this value times the shape's larger world-space side. The prefab asset itself is never modified.")]
    [Range(0.01f, 2f)]
    [SerializeField] float _completeEffectScale = DEFAULT_COMPLETE_EFFECT_SCALE;
    public float completeGrowScale => _completeGrowScale;
    public float completeGrowDuration => _completeGrowDuration;
    public float completeScaleOutDuration => _completeScaleOutDuration;
    public GameObject completeEffectPrefab => _completeEffectPrefab;
    public float completeEffectScale => _completeEffectScale;

    [Header("Shape Sand Fill")]
    [Tooltip("How much the sand inside a shape darkens toward the shape's walls: 0.10 = the sand at the visible wall line is 10% darker than at the centre, easing back to the full shape colour over about half a cell. 0 = flat colour. Keep it small — larger values start to read as an outline. Read live by ShapeSandFill, so changes show during Play.")]
    [Range(0f, 0.3f)]
    [SerializeField] float _sandFillEdgeDarken = DEFAULT_SAND_FILL_EDGE_DARKEN;
    public float sandFillEdgeDarken => _sandFillEdgeDarken;
    [Tooltip("Per-pixel brightness jitter on the sand inside a shape, as a peak-to-peak fraction: 0.20 = each pixel is randomly up to 10% brighter or darker than its neighbour, which breaks up the flat, cloth-like sheen and gives the fill a sandy surface. 0 = perfectly flat colour. This is FIXED grain — it never changes as the shape fills, and it is what makes the surface read as sand rather than as paint. The MOVEMENT while sand arrives is Sand Fill Depth Amount's job, not this one. Independent of Sand Fill Edge Darken; all three are applied together. Read live by ShapeSandFill, so changes show during Play.")]
    [Range(0f, 0.5f)]
    [SerializeField] float _sandFillColorNoiseAmount = DEFAULT_SAND_FILL_COLOR_NOISE_AMOUNT;
    public float sandFillColorNoiseAmount => _sandFillColorNoiseAmount;
    [Tooltip("How strongly the sand inside a shape shows LOCAL DEPTH — soft patches roughly half a cell across that each sit at one of 6 depth levels and walk 1->2->...->6 and back to 1 as the shape fills, every patch on its own phase, so the surface reads as sand piling up and re-settling in places rather than as one sticker sliding forward. 0.14 = the deepest patch is 14% darker than the shallowest. 0 = off. Driven ONLY by the fill level, so it is frozen whenever the fill is and the same fill always draws the same picture. The shape's average colour is held constant whatever this is set to (ShapeSandFill re-centres the field every redraw), so raising it adds contrast, never overall darkness. Read live by ShapeSandFill, so changes show during Play.")]
    [Range(0f, 0.4f)]
    [SerializeField] float _sandFillDepthAmount = DEFAULT_SAND_FILL_DEPTH_AMOUNT;
    public float sandFillDepthAmount => _sandFillDepthAmount;

    // The source material for the masked Shape quads (ShapeCavityFloor, ShapeSandFill). It must be a
    // URP Unlit material AUTHORED with alpha clipping on (_ALPHATEST_ON): _ALPHATEST_ON is a
    // shader_feature, so a player build only keeps that variant if a material in the build uses it.
    // Enabling it on a runtime copy of the opaque sand material worked in the Editor but was stripped
    // on Android, where every masked-out texel then drew as opaque black. Referenced here because
    // Level.prefab already ships this asset. Null = fall back to the level's sand material.
    [Tooltip("URP Unlit material with Alpha Clipping enabled, used as the source for the shape cavity floor and sand-fill quads. Must stay alpha-clipped: the Android build only keeps the alpha-test shader variant because this material uses it.")]
    [SerializeField] Material _shapeMaskMaterial;
    public Material shapeMaskMaterial => _shapeMaskMaterial;
}
